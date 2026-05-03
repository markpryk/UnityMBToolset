using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using UnityEngine;

namespace MountAndBlade.Data
{
    /// <summary>
    /// Decoder for Mount & Blade scene prop flags.
    /// Handles type extraction, property flags, hit points, and use time.
    /// </summary>
    public static class ScenePropFlagsDecoder
    {
        // SCENE PROP TYPES (bits 0-7) - These are ENCODED VALUES, not bitwise flags!
        // Only ONE type can be active at a time (mutually exclusive)
        private static readonly Dictionary<string, BigInteger> ScenePropTypes = new Dictionary<string, BigInteger>
        {
            { "sokf_type_container", 0x05 },
            { "sokf_type_ai_limiter", 0x08 },
            { "sokf_type_barrier", 0x09 },
            { "sokf_type_barrier_leave", 0x0A },
            { "sokf_type_ladder", 0x0B },
            { "sokf_type_barrier3d", 0x0C },
            { "sokf_type_player_limiter", 0x0D },
            { "sokf_type_ai_limiter3d", 0x0E },
        };
        
        // PROPERTY FLAGS (bits 8+) - Bitwise flags, can be combined
        private static readonly Dictionary<string, BigInteger> PropertyFlags = new Dictionary<string, BigInteger>
        {
            // Deprecated/Unused flags (bits 8-10)
            { "sokf_add_fire", 0x0100UL },
            { "sokf_add_smoke", 0x0200UL },
            { "sokf_add_light", 0x0400UL },
            
            // Core property flags
            { "sokf_show_hit_point_bar", 0x0800UL },
            { "sokf_place_at_origin", 0x1000UL },
            { "sokf_dynamic", 0x2000UL },
            { "sokf_invisible", 0x4000UL },
            { "sokf_destructible", 0x8000UL },
            
            { "sokf_moveable", 0x10000UL },
            { "sokf_face_player", 0x20000UL },
            { "sokf_dynamic_physics", 0x40000UL },
            { "sokf_missiles_not_attached", 0x80000UL },
            
            { "sokf_enforce_shadows", 0x100000UL },
            { "sokf_dont_move_agent_over", 0x200000UL },
            
            { "sokf_handle_as_flora", 0x1000000UL },
            { "sokf_static_movement", 0x2000000UL },
        };
        
        // ANIMATION MODES - Not stored in flags, separate field
        private static readonly Dictionary<string, int> AnimationModes = new Dictionary<string, int>
        {
            { "spanim_linear", 0 },
            { "spanim_loop_linear", 4 },
        };
        
        // BIT MASKS AND SHIFTS
        private const ulong TYPE_MASK = 0xFF;                    // Bits 0-7
        private const ulong HIT_POINTS_MASK = 0xFF;              // 8-bit value
        private const int HIT_POINTS_SHIFT = 20;                 // Bits 20-27
        private const ulong USE_TIME_MASK = 0xFF;                // 8-bit value
        private const int USE_TIME_SHIFT = 28;                   // Bits 28-35
        
        // NOTE: PROPERTY_BITS_MASK is NOT used for decoding!
        // Mount & Blade intentionally overlaps flags with hit points/use time storage.
        // Some flags (enforce_shadows, dont_move_agent_over, handle_as_flora, static_movement)
        // share bit positions with the hit points field (bits 20-27).
        // This is by design - the game can differentiate context when reading these values.
        // DO NOT use this mask when checking flags or you'll lose these important flags!
        private const ulong PROPERTY_BITS_MASK_UNUSED = ~(TYPE_MASK | 
                                                           (HIT_POINTS_MASK << HIT_POINTS_SHIFT) | 
                                                           (USE_TIME_MASK << USE_TIME_SHIFT));
        
        // PUBLIC API - TYPE EXTRACTION
        
        /// <summary>
        /// Gets the scene prop type from bits 0-7 (ENCODED VALUE, not bitwise flag!)
        /// Returns the type name (e.g., "sokf_type_barrier") or empty string if no type.
        /// </summary>
        public static string GetScenePropType(BigInteger value)
        {
            BigInteger typeValue = value & TYPE_MASK;
            var type = ScenePropTypes.FirstOrDefault(x => x.Value == typeValue);
            return type.Key ?? (typeValue == 0 ? "" : $"sokf_type_unknown_{typeValue:X}");
        }
        
        /// <summary>
        /// Gets the scene prop type value from bits 0-7.
        /// Returns 0 if no type is set.
        /// </summary>
        public static int GetScenePropTypeValue(BigInteger value)
        {
            return (int)(value & TYPE_MASK);
        }
        
        // PUBLIC API - HIT POINTS & USE TIME EXTRACTION
        
        /// <summary>
        /// Extracts hit points from bits 20-27.
        /// Returns 0-255. Only meaningful if sokf_destructible flag is set.
        /// 
        /// NOTE: As of the latest encoder fix, overlapping flags are automatically
        /// excluded when destructible + HP > 0, so no compensation is needed here.
        /// The encoder prioritizes clean HP storage over overlapping flags.
        /// </summary>
        public static int GetHitPoints(BigInteger value)
        {
            // Extract hit points value directly from bits 20-27
            int hitPoints = (int)((value >> HIT_POINTS_SHIFT) & HIT_POINTS_MASK);
            return hitPoints;
        }
        
        /// <summary>
        /// Extracts use time from bits 28-35.
        /// Returns 0-255 seconds. 0 = instant activation.
        /// Only meaningful if prop has collision mesh and ti_on_scene_prop_use trigger.
        /// </summary>
        public static int GetUseTime(BigInteger value)
        {
            return (int)((value >> USE_TIME_SHIFT) & USE_TIME_MASK);
        }
        
        // PUBLIC API - PROPERTY FLAGS DECODING
        
        /// <summary>
        /// Decodes all active property flags from bits 8+.
        /// Note: Some flags overlap with hit points/use time storage - this is intentional in M&B.
        /// Returns a list of flag names in ascending bit order.
        /// </summary>
        public static List<string> DecodePropertyFlags(BigInteger value)
        {
            var result = new List<string>();
            
            // DON'T mask out hit points/use time bits - flags can overlap with them!
            // Mount & Blade intentionally reuses bits for both flags and data storage
            
            // Check each property flag directly against the value
            foreach (var flag in PropertyFlags.OrderBy(x => x.Value))
            {
                if ((value & flag.Value) != 0)
                {
                    result.Add(flag.Key);
                }
            }
            
            return result;
        }
        
        /// <summary>
        /// Decodes ALL components of scene prop flags: type, properties, hit points, use time.
        /// </summary>
        public static ScenePropFlagsData DecodeComplete(BigInteger value)
        {
            return new ScenePropFlagsData
            {
                RawValue = value,
                Type = GetScenePropType(value),
                TypeValue = GetScenePropTypeValue(value),
                PropertyFlags = DecodePropertyFlags(value),
                HitPoints = GetHitPoints(value),
                UseTime = GetUseTime(value)
            };
        }
        
        /// <summary>
        /// Checks if a specific property flag is active.
        /// </summary>
        public static bool HasFlag(BigInteger value, string flagName)
        {
            if (!PropertyFlags.TryGetValue(flagName, out var flagBit))
            {
                return false;
            }
            
            return (value & flagBit) != 0;
        }
        
        // PUBLIC API - FLAG ENCODING
        
        /// <summary>
        /// Encodes scene prop flags from components.
        /// 
        /// CRITICAL BEHAVIOR - DESTRUCTIBLE MODE WITH HIT POINTS:
        /// When sokf_destructible is enabled AND hitPoints > 0, bits 20-27 are RESERVED
        /// for hit points storage. Overlapping flags will be AUTOMATICALLY EXCLUDED
        /// from encoding to prevent hit points corruption.
        /// 
        /// Overlapping flags (CANNOT be used with destructible + HP > 0):
        /// - sokf_enforce_shadows (bit 20)
        /// - sokf_dont_move_agent_over (bit 21)
        /// - sokf_handle_as_flora (bit 24)
        /// - sokf_static_movement (bit 25)
        /// 
        /// When NOT destructible OR hitPoints == 0:
        /// - All flags can be used freely, including overlapping ones
        /// </summary>
        /// <param name="typeName">Type flag name (e.g., "sokf_type_barrier") or empty for no type</param>
        /// <param name="propertyFlagNames">List of property flag names to set</param>
        /// <param name="hitPoints">Hit points (0-255) - the ACTUAL desired hit points</param>
        /// <param name="useTime">Use time in seconds (0-255)</param>
        /// <returns>Encoded flags value</returns>
        public static BigInteger EncodeFlags(string typeName, List<string> propertyFlagNames, 
                                             int hitPoints = 0, int useTime = 0)
        {
            BigInteger result = 0;
            
            // Add type (bits 0-7)
            if (!string.IsNullOrEmpty(typeName) && ScenePropTypes.TryGetValue(typeName, out var typeValue))
            {
                result |= typeValue;
            }
            
            // Check if destructible flag is set + has hit points
            bool isDestructible = propertyFlagNames != null && propertyFlagNames.Contains("sokf_destructible");
            bool hasHitPoints = hitPoints > 0;
            bool reserveHPBits = isDestructible && hasHitPoints;
            
            // Add property flags (bits 8+)
            if (propertyFlagNames != null)
            {
                foreach (var flagName in propertyFlagNames)
                {
                    if (PropertyFlags.TryGetValue(flagName, out var flagBit))
                    {
                        // CRITICAL: When destructible with HP > 0, SKIP overlapping flags
                        // These flags use bits 20-27 which are reserved for hit points storage
                        if (reserveHPBits)
                        {
                            if (flagName == "sokf_enforce_shadows" ||      // bit 20
                                flagName == "sokf_dont_move_agent_over" || // bit 21
                                flagName == "sokf_handle_as_flora" ||      // bit 24
                                flagName == "sokf_static_movement")        // bit 25
                            {
                                // Skip encoding - these bits are reserved for hit points
                                continue;
                            }
                        }
                        
                        result |= flagBit;
                    }
                }
            }
            
            // Encode hit points (bits 20-27)
            if (hitPoints > 0)
            {
                // Clamp to valid range (0-255)
                int clampedHP = System.Math.Max(0, System.Math.Min(255, hitPoints));
                
                // Clear bits 20-27 first to ensure clean encoding
                BigInteger hpClearMask = ~((BigInteger)HIT_POINTS_MASK << HIT_POINTS_SHIFT);
                result &= hpClearMask;
                
                // Encode hit points cleanly in bits 20-27
                result |= (BigInteger)clampedHP << HIT_POINTS_SHIFT;
            }
            
            // Add use time (bits 28-35)
            if (useTime > 0)
            {
                // Clamp to valid range (0-255)
                int clampedTime = System.Math.Max(0, System.Math.Min(255, useTime));
                result |= (BigInteger)clampedTime << USE_TIME_SHIFT;
            }
            
            return result;
        }
        
        /// <summary>
        /// Encodes hit points into flags format.
        /// Equivalent to Python: spr_hit_points(x)
        /// </summary>
        public static BigInteger EncodeHitPoints(int hitPoints)
        {
            int clamped = hitPoints & (int)HIT_POINTS_MASK;
            return (BigInteger)clamped << HIT_POINTS_SHIFT;
        }
        
        /// <summary>
        /// Encodes use time into flags format.
        /// Equivalent to Python: spr_use_time(x)
        /// </summary>
        public static BigInteger EncodeUseTime(int useTime)
        {
            int clamped = useTime & (int)USE_TIME_MASK;
            return (BigInteger)clamped << USE_TIME_SHIFT;
        }
        
        // PUBLIC API - VALIDATION
        
        /// <summary>
        /// Validates scene prop flags and returns a list of validation issues.
        /// </summary>
        public static List<string> ValidateFlags(BigInteger value)
        {
            var issues = new List<string>();
            var decoded = DecodeComplete(value);
            
            // Check for destructible without hit points
            if (decoded.PropertyFlags.Contains("sokf_destructible") && decoded.HitPoints == 0)
            {
                issues.Add("Destructible props should have hit_points > 0");
            }
            
            // Check for hit points without destructible
            if (decoded.HitPoints > 0 && !decoded.PropertyFlags.Contains("sokf_destructible"))
            {
                issues.Add("Hit points set but sokf_destructible flag is missing");
            }
            
            // Check for use time with moveable (might cause issues)
            if (decoded.UseTime > 0 && decoded.PropertyFlags.Contains("sokf_moveable"))
            {
                issues.Add("Useable + moveable props may have interaction issues");
            }
            
            // Check for show_hit_point_bar without destructible
            if (decoded.PropertyFlags.Contains("sokf_show_hit_point_bar") && 
                !decoded.PropertyFlags.Contains("sokf_destructible"))
            {
                issues.Add("show_hit_point_bar requires sokf_destructible flag");
            }
            
            // Check for invisible + show_hit_point_bar (doesn't make sense)
            if (decoded.PropertyFlags.Contains("sokf_invisible") && 
                decoded.PropertyFlags.Contains("sokf_show_hit_point_bar"))
            {
                issues.Add("Invisible props can't show hit point bars");
            }
            
            // Check for deprecated flags
            var deprecatedFlags = new[] { "sokf_add_fire", "sokf_add_smoke", "sokf_add_light" };
            foreach (var deprecated in deprecatedFlags)
            {
                if (decoded.PropertyFlags.Contains(deprecated))
                {
                    issues.Add($"{deprecated} is deprecated - use triggers instead");
                }
            }
            
            // Check for overlapping flags with hit points (informational - only if destructible)
            if (decoded.HitPoints > 0 && decoded.PropertyFlags.Contains("sokf_destructible"))
            {
                var overlappingFlags = new[] 
                { 
                    "sokf_enforce_shadows", 
                    "sokf_dont_move_agent_over", 
                    "sokf_handle_as_flora", 
                    "sokf_static_movement" 
                };
                
                var activeOverlaps = overlappingFlags.Where(f => decoded.PropertyFlags.Contains(f)).ToList();
                
                if (activeOverlaps.Count > 0)
                {
                    issues.Add($"INFO: Flags [{string.Join(", ", activeOverlaps)}] overlap with hit points storage. " +
                              "Encoder/decoder automatically compensate for this.");
                }
            }
            
            return issues;
        }
        
        // STATIC ACCESSORS
        
        /// <summary>
        /// Returns all scene prop type definitions.
        /// </summary>
        public static Dictionary<string, BigInteger> GetScenePropTypes() => 
            new Dictionary<string, BigInteger>(ScenePropTypes);
        
        /// <summary>
        /// Returns all property flag definitions.
        /// </summary>
        public static Dictionary<string, BigInteger> GetPropertyFlags() => 
            new Dictionary<string, BigInteger>(PropertyFlags);
        
        /// <summary>
        /// Returns all animation mode definitions.
        /// </summary>
        public static Dictionary<string, int> GetAnimationModes() => 
            new Dictionary<string, int>(AnimationModes);
        
        /// <summary>
        /// Gets the bit value for a property flag name.
        /// Returns 0 if flag name is not found.
        /// </summary>
        public static BigInteger GetFlagBitValue(string flagName)
        {
            return PropertyFlags.TryGetValue(flagName, out var value) ? value : 0;
        }
        
        /// <summary>
        /// Gets the description/tooltip for a flag.
        /// </summary>
        public static string GetFlagDescription(string flagName)
        {
            // Flag descriptions based on Mount & Blade documentation
            var descriptions = new Dictionary<string, string>
            {
                // Type flags
                { "sokf_type_container", "Usable chest/container prop" },
                { "sokf_type_ai_limiter", "Blocks AI movement only (2D)" },
                { "sokf_type_barrier", "Blocks all movement (2D)" },
                { "sokf_type_barrier_leave", "Exit barrier - can leave scene here" },
                { "sokf_type_ladder", "Ladder - affects movement speed" },
                { "sokf_type_barrier3d", "Blocks all movement (3D)" },
                { "sokf_type_player_limiter", "Blocks player movement only" },
                { "sokf_type_ai_limiter3d", "Blocks AI movement only (3D)" },
                
                // Property flags
                { "sokf_add_fire", "DEPRECATED - Add fire particle (use triggers instead)" },
                { "sokf_add_smoke", "DEPRECATED - Add smoke particle (use triggers instead)" },
                { "sokf_add_light", "DEPRECATED - Add light source (use triggers instead)" },
                { "sokf_show_hit_point_bar", "Display health bar for destructible props" },
                { "sokf_place_at_origin", "Force prop to entry point 0 (for inventory)" },
                { "sokf_dynamic", "Dynamic physics object" },
                { "sokf_invisible", "Hide mesh (for barriers/lights)" },
                { "sokf_destructible", "Can be destroyed (requires hit_points)" },
                { "sokf_moveable", "Can move - synced in MP (prevents shadows)" },
                { "sokf_face_player", "Billboard - always faces camera" },
                { "sokf_dynamic_physics", "Enhanced dynamic physics" },
                { "sokf_missiles_not_attached", "Projectiles don't stick to prop" },
                { "sokf_enforce_shadows", "Force shadow rendering" },
                { "sokf_dont_move_agent_over", "Agents don't move over this prop" },
                { "sokf_handle_as_flora", "Render like vegetation/flora" },
                { "sokf_static_movement", "Optimized MP movement (doors, etc.)" },
            };
            
            return descriptions.TryGetValue(flagName, out var desc) ? desc : "";
        }
        
        /// <summary>
        /// Gets category/usage hint for a flag.
        /// </summary>
        public static string GetFlagCategory(string flagName)
        {
            if (ScenePropTypes.ContainsKey(flagName))
                return "Type";
            
            var categories = new Dictionary<string, string>
            {
                { "sokf_destructible", "Gameplay" },
                { "sokf_moveable", "Gameplay" },
                { "sokf_show_hit_point_bar", "Visual" },
                { "sokf_invisible", "Visual" },
                { "sokf_face_player", "Visual" },
                { "sokf_enforce_shadows", "Visual" },
                { "sokf_handle_as_flora", "Visual" },
                { "sokf_dynamic", "Physics" },
                { "sokf_dynamic_physics", "Physics" },
                { "sokf_missiles_not_attached", "Physics" },
                { "sokf_dont_move_agent_over", "Physics" },
                { "sokf_place_at_origin", "Placement" },
                { "sokf_static_movement", "Multiplayer" },
                { "sokf_add_fire", "Deprecated" },
                { "sokf_add_smoke", "Deprecated" },
                { "sokf_add_light", "Deprecated" },
            };
            
            return categories.TryGetValue(flagName, out var cat) ? cat : "Other";
        }
    }
    
    // DATA STRUCTURES
    
    /// <summary>
    /// Complete decoded scene prop flags data.
    /// </summary>
    public class ScenePropFlagsData
    {
        public BigInteger RawValue { get; set; }
        public string Type { get; set; }
        public int TypeValue { get; set; }
        public List<string> PropertyFlags { get; set; } = new List<string>();
        public int HitPoints { get; set; }
        public int UseTime { get; set; }
        
        /// <summary>
        /// Returns a human-readable summary of the flags.
        /// </summary>
        public string GetSummary()
        {
            var parts = new List<string>();
            
            if (!string.IsNullOrEmpty(Type))
                parts.Add($"Type: {Type}");
            
            if (PropertyFlags.Count > 0)
                parts.Add($"Flags: {PropertyFlags.Count}");
            
            if (HitPoints > 0)
                parts.Add($"HP: {HitPoints}");
            
            if (UseTime > 0)
                parts.Add($"Use: {UseTime}s");
            
            return string.Join(", ", parts);
        }
        
        /// <summary>
        /// Checks if a specific property flag is active.
        /// </summary>
        public bool HasFlag(string flagName)
        {
            return PropertyFlags.Contains(flagName);
        }
        
        /// <summary>
        /// Quick check for common flag combinations.
        /// </summary>
        public bool IsDestructible => HasFlag("sokf_destructible");
        public bool IsMoveable => HasFlag("sokf_moveable");
        public bool IsInvisible => HasFlag("sokf_invisible");
        public bool IsUseable => UseTime > 0;
        public bool ShowsHealthBar => HasFlag("sokf_show_hit_point_bar");
    }
}
