using System;
using System.Collections.Generic;
using System.Linq;

namespace MountAndBladeTools
{
    /// <summary>
    /// Mount & Blade skill flags encoder/decoder.
    /// Handles both encoded base attribute values and bitwise flags.
    /// Corresponds to flags defined in header_skills.py
    /// </summary>
    public static class MBSkillFlagsDecoder
    {
        #region Constants
        
        // Base attribute encoded values (bits 0-3)
        public const int SF_BASE_ATT_STR = 0x000;  // Strength
        public const int SF_BASE_ATT_AGI = 0x001;  // Agility
        public const int SF_BASE_ATT_INT = 0x002;  // Intelligence
        public const int SF_BASE_ATT_CHA = 0x003;  // Charisma
        
        // Bitwise flags
        public const int SF_EFFECTS_PARTY = 0x010;  // Party skill flag
        public const int SF_INACTIVE = 0x100;       // Disabled skill flag
        
        // Masks
        private const int BASE_ATTRIBUTE_MASK = 0x00F;  // Bits 0-3
        
        #endregion
        
        #region Enums
        
        /// <summary>
        /// Base attribute enumeration
        /// </summary>
        public enum BaseAttribute
        {
            Strength = SF_BASE_ATT_STR,
            Agility = SF_BASE_ATT_AGI,
            Intelligence = SF_BASE_ATT_INT,
            Charisma = SF_BASE_ATT_CHA
        }
        
        /// <summary>
        /// Skill type enumeration
        /// </summary>
        public enum SkillType
        {
            Leader,    // Affects party, only leader's level counts
            Personal,  // Affects individual character only
            Party      // Affects party, highest level counts (sf_effects_party flag)
        }
        
        #endregion
        
        #region Lookup Tables
        
        private static readonly Dictionary<int, string> BaseAttributeNames = new Dictionary<int, string>
        {
            { SF_BASE_ATT_STR, "sf_base_att_str" },
            { SF_BASE_ATT_AGI, "sf_base_att_agi" },
            { SF_BASE_ATT_INT, "sf_base_att_int" },
            { SF_BASE_ATT_CHA, "sf_base_att_cha" }
        };
        
        private static readonly Dictionary<string, int> BaseAttributeValues = new Dictionary<string, int>
        {
            { "sf_base_att_str", SF_BASE_ATT_STR },
            { "sf_base_att_agi", SF_BASE_ATT_AGI },
            { "sf_base_att_int", SF_BASE_ATT_INT },
            { "sf_base_att_cha", SF_BASE_ATT_CHA }
        };
        
        private static readonly Dictionary<BaseAttribute, string> AttributeDisplayNames = new Dictionary<BaseAttribute, string>
        {
            { BaseAttribute.Strength, "Strength" },
            { BaseAttribute.Agility, "Agility" },
            { BaseAttribute.Intelligence, "Intelligence" },
            { BaseAttribute.Charisma, "Charisma" }
        };
        
        #endregion
        
        #region Result Class
        
        /// <summary>
        /// Decoded skill flags result
        /// </summary>
        public class DecodedSkillFlags
        {
            public int RawFlags { get; set; }
            public BaseAttribute BaseAttribute { get; set; }
            public string BaseAttributeName { get; set; }
            public string BaseAttributeDisplayName { get; set; }
            public SkillType SkillType { get; set; }
            public bool IsPartySkill { get; set; }
            public bool IsInactive { get; set; }
            public List<string> FlagNames { get; set; } = new List<string>();
            
            public override string ToString()
            {
                var parts = new List<string>();
                parts.Add(BaseAttributeName);
                if (IsPartySkill) parts.Add("sf_effects_party");
                if (IsInactive) parts.Add("sf_inactive");
                return string.Join(" | ", parts);
            }
            
            public string ToDisplayString()
            {
                var parts = new List<string>();
                parts.Add(BaseAttributeDisplayName);
                if (IsPartySkill) parts.Add("Party Skill");
                if (IsInactive) parts.Add("Inactive");
                return string.Join(" | ", parts);
            }
        }
        
        #endregion
        
        #region Decoding Methods
        
        /// <summary>
        /// Decodes skill flags into a structured result
        /// </summary>
        public static DecodedSkillFlags DecodeFlags(int flags)
        {
            var result = new DecodedSkillFlags
            {
                RawFlags = flags
            };
            
            // 1. Extract base attribute (encoded value in bits 0-3)
            int baseAttValue = flags & BASE_ATTRIBUTE_MASK;
            if (BaseAttributeNames.ContainsKey(baseAttValue))
            {
                result.BaseAttributeName = BaseAttributeNames[baseAttValue];
                result.FlagNames.Add(BaseAttributeNames[baseAttValue]);
                
                // Set enum value
                result.BaseAttribute = (BaseAttribute)baseAttValue;
                result.BaseAttributeDisplayName = AttributeDisplayNames[result.BaseAttribute];
            }
            else
            {
                result.BaseAttribute = BaseAttribute.Strength;
                result.BaseAttributeName = "unknown";
                result.BaseAttributeDisplayName = "Unknown";
            }
            
            // 2. Check party skill flag (bitwise)
            result.IsPartySkill = (flags & SF_EFFECTS_PARTY) != 0;
            if (result.IsPartySkill)
            {
                result.FlagNames.Add("sf_effects_party");
                result.SkillType = SkillType.Party;
            }
            else
            {
                // Note: Cannot distinguish Leader vs Personal without skill index
                result.SkillType = SkillType.Leader;
            }
            
            // 3. Check inactive flag (bitwise)
            result.IsInactive = (flags & SF_INACTIVE) != 0;
            if (result.IsInactive)
            {
                result.FlagNames.Add("sf_inactive");
            }
            
            return result;
        }
        
        /// <summary>
        /// Decodes skill flags into a simple list of flag names
        /// </summary>
        public static List<string> DecodeFlagsToList(int flags)
        {
            return DecodeFlags(flags).FlagNames;
        }
        
        /// <summary>
        /// Decodes flags to a Python-style string (e.g., "sf_base_att_cha|sf_effects_party")
        /// </summary>
        public static string DecodeToPythonString(int flags)
        {
            var flagNames = DecodeFlagsToList(flags);
            return string.Join("|", flagNames);
        }
        
        #endregion
        
        #region Encoding Methods
        
        /// <summary>
        /// Encodes skill flags from base attribute enum
        /// </summary>
        public static int EncodeFlags(BaseAttribute baseAttribute, bool isPartySkill = false, bool isInactive = false)
        {
            int flags = (int)baseAttribute;
            
            if (isPartySkill)
                flags |= SF_EFFECTS_PARTY;
            
            if (isInactive)
                flags |= SF_INACTIVE;
            
            return flags;
        }
        
        /// <summary>
        /// Encodes skill flags from base attribute constant
        /// </summary>
        public static int EncodeFlags(int baseAttributeConstant, bool isPartySkill = false, bool isInactive = false)
        {
            int flags = baseAttributeConstant & BASE_ATTRIBUTE_MASK;
            
            if (isPartySkill)
                flags |= SF_EFFECTS_PARTY;
            
            if (isInactive)
                flags |= SF_INACTIVE;
            
            return flags;
        }
        
        /// <summary>
        /// Parses a Python-style flag string (e.g., "sf_base_att_cha|sf_effects_party")
        /// </summary>
        public static int ParseFromPythonString(string flagString)
        {
            if (string.IsNullOrEmpty(flagString))
                return 0;
            
            int flags = 0;
            var parts = flagString.Split('|');
            
            foreach (var part in parts)
            {
                string trimmed = part.Trim();
                
                // Check if it's a base attribute
                if (BaseAttributeValues.ContainsKey(trimmed))
                {
                    flags = (flags & ~BASE_ATTRIBUTE_MASK) | BaseAttributeValues[trimmed];
                }
                // Check if it's sf_effects_party
                else if (trimmed == "sf_effects_party")
                {
                    flags |= SF_EFFECTS_PARTY;
                }
                // Check if it's sf_inactive
                else if (trimmed == "sf_inactive")
                {
                    flags |= SF_INACTIVE;
                }
            }
            
            return flags;
        }
        
        #endregion
        
        #region Flag Check Methods
        
        /// <summary>
        /// Checks if a specific flag is present
        /// </summary>
        public static bool HasFlag(int flags, string flagName)
        {
            return DecodeFlags(flags).FlagNames.Contains(flagName);
        }
        
        /// <summary>
        /// Gets the base attribute from skill flags
        /// </summary>
        public static BaseAttribute GetBaseAttribute(int flags)
        {
            return DecodeFlags(flags).BaseAttribute;
        }
        
        /// <summary>
        /// Gets the base attribute constant value (0x000-0x003)
        /// </summary>
        public static int GetBaseAttributeValue(int flags)
        {
            return flags & BASE_ATTRIBUTE_MASK;
        }
        
        /// <summary>
        /// Gets the base attribute name from skill flags
        /// </summary>
        public static string GetBaseAttributeName(int flags)
        {
            int baseAttValue = flags & BASE_ATTRIBUTE_MASK;
            return BaseAttributeNames.ContainsKey(baseAttValue) 
                ? BaseAttributeNames[baseAttValue] 
                : "unknown";
        }
        
        /// <summary>
        /// Gets the base attribute display name
        /// </summary>
        public static string GetBaseAttributeDisplayName(int flags)
        {
            return DecodeFlags(flags).BaseAttributeDisplayName;
        }
        
        /// <summary>
        /// Checks if skill is a party skill
        /// </summary>
        public static bool IsPartySkill(int flags)
        {
            return (flags & SF_EFFECTS_PARTY) != 0;
        }
        
        /// <summary>
        /// Checks if skill is inactive/disabled
        /// </summary>
        public static bool IsInactive(int flags)
        {
            return (flags & SF_INACTIVE) != 0;
        }
        
        #endregion
        
        #region Flag Modification Methods
        
        /// <summary>
        /// Sets the base attribute in existing flags
        /// </summary>
        public static int SetBaseAttribute(int flags, BaseAttribute baseAttribute)
        {
            flags &= ~BASE_ATTRIBUTE_MASK;  // Clear base attribute bits
            flags |= (int)baseAttribute;     // Set new base attribute
            return flags;
        }
        
        /// <summary>
        /// Adds the party skill flag
        /// </summary>
        public static int AddPartySkillFlag(int flags)
        {
            return flags | SF_EFFECTS_PARTY;
        }
        
        /// <summary>
        /// Removes the party skill flag
        /// </summary>
        public static int RemovePartySkillFlag(int flags)
        {
            return flags & ~SF_EFFECTS_PARTY;
        }
        
        /// <summary>
        /// Adds the inactive flag
        /// </summary>
        public static int AddInactiveFlag(int flags)
        {
            return flags | SF_INACTIVE;
        }
        
        /// <summary>
        /// Removes the inactive flag
        /// </summary>
        public static int RemoveInactiveFlag(int flags)
        {
            return flags & ~SF_INACTIVE;
        }
        
        /// <summary>
        /// Toggles the party skill flag
        /// </summary>
        public static int TogglePartySkillFlag(int flags)
        {
            return flags ^ SF_EFFECTS_PARTY;
        }
        
        /// <summary>
        /// Toggles the inactive flag
        /// </summary>
        public static int ToggleInactiveFlag(int flags)
        {
            return flags ^ SF_INACTIVE;
        }
        
        #endregion
        
        #region Utility Methods
        
        /// <summary>
        /// Gets all possible base attribute values
        /// </summary>
        public static Dictionary<int, string> GetBaseAttributeConstants()
        {
            return new Dictionary<int, string>(BaseAttributeNames);
        }
        
        /// <summary>
        /// Gets all base attribute enum values
        /// </summary>
        public static IEnumerable<BaseAttribute> GetAllBaseAttributes()
        {
            return Enum.GetValues(typeof(BaseAttribute)).Cast<BaseAttribute>();
        }
        
        /// <summary>
        /// Validates if flags are well-formed
        /// </summary>
        public static bool ValidateFlags(int flags)
        {
            int baseAtt = flags & BASE_ATTRIBUTE_MASK;
            return BaseAttributeNames.ContainsKey(baseAtt);
        }
        
        /// <summary>
        /// Creates a detailed breakdown of skill flags
        /// </summary>
        public static string GetFlagBreakdown(int flags)
        {
            var decoded = DecodeFlags(flags);
            var lines = new List<string>
            {
                $"Raw Value: {flags} (0x{flags:X})",
                $"Base Attribute: {decoded.BaseAttributeDisplayName} ({decoded.BaseAttributeName})",
                $"Skill Type: {decoded.SkillType}",
                $"Is Party Skill: {decoded.IsPartySkill}",
                $"Is Inactive: {decoded.IsInactive}",
                $"Python Format: {DecodeToPythonString(flags)}"
            };
            
            return string.Join("\n", lines);
        }
        
        /// <summary>
        /// Generates all common flag combinations
        /// </summary>
        public static Dictionary<string, int> GenerateCommonFlagCombinations()
        {
            var combinations = new Dictionary<string, int>();
            
            foreach (var baseAtt in GetAllBaseAttributes())
            {
                string baseName = AttributeDisplayNames[baseAtt];
                
                // Just base attribute
                combinations[$"{baseName}"] = (int)baseAtt;
                
                // Base attribute + party
                combinations[$"{baseName} | Party"] = EncodeFlags(baseAtt, isPartySkill: true);
                
                // Base attribute + inactive
                combinations[$"{baseName} | Inactive"] = EncodeFlags(baseAtt, isInactive: true);
                
                // Base attribute + party + inactive
                combinations[$"{baseName} | Party | Inactive"] = 
                    EncodeFlags(baseAtt, isPartySkill: true, isInactive: true);
            }
            
            return combinations;
        }
        
        #endregion
    }
    
    #region Extension Methods
    
    /// <summary>
    /// Extension methods for skill flags
    /// </summary>
    public static class SkillFlagsExtensions
    {
        public static string ToSkillFlagString(this int flags)
        {
            return MBSkillFlagsDecoder.DecodeFlags(flags).ToString();
        }
        
        public static string ToDisplayString(this int flags)
        {
            return MBSkillFlagsDecoder.DecodeFlags(flags).ToDisplayString();
        }
        
        public static MBSkillFlagsDecoder.DecodedSkillFlags ToDecodedFlags(this int flags)
        {
            return MBSkillFlagsDecoder.DecodeFlags(flags);
        }
        
        public static MBSkillFlagsDecoder.BaseAttribute GetBaseAttribute(this int flags)
        {
            return MBSkillFlagsDecoder.GetBaseAttribute(flags);
        }
        
        public static bool IsPartySkill(this int flags)
        {
            return MBSkillFlagsDecoder.IsPartySkill(flags);
        }
        
        public static bool IsInactive(this int flags)
        {
            return MBSkillFlagsDecoder.IsInactive(flags);
        }
        
        public static int WithBaseAttribute(this int flags, MBSkillFlagsDecoder.BaseAttribute baseAttribute)
        {
            return MBSkillFlagsDecoder.SetBaseAttribute(flags, baseAttribute);
        }
        
        public static int WithPartySkill(this int flags, bool enabled = true)
        {
            return enabled 
                ? MBSkillFlagsDecoder.AddPartySkillFlag(flags)
                : MBSkillFlagsDecoder.RemovePartySkillFlag(flags);
        }
        
        public static int WithInactive(this int flags, bool enabled = true)
        {
            return enabled 
                ? MBSkillFlagsDecoder.AddInactiveFlag(flags)
                : MBSkillFlagsDecoder.RemoveInactiveFlag(flags);
        }
    }
    
    #endregion
}