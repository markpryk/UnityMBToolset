using System;
using System.Collections.Generic;
using System.Numerics;
using UnityEngine;

public static class ItemStatDecoder
{
    // BIT FIELD DEFINITIONS from header_items.py
    
    // Masks
    private const ulong IBF_ARMOR_MASK = 0x00000000000000FF;
    private const ulong IBF_DAMAGE_MASK = 0x00000000000003FF;
    private const ulong IBF_10BIT_MASK = 0x00000000000003FF;
    private const ulong IBF_HITPOINTS_MASK = 0x000000000000FFFF;
    
    // Bit positions
    public const int IBF_HEAD_ARMOR_BITS = 0;
    public const int IBF_BODY_ARMOR_BITS = 8;
    public const int IBF_LEG_ARMOR_BITS = 16;
    public const int IBF_WEIGHT_BITS = 24;
    public const int IBF_DIFFICULTY_BITS = 32;
    public const int IBF_HITPOINTS_BITS = 40;
    
    public const int IWF_SWING_DAMAGE_BITS = 50;
    public const int IWF_SWING_DAMAGE_TYPE_BITS = 58;
    public const int IWF_THRUST_DAMAGE_BITS = 60;
    public const int IWF_THRUST_DAMAGE_TYPE_BITS = 68;
    public const int IWF_WEAPON_LENGTH_BITS = 70;
    public const int IWF_SPEED_RATING_BITS = 80;
    public const int IWF_SHOOT_SPEED_BITS = 90;
    public const int IWF_MAX_AMMO_BITS = 100;
    public const int IWF_ABUNDANCE_BITS = 110;
    public const int IWF_ACCURACY_BITS = 16; // Reuses leg_armor
    public const int IWF_DAMAGE_TYPE_BITS = 8;
    
    // Damage types
    public enum DamageType
    {
        Cut = 0,
        Pierce = 1,
        Blunt = 2
    }
    
    // CONTEXT-AWARE STAT STRUCTURE
    
    public class ItemStats
    {
        // Item type (needed for context-sensitive interpretation)
        public string ItemType { get; set; }
        
        // General stats (universal - apply to all items)
        public float Weight { get; set; }
        public int Difficulty { get; set; }
        public int Abundance { get; set; }
        
        // Context-sensitive fields (interpretation depends on item type)
        // Bits 0-7 (head_armor / food_quality)
        public int HeadArmorOrFoodQuality { get; set; }
        
        // Bits 8-15 (body_armor / shield_armor / horse_armor)
        public int BodyArmorOrShieldArmorOrHorseArmor { get; set; }
        
        // Bits 16-23 (leg_armor / accuracy)
        public int LegArmorOrAccuracy { get; set; }
        
        // Bits 40-55 (hit_points - shields and weapons)
        public int HitPoints { get; set; }
        
        // Bits 70-79 (weapon_length / shield_width / horse_scale)
        public int WeaponLengthOrShieldWidthOrHorseScale { get; set; }
        
        // Bits 80-87 (spd_rtng / horse_maneuver)
        public int SpeedRatingOrHorseManeuver { get; set; }
        
        // Bits 90-99 (shoot_speed / shield_height / horse_speed)
        public int ShootSpeedOrShieldHeightOrHorseSpeed { get; set; }
        
        // Bits 50-59 (swing_damage)
        public int SwingDamage { get; set; }
        public DamageType SwingDamageType { get; set; }
        
        // Bits 60-69 (thrust_damage / horse_charge)
        public int ThrustDamageOrHorseCharge { get; set; }
        public DamageType ThrustDamageType { get; set; }
        
        // Bits 100-107 (max_ammo)
        public int MaxAmmo { get; set; }
        
        // CONTEXT-AWARE ACCESSORS
        
        // Armor accessors
        public int HeadArmor => IsArmorType ? HeadArmorOrFoodQuality : 0;
        public int BodyArmor => IsArmorOrHandArmor ? BodyArmorOrShieldArmorOrHorseArmor : 0;
        public int LegArmor => IsArmorType ? LegArmorOrAccuracy : 0;
        
        // Food accessor
        public int FoodQuality => ItemType == "itp_type_goods" ? HeadArmorOrFoodQuality : 0;
        
        // Shield accessors
        public int ShieldArmor => ItemType == "itp_type_shield" ? BodyArmorOrShieldArmorOrHorseArmor : 0;
        public int ShieldWidth => ItemType == "itp_type_shield" ? WeaponLengthOrShieldWidthOrHorseScale : 0;
        public int ShieldHeight => ItemType == "itp_type_shield" ? ShootSpeedOrShieldHeightOrHorseSpeed : 0;
        public int ShieldSpeed => ItemType == "itp_type_shield" ? SpeedRatingOrHorseManeuver : 0;
        
        // Horse accessors
        public int HorseArmor => IsHorseType ? BodyArmorOrShieldArmorOrHorseArmor : 0;
        public int HorseSpeed => IsHorseType ? ShootSpeedOrShieldHeightOrHorseSpeed : 0;
        public int HorseManeuver => IsHorseType ? SpeedRatingOrHorseManeuver : 0;
        public int HorseCharge => IsHorseType ? ThrustDamageOrHorseCharge : 0;
        public float HorseScale => IsHorseType ? WeaponLengthOrShieldWidthOrHorseScale / 100f : 1f;
        
        // Weapon accessors
        public int WeaponLength => IsWeaponType ? WeaponLengthOrShieldWidthOrHorseScale : 0;
        public int SpeedRating => IsWeaponType ? SpeedRatingOrHorseManeuver : 0;
        public int ThrustDamage => IsWeaponType ? ThrustDamageOrHorseCharge : 0;
        
        // Ranged weapon accessors
        public int ShootSpeed => IsRangedWeaponType ? ShootSpeedOrShieldHeightOrHorseSpeed : 0;
        public int Accuracy => IsRangedWeaponType ? LegArmorOrAccuracy : 0;
        
        // TYPE CHECKING HELPERS
        
        private bool IsArmorType =>
            ItemType == "itp_type_head_armor" ||
            ItemType == "itp_type_body_armor" ||
            ItemType == "itp_type_foot_armor";
        
        private bool IsArmorOrHandArmor =>
            IsArmorType || ItemType == "itp_type_hand_armor";
        
        private bool IsHorseType =>
            ItemType == "itp_type_horse" || ItemType == "itp_type_animal";
        
        private bool IsWeaponType =>
            ItemType == "itp_type_one_handed_wpn" ||
            ItemType == "itp_type_two_handed_wpn" ||
            ItemType == "itp_type_polearm" ||
            ItemType == "itp_type_arrows" ||
            ItemType == "itp_type_bolts" ||
            ItemType == "itp_type_thrown" ||
            IsRangedWeaponType;
        
        private bool IsRangedWeaponType =>
            ItemType == "itp_type_bow" ||
            ItemType == "itp_type_crossbow" ||
            ItemType == "itp_type_pistol" ||
            ItemType == "itp_type_musket";
    }
    
    // RAW EXTRACTION METHODS (no context interpretation)
    
    /// <summary>
    /// Extracts weight from bits 24-31 (value * 0.25 = kg)
    /// </summary>
    public static float GetWeight(BigInteger value)
    {
        ulong extracted = (ulong)((value >> IBF_WEIGHT_BITS) & IBF_ARMOR_MASK);
        return extracted * 0.25f;
    }
    
    /// <summary>
    /// Extracts head armor from bits 0-7
    /// NOTE: Also used for food_quality depending on item type
    /// </summary>
    public static int GetHeadArmor(BigInteger value)
    {
        return (int)((value >> IBF_HEAD_ARMOR_BITS) & IBF_ARMOR_MASK);
    }
    
    /// <summary>
    /// Extracts body armor from bits 8-15
    /// NOTE: Also used for shield_armor and horse_armor depending on item type
    /// </summary>
    public static int GetBodyArmor(BigInteger value)
    {
        return (int)((value >> IBF_BODY_ARMOR_BITS) & IBF_ARMOR_MASK);
    }
    
    /// <summary>
    /// Extracts leg armor from bits 16-23
    /// NOTE: Also used for accuracy depending on item type
    /// </summary>
    public static int GetLegArmor(BigInteger value)
    {
        return (int)((value >> IBF_LEG_ARMOR_BITS) & IBF_ARMOR_MASK);
    }
    
    /// <summary>
    /// Extracts difficulty from bits 32-39
    /// </summary>
    public static int GetDifficulty(BigInteger value)
    {
        return (int)((value >> IBF_DIFFICULTY_BITS) & IBF_ARMOR_MASK);
    }
    
    /// <summary>
    /// Extracts hit points from bits 40-55
    /// </summary>
    public static int GetHitPoints(BigInteger value)
    {
        return (int)((value >> IBF_HITPOINTS_BITS) & IBF_HITPOINTS_MASK);
    }
    
    /// <summary>
    /// Extracts speed rating from bits 80-87
    /// NOTE: Also used for horse_maneuver depending on item type
    /// </summary>
    public static int GetSpeedRating(BigInteger value)
    {
        return (int)((value >> IWF_SPEED_RATING_BITS) & IBF_ARMOR_MASK);
    }
    
    /// <summary>
    /// Extracts missile speed from bits 90-99
    /// NOTE: Also used for shield_height and horse_speed depending on item type
    /// </summary>
    public static int GetMissileSpeed(BigInteger value)
    {
        return (int)((value >> IWF_SHOOT_SPEED_BITS) & IBF_10BIT_MASK);
    }
    
    /// <summary>
    /// Extracts weapon length from bits 70-79
    /// NOTE: Also used for shield_width and horse_scale depending on item type
    /// </summary>
    public static int GetWeaponLength(BigInteger value)
    {
        return (int)((value >> IWF_WEAPON_LENGTH_BITS) & IBF_10BIT_MASK);
    }
    
    /// <summary>
    /// Extracts max ammo from bits 100-107
    /// </summary>
    public static int GetMaxAmmo(BigInteger value)
    {
        return (int)((value >> IWF_MAX_AMMO_BITS) & IBF_ARMOR_MASK);
    }
    
    /// <summary>
    /// Extracts swing damage from bits 50-59
    /// Returns: (damage_value, damage_type)
    /// </summary>
    public static (int damage, DamageType type) GetSwingDamage(BigInteger value)
    {
        ulong damageData = (ulong)((value >> IWF_SWING_DAMAGE_BITS) & IBF_DAMAGE_MASK);
        int damage = (int)(damageData & IBF_ARMOR_MASK);
        int damageType = (int)(damageData >> IWF_DAMAGE_TYPE_BITS);
        return (damage, (DamageType)damageType);
    }
    
    /// <summary>
    /// Extracts thrust damage from bits 60-69
    /// NOTE: Also used for horse_charge depending on item type
    /// Returns: (damage_value, damage_type)
    /// </summary>
    public static (int damage, DamageType type) GetThrustDamage(BigInteger value)
    {
        ulong damageData = (ulong)((value >> IWF_THRUST_DAMAGE_BITS) & IBF_DAMAGE_MASK);
        int damage = (int)(damageData & IBF_ARMOR_MASK);
        int damageType = (int)(damageData >> IWF_DAMAGE_TYPE_BITS);
        return (damage, (DamageType)damageType);
    }
    
    /// <summary>
    /// Extracts abundance from bits 110-117
    /// </summary>
    public static int GetAbundance(BigInteger value)
    {
        int abundance = (int)((value >> IWF_ABUNDANCE_BITS) & IBF_ARMOR_MASK);
        return abundance == 0 ? 100 : abundance; // Default is 100
    }
    
    /// <summary>
    /// Extracts accuracy from bits 16-23 (reuses leg_armor position)
    /// </summary>
    public static int GetAccuracy(BigInteger value)
    {
        int accuracy = (int)((value >> IWF_ACCURACY_BITS) & IBF_ARMOR_MASK);
        return accuracy == 0 ? 100 : accuracy; // Default is 100
    }
    
    // CONTEXT-AWARE DECODE
    
    /// <summary>
    /// Decodes all stats from the raw value with context-aware interpretation
    /// </summary>
    public static ItemStats DecodeAllStats(BigInteger value, string itemType)
    {
        var swing = GetSwingDamage(value);
        var thrust = GetThrustDamage(value);
        
        return new ItemStats
        {
            ItemType = itemType,
            
            // Universal stats
            Weight = GetWeight(value),
            Difficulty = GetDifficulty(value),
            Abundance = GetAbundance(value),
            
            // Context-sensitive raw values
            HeadArmorOrFoodQuality = GetHeadArmor(value),
            BodyArmorOrShieldArmorOrHorseArmor = GetBodyArmor(value),
            LegArmorOrAccuracy = GetLegArmor(value),
            
            HitPoints = GetHitPoints(value),
            WeaponLengthOrShieldWidthOrHorseScale = GetWeaponLength(value),
            SpeedRatingOrHorseManeuver = GetSpeedRating(value),
            ShootSpeedOrShieldHeightOrHorseSpeed = GetMissileSpeed(value),
            
            SwingDamage = swing.damage,
            SwingDamageType = swing.type,
            ThrustDamageOrHorseCharge = thrust.damage,
            ThrustDamageType = thrust.type,
            
            MaxAmmo = GetMaxAmmo(value)
        };
    }
    
    // CONTEXT-AWARE FORMATTING
    
    /// <summary>
    /// Returns human-readable stat description based on item type
    /// NOW USES CONTEXT-AWARE ACCESSORS
    /// </summary>
    public static Dictionary<string, string> GetRelevantStats(BigInteger statValue, string itemType)
    {
        var stats = DecodeAllStats(statValue, itemType);
        var result = new Dictionary<string, string>();
        
        // Always show these
        result["Weight"] = $"{stats.Weight:F2} kg";
        if (stats.Difficulty > 0)
            result["Difficulty"] = stats.Difficulty.ToString();
        if (stats.Abundance != 100)
            result["Abundance"] = stats.Abundance.ToString();
        
        // Type-specific stats using context-aware accessors
        switch (itemType)
        {
            case "itp_type_one_handed_wpn":
            case "itp_type_two_handed_wpn":
            case "itp_type_polearm":
                if (stats.WeaponLength > 0)
                    result["Length"] = $"{stats.WeaponLength} cm";
                if (stats.SpeedRating > 0)
                    result["Speed"] = stats.SpeedRating.ToString();
                if (stats.SwingDamage > 0)
                    result["Swing Damage"] = $"{stats.SwingDamage} ({stats.SwingDamageType})";
                if (stats.ThrustDamage > 0)
                    result["Thrust Damage"] = $"{stats.ThrustDamage} ({stats.ThrustDamageType})";
                break;
                
            case "itp_type_bow":
            case "itp_type_crossbow":
            case "itp_type_pistol":
            case "itp_type_musket":
                if (stats.SpeedRating > 0)
                    result["Speed"] = stats.SpeedRating.ToString();
                if (stats.ShootSpeed > 0)
                    result["Missile Speed"] = stats.ShootSpeed.ToString();
                if (stats.ThrustDamage > 0)
                    result["Damage"] = $"{stats.ThrustDamage} ({stats.ThrustDamageType})";
                if (stats.Accuracy != 100)
                    result["Accuracy"] = stats.Accuracy.ToString();
                if (stats.MaxAmmo > 0)
                    result["Ammo"] = stats.MaxAmmo.ToString();
                break;
                
            case "itp_type_shield":
                if (stats.HitPoints > 0)
                    result["Hit Points"] = stats.HitPoints.ToString();
                if (stats.ShieldArmor > 0)
                    result["Armor"] = stats.ShieldArmor.ToString();
                if (stats.ShieldSpeed > 0)
                    result["Speed"] = stats.ShieldSpeed.ToString();
                if (stats.ShieldWidth > 0)
                    result["Width"] = stats.ShieldWidth.ToString();
                if (stats.ShieldHeight > 0)
                    result["Height"] = stats.ShieldHeight.ToString();
                break;
                
            case "itp_type_horse":
            case "itp_type_animal":
                if (stats.HorseArmor > 0)
                    result["Armor"] = stats.HorseArmor.ToString();
                if (stats.HorseSpeed > 0)
                    result["Speed"] = stats.HorseSpeed.ToString();
                if (stats.HorseManeuver > 0)
                    result["Maneuver"] = stats.HorseManeuver.ToString();
                if (stats.HorseCharge > 0)
                    result["Charge"] = stats.HorseCharge.ToString();
                if (stats.HorseScale != 1.0f)
                    result["Scale"] = $"{stats.HorseScale:F2}";
                if (stats.HitPoints > 0)
                    result["Hit Points"] = stats.HitPoints.ToString();
                break;
                
            case "itp_type_head_armor":
                if (stats.HeadArmor > 0)
                    result["Head Armor"] = stats.HeadArmor.ToString();
                if (stats.BodyArmor > 0)
                    result["Body Armor"] = stats.BodyArmor.ToString();
                if (stats.LegArmor > 0)
                    result["Leg Armor"] = stats.LegArmor.ToString();
                if (stats.HitPoints > 0)
                    result["Hit Points"] = stats.HitPoints.ToString();
                break;
                
            case "itp_type_body_armor":
                if (stats.BodyArmor > 0)
                    result["Body Armor"] = stats.BodyArmor.ToString();
                if (stats.LegArmor > 0)
                    result["Leg Armor"] = stats.LegArmor.ToString();
                if (stats.HitPoints > 0)
                    result["Hit Points"] = stats.HitPoints.ToString();
                break;
                
            case "itp_type_foot_armor":
                if (stats.LegArmor > 0)
                    result["Leg Armor"] = stats.LegArmor.ToString();
                if (stats.HitPoints > 0)
                    result["Hit Points"] = stats.HitPoints.ToString();
                break;
                
            case "itp_type_hand_armor":
                if (stats.BodyArmor > 0)
                    result["Body Armor"] = stats.BodyArmor.ToString();
                if (stats.HitPoints > 0)
                    result["Hit Points"] = stats.HitPoints.ToString();
                break;
                
            case "itp_type_arrows":
            case "itp_type_bolts":
            case "itp_type_bullets":
                if (stats.WeaponLength > 0)
                    result["Length"] = $"{stats.WeaponLength} cm";
                if (stats.ThrustDamage > 0)
                    result["Damage"] = $"{stats.ThrustDamage} ({stats.ThrustDamageType})";
                if (stats.MaxAmmo > 0)
                    result["Stack Size"] = stats.MaxAmmo.ToString();
                break;
                
            case "itp_type_thrown":
                if (stats.WeaponLength > 0)
                    result["Length"] = $"{stats.WeaponLength} cm";
                if (stats.SpeedRating > 0)
                    result["Speed"] = stats.SpeedRating.ToString();
                if (stats.ShootSpeed > 0)
                    result["Missile Speed"] = stats.ShootSpeed.ToString();
                if (stats.ThrustDamage > 0)
                    result["Damage"] = $"{stats.ThrustDamage} ({stats.ThrustDamageType})";
                if (stats.MaxAmmo > 0)
                    result["Stack Size"] = stats.MaxAmmo.ToString();
                break;
                
            case "itp_type_goods":
                if (stats.FoodQuality > 0)
                    result["Food Quality"] = stats.FoodQuality.ToString();
                if (stats.MaxAmmo > 0)
                    result["Quantity"] = stats.MaxAmmo.ToString();
                break;
        }
        
        return result;
    }
    
    /// <summary>
    /// Gets a detailed debug breakdown showing both raw values and context-aware interpretation
    /// </summary>
    public static string GetDebugBreakdown(BigInteger statValue, string itemType)
    {
        var stats = DecodeAllStats(statValue, itemType);
        var sb = new System.Text.StringBuilder();
        
        sb.AppendLine($"=== ITEM STATS DECODER DEBUG ===");
        sb.AppendLine($"Item Type: {itemType}");
        sb.AppendLine($"Raw Value: {statValue}");
        sb.AppendLine();
        
        sb.AppendLine("--- UNIVERSAL STATS ---");
        sb.AppendLine($"Weight: {stats.Weight:F2} kg");
        sb.AppendLine($"Difficulty: {stats.Difficulty}");
        sb.AppendLine($"Abundance: {stats.Abundance}");
        sb.AppendLine();
        
        sb.AppendLine("--- RAW VALUES (context-sensitive fields) ---");
        sb.AppendLine($"Bits 0-7   (head_armor/food_quality): {stats.HeadArmorOrFoodQuality}");
        sb.AppendLine($"Bits 8-15  (body_armor/shield_armor/horse_armor): {stats.BodyArmorOrShieldArmorOrHorseArmor}");
        sb.AppendLine($"Bits 16-23 (leg_armor/accuracy): {stats.LegArmorOrAccuracy}");
        sb.AppendLine($"Bits 40-55 (hit_points): {stats.HitPoints}");
        sb.AppendLine($"Bits 70-79 (weapon_length/shield_width/horse_scale): {stats.WeaponLengthOrShieldWidthOrHorseScale}");
        sb.AppendLine($"Bits 80-87 (spd_rtng/horse_maneuver): {stats.SpeedRatingOrHorseManeuver}");
        sb.AppendLine($"Bits 90-99 (shoot_speed/shield_height/horse_speed): {stats.ShootSpeedOrShieldHeightOrHorseSpeed}");
        sb.AppendLine($"Bits 50-59 (swing_damage): {stats.SwingDamage} ({stats.SwingDamageType})");
        sb.AppendLine($"Bits 60-69 (thrust_damage/horse_charge): {stats.ThrustDamageOrHorseCharge} ({stats.ThrustDamageType})");
        sb.AppendLine($"Bits 100-107 (max_ammo): {stats.MaxAmmo}");
        sb.AppendLine();
        
        sb.AppendLine("--- CONTEXT-AWARE INTERPRETATION ---");
        var relevantStats = GetRelevantStats(statValue, itemType);
        foreach (var kvp in relevantStats)
        {
            sb.AppendLine($"{kvp.Key}: {kvp.Value}");
        }
        
        return sb.ToString();
    }
}
