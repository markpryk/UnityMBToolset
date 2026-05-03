using System;
using System.Collections.Generic;
using System.Linq;
using MountAndBlade.Data;
using UnityEditor;
using UnityEngine;

namespace MountAndBlade.ModdingToolkit
{
    /// <summary>
    /// Resolves the correct M&B shader variant and Warband mode from BRF material + shader data.
    /// Uses the same logic as OpenBRF's shader selection:
    ///   1. Check BrfShader technique name for known patterns
    ///   2. Check if material has specular map -> NM_SHINE
    ///   3. Check if shader name contains "iron" -> NM_IRON
    ///   4. Check material flags for alpha test/blend -> NM_ALPHA / Transparent
    ///   5. Default -> NM_PLAIN
    /// </summary>
    public static class MBShaderResolver
    {
        /// <summary>
        /// Result of shader resolution containing shader name and properties to set.
        /// </summary>
        public struct ShaderSetup
        {
            public string ShaderName;       // Shader_Standard, Shader_Cutout, etc.
            public int WarbandMode;         // 0=Plain, 1=Iron, 2=Shine, 3=Preshaded
            public string WarbandModeName;  // For debugging
            public bool UseVertexColors;
            public bool UseAGnm;
            public bool UseRGBnm;
            public bool UseEnvMap;
            public bool IsParticleShader;   // True for soft_particle_*/soft_sunflare techniques
        }

        // Shader path constants
        public const string Shader_Standard    = "M&B/M&B_Standard";
        public const string Shader_Cutout      = "M&B/M&B_Cutout";
        public const string Shader_Transparent = "M&B/M&B_Transparent";
        public const string Shader_Additive    = "M&B/M&B_Additive";

        // Particle shader paths (GPU instanced StructuredBuffer shaders)
        public const string Shader_Particle_AlphaBlend = "M&B/Particles/AlphaBlend";
        public const string Shader_Particle_Additive   = "M&B/Particles/Additive";
        public const string Shader_Particle_SunFlare   = "M&B/Particles/SunFlare";

        // Known technique patterns from mb.fx and common Warband mods
        static readonly Dictionary<string, (string shader, int mode)> TechniqueMap = new(StringComparer.OrdinalIgnoreCase)
        {
            // These map to GPU-instanced particle shaders, NOT material shaders
            { "soft_particle_modulate",  (Shader_Particle_AlphaBlend, 0) },
            { "soft_particle_add",       (Shader_Particle_Additive, 0) },
            { "soft_sunflare",           (Shader_Particle_SunFlare, 0) },

            // Particle variants seen in mods
            { "soft_particle_alpha",     (Shader_Particle_AlphaBlend, 0) },
            { "particle_add",            (Shader_Particle_Additive, 0) },
            { "particle_modulate",       (Shader_Particle_AlphaBlend, 0) },
            { "particle_blend",          (Shader_Particle_AlphaBlend, 0) },
            { "sunflare",               (Shader_Particle_SunFlare, 0) },

            { "normap",                (Shader_Standard, 0) },
            { "normap_specular",       (Shader_Standard, 2) },
            { "normap_iron",           (Shader_Standard, 1) },
            { "normap_alpha",          (Shader_Cutout, 0) },
            { "normap_fade",           (Shader_Transparent, 0) },

            // Flora / vegetation
            { "flora",                 (Shader_Cutout, 0) },
            { "flora_normap",          (Shader_Cutout, 0) },

            // Preshaded / vertex lit
            { "preshaded",             (Shader_Standard, 3) },
            { "normap_preshaded",      (Shader_Standard, 3) },

            // Additive / glow (material, not particle)
            { "normap_additive",       (Shader_Additive, 0) },
            { "glow",                  (Shader_Additive, 0) },

            // Hair
            { "normap_hair",           (Shader_Cutout, 0) },
            { "hair",                  (Shader_Cutout, 0) },

            // Skin
            { "normap_skin",           (Shader_Standard, 0) },
            { "skin",                  (Shader_Standard, 0) },

            // Face
            { "face",                  (Shader_Standard, 0) },
            { "normap_face",           (Shader_Standard, 0) },

            // Terrain
            { "normap_terrain",        (Shader_Standard, 0) },

            // Enviro / reflective
            { "normap_envmap",         (Shader_Standard, 0) },
            { "envmap",                (Shader_Standard, 0) },
        };

        /// <summary>
        /// Resolve shader setup from BRF material and optional shader data.
        /// This is the main entry point - call this from BrfDataImporter.CreateMaterial.
        /// </summary>
        public static ShaderSetup Resolve(BrfMaterial brfMat, BrfShader brfShader = null)
        {
            var setup = new ShaderSetup
            {
                ShaderName = Shader_Standard,
                WarbandMode = 0,
                WarbandModeName = "Plain",
                UseVertexColors = false,
                UseAGnm = false,       // Warband default
                UseRGBnm = true,       // Warband default
                UseEnvMap = false,
                IsParticleShader = false
            };

            string technique = brfShader?.technique?.ToLowerInvariant() ?? "";
            string shaderName = brfMat.shader?.ToLowerInvariant() ?? "";

            // 1. Try exact technique match first
            if (!string.IsNullOrEmpty(technique) && TechniqueMap.TryGetValue(technique, out var exactMatch))
            {
                setup.ShaderName = exactMatch.shader;
                setup.WarbandMode = exactMatch.mode;
            }
            // 2. Try partial technique match
            else if (!string.IsNullOrEmpty(technique))
            {
                setup = ResolveTechniquePartial(technique, setup);
            }
            // 3. Also check shader name for particle patterns (some BRFs put it in shader not technique)
            else if (!string.IsNullOrEmpty(shaderName))
            {
                setup = ResolveShaderNamePartial(shaderName, setup);
            }

            // Detect particle shader
            setup.IsParticleShader = IsParticleShaderPath(setup.ShaderName);

            // For particle shaders, skip all material/flag overrides - the technique
            // is authoritative. Particle materials don't use normal maps, specular modes, etc.
            if (setup.IsParticleShader)
            {
                setup.UseAGnm = false;  // Particle textures are plain RGBA
                setup.WarbandModeName = "Particle";
                return setup;
            }

            // 3. Override based on material data (OpenBRF logic)
            setup = ApplyMaterialOverrides(brfMat, setup);

            // 4. Detect special features from technique/shader name
            if (technique.Contains("preshaded") || shaderName.Contains("preshaded"))
            {
                setup.WarbandMode = 3;
                setup.UseVertexColors = true;
            }

            if (technique.Contains("envmap") || technique.Contains("enviro"))
            {
                setup.UseEnvMap = true;
            }

            if (technique.Contains("skin") || technique.Contains("face"))
            {
                setup.UseVertexColors = true; // Skin tinting
            }

            // 5. Flag-based overrides (highest priority for blend mode)
            setup = ApplyFlagOverrides(brfMat, setup);

            // Set mode name for debugging
            setup.WarbandModeName = setup.WarbandMode switch
            {
                0 => "Plain",
                1 => "Iron",
                2 => "Shine",
                3 => "Preshaded",
                _ => "Unknown"
            };

            return setup;
        }

        /// <summary>
        /// Check if a shader path is a particle shader.
        /// </summary>
        public static bool IsParticleShaderPath(string shaderPath)
        {
            return shaderPath != null && shaderPath.StartsWith("M&B/Particles/");
        }

        /// <summary>
        /// Resolve by partial technique name matching.
        /// </summary>
        private static ShaderSetup ResolveTechniquePartial(string technique, ShaderSetup setup)
        {
            if (technique.Contains("sunflare"))
            {
                setup.ShaderName = Shader_Particle_SunFlare;
                return setup;
            }
            if (technique.Contains("soft_particle") || technique.Contains("particle_"))
            {
                // Determine add vs modulate
                if (technique.Contains("add"))
                    setup.ShaderName = Shader_Particle_Additive;
                else
                    setup.ShaderName = Shader_Particle_AlphaBlend;
                return setup;
            }

            if (technique.Contains("additive") || technique.Contains("glow"))
            {
                setup.ShaderName = Shader_Additive;
            }
            else if (technique.Contains("flora") || technique.Contains("hair"))
            {
                setup.ShaderName = Shader_Cutout;
            }
            else if (technique.Contains("alpha") && technique.Contains("blend"))
            {
                setup.ShaderName = Shader_Transparent;
            }
            else if (technique.Contains("alpha") || technique.Contains("cutout"))
            {
                setup.ShaderName = Shader_Cutout;
            }
            else if (technique.Contains("fade") || technique.Contains("transparent"))
            {
                setup.ShaderName = Shader_Transparent;
            }
            else if (technique.Contains("iron"))
            {
                setup.WarbandMode = 1;
            }
            else if (technique.Contains("specular") || technique.Contains("specmap"))
            {
                setup.WarbandMode = 2;
            }

            if (technique.Contains("bumpmap") || technique.Contains("bumpmap_interior"))
            {
                setup.UseRGBnm = false;
                setup.UseAGnm = true;
            }
            else
            {
                setup.UseRGBnm = true;
                setup.UseAGnm = false;
            }

            return setup;
        }

        /// <summary>
        /// Resolve by shader name when technique is empty.
        /// Some BRFs store particle info in the shader name field.
        /// </summary>
        private static ShaderSetup ResolveShaderNamePartial(string shaderName, ShaderSetup setup)
        {
            if (shaderName.Contains("sunflare"))
            {
                setup.ShaderName = Shader_Particle_SunFlare;
            }
            else if (shaderName.Contains("soft_particle") || shaderName.Contains("particle_add"))
            {
                if (shaderName.Contains("add"))
                    setup.ShaderName = Shader_Particle_Additive;
                else
                    setup.ShaderName = Shader_Particle_AlphaBlend;
            }
            else if (shaderName.Contains("particle_modulate") || shaderName.Contains("particle_blend"))
            {
                setup.ShaderName = Shader_Particle_AlphaBlend;
            }
            
            if (shaderName.Contains("bump_static") || shaderName.Contains("bumpmap_interior"))
            {
                setup.UseRGBnm = false;
                setup.UseAGnm = true;
            }
            else
            {
                setup.UseAGnm = false;
                setup.UseRGBnm = true;
            }

            return setup;
        }

        /// <summary>
        /// Apply OpenBRF-style material data overrides.
        /// </summary>
        private static ShaderSetup ApplyMaterialOverrides(BrfMaterial brfMat, ShaderSetup setup)
        {
            // Only override mode for Standard shader (don't touch Cutout/Transparent/Additive)
            if (setup.ShaderName != Shader_Standard)
                return setup;

            // OpenBRF logic:
            // if (HasSpec) -> NM_SHINE
            // else if ("iron" in shader) -> NM_IRON
            bool hasSpec = !string.IsNullOrEmpty(brfMat.spec) && brfMat.spec != "none";
            bool isIron = brfMat.shader?.IndexOf("iron", StringComparison.OrdinalIgnoreCase) >= 0;

            if (hasSpec && setup.WarbandMode == 0)
                setup.WarbandMode = 2; // Shine

            if (isIron && setup.WarbandMode == 0)
                setup.WarbandMode = 1; // Iron

            return setup;
        }

        /// <summary>
        /// Apply BRF flag overrides for blend mode and alpha testing.
        /// Skipped for particle shaders - technique is authoritative.
        /// </summary>
        private static ShaderSetup ApplyFlagOverrides(BrfMaterial brfMat, ShaderSetup setup)
        {
            // Never override particle shader assignment with flag-based logic
            if (setup.IsParticleShader)
                return setup;

            var blendMode = brfMat.BlendMode;
            float alphaRef = brfMat.AlphaTestThreshold;
            bool noZWrite = brfMat.NoZWrite;

            // Additive blend -> force additive shader
            if (blendMode == BrfBlendMode.Additive)
            {
                setup.ShaderName = Shader_Additive;
                return setup;
            }

            // Alpha blend with no z-write -> transparent
            if (blendMode == BrfBlendMode.AlphaBlend || (noZWrite && blendMode != BrfBlendMode.None))
            {
                setup.ShaderName = Shader_Transparent;
                return setup;
            }

            // Has alpha test reference -> cutout
            if (alphaRef > 0 && setup.ShaderName == Shader_Standard)
            {
                setup.ShaderName = Shader_Cutout;
            }

            return setup;
        }

        /// <summary>
        /// Apply resolved ShaderSetup to a Unity Material (properties, keywords, flags only).
        /// Does NOT assign textures - use the overload with textureResolver for full setup.
        /// </summary>
        public static void ApplySetup(Material material, ShaderSetup setup, BrfMaterial brfMat)
        {
            ApplyProperties(material, setup, brfMat);
        }

        /// <summary>
        /// Delegate for resolving Warband texture names to Unity Texture2D.
        /// Matches BrfDataImporter.ResolveTexture signature.
        /// </summary>
        public delegate Texture2D TextureResolverDelegate(string textureName);

        /// <summary>
        /// Apply resolved ShaderSetup to a Unity Material INCLUDING texture assignment.
        /// This is the preferred overload - use from BrfDataImporter.CreateMaterial.
        /// 
        /// Usage:
        ///   MBShaderResolver.ApplySetup(material, setup, brfMat, ResolveTexture);
        /// </summary>
        public static void ApplySetup(Material material, ShaderSetup setup, BrfMaterial brfMat,
            TextureResolverDelegate textureResolver)
        {
            ApplyProperties(material, setup, brfMat);

            if (textureResolver == null) return;


            // Slot 0: Diffuse A (always - both mesh and particle materials)
            AssignTexture(material, "_MainTex", brfMat.diffuseA, textureResolver, false);

            // Particle materials only use _MainTex - no normal/spec/detail/enviro
            if (setup.IsParticleShader)
                return;

            // Slot 1: Normal / Bump map
            AssignTexture(material, "_BumpMap", brfMat.bump, textureResolver, true);

            // Slot 2: Specular map (only for Shine mode or if spec texture exists)
            AssignTexture(material, "_SpecGlossMap", brfMat.spec, textureResolver, false);

            // Diffuse B: Detail / secondary texture
            if (!string.IsNullOrEmpty(brfMat.diffuseB) && brfMat.diffuseB != "none")
                AssignTexture(material, "_DetailAlbedoMap", brfMat.diffuseB, textureResolver, false);

            // Environment map
            if (!string.IsNullOrEmpty(brfMat.enviro) && brfMat.enviro != "none")
                AssignTexture(material, "_EnviroMap", brfMat.enviro, textureResolver, false);
        }

        /// <summary>
        /// Apply all non-texture properties, keywords, and flags to the material.
        /// Handles both mesh materials and particle materials.
        /// </summary>
        private static void ApplyProperties(Material material, ShaderSetup setup, BrfMaterial brfMat)
        {
            if (setup.IsParticleShader)
            {
                // DO NOT set _TintColor here - it stays at the shader default:
                //   Additive/SunFlare: (0.5, 0.5, 0.5, 0.5)
                //   AlphaBlend:        (1, 1, 1, 1)
                // Per-particle color comes at runtime via _ParticleBuffer,
                // and brfMat.GetColor() is the SPECULAR color, not a tint.

                // Store BRF flags for debugging
                if (material.HasProperty("_BrfFlags"))
                    material.SetFloat("_BrfFlags", brfMat.flags);

                return;
            }


            // Warband mode (Standard shader only)
            if (material.HasProperty("_WBMode"))
            {
                material.SetFloat("_WBMode", setup.WarbandMode);
                UpdateModeKeywords(material, setup.WarbandMode);
            }

            // DXT5nm
            if (material.HasProperty("_AGnm"))
            {
                material.SetFloat("_AGnm", setup.UseAGnm ? 1f : 0f);
                SetKeyword(material, "_AG_NORMAL", setup.UseAGnm);
            }
            
            if (material.HasProperty("_RGBnm"))
            {
                material.SetFloat("_RGBnm", setup.UseRGBnm ? 1f : 0f);
                SetKeyword(material, "_RGB_NORMAL", setup.UseRGBnm);
            }

            // Vertex colors
            if (material.HasProperty("_UseVertexColor"))
            {
                material.SetFloat("_UseVertexColor", setup.UseVertexColors ? 1f : 0f);
                SetKeyword(material, "_USEVERTEXCOLOR_ON", setup.UseVertexColors);
            }

            // Env map
            if (material.HasProperty("_UseEnvMap"))
            {
                material.SetFloat("_UseEnvMap", setup.UseEnvMap ? 1f : 0f);
                SetKeyword(material, "_USEENVMAP_ON", setup.UseEnvMap);
            }

            // Specular value
            if (material.HasProperty("_Specular"))
                material.SetFloat("_Specular", brfMat.specular_value);

            // Specular color from material RGB
            if (material.HasProperty("_MBSpecColor"))
                material.SetColor("_MBSpecColor", brfMat.GetColor());

            // Alpha cutoff from flags
            float alphaRef = brfMat.AlphaTestThreshold;
            if (alphaRef > 0 && material.HasProperty("_Cutoff"))
                material.SetFloat("_Cutoff", alphaRef);

            // Z-write
            if (brfMat.NoZWrite && material.HasProperty("_ZWrite"))
                material.SetFloat("_ZWrite", 0f);

            // Render order
            // int renderOrder = brfMat.RenderOrder;
            // if (renderOrder != 0)
            //     material.renderQueue = 2000 + (renderOrder * 100);

            // Store BRF flags for debugging
            if (material.HasProperty("_BrfFlags"))
                material.SetFloat("_BrfFlags", brfMat.flags);
        }

        /// <summary>
        /// Assign a texture to a material property using the resolver.
        /// Handles null checks and normal map import settings.
        /// </summary>
        private static void AssignTexture(Material material, string property, string textureName,
            TextureResolverDelegate resolver, bool isNormalMap)
        {
            if (string.IsNullOrEmpty(textureName) || textureName == "none")
                return;

            if (!material.HasProperty(property))
                return;

            Texture2D texture = resolver(textureName);
            if (texture == null) return;

            material.SetTexture(property, texture);

            // Fix normal map import settings if needed
            if (isNormalMap)
            {
                string texPath = AssetDatabase.GetAssetPath(texture);
                if (!string.IsNullOrEmpty(texPath))
                {
                    var importer = AssetImporter.GetAtPath(texPath) as TextureImporter;
                    if (importer != null && importer.textureType != TextureImporterType.NormalMap)
                    {
                        importer.textureType = TextureImporterType.NormalMap;
                        importer.SaveAndReimport();
                    }
                }
            }
        }

        // ---- Keyword Helpers ----

        private static void UpdateModeKeywords(Material mat, int mode)
        {
            mat.DisableKeyword("_WBMODE_PLAIN");
            mat.DisableKeyword("_WBMODE_IRON");
            mat.DisableKeyword("_WBMODE_SHINE");
            mat.DisableKeyword("_WBMODE_PRESHADED");

            string kw = mode switch
            {
                0 => "_WBMODE_PLAIN",
                1 => "_WBMODE_IRON",
                2 => "_WBMODE_SHINE",
                3 => "_WBMODE_PRESHADED",
                _ => "_WBMODE_PLAIN"
            };
            mat.EnableKeyword(kw);
        }

        private static void SetKeyword(Material mat, string keyword, bool on)
        {
            if (on) mat.EnableKeyword(keyword);
            else mat.DisableKeyword(keyword);
        }
    }
}
