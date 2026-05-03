using System;
using System.Collections.Generic;
using System.Numerics;

namespace MountAndBlade.Data
{
    /// <summary>
    /// Decodes and encodes Mount & Blade Warband skin flags.
    /// Based on header_skins.py definitions.
    /// 
    /// IMPORTANT NOTES:
    /// - Skin flags determine which morph key/frame to use for body transformations
    /// - Only values 0x1 through 0x7 are valid (morph keys 10-70)
    /// - Morph keys work only on body armors (boots and helmet only with WSE)
    /// - Different races should use different vertex animation frames
    /// - Female armors typically use morph_key_10 for narrower shoulders and breasts
    /// </summary>
    public static class SkinFlagPropertyDecoder
    {
        // SKIN FLAGS - Morph Key Selection
        private static readonly Dictionary<string, BigInteger> SkinFlags = new Dictionary<string, BigInteger>
        {
            { "skf_use_morph_key_10", 0x00000001 },
            { "skf_use_morph_key_20", 0x00000002 },
            { "skf_use_morph_key_30", 0x00000003 },
            { "skf_use_morph_key_40", 0x00000004 },
            { "skf_use_morph_key_50", 0x00000005 },
            { "skf_use_morph_key_60", 0x00000006 },
            { "skf_use_morph_key_70", 0x00000007 }
        };

        private static readonly BigInteger SKIN_FLAG_MASK = 0x000000FF;

        // PUBLIC API - GETTERS

        public static Dictionary<string, BigInteger> GetSkinFlags() => new Dictionary<string, BigInteger>(SkinFlags);

        // DECODING

        /// <summary>
        /// Decode skin flag from a hex/numeric string value
        /// Returns the flag name (e.g., "skf_use_morph_key_10") or empty string if none/invalid
        /// </summary>
        public static string DecodeSkinFlag(string value)
        {
            BigInteger parsedValue = ParseBigInteger(value);
            return DecodeSkinFlag(parsedValue);
        }

        /// <summary>
        /// Decode skin flag from a BigInteger value
        /// </summary>
        public static string DecodeSkinFlag(BigInteger value)
        {
            BigInteger flagValue = value & SKIN_FLAG_MASK;

            // Check if it's a valid skin flag
            foreach (var kvp in SkinFlags)
            {
                if (kvp.Value == flagValue)
                    return kvp.Key;
            }

            // If value is 0, it means no morph key is used (default)
            if (flagValue == 0)
                return "None (0)";

            // Invalid flag value
            return $"Unknown ({flagValue})";
        }

        /// <summary>
        /// Decode skin flag from an integer value
        /// </summary>
        public static string DecodeSkinFlag(int value)
        {
            return DecodeSkinFlag(new BigInteger(value));
        }

        /// <summary>
        /// Get the morph key number from the flag (10, 20, 30, etc.)
        /// Returns 0 if no morph key is used
        /// </summary>
        public static int GetMorphKeyNumber(BigInteger value)
        {
            BigInteger flagValue = value & SKIN_FLAG_MASK;

            if (flagValue == 0) return 0;
            if (flagValue == 0x1) return 10;
            if (flagValue == 0x2) return 20;
            if (flagValue == 0x3) return 30;
            if (flagValue == 0x4) return 40;
            if (flagValue == 0x5) return 50;
            if (flagValue == 0x6) return 60;
            if (flagValue == 0x7) return 70;

            return -1; // Invalid
        }

        /// <summary>
        /// Get the morph key number from an integer value
        /// </summary>
        public static int GetMorphKeyNumber(int value)
        {
            return GetMorphKeyNumber(new BigInteger(value));
        }

        // ENCODING

        /// <summary>
        /// Encode a skin flag name to its numeric value
        /// </summary>
        public static BigInteger EncodeSkinFlag(string flagName)
        {
            if (string.IsNullOrWhiteSpace(flagName) || flagName == "None (0)")
                return 0;

            if (SkinFlags.TryGetValue(flagName, out BigInteger value))
                return value;

            return 0; // Default to no flag
        }

        /// <summary>
        /// Encode a morph key number (10, 20, 30, etc.) to its flag value
        /// </summary>
        public static BigInteger EncodeMorphKeyNumber(int morphKeyNumber)
        {
            switch (morphKeyNumber)
            {
                case 0: return 0;
                case 10: return 0x1;
                case 20: return 0x2;
                case 30: return 0x3;
                case 40: return 0x4;
                case 50: return 0x5;
                case 60: return 0x6;
                case 70: return 0x7;
                default: return 0;
            }
        }

        // HELPER METHODS

        private static BigInteger ParseBigInteger(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return 0;

            input = input.Trim();

            if (input.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            {
                return BigInteger.Parse(
                    input.Substring(2),
                    System.Globalization.NumberStyles.HexNumber);
            }

            return BigInteger.Parse(input);
        }

        /// <summary>
        /// Format friendly name for display (removes skf_ prefix, capitalizes)
        /// </summary>
        public static string FormatFriendlyName(string flagName)
        {
            if (string.IsNullOrEmpty(flagName))
                return "";

            if (flagName == "None (0)")
                return "No Morph Key";

            // Remove skf_ prefix
            string name = flagName;
            if (name.StartsWith("skf_"))
                name = name.Substring(4);

            // Split by underscores and capitalize
            var parts = name.Split('_');
            for (int i = 0; i < parts.Length; i++)
            {
                if (parts[i].Length > 0)
                {
                    parts[i] = char.ToUpper(parts[i][0]) + parts[i].Substring(1);
                }
            }

            return string.Join(" ", parts);
        }

        /// <summary>
        /// Get description/tooltip for a skin flag
        /// </summary>
        public static string GetFlagDescription(string flagName)
        {
            if (flagName == "None (0)")
                return "No morph key applied. Uses default mesh without vertex animation transformations.";

            switch (flagName)
            {
                case "skf_use_morph_key_10":
                    return "Uses morph key 10 (time=10 in vertex animation). Typically used for female body transformations (narrower shoulders, breasts).";
                
                case "skf_use_morph_key_20":
                    return "Uses morph key 20 (time=20 in vertex animation). Can be used for custom race body transformations.";
                
                case "skf_use_morph_key_30":
                    return "Uses morph key 30 (time=30 in vertex animation). Can be used for custom race body transformations.";
                
                case "skf_use_morph_key_40":
                    return "Uses morph key 40 (time=40 in vertex animation). Can be used for custom race body transformations.";
                
                case "skf_use_morph_key_50":
                    return "Uses morph key 50 (time=50 in vertex animation). Can be used for custom race body transformations.";
                
                case "skf_use_morph_key_60":
                    return "Uses morph key 60 (time=60 in vertex animation). Can be used for custom race body transformations.";
                
                case "skf_use_morph_key_70":
                    return "Uses morph key 70 (time=70 in vertex animation). Can be used for custom race body transformations.";
                
                default:
                    return "";
            }
        }

        /// <summary>
        /// Get general information about skin flags
        /// </summary>
        public static string GetGeneralInfo()
        {
            return "Skin flags determine which morph key/frame is used for body transformations.\n\n" +
                   "• Only works on body armors (boots/helmets require WSE)\n" +
                   "• Morph keys are vertex animation frames in OpenBRF\n" +
                   "• Female armors typically use morph_key_10\n" +
                   "• Custom races should use different morph keys\n" +
                   "• Maximum 8 morph keys available (10-70)";
        }
    }
}
