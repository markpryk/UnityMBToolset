using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace MountAndBlade.Data
{
    /// <summary>
    /// Decoder for Mount & Blade scene flags (from header_scenes.py)
    /// Scene flags are straightforward bitwise flags with no overlapping meanings
    /// </summary>
    public static class SceneFlagDecoder
    {
        // SCENE FLAGS - All are bitwise flags that can be combined with OR (|)
        // From header_scenes.py in the Mount & Blade module system
        
        private static readonly Dictionary<string, BigInteger> SceneFlags = new Dictionary<string, BigInteger>
        {
            // Bit 0 (0x1)
            { "sf_indoors", 0x00000001 },           // The scene shouldn't have a skybox and lighting by sun
            
            // Bit 1 (0x2)
            { "sf_force_skybox", 0x00000002 },      // Force adding a skybox even if indoors flag is set
            
            // Bit 8 (0x100)
            { "sf_generate", 0x00000100 },          // Generate terrain by terrain-generator
            
            // Bit 9 (0x200)
            { "sf_randomize", 0x00000200 },         // Randomize terrain generator key
            
            // Bit 10 (0x400)
            { "sf_auto_entry_points", 0x00000400 }, // Automatically create entry points
            
            // Bit 11 (0x800)
            { "sf_no_horses", 0x00000800 },         // Horses are not available
            
            // Bit 12 (0x1000)
            { "sf_muddy_water", 0x00001000 },       // Changes the shader of the river mesh
        };
        
        // FLAG DESCRIPTIONS - Helpful descriptions for each flag
        
        private static readonly Dictionary<string, string> FlagDescriptions = new Dictionary<string, string>
        {
            { "sf_indoors", "Removes skybox and sun lighting. Used for interior scenes." },
            { "sf_force_skybox", "Forces skybox rendering even if sf_indoors is set." },
            { "sf_generate", "Generates terrain using the terrain generator with the provided terrain code." },
            { "sf_randomize", "Randomizes the terrain generation seed for varied landscapes." },
            { "sf_auto_entry_points", "Automatically creates entry points in randomly generated scenes." },
            { "sf_no_horses", "Marks scene as not allowing horses. Used with scene_allows_mounted_units operation." },
            { "sf_muddy_water", "Changes the river mesh shader to muddy/brown water appearance." },
        };
        
        // PUBLIC API - Flag Decoding
        
        /// <summary>
        /// Decodes all active scene flags from a value
        /// </summary>
        /// <param name="value">The scene flags value (can be BigInteger, long, int, or string)</param>
        /// <returns>List of active flag names in order</returns>
        public static List<string> DecodeFlags(BigInteger value)
        {
            var activeFlags = new List<string>();
            
            foreach (var flag in SceneFlags.OrderBy(x => x.Value))
            {
                if ((value & flag.Value) != 0)
                {
                    activeFlags.Add(flag.Key);
                }
            }
            
            return activeFlags;
        }
        
        /// <summary>
        /// Decodes scene flags from a string value (supports hex or decimal)
        /// </summary>
        public static List<string> DecodeFlags(string value)
        {
            if (string.IsNullOrEmpty(value))
                return new List<string>();
            
            BigInteger flagValue = ParseFlagString(value);
            return DecodeFlags(flagValue);
        }
        
        /// <summary>
        /// Checks if a specific flag is set in the value
        /// </summary>
        public static bool HasFlag(BigInteger value, string flagName)
        {
            if (!SceneFlags.TryGetValue(flagName, out var flagBit))
                return false;
            
            return (value & flagBit) != 0;
        }
        
        /// <summary>
        /// Checks if a specific flag is set (string value version)
        /// </summary>
        public static bool HasFlag(string value, string flagName)
        {
            if (string.IsNullOrEmpty(value))
                return false;
            
            BigInteger flagValue = ParseFlagString(value);
            return HasFlag(flagValue, flagName);
        }
        
        /// <summary>
        /// Gets the bit value for a flag name
        /// </summary>
        public static BigInteger GetFlagBitValue(string flagName)
        {
            return SceneFlags.TryGetValue(flagName, out var value) ? value : 0;
        }
        
        /// <summary>
        /// Checks if a flag name is valid
        /// </summary>
        public static bool IsValidFlag(string flagName)
        {
            return SceneFlags.ContainsKey(flagName);
        }
        
        /// <summary>
        /// Gets the description for a flag
        /// </summary>
        public static string GetFlagDescription(string flagName)
        {
            return FlagDescriptions.TryGetValue(flagName, out var desc) ? desc : "";
        }
        
        /// <summary>
        /// Encodes a list of flag names into a single value
        /// </summary>
        public static BigInteger EncodeFlags(IEnumerable<string> flagNames)
        {
            BigInteger result = 0;
            
            foreach (var flagName in flagNames)
            {
                if (SceneFlags.TryGetValue(flagName, out var flagBit))
                {
                    result |= flagBit;
                }
            }
            
            return result;
        }
        
        /// <summary>
        /// Creates a formatted string showing all active flags with descriptions
        /// </summary>
        public static string GetFormattedFlagList(BigInteger value)
        {
            var activeFlags = DecodeFlags(value);
            
            if (activeFlags.Count == 0)
                return "No flags set";
            
            var lines = new List<string>();
            foreach (var flag in activeFlags)
            {
                string desc = GetFlagDescription(flag);
                lines.Add($"• {flag}: {desc}");
            }
            
            return string.Join("\n", lines);
        }
        
        // PUBLIC API - Flag Information
        
        /// <summary>
        /// Gets all available scene flags
        /// </summary>
        public static Dictionary<string, BigInteger> GetAllFlags()
        {
            return new Dictionary<string, BigInteger>(SceneFlags);
        }
        
        /// <summary>
        /// Gets all flag descriptions
        /// </summary>
        public static Dictionary<string, string> GetAllDescriptions()
        {
            return new Dictionary<string, string>(FlagDescriptions);
        }
        
        /// <summary>
        /// Gets a list of all flag names
        /// </summary>
        public static List<string> GetAllFlagNames()
        {
            return SceneFlags.Keys.OrderBy(k => SceneFlags[k]).ToList();
        }
        
        // SCENE TYPE HELPERS - Common flag combinations
        
        /// <summary>
        /// Checks if the flags indicate an indoor scene
        /// </summary>
        public static bool IsIndoor(BigInteger value)
        {
            return HasFlag(value, "sf_indoors");
        }
        
        /// <summary>
        /// Checks if the flags indicate an outdoor generated scene
        /// </summary>
        public static bool IsGeneratedOutdoor(BigInteger value)
        {
            return HasFlag(value, "sf_generate") && !HasFlag(value, "sf_indoors");
        }
        
        /// <summary>
        /// Checks if the flags indicate a random battle scene
        /// (generated + randomized + auto entry points)
        /// </summary>
        public static bool IsRandomBattleScene(BigInteger value)
        {
            return HasFlag(value, "sf_generate") && 
                   HasFlag(value, "sf_randomize") && 
                   HasFlag(value, "sf_auto_entry_points");
        }
        
        /// <summary>
        /// Gets a human-readable scene type description based on flags
        /// </summary>
        public static string GetSceneTypeDescription(BigInteger value)
        {
            if (IsIndoor(value))
            {
                return HasFlag(value, "sf_force_skybox") 
                    ? "Indoor (with forced skybox)" 
                    : "Indoor";
            }
            
            if (IsRandomBattleScene(value))
            {
                return "Random Battle Scene";
            }
            
            if (HasFlag(value, "sf_generate") && HasFlag(value, "sf_randomize"))
            {
                return "Randomized Generated Outdoor";
            }
            
            if (HasFlag(value, "sf_generate"))
            {
                return "Generated Outdoor";
            }
            
            return "Static Scene";
        }
        
        // VALIDATION HELPERS
        
        /// <summary>
        /// Validates flag combinations and returns warnings about potentially problematic setups
        /// </summary>
        public static List<string> ValidateFlagCombination(BigInteger value)
        {
            var warnings = new List<string>();
            
            // Warning: Indoor with generate flag
            if (HasFlag(value, "sf_indoors") && HasFlag(value, "sf_generate"))
            {
                warnings.Add("Indoor scenes typically don't use terrain generation (sf_generate flag)");
            }
            
            // Warning: sf_randomize without sf_generate
            if (HasFlag(value, "sf_randomize") && !HasFlag(value, "sf_generate"))
            {
                warnings.Add("sf_randomize flag has no effect without sf_generate");
            }
            
            // Warning: sf_auto_entry_points without sf_generate
            if (HasFlag(value, "sf_auto_entry_points") && !HasFlag(value, "sf_generate"))
            {
                warnings.Add("sf_auto_entry_points typically requires sf_generate");
            }
            
            // Warning: Both indoor and force_skybox (redundant but valid)
            if (HasFlag(value, "sf_indoors") && HasFlag(value, "sf_force_skybox"))
            {
                warnings.Add("sf_force_skybox with sf_indoors is unusual (forces skybox in indoor scene)");
            }
            
            return warnings;
        }
        
        /// <summary>
        /// Gets validation info as a formatted string
        /// </summary>
        public static string GetValidationInfo(BigInteger value)
        {
            var warnings = ValidateFlagCombination(value);
            
            if (warnings.Count == 0)
                return "✓ Flag combination is valid";
            
            return "⚠ Validation Warnings:\n" + string.Join("\n", warnings.Select(w => $"  • {w}"));
        }
        
        // HELPER METHODS
        
        /// <summary>
        /// Parses a flag string (hex or decimal) to BigInteger
        /// </summary>
        private static BigInteger ParseFlagString(string value)
        {
            if (string.IsNullOrEmpty(value))
                return 0;
            
            value = value.Trim();
            
            // Try parsing as hex
            if (value.StartsWith("0x") || value.StartsWith("0X"))
            {
                if (BigInteger.TryParse(value.Substring(2), 
                    System.Globalization.NumberStyles.HexNumber, null, out var hexResult))
                {
                    return hexResult;
                }
            }
            
            // Try parsing as decimal
            if (BigInteger.TryParse(value, out var decResult))
            {
                return decResult;
            }
            
            return 0;
        }
        
        /// <summary>
        /// Converts a BigInteger flag value to hex string
        /// </summary>
        public static string ToHexString(BigInteger value)
        {
            if (value == 0)
                return "0x0";
            
            return "0x" + value.ToString("X");
        }
        
        /// <summary>
        /// Formats a flag value for display (shows both decimal and hex)
        /// </summary>
        public static string FormatFlagValue(BigInteger value)
        {
            if (value == 0)
                return "0 (0x0)";
            
            return $"{value} ({ToHexString(value)})";
        }
    }
}
