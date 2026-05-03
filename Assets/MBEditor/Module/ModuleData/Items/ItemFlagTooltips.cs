using System.Collections.Generic;
using System.Numerics;

public static class ItemFlagTooltips
{
    // ITEM TYPE TOOLTIPS
    private static readonly Dictionary<string, string> ItemTypeTooltips = new Dictionary<string, string>
    {
        { "itp_type_horse", "Mount - Can be ridden by agents. Determines idle/movement animations." },
        { "itp_type_one_handed_wpn", "One-Handed Weapon - Can be used with shields. Uses one-handed animations." },
        { "itp_type_two_handed_wpn", "Two-Handed Weapon - Requires both hands. Uses two-handed animations." },
        { "itp_type_polearm", "Polearm - Long reach weapon. Can be couched if flagged." },
        { "itp_type_arrows", "Arrows - Ammunition for bows." },
        { "itp_type_bolts", "Bolts - Ammunition for crossbows." },
        { "itp_type_shield", "Shield - Defensive equipment, blocks attacks." },
        { "itp_type_bow", "Bow - Ranged weapon requiring Power Draw skill." },
        { "itp_type_crossbow", "Crossbow - Ranged weapon with reload mechanism." },
        { "itp_type_thrown", "Thrown Weapon - Can be thrown at enemies." },
        { "itp_type_goods", "Trade Goods - Food or merchandise for trading." },
        { "itp_type_head_armor", "Head Armor - Protects the head, replaces head mesh." },
        { "itp_type_body_armor", "Body Armor - Protects torso, replaces body mesh." },
        { "itp_type_foot_armor", "Foot Armor - Protects legs, replaces leg mesh." },
        { "itp_type_hand_armor", "Hand Armor - Protects hands, replaces hand mesh." },
        { "itp_type_pistol", "Pistol - Firearm with limited range." },
        { "itp_type_musket", "Musket - Long-range firearm." },
        { "itp_type_bullets", "Bullets - Ammunition for firearms. Requires ixmesh_inventory or leaves flying mesh in scene." },
        { "itp_type_animal", "Animal - Like horses but cannot be mounted by agents." },
        { "itp_type_book", "Book - Grants skill/attribute bonuses when read or carried." },
    };

    // ATTACHMENT FLAG TOOLTIPS (MUTUALLY EXCLUSIVE)
    private static readonly Dictionary<string, string> AttachmentFlagTooltips = new Dictionary<string, string>
    {
        { "itp_force_attach_left_hand", "Force Attach Left Hand - Item mesh attaches to left hand bone when drawn." },
        { "itp_force_attach_right_hand", "Force Attach Right Hand - Item mesh attaches to right hand bone when drawn." },
        { "itp_force_attach_left_forearm", "Force Attach Left Forearm - Item mesh attaches to left forearm bone when drawn." },
        { "itp_attach_armature", "Attach Armature - Required for rigged items with bone animations." },
    };

    // PROPERTY FLAG TOOLTIPS (BITWISE COMBINABLE)
    private static readonly Dictionary<string, string> PropertyFlagTooltips = new Dictionary<string, string>
    {
        // Core Properties
        { "itp_unique", "Unique - Cannot be looted via normal post-battle loot screen." },
        { "itp_always_loot", "Always Loot - Always appears in post-battle loot screen." },
        { "itp_no_parry", "No Parry - Adds 'Can't be used to block' description (must also remove parry itc flags)." },
        { "itp_default_ammo", "Default Ammo - Sets as default ammunition (e.g. preloaded bolt for crossbows)." },
        { "itp_merchandise", "Merchandise - Available in shop inventories." },
        { "itp_wooden_attack", "Wooden Attack - Weapon makes wooden sound when attacking." },
        { "itp_wooden_parry", "Wooden Parry - Weapon makes wooden sound when parrying." },
        { "itp_food", "Food - Provides morale bonus to party (inconsistently used in Native)." },
        
        // Usage Restrictions
        { "itp_cant_reload_on_horseback", "Can't Reload On Horseback - Prevents reloading while mounted." },
        { "itp_two_handed", "Two-Handed - Cannot be used with shields simultaneously." },
        { "itp_primary", "Primary - AI considers this when choosing primary melee weapon (REQUIRED for AI to wield!)." },
        { "itp_secondary", "Secondary - Deprecated. No longer used." },
        
        // Coverage Flags (context-dependent)
        { "itp_covers_legs", "Covers Legs - Removes leg mesh from body. Without this, loads purple def_trousers submesh." },
        { "itp_doesnt_cover_hair", "Doesn't Cover Hair - Item won't hide hair (may cause clipping)." },
        { "itp_can_penetrate_shield", "Can Penetrate Shield - Ammunition can pierce through shields (ammo only)." },
        
        // Consumables & Combat
        { "itp_consumable", "Consumable - Item perishes slowly each day (inconsistently used in Native)." },
        { "itp_bonus_against_shield", "Bonus vs Shields - 2.0x damage vs shields, 1.1x vs destructible constructions." },
        { "itp_penalty_with_shield", "Penalty With Shield - Reduced damage when used alongside a shield." },
        { "itp_cant_use_on_horseback", "Can't Use On Horseback - Disables item usage while mounted." },
        
        // Mode Switches (context-dependent)
        { "itp_civilian", "Civilian - Worn by heroes in civilian missions (af_require_civilian flag)." },
        { "itp_next_item_as_melee", "Next Item As Melee - Uses next item as melee mode. Meshes must match." },
        
        // Position Offsets (context-dependent)
        { "itp_fit_to_head", "Fit To Head - Deforms helmet to adapt to head mesh shape." },
        { "itp_offset_lance", "Offset Lance - Moves polearm forward for couching grip position." },
        
        // Head Coverage (context-dependent)
        { "itp_covers_head", "Covers Head - Removes head mesh entirely." },
        { "itp_couchable", "Couchable - Spear/lance can be couched. Applies couched damage on successful hits." },
        
        // Combat Mechanics
        { "itp_crush_through", "Crush Through - Can break through blocks dealing damage/stun. Overhead only on foot." },
        { "itp_remove_item_on_use", "Remove On Use - Ammo won't refill after battle (agent_refill_ammo won't work)." },
        { "itp_unbalanced", "Unbalanced - Can't cancel mid-swing. Delays blocking after attack finishes." },
        
        // Mesh Visibility
        { "itp_covers_beard", "Covers Beard - Removes beard mesh (armor only)." },
        { "itp_no_pick_up_from_ground", "No Pick Up - Player cannot pick up this item from ground." },
        { "itp_can_knock_down", "Can Knock Down - Weapon has chance to knock down enemies." },
        { "itp_covers_hair", "Covers Hair - Removes hair mesh (armor only)." },
        
        // Force Show Body Parts
        { "itp_force_show_body", "Force Show Body - Forces body mesh visible (body armor only)." },
        { "itp_force_show_left_hand", "Force Show Left Hand - Forces left hand visible (hand armor only)." },
        { "itp_force_show_right_hand", "Force Show Right Hand - Forces right hand visible (hand armor only)." },
        { "itp_covers_hair_partially", "Covers Hair Partially - Switches hair to frame 1 vertex animation (VC feature)." },
        
        // Advanced Combat
        { "itp_extra_penetration", "Extra Penetration - Multiplies damage (factors in module.ini, WFaS feature)." },
        { "itp_has_bayonet", "Has Bayonet - Adds equip_bayonet/unequip_bayonet animations. Needs itp_next_item_as_melee." },
        { "itp_cant_reload_while_moving", "Can't Reload Moving - Forces player to stand still for reload." },
        { "itp_ignore_gravity", "Ignore Gravity - Projectile trajectory doesn't bend downward (no bullet drop)." },
        { "itp_ignore_friction", "Ignore Friction - Projectile doesn't slow down in air." },
        { "itp_is_pike", "Is Pike - Enables pike bracing animation when crouching." },
        { "itp_offset_musket", "Offset Musket - Flips item upside down, offset -20 + weapon_length." },
        { "itp_no_blur", "No Blur - Removes motion blur effect when swinging spears." },
        
        // Mounted Combat
        { "itp_cant_reload_while_moving_mounted", "Can't Reload Mounted Moving - Horse must stand still for reload." },
        { "itp_has_upper_stab", "Has Upper Stab - Overswing attacks use thrust damage values instead of swing." },
        { "itp_disable_agent_sounds", "Disable Agent Sounds - Disables agent sounds (not voices). Horse agents only." },
        
        // WSE2 Extensions
        { "itp_shield_no_parry", "Shield No Parry - Left handed item without shield functionality (WSE2 only)." },
        { "itp_offset_mortschlag", "Offset Mortschlag - Offsets melee weapon to mortschlag grip (WSE2 only)." },
        { "itp_covers_hands", "Covers Hands - Removes hand meshes (body armor, WSE2 only)." },
        { "itp_crush_through_any_direction", "Crush Through Any Direction - Crush through works in all attack directions (WSE2)." },
        { "itp_offset_flip", "Offset Flip - Flips melee weapon model 180 degrees on y-axis (WSE2 only)." },
    };

    // PUBLIC ACCESSORS
    
    public static string GetItemTypeTooltip(string itemType)
    {
        return ItemTypeTooltips.TryGetValue(itemType, out string tooltip) 
            ? tooltip 
            : $"Unknown item type: {itemType}";
    }

    public static string GetAttachmentFlagTooltip(string attachmentFlag)
    {
        if (string.IsNullOrEmpty(attachmentFlag))
            return "No attachment flag (default behavior)";
            
        return AttachmentFlagTooltips.TryGetValue(attachmentFlag, out string tooltip)
            ? tooltip
            : $"Unknown attachment flag: {attachmentFlag}";
    }

    public static string GetPropertyFlagTooltip(string propertyFlag)
    {
        return PropertyFlagTooltips.TryGetValue(propertyFlag, out string tooltip)
            ? tooltip
            : $"Unknown property flag: {propertyFlag}";
    }

    public static List<string> GetAllPropertyFlagTooltips(BigInteger value)
    {
        var tooltips = new List<string>();
        var flags = ItemFlagPropertyDecoder.DecodePropertyFlags(value);
        
        foreach (var flag in flags)
        {
            tooltips.Add(GetPropertyFlagTooltip(flag));
        }
        
        return tooltips;
    }
}
