using System;
using UnityEngine;

namespace MountAndBlade.Data
{
    /// <summary>
    /// Decodes BRF flags for Materials, Textures, Meshes, and Bodies.
    /// Based on BRF Synchronizer documentation.
    /// </summary>
    public static class BrfFlagDecoder
    {
        #region Material Flags

        [Flags]
        public enum MaterialFlags : long
        {
            None              = 0,
            NoZWrite          = 1 << 3,      // 0x00008 - Disable depth buffer writes
            NoDepthTest       = 1 << 4,      // 0x00010 - Disable depth testing
            UniformLighting   = 1 << 6,      // 0x00040 - Use uniform lighting
            AutoNormalize     = 1 << 11,     // 0x00800 - Auto-normalize normals
            RenderFirst       = 1 << 16,     // 0x10000 - Force render first (-9 order)
            AlphaTestMask     = 3L << 12,    // 0x03000 - Alpha test threshold bits
            BlendModeMask     = 7L << 8,     // 0x00700 - Blend mode bits
            RenderOrderMask   = 0xFL << 24   // 0xF000000 - Render order bits
        }

        /// <summary>
        /// Check if a material flag is set.
        /// </summary>
        public static bool HasMaterialFlag(long flags, MaterialFlags flag)
        {
            return (flags & (long)flag) != 0;
        }

        /// <summary>
        /// Convenience method matching old API.
        /// </summary>
        public static bool HasFlag(long flags, BrfMaterialFlags flag) 
            => (flags & (long)flag) != 0;

        /// <summary>
        /// Gets render order from material flags.
        /// Returns -9 if RenderFirst flag is set, otherwise -8 to +7.
        /// </summary>
        public static int GetRenderOrder(long flags)
        {
            // Check RenderFirst flag (bit 16)
            if ((flags & (1L << 16)) != 0) 
                return -9;

            // 4-bit signed encoding in bits 24-27 (-8 to +7)
            int res = (int)((flags >> 24) & 0xF);
            if (res > 7) res -= 16;
            return res;
        }

        /// <summary>
        /// Encodes render order into flags.
        /// </summary>
        public static long EncodeRenderOrder(int order)
        {
            if (order == -9)
                return 1L << 16; // RenderFirst flag

            // Clamp to valid range
            order = Mathf.Clamp(order, -8, 7);
            
            // Convert to unsigned 4-bit
            if (order < 0) order += 16;
            return (long)order << 24;
        }

        /// <summary>
        /// Gets alpha test threshold from material flags.
        /// Returns 0.0, ~0.031, ~0.531, or ~0.980.
        /// </summary>
        public static float GetAlphaTestThreshold(long flags)
        {
            long val = (flags >> 12) & 3;
            return val switch
            {
                1 => 8f / 256f,      // ~0.031
                2 => 136f / 256f,    // ~0.531
                3 => 251f / 256f,    // ~0.980
                _ => 0f
            };
        }

        /// <summary>
        /// Alias for GetAlphaTestThreshold (compatibility).
        /// </summary>
        public static float GetAlphaTestRef(long flags) => GetAlphaTestThreshold(flags);

        /// <summary>
        /// Encodes alpha test threshold into flags.
        /// </summary>
        public static long EncodeAlphaTestThreshold(float threshold)
        {
            int val;
            if (threshold <= 0f) val = 0;
            else if (threshold < 0.1f) val = 1;
            else if (threshold < 0.7f) val = 2;
            else val = 3;

            return (long)val << 12;
        }

        /// <summary>
        /// Alias for EncodeAlphaTestThreshold (compatibility).
        /// </summary>
        public static long EncodeAlphaTestRef(float threshold) => EncodeAlphaTestThreshold(threshold);

        /// <summary>
        /// Gets blend mode from material flags.
        /// </summary>
        public static BrfBlendMode GetBlendMode(long flags)
        {
            return (BrfBlendMode)((flags >> 8) & 7);
        }

        /// <summary>
        /// Encodes blend mode into flags.
        /// </summary>
        public static long EncodeBlendMode(BrfBlendMode mode)
        {
            return (long)mode << 8;
        }

        /// <summary>
        /// Gets OpenGL-style blend functions from material flags.
        /// Returns (srcBlend, dstBlend) GL constants.
        /// </summary>
        public static (int src, int dst) GetBlendFuncs(long flags)
        {
            int mode = (int)((flags >> 8) & 7);
            return mode switch
            {
                0 => (1, 0),          // GL_ONE, GL_ZERO (Opaque)
                1 or 7 => (770, 771), // GL_SRC_ALPHA, GL_ONE_MINUS_SRC_ALPHA
                2 => (770, 1),        // GL_SRC_ALPHA, GL_ONE (Additive)
                3 => (0, 768),        // GL_ZERO, GL_SRC_COLOR (Multiply)
                4 => (1, 1),          // GL_ONE, GL_ONE (Add)
                _ => (1, 0)
            };
        }

        /// <summary>
        /// Gets a summary string of all material flags for debugging.
        /// </summary>
        public static string GetMaterialFlagsSummary(long flags)
        {
            var parts = new System.Collections.Generic.List<string>();

            if (HasMaterialFlag(flags, MaterialFlags.NoZWrite)) parts.Add("NoZWrite");
            if (HasMaterialFlag(flags, MaterialFlags.NoDepthTest)) parts.Add("NoDepthTest");
            if (HasMaterialFlag(flags, MaterialFlags.UniformLighting)) parts.Add("UniformLighting");
            if (HasMaterialFlag(flags, MaterialFlags.AutoNormalize)) parts.Add("AutoNormalize");
            if (HasMaterialFlag(flags, MaterialFlags.RenderFirst)) parts.Add("RenderFirst");

            var blendMode = GetBlendMode(flags);
            if (blendMode != BrfBlendMode.None) parts.Add($"Blend:{blendMode}");

            float alphaRef = GetAlphaTestThreshold(flags);
            if (alphaRef > 0) parts.Add($"AlphaRef:{alphaRef:F2}");

            int renderOrder = GetRenderOrder(flags);
            if (renderOrder != 0) parts.Add($"Order:{renderOrder}");

            return parts.Count > 0 ? string.Join(", ", parts) : "None";
        }

        #endregion

        #region Texture Flags

        [Flags]
        public enum TextureFlags : long
        {
            None              = 0,
            Unknown0          = 1 << 0,      // 0x000001 - Unknown purpose
            ForceHiRes        = 1 << 1,      // 0x000002 - Force loading high-res mipmaps
            Unknown2          = 1 << 2,      // 0x000004 - Unknown purpose
            LanguageDependent = 1 << 3,      // 0x000008 - Load from language folder (Warband)
            HDROnly           = 1 << 4,      // 0x000010 - Only load if HDR is enabled
            NoHDR             = 1 << 5,      // 0x000020 - Don't load if HDR is enabled
            Unknown7          = 1 << 7,      // 0x000080 - Unknown purpose
            ClampU            = 1 << 20,     // 0x100000 - Clamp U coordinate (no horizontal tiling)
            ClampV            = 1 << 21,     // 0x200000 - Clamp V coordinate (no vertical tiling)
            SizeUMask         = 0xFL << 12,  // 0x00F000 - Size U bits
            SizeVMask         = 0xFL << 16,  // 0x0F0000 - Size V bits
            AnimFramesMask    = 0xFL << 24   // 0xF000000 - Animation frame count multiplier
        }

        /// <summary>
        /// Check if a texture flag is set.
        /// </summary>
        public static bool HasTextureFlag(long flags, TextureFlags flag)
        {
            return (flags & (long)flag) != 0;
        }

        /// <summary>
        /// Gets the number of animation frames for a texture.
        /// Frame count = bits 24-27 × 4 (possible values: 0, 4, 8, 12, ... 60).
        /// </summary>
        public static int GetTextureFrameCount(long flags)
        {
            return (int)((flags >> 24) & 0xF) * 4;
        }

        /// <summary>
        /// Checks if a texture is animated.
        /// </summary>
        public static bool IsTextureAnimated(long flags)
        {
            return GetTextureFrameCount(flags) > 0;
        }

        /// <summary>
        /// Gets the filename for a specific animation frame.
        /// Warband expects: basename_0.dds, basename_1.dds, etc.
        /// </summary>
        public static string GetTextureFrameName(string baseName, int frameIndex)
        {
            return $"{baseName}_{frameIndex}.dds";
        }

        /// <summary>
        /// Gets the Size U value (power-of-2, for facial textures).
        /// </summary>
        public static int GetTextureSizeU(long flags)
        {
            int val = (int)((flags >> 12) & 0xF);
            return val == 0 ? 0 : 1 << val;
        }

        /// <summary>
        /// Gets the Size V value (power-of-2, for facial textures).
        /// </summary>
        public static int GetTextureSizeV(long flags)
        {
            int val = (int)((flags >> 16) & 0xF);
            return val == 0 ? 0 : 1 << val;
        }

        /// <summary>
        /// Gets Unity TextureWrapMode based on clamp flags.
        /// </summary>
        public static TextureWrapMode GetTextureWrapModeU(long flags)
        {
            return HasTextureFlag(flags, TextureFlags.ClampU) 
                ? TextureWrapMode.Clamp 
                : TextureWrapMode.Repeat;
        }

        public static TextureWrapMode GetTextureWrapModeV(long flags)
        {
            return HasTextureFlag(flags, TextureFlags.ClampV) 
                ? TextureWrapMode.Clamp 
                : TextureWrapMode.Repeat;
        }

        #endregion

        #region Mesh Flags

        [Flags]
        public enum MeshFlags : long
        {
            None                  = 0,
            UnknownProps          = 1 << 0,      // 0x000001 - Unknown (for props?)
            UnknownParticles1     = 1 << 1,      // 0x000002 - Unknown (for particles?)
            UnknownPlants         = 1 << 2,      // 0x000004 - Unknown (plants?)
            UnknownParticles2     = 1 << 3,      // 0x000008 - Unknown (for particles?)
            UnknownHairsBodyParts = 1 << 5,      // 0x000020 - Unknown (hairs and body parts?)
            UnknownParticles3     = 1 << 8,      // 0x000100 - Unknown (for particles?)
            UnknownScreenSpace    = 1 << 9,      // 0x000200 - Unknown (screen space?)
            HasTangents           = 1 << 16,     // 0x010000 - Mesh stores tangent vectors (auto-set)
            WarbandFormat         = 1 << 17,     // 0x020000 - Warband format flag (auto-set)
            PreExponentiateColors = 1 << 24,     // 0x1000000 - Pre-exponentiate vertex colors
            UnknownParticles4     = 1 << 26      // 0x4000000 - Unknown (for particles?)
        }

        /// <summary>
        /// Check if a mesh flag is set.
        /// </summary>
        public static bool HasMeshFlag(long flags, MeshFlags flag)
        {
            return (flags & (long)flag) != 0;
        }

        /// <summary>
        /// Checks if mesh has tangent data (required for normal mapping).
        /// </summary>
        public static bool MeshHasTangents(long flags)
        {
            return HasMeshFlag(flags, MeshFlags.HasTangents);
        }

        /// <summary>
        /// Checks if vertex colors should be pre-exponentiated for gamma correction.
        /// </summary>
        public static bool MeshPreExponentiateColors(long flags)
        {
            return HasMeshFlag(flags, MeshFlags.PreExponentiateColors);
        }

        #endregion

        #region Body/Primitive Flags (Hitbox)

        /// <summary>
        /// Hitbox flags stored in the lower 8 bits of primitive flags.
        /// </summary>
        [Flags]
        public enum HitboxFlags : byte
        {
            None        = 0,
            TwoSided    = 1 << 0,  // 0x01 - Collision from both sides
            NoCollision = 1 << 1,  // 0x02 - No collision detection (visual only)
            NoShadow    = 1 << 2,  // 0x04 - Don't cast shadows
            Difficult   = 1 << 3,  // 0x08 - Difficult terrain (affects AI pathfinding)
            Unwalkable  = 1 << 4   // 0x10 - Cannot walk on this surface
        }

        /// <summary>
        /// Extracts hitbox flags from the lower 8 bits of primitive flags.
        /// </summary>
        public static HitboxFlags GetHitboxFlags(long flags)
        {
            return (HitboxFlags)(flags & 0xFF);
        }

        /// <summary>
        /// Checks if a specific hitbox flag is set.
        /// </summary>
        public static bool HasHitboxFlag(long flags, HitboxFlags flag)
        {
            return ((flags & 0xFF) & (byte)flag) != 0;
        }

        /// <summary>
        /// Gets the hitbox type from the lower 8 bits.
        /// </summary>
        public static byte GetHitboxType(long flags)
        {
            return (byte)(flags & 0xFF);
        }

        /// <summary>
        /// Extracts the skeleton name hint from upper 24 bits (bytes 1-3).
        /// Used to associate collision primitives with specific skeleton bones.
        /// </summary>
        public static string GetSkeletonNameHint(long flags)
        {
            char c1 = (char)((flags >> 8) & 0xFF);
            char c2 = (char)((flags >> 16) & 0xFF);
            char c3 = (char)((flags >> 24) & 0xFF);
            return new string(new[] { c1, c2, c3 }).TrimEnd('\0');
        }

        /// <summary>
        /// Alias for GetSkeletonNameHint (compatibility).
        /// </summary>
        public static string GetSkeletonNameFromBodyFlags(long flags) => GetSkeletonNameHint(flags);

        #endregion
    }

    #region Enums (Global)

    /// <summary>
    /// Blend modes for BRF materials.
    /// </summary>
    public enum BrfBlendMode
    {
        None = 0,           // Opaque - No blending
        AlphaBlend = 1,     // SrcAlpha, InvSrcAlpha (standard transparency)
        Additive = 2,       // SrcAlpha, One (glow effects)
        Multiply = 3,       // Zero, SrcColor (darken)
        Add = 4,            // One, One (bright additive)
        Auto = 7            // Engine decides
    }

    /// <summary>
    /// Common material flags for quick access.
    /// </summary>
    [Flags]
    public enum BrfMaterialFlags : long
    {
        None            = 0,
        NoZWrite        = 1 << 3,
        NoDepthTest     = 1 << 4,
        UniformLighting = 1 << 6,
        AutoNormalize   = 1 << 11,
        RenderFirst     = 1 << 16
    }

    #endregion
}