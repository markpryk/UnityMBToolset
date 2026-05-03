using System;
using System.Collections.Generic;

namespace MountAndBladeTools
{
    /// <summary>
    /// Mount & Blade party personality decoder.
    /// Decodes packed personality values: courage (bits 0-3) + aggressiveness (bits 4-7) + banditness (bit 8).
    /// </summary>
    public static class PartyPersonalityDecoder
    {
        // Masks for personality components
        private const int COURAGE_MASK = 0x000F;           // Bits 0-3
        private const int AGGRESSIVENESS_MASK = 0x00F0;    // Bits 4-7
        private const int BANDITNESS_FLAG = 0x0100;        // Bit 8
        
        // Courage levels (bits 0-3) - courage_8 is neutral
        private static readonly Dictionary<int, string> CourageLevels = new Dictionary<int, string>
        {
            { 0x04, "courage_4" },   // Very low
            { 0x05, "courage_5" },
            { 0x06, "courage_6" },
            { 0x07, "courage_7" },
            { 0x08, "courage_8" },   // Neutral
            { 0x09, "courage_9" },
            { 0x0A, "courage_10" },
            { 0x0B, "courage_11" },
            { 0x0C, "courage_12" },
            { 0x0D, "courage_13" },
            { 0x0E, "courage_14" },
            { 0x0F, "courage_15" },  // Very high
        };
        
        // Aggressiveness levels (bits 4-7)
        private static readonly Dictionary<int, string> AggressivenessLevels = new Dictionary<int, string>
        {
            { 0x00, "aggressiveness_0" },   // Passive
            { 0x10, "aggressiveness_1" },
            { 0x20, "aggressiveness_2" },
            { 0x30, "aggressiveness_3" },
            { 0x40, "aggressiveness_4" },
            { 0x50, "aggressiveness_5" },
            { 0x60, "aggressiveness_6" },
            { 0x70, "aggressiveness_7" },
            { 0x80, "aggressiveness_8" },   // Neutral
            { 0x90, "aggressiveness_9" },
            { 0xA0, "aggressiveness_10" },
            { 0xB0, "aggressiveness_11" },
            { 0xC0, "aggressiveness_12" },
            { 0xD0, "aggressiveness_13" },
            { 0xE0, "aggressiveness_14" },
            { 0xF0, "aggressiveness_15" },  // Very aggressive
        };
        
        // Personality presets (common combinations)
        private static readonly Dictionary<int, string> PersonalityPresets = new Dictionary<int, string>
        {
            { 0x89, "soldier_personality" },              // aggressiveness_8 | courage_9
            { 0x07, "merchant_personality" },             // aggressiveness_0 | courage_7
            { 0x0B, "escorted_merchant_personality" },    // aggressiveness_0 | courage_11
            { 0x138, "bandit_personality" },              // aggressiveness_3 | courage_8 | banditness
        };

        /// <summary>
        /// Decodes personality value into its components
        /// </summary>
        public static PersonalityResult DecodePersonality(int value)
        {
            var result = new PersonalityResult
            {
                RawValue = value,
                Components = new List<string>()
            };
            
            // Check for preset personalities first
            if (PersonalityPresets.ContainsKey(value))
            {
                result.PresetName = PersonalityPresets[value];
            }
            
            // Extract courage (bits 0-3)
            int courageValue = value & COURAGE_MASK;
            result.CourageValue = courageValue;
            
            if (CourageLevels.ContainsKey(courageValue))
            {
                result.CourageName = CourageLevels[courageValue];
                result.Components.Add(CourageLevels[courageValue]);
                
                // Courage 8 is neutral
                if (courageValue < 8)
                    result.CourageDescription = "low";
                else if (courageValue > 8)
                    result.CourageDescription = "high";
                else
                    result.CourageDescription = "neutral";
            }
            
            // Extract aggressiveness (bits 4-7)
            int aggressivenessValue = value & AGGRESSIVENESS_MASK;
            result.AggressivenessValue = aggressivenessValue;
            result.AggressivenessLevel = aggressivenessValue >> 4; // Get 0-15 value
            
            if (AggressivenessLevels.ContainsKey(aggressivenessValue))
            {
                result.AggressivenessName = AggressivenessLevels[aggressivenessValue];
                result.Components.Add(AggressivenessLevels[aggressivenessValue]);
            }
            
            // Check banditness flag (bit 8)
            if ((value & BANDITNESS_FLAG) != 0)
            {
                result.IsBandit = true;
                result.Components.Add("banditness");
            }
            
            return result;
        }
        
        /// <summary>
        /// Gets courage level from personality value
        /// </summary>
        public static int GetCourage(int personalityValue)
        {
            return personalityValue & COURAGE_MASK;
        }
        
        /// <summary>
        /// Gets aggressiveness level from personality value (0-15)
        /// </summary>
        public static int GetAggressiveness(int personalityValue)
        {
            return (personalityValue & AGGRESSIVENESS_MASK) >> 4;
        }
        
        /// <summary>
        /// Checks if personality has banditness flag
        /// </summary>
        public static bool IsBandit(int personalityValue)
        {
            return (personalityValue & BANDITNESS_FLAG) != 0;
        }
        
        /// <summary>
        /// Encodes personality from components
        /// </summary>
        public static int EncodePersonality(int courage, int aggressiveness, bool isBandit = false)
        {
            int result = 0;
            
            // Add courage (bits 0-3)
            result |= (courage & 0x0F);
            
            // Add aggressiveness (bits 4-7)
            result |= ((aggressiveness & 0x0F) << 4);
            
            // Add banditness flag
            if (isBandit)
                result |= BANDITNESS_FLAG;
            
            return result;
        }
        
        /// <summary>
        /// Gets preset personality values
        /// </summary>
        public static int GetSoldierPersonality() => 0x89;
        public static int GetMerchantPersonality() => 0x07;
        public static int GetEscortedMerchantPersonality() => 0x0B;
        public static int GetBanditPersonality() => 0x138;
        
        public static Dictionary<int, string> GetAllCourageLevels() => 
            new Dictionary<int, string>(CourageLevels);
        
        public static Dictionary<int, string> GetAllAggressivenessLevels() => 
            new Dictionary<int, string>(AggressivenessLevels);
        
        public static Dictionary<int, string> GetAllPresets() => 
            new Dictionary<int, string>(PersonalityPresets);
    }
    
    /// <summary>
    /// Result of personality decoding
    /// </summary>
    public class PersonalityResult
    {
        public int RawValue { get; set; }
        public string PresetName { get; set; }
        
        public int CourageValue { get; set; }
        public string CourageName { get; set; }
        public string CourageDescription { get; set; }
        
        public int AggressivenessValue { get; set; }
        public int AggressivenessLevel { get; set; }
        public string AggressivenessName { get; set; }
        
        public bool IsBandit { get; set; }
        
        public List<string> Components { get; set; }
        
        public override string ToString()
        {
            if (!string.IsNullOrEmpty(PresetName))
                return PresetName;
            
            if (Components.Count > 0)
                return string.Join("|", Components);
            
            return RawValue == 0 ? "0" : $"0x{RawValue:X}";
        }
    }
}