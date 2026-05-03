using System;
using System.Collections.Generic;

namespace MountAndBladeTools
{
    /// <summary>
    /// Mount & Blade party member flags decoder.
    /// Decodes member flags for troops in party stacks.
    /// </summary>
    public static class PartyMemberFlagsDecoder
    {
        // Party member flags (currently only one defined)
        private static readonly Dictionary<string, int> MemberFlags = new Dictionary<string, int>
        {
            { "pmf_is_prisoner", 0x0001 },
        };

        /// <summary>
        /// Decodes member flags
        /// </summary>
        public static List<string> DecodeFlags(int value)
        {
            var result = new List<string>();
            
            foreach (var flag in MemberFlags)
            {
                if ((value & flag.Value) != 0)
                {
                    result.Add(flag.Key);
                }
            }
            
            return result;
        }
        
        /// <summary>
        /// Checks if troop is a prisoner
        /// </summary>
        public static bool IsPrisoner(int memberFlags)
        {
            return (memberFlags & MemberFlags["pmf_is_prisoner"]) != 0;
        }
        
        /// <summary>
        /// Encodes member flags from flag names
        /// </summary>
        public static int EncodeFlags(IEnumerable<string> flagNames)
        {
            int result = 0;
            
            foreach (var flagName in flagNames)
            {
                if (MemberFlags.ContainsKey(flagName))
                {
                    result |= MemberFlags[flagName];
                }
            }
            
            return result;
        }
        
        /// <summary>
        /// Creates prisoner flag
        /// </summary>
        public static int CreatePrisonerFlag()
        {
            return MemberFlags["pmf_is_prisoner"];
        }
        
        /// <summary>
        /// Gets all member flags
        /// </summary>
        public static Dictionary<string, int> GetAllFlags() => 
            new Dictionary<string, int>(MemberFlags);
    }
}