using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using UnityEngine;

public static class ItemFlagPropertyDecoder
{
    // ITEM TYPES (bits 0-4) - These are ENCODED VALUES, not bitwise flags!
    private static readonly Dictionary<string, BigInteger> ItemTypes = new Dictionary<string, BigInteger>
    {
        { "itp_type_horse", 0x01 },
        { "itp_type_one_handed_wpn", 0x02 },
        { "itp_type_two_handed_wpn", 0x03 },
        { "itp_type_polearm", 0x04 },
        { "itp_type_arrows", 0x05 },
        { "itp_type_bolts", 0x06 },
        { "itp_type_shield", 0x07 },
        { "itp_type_bow", 0x08 },
        { "itp_type_crossbow", 0x09 },
        { "itp_type_thrown", 0x0A },
        { "itp_type_goods", 0x0B },
        { "itp_type_head_armor", 0x0C },
        { "itp_type_body_armor", 0x0D },
        { "itp_type_foot_armor", 0x0E },
        { "itp_type_hand_armor", 0x0F },
        { "itp_type_pistol", 0x10 },
        { "itp_type_musket", 0x11 },
        { "itp_type_bullets", 0x12 },
        { "itp_type_animal", 0x13 },
        { "itp_type_book", 0x14 },
    };
    
    // ATTACHMENT FLAGS (bits 8-11) - MUTUALLY EXCLUSIVE ENCODED VALUES
    private static readonly Dictionary<string, BigInteger> AttachmentFlags = new Dictionary<string, BigInteger>
    {
        { "itp_force_attach_left_hand", 0x0100UL },
        { "itp_force_attach_right_hand", 0x0200UL },
        { "itp_force_attach_left_forearm", 0x0300UL },
        { "itp_attach_armature", 0x0F00UL },
    };
    
    // OVERLAPPING FLAG GROUPS - Same bit, different semantic meanings per item type
    // Each group contains all flag names that share the same bit position
    public class OverlappingFlagGroup
    {
        public BigInteger BitValue { get; set; }
        public List<string> FlagNames { get; set; } = new List<string>();
        public string DisplayName => string.Join(" / ", FlagNames);
        public string PrimaryFlag => FlagNames.FirstOrDefault() ?? "";
        
        // Context hints for which item types each flag applies to
        public Dictionary<string, string> ContextHints { get; set; } = new Dictionary<string, string>();
    }
    
    private static readonly List<OverlappingFlagGroup> OverlappingGroups = new List<OverlappingFlagGroup>
    {
        // Bit 14 (0x4000)
        new OverlappingFlagGroup
        {
            BitValue = 0x4000UL,
            FlagNames = new List<string> { "itp_no_parry", "itp_shield_no_parry" },
            ContextHints = new Dictionary<string, string>
            {
                { "itp_no_parry", "Weapons" },
                { "itp_shield_no_parry", "Shields (WSE2)" }
            }
        },
        
        // Bit 24 (0x1000000)
        new OverlappingFlagGroup
        {
            BitValue = 0x1000000UL,
            FlagNames = new List<string> { "itp_covers_legs", "itp_doesnt_cover_hair", "itp_can_penetrate_shield" },
            ContextHints = new Dictionary<string, string>
            {
                { "itp_covers_legs", "Body Armor" },
                { "itp_doesnt_cover_hair", "Head Armor" },
                { "itp_can_penetrate_shield", "Weapons" }
            }
        },
        
        // Bit 29 (0x20000000)
        new OverlappingFlagGroup
        {
            BitValue = 0x20000000UL,
            FlagNames = new List<string> { "itp_civilian", "itp_next_item_as_melee" },
            ContextHints = new Dictionary<string, string>
            {
                { "itp_civilian", "Armor" },
                { "itp_next_item_as_melee", "Ranged Weapons" }
            }
        },
        
        // Bit 30 (0x40000000)
        new OverlappingFlagGroup
        {
            BitValue = 0x40000000UL,
            FlagNames = new List<string> { "itp_fit_to_head", "itp_offset_lance" },
            ContextHints = new Dictionary<string, string>
            {
                { "itp_fit_to_head", "Head Armor" },
                { "itp_offset_lance", "Polearms" }
            }
        },
        
        // Bit 31 (0x80000000)
        new OverlappingFlagGroup
        {
            BitValue = 0x80000000UL,
            FlagNames = new List<string> { "itp_covers_head", "itp_couchable" },
            ContextHints = new Dictionary<string, string>
            {
                { "itp_covers_head", "Body Armor" },
                { "itp_couchable", "Polearms" }
            }
        },
        
        // Bit 60 (0x1000000000000000)
        new OverlappingFlagGroup
        {
            BitValue = BigInteger.Parse("1000000000000000", System.Globalization.NumberStyles.HexNumber),
            FlagNames = new List<string> { "itp_offset_mortschlag", "itp_covers_hands" },
            ContextHints = new Dictionary<string, string>
            {
                { "itp_offset_mortschlag", "Weapons (WSE2)" },
                { "itp_covers_hands", "Body Armor (WSE2)" }
            }
        },
    };
    
    // UNIQUE PROPERTY FLAGS (bits 12+) - No overlapping
    private static readonly Dictionary<string, BigInteger> UniquePropertyFlags = new Dictionary<string, BigInteger>
    {
        // Core property flags
        { "itp_unique", 0x1000UL },
        { "itp_always_loot", 0x2000UL },
        // Note: 0x4000 is in overlapping group (itp_no_parry / itp_shield_no_parry)
        { "itp_default_ammo", 0x8000UL },
        { "itp_merchandise", 0x10000UL },
        { "itp_wooden_attack", 0x20000UL },
        { "itp_wooden_parry", 0x40000UL },
        { "itp_food", 0x80000UL },
        
        { "itp_cant_reload_on_horseback", 0x100000UL },
        { "itp_two_handed", 0x200000UL },
        { "itp_primary", 0x400000UL },
        { "itp_secondary", 0x800000UL },
        
        // Note: 0x1000000 is in overlapping group
        
        { "itp_consumable", 0x2000000UL },
        { "itp_bonus_against_shield", 0x4000000UL },
        { "itp_penalty_with_shield", 0x8000000UL },
        { "itp_cant_use_on_horseback", 0x10000000UL },
        
        // Note: 0x20000000, 0x40000000, 0x80000000 are in overlapping groups
        
        { "itp_crush_through", 0x100000000UL },
        { "itp_remove_item_on_use", 0x400000000UL },
        { "itp_unbalanced", 0x800000000UL },
        
        { "itp_covers_beard", 0x1000000000UL },
        { "itp_no_pick_up_from_ground", 0x2000000000UL },
        { "itp_can_knock_down", 0x4000000000UL },
        { "itp_covers_hair", 0x8000000000UL },
        
        { "itp_force_show_body", 0x10000000000UL },
        { "itp_force_show_left_hand", 0x20000000000UL },
        { "itp_force_show_right_hand", 0x40000000000UL },
        { "itp_covers_hair_partially", 0x80000000000UL },
        
        { "itp_extra_penetration", 0x100000000000UL },
        { "itp_has_bayonet", 0x200000000000UL },
        { "itp_cant_reload_while_moving", 0x400000000000UL },
        { "itp_ignore_gravity", 0x800000000000UL },
        { "itp_ignore_friction", 0x1000000000000UL },
        { "itp_is_pike", 0x2000000000000UL },
        { "itp_offset_musket", 0x4000000000000UL },
        { "itp_no_blur", 0x8000000000000UL },
        
        { "itp_cant_reload_while_moving_mounted", 0x10000000000000UL },
        { "itp_has_upper_stab", 0x20000000000000UL },
        { "itp_disable_agent_sounds", 0x40000000000000UL },
        
        // WSE2 unique flags
        { "itp_crush_through_any_direction", BigInteger.Parse("2000000000000000", System.Globalization.NumberStyles.HexNumber) },
        { "itp_offset_flip", BigInteger.Parse("4000000000000000", System.Globalization.NumberStyles.HexNumber) },
    };
    
    // Combined legacy dictionary for backward compatibility
    private static Dictionary<string, BigInteger> _allPropertyFlags;
    private static Dictionary<string, BigInteger> AllPropertyFlags
    {
        get
        {
            if (_allPropertyFlags == null)
            {
                _allPropertyFlags = new Dictionary<string, BigInteger>(UniquePropertyFlags);
                foreach (var group in OverlappingGroups)
                {
                    foreach (var flagName in group.FlagNames)
                    {
                        if (!_allPropertyFlags.ContainsKey(flagName))
                        {
                            _allPropertyFlags[flagName] = group.BitValue;
                        }
                    }
                }
            }
            return _allPropertyFlags;
        }
    }
    
    // PUBLIC API
    
    /// <summary>
    /// Gets the item type from bits 0-4 (ENCODED VALUE, not bitwise flag!)
    /// </summary>
    public static string GetItemType(BigInteger value)
    {
        BigInteger typeValue = value & 0x1F;
        var type = ItemTypes.FirstOrDefault(x => x.Value == typeValue);
        return type.Key ?? (typeValue == 0 ? "" : $"itp_type_unknown_{typeValue:X}");
    }

    /// <summary>
    /// Gets the attachment flag from bits 8-11 (ENCODED VALUE)
    /// </summary>
    public static string GetAttachmentFlag(BigInteger value)
    {
        BigInteger attachValue = (value >> 8) & 0x0F;
        BigInteger fullValue = attachValue << 8;
        var attachment = AttachmentFlags.FirstOrDefault(x => x.Value == fullValue);
        return attachment.Key ?? "";
    }
    
    /// <summary>
    /// Decodes all active flags from a value, returning unique flags and overlapping group indices
    /// </summary>
    public static (List<string> UniqueFlags, HashSet<int> ActiveOverlappingGroups) DecodeAllFlags(BigInteger value)
    {
        var uniqueFlags = new List<string>();
        var activeGroups = new HashSet<int>();
        
        // Mask out item type (0-7) AND attachment flags (8-11)
        BigInteger propertyBits = value & ~0xFFFUL;
        
        // Check unique flags
        foreach (var flag in UniquePropertyFlags.OrderBy(x => x.Value))
        {
            if ((propertyBits & flag.Value) != 0)
            {
                uniqueFlags.Add(flag.Key);
            }
        }
        
        // Check overlapping groups
        for (int i = 0; i < OverlappingGroups.Count; i++)
        {
            if ((propertyBits & OverlappingGroups[i].BitValue) != 0)
            {
                activeGroups.Add(i);
            }
        }
        
        return (uniqueFlags, activeGroups);
    }
    
    /// <summary>
    /// Legacy method - Decodes property flags (bits 12+)
    /// </summary>
    public static List<string> DecodePropertyFlags(BigInteger value)
    {
        var result = new List<string>();
        BigInteger propertyBits = value & ~0xFFFUL;
        
        // Use a set to track which bit values we've already added a flag for
        var processedBits = new HashSet<BigInteger>();
        
        foreach (var flag in AllPropertyFlags.OrderBy(x => x.Value))
        {
            if ((propertyBits & flag.Value) != 0 && !processedBits.Contains(flag.Value))
            {
                result.Add(flag.Key);
                processedBits.Add(flag.Value);
            }
        }
        
        return result;
    }
    
    // STATIC ACCESSORS
    
    public static Dictionary<string, BigInteger> GetItemTypes() => 
        new Dictionary<string, BigInteger>(ItemTypes);
    
    public static Dictionary<string, BigInteger> GetAttachmentFlags() => 
        new Dictionary<string, BigInteger>(AttachmentFlags);
    
    /// <summary>
    /// Returns only the unique (non-overlapping) property flags
    /// </summary>
    public static Dictionary<string, BigInteger> GetUniquePropertyFlags() => 
        new Dictionary<string, BigInteger>(UniquePropertyFlags);
    
    /// <summary>
    /// Returns the overlapping flag groups
    /// </summary>
    public static List<OverlappingFlagGroup> GetOverlappingGroups() => 
        OverlappingGroups.ToList();
    
    /// <summary>
    /// Legacy method - Returns all property flags (including overlapping ones)
    /// </summary>
    public static Dictionary<string, BigInteger> GetPropertyFlags() => 
        new Dictionary<string, BigInteger>(AllPropertyFlags);
    
    /// <summary>
    /// Gets the bit value for any flag name (unique or overlapping)
    /// </summary>
    public static BigInteger GetFlagBitValue(string flagName)
    {
        if (UniquePropertyFlags.TryGetValue(flagName, out var value))
            return value;
            
        var group = OverlappingGroups.FirstOrDefault(g => g.FlagNames.Contains(flagName));
        return group?.BitValue ?? 0;
    }
    
    /// <summary>
    /// Checks if a flag name is part of an overlapping group
    /// </summary>
    public static bool IsOverlappingFlag(string flagName)
    {
        return OverlappingGroups.Any(g => g.FlagNames.Contains(flagName));
    }
    
    /// <summary>
    /// Gets the overlapping group containing a flag, or null if unique
    /// </summary>
    public static OverlappingFlagGroup GetOverlappingGroupForFlag(string flagName)
    {
        return OverlappingGroups.FirstOrDefault(g => g.FlagNames.Contains(flagName));
    }
}