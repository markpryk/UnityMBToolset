using System;
using System.Collections.Generic;
using System.Numerics;

namespace MountAndBladeTools
{
    /// <summary>
    /// Comprehensive Mount & Blade party decoder.
    /// Combines all party-related decoders into a single convenient interface.
    /// </summary>
    public static class PartyDecoder
    {
        /// <summary>
        /// Decodes complete party flags (icon + flags)
        /// </summary>
        public static PartyFlagsResult DecodeFlags(BigInteger flagsValue)
        {
            return PartyFlagsDecoder.DecodeFlags(flagsValue);
        }
        
        /// <summary>
        /// Decodes complete party flags from string (hex or decimal)
        /// </summary>
        public static PartyFlagsResult DecodeFlags(string flagsValue)
        {
            BigInteger value = ParseBigInteger(flagsValue);
            return PartyFlagsDecoder.DecodeFlags(value);
        }
        
        /// <summary>
        /// Decodes personality value
        /// </summary>
        public static PersonalityResult DecodePersonality(int personalityValue)
        {
            return PartyPersonalityDecoder.DecodePersonality(personalityValue);
        }
        
        /// <summary>
        /// Decodes personality from string
        /// </summary>
        public static PersonalityResult DecodePersonality(string personalityValue)
        {
            int value = ParseInt(personalityValue);
            return PartyPersonalityDecoder.DecodePersonality(value);
        }
        
        /// <summary>
        /// Decodes AI behavior
        /// </summary>
        public static string DecodeAIBehavior(int behaviorValue)
        {
            return PartyAIBehaviorDecoder.DecodeBehavior(behaviorValue);
        }
        
        /// <summary>
        /// Decodes AI behavior from string
        /// </summary>
        public static string DecodeAIBehavior(string behaviorValue)
        {
            int value = ParseInt(behaviorValue);
            return PartyAIBehaviorDecoder.DecodeBehavior(value);
        }
        
        /// <summary>
        /// Decodes member flags
        /// </summary>
        public static List<string> DecodeMemberFlags(int memberFlags)
        {
            return PartyMemberFlagsDecoder.DecodeFlags(memberFlags);
        }
        
        /// <summary>
        /// Decodes member flags from string
        /// </summary>
        public static List<string> DecodeMemberFlags(string memberFlags)
        {
            int value = ParseInt(memberFlags);
            return PartyMemberFlagsDecoder.DecodeFlags(value);
        }
        
        /// <summary>
        /// Checks if member is a prisoner
        /// </summary>
        public static bool IsPrisoner(int memberFlags)
        {
            return PartyMemberFlagsDecoder.IsPrisoner(memberFlags);
        }
        
        /// <summary>
        /// Gets icon ID from combined flags value
        /// </summary>
        public static int GetIconId(BigInteger flagsValue)
        {
            return PartyFlagsDecoder.GetIconId(flagsValue);
        }
        
        /// <summary>
        /// Gets icon ID from string value
        /// </summary>
        public static int GetIconId(string flagsValue)
        {
            BigInteger value = ParseBigInteger(flagsValue);
            return PartyFlagsDecoder.GetIconId(value);
        }
        
        /// <summary>
        /// Decodes all party data at once
        /// </summary>
        public static CompletePartyData DecodePartyData(
            BigInteger flags,
            int personality,
            int aiBehavior,
            int aiTarget)
        {
            return new CompletePartyData
            {
                Flags = DecodeFlags(flags),
                Personality = DecodePersonality(personality),
                AIBehavior = DecodeAIBehavior(aiBehavior),
                AITarget = aiTarget,
                AIBehaviorRequiresTarget = PartyAIBehaviorDecoder.RequiresTarget(aiBehavior),
                AIBehaviorRequiresCoordinates = PartyAIBehaviorDecoder.RequiresCoordinates(aiBehavior)
            };
        }
        
        /// <summary>
        /// Decodes all party data from string values
        /// </summary>
        public static CompletePartyData DecodePartyData(
            string flags,
            string personality,
            string aiBehavior,
            string aiTarget)
        {
            return DecodePartyData(
                ParseBigInteger(flags),
                ParseInt(personality),
                ParseInt(aiBehavior),
                ParseInt(aiTarget)
            );
        }
        
        // Helper methods for parsing
        private static BigInteger ParseBigInteger(string value)
        {
            if (string.IsNullOrEmpty(value))
                return 0;
            
            value = value.Trim();
            
            // Handle hex format
            if (value.StartsWith("0x") || value.StartsWith("0X"))
            {
                value = value.Substring(2);
                return BigInteger.Parse(value, System.Globalization.NumberStyles.HexNumber);
            }
            
            // Handle decimal format
            return BigInteger.Parse(value);
        }
        
        private static int ParseInt(string value)
        {
            if (string.IsNullOrEmpty(value))
                return 0;
            
            value = value.Trim();
            
            // Handle hex format
            if (value.StartsWith("0x") || value.StartsWith("0X"))
            {
                value = value.Substring(2);
                return int.Parse(value, System.Globalization.NumberStyles.HexNumber);
            }
            
            // Handle decimal format
            return int.Parse(value);
        }
    }
    
    /// <summary>
    /// Complete decoded party data
    /// </summary>
    public class CompletePartyData
    {
        public PartyFlagsResult Flags { get; set; }
        public PersonalityResult Personality { get; set; }
        public string AIBehavior { get; set; }
        public int AITarget { get; set; }
        public bool AIBehaviorRequiresTarget { get; set; }
        public bool AIBehaviorRequiresCoordinates { get; set; }
        
        public override string ToString()
        {
            var parts = new List<string>();
            
            parts.Add($"Flags: {Flags}");
            parts.Add($"Personality: {Personality}");
            parts.Add($"AI: {AIBehavior}");
            
            if (AIBehaviorRequiresTarget)
                parts.Add($"Target: {AITarget}");
            
            return string.Join(" | ", parts);
        }
    }
}