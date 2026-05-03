using System;
using System.Collections.Generic;
using System.Linq;

namespace MountAndBladeTools
{
    /// <summary>
    /// Comprehensive Mount & Blade skill system decoder.
    /// Includes skill indexes, flags, and hardcoded skill information.
    /// </summary>
    public static class MBSkillDecoder
    {
        /// <summary>
        /// Skill indexes (as defined in header_skills.py)
        /// </summary>
        public static class SkillIndex
        {
            public const int Trade = 0;
            public const int Leadership = 1;
            public const int PrisonerManagement = 2;
            public const int Reserved1 = 3;
            public const int Reserved2 = 4;
            public const int Reserved3 = 5;
            public const int Reserved4 = 6;
            public const int Persuasion = 7;
            public const int Engineer = 8;
            public const int FirstAid = 9;
            public const int Surgery = 10;
            public const int WoundTreatment = 11;
            public const int InventoryManagement = 12;
            public const int Spotting = 13;
            public const int Pathfinding = 14;
            public const int Tactics = 15;
            public const int Tracking = 16;
            public const int Trainer = 17;
            public const int Reserved5 = 18;
            public const int Reserved6 = 19;
            public const int Reserved7 = 20;
            public const int Reserved8 = 21;
            public const int Looting = 22;
            public const int HorseArchery = 23;
            public const int Riding = 24;
            public const int Athletics = 25;
            public const int Shield = 26;
            public const int WeaponMaster = 27;
            public const int Reserved9 = 28;
            public const int Reserved10 = 29;
            public const int Reserved11 = 30;
            public const int Reserved12 = 31;
            public const int Reserved13 = 32;
            public const int PowerDraw = 33;
            public const int PowerThrow = 34;
            public const int PowerStrike = 35;
            public const int Ironflesh = 36;
            public const int Reserved14 = 37;
            public const int Reserved15 = 38;
            public const int Reserved16 = 39;
            public const int Reserved17 = 40;
            public const int Reserved18 = 41;
        }
        
        /// <summary>
        /// Skill ID to index mapping
        /// </summary>
        private static readonly Dictionary<string, int> SkillIdToIndex = new Dictionary<string, int>
        {
            { "trade", 0 },
            { "leadership", 1 },
            { "prisoner_management", 2 },
            { "reserved_1", 3 },
            { "reserved_2", 4 },
            { "reserved_3", 5 },
            { "reserved_4", 6 },
            { "persuasion", 7 },
            { "engineer", 8 },
            { "first_aid", 9 },
            { "surgery", 10 },
            { "wound_treatment", 11 },
            { "inventory_management", 12 },
            { "spotting", 13 },
            { "pathfinding", 14 },
            { "tactics", 15 },
            { "tracking", 16 },
            { "trainer", 17 },
            { "reserved_5", 18 },
            { "reserved_6", 19 },
            { "reserved_7", 20 },
            { "reserved_8", 21 },
            { "looting", 22 },
            { "horse_archery", 23 },
            { "riding", 24 },
            { "athletics", 25 },
            { "shield", 26 },
            { "weapon_master", 27 },
            { "reserved_9", 28 },
            { "reserved_10", 29 },
            { "reserved_11", 30 },
            { "reserved_12", 31 },
            { "reserved_13", 32 },
            { "power_draw", 33 },
            { "power_throw", 34 },
            { "power_strike", 35 },
            { "ironflesh", 36 },
            { "reserved_14", 37 },
            { "reserved_15", 38 },
            { "reserved_16", 39 },
            { "reserved_17", 40 },
            { "reserved_18", 41 }
        };
        
        /// <summary>
        /// Index to skill ID mapping
        /// </summary>
        private static readonly Dictionary<int, string> IndexToSkillId = 
            SkillIdToIndex.ToDictionary(x => x.Value, x => x.Key);
        
        /// <summary>
        /// Hardcoded skills that have engine-level effects
        /// </summary>
        private static readonly HashSet<int> HardcodedSkills = new HashSet<int>
        {
            0,  // Trade
            1,  // Leadership
            2,  // Prisoner Management
            8,  // Engineer
            9,  // First Aid
            10, // Surgery
            11, // Wound Treatment
            12, // Inventory Management
            13, // Spotting
            14, // Pathfinding
            15, // Tactics
            16, // Tracking
            17, // Trainer
            23, // Horse Archery
            24, // Riding
            25, // Athletics
            26, // Shield
            27, // Weapon Master
            33, // Power Draw
            34, // Power Throw
            35, // Power Strike
            36  // Ironflesh
        };
        
        /// <summary>
        /// Skills classified as party skills (based on Native module_skills.py)
        /// </summary>
        private static readonly HashSet<int> PartySkills = new HashSet<int>
        {
            0,  // Trade
            8,  // Engineer
            9,  // First Aid
            10, // Surgery
            11, // Wound Treatment
            13, // Spotting
            14, // Pathfinding
            15, // Tactics
            16, // Tracking
            22  // Looting
        };
        
        /// <summary>
        /// Skills classified as leader skills (based on Native module_skills.py)
        /// </summary>
        private static readonly HashSet<int> LeaderSkills = new HashSet<int>
        {
            1,  // Leadership
            2,  // Prisoner Management
            12  // Inventory Management
        };
        
        /// <summary>
        /// Detailed skill information
        /// </summary>
        public class SkillInfo
        {
            public int Index { get; set; }
            public string SkillId { get; set; }
            public string SkillName { get; set; }
            public MBSkillFlagsDecoder.BaseAttribute BaseAttribute { get; set; }
            public string BaseAttributeName { get; set; }
            public MBSkillFlagsDecoder.SkillType SkillType { get; set; }
            public bool IsHardcoded { get; set; }
            public bool IsReserved { get; set; }
            public string Description { get; set; }
            
            public override string ToString()
            {
                return $"{SkillName} (Index: {Index}, Base: {BaseAttribute}, Type: {SkillType})";
            }
        }
        
        /// <summary>
        /// Gets skill index from skill ID
        /// </summary>
        public static int GetSkillIndex(string skillId)
        {
            return SkillIdToIndex.TryGetValue(skillId, out int index) ? index : -1;
        }
        
        /// <summary>
        /// Gets skill ID from skill index
        /// </summary>
        public static string GetSkillId(int index)
        {
            return IndexToSkillId.TryGetValue(index, out string id) ? id : null;
        }
        
        /// <summary>
        /// Gets skill ID with "skl_" prefix (as used in module system)
        /// </summary>
        public static string GetSkillReference(string skillId)
        {
            return $"skl_{skillId}";
        }
        
        /// <summary>
        /// Checks if a skill is hardcoded (has engine-level effects)
        /// </summary>
        public static bool IsHardcodedSkill(int skillIndex)
        {
            return HardcodedSkills.Contains(skillIndex);
        }
        
        /// <summary>
        /// Checks if a skill is reserved
        /// </summary>
        public static bool IsReservedSkill(int skillIndex)
        {
            string skillId = GetSkillId(skillIndex);
            return skillId != null && skillId.StartsWith("reserved_");
        }
        
        /// <summary>
        /// Gets the skill type based on Native classification
        /// </summary>
        public static MBSkillFlagsDecoder.SkillType GetSkillType(int skillIndex)
        {
            if (PartySkills.Contains(skillIndex))
                return MBSkillFlagsDecoder.SkillType.Party;
            else if (LeaderSkills.Contains(skillIndex))
                return MBSkillFlagsDecoder.SkillType.Leader;
            else
                return MBSkillFlagsDecoder.SkillType.Personal;
        }
        
        /// <summary>
        /// Gets detailed information about a skill
        /// </summary>
        public static SkillInfo GetSkillInfo(int skillIndex, int flags, string skillName = null, string description = null)
        {
            var decodedFlags = MBSkillFlagsDecoder.DecodeFlags(flags);
            string skillId = GetSkillId(skillIndex);
            
            return new SkillInfo
            {
                Index = skillIndex,
                SkillId = skillId,
                SkillName = skillName ?? FormatSkillName(skillId),
                BaseAttribute = decodedFlags.BaseAttribute,
                BaseAttributeName = decodedFlags.BaseAttributeName,
                SkillType = GetSkillType(skillIndex),
                IsHardcoded = IsHardcodedSkill(skillIndex),
                IsReserved = IsReservedSkill(skillIndex),
                Description = description
            };
        }
        
        /// <summary>
        /// Formats skill ID into a readable name
        /// </summary>
        private static string FormatSkillName(string skillId)
        {
            if (string.IsNullOrEmpty(skillId))
                return "Unknown";
            
            // Convert snake_case to Title Case
            return string.Join(" ", skillId.Split('_')
                .Select(word => char.ToUpper(word[0]) + word.Substring(1).ToLower()));
        }
        
        /// <summary>
        /// Gets all skill indexes
        /// </summary>
        public static IEnumerable<int> GetAllSkillIndexes()
        {
            return IndexToSkillId.Keys.OrderBy(x => x);
        }
        
        /// <summary>
        /// Gets all non-reserved skill indexes
        /// </summary>
        public static IEnumerable<int> GetActiveSkillIndexes()
        {
            return IndexToSkillId.Keys.Where(i => !IsReservedSkill(i)).OrderBy(x => x);
        }
        
        /// <summary>
        /// Gets all hardcoded skill indexes
        /// </summary>
        public static IEnumerable<int> GetHardcodedSkillIndexes()
        {
            return HardcodedSkills.OrderBy(x => x);
        }
        
        /// <summary>
        /// Gets skill ID to index mapping
        /// </summary>
        public static Dictionary<string, int> GetSkillIdToIndexMap()
        {
            return new Dictionary<string, int>(SkillIdToIndex);
        }
        
        /// <summary>
        /// Gets index to skill ID mapping
        /// </summary>
        public static Dictionary<int, string> GetIndexToSkillIdMap()
        {
            return new Dictionary<int, string>(IndexToSkillId);
        }
        
        /// <summary>
        /// Calculates party skill bonus based on player skill level
        /// As per Native rules: 0-1 (+0), 2-4 (+1), 5-7 (+2), 8-9 (+3), 10 (+4)
        /// </summary>
        public static int GetPartySkillBonus(int playerSkillLevel)
        {
            if (playerSkillLevel <= 1) return 0;
            if (playerSkillLevel <= 4) return 1;
            if (playerSkillLevel <= 7) return 2;
            if (playerSkillLevel <= 9) return 3;
            return 4; // Level 10+
        }
        
        /// <summary>
        /// Calculates effective party skill level (highest member + player bonus)
        /// </summary>
        public static int GetEffectivePartySkillLevel(int highestMemberLevel, int playerSkillLevel)
        {
            return highestMemberLevel + GetPartySkillBonus(playerSkillLevel);
        }
        
        /// <summary>
        /// Gets the maximum allowed skill level based on base attribute
        /// In Native: skill level cannot exceed 1/3 of base attribute level (except via books)
        /// </summary>
        public static int GetMaxSkillLevelForAttribute(int attributeLevel)
        {
            return attributeLevel / 3;
        }

        public static Dictionary<string, List<int>> GetSkillCategories()
        {   
            return new Dictionary<string, List<int>>
            {
                { "Party", PartySkills.ToList() },
                { "Leader", LeaderSkills.ToList() },
                { "Personal", GetAllSkillIndexes().Where(i => !PartySkills.Contains(i) && !LeaderSkills.Contains(i)).ToList() }
            };
        }
    }
    
    /// <summary>
    /// Extension methods for skill-related operations
    /// </summary>
    public static class SkillExtensions
    {
        public static bool IsHardcoded(this int skillIndex)
        {
            return MBSkillDecoder.IsHardcodedSkill(skillIndex);
        }
        
        public static bool IsReserved(this int skillIndex)
        {
            return MBSkillDecoder.IsReservedSkill(skillIndex);
        }
        
        public static string GetSkillId(this int skillIndex)
        {
            return MBSkillDecoder.GetSkillId(skillIndex);
        }
        
        public static string GetSkillReference(this int skillIndex)
        {
            string skillId = MBSkillDecoder.GetSkillId(skillIndex);
            return skillId != null ? $"skl_{skillId}" : null;
        }
    }
}