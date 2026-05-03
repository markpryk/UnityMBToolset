using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MountAndBlade.Data;
using UnityEditor;
using UnityEngine;

namespace MountAndBlade.ModdingToolkit
{
    /// <summary>
    /// Helper class to populate MBBrfData assets from data.json files.
    /// </summary>
    public static class BrfDataPopulator
    {
        // Extensions that Unity can load as Texture2D via AssetDatabase
        private static readonly string[] TextureExtensions =
            { "*.png", "*.tga", "*.psd", "*.jpg", "*.jpeg", "*.bmp" };

        /// <summary>
        /// Populate a single MBBrfData asset from its data.json file.
        /// </summary>
        public static BrfPopulateResult PopulateBrfData(
            MBBrfData brfData,
            string brfFolder,
            MBModuleImportContext moduleContext,
            MBModuleImportContext nativeContext)
        {
            var result = new BrfPopulateResult();

            var importer = new BrfDataImporter(brfData.ModuleName, brfFolder, moduleContext, nativeContext);
            if (!importer.LoadDataJson())
                return result;

            // Clear existing data
            brfData.Meshes.Clear();
            brfData.Materials.Clear();
            brfData.Bodies.Clear();
            brfData.Textures.Clear();

            // TEXTURES - build file cache, then populate entries

            // Scan BRF Textures/ folder + contexts for actual files on disk
            var textureFileCache = BuildTextureFileCache(brfFolder, moduleContext, nativeContext);

            // Populate texture entries from data.json
            // data.json lists textures that THIS BRF defines (name + flags)
            var dataTextures = importer.GetTextures();
            if (dataTextures != null)
            {
                foreach (var brfTex in dataTextures)
                {
                    var texEntry = new BrfTextureEntry
                    {
                        Name = brfTex.name,
                        Flags = brfTex.flags
                    };

                    // Resolve to Unity Texture2D from file cache
                    texEntry.UnityTexture = ResolveTexture(brfTex.name, textureFileCache);

                    brfData.Textures.Add(texEntry);
                    result.TextureCount++;
                }
            }

            // Also add entries for texture files present in Textures/ folder
            // but not listed in data.json (cross-BRF texture references)
            var knownNames = new HashSet<string>(
                brfData.Textures.Select(t => t.Name), StringComparer.OrdinalIgnoreCase);

            foreach (var kvp in textureFileCache)
            {
                if (!knownNames.Contains(kvp.Key))
                {
                    brfData.Textures.Add(new BrfTextureEntry
                    {
                        Name = kvp.Key,
                        Flags = 0, // No flag data for cross-BRF textures
                        UnityTexture = kvp.Value
                    });
                    result.TextureCount++;
                }
            }

            // MESHES

            foreach (var brfMesh in importer.GetMeshes())
            {
                var (baseName, lodLevel, subMesh) = BrfMeshEntry.ParseMeshName(brfMesh.Name);

                var meshEntry = new BrfMeshEntry
                {
                    Name = brfMesh.Name,
                    BaseName = baseName,
                    MaterialName = brfMesh.Material,
                    Flags = brfMesh.Flags,
                    LodLevel = lodLevel,
                    SubMeshIndex = subMesh,
                    IsCollisionMesh = brfMesh.Name.StartsWith("bo_", StringComparison.OrdinalIgnoreCase)
                };

                string meshPath = Path.Combine(brfFolder, "Meshes", $"{brfMesh.Name}.obj");
                meshEntry.UnityMesh = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
                meshEntry.SourcePath = meshPath;

                brfData.Meshes.Add(meshEntry);
                result.MeshCount++;
            }

            // MATERIALS - with Texture2D references

            string materialsFolder = Path.Combine(brfFolder, "Materials");
            foreach (var brfMat in importer.GetMaterials())
            {
                var matEntry = new BrfMaterialEntry
                {
                    Name = brfMat.name,
                    Flags = brfMat.flags,
                    ShaderName = brfMat.shader,

                    // String names from BRF
                    DiffuseA = brfMat.diffuseA,
                    DiffuseB = brfMat.diffuseB,
                    Bump = brfMat.bump,
                    Enviro = brfMat.enviro,
                    Spec = brfMat.spec,

                    // Resolve to Unity Texture2D
                    DiffuseATexture = ResolveTexture(brfMat.diffuseA, textureFileCache),
                    DiffuseBTexture = ResolveTexture(brfMat.diffuseB, textureFileCache),
                    BumpTexture = ResolveTexture(brfMat.bump, textureFileCache),
                    EnviroTexture = ResolveTexture(brfMat.enviro, textureFileCache),
                    SpecTexture = ResolveTexture(brfMat.spec, textureFileCache),

                    Color = brfMat.GetColor(),
                    SpecularValue = brfMat.specular_value
                };

                // Find Unity material asset
                string matPath = Path.Combine(materialsFolder, $"{brfMat.name}.mat");
                matEntry.UnityMaterial = AssetDatabase.LoadAssetAtPath<Material>(matPath);

                brfData.Materials.Add(matEntry);
                result.MaterialCount++;
            }

            // BODIES

            foreach (var brfBody in importer.GetBodies())
            {
                var bodyEntry = new BrfBodyEntry
                {
                    Name = brfBody.name,
                    Flags = brfBody.flags,
                    Primitives = new List<BrfCollisionPrimitive>()
                };

                bool hasManifold = false;

                if (brfBody.primitives != null)
                {
                    foreach (var prim in brfBody.primitives)
                    {
                        var primType = prim.GetPrimitiveType();

                        if (primType == BrfPrimitiveType.Manifold || primType == BrfPrimitiveType.Polygon)
                        {
                            hasManifold = true;
                            continue;
                        }

                        bodyEntry.Primitives.Add(new BrfCollisionPrimitive
                        {
                            Type = primType,
                            Flags = prim.flags,
                            Radius = prim.radius,
                            Center = prim.GetCenterUnity(),
                            Point1 = prim.GetP1Unity(),
                            Point2 = prim.GetP2Unity()
                        });
                    }
                }

                // Only assign collision mesh if manifold primitives exist
                if (hasManifold && !string.IsNullOrEmpty(brfBody.source))
                {
                    string collisionPath = Path.Combine(brfFolder, brfBody.source);
                    bodyEntry.CollisionMesh = AssetDatabase.LoadAssetAtPath<Mesh>(collisionPath);
                    bodyEntry.SourcePath = collisionPath;
                }

                brfData.Bodies.Add(bodyEntry);
                result.BodyCount++;
            }

            // MODEL GROUPS + STATISTICS

            brfData.RebuildModelGroups();
            result.ModelGroupCount = brfData.ModelGroups.Count;

            brfData.UpdateStatistics();

            return result;
        }

        #region Texture Resolution

        /// <summary>
        /// Build a name → Texture2D cache from the BRF's Textures/ folder and contexts.
        /// Priority: BRF local folder → module context → native context.
        /// </summary>
        private static Dictionary<string, Texture2D> BuildTextureFileCache(
            string brfFolder,
            MBModuleImportContext moduleContext,
            MBModuleImportContext nativeContext)
        {
            var cache = new Dictionary<string, Texture2D>(StringComparer.OrdinalIgnoreCase);

            // 1. BRF's own Textures/ folder (highest priority)
            string texturesFolder = Path.Combine(brfFolder, "Textures");
            if (Directory.Exists(texturesFolder))
            {
                AddTexturesFromFolder(texturesFolder, cache);
            }

            // 2. Module context - other BRF Textures/ folders
            if (moduleContext != null)
            {
                var moduleTextures = moduleContext.GetTextureFiles();
                if (moduleTextures != null)
                {
                    foreach (var texPath in moduleTextures)
                    {
                        string texName = Path.GetFileNameWithoutExtension(texPath).ToLowerInvariant();
                        if (!cache.ContainsKey(texName))
                        {
                            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
                            if (texture != null)
                                cache[texName] = texture;
                        }
                    }
                }
            }

            // 3. Native context - fallback
            if (nativeContext != null)
            {
                var nativeTextures = nativeContext.GetTextureFiles();
                if (nativeTextures != null)
                {
                    foreach (var texPath in nativeTextures)
                    {
                        string texName = Path.GetFileNameWithoutExtension(texPath).ToLowerInvariant();
                        if (!cache.ContainsKey(texName))
                        {
                            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
                            if (texture != null)
                                cache[texName] = texture;
                        }
                    }
                }
            }

            return cache;
        }

        /// <summary>
        /// Scan a folder for texture files and add them to cache.
        /// </summary>
        private static void AddTexturesFromFolder(string folder, Dictionary<string, Texture2D> cache)
        {
            foreach (var ext in TextureExtensions)
            {
                foreach (var texFile in Directory.GetFiles(folder, ext))
                {
                    string texName = Path.GetFileNameWithoutExtension(texFile).ToLowerInvariant();
                    if (!cache.ContainsKey(texName))
                    {
                        var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(texFile);
                        if (texture != null)
                        {
                            cache[texName] = texture;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Resolve a BRF texture name string to a Unity Texture2D.
        /// </summary>
        private static Texture2D ResolveTexture(string textureName, Dictionary<string, Texture2D> cache)
        {
            if (string.IsNullOrEmpty(textureName) ||
                textureName.Equals("none", StringComparison.OrdinalIgnoreCase))
                return null;

            // Remove extension if present (BRF may reference "armor_a.dds")
            string name = Path.GetFileNameWithoutExtension(textureName).ToLowerInvariant();

            return cache.TryGetValue(name, out var texture) ? texture : null;
        }

        #endregion

        #region Prefab Creation

        /// <summary>
        /// Create a prefab from a BrfModelGroup.
        /// </summary>
        public static GameObject CreatePrefabFromModelGroup(
            string moduleName,
            MBBrfData brfData,
            BrfModelGroup group,
            string outputPath,
            MBModuleImportContext moduleContext,
            MBModuleImportContext nativeContext)
        {
            if (group.MeshEntries.Count == 0)
                return null;

            string prefabPath = Path.Combine(outputPath, $"{group.GroupID}.prefab");

            // Check if prefab already exists
            var existingPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (existingPrefab != null)
                return existingPrefab;

            // Create root GameObject
            var root = new GameObject(group.GroupID);

            // Add MBModel component
            var mbModel = root.AddComponent<MBModel>();
            mbModel.ModelID = group.GroupID;
            mbModel.BRFSource = brfData.BrfName;
            mbModel.SourceModule = moduleName;

            // Add meshes
            foreach (var meshEntry in group.MeshEntries)
            {
                if (meshEntry.UnityMesh == null) continue;

                Material material = null;
                if (!string.IsNullOrEmpty(meshEntry.MaterialName))
                {
                    var matEntry = brfData.GetMaterialEntry(meshEntry.MaterialName);
                    material = matEntry?.UnityMaterial;

                    if (material == null)
                    {
                        material = moduleContext?.GetMaterial(meshEntry.MaterialName) ??
                                   nativeContext?.GetMaterial(meshEntry.MaterialName);
                    }
                }

                mbModel.AddMesh(meshEntry.UnityMesh, material, meshEntry.MaterialName, meshEntry.Flags);
            }

            // Add LOD meshes
            foreach (var lodEntry in group.LodMeshEntries)
            {
                if (lodEntry.UnityMesh == null) continue;

                Material material = null;
                if (!string.IsNullOrEmpty(lodEntry.MaterialName))
                {
                    var matEntry = brfData.GetMaterialEntry(lodEntry.MaterialName);
                    material = matEntry?.UnityMaterial;
                }

                mbModel.AddLodMesh(lodEntry.UnityMesh, material, lodEntry.LodLevel);
            }

            // Add collision
            if (group.CollisionBody != null)
            {
                var collision = ConvertBodyEntryToCollision(group.CollisionBody);
                mbModel.Collision = collision;
            }

            // Build the hierarchy
            mbModel.Compose();

            // Save as prefab
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            UnityEngine.Object.DestroyImmediate(root);

            return prefab;
        }

        #endregion

        #region Collision Conversion

        /// <summary>
        /// Convert BrfBodyEntry to MBModelCollision.
        /// </summary>
        public static MBModelCollision ConvertBodyEntryToCollision(BrfBodyEntry bodyEntry)
        {
            var collision = new MBModelCollision
            {
                BodyName = bodyEntry.Name,
                Flags = bodyEntry.Flags,
                CollisionMesh = bodyEntry.CollisionMesh
            };

            foreach (var prim in bodyEntry.Primitives)
            {
                if (prim.NoCollision) continue;

                collision.Primitives.Add(new MBCollisionPrimitive
                {
                    Type = prim.Type switch
                    {
                        BrfPrimitiveType.Sphere => MBCollisionType.Sphere,
                        BrfPrimitiveType.Capsule => MBCollisionType.Capsule,
                        _ => MBCollisionType.Box
                    },
                    Radius = prim.Radius,
                    Center = prim.Center,
                    Point1 = prim.Point1,
                    Point2 = prim.Point2,
                    Size = prim.Size
                });
            }

            return collision;
        }

        #endregion
    }

    public struct BrfPopulateResult
    {
        public int MeshCount;
        public int MaterialCount;
        public int TextureCount;
        public int BodyCount;
        public int ModelGroupCount;
    }
}
