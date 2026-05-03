using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace MountAndBlade.Data
{
    /// <summary>
    /// Decoder for Mount & Blade particle system flags.
    /// Handles billboard mode extraction and property flag decomposition.
    /// 
    /// Flag layout (from header_particle_systems.py):
    ///   Bits 0-3:   Behavior flags (always_emit, global_emit_dir, emit_at_water_level)
    ///   Bits 8-10:  Billboard mode (mutually exclusive encoded value)
    ///   Bits 12-13: Randomization flags
    ///   Bits 16-17: Turbulence / LOD flags
    /// </summary>
    public static class ParticleSystemFlagsDecoder
    {
        // BILLBOARD MODES (bits 8-10) - ENCODED VALUES, mutually exclusive
        private static readonly Dictionary<string, int> BillboardModes = new Dictionary<string, int>
        {
            { "psf_billboard_2d",      0x0100 }, // up_vec = dir, front rotated towards camera
            { "psf_billboard_3d",      0x0200 }, // front_vec points to camera
            { "psf_billboard_drop",    0x0300 },
            { "psf_turn_to_velocity",  0x0400 },
        };

        // PROPERTY FLAGS - Bitwise flags, can be combined
        private static readonly Dictionary<string, int> PropertyFlags = new Dictionary<string, int>
        {
            { "psf_always_emit",         0x00000002 },
            { "psf_global_emit_dir",     0x00000010 },
            { "psf_emit_at_water_level", 0x00000020 },
            { "psf_randomize_rotation",  0x00001000 },
            { "psf_randomize_size",      0x00002000 },
            { "psf_2d_turbulance",       0x00010000 },
            { "psf_next_effect_is_lod",  0x00020000 },
        };

        // BIT MASKS
        private const int BILLBOARD_MASK = 0x0F00; // Bits 8-11

        // PUBLIC API - BILLBOARD MODE EXTRACTION

        /// <summary>
        /// Gets the billboard mode from bits 8-11 (encoded value, mutually exclusive).
        /// Returns the mode name (e.g., "psf_billboard_3d") or empty string if none.
        /// </summary>
        public static string GetBillboardMode(int value)
        {
            int modeValue = value & BILLBOARD_MASK;
            var mode = BillboardModes.FirstOrDefault(x => x.Value == modeValue);
            return mode.Key ?? (modeValue == 0 ? "" : $"psf_billboard_unknown_{modeValue:X}");
        }

        /// <summary>
        /// Gets the billboard mode raw value from bits 8-11.
        /// </summary>
        public static int GetBillboardModeValue(int value)
        {
            return value & BILLBOARD_MASK;
        }

        // PUBLIC API - PROPERTY FLAGS DECODING

        /// <summary>
        /// Decodes all active property flags (excluding billboard mode).
        /// Returns a list of flag names in ascending bit order.
        /// </summary>
        public static List<string> DecodePropertyFlags(int value)
        {
            var result = new List<string>();

            foreach (var flag in PropertyFlags.OrderBy(x => x.Value))
            {
                if ((value & flag.Value) != 0)
                {
                    result.Add(flag.Key);
                }
            }

            return result;
        }

        /// <summary>
        /// Decodes ALL components: billboard mode + property flags.
        /// </summary>
        public static ParticleSystemFlagsData DecodeComplete(int value)
        {
            return new ParticleSystemFlagsData
            {
                RawValue = value,
                BillboardMode = GetBillboardMode(value),
                BillboardModeValue = GetBillboardModeValue(value),
                PropertyFlags = DecodePropertyFlags(value)
            };
        }

        /// <summary>
        /// Checks if a specific property flag is active.
        /// </summary>
        public static bool HasFlag(int value, string flagName)
        {
            if (!PropertyFlags.TryGetValue(flagName, out var flagBit))
            {
                return false;
            }

            return (value & flagBit) != 0;
        }

        // PUBLIC API - FLAG ENCODING

        /// <summary>
        /// Encodes particle system flags from components.
        /// </summary>
        /// <param name="billboardModeName">Billboard mode name (e.g., "psf_billboard_3d") or empty for none</param>
        /// <param name="propertyFlagNames">List of property flag names to set</param>
        /// <returns>Encoded flags value</returns>
        public static int EncodeFlags(string billboardModeName, List<string> propertyFlagNames)
        {
            int result = 0;

            // Add billboard mode (bits 8-11)
            if (!string.IsNullOrEmpty(billboardModeName) && BillboardModes.TryGetValue(billboardModeName, out var modeValue))
            {
                result |= modeValue;
            }

            // Add property flags
            if (propertyFlagNames != null)
            {
                foreach (var flagName in propertyFlagNames)
                {
                    if (PropertyFlags.TryGetValue(flagName, out var flagBit))
                    {
                        result |= flagBit;
                    }
                }
            }

            return result;
        }

        // PUBLIC API - VALIDATION

        /// <summary>
        /// Validates particle system flags and returns a list of issues.
        /// </summary>
        public static List<string> ValidateFlags(int value)
        {
            var issues = new List<string>();
            var decoded = DecodeComplete(value);

            // No billboard mode set
            if (string.IsNullOrEmpty(decoded.BillboardMode))
            {
                issues.Add("No billboard mode set - particles may not render correctly");
            }

            // 2D turbulence without billboard
            if (decoded.HasFlag("psf_2d_turbulance") &&
                decoded.BillboardMode != "psf_billboard_2d" && decoded.BillboardMode != "psf_billboard_3d")
            {
                issues.Add("psf_2d_turbulance typically used with billboard modes");
            }

            // next_effect_is_lod should link to another particle system
            if (decoded.HasFlag("psf_next_effect_is_lod"))
            {
                issues.Add("INFO: psf_next_effect_is_lod - ensure next particle system in list is the LOD variant");
            }

            return issues;
        }

        // STATIC ACCESSORS

        /// <summary>
        /// Returns all billboard mode definitions.
        /// </summary>
        public static Dictionary<string, int> GetBillboardModes() =>
            new Dictionary<string, int>(BillboardModes);

        /// <summary>
        /// Returns all property flag definitions.
        /// </summary>
        public static Dictionary<string, int> GetPropertyFlags() =>
            new Dictionary<string, int>(PropertyFlags);

        /// <summary>
        /// Gets the bit value for a flag name (checks both billboard modes and property flags).
        /// Returns 0 if not found.
        /// </summary>
        public static int GetFlagBitValue(string flagName)
        {
            if (BillboardModes.TryGetValue(flagName, out var modeVal))
                return modeVal;
            if (PropertyFlags.TryGetValue(flagName, out var flagVal))
                return flagVal;
            return 0;
        }

        /// <summary>
        /// Gets the description/tooltip for a flag.
        /// </summary>
        public static string GetFlagDescription(string flagName)
        {
            var descriptions = new Dictionary<string, string>
            {
                // Billboard modes
                { "psf_billboard_2d",      "Up = direction, front rotated towards camera" },
                { "psf_billboard_3d",      "Front vector points to camera" },
                { "psf_billboard_drop",    "Drop billboard mode" },
                { "psf_turn_to_velocity",  "Particle faces its velocity direction" },

                // Property flags
                { "psf_always_emit",         "Particle system always emits (no distance culling)" },
                { "psf_global_emit_dir",     "Emit direction is in world space (not local)" },
                { "psf_emit_at_water_level", "Particles emit at the water surface level" },
                { "psf_randomize_rotation",  "Randomize initial particle rotation" },
                { "psf_randomize_size",      "Randomize particle size" },
                { "psf_2d_turbulance",       "Apply 2D turbulence to particle movement" },
                { "psf_next_effect_is_lod",  "Next particle system in list is LOD variant" },
            };

            return descriptions.TryGetValue(flagName, out var desc) ? desc : "";
        }

        /// <summary>
        /// Gets the category for a flag.
        /// </summary>
        public static string GetFlagCategory(string flagName)
        {
            if (BillboardModes.ContainsKey(flagName))
                return "Billboard";

            var categories = new Dictionary<string, string>
            {
                { "psf_always_emit",         "Emission" },
                { "psf_global_emit_dir",     "Emission" },
                { "psf_emit_at_water_level", "Emission" },
                { "psf_randomize_rotation",  "Randomization" },
                { "psf_randomize_size",      "Randomization" },
                { "psf_2d_turbulance",       "Turbulence" },
                { "psf_next_effect_is_lod",  "LOD" },
            };

            return categories.TryGetValue(flagName, out var cat) ? cat : "Other";
        }
    }

    // DATA STRUCTURES

    /// <summary>
    /// Complete decoded particle system flags data.
    /// </summary>
    public class ParticleSystemFlagsData
    {
        public int RawValue { get; set; }
        public string BillboardMode { get; set; }
        public int BillboardModeValue { get; set; }
        public List<string> PropertyFlags { get; set; } = new List<string>();

        /// <summary>
        /// Returns a human-readable summary of the flags.
        /// </summary>
        public string GetSummary()
        {
            var parts = new List<string>();

            if (!string.IsNullOrEmpty(BillboardMode))
                parts.Add($"Billboard: {BillboardMode}");

            if (PropertyFlags.Count > 0)
                parts.Add($"Flags: {PropertyFlags.Count}");

            return string.Join(", ", parts);
        }

        /// <summary>
        /// Checks if a specific property flag is active.
        /// </summary>
        public bool HasFlag(string flagName)
        {
            return PropertyFlags.Contains(flagName);
        }

        // Quick checks for common flags
        public bool AlwaysEmits => HasFlag("psf_always_emit");
        public bool GlobalEmitDir => HasFlag("psf_global_emit_dir");
        public bool RandomizeSize => HasFlag("psf_randomize_size");
        public bool RandomizeRotation => HasFlag("psf_randomize_rotation");
        public bool IsLodLinked => HasFlag("psf_next_effect_is_lod");
    }
}
