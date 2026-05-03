using System.Collections.Generic;

namespace MountAndBlade.Editor
{
    /// <summary>
    /// Provides tooltip information for Mount & Blade item stat flags.
    /// Maps flag IDs to human-readable descriptions.
    /// </summary>
    public static class ItemStatsTooltips
    {
        /// <summary>
        /// Dictionary mapping flag bit positions to their tooltip descriptions.
        /// Key: Flag bit position (0-31)
        /// Value: Tooltip description string
        /// </summary>
        public static readonly Dictionary<int, string> FlagTooltips = new Dictionary<int, string>
        {
            // Weight (bits 0-10, 11 bits for up to 2047 units of 0.1kg)
            { 0, "Weight: Item weight in 0.1 kg units (0-2047 = 0-204.7 kg)\nAffects inventory management and travel speed on world map" },
            
            // Abundance (bits 11-17, 7 bits for 0-100%)
            { 11, "Abundance: Merchant spawn chance (0-100%)\n100 = standard appearance rate in shops and loot" },
            
            // Head Armor (bits 18-24, 7 bits for 0-127)
            { 18, "Head Armor: Damage reduction for head hits (0-127)\nOnly used by head armor items" },
            
            // Body Armor (bits 25-31, 7 bits for 0-127)
            { 25, "Body Armor: Damage reduction for body hits (0-127)\nUsed by body armor and horse armor" },
            
            // Leg Armor (bits 32-38, 7 bits for 0-127)
            { 32, "Leg Armor: Damage reduction for leg hits (0-127)\nOnly used by leg armor items" },
            
            // Difficulty (bits 39-46, 8 bits for 0-255)
            { 39, "Difficulty: Minimum skill/stat requirement\nWeapons: STR/Power Draw/Power Throw\nArmor: STR\nHorses: Riding skill\nShields: Shield skill (not in Native)" },
            
            // Hit Points (bits 47-54, 8 bits for 0-255)
            { 47, "Hit Points: Maximum durability (0-255)\nOnly used by shields\nReduces when blocking attacks" },
            
            // Speed Rating (bits 55-62, 8 bits for 0-255)
            { 55, "Speed Rating: Attack/reload speed (0-255)\nWeapons: Attack speed\nBows/Crossbows: Reload speed\nShields: Block speed" },
            
            // Missile Speed (bits 63-70, 8 bits for 0-255)
            { 63, "Missile Speed: Projectile velocity in m/s (0-255)\nHigher = flatter trajectory, less drop\nWarning: Values >200 may clip through targets" },
            
            // Weapon Length (bits 71-78, 8 bits for 0-255 cm)
            { 71, "Weapon Length: Reach in centimeters (0-255)\nMelee: Attack range\nShields: Half-width (radius)\nAmmo: Penetration depth" },
            
            // Max Ammo (bits 79-86, 8 bits for 0-255)
            { 79, "Max Ammo: Stack size or magazine capacity (0-255)\nArrows/Bolts: Stack size\nCrossbows/Firearms: Shots before reload\nThrown: Items per bundle\nFood: Consumable portions" },
            
            // Thrust Damage (bits 87-94, 8 bits for 0-255)
            { 87, "Thrust Damage: Pierce/stab damage (0-255)\nMelee: Thrust attack damage\nRanged: Projectile base damage\nNote: First 2 bits encode damage type" },
            
            // Swing Damage (bits 95-102, 8 bits for 0-255)
            { 95, "Swing Damage: Slash damage (0-255)\nOnly used by melee weapons\nFirst 2 bits encode damage type (cut/blunt)\nNot used by ranged weapons or shields" },
            
            // Horse Speed (bits 103-109, 7 bits for 0-127)
            { 103, "Horse Speed: Mount speed on battle map (0-127)\nHigher = faster movement\nOnly used by horses and animals" },
            
            // Horse Maneuver (bits 110-116, 7 bits for 0-127)
            { 110, "Horse Maneuver: Mount agility (0-127)\nAffects turning speed and responsiveness\nOnly used by horses and animals" },
            
            // Horse Charge (bits 117-122, 6 bits for 0-63)
            { 117, "Horse Charge: Collision damage (0-63)\nDamage dealt when charging infantry\nHigher = more damage, less speed loss per collision" },
            
            // Horse Scale (bits 123-131, 9 bits for fixed point 0-511)
            { 123, "Horse Scale: Mount size multiplier\nFixed point value (default = 100)\nAffects visual size only\nBuggy: Returns value-1 due to rounding" },
            
            // Food Quality (bits 132-139, 8 bits for 0-255)
            { 132, "Food Quality: Morale impact (0-255)\n>50 = increases morale\n<50 = decreases morale\nOnly used by food items" },
            
            // Accuracy (bits 140-147, 8 bits for 0-100%)
            { 140, "Accuracy: Hit precision percentage (0-100)\n100 = perfect accuracy\nLower = larger spread cone\nUsed by bows, crossbows, and firearms" },
        };

        /// <summary>
        /// Dictionary mapping flag categories to their field descriptions.
        /// Useful for grouping related flags in UI.
        /// </summary>
        public static readonly Dictionary<string, string> CategoryDescriptions = new Dictionary<string, string>
        {
            { "General", "Basic item properties affecting all item types" },
            { "Armor", "Protection values for head, body, and leg armor" },
            { "Melee", "Damage and speed statistics for melee weapons" },
            { "Ranged", "Projectile and accuracy statistics for ranged weapons" },
            { "Ammunition", "Properties for arrows, bolts, and thrown weapons" },
            { "Shield", "Durability and blocking effectiveness" },
            { "Horse", "Mount statistics for speed, maneuver, and charge" },
            { "Food", "Consumable properties and morale effects" },
        };

        /// <summary>
        /// Get tooltip for a specific flag bit position
        /// </summary>
        /// <param name="bitPosition">The bit position (0-31 for standard flags)</param>
        /// <returns>Tooltip string, or generic message if not found</returns>
        public static string GetTooltip(int bitPosition)
        {
            if (FlagTooltips.TryGetValue(bitPosition, out string tooltip))
            {
                return tooltip;
            }
            return $"Flag bit {bitPosition}: No description available";
        }

        /// <summary>
        /// Get damage type description based on the damage value's lowest 2 bits
        /// </summary>
        public static string GetDamageTypeTooltip(int damageType)
        {
            switch (damageType)
            {
                case 0:
                    return "Cut Damage: Slicing attacks\n+Bonus vs unarmored\n-Penalty vs heavy armor\nKills at 0 HP";
                case 1:
                    return "Pierce Damage: Penetrating attacks\n+50% vs heavy armor\nKills at 0 HP\nRequired for anti-cavalry polearms";
                case 2:
                    return "Blunt Damage: Crushing attacks\n+50% vs heavy armor\nKnocks unconscious at 0 HP\nAllows prisoner capture";
                default:
                    return "Unknown damage type";
            }
        }

        /// <summary>
        /// Get category name for a flag bit position
        /// </summary>
        public static string GetFlagCategory(int bitPosition)
        {
            if (bitPosition >= 0 && bitPosition <= 10) return "General"; // Weight
            if (bitPosition >= 11 && bitPosition <= 17) return "General"; // Abundance
            if (bitPosition >= 18 && bitPosition <= 38) return "Armor"; // Head/Body/Leg armor
            if (bitPosition >= 39 && bitPosition <= 46) return "General"; // Difficulty
            if (bitPosition >= 47 && bitPosition <= 54) return "Shield"; // Hit points
            if (bitPosition >= 55 && bitPosition <= 62) return "Melee"; // Speed rating
            if (bitPosition >= 63 && bitPosition <= 70) return "Ranged"; // Missile speed
            if (bitPosition >= 71 && bitPosition <= 78) return "Melee"; // Weapon length
            if (bitPosition >= 79 && bitPosition <= 86) return "Ammunition"; // Max ammo
            if (bitPosition >= 87 && bitPosition <= 94) return "Melee"; // Thrust damage
            if (bitPosition >= 95 && bitPosition <= 102) return "Melee"; // Swing damage
            if (bitPosition >= 103 && bitPosition <= 131) return "Horse"; // Horse stats
            if (bitPosition >= 132 && bitPosition <= 139) return "Food"; // Food quality
            if (bitPosition >= 140 && bitPosition <= 147) return "Ranged"; // Accuracy
            
            return "Unknown";
        }
    }
}