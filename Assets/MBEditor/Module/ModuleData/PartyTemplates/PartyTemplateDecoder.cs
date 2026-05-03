using System;
using System.Collections.Generic;
using System.Numerics;

namespace MountAndBladeTools
{
    /// <summary>
    /// Mount & Blade party template decoder.
    /// Provides template-specific decoding and validation.
    /// Party templates use the same flags and personality systems as regular parties,
    /// but don't have AI behavior, targets, or positions.
    /// </summary>
    public static class PartyTemplateDecoder
    {
        /// <summary>
        /// Decodes complete party template data
        /// </summary>
        public static TemplateData DecodeTemplate(
            BigInteger flags,
            int personality)
        {
            return new TemplateData
            {
                Flags = PartyDecoder.DecodeFlags(flags),
                Personality = PartyDecoder.DecodePersonality(personality)
            };
        }
        
        /// <summary>
        /// Decodes party template data from string values
        /// </summary>
        public static TemplateData DecodeTemplate(
            string flags,
            string personality)
        {
            return new TemplateData
            {
                Flags = PartyDecoder.DecodeFlags(flags),
                Personality = PartyDecoder.DecodePersonality(personality)
            };
        }
        
        /// <summary>
        /// Validates a party template's troop stack configuration
        /// </summary>
        public static ValidationResult ValidateTemplate(
            string templateId,
            string templateName,
            List<TemplateStack> stacks)
        {
            var result = new ValidationResult
            {
                IsValid = true,
                Errors = new List<string>(),
                Warnings = new List<string>()
            };
            
            // Check required fields
            if (string.IsNullOrWhiteSpace(templateId))
            {
                result.Errors.Add("Template ID is required");
                result.IsValid = false;
            }
            
            if (string.IsNullOrWhiteSpace(templateName))
            {
                result.Errors.Add("Template name is required");
                result.IsValid = false;
            }
            
            // Check stack count (Mount & Blade limit: 6 stacks)
            if (stacks == null || stacks.Count == 0)
            {
                result.Warnings.Add("Template has no troop stacks - parties spawned will be empty");
            }
            else if (stacks.Count > 6)
            {
                result.Errors.Add($"Too many troop stacks ({stacks.Count}). Mount & Blade supports maximum 6 stacks per template.");
                result.IsValid = false;
            }
            
            // Validate each stack
            if (stacks != null)
            {
                for (int i = 0; i < stacks.Count; i++)
                {
                    var stack = stacks[i];
                    var stackErrors = ValidateStack(stack, i + 1);
                    
                    if (stackErrors.Count > 0)
                    {
                        result.Errors.AddRange(stackErrors);
                        result.IsValid = false;
                    }
                }
            }
            
            return result;
        }
        
        /// <summary>
        /// Validates a single template stack
        /// </summary>
        private static List<string> ValidateStack(TemplateStack stack, int stackNumber)
        {
            var errors = new List<string>();
            
            if (string.IsNullOrWhiteSpace(stack.TroopID))
                errors.Add($"Stack {stackNumber}: Troop ID is required");
            
            if (stack.MinCount < 0)
                errors.Add($"Stack {stackNumber}: Minimum count cannot be negative");
            
            if (stack.MaxCount < stack.MinCount)
                errors.Add($"Stack {stackNumber}: Maximum count ({stack.MaxCount}) must be >= minimum ({stack.MinCount})");
            
            if (stack.MaxCount == 0 && stack.MinCount == 0)
                errors.Add($"Stack {stackNumber}: Both min and max are 0 - this stack will spawn no troops");
            
            return errors;
        }
        
        /// <summary>
        /// Generates a random party composition from a template
        /// </summary>
        public static List<SpawnedStack> GenerateRandomComposition(List<TemplateStack> templateStacks, Random random = null)
        {
            if (random == null)
                random = new Random();
            
            var result = new List<SpawnedStack>();
            
            foreach (var stack in templateStacks)
            {
                int count = stack.MinCount;
                
                if (stack.MaxCount > stack.MinCount)
                {
                    count = random.Next(stack.MinCount, stack.MaxCount + 1);
                }
                
                result.Add(new SpawnedStack
                {
                    TroopID = stack.TroopID,
                    Count = count,
                    MemberFlags = stack.MemberFlags
                });
            }
            
            return result;
        }
        
        /// <summary>
        /// Gets personality preset values for common template types
        /// </summary>
        public static class PersonalityPresets
        {
            public static int Soldier => PartyPersonalityDecoder.GetSoldierPersonality();
            public static int Merchant => PartyPersonalityDecoder.GetMerchantPersonality();
            public static int EscortedMerchant => PartyPersonalityDecoder.GetEscortedMerchantPersonality();
            public static int Bandit => PartyPersonalityDecoder.GetBanditPersonality();
        }
        
        /// <summary>
        /// Gets common flag combinations for templates
        /// </summary>
        public static class FlagPresets
        {
            /// <summary>
            /// Civilian party (merchant, farmer)
            /// </summary>
            public static List<string> Civilian => new List<string> 
            { 
                "pf_civilian",
                "pf_show_faction"
            };
            
            /// <summary>
            /// Military party (patrol, garrison reinforcement)
            /// </summary>
            public static List<string> Military => new List<string>
            {
                "pf_show_faction",
                "pf_default_behavior"
            };
            
            /// <summary>
            /// Quest party
            /// </summary>
            public static List<string> Quest => new List<string>
            {
                "pf_quest_party",
                "pf_always_visible"
            };
            
            /// <summary>
            /// Bandit party
            /// </summary>
            public static List<string> Bandit => new List<string>
            {
                "pf_default_behavior"
            };
            
            /// <summary>
            /// Caravan
            /// </summary>
            public static List<string> Caravan => new List<string>
            {
                "pf_civilian",
                "pf_show_faction",
                "pf_default_behavior"
            };
        }
    }
    
    /// <summary>
    /// Decoded party template data
    /// </summary>
    public class TemplateData
    {
        public PartyFlagsResult Flags { get; set; }
        public PersonalityResult Personality { get; set; }
        
        public override string ToString()
        {
            return $"Flags: {Flags} | Personality: {Personality}";
        }
    }
    
    /// <summary>
    /// Template troop stack configuration
    /// </summary>
    public class TemplateStack
    {
        public string TroopID { get; set; }
        public int MinCount { get; set; }
        public int MaxCount { get; set; }
        public int MemberFlags { get; set; }
        
        public override string ToString()
        {
            string flags = MemberFlags != 0 
                ? $" [{string.Join("|", PartyMemberFlagsDecoder.DecodeFlags(MemberFlags))}]" 
                : "";
            
            return $"{TroopID}: {MinCount}-{MaxCount}{flags}";
        }
    }
    
    /// <summary>
    /// Spawned troop stack (generated from template)
    /// </summary>
    public class SpawnedStack
    {
        public string TroopID { get; set; }
        public int Count { get; set; }
        public int MemberFlags { get; set; }
        
        public override string ToString()
        {
            string flags = MemberFlags != 0 
                ? $" [{string.Join("|", PartyMemberFlagsDecoder.DecodeFlags(MemberFlags))}]" 
                : "";
            
            return $"{TroopID}: {Count}{flags}";
        }
    }
    
    /// <summary>
    /// Validation result
    /// </summary>
    public class ValidationResult
    {
        public bool IsValid { get; set; }
        public List<string> Errors { get; set; }
        public List<string> Warnings { get; set; }
        
        public override string ToString()
        {
            if (IsValid && Warnings.Count == 0)
                return "Valid";
            
            var parts = new List<string>();
            
            if (!IsValid)
                parts.Add($"ERRORS: {string.Join(", ", Errors)}");
            
            if (Warnings.Count > 0)
                parts.Add($"WARNINGS: {string.Join(", ", Warnings)}");
            
            return string.Join(" | ", parts);
        }
    }
}