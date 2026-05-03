using System.Collections.Generic;

namespace MountAndBladeTools
{
    /// <summary>
    /// Mount & Blade AI behavior decoder.
    /// Maps AI behavior constants to their symbolic names.
    /// </summary>
    public static class PartyAIBehaviorDecoder
    {
        // AI Behavior constants (simple integer values)
        private static readonly Dictionary<int, string> AIBehaviors = new Dictionary<int, string>
        {
            { 0, "ai_bhvr_hold" },              // Stand still
            { 1, "ai_bhvr_travel_to_party" },   // Travel to party
            { 2, "ai_bhvr_patrol_location" },   // Patrol area
            { 3, "ai_bhvr_patrol_party" },      // Patrol around party
            { 4, "ai_bhvr_attack_party" },      // Attack party (alias: ai_bhvr_track_party, deprecated)
            { 5, "ai_bhvr_avoid_party" },       // Avoid party
            { 6, "ai_bhvr_travel_to_point" },   // Travel to coordinates
            { 7, "ai_bhvr_negotiate_party" },   // Negotiate with party
            { 8, "ai_bhvr_in_town" },           // In town
            { 9, "ai_bhvr_travel_to_ship" },    // Travel to ship
            { 10, "ai_bhvr_escort_party" },     // Escort party
            { 11, "ai_bhvr_driven_by_party" },  // Driven by party
        };
        
        // Behavior descriptions
        private static readonly Dictionary<int, string> BehaviorDescriptions = new Dictionary<int, string>
        {
            { 0, "Stand completely still on the world map" },
            { 1, "Travel to a predetermined party destination" },
            { 2, "Patrol around a predetermined area" },
            { 3, "Patrol around a predetermined party" },
            { 4, "Attack a predetermined party" },
            { 5, "Avoid a predetermined party" },
            { 6, "Travel to predetermined coordinates" },
            { 7, "Negotiate with a predetermined party" },
            { 8, "Party is in a town" },
            { 9, "Travel to a ship" },
            { 10, "Escort a predetermined party" },
            { 11, "Driven by a predetermined party (e.g., cattle herd quest)" },
        };

        /// <summary>
        /// Decodes AI behavior value to symbolic name
        /// </summary>
        public static string DecodeBehavior(int value)
        {
            return AIBehaviors.ContainsKey(value) 
                ? AIBehaviors[value] 
                : $"unknown_behavior_{value}";
        }
        
        /// <summary>
        /// Gets description of AI behavior
        /// </summary>
        public static string GetDescription(int value)
        {
            return BehaviorDescriptions.ContainsKey(value)
                ? BehaviorDescriptions[value]
                : "Unknown behavior";
        }
        
        /// <summary>
        /// Gets behavior value from name
        /// </summary>
        public static int? GetBehaviorValue(string behaviorName)
        {
            foreach (var kvp in AIBehaviors)
            {
                if (kvp.Value == behaviorName)
                    return kvp.Key;
            }
            return null;
        }
        
        /// <summary>
        /// Checks if behavior requires a target party
        /// </summary>
        public static bool RequiresTarget(int behaviorValue)
        {
            switch (behaviorValue)
            {
                case 1:  // travel_to_party
                case 3:  // patrol_party
                case 4:  // attack_party
                case 5:  // avoid_party
                case 7:  // negotiate_party
                case 10: // escort_party
                case 11: // driven_by_party
                    return true;
                
                default:
                    return false;
            }
        }
        
        /// <summary>
        /// Checks if behavior requires coordinates
        /// </summary>
        public static bool RequiresCoordinates(int behaviorValue)
        {
            switch (behaviorValue)
            {
                case 2: // patrol_location
                case 6: // travel_to_point
                    return true;
                
                default:
                    return false;
            }
        }
        
        /// <summary>
        /// Gets all AI behavior names
        /// </summary>
        public static Dictionary<int, string> GetAllBehaviors() => 
            new Dictionary<int, string>(AIBehaviors);
        
        /// <summary>
        /// Gets all behavior descriptions
        /// </summary>
        public static Dictionary<int, string> GetAllDescriptions() => 
            new Dictionary<int, string>(BehaviorDescriptions);
    }
}