using System;
using System.Numerics;

namespace MountAndBlade.Data
{
    /// <summary>
    /// Decodes and encodes Mount & Blade Warband troop attributes.
    /// Handles: Strength, Agility, Intelligence, Charisma, and Level.
    /// Based on header_troops.py definitions.
    /// 
    /// Encoding format (using BigInteger with bignum flag):
    /// - BIGNUM FLAG: 0x40000000000000000000000000000000 (bit 126)
    /// - STR: bits 0-7   (values 3-63, stored as 0x00000003 to 0x0000003f)
    /// - AGI: bits 8-15  (values 3-63, stored as 0x00000300 to 0x00003f00)
    /// - INT: bits 16-23 (values 3-63, stored as 0x00030000 to 0x003f0000)
    /// - CHA: bits 24-31 (values 3-63, stored as 0x03000000 to 0x3f000000)
    /// - Level: bits 32-39 (values 0-255, shifted by 32 bits)
    /// 
    /// The 'bignum' flag (0x40000000000000000000000000000000) is ORed with all values
    /// </summary>
    public static class TroopAttributeDecoder
    {
        // The bignum flag used in Python module system
        // This is bit 126 set: 0x40000000000000000000000000000000
        private static readonly BigInteger BIGNUM_FLAG = BigInteger.Parse("40000000000000000000000000000000", 
            System.Globalization.NumberStyles.HexNumber);

        // Bit positions for each attribute
        private const int STR_SHIFT = 0;
        private const int AGI_SHIFT = 8;
        private const int INT_SHIFT = 16;
        private const int CHA_SHIFT = 24;
        private const int LEVEL_SHIFT = 32;

        // Masks for extracting values (8 bits each)
        private const long ATTRIBUTE_MASK = 0xFF;
        private const long LEVEL_MASK = 0xFF;

        // Mask to extract only the lower 40 bits (all attributes + level)
        // This effectively strips the bignum flag and any higher bits
        private static readonly BigInteger ATTRIBUTE_DATA_MASK = (BigInteger.One << 40) - 1;

        // Valid attribute range
        public const int MIN_ATTRIBUTE_VALUE = 3;
        public const int MAX_ATTRIBUTE_VALUE = 63; // Game max is 63
        public const int DEFAULT_ATTRIBUTE_VALUE = 4;

        // Valid level range
        public const int MIN_LEVEL = 0;
        public const int MAX_LEVEL = 255;
        public const int DEFAULT_LEVEL = 1;

        // DECODING

        /// <summary>
        /// Decode all attributes from a hex/numeric string value.
        /// Automatically strips the bignum flag if present.
        /// Returns: (strength, agility, intelligence, charisma, level)
        /// </summary>
        public static (int str, int agi, int intel, int cha, int level) DecodeAllAttributes(string value)
        {
            BigInteger parsedValue = ParseBigInteger(value);
            
            // Strip the bignum flag - only keep lower 40 bits
            BigInteger cleanValue = StripBignumFlag(parsedValue);

            int str = DecodeAttribute(cleanValue, STR_SHIFT);
            int agi = DecodeAttribute(cleanValue, AGI_SHIFT);
            int intel = DecodeAttribute(cleanValue, INT_SHIFT);
            int cha = DecodeAttribute(cleanValue, CHA_SHIFT);
            int level = DecodeLevel(cleanValue);

            return (str, agi, intel, cha, level);
        }

        /// <summary>
        /// Decode all attributes from a BigInteger value.
        /// Automatically strips the bignum flag if present.
        /// Returns: (strength, agility, intelligence, charisma, level)
        /// </summary>
        public static (int str, int agi, int intel, int cha, int level) DecodeAllAttributes(BigInteger value)
        {
            // Strip the bignum flag - only keep lower 40 bits
            BigInteger cleanValue = StripBignumFlag(value);

            int str = DecodeAttribute(cleanValue, STR_SHIFT);
            int agi = DecodeAttribute(cleanValue, AGI_SHIFT);
            int intel = DecodeAttribute(cleanValue, INT_SHIFT);
            int cha = DecodeAttribute(cleanValue, CHA_SHIFT);
            int level = DecodeLevel(cleanValue);

            return (str, agi, intel, cha, level);
        }

        /// <summary>
        /// Strip the bignum flag and return only the attribute data (lower 40 bits)
        /// </summary>
        private static BigInteger StripBignumFlag(BigInteger value)
        {
            // Only keep the lower 40 bits (attributes + level data)
            return value & ATTRIBUTE_DATA_MASK;
        }

        /// <summary>
        /// Check if a value has the bignum flag set
        /// </summary>
        public static bool HasBignumFlag(BigInteger value)
        {
            return (value & BIGNUM_FLAG) != 0;
        }

        /// <summary>
        /// Decode a single attribute value from the given shift position
        /// </summary>
        private static int DecodeAttribute(BigInteger value, int shift)
        {
            long extracted = (long)((value >> shift) & ATTRIBUTE_MASK);
            
            // If 0, return default value
            if (extracted == 0)
                return DEFAULT_ATTRIBUTE_VALUE;
            
            return (int)extracted;
        }

        /// <summary>
        /// Decode level value
        /// </summary>
        private static int DecodeLevel(BigInteger value)
        {
            long extracted = (long)((value >> LEVEL_SHIFT) & LEVEL_MASK);
            
            // If 0, return default level
            if (extracted == 0)
                return DEFAULT_LEVEL;
            
            return (int)extracted;
        }

        // ENCODING

        /// <summary>
        /// Encode all attributes into a single BigInteger value.
        /// By default, includes the bignum flag for compatibility with Python module system.
        /// </summary>
        public static BigInteger EncodeAllAttributes(int str, int agi, int intel, int cha, int level, bool includeBignumFlag = true)
        {
            // Clamp values to valid ranges
            str = Math.Clamp(str, MIN_ATTRIBUTE_VALUE, MAX_ATTRIBUTE_VALUE);
            agi = Math.Clamp(agi, MIN_ATTRIBUTE_VALUE, MAX_ATTRIBUTE_VALUE);
            intel = Math.Clamp(intel, MIN_ATTRIBUTE_VALUE, MAX_ATTRIBUTE_VALUE);
            cha = Math.Clamp(cha, MIN_ATTRIBUTE_VALUE, MAX_ATTRIBUTE_VALUE);
            level = Math.Clamp(level, MIN_LEVEL, MAX_LEVEL);

            BigInteger encoded = 0;

            // Encode each attribute at its bit position
            encoded |= (BigInteger)str << STR_SHIFT;
            encoded |= (BigInteger)agi << AGI_SHIFT;
            encoded |= (BigInteger)intel << INT_SHIFT;
            encoded |= (BigInteger)cha << CHA_SHIFT;
            encoded |= (BigInteger)level << LEVEL_SHIFT;

            // Add bignum flag if requested (default for Python module system compatibility)
            if (includeBignumFlag)
            {
                encoded |= BIGNUM_FLAG;
            }

            return encoded;
        }

        /// <summary>
        /// Encode a single attribute with the bignum flag
        /// </summary>
        public static BigInteger EncodeStrength(int value)
        {
            value = Math.Clamp(value, MIN_ATTRIBUTE_VALUE, MAX_ATTRIBUTE_VALUE);
            return BIGNUM_FLAG | ((BigInteger)value << STR_SHIFT);
        }

        /// <summary>
        /// Encode a single attribute with the bignum flag
        /// </summary>
        public static BigInteger EncodeAgility(int value)
        {
            value = Math.Clamp(value, MIN_ATTRIBUTE_VALUE, MAX_ATTRIBUTE_VALUE);
            return BIGNUM_FLAG | ((BigInteger)value << AGI_SHIFT);
        }

        /// <summary>
        /// Encode a single attribute with the bignum flag
        /// </summary>
        public static BigInteger EncodeIntelligence(int value)
        {
            value = Math.Clamp(value, MIN_ATTRIBUTE_VALUE, MAX_ATTRIBUTE_VALUE);
            return BIGNUM_FLAG | ((BigInteger)value << INT_SHIFT);
        }

        /// <summary>
        /// Encode a single attribute with the bignum flag
        /// </summary>
        public static BigInteger EncodeCharisma(int value)
        {
            value = Math.Clamp(value, MIN_ATTRIBUTE_VALUE, MAX_ATTRIBUTE_VALUE);
            return BIGNUM_FLAG | ((BigInteger)value << CHA_SHIFT);
        }

        /// <summary>
        /// Encode level with the bignum flag
        /// </summary>
        public static BigInteger EncodeLevel(int value)
        {
            value = Math.Clamp(value, MIN_LEVEL, MAX_LEVEL);
            return (BIGNUM_FLAG | (BigInteger)value) << LEVEL_SHIFT;
        }

        // VALIDATION & FORMULAS

        /// <summary>
        /// Calculate total attribute points based on level.
        /// Formula: 17 + 16 + (Level * 1) = 33 + Level
        /// Where: 17 is base attributes (4+4+4+5=17), 16 is starting points, 1 per level
        /// </summary>
        public static int CalculateExpectedAttributePoints(int level)
        {
            return 33 + level;
        }

        /// <summary>
        /// Calculate actual attribute points from current values
        /// </summary>
        public static int CalculateActualAttributePoints(int str, int agi, int intel, int cha)
        {
            return str + agi + intel + cha;
        }

        /// <summary>
        /// Calculate total skill points based on level and intelligence.
        /// Formula: 13 + Level + INT
        /// </summary>
        public static int CalculateExpectedSkillPoints(int level, int intelligence)
        {
            return 13 + level + intelligence;
        }

        /// <summary>
        /// Calculate hit points based on strength and ironflesh skill.
        /// Formula: 35 + (Ironflesh * 2) + Strength
        /// </summary>
        public static int CalculateHitPoints(int strength, int ironfleshLevel)
        {
            return 35 + (ironfleshLevel * 2) + strength;
        }

        /// <summary>
        /// Get the attribute point warning/validation message
        /// </summary>
        public static string GetAttributePointsValidation(int str, int agi, int intel, int cha, int level)
        {
            int expected = CalculateExpectedAttributePoints(level);
            int actual = CalculateActualAttributePoints(str, agi, intel, cha);
            int difference = actual - expected;

            if (difference == 0)
                return $"✓ Attribute points correct for level {level} ({actual}/{expected})";
            else if (difference < 0)
                return $"⚠️ Attribute points too low! {actual}/{expected} (missing {-difference} points) - Game will auto-assign random points";
            else
                return $"⚠️ Attribute points too high! {actual}/{expected} (excess {difference} points)";
        }

        /// <summary>
        /// Get attribute descriptions
        /// </summary>
        public static string GetAttributeDescription(string attributeName)
        {
            switch (attributeName.ToLower())
            {
                case "strength":
                case "str":
                    return "Every point adds +1 to hit points. Hidden bonus: increases melee, bow, and thrown weapon damage";
                
                case "agility":
                case "agi":
                    return "Each point gives 5 weapon proficiency points and slightly increases battlefield movement speed";
                
                case "intelligence":
                case "int":
                    return "Every point immediately gives one extra skill point";
                
                case "charisma":
                case "cha":
                    return "Each point increases party size limit by +1";
                
                case "level":
                    return "Troop level. Determines attribute/skill points and experience requirements for upgrades";
                
                default:
                    return "";
            }
        }

        // HELPER METHODS

        public static BigInteger ParseBigInteger(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return 0;

            input = input.Trim();

            // Handle hex format
            if (input.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            {
                return BigInteger.Parse(
                    input.Substring(2),
                    System.Globalization.NumberStyles.HexNumber);
            }

            // Handle decimal format
            return BigInteger.Parse(input);
        }

        /// <summary>
        /// Format a BigInteger value to hex string
        /// </summary>
        public static string FormatToHex(BigInteger value)
        {
            if (value == 0)
                return "0x0";

            return "0x" + value.ToString("X");
        }

        /// <summary>
        /// Format a BigInteger value to hex string with leading zeros for readability
        /// </summary>
        public static string FormatToHexPadded(BigInteger value)
        {
            if (value == 0)
                return "0x0";

            // Check if bignum flag is present
            bool hasBignum = HasBignumFlag(value);
            
            if (hasBignum)
            {
                // Format with full 128-bit representation
                string hex = value.ToString("X");
                return "0x" + hex.PadLeft(32, '0');
            }
            else
            {
                // Format with minimal padding
                return "0x" + value.ToString("X");
            }
        }

        /// <summary>
        /// Create a formatted attribute string for display (e.g., "str_15|agi_12|int_10|cha_8|level(25)")
        /// This is the Python module system format
        /// </summary>
        public static string FormatAttributesForDisplay(int str, int agi, int intel, int cha, int level)
        {
            return $"str_{str}|agi_{agi}|int_{intel}|cha_{cha}|level({level})";
        }

        /// <summary>
        /// Parse a formatted attribute string (e.g., "str_15|agi_12|int_10|cha_8|level(25)")
        /// Returns false if parsing fails
        /// </summary>
        public static bool TryParseFormattedString(string formatted, 
            out int str, out int agi, out int intel, out int cha, out int level)
        {
            str = DEFAULT_ATTRIBUTE_VALUE;
            agi = DEFAULT_ATTRIBUTE_VALUE;
            intel = DEFAULT_ATTRIBUTE_VALUE;
            cha = DEFAULT_ATTRIBUTE_VALUE;
            level = DEFAULT_LEVEL;

            if (string.IsNullOrWhiteSpace(formatted))
                return false;

            try
            {
                string[] parts = formatted.Split('|');
                
                foreach (string part in parts)
                {
                    string trimmed = part.Trim();
                    
                    if (trimmed.StartsWith("str_"))
                        str = int.Parse(trimmed.Substring(4));
                    else if (trimmed.StartsWith("agi_"))
                        agi = int.Parse(trimmed.Substring(4));
                    else if (trimmed.StartsWith("int_"))
                        intel = int.Parse(trimmed.Substring(4));
                    else if (trimmed.StartsWith("cha_"))
                        cha = int.Parse(trimmed.Substring(4));
                    else if (trimmed.StartsWith("level(") && trimmed.EndsWith(")"))
                    {
                        string levelStr = trimmed.Substring(6, trimmed.Length - 7);
                        level = int.Parse(levelStr);
                    }
                }
                
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Create a detailed debug string showing the bit structure
        /// </summary>
        public static string DebugFormat(BigInteger value)
        {
            bool hasBignum = HasBignumFlag(value);
            BigInteger cleanValue = StripBignumFlag(value);
            
            var (str, agi, intel, cha, level) = DecodeAllAttributes(value);
            
            return $"Value: {FormatToHex(value)}\n" +
                   $"Has Bignum Flag: {hasBignum}\n" +
                   $"Clean Value: {FormatToHex(cleanValue)}\n" +
                   $"Decoded: STR={str}, AGI={agi}, INT={intel}, CHA={cha}, Level={level}\n" +
                   $"Formatted: {FormatAttributesForDisplay(str, agi, intel, cha, level)}";
        }
    }
}
