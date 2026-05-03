using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;

namespace MountAndBlade.Data
{
    /// <summary>
    /// Decoder/Encoder for Mount & Blade item modifier bits.
    /// These correspond to imodbit_* values in header_item_modifiers.py
    /// Modifier bits are combinable bitwise flags that define which modifiers can appear on an item.
    /// </summary>
    public static class ItemModifierBitsDecoder
    {
        // MODIFIER BITS - These match header_item_modifiers.py
        // Each bit represents a possible modifier that can appear on an item
        private static readonly Dictionary<string, BigInteger> ModifierBits = new Dictionary<string, BigInteger>
        {
            // Base/Negative Modifiers
            { "imodbit_plain", 1UL },
            { "imodbit_cracked", 2UL },
            { "imodbit_rusty", 4UL },
            { "imodbit_bent", 8UL },
            { "imodbit_chipped", 16UL },
            { "imodbit_battered", 32UL },
            { "imodbit_poor", 64UL },
            { "imodbit_crude", 128UL },
            { "imodbit_old", 256UL },
            { "imodbit_cheap", 512UL },
            
            // Positive Weapon Modifiers
            { "imodbit_fine", 1024UL },
            { "imodbit_well_made", 2048UL },
            { "imodbit_sharp", 4096UL },
            { "imodbit_balanced", 8192UL },
            { "imodbit_tempered", 16384UL },
            { "imodbit_deadly", 32768UL },
            { "imodbit_exquisite", 65536UL },
            { "imodbit_masterwork", 131072UL },
            { "imodbit_heavy", 262144UL },
            { "imodbit_strong", 524288UL },
            { "imodbit_powerful", 1048576UL },
            
            // Armor Modifiers
            { "imodbit_tattered", 2097152UL },
            { "imodbit_ragged", 4194304UL },
            { "imodbit_rough", 8388608UL },
            { "imodbit_sturdy", 16777216UL },
            { "imodbit_thick", 33554432UL },
            { "imodbit_hardened", 67108864UL },
            { "imodbit_reinforced", 134217728UL },
            { "imodbit_superb", 268435456UL },
            { "imodbit_lordly", 536870912UL },
            
            // Horse Modifiers
            { "imodbit_lame", 1073741824UL },
            { "imodbit_swaybacked", 2147483648UL },
            { "imodbit_stubborn", 4294967296UL },
            { "imodbit_timid", 8589934592UL },
            { "imodbit_meek", 17179869184UL },
            { "imodbit_spirited", 34359738368UL },
            { "imodbit_champion", 68719476736UL },
            
            // Food Modifiers
            { "imodbit_fresh", 137438953472UL },
            { "imodbit_day_old", 274877906944UL },
            { "imodbit_two_day_old", 549755813888UL },
            { "imodbit_smelling", 1099511627776UL },
            { "imodbit_rotten", 2199023255552UL },
            
            // Ammunition Modifiers
            { "imodbit_large_bag", 4398046511104UL },
        };

        // PRESET MODIFIER COMBINATIONS - Common combinations from module_items.py
        public static readonly Dictionary<string, BigInteger> PresetCombinations = new Dictionary<string, BigInteger>
        {
            { "imodbits_none", 0 },
            { "imodbits_horse_basic", GetBitValue("imodbit_swaybacked") | GetBitValue("imodbit_lame") | GetBitValue("imodbit_spirited") | GetBitValue("imodbit_heavy") | GetBitValue("imodbit_stubborn") },
            { "imodbits_cloth", GetBitValue("imodbit_tattered") | GetBitValue("imodbit_ragged") | GetBitValue("imodbit_sturdy") | GetBitValue("imodbit_thick") | GetBitValue("imodbit_hardened") },
            { "imodbits_armor", GetBitValue("imodbit_rusty") | GetBitValue("imodbit_battered") | GetBitValue("imodbit_crude") | GetBitValue("imodbit_thick") | GetBitValue("imodbit_reinforced") | GetBitValue("imodbit_lordly") },
            { "imodbits_plate", GetBitValue("imodbit_cracked") | GetBitValue("imodbit_rusty") | GetBitValue("imodbit_battered") | GetBitValue("imodbit_crude") | GetBitValue("imodbit_thick") | GetBitValue("imodbit_reinforced") | GetBitValue("imodbit_lordly") },
            { "imodbits_polearm", GetBitValue("imodbit_cracked") | GetBitValue("imodbit_bent") | GetBitValue("imodbit_balanced") },
            { "imodbits_shield", GetBitValue("imodbit_cracked") | GetBitValue("imodbit_battered") | GetBitValue("imodbit_thick") | GetBitValue("imodbit_reinforced") },
            { "imodbits_sword", GetBitValue("imodbit_rusty") | GetBitValue("imodbit_chipped") | GetBitValue("imodbit_balanced") | GetBitValue("imodbit_tempered") },
            { "imodbits_sword_high", GetBitValue("imodbit_rusty") | GetBitValue("imodbit_chipped") | GetBitValue("imodbit_balanced") | GetBitValue("imodbit_tempered") | GetBitValue("imodbit_masterwork") },
            { "imodbits_axe", GetBitValue("imodbit_rusty") | GetBitValue("imodbit_chipped") | GetBitValue("imodbit_heavy") },
            { "imodbits_mace", GetBitValue("imodbit_rusty") | GetBitValue("imodbit_chipped") | GetBitValue("imodbit_heavy") },
            { "imodbits_pick", GetBitValue("imodbit_rusty") | GetBitValue("imodbit_chipped") | GetBitValue("imodbit_balanced") | GetBitValue("imodbit_heavy") },
            { "imodbits_bow", GetBitValue("imodbit_cracked") | GetBitValue("imodbit_bent") | GetBitValue("imodbit_strong") | GetBitValue("imodbit_masterwork") },
            { "imodbits_crossbow", GetBitValue("imodbit_cracked") | GetBitValue("imodbit_bent") | GetBitValue("imodbit_masterwork") },
            { "imodbits_missile", GetBitValue("imodbit_bent") | GetBitValue("imodbit_large_bag") },
            { "imodbits_thrown", GetBitValue("imodbit_bent") | GetBitValue("imodbit_heavy") | GetBitValue("imodbit_balanced") | GetBitValue("imodbit_large_bag") },
            { "imodbits_thrown_minus_heavy", GetBitValue("imodbit_bent") | GetBitValue("imodbit_balanced") | GetBitValue("imodbit_large_bag") },
            { "imodbits_horse_good", GetBitValue("imodbit_spirited") | GetBitValue("imodbit_heavy") },
            { "imodbits_good", GetBitValue("imodbit_sturdy") | GetBitValue("imodbit_thick") | GetBitValue("imodbit_hardened") | GetBitValue("imodbit_reinforced") },
            { "imodbits_bad", GetBitValue("imodbit_rusty") | GetBitValue("imodbit_chipped") | GetBitValue("imodbit_tattered") | GetBitValue("imodbit_ragged") | GetBitValue("imodbit_cracked") | GetBitValue("imodbit_bent") },
        };

        // MODIFIER CATEGORIES - For UI grouping
        public static readonly Dictionary<string, string[]> ModifierCategories = new Dictionary<string, string[]>
        {
            { "Negative/Damaged", new[] { "imodbit_plain", "imodbit_cracked", "imodbit_rusty", "imodbit_bent", "imodbit_chipped", "imodbit_battered", "imodbit_poor", "imodbit_crude", "imodbit_old", "imodbit_cheap" } },
            { "Weapon Quality", new[] { "imodbit_fine", "imodbit_well_made", "imodbit_sharp", "imodbit_balanced", "imodbit_tempered", "imodbit_deadly", "imodbit_exquisite", "imodbit_masterwork", "imodbit_heavy", "imodbit_strong", "imodbit_powerful" } },
            { "Armor Quality", new[] { "imodbit_tattered", "imodbit_ragged", "imodbit_rough", "imodbit_sturdy", "imodbit_thick", "imodbit_hardened", "imodbit_reinforced", "imodbit_superb", "imodbit_lordly" } },
            { "Horse", new[] { "imodbit_lame", "imodbit_swaybacked", "imodbit_stubborn", "imodbit_timid", "imodbit_meek", "imodbit_spirited", "imodbit_champion" } },
            { "Food", new[] { "imodbit_fresh", "imodbit_day_old", "imodbit_two_day_old", "imodbit_smelling", "imodbit_rotten" } },
            { "Ammunition", new[] { "imodbit_large_bag" } },
        };

        // DECODING

        /// <summary>
        /// Decodes modifier bits value into a list of active modifier names.
        /// </summary>
        public static List<string> DecodeModifiers(BigInteger value)
        {
            var result = new List<string>();

            foreach (var mod in ModifierBits.OrderBy(x => x.Value))
            {
                if ((value & mod.Value) != 0)
                {
                    result.Add(mod.Key);
                }
            }

            return result;
        }

        /// <summary>
        /// Decodes modifier bits and returns results grouped by category.
        /// </summary>
        public static Dictionary<string, List<string>> DecodeModifiersByCategory(BigInteger value)
        {
            var result = new Dictionary<string, List<string>>();
            var activeModifiers = DecodeModifiers(value);

            foreach (var category in ModifierCategories)
            {
                var categoryModifiers = activeModifiers.Where(m => category.Value.Contains(m)).ToList();
                if (categoryModifiers.Count > 0)
                {
                    result[category.Key] = categoryModifiers;
                }
            }

            return result;
        }

        /// <summary>
        /// Checks if a specific modifier bit is set.
        /// </summary>
        public static bool HasModifier(BigInteger value, string modifierName)
        {
            if (!ModifierBits.TryGetValue(modifierName, out var bitValue))
                return false;

            return (value & bitValue) != 0;
        }

        /// <summary>
        /// Finds which preset combination matches the value exactly.
        /// Returns null if no exact match found.
        /// </summary>
        public static string FindMatchingPreset(BigInteger value)
        {
            foreach (var preset in PresetCombinations)
            {
                if (preset.Value == value)
                    return preset.Key;
            }
            return null;
        }

        /// <summary>
        /// Gets the display name for a modifier (removes imodbit_ prefix and formats).
        /// </summary>
        public static string GetDisplayName(string modifierName)
        {
            if (modifierName.StartsWith("imodbit_"))
            {
                var name = modifierName.Substring(8);
                // Convert snake_case to Title Case
                return string.Join(" ", name.Split('_').Select(s => 
                    char.ToUpper(s[0]) + s.Substring(1)));
            }
            return modifierName;
        }

        // ENCODING

        /// <summary>
        /// Encodes a list of modifier names into a BigInteger value.
        /// </summary>
        public static BigInteger EncodeModifiers(IEnumerable<string> modifierNames)
        {
            BigInteger result = 0;

            foreach (var name in modifierNames)
            {
                if (ModifierBits.TryGetValue(name, out var bitValue))
                {
                    result |= bitValue;
                }
            }

            return result;
        }

        /// <summary>
        /// Encodes modifiers from a dictionary of modifier states.
        /// </summary>
        public static BigInteger EncodeModifiers(Dictionary<string, bool> modifierStates)
        {
            return EncodeModifiers(modifierStates.Where(kvp => kvp.Value).Select(kvp => kvp.Key));
        }

        /// <summary>
        /// Adds a modifier to an existing value.
        /// </summary>
        public static BigInteger AddModifier(BigInteger currentValue, string modifierName)
        {
            if (ModifierBits.TryGetValue(modifierName, out var bitValue))
            {
                return currentValue | bitValue;
            }
            return currentValue;
        }

        /// <summary>
        /// Removes a modifier from an existing value.
        /// </summary>
        public static BigInteger RemoveModifier(BigInteger currentValue, string modifierName)
        {
            if (ModifierBits.TryGetValue(modifierName, out var bitValue))
            {
                return currentValue & ~bitValue;
            }
            return currentValue;
        }

        /// <summary>
        /// Toggles a modifier in an existing value.
        /// </summary>
        public static BigInteger ToggleModifier(BigInteger currentValue, string modifierName)
        {
            if (ModifierBits.TryGetValue(modifierName, out var bitValue))
            {
                return currentValue ^ bitValue;
            }
            return currentValue;
        }

        /// <summary>
        /// Sets a modifier to a specific state.
        /// </summary>
        public static BigInteger SetModifier(BigInteger currentValue, string modifierName, bool enabled)
        {
            return enabled 
                ? AddModifier(currentValue, modifierName) 
                : RemoveModifier(currentValue, modifierName);
        }

        // UTILITIES

        /// <summary>
        /// Gets the bit value for a specific modifier name.
        /// </summary>
        public static BigInteger GetBitValue(string modifierName)
        {
            return ModifierBits.TryGetValue(modifierName, out var value) ? value : 0;
        }

        /// <summary>
        /// Gets a copy of all modifier bits dictionary.
        /// </summary>
        public static Dictionary<string, BigInteger> GetModifierBits()
        {
            return new Dictionary<string, BigInteger>(ModifierBits);
        }

        /// <summary>
        /// Gets all modifier names.
        /// </summary>
        public static IEnumerable<string> GetAllModifierNames()
        {
            return ModifierBits.Keys;
        }

        /// <summary>
        /// Gets all modifier names in a specific category.
        /// </summary>
        public static IEnumerable<string> GetModifiersInCategory(string category)
        {
            return ModifierCategories.TryGetValue(category, out var modifiers) 
                ? modifiers 
                : Enumerable.Empty<string>();
        }

        /// <summary>
        /// Gets all category names.
        /// </summary>
        public static IEnumerable<string> GetAllCategories()
        {
            return ModifierCategories.Keys;
        }

        /// <summary>
        /// Creates a state dictionary with all modifiers initialized based on a value.
        /// </summary>
        public static Dictionary<string, bool> CreateStateDict(BigInteger value)
        {
            var result = new Dictionary<string, bool>();
            foreach (var mod in ModifierBits)
            {
                result[mod.Key] = (value & mod.Value) != 0;
            }
            return result;
        }

        /// <summary>
        /// Parses a string value to BigInteger, handling various formats.
        /// </summary>
        public static bool TryParseValue(string valueString, out BigInteger result)
        {
            if (string.IsNullOrWhiteSpace(valueString))
            {
                result = 0;
                return true;
            }

            valueString = valueString.Trim();

            // Handle hex format
            if (valueString.StartsWith("0x") || valueString.StartsWith("0X"))
            {
                return BigInteger.TryParse(valueString.Substring(2), 
                    NumberStyles.HexNumber, null, out result);
            }

            // Handle decimal format
            return BigInteger.TryParse(valueString, out result);
        }
        
        /// <summary>
        /// Parses a string value and extracts the first (lowest) modifier bit found.
        /// Returns the modifier name and the remaining value after removing that bit.
        /// </summary>
        public static bool TryExtractFirstModifier(string valueString, out string modifierName, out BigInteger remainingValue)
        {
            modifierName = null;
            remainingValue = 0;

            if (!TryParseValue(valueString, out BigInteger value))
                return false;

            return TryExtractFirstModifier(value, out modifierName, out remainingValue);
        }
        
        /// <summary>
        /// Extracts the first (lowest) modifier bit from a value.
        /// Returns the modifier name and the remaining value after removing that bit.
        /// </summary>
        public static bool TryExtractFirstModifier(BigInteger value, out string modifierName, out BigInteger remainingValue)
        {
            modifierName = "0";
            remainingValue = value;

            if (value == 0)
                return false;

            // Find the lowest set bit by isolating it: value & -value
            BigInteger lowestBit = value & -value;

            // Find which modifier this bit represents
            foreach (var mod in ModifierBits)
            {
                if (mod.Value == lowestBit)
                {
                    modifierName = mod.Key;
                    remainingValue = value & ~lowestBit; // Remove this bit from the value
                    return true;
                }
            }

            return false;
        }


        public static Dictionary<string, BigInteger> GetAllModifiers() => 
            new Dictionary<string, BigInteger>(ModifierBits);

    }
}
