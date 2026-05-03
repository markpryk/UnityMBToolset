using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace MountAndBladeTools
{
    /// <summary>
    /// Comprehensive Mount & Blade skill knowledge system decoder.
    /// Handles the "knows_" values used to track NPC skill knowledge levels.
    /// </summary>
    public static class MBSkillKnowledgeDecoder
    {
        // Maximum skill level (1-10 in Native)
        public const int MaxSkillLevel = 10;
        public const int MinSkillLevel = 1;
        
        /// <summary>
        /// Calculates the base multiplier for a given skill index
        /// Formula: 16^skill_index or 2^(4 * skill_index)
        /// </summary>
        private static BigInteger GetSkillBaseMultiplier(int skillIndex)
        {
            // Using BigInteger to handle large values for higher skill indexes
            return BigInteger.Pow(16, skillIndex);
        }
        
        /// <summary>
        /// Calculates the "knows_" value for a specific skill and level
        /// Formula: level * (16^skill_index)
        /// </summary>
        public static BigInteger CalculateKnowsValue(int skillIndex, int level)
        {
            if (level < MinSkillLevel || level > MaxSkillLevel)
                throw new ArgumentOutOfRangeException(nameof(level), 
                    $"Level must be between {MinSkillLevel} and {MaxSkillLevel}");
            
            if (skillIndex < 0 || skillIndex > 41)
                throw new ArgumentOutOfRangeException(nameof(skillIndex), 
                    "Skill index must be between 0 and 41");
            
            return level * GetSkillBaseMultiplier(skillIndex);
        }
        
        /// <summary>
        /// Gets the "knows_" reference name (as used in module system)
        /// Example: GetKnowsReference(0, 5) returns "knows_trade_5"
        /// </summary>
        public static string GetKnowsReference(int skillIndex, int level)
        {
            string skillId = MBSkillDecoder.GetSkillId(skillIndex);
            if (skillId == null)
                return null;
            
            return $"knows_{skillId}_{level}";
        }
        
        /// <summary>
        /// Decodes a knows value to determine which skill and level it represents
        /// </summary>
        public static (int skillIndex, int level) DecodeKnowsValue(BigInteger knowsValue)
        {
            if (knowsValue <= 0)
                return (-1, 0);
            
            // Iterate through skills from highest to lowest to find match
            for (int skillIndex = 41; skillIndex >= 0; skillIndex--)
            {
                BigInteger baseMultiplier = GetSkillBaseMultiplier(skillIndex);
                
                // Check if this value could belong to this skill
                if (knowsValue >= baseMultiplier)
                {
                    BigInteger level = knowsValue / baseMultiplier;
                    
                    // Verify it's an exact match and within valid range
                    if (knowsValue == level * baseMultiplier && 
                        level >= MinSkillLevel && level <= MaxSkillLevel)
                    {
                        return (skillIndex, (int)level);
                    }
                }
            }
            
            return (-1, 0); // Invalid value
        }
        
        /// <summary>
        /// Extracts all skill knowledge from a combined knows value (bitwise OR of multiple knows values)
        /// </summary>
        public static Dictionary<int, int> DecodeAllKnowledge(BigInteger combinedKnows)
        {
            var knownSkills = new Dictionary<int, int>();
            
            if (combinedKnows <= 0)
                return knownSkills;
            
            BigInteger remaining = combinedKnows;
            
            // Process from highest skill index to lowest
            for (int skillIndex = 41; skillIndex >= 0; skillIndex--)
            {
                BigInteger baseMultiplier = GetSkillBaseMultiplier(skillIndex);
                
                if (remaining >= baseMultiplier)
                {
                    // Extract the level for this skill
                    BigInteger quotient = remaining / baseMultiplier;
                    BigInteger remainder = remaining % baseMultiplier;
                    
                    // The quotient might span multiple levels (if values were ORed)
                    // Extract the actual level
                    for (int level = MaxSkillLevel; level >= MinSkillLevel; level--)
                    {
                        BigInteger levelValue = level * baseMultiplier;
                        if (remaining >= levelValue)
                        {
                            knownSkills[skillIndex] = level;
                            remaining -= levelValue;
                            break;
                        }
                    }
                }
            }
            
            return knownSkills;
        }
        
        /// <summary>
        /// Combines multiple knows values into a single value (bitwise OR equivalent)
        /// </summary>
        public static BigInteger CombineKnowsValues(params BigInteger[] knowsValues)
        {
            BigInteger result = 0;
            foreach (var value in knowsValues)
            {
                result += value;
            }
            return result;
        }
        
        /// <summary>
        /// Checks if a character knows a specific skill at a given level
        /// </summary>
        public static bool HasKnowledge(BigInteger combinedKnows, int skillIndex, int level)
        {
            BigInteger requiredValue = CalculateKnowsValue(skillIndex, level);
            var allKnowledge = DecodeAllKnowledge(combinedKnows);
            
            return allKnowledge.TryGetValue(skillIndex, out int knownLevel) && knownLevel >= level;
        }
        
        /// <summary>
        /// Gets the known level for a specific skill (0 if unknown)
        /// </summary>
        public static int GetKnownLevel(BigInteger combinedKnows, int skillIndex)
        {
            var allKnowledge = DecodeAllKnowledge(combinedKnows);
            return allKnowledge.TryGetValue(skillIndex, out int level) ? level : 0;
        }
        
        /// <summary>
        /// Creates a human-readable report of all known skills
        /// </summary>
        public static string GetKnowledgeReport(BigInteger combinedKnows)
        {
            var knowledge = DecodeAllKnowledge(combinedKnows);
            
            if (knowledge.Count == 0)
                return "No skills known";
            
            var lines = new List<string>();
            foreach (var kvp in knowledge.OrderBy(x => x.Key))
            {
                string skillId = MBSkillDecoder.GetSkillId(kvp.Key);
                string skillName = MBSkillDecoder.GetSkillInfo(kvp.Key, 0).SkillName;
                lines.Add($"{skillName} (Index {kvp.Key}): Level {kvp.Value}");
            }
            
            return string.Join(Environment.NewLine, lines);
        }
        
        /// <summary>
        /// Generates all knows_ constants for a specific skill (levels 1-10)
        /// </summary>
        public static Dictionary<string, BigInteger> GenerateKnowsConstants(int skillIndex)
        {
            var constants = new Dictionary<string, BigInteger>();
            
            for (int level = MinSkillLevel; level <= MaxSkillLevel; level++)
            {
                string constantName = GetKnowsReference(skillIndex, level);
                BigInteger value = CalculateKnowsValue(skillIndex, level);
                constants[constantName] = value;
            }
            
            return constants;
        }
        
        /// <summary>
        /// Generates all knows_ constants for all skills
        /// </summary>
        public static Dictionary<string, BigInteger> GenerateAllKnowsConstants()
        {
            var allConstants = new Dictionary<string, BigInteger>();
            
            for (int skillIndex = 0; skillIndex <= 41; skillIndex++)
            {
                var skillConstants = GenerateKnowsConstants(skillIndex);
                foreach (var kvp in skillConstants)
                {
                    allConstants[kvp.Key] = kvp.Value;
                }
            }
            
            return allConstants;
        }
        
        /// <summary>
        /// Detailed knowledge information
        /// </summary>
        public class KnowledgeInfo
        {
            public int SkillIndex { get; set; }
            public string SkillId { get; set; }
            public string SkillName { get; set; }
            public int Level { get; set; }
            public BigInteger KnowsValue { get; set; }
            public string KnowsReference { get; set; }
            
            public override string ToString()
            {
                return $"{SkillName} Level {Level} (knows_{SkillId}_{Level} = {KnowsValue})";
            }
        }
        
        /// <summary>
        /// Gets detailed knowledge information for a skill and level
        /// </summary>
        public static KnowledgeInfo GetKnowledgeInfo(int skillIndex, int level)
        {
            var skillInfo = MBSkillDecoder.GetSkillInfo(skillIndex, 0);
            
            return new KnowledgeInfo
            {
                SkillIndex = skillIndex,
                SkillId = skillInfo.SkillId,
                SkillName = skillInfo.SkillName,
                Level = level,
                KnowsValue = CalculateKnowsValue(skillIndex, level),
                KnowsReference = GetKnowsReference(skillIndex, level)
            };
        }
        
        /// <summary>
        /// Gets all knowledge info from a combined knows value
        /// </summary>
        public static List<KnowledgeInfo> GetAllKnowledgeInfo(BigInteger combinedKnows)
        {
            var knowledge = DecodeAllKnowledge(combinedKnows);
            var infoList = new List<KnowledgeInfo>();
            
            foreach (var kvp in knowledge.OrderBy(x => x.Key))
            {
                infoList.Add(GetKnowledgeInfo(kvp.Key, kvp.Value));
            }
            
            return infoList;
        }
        
        /// <summary>
        /// Validates a knows value
        /// </summary>
        public static bool IsValidKnowsValue(BigInteger knowsValue)
        {
            var (skillIndex, level) = DecodeKnowsValue(knowsValue);
            return skillIndex >= 0 && level > 0;
        }
    }
    
    /// <summary>
    /// Extension methods for skill knowledge operations
    /// </summary>
    public static class SkillKnowledgeExtensions
    {
        /// <summary>
        /// Calculates knows value for this skill index at given level
        /// </summary>
        public static BigInteger GetKnowsValue(this int skillIndex, int level)
        {
            return MBSkillKnowledgeDecoder.CalculateKnowsValue(skillIndex, level);
        }
        
        /// <summary>
        /// Gets knows reference string for this skill index
        /// </summary>
        public static string GetKnowsReference(this int skillIndex, int level)
        {
            return MBSkillKnowledgeDecoder.GetKnowsReference(skillIndex, level);
        }
        
        /// <summary>
        /// Checks if combined knows value contains knowledge of this skill
        /// </summary>
        public static bool IsKnownIn(this int skillIndex, BigInteger combinedKnows, int minLevel = 1)
        {
            return MBSkillKnowledgeDecoder.HasKnowledge(combinedKnows, skillIndex, minLevel);
        }
    }
}