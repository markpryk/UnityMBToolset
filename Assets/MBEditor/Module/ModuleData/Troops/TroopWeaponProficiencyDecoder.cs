using System;
using System.Collections.Generic;
using System.Numerics;

namespace MountAndBlade.Data
{
    /// <summary>
    /// Decodes and encodes Mount & Blade Warband weapon proficiencies.
    /// Handles: One-handed, Two-handed, Polearm, Archery, Crossbow, Throwing, Firearm.
    /// Based on header_troops.py definitions.
    /// 
    /// Encoding format (each proficiency uses 10 bits, max value 1023):
    /// - One-handed: bits 0-9   (shift 0)
    /// - Two-handed: bits 10-19 (shift 10)
    /// - Polearm:    bits 20-29 (shift 20)
    /// - Archery:    bits 30-39 (shift 30)
    /// - Crossbow:   bits 40-49 (shift 40)
    /// - Throwing:   bits 50-59 (shift 50)
    /// - Firearm:    bits 60-69 (shift 60)
    /// 
    /// Note: Maximum usable proficiency in-game is 699 (hard cap)
    /// </summary>
    public static class TroopWeaponProficiencyDecoder
    {
        // Proficiency types
        public enum ProficiencyType
        {
            OneHanded = 0,
            TwoHanded = 1,
            Polearm = 2,
            Archery = 3,
            Crossbow = 4,
            Throwing = 5,
            Firearm = 6
        }

        // Bit shifts for each proficiency
        private const int ONE_HANDED_SHIFT = 0;
        private const int TWO_HANDED_SHIFT = 10;
        private const int POLEARM_SHIFT = 20;
        private const int ARCHERY_SHIFT = 30;
        private const int CROSSBOW_SHIFT = 40;
        private const int THROWING_SHIFT = 50;
        private const int FIREARM_SHIFT = 60;

        // Mask for extracting 10-bit proficiency values
        private const long PROFICIENCY_MASK = 0x3FF; // 1023 (10 bits)

        // Valid proficiency range
        public const int MIN_PROFICIENCY = 0;
        public const int MAX_PROFICIENCY = 699; // Game enforced cap
        public const int MAX_TECHNICAL_VALUE = 1023; // 10-bit max, but game caps at 699
        public const int DEFAULT_PROFICIENCY = 0;

        // DECODING

        /// <summary>
        /// Decode all weapon proficiencies from a hex/numeric string value.
        /// Returns dictionary with proficiency type as key
        /// </summary>
        public static Dictionary<ProficiencyType, int> DecodeAllProficiencies(string value)
        {
            BigInteger parsedValue = ParseBigInteger(value);

            return new Dictionary<ProficiencyType, int>
            {
                { ProficiencyType.OneHanded, DecodeProficiency(parsedValue, ONE_HANDED_SHIFT) },
                { ProficiencyType.TwoHanded, DecodeProficiency(parsedValue, TWO_HANDED_SHIFT) },
                { ProficiencyType.Polearm, DecodeProficiency(parsedValue, POLEARM_SHIFT) },
                { ProficiencyType.Archery, DecodeProficiency(parsedValue, ARCHERY_SHIFT) },
                { ProficiencyType.Crossbow, DecodeProficiency(parsedValue, CROSSBOW_SHIFT) },
                { ProficiencyType.Throwing, DecodeProficiency(parsedValue, THROWING_SHIFT) },
                { ProficiencyType.Firearm, DecodeProficiency(parsedValue, FIREARM_SHIFT) }
            };
        }

        /// <summary>
        /// Decode a single proficiency value from the given shift position
        /// </summary>
        private static int DecodeProficiency(BigInteger value, int shift)
        {
            long extracted = (long)((value >> shift) & PROFICIENCY_MASK);
            return (int)extracted;
        }

        // ENCODING

        /// <summary>
        /// Encode all weapon proficiencies into a single BigInteger value
        /// </summary>
        public static BigInteger EncodeAllProficiencies(Dictionary<ProficiencyType, int> proficiencies)
        {
            BigInteger encoded = 0;

            foreach (var kvp in proficiencies)
            {
                int shift = GetShiftForProficiency(kvp.Key);
                int value = Math.Clamp(kvp.Value, MIN_PROFICIENCY, MAX_PROFICIENCY);
                
                encoded |= (BigInteger)value << shift;
            }

            return encoded;
        }

        private static int GetShiftForProficiency(ProficiencyType type)
        {
            switch (type)
            {
                case ProficiencyType.OneHanded: return ONE_HANDED_SHIFT;
                case ProficiencyType.TwoHanded: return TWO_HANDED_SHIFT;
                case ProficiencyType.Polearm: return POLEARM_SHIFT;
                case ProficiencyType.Archery: return ARCHERY_SHIFT;
                case ProficiencyType.Crossbow: return CROSSBOW_SHIFT;
                case ProficiencyType.Throwing: return THROWING_SHIFT;
                case ProficiencyType.Firearm: return FIREARM_SHIFT;
                default: return 0;
            }
        }

        // VALIDATION & FORMULAS

        /// <summary>
        /// Calculate expected weapon proficiency points based on level and agility.
        /// Formula: (Level * weapon_points_per_level) + (5 * AGI) + start_points
        /// where weapon_points_per_level = 10 in Native
        /// </summary>
        public static int CalculateExpectedProficiencyPoints(int level, int agility, int startPoints = 0)
        {
            int weaponPointsPerLevel = 10; // Native default
            return (level * weaponPointsPerLevel) + (5 * agility) + startPoints;
        }

        /// <summary>
        /// Calculate actual proficiency points from current values
        /// </summary>
        public static int CalculateActualProficiencyPoints(Dictionary<ProficiencyType, int> proficiencies)
        {
            int total = 0;
            foreach (var kvp in proficiencies)
            {
                total += kvp.Value;
            }
            return total;
        }

        /// <summary>
        /// Get the weapon master skill cap for a given proficiency level.
        /// Cap = 60 + (Weapon Master skill * 40)
        /// </summary>
        public static int GetWeaponMasterCap(int weaponMasterSkill)
        {
            return 60 + (weaponMasterSkill * 40);
        }

        /// <summary>
        /// Get proficiency points validation message
        /// </summary>
        public static string GetProficiencyPointsValidation(
            Dictionary<ProficiencyType, int> proficiencies, 
            int level, 
            int agility,
            int startPoints = 0)
        {
            int expected = CalculateExpectedProficiencyPoints(level, agility, startPoints);
            int actual = CalculateActualProficiencyPoints(proficiencies);
            
            // Note: Proficiencies don't auto-assign like attributes, so we just inform
            return $"Proficiency points: {actual} (suggested ~{expected} for level {level}, AGI {agility})";
        }

        /// <summary>
        /// Get proficiency description
        /// </summary>
        public static string GetProficiencyDescription(ProficiencyType type)
        {
            switch (type)
            {
                case ProficiencyType.OneHanded:
                    return "Swords, axes, maces, and other one-handed weapons. Improves damage, speed, and accuracy";
                
                case ProficiencyType.TwoHanded:
                    return "Two-handed swords, axes, and other large melee weapons. Improves damage, speed, and accuracy";
                
                case ProficiencyType.Polearm:
                    return "Spears, pikes, and other polearms (can be one or two-handed). Improves damage, speed, and accuracy";
                
                case ProficiencyType.Archery:
                    return "Bows and arrows. Improves accuracy and drawing speed. Affected by Power Draw skill";
                
                case ProficiencyType.Crossbow:
                    return "Crossbows and bolts. Improves accuracy and reload speed. Easier to use than bows";
                
                case ProficiencyType.Throwing:
                    return "Throwing weapons (javelins, throwing axes, stones). Improves accuracy and damage. Affected by Power Throw skill";
                
                case ProficiencyType.Firearm:
                    return "Firearms (muskets, pistols). Improves accuracy and reload speed. Only in mods or WFaS";
                
                default:
                    return "";
            }
        }

        /// <summary>
        /// Get friendly name for proficiency type
        /// </summary>
        public static string GetFriendlyName(ProficiencyType type)
        {
            switch (type)
            {
                case ProficiencyType.OneHanded: return "One Handed";
                case ProficiencyType.TwoHanded: return "Two Handed";
                case ProficiencyType.Polearm: return "Polearm";
                case ProficiencyType.Archery: return "Archery";
                case ProficiencyType.Crossbow: return "Crossbow";
                case ProficiencyType.Throwing: return "Throwing";
                case ProficiencyType.Firearm: return "Firearm";
                default: return type.ToString();
            }
        }

        /// <summary>
        /// Get internal name for proficiency type (wp_xxx format)
        /// </summary>
        public static string GetInternalName(ProficiencyType type)
        {
            switch (type)
            {
                case ProficiencyType.OneHanded: return "wp_one_handed";
                case ProficiencyType.TwoHanded: return "wp_two_handed";
                case ProficiencyType.Polearm: return "wp_polearm";
                case ProficiencyType.Archery: return "wp_archery";
                case ProficiencyType.Crossbow: return "wp_crossbow";
                case ProficiencyType.Throwing: return "wp_throwing";
                case ProficiencyType.Firearm: return "wp_firearm";
                default: return type.ToString();
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
        /// Format a BigInteger value to hex string
        /// </summary>
        public static string FormatToHex(BigInteger value)
        {
            if (value == 0)
                return "0";

            return "0x" + value.ToString("X");
        }

        /// <summary>
        /// Create a formatted proficiency string for display 
        /// (e.g., "wp_one_handed(120)|wp_two_handed(80)|wp_polearm(60)")
        /// This is the Python module system format
        /// </summary>
        public static string FormatProficienciesForDisplay(Dictionary<ProficiencyType, int> proficiencies, bool debugMode = false)
        {
            List<string> parts = new List<string>();

            foreach (var type in new[] { 
                ProficiencyType.OneHanded, 
                ProficiencyType.TwoHanded, 
                ProficiencyType.Polearm, 
                ProficiencyType.Archery, 
                ProficiencyType.Crossbow, 
                ProficiencyType.Throwing, 
                ProficiencyType.Firearm })
            {
                if (proficiencies.TryGetValue(type, out int value) && value > 0)
                {
                    string name = debugMode ? GetInternalName(type) : GetFriendlyName(type);
                    parts.Add($"{name}({value})");
                }
            }

            return string.Join("|", parts);
        }

        /// <summary>
        /// Parse a formatted proficiency string (e.g., "wp_one_handed(120)|wp_archery(80)")
        /// Returns false if parsing fails
        /// </summary>
        public static bool TryParseFormattedString(string formatted, 
            out Dictionary<ProficiencyType, int> proficiencies)
        {
            proficiencies = new Dictionary<ProficiencyType, int>();

            if (string.IsNullOrWhiteSpace(formatted))
                return false;

            try
            {
                string[] parts = formatted.Split('|');
                
                foreach (string part in parts)
                {
                    string trimmed = part.Trim();
                    
                    // Match pattern like "wp_one_handed(120)" or "One Handed(120)"
                    int parenIndex = trimmed.IndexOf('(');
                    if (parenIndex < 0 || !trimmed.EndsWith(")"))
                        continue;

                    string name = trimmed.Substring(0, parenIndex).Trim().ToLower().Replace(" ", "_");
                    string valueStr = trimmed.Substring(parenIndex + 1, trimmed.Length - parenIndex - 2);
                    int value = int.Parse(valueStr);

                    if (name.Contains("one") || name.Contains("1h"))
                        proficiencies[ProficiencyType.OneHanded] = value;
                    else if (name.Contains("two") || name.Contains("2h"))
                        proficiencies[ProficiencyType.TwoHanded] = value;
                    else if (name.Contains("polearm") || name.Contains("pole"))
                        proficiencies[ProficiencyType.Polearm] = value;
                    else if (name.Contains("arch") || name.Contains("bow"))
                        proficiencies[ProficiencyType.Archery] = value;
                    else if (name.Contains("cross"))
                        proficiencies[ProficiencyType.Crossbow] = value;
                    else if (name.Contains("throw"))
                        proficiencies[ProficiencyType.Throwing] = value;
                    else if (name.Contains("fire") || name.Contains("gun") || name.Contains("musket"))
                        proficiencies[ProficiencyType.Firearm] = value;
                }
                
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
