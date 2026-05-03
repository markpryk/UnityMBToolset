using System;
using System.Collections.Generic;
using System.Numerics;

namespace MountAndBlade.Data
{
    /// <summary>
    /// Decodes and encodes Mount & Blade Warband troop flags.
    /// Based on header_troops.py definitions.
    /// 
    /// IMPORTANT NOTES:
    /// - tf_mounted overrides tf_guarantee_ranged for troop categorization (cavalry > archer)
    /// - tf_unkillable and tf_no_capture_alive are DEPRECATED and non-functional in Warband
    /// - tf_undead requires additional setup in module_skins.py and proper meshes
    /// - Heroes (tf_hero) are always unkillable by default (health shown as percentage)
    /// - Guarantee flags only work if the item type exists in the troop's inventory
    /// </summary>
    public static class TroopFlagPropertyDecoder
    {
        // TROOP TYPE FLAGS (bits 0-3) - Mutually Exclusive
        private static readonly Dictionary<string, BigInteger> TroopTypes = new Dictionary<string, BigInteger>
        {
            { "tf_male", 0x0 },
            { "tf_female", 0x1 },
            { "tf_undead", 0x2 }
        };

        private static readonly BigInteger TROOP_TYPE_MASK = 0x0000000F;

        // GENERAL PROPERTY FLAGS
        private static readonly Dictionary<string, BigInteger> PropertyFlags = new Dictionary<string, BigInteger>
        {
            { "tf_hero", 0x00000010 },
            { "tf_inactive", 0x00000020 },
            { "tf_unkillable", 0x00000040 },
            { "tf_allways_fall_dead", 0x00000080 },
            { "tf_no_capture_alive", 0x00000100 },
            { "tf_mounted", 0x00000400 },
            { "tf_is_merchant", 0x00001000 },
            { "tf_randomize_face", 0x00008000 }
        };

        // GUARANTEE FLAGS (Starting Equipment)
        private static readonly Dictionary<string, BigInteger> GuaranteeFlags = new Dictionary<string, BigInteger>
        {
            { "tf_guarantee_boots", 0x00100000 },
            { "tf_guarantee_armor", 0x00200000 },
            { "tf_guarantee_helmet", 0x00400000 },
            { "tf_guarantee_gloves", 0x00800000 },
            { "tf_guarantee_horse", 0x01000000 },
            { "tf_guarantee_shield", 0x02000000 },
            { "tf_guarantee_ranged", 0x04000000 },
            { "tf_guarantee_polearm", 0x08000000 }
        };

        // SPECIAL FLAGS
        private static readonly Dictionary<string, BigInteger> SpecialFlags = new Dictionary<string, BigInteger>
        {
            { "tf_unmoveable_in_party_window", 0x10000000 }
        };

        // PUBLIC API - GETTERS

        public static Dictionary<string, BigInteger> GetTroopTypes() => new Dictionary<string, BigInteger>(TroopTypes);
        public static Dictionary<string, BigInteger> GetPropertyFlags() => new Dictionary<string, BigInteger>(PropertyFlags);
        public static Dictionary<string, BigInteger> GetGuaranteeFlags() => new Dictionary<string, BigInteger>(GuaranteeFlags);
        public static Dictionary<string, BigInteger> GetSpecialFlags() => new Dictionary<string, BigInteger>(SpecialFlags);

        /// <summary>
        /// Get all individual flags (excluding troop types)
        /// </summary>
        public static Dictionary<string, BigInteger> GetAllIndividualFlags()
        {
            var allFlags = new Dictionary<string, BigInteger>();
            
            foreach (var kvp in PropertyFlags)
                allFlags[kvp.Key] = kvp.Value;
            
            foreach (var kvp in GuaranteeFlags)
                allFlags[kvp.Key] = kvp.Value;
            
            foreach (var kvp in SpecialFlags)
                allFlags[kvp.Key] = kvp.Value;
            
            return allFlags;
        }

        // DECODING

        /// <summary>
        /// Decode all flags from a hex/numeric string value
        /// Returns: (troopType, activePropertyFlags, activeGuaranteeFlags, activeSpecialFlags)
        /// </summary>
        public static (
            string troopType,
            HashSet<string> propertyFlags,
            HashSet<string> guaranteeFlags,
            HashSet<string> specialFlags
        ) DecodeAllFlags(string value)
        {
            BigInteger parsedValue = ParseBigInteger(value);

            // 1. DECODE TROOP TYPE (bits 0-3)
            string troopType = DecodeTroopType(parsedValue);

            // 2. DECODE PROPERTY FLAGS
            var propertyFlags = DecodeFlags(parsedValue, PropertyFlags);

            // 3. DECODE GUARANTEE FLAGS
            var guaranteeFlags = DecodeFlags(parsedValue, GuaranteeFlags);

            // 4. DECODE SPECIAL FLAGS
            var specialFlags = DecodeFlags(parsedValue, SpecialFlags);

            return (troopType, propertyFlags, guaranteeFlags, specialFlags);
        }

        /// <summary>
        /// Decode the troop type from the value
        /// </summary>
        public static string DecodeTroopType(BigInteger value)
        {
            BigInteger typeValue = value & TROOP_TYPE_MASK;

            foreach (var kvp in TroopTypes)
            {
                if (kvp.Value == typeValue)
                    return kvp.Key;
            }

            return ""; // Default to empty if no match
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
        /// Format friendly names for display (removes tf_ prefix, capitalizes)
        /// </summary>
        public static string FormatFriendlyName(string flagName)
        {
            if (string.IsNullOrEmpty(flagName))
                return "";

            // Remove tf_ prefix
            string name = flagName;
            if (name.StartsWith("tf_"))
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
                // Troop Types
                case "tf_male":
                    return "Male troop (assigned to first skin entry)";
                case "tf_female":
                    return "Female troop (assigned to second skin entry)";
                case "tf_undead":
                    return "Third gender/race (requires work in module_skins.py and proper meshes)";

                // Properties
                case "tf_hero":
                    return "Hero/unique NPC. Heroes are unkillable (health as %), take full party stack, stay with player when defeated, and merchants use this flag";
                case "tf_inactive":
                    return "For non-NPC troops like chests/markers. Troops won't be dressed even with other flags set";
                case "tf_unkillable":
                    return "DEPRECATED - Not usable in Warband";
                case "tf_allways_fall_dead":
                    return "Troop always dies in battle (can't be knocked out and healed later). Works for both heroes and non-heroes";
                case "tf_no_capture_alive":
                    return "DEPRECATED - Old M&B flag, not usable in Warband";
                case "tf_mounted":
                    return "Troop's map speed determined by riding skill. Puts troop in cavalry category (overrides tf_guarantee_ranged)";
                case "tf_is_merchant":
                    return "Heroes won't equip items in inventory except original loadout. Items will show up for sale instead";
                case "tf_randomize_face":
                    return "Randomize face at game start (requires two face codes in troop tuple)";

                // Guarantees
                case "tf_guarantee_boots":
                    return "Guarantees troop will have boots if there are some in their loadout";
                case "tf_guarantee_armor":
                    return "Guarantees troop will have armor if there is one in their loadout";
                case "tf_guarantee_helmet":
                    return "Guarantees troop will have helmet if there is one in their loadout";
                case "tf_guarantee_gloves":
                    return "Guarantees troop will have gloves if there are some in their loadout";
                case "tf_guarantee_horse":
                    return "Guarantees troop will have horse if there is one in their loadout";
                case "tf_guarantee_shield":
                    return "Guarantees troop will have shield if there is one in their loadout";
                case "tf_guarantee_ranged":
                    return "Guarantees troop will have ranged weapon if there is one in their loadout. Puts troop in archer category";
                case "tf_guarantee_polearm":
                    return "Guarantees troop will have polearm if there is one in their loadout";

                // Special
                case "tf_unmoveable_in_party_window":
                    return "Unless troop is enemy prisoner, cannot garrison or trade to another party. Used for heroes";

                default:
                    return "";
            }
        }
    }
}
