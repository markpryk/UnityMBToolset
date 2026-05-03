using System;
using System.Collections.Generic;
using UnityEngine;

namespace MountAndBlade.Data
{
    /// <summary>
    /// Root container for all BRF data from data.json.
    /// </summary>
    [Serializable]
    public class BrfData
    {
        public string version;
        public List<BrfMesh> meshes = new();
        public List<BrfMaterial> materials = new();
        public List<BrfTexture> textures = new();
        public List<BrfShader> shaders = new();
        public List<BrfSkeleton> skeletons = new();
        public List<BrfAnimation> animations = new();
        public List<BrfBody> bodies = new();
    }


    /// <summary>
    /// Material data from BRF.
    /// </summary>
    [Serializable]
    public class BrfMaterial
    {
        public string name;
        public long flags;              // MaterialFlags bitmask
        public string shader;           // Reference to shader name
        public string diffuseA;         // Primary diffuse texture (albedo/color map)
        public string diffuseB;         // Secondary diffuse texture (for multi-texturing)
        public string bump;             // Normal/bump map texture
        public string enviro;           // Environment map texture (for reflections)
        public string spec;             // Specular map texture (gloss/shininess)
        public float[] color_rgb;       // Specular RGB color multiplier [r, g, b] (0.0-1.0 range)
        public float specular_value;    // Specular coefficient (shininess/intensity)

        /// <summary>
        /// Gets the Specular color as a Unity Color.
        /// </summary>
        public Color GetColor()
        {
            if (color_rgb == null || color_rgb.Length < 3)
                return Color.white;

            return new Color(
                Mathf.Clamp01(color_rgb[0]),
                Mathf.Clamp01(color_rgb[1]),
                Mathf.Clamp01(color_rgb[2])
            );
        }

        /// <summary>
        /// Gets the normalized specular value (0-1 range).
        /// </summary>
        public float GetNormalizedSpecular()
        {
            return Mathf.Clamp01(specular_value / 100f);
        }

        // Flag helpers
        public int RenderOrder => BrfFlagDecoder.GetRenderOrder(flags);
        public BrfBlendMode BlendMode => BrfFlagDecoder.GetBlendMode(flags);
        public float AlphaTestThreshold => BrfFlagDecoder.GetAlphaTestThreshold(flags);
        public bool NoZWrite => BrfFlagDecoder.HasFlag(flags, BrfMaterialFlags.NoZWrite);
        public bool NoDepthTest => BrfFlagDecoder.HasFlag(flags, BrfMaterialFlags.NoDepthTest);
        public bool UniformLighting => BrfFlagDecoder.HasFlag(flags, BrfMaterialFlags.UniformLighting);
        public bool IsTransparent => BlendMode != BrfBlendMode.None || NoZWrite;
    }

    /// <summary>
    /// Texture data from BRF.
    /// </summary>
    [Serializable]
    public class BrfTexture
    {
        public string name;
        public long flags;  // TextureFlags bitmask

        // Flag helpers
        public int FrameCount => BrfFlagDecoder.GetTextureFrameCount(flags);
        public bool IsAnimated => FrameCount > 0;
        public bool ForceHiRes => BrfFlagDecoder.HasTextureFlag(flags, BrfFlagDecoder.TextureFlags.ForceHiRes);
        public bool ClampU => BrfFlagDecoder.HasTextureFlag(flags, BrfFlagDecoder.TextureFlags.ClampU);
        public bool ClampV => BrfFlagDecoder.HasTextureFlag(flags, BrfFlagDecoder.TextureFlags.ClampV);
        public bool LanguageDependent => BrfFlagDecoder.HasTextureFlag(flags, BrfFlagDecoder.TextureFlags.LanguageDependent);
        public bool HDROnly => BrfFlagDecoder.HasTextureFlag(flags, BrfFlagDecoder.TextureFlags.HDROnly);
        public bool NoHDR => BrfFlagDecoder.HasTextureFlag(flags, BrfFlagDecoder.TextureFlags.NoHDR);

        /// <summary>
        /// Gets Unity wrap mode for U coordinate.
        /// </summary>
        public TextureWrapMode WrapModeU => ClampU ? TextureWrapMode.Clamp : TextureWrapMode.Repeat;

        /// <summary>
        /// Gets Unity wrap mode for V coordinate.
        /// </summary>
        public TextureWrapMode WrapModeV => ClampV ? TextureWrapMode.Clamp : TextureWrapMode.Repeat;

        /// <summary>
        /// Gets the filename for a specific animation frame.
        /// </summary>
        public string GetFrameName(int frameIndex)
        {
            return BrfFlagDecoder.GetTextureFrameName(name, frameIndex);
        }
    }

    /// <summary>
    /// Shader data from BRF.
    /// </summary>
    [Serializable]
    public class BrfShader
    {
        public string name;
        public string technique;
        public string fallback;
        public long flags;
        public long requirements;
        public List<BrfShaderOption> options = new();
    }

    /// <summary>
    /// Shader option data from BRF.
    /// </summary>
    [Serializable]
    public class BrfShaderOption
    {
        public int map;
        public long colorOp;
        public long alphaOp;
        public long flags;
    }

    /// <summary>
    /// Skeleton data from BRF.
    /// </summary>
    [Serializable]
    public class BrfSkeleton
    {
        public string name;
        public long flags;
        public string source;   // Path to .smd file
    }

    /// <summary>
    /// Animation data from BRF.
    /// </summary>
    [Serializable]
    public class BrfAnimation
    {
        public string name;
        public string source;   // Path to .smd file
    }

    /// <summary>
    /// Collision body data from BRF.
    /// </summary>
    [Serializable]
    public class BrfBody
    {
        public string name;
        public string source;   // Path to .obj collision mesh
        public long flags;
        public List<BrfPrimitive> primitives = new();
    }

    /// <summary>
    /// Collision primitive data from BRF.
    /// Supports sphere, capsule, manifold (mesh), and polygon types.
    /// </summary>
    [Serializable]
    public class BrfPrimitive
    {
        public string type;     // "sphere", "capsule", "manifold", "polygon"
        public long flags;      // Hitbox flags (lower 8 bits) + skeleton name (upper bytes)

        // === SPHERE fields ===
        public float radius;
        public Vector3 center;

        // === CAPSULE fields ===
        // radius (shared with sphere)
        public Vector3 p1;      // Capsule endpoint 1
        public Vector3 p2;      // Capsule endpoint 2

        // === MANIFOLD fields (polygon mesh collision) ===
        public int orientation;     // +1 or -1 (face winding order)
        public Vector3[] vertices;  // Vertex positions
        public int[][] faces;       // Polygon indices (array of vertex index arrays)

        // Flag helpers
        public BrfFlagDecoder.HitboxFlags HitboxFlags => BrfFlagDecoder.GetHitboxFlags(flags);
        public string SkeletonNameHint => BrfFlagDecoder.GetSkeletonNameHint(flags);
        public bool IsTwoSided => BrfFlagDecoder.HasHitboxFlag(flags, BrfFlagDecoder.HitboxFlags.TwoSided);
        public bool NoCollision => BrfFlagDecoder.HasHitboxFlag(flags, BrfFlagDecoder.HitboxFlags.NoCollision);
        public bool NoShadow => BrfFlagDecoder.HasHitboxFlag(flags, BrfFlagDecoder.HitboxFlags.NoShadow);
        public bool IsDifficultTerrain => BrfFlagDecoder.HasHitboxFlag(flags, BrfFlagDecoder.HitboxFlags.Difficult);
        public bool IsUnwalkable => BrfFlagDecoder.HasHitboxFlag(flags, BrfFlagDecoder.HitboxFlags.Unwalkable);

        /// <summary>
        /// Gets the primitive type as an enum.
        /// </summary>
        public BrfPrimitiveType GetPrimitiveType()
        {
            return type?.ToLowerInvariant() switch
            {
                "sphere" => BrfPrimitiveType.Sphere,
                "capsule" => BrfPrimitiveType.Capsule,
                "manifold" => BrfPrimitiveType.Manifold,
                "polygon" => BrfPrimitiveType.Polygon,
                _ => BrfPrimitiveType.Unknown
            };
        }

        /// <summary>
        /// Converts BRF Z-up coordinates to Unity Y-up.
        /// </summary>
        public Vector3 GetCenterUnity() => ConvertToUnity(center);
        public Vector3 GetP1Unity() => ConvertToUnity(p1);
        public Vector3 GetP2Unity() => ConvertToUnity(p2);

        private static Vector3 ConvertToUnity(Vector3 brfCoord)
        {
            return new Vector3(brfCoord.x, brfCoord.z, brfCoord.y);
        }
    }

    /// <summary>
    /// Collision primitive types.
    /// </summary>
    public enum BrfPrimitiveType
    {
        Unknown,
        Sphere,
        Capsule,
        Manifold,   // Mesh-based collision
        Polygon     // Single face polygon
    }
}