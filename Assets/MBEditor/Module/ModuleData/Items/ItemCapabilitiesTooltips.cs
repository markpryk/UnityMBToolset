using System.Collections.Generic;
using System.Numerics;
using UnityEngine;

namespace MountAndBladeTools
{
    /// <summary>
    /// Provides tooltip descriptions for Mount & Blade item capability flags.
    /// </summary>
    public static class ItemCapabilitiesTooltips
    {
        // Bitwise flag tooltips
        private static readonly Dictionary<string, string> BitwiseFlagTooltips = new Dictionary<string, string>
        {
            // One-handed attacks
            { "itcf_thrust_onehanded", "Enables one-handed thrust attack animation" },
            { "itcf_overswing_onehanded", "Enables one-handed overhead swing attack animation" },
            { "itcf_slashright_onehanded", "Enables one-handed right slash attack animation" },
            { "itcf_slashleft_onehanded", "Enables one-handed left slash attack animation" },
            
            // Two-handed attacks
            { "itcf_thrust_twohanded", "Enables two-handed thrust attack animation" },
            { "itcf_overswing_twohanded", "Enables two-handed overhead swing attack animation" },
            { "itcf_slashright_twohanded", "Enables two-handed right slash attack animation" },
            { "itcf_slashleft_twohanded", "Enables two-handed left slash attack animation" },
            
            // Polearm attacks
            { "itcf_thrust_polearm", "Enables polearm thrust attack animation" },
            { "itcf_overswing_polearm", "Enables polearm overhead swing attack animation" },
            { "itcf_slashright_polearm", "Enables polearm right slash attack animation" },
            { "itcf_slashleft_polearm", "Enables polearm left slash attack animation" },
            
            // Basic ranged weapons
            { "itcf_shoot_bow", "Enables bow shooting animation" },
            { "itcf_shoot_javelin", "Enables javelin throwing animation.\n" +
                                    "Note: itcf_shoot_javelin is only usable in Vanilla Mount & Blade, not at the Warband game engine." },
            { "itcf_shoot_crossbow", "Enables crossbow shooting animation" },
            
            // Horseback attacks
            { "itcf_horseback_thrust_onehanded", "Enables one-handed thrust attack while mounted (deprecated)" },
            { "itcf_horseback_overswing_right_onehanded", "Enables right overhead swing while mounted (deprecated)" },
            { "itcf_horseback_overswing_left_onehanded", "Enables left overhead swing while mounted (deprecated)" },
            { "itcf_horseback_slashright_onehanded", "Enables right slash attack while mounted" },
            { "itcf_horseback_slashleft_onehanded", "Enables left slash attack while mounted" },
            { "itcf_thrust_onehanded_lance", "Enables one-handed lance thrust attack" },
            { "itcf_thrust_onehanded_lance_horseback", "Enables lance thrust while mounted (deprecated)" },
            
            // Display options
            { "itcf_show_holster_when_drawn", "Shows holster/scabbard even when weapon is drawn (for swords, bows, throwing weapons)" },
            
            // One-handed parry
            { "itcf_parry_forward_onehanded", "Enables one-handed forward/down parry (block direction: down)" },
            { "itcf_parry_up_onehanded", "Enables one-handed up parry (block direction: up)" },
            { "itcf_parry_right_onehanded", "Enables one-handed right parry (block direction: right)" },
            { "itcf_parry_left_onehanded", "Enables one-handed left parry (block direction: left)" },
            
            // Two-handed parry
            { "itcf_parry_forward_twohanded", "Enables two-handed forward/down parry (block direction: down)" },
            { "itcf_parry_up_twohanded", "Enables two-handed up parry (block direction: up)" },
            { "itcf_parry_right_twohanded", "Enables two-handed right parry (block direction: right)" },
            { "itcf_parry_left_twohanded", "Enables two-handed left parry (block direction: left)" },
            
            // Polearm parry
            { "itcf_parry_forward_polearm", "Enables polearm forward/down parry (block direction: down)" },
            { "itcf_parry_up_polearm", "Enables polearm up parry (block direction: up)" },
            { "itcf_parry_right_polearm", "Enables polearm right parry (block direction: right)" },
            { "itcf_parry_left_polearm", "Enables polearm left parry (block direction: left)" },
            
            // Special attacks
            { "itcf_horseback_slash_polearm", "Enables polearm slash attack while mounted" },
            { "itcf_overswing_spear", "Enables overhead swing for spears" },
            { "itcf_overswing_musket", "Enables overhead swing attack for muskets (bayonet)" },
            { "itcf_thrust_musket", "Enables thrust attack for muskets (bayonet)" },
            
            // Technical
            { "itcf_force_64_bits", "Forces 64-bit flag storage (technical flag)" },
        };
        
        // Encoded throw/shoot types tooltips
        private static readonly Dictionary<string, string> ThrowTypeTooltips = new Dictionary<string, string>
        {
            { "itcf_throw_stone", "Throwing animation without spinning. Projectile doesn't leave mesh when it hits" },
            { "itcf_throw_knife", "Knife throwing animation with spinning. Mesh sticks opposite side from axes" },
            { "itcf_throw_axe", "Axe throwing animation with spinning rotation" },
            { "itcf_throw_javelin", "Javelin throwing animation" },
            { "itcf_shoot_pistol", "Pistol shooting animation" },
            { "itcf_shoot_musket", "Musket shooting animation" },
        };
        
        // Encoded carry positions tooltips
        private static readonly Dictionary<string, string> CarryPositions = new Dictionary<string, string>
        {
            { "itcf_carry_sword_left_hip", "Carries sword at left hip. Attachment: abdomen, offset: (0.24, 0.1, 0.1)" },
            { "itcf_carry_axe_left_hip", "Carries axe at left hip. Attachment: abdomen, offset: (0.21, 0.12, 0.14)" },
            { "itcf_carry_dagger_front_left", "Carries dagger at front left. Attachment: abdomen, offset: (0.04, 0.25, 0.15)" },
            { "itcf_carry_dagger_front_right", "Carries dagger at front right. Attachment: abdomen, offset: (-0.04, 0.25, 0.15)" },
            { "itcf_carry_quiver_front_right", "Carries quiver at front right. Attachment: abdomen, offset: (-0.21, 0.15, 0.0)" },
            { "itcf_carry_quiver_back_right", "Carries quiver at back right. Attachment: abdomen, offset: (-0.05, 0.03, -0.19)" },
            { "itcf_carry_quiver_right_vertical", "Carries quiver vertically on right side. Attachment: abdomen, offset: (-0.23, 0.05, 0.0)" },
            { "itcf_carry_quiver_back", "Carries quiver on back. Attachment: thorax, offset: (-0.05, 0.02, -0.17)" },
            { "itcf_carry_revolver_right", "Carries revolver on right thigh. Attachment: thigh_r, offset: (-0.1, 0.0, 0.05)" },
            { "itcf_carry_pistol_front_left", "Carries pistol at front left. Attachment: abdomen, offset: (-0.07, 0.25, 0.15)" },
            { "itcf_carry_bowcase_left", "Carries bow case on left side. Attachment: abdomen, offset: (0.25, 0.03, -0.1)" },
            { "itcf_carry_mace_left_hip", "Carries mace at left hip. Attachment: abdomen, offset: (0.21, 0.12, 0.14)" },
            { "itcf_carry_axe_back", "Carries axe on back. Attachment: thorax, offset: (-0.19, 0.29, -0.17)" },
            { "itcf_carry_sword_back", "Carries sword on back. Attachment: thorax, offset: (-0.19, 0.29, -0.17)" },
            { "itcf_carry_kite_shield", "Carries kite shield on back. Attachment: thorax, offset: (-0.19, 0.04, -0.18)" },
            { "itcf_carry_round_shield", "Carries round shield on back. Attachment: thorax, offset: (-0.22, -0.06, -0.2)" },
            { "itcf_carry_buckler_left", "Carries buckler on left side. Attachment: abdomen, offset: (0.18, -0.1, -0.1)" },
            { "itcf_carry_crossbow_back", "Carries crossbow on back. Attachment: thorax, offset: (0.19, 0.34, -0.155)" },
            { "itcf_carry_bow_back", "Carries bow on back. Attachment: thorax, offset: (-0.05, 0.0, -0.19)" },
            { "itcf_carry_spear", "Carries spear on back. Attachment: thorax, offset: (-0.19, 0.29, -0.17)" },
            { "itcf_carry_board_shield", "Carries board shield on back. Attachment: thorax, offset: (-0.06, 0.18, -0.2)" },
            { "itcf_carry_katana", "Carries katana at waist. Attachment: abdomen, offset: (0.23, 0.15, 0.23)" },
            { "itcf_carry_wakizashi", "Carries wakizashi (short katana) at waist. Attachment: abdomen, offset: (0.05, 0.19, 0.24)" },
        };
        
        // Encoded reload animations tooltips
        private static readonly Dictionary<string, string> ReloadTypeTooltips = new Dictionary<string, string>
        {
            { "itcf_reload_pistol", "Uses pistol reload animation" },
            { "itcf_reload_musket", "Uses musket reload animation" },
        };
        
        /// <summary>
        /// Gets tooltip for a capability flag (checks all categories)
        /// </summary>
        public static string GetTooltip(string capabilityName)
        {
            if (BitwiseFlagTooltips.ContainsKey(capabilityName))
                return BitwiseFlagTooltips[capabilityName];
            
            if (ThrowTypeTooltips.ContainsKey(capabilityName))
                return ThrowTypeTooltips[capabilityName];
            
            if (CarryPositions.ContainsKey(capabilityName))
                return CarryPositions[capabilityName];
            
            if (ReloadTypeTooltips.ContainsKey(capabilityName))
                return ReloadTypeTooltips[capabilityName];
            
            return "Unknown capability flag";
        }
        
        /// <summary>
        /// Gets all tooltips for decoded capabilities
        /// </summary>
        public static Dictionary<string, string> GetTooltipsForCapabilities(List<string> capabilities)
        {
            var result = new Dictionary<string, string>();
            foreach (var capability in capabilities)
            {
                result[capability] = GetTooltip(capability);
            }
            return result;
        }
        
        /// <summary>
        /// Gets category name for a capability flag
        /// </summary>
        public static string GetCategory(string capabilityName)
        {
            if (BitwiseFlagTooltips.ContainsKey(capabilityName))
            {
                if (capabilityName.Contains("parry"))
                    return "Defense";
                if (capabilityName.Contains("horseback"))
                    return "Mounted Combat";
                if (capabilityName.StartsWith("itcf_shoot_") || capabilityName.Contains("thrust") || 
                    capabilityName.Contains("swing") || capabilityName.Contains("slash"))
                    return "Attack";
                if (capabilityName.Contains("holster"))
                    return "Display";
                return "Special";
            }
            
            if (ThrowTypeTooltips.ContainsKey(capabilityName))
                return "Ranged/Throwing";
            
            if (CarryPositions.ContainsKey(capabilityName))
                return "Carry Position";
            
            if (ReloadTypeTooltips.ContainsKey(capabilityName))
                return "Reload";
            
            return "Unknown";
        }
    }
}