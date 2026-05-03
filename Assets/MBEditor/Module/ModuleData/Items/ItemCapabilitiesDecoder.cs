using System;
using System.Collections.Generic;
using System.Numerics;

namespace MountAndBladeTools
{
    /// <summary>
    /// Mount & Blade item capabilities decoder.
    /// Handles both bitwise flags AND encoded sequential values within masked regions.
    /// </summary>
    public static class ItemCapabilitiesDecoder
    {
        // Masks for encoded value regions
        private const ulong SHOOT_MASK = 0x00000000000FF000UL;
        private const ulong CARRY_MASK = 0x00000007F0000000UL;
        private const ulong RELOAD_MASK = 0x000000F000000000UL;
        
        // Pure bitwise flags (can be tested with &)
        private static readonly Dictionary<string, BigInteger> BitwiseFlags = new Dictionary<string, BigInteger>
        {
            // Attack animations (pure bitwise)
            { "itcf_thrust_onehanded", 0x0000000000000001UL },
            { "itcf_overswing_onehanded", 0x0000000000000002UL },
            { "itcf_slashright_onehanded", 0x0000000000000004UL },
            { "itcf_slashleft_onehanded", 0x0000000000000008UL },
            
            { "itcf_thrust_twohanded", 0x0000000000000010UL },
            { "itcf_overswing_twohanded", 0x0000000000000020UL },
            { "itcf_slashright_twohanded", 0x0000000000000040UL },
            { "itcf_slashleft_twohanded", 0x0000000000000080UL },
            
            { "itcf_thrust_polearm", 0x0000000000000100UL },
            { "itcf_overswing_polearm", 0x0000000000000200UL },
            { "itcf_slashright_polearm", 0x0000000000000400UL },
            { "itcf_slashleft_polearm", 0x0000000000000800UL },
            
            // Basic shooting flags (bitwise)
            { "itcf_shoot_bow", 0x0000000000001000UL },
            { "itcf_shoot_javelin", 0x0000000000002000UL },
            { "itcf_shoot_crossbow", 0x0000000000004000UL },
            
            // Horseback attacks (bitwise)
            { "itcf_horseback_thrust_onehanded", 0x0000000000100000UL },
            { "itcf_horseback_overswing_right_onehanded", 0x0000000000200000UL },
            { "itcf_horseback_overswing_left_onehanded", 0x0000000000400000UL },
            { "itcf_horseback_slashright_onehanded", 0x0000000000800000UL },
            { "itcf_horseback_slashleft_onehanded", 0x0000000001000000UL },
            { "itcf_thrust_onehanded_lance", 0x0000000004000000UL },
            { "itcf_thrust_onehanded_lance_horseback", 0x0000000008000000UL },
            
            { "itcf_show_holster_when_drawn", 0x0000000800000000UL },
            
            // Parry animations (bitwise)
            { "itcf_parry_forward_onehanded", 0x0000010000000000UL },
            { "itcf_parry_up_onehanded", 0x0000020000000000UL },
            { "itcf_parry_right_onehanded", 0x0000040000000000UL },
            { "itcf_parry_left_onehanded", 0x0000080000000000UL },
            
            { "itcf_parry_forward_twohanded", 0x0000100000000000UL },
            { "itcf_parry_up_twohanded", 0x0000200000000000UL },
            { "itcf_parry_right_twohanded", 0x0000400000000000UL },
            { "itcf_parry_left_twohanded", 0x0000800000000000UL },
            
            { "itcf_parry_forward_polearm", 0x0001000000000000UL },
            { "itcf_parry_up_polearm", 0x0002000000000000UL },
            { "itcf_parry_right_polearm", 0x0004000000000000UL },
            { "itcf_parry_left_polearm", 0x0008000000000000UL },
            
            { "itcf_horseback_slash_polearm", 0x0010000000000000UL },
            { "itcf_overswing_spear", 0x0020000000000000UL },
            { "itcf_overswing_musket", 0x0040000000000000UL },
            { "itcf_thrust_musket", 0x0080000000000000UL },
            { "itcf_force_64_bits", 0x8000000000000000UL },
        };
        
        // Encoded throw types (sequential values, use == comparison)
        private static readonly Dictionary<int, string> ThrowTypes = new Dictionary<int, string>
        {
            { 0x01, "itcf_throw_stone" },
            { 0x02, "itcf_throw_knife" },
            { 0x03, "itcf_throw_axe" },      // NOTE: 0x03, not 0x01|0x02!
            { 0x04, "itcf_throw_javelin" },
            { 0x07, "itcf_shoot_pistol" },
            { 0x08, "itcf_shoot_musket" },
        };
        
        // Encoded carry positions (sequential values)
        private static readonly Dictionary<int, string> CarryPositions = new Dictionary<int, string>
        {
            { 0x01, "itcf_carry_sword_left_hip" },
            { 0x02, "itcf_carry_axe_left_hip" },
            { 0x03, "itcf_carry_dagger_front_left" },
            { 0x04, "itcf_carry_dagger_front_right" },
            { 0x05, "itcf_carry_quiver_front_right" },
            { 0x06, "itcf_carry_quiver_back_right" },
            { 0x07, "itcf_carry_quiver_right_vertical" },
            { 0x08, "itcf_carry_quiver_back" },
            { 0x09, "itcf_carry_revolver_right" },
            { 0x0A, "itcf_carry_pistol_front_left" },
            { 0x0B, "itcf_carry_bowcase_left" },
            { 0x0C, "itcf_carry_mace_left_hip" },
            { 0x10, "itcf_carry_axe_back" },
            { 0x11, "itcf_carry_sword_back" },
            { 0x12, "itcf_carry_kite_shield" },
            { 0x13, "itcf_carry_round_shield" },
            { 0x14, "itcf_carry_buckler_left" },
            { 0x15, "itcf_carry_crossbow_back" },
            { 0x16, "itcf_carry_bow_back" },
            { 0x17, "itcf_carry_spear" },
            { 0x18, "itcf_carry_board_shield" },
            { 0x21, "itcf_carry_katana" },
            { 0x22, "itcf_carry_wakizashi" },
        };
        
        // Encoded reload animations (sequential values)
        private static readonly Dictionary<int, string> ReloadTypes = new Dictionary<int, string>
        {
            { 0x07, "itcf_reload_pistol" },
            { 0x08, "itcf_reload_musket" },
        };

        /// <summary>
        /// Decodes item capabilities correctly, handling both bitwise and encoded values
        /// </summary>
        public static List<string> DecodeCapabilities(BigInteger value)
        {
            var result = new List<string>();
            
            // 1. Check pure bitwise flags
            foreach (var flag in BitwiseFlags)
            {
                if ((value & flag.Value) != 0)
                {
                    result.Add(flag.Key);
                }
            }
            
            // 2. Extract and decode throw/shoot type (encoded value)
            ulong shootEncoded = (ulong)((value & (BigInteger)SHOOT_MASK) >> 16);
            if (shootEncoded > 0 && ThrowTypes.ContainsKey((int)shootEncoded))
            {
                result.Add(ThrowTypes[(int)shootEncoded]);
            }
            
            // 3. Extract and decode carry position (encoded value)
            ulong carryEncoded = (ulong)((value & (BigInteger)CARRY_MASK) >> 28);
            if (carryEncoded > 0 && CarryPositions.ContainsKey((int)carryEncoded))
            {
                result.Add(CarryPositions[(int)carryEncoded]);
            }
            
            // 4. Extract and decode reload animation (encoded value)
            ulong reloadEncoded = (ulong)((value & (BigInteger)RELOAD_MASK) >> 36);
            if (reloadEncoded > 0 && ReloadTypes.ContainsKey((int)reloadEncoded))
            {
                result.Add(ReloadTypes[(int)reloadEncoded]);
            }
            
            return result;
        }
        
        /// <summary>
        /// Checks if a specific capability is present (handles both types)
        /// </summary>
        public static bool HasCapability(BigInteger value, string capabilityName)
        {
            // Check bitwise flags
            if (BitwiseFlags.ContainsKey(capabilityName))
            {
                return (value & BitwiseFlags[capabilityName]) != 0;
            }
            
            // Check encoded values
            var decoded = DecodeCapabilities(value);
            return decoded.Contains(capabilityName);
        }
        
        public static Dictionary<string, BigInteger> GetBitwiseFlags() => 
            new Dictionary<string, BigInteger>(BitwiseFlags);
        
        public static Dictionary<int, string> GetThrowTypes() => 
            new Dictionary<int, string>(ThrowTypes);
        
        public static Dictionary<int, string> GetCarryPositions() =>
            new Dictionary<int, string>(CarryPositions);
        
        public static Dictionary<int, string> GetReloadTypes() =>
            new Dictionary<int, string>(ReloadTypes);
    }
}