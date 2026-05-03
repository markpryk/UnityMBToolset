using System;
using System.Collections.Generic;
using System.Numerics;

namespace MountAndBlade.Data
{
    /// <summary>
    /// Decodes and encodes Mount & Blade Warband faction flags.
    /// Based on header_factions.py definitions.
    /// 
    /// IMPORTANT NOTES:
    /// - ff_always_hide_label hides faction name in popup tooltips (used by Innocents/Merchants)
    /// - max_player_rating encodes maximum faction reputation (0-100 scale)
    /// - Faction coherence ranges from 0.0 to 1.0 (relation between faction members)
    /// - Faction relations range from -1.0 (war) to 1.0 (allied), with 0.0 being neutral
    /// - Maximum of 128 factions (0-127) - hard-coded engine limit
    /// </summary>
    public static class FactionFlagPropertyDecoder
    {
        // FACTION FLAGS
        private static readonly Dictionary<string, BigInteger> FactionFlags = new Dictionary<string, BigInteger>
        {
            { "ff_always_hide_label", 0x00000001 }
        };

        // MAX RATING CONSTANTS
        private const int FF_MAX_RATING_BITS = 8;
        private static readonly BigInteger FF_MAX_RATING_MASK = 0x0000FF00;

        // PUBLIC API - GETTERS
        
        public static Dictionary<string, BigInteger> GetFactionFlags() => new Dictionary<string, BigInteger>(FactionFlags);

        // DECODING
        
        /// <summary>
        /// Decode all flags from a hex/numeric string value
        /// Returns: (activeFlags, maxPlayerRating)
        /// </summary>
        public static (HashSet<string> flags, int? maxPlayerRating) DecodeAllFlags(string value)
        {
            BigInteger parsedValue = ParseBigInteger(value);

            // 1. DECODE STANDARD FLAGS
            var flags = DecodeFlags(parsedValue, FactionFlags);

            // 2. DECODE MAX PLAYER RATING
            int? maxRating = DecodeMaxPlayerRating(parsedValue);

            return (flags, maxRating);
        }

        /// <summary>
        /// Decode which flags are active from a given dictionary
        /// </summary>
        private static HashSet<string> DecodeFlags(BigInteger value, Dictionary<string, BigInteger> flagDict)
        {
            var activeFlags = new HashSet<string>();

            foreach (var kvp in flagDict)
            {
                if ((value & kvp.Value) != 0)
                {
                    activeFlags.Add(kvp.Key);
                }
            }

            return activeFlags;
        }

        /// <summary>
        /// Decode max player rating from flags value
        /// Returns null if no max rating is encoded
        /// </summary>
        public static int? DecodeMaxPlayerRating(BigInteger value)
        {
            BigInteger ratingBits = (value & FF_MAX_RATING_MASK) >> FF_MAX_RATING_BITS;
            
            if (ratingBits == 0)
                return null;

            // Reverse the encoding: max_player_rating(rating) = (100 - rating) << 8
            // So: rating = 100 - (flags >> 8)
            int rating = 100 - (int)ratingBits;
            return rating;
        }

        // ENCODING
        
        /// <summary>
        /// Encode max player rating value (0-100) into faction flags
        /// Python equivalent: max_player_rating(rating) = (100 - rating) << 8
        /// </summary>
        public static BigInteger EncodeMaxPlayerRating(int rating)
        {
            int r = 100 - rating;
            BigInteger encoded = (BigInteger)r << FF_MAX_RATING_BITS;
            return encoded & FF_MAX_RATING_MASK;
        }

        /// <summary>
        /// Encode all flags and max rating into a single value
        /// </summary>
        public static BigInteger EncodeAllFlags(HashSet<string> activeFlags, int? maxPlayerRating = null)
        {
            BigInteger result = 0;

            // Add standard flags
            foreach (var flag in activeFlags)
            {
                if (FactionFlags.TryGetValue(flag, out BigInteger flagValue))
                {
                    result |= flagValue;
                }
            }

            // Add max rating if specified
            if (maxPlayerRating.HasValue)
            {
                result |= EncodeMaxPlayerRating(maxPlayerRating.Value);
            }

            return result;
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
        /// Format friendly names for display (removes ff_ prefix, capitalizes)
        /// </summary>
        public static string FormatFriendlyName(string flagName)
        {
            if (string.IsNullOrEmpty(flagName))
                return "";

            // Remove ff_ prefix
            string name = flagName;
            if (name.StartsWith("ff_"))
                name = name.Substring(3);

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
        /// Get description/tooltip for a flag
        /// </summary>
        public static string GetFlagDescription(string flagName)
        {
            switch (flagName)
            {
                case "ff_always_hide_label":
                    return "Hides faction name when hovering over party icons in popup tooltips. Used by Innocents and Merchants factions";

                default:
                    return "";
            }
        }

        /// <summary>
        /// Validate faction coherence value (should be 0.0 to 1.0)
        /// </summary>
        public static bool IsValidCoherence(float coherence)
        {
            return coherence >= 0.0f && coherence <= 1.0f;
        }

        /// <summary>
        /// Validate faction relation value (should be -1.0 to 1.0)
        /// </summary>
        public static bool IsValidRelation(float relation)
        {
            return relation >= -1.0f && relation <= 1.0f;
        }

        /// <summary>
        /// Format flags value for display (includes hex and decoded components)
        /// </summary>
        public static string FormatFlagsValue(string flagsValue)
        {
            if (string.IsNullOrWhiteSpace(flagsValue) || flagsValue == "0")
                return "0 (No flags)";

            var (flags, maxRating) = DecodeAllFlags(flagsValue);
            var parts = new List<string>();

            foreach (var flag in flags)
            {
                parts.Add(flag);
            }

            if (maxRating.HasValue)
            {
                parts.Add($"max_player_rating({maxRating.Value})");
            }

            BigInteger numValue = ParseBigInteger(flagsValue);
            string hexValue = numValue.ToString("X");
            
            if (parts.Count > 0)
                return $"0x{hexValue} ({string.Join(" | ", parts)})";
            else
                return $"0x{hexValue}";
        }
    }
}