using System;
using System.Collections.Generic;
using System.Numerics;

namespace MountAndBladeTools
{
    /// <summary>
    /// Mount & Blade party flags decoder.
    /// Handles icon extraction (bits 0-7, ENCODED) and party flags (bits 8+, BITWISE).
    /// </summary>
    public static class PartyFlagsDecoder
    {
        // Icon mask - bits 0-7 (ENCODED VALUE, not bitwise!)
        // Icons can be 0-255 (8 bits) - map icon IDs externally to your icon assets
        private const ulong ICON_MASK = 0x000000FF;
        
        // Pure bitwise party flags (bits 8 and above)
        private static readonly Dictionary<string, BigInteger> PartyFlags = new Dictionary<string, BigInteger>
        {
            // Basic flags
            { "pf_disabled", 0x00000100UL },
            { "pf_is_ship", 0x00000200UL },
            { "pf_is_static", 0x00000400UL },
            
            // Label sizes
            { "pf_label_small", 0x00000000UL },      // Note: This is 0, so it won't appear in decomposition
            { "pf_label_medium", 0x00001000UL },
            { "pf_label_large", 0x00002000UL },
            
            // Behavior flags
            { "pf_always_visible", 0x00004000UL },
            { "pf_default_behavior", 0x00010000UL },
            { "pf_auto_remove_in_town", 0x00020000UL },
            { "pf_quest_party", 0x00040000UL },
            { "pf_no_label", 0x00080000UL },
            { "pf_limit_members", 0x00100000UL },
            { "pf_hide_defenders", 0x00200000UL },
            { "pf_show_faction", 0x00400000UL },
            
            // Advanced flags
            { "pf_dont_attack_civilians", 0x02000000UL },
            { "pf_civilian", 0x04000000UL },
        };
        
        // Carries goods/gold (encoded in high bits - mostly deprecated)
        private const int CARRY_GOODS_BITS = 48;
        private const int CARRY_GOLD_BITS = 56;
        private const ulong CARRY_GOODS_MASK = 0x00FF000000000000UL;
        private const ulong CARRY_GOLD_MASK = 0xFF00000000000000UL;
        private const int CARRY_GOLD_MULTIPLIER = 20;

        /// <summary>
        /// Decodes party flags into icon ID and flag components
        /// </summary>
        public static PartyFlagsResult DecodeFlags(BigInteger value)
        {
            var result = new PartyFlagsResult
            {
                RawValue = value,
                ActiveFlags = new List<string>()
            };
            
            // 1. Extract icon ID (bits 0-7, ENCODED value 0-255)
            int iconId = (int)((ulong)(value & (BigInteger)ICON_MASK));
            result.IconId = iconId;
            
            // 2. Extract party flags (bits 8+, BITWISE)
            BigInteger flagsOnly = value & ~(BigInteger)ICON_MASK;
            
            foreach (var flag in PartyFlags)
            {
                if (flag.Value == 0)
                    continue; // Skip pf_label_small since it's 0
                
                if ((flagsOnly & flag.Value) != 0)
                {
                    result.ActiveFlags.Add(flag.Key);
                }
            }
            
            // 3. Check for carries_goods (deprecated but still encoded)
            ulong carryGoodsValue = ExtractCarryGoods(value);
            if (carryGoodsValue > 0)
            {
                result.CarryGoodsValue = (int)carryGoodsValue;
            }
            
            // 4. Check for carries_gold (deprecated but still encoded)
            ulong carryGoldValue = ExtractCarryGold(value);
            if (carryGoldValue > 0)
            {
                result.CarryGoldValue = (int)(carryGoldValue * CARRY_GOLD_MULTIPLIER);
            }
            
            return result;
        }
        
        /// <summary>
        /// Checks if a specific party flag is set
        /// </summary>
        public static bool HasFlag(BigInteger value, string flagName)
        {
            if (PartyFlags.ContainsKey(flagName))
            {
                BigInteger flagValue = PartyFlags[flagName];
                if (flagValue == 0) // Special case for pf_label_small
                    return (value & 0x00003000UL) == 0; // No medium or large label set
                
                return (value & flagValue) != 0;
            }
            return false;
        }
        
        /// <summary>
        /// Gets the icon ID from combined icon+flags value
        /// </summary>
        public static int GetIconId(BigInteger value)
        {
            return (int)((ulong)(value & (BigInteger)ICON_MASK));
        }
        
        /// <summary>
        /// Extracts carries_goods value (deprecated)
        /// </summary>
        private static ulong ExtractCarryGoods(BigInteger value)
        {
            return (ulong)((value & (BigInteger)CARRY_GOODS_MASK) >> CARRY_GOODS_BITS);
        }
        
        /// <summary>
        /// Extracts carries_gold value (deprecated)
        /// </summary>
        private static ulong ExtractCarryGold(BigInteger value)
        {
            return (ulong)((value & (BigInteger)CARRY_GOLD_MASK) >> CARRY_GOLD_BITS);
        }
        
        /// <summary>
        /// Builds a bitwise flags value from flag names (excludes icon)
        /// </summary>
        public static BigInteger EncodeFlags(IEnumerable<string> flagNames)
        {
            BigInteger result = 0;
            
            foreach (var flagName in flagNames)
            {
                if (PartyFlags.ContainsKey(flagName))
                {
                    result |= PartyFlags[flagName];
                }
            }
            
            return result;
        }
        
        /// <summary>
        /// Combines icon ID and flags into a single value
        /// </summary>
        public static BigInteger CombineIconAndFlags(int iconId, IEnumerable<string> flagNames)
        {
            // Ensure icon ID is in valid range (0-255)
            if (iconId < 0 || iconId > 255)
                throw new ArgumentOutOfRangeException(nameof(iconId), "Icon ID must be 0-255");
            
            BigInteger result = iconId; // Add icon (bits 0-7)
            result |= EncodeFlags(flagNames); // Add flags (bits 8+)
            
            return result;
        }
        
        public static Dictionary<string, BigInteger> GetAllFlags() => 
            new Dictionary<string, BigInteger>(PartyFlags);
    }
    
    /// <summary>
    /// Result of party flags decoding
    /// </summary>
    public class PartyFlagsResult
    {
        public BigInteger RawValue { get; set; }
        public int IconId { get; set; }  // Icon ID (0-255) - map to your icon assets externally
        public List<string> ActiveFlags { get; set; }
        public int? CarryGoodsValue { get; set; }
        public int? CarryGoldValue { get; set; }
        
        public override string ToString()
        {
            var parts = new List<string> { $"icon_{IconId}" };
            parts.AddRange(ActiveFlags);
            
            if (CarryGoodsValue.HasValue)
                parts.Add($"carries_goods({CarryGoodsValue})");
            
            if (CarryGoldValue.HasValue)
                parts.Add($"carries_gold({CarryGoldValue})");
            
            return string.Join("|", parts);
        }
    }
}