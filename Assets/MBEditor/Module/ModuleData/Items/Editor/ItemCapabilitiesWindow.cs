using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using MountAndBlade.Data;
using MountAndBladeTools;
using UnityEngine;
using UnityEditor;
using Vector2 = UnityEngine.Vector2;

namespace MountAndBlade.Editor
{
    public class ItemCapabilitiesWindow : EditorWindow
    {
        private MBItemData targetItem;
        private Vector2 scrollPosition;

        // UI State - Foldouts
        private bool showMeleeAttacks = true;
        private bool showRangedWeapons = true;
        private bool showHorsebackAttacks = true;
        private bool showParryAnimations = true;
        private bool showCarryPositions = true;
        private bool showReloadAnimations = true;
        private bool showMiscellaneous = true;

        // Decoded values
        private HashSet<string> activeBitwiseFlags = new HashSet<string>();
        private string selectedThrowType = "";
        private string selectedCarryPosition = "";
        private string selectedReloadType = "";

        // For real-time sync
        private string lastParsedCapabilitiesValue = "";

        private bool debugMode = false;

        // Tooltip style
        private GUIStyle tooltipStyle;

        public static void ShowWindow(MBItemData item)
        {
            var window = GetWindow<ItemCapabilitiesWindow>("Capabilities Editor");
            window.targetItem = item;
            window.minSize = new Vector2(550, 700);
            window.ParseCurrentCapabilities();
        }

        private void OnEnable()
        {
            if (targetItem != null)
            {
                ParseCurrentCapabilities();
            }

            InitializeStyles();
        }

        private void InitializeStyles()
        {
            tooltipStyle = new GUIStyle(EditorStyles.helpBox)
            {
                fontSize = 11,
                wordWrap = true,
                padding = new RectOffset(6, 6, 4, 4),
                normal = { textColor = new Color(0.7f, 0.7f, 0.7f) }
            };
        }

        private void OnGUI()
        {
            if (tooltipStyle == null)
                InitializeStyles();

            EditorGUILayout.Space(10);

            // HEADER
            EditorGUILayout.LabelField("Mount & Blade Capabilities Editor",
                new GUIStyle(EditorStyles.boldLabel) { fontSize = 16, alignment = TextAnchor.MiddleCenter });

            EditorGUILayout.Space(10);

            // TARGET ITEM SELECTION
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Target Item", EditorStyles.boldLabel);

            var newTarget = (MBItemData)EditorGUILayout.ObjectField(
                "Item Data",
                targetItem,
                typeof(MBItemData),
                false);

            if (newTarget != targetItem)
            {
                targetItem = newTarget;
                if (targetItem != null)
                {
                    ParseCurrentCapabilities();
                }
            }

            EditorGUILayout.EndVertical();

            if (targetItem == null)
            {
                EditorGUILayout.HelpBox("Select an MBItemData asset to edit its capabilities.", MessageType.Info);
                return;
            }

            EditorGUILayout.Space(5);

            // Check if capabilities value changed externally
            if (targetItem.Capabilities != lastParsedCapabilitiesValue)
            {
                ParseCurrentCapabilities();
            }


            // RAW VALUE DISPLAY
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            
            // Debug mode toggle
            debugMode = EditorGUILayout.ToggleLeft(
                new GUIContent("Debug Mode",
                    "When enabled, all flags and options will display their internal names instead of formatted names."),
                debugMode);

            if (debugMode)
            {
                EditorGUILayout.LabelField("Current Raw Value:", EditorStyles.boldLabel);
                EditorGUILayout.SelectableLabel(
                    string.IsNullOrEmpty(targetItem.Capabilities) ? "(empty)" : targetItem.Capabilities,
                    GUILayout.Height(20));
            }

            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(5);
            
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);


            // MELEE ATTACK ANIMATIONS (bitwise flags)
            showMeleeAttacks = EditorGUILayout.BeginFoldoutHeaderGroup(showMeleeAttacks, "Melee Attack Animations");
            if (showMeleeAttacks)
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);

                DrawMeleeAttackSection("One-Handed", new[]
                {
                    "itcf_thrust_onehanded",
                    "itcf_overswing_onehanded",
                    "itcf_slashright_onehanded",
                    "itcf_slashleft_onehanded"
                });

                EditorGUILayout.Space(5);

                DrawMeleeAttackSection("Two-Handed", new[]
                {
                    "itcf_thrust_twohanded",
                    "itcf_overswing_twohanded",
                    "itcf_slashright_twohanded",
                    "itcf_slashleft_twohanded"
                });

                EditorGUILayout.Space(5);

                DrawMeleeAttackSection("Polearm", new[]
                {
                    "itcf_thrust_polearm",
                    "itcf_overswing_polearm",
                    "itcf_slashright_polearm",
                    "itcf_slashleft_polearm"
                });

                EditorGUILayout.EndVertical();
            }

            EditorGUILayout.EndFoldoutHeaderGroup();

            EditorGUILayout.Space(5);

            // RANGED WEAPONS (bitwise flags + encoded throw type)
            showRangedWeapons = EditorGUILayout.BeginFoldoutHeaderGroup(showRangedWeapons, "Ranged Weapons");
            if (showRangedWeapons)
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);

                // Basic shooting flags (bitwise)
                EditorGUILayout.LabelField("Basic Shooting (Bitwise)", EditorStyles.boldLabel);
                DrawBitwiseFlagCheckboxes(new[]
                {
                    "itcf_shoot_bow",
                    "itcf_shoot_javelin",
                    "itcf_shoot_crossbow"
                });

                EditorGUILayout.Space(10);

                // Throw/Shoot type (ENCODED - mutually exclusive)
                EditorGUILayout.LabelField("Throw/Shoot Type (Mutually Exclusive)", EditorStyles.boldLabel);
                EditorGUILayout.HelpBox(
                    "These are ENCODED values, not bitwise flags. Only ONE can be active.",
                    MessageType.Info);

                DrawEncodedSelection(
                    GetThrowTypeOptions(),
                    ref selectedThrowType,
                    "None (default)");

                EditorGUILayout.EndVertical();
            }

            EditorGUILayout.EndFoldoutHeaderGroup();

            EditorGUILayout.Space(5);

            // HORSEBACK ATTACKS (bitwise flags)
            showHorsebackAttacks = EditorGUILayout.BeginFoldoutHeaderGroup(showHorsebackAttacks, "Horseback Attacks");
            if (showHorsebackAttacks)
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);

                DrawBitwiseFlagCheckboxes(new[]
                {
                    "itcf_horseback_thrust_onehanded",
                    "itcf_horseback_overswing_right_onehanded",
                    "itcf_horseback_overswing_left_onehanded",
                    "itcf_horseback_slashright_onehanded",
                    "itcf_horseback_slashleft_onehanded",
                    "itcf_thrust_onehanded_lance",
                    "itcf_thrust_onehanded_lance_horseback",
                    "itcf_horseback_slash_polearm"
                });

                EditorGUILayout.EndVertical();
            }

            EditorGUILayout.EndFoldoutHeaderGroup();

            EditorGUILayout.Space(5);

            // PARRY ANIMATIONS (bitwise flags)
            showParryAnimations = EditorGUILayout.BeginFoldoutHeaderGroup(showParryAnimations, "Parry Animations");
            if (showParryAnimations)
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);

                DrawMeleeAttackSection("One-Handed Parry", new[]
                {
                    "itcf_parry_forward_onehanded",
                    "itcf_parry_up_onehanded",
                    "itcf_parry_right_onehanded",
                    "itcf_parry_left_onehanded"
                });

                EditorGUILayout.Space(5);

                DrawMeleeAttackSection("Two-Handed Parry", new[]
                {
                    "itcf_parry_forward_twohanded",
                    "itcf_parry_up_twohanded",
                    "itcf_parry_right_twohanded",
                    "itcf_parry_left_twohanded"
                });

                EditorGUILayout.Space(5);

                DrawMeleeAttackSection("Polearm Parry", new[]
                {
                    "itcf_parry_forward_polearm",
                    "itcf_parry_up_polearm",
                    "itcf_parry_right_polearm",
                    "itcf_parry_left_polearm"
                });

                EditorGUILayout.EndVertical();
            }

            EditorGUILayout.EndFoldoutHeaderGroup();

            EditorGUILayout.Space(5);

            // CARRY POSITIONS (ENCODED - mutually exclusive)
            showCarryPositions = EditorGUILayout.BeginFoldoutHeaderGroup(showCarryPositions, "Carry Position");
            if (showCarryPositions)
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.HelpBox(
                    "ENCODED value - only ONE carry position can be active.",
                    MessageType.Info);

                DrawEncodedSelection(
                    GetCarryPositionOptions(),
                    ref selectedCarryPosition,
                    "None (default)");

                EditorGUILayout.EndVertical();
            }

            EditorGUILayout.EndFoldoutHeaderGroup();

            EditorGUILayout.Space(5);

            // RELOAD ANIMATIONS (ENCODED - mutually exclusive)
            showReloadAnimations = EditorGUILayout.BeginFoldoutHeaderGroup(showReloadAnimations, "Reload Animation");
            if (showReloadAnimations)
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.HelpBox(
                    "ENCODED value - only ONE reload animation can be active.",
                    MessageType.Info);

                DrawEncodedSelection(
                    GetReloadTypeOptions(),
                    ref selectedReloadType,
                    "None (default)");

                EditorGUILayout.EndVertical();
            }

            EditorGUILayout.EndFoldoutHeaderGroup();

            EditorGUILayout.Space(5);

// MISCELLANEOUS (bitwise flags)
            showMiscellaneous = EditorGUILayout.BeginFoldoutHeaderGroup(showMiscellaneous, "Miscellaneous");
            if (showMiscellaneous)
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);

                DrawBitwiseFlagCheckboxes(new[]
                {
                    "itcf_show_holster_when_drawn",
                    "itcf_overswing_spear",
                    "itcf_overswing_musket",
                    "itcf_thrust_musket"
                });

                EditorGUILayout.Space(10);
    
                // SEPARATE SECTION: Auto-calculated flags (read-only)
                EditorGUILayout.LabelField("Auto-Calculated Flags (Read-Only)", EditorStyles.boldLabel);
    
                bool has64BitFlag = activeBitwiseFlags.Contains("itcf_force_64_bits");
    
                // Show info box explaining why it's auto-calculated
                if (has64BitFlag)
                {
                    EditorGUILayout.HelpBox(
                        "This flag is automatically enabled because the encoded value exceeds 32-bit range. " +
                        "It ensures proper Module System compilation.",
                        MessageType.Info);
                }
    
                // Draw disabled toggle (read-only display)
                GUI.enabled = false;  // Disable interaction
    
                var label = new GUIContent(
                    FormatFlagName("itcf_force_64_bits"),
                    "Automatically enabled when capabilities require 64-bit encoding. Cannot be manually toggled."
                );
    
                EditorGUILayout.ToggleLeft(label, has64BitFlag);
    
                GUI.enabled = true;  // Re-enable interaction for other controls

                EditorGUILayout.EndVertical();
            }

            EditorGUILayout.EndFoldoutHeaderGroup();
            EditorGUILayout.EndScrollView();
        }

        // UI HELPER METHODS

        private void DrawMeleeAttackSection(string label, string[] flags)
        {
            EditorGUILayout.LabelField(label, EditorStyles.boldLabel);
            EditorGUI.indentLevel++;
            DrawBitwiseFlagCheckboxes(flags);
            EditorGUI.indentLevel--;
        }

        private void DrawBitwiseFlagCheckboxes(string[] flags)
        {
            foreach (var flag in flags)
            {
                bool isActive = activeBitwiseFlags.Contains(flag);

                var option =  ItemCapabilitiesDecoder.GetBitwiseFlags().FirstOrDefault(x => x.Key == flag);
                var hex = debugMode ? $" (0x{option.Value:X})" : "";
                
                // Create GUIContent with tooltip
                var label = new GUIContent(
                    $"{FormatFlagName(option.Key)}{hex}",
                    ItemCapabilitiesTooltips.GetTooltip(flag)
                );

                bool newActive = EditorGUILayout.ToggleLeft(label, isActive);

                if (newActive != isActive)
                {
                    if (newActive)
                        activeBitwiseFlags.Add(flag);
                    else
                        activeBitwiseFlags.Remove(flag);

                    EncodeCapabilities();
                }
            }
        }

        private void DrawEncodedSelection(Dictionary<string, int> options, ref string selectedValue, string noneLabel)
        {
            // "None" option
            bool isNoneSelected = string.IsNullOrEmpty(selectedValue);
            bool newNoneSelected = EditorGUILayout.ToggleLeft(noneLabel, isNoneSelected);

            if (newNoneSelected && !isNoneSelected)
            {
                selectedValue = "";
                EncodeCapabilities();
            }

            // All options
            string newSelection = selectedValue;
            foreach (var option in options.OrderBy(x => x.Value))
            {
                bool isSelected = selectedValue == option.Key;

                // Create GUIContent with tooltip
                var hex = debugMode ? $" (0x{option.Value:X})" : "";
                var label = new GUIContent(
                    $"{FormatFlagName(option.Key)}{hex}",
                    ItemCapabilitiesTooltips.GetTooltip(option.Key)
                );

                bool newSelected = EditorGUILayout.ToggleLeft(label, isSelected);

                if (newSelected && !isSelected)
                {
                    newSelection = option.Key;
                }
                else if (!newSelected && isSelected)
                {
                    newSelection = "";
                }
            }

            if (newSelection != selectedValue)
            {
                selectedValue = newSelection;
                EncodeCapabilities();
            }
        }
        private string FormatFlagName(string flag)
        {
            if (debugMode)
                return flag;

            // Remove "itcf_" prefix and format nicely
            string name = flag.Replace("itcf_", "");
            name = System.Text.RegularExpressions.Regex.Replace(name, "_", " ");
            return System.Globalization.CultureInfo.CurrentCulture.TextInfo.ToTitleCase(name);
        }

        // PARSING (Decode from raw value)

        private void ParseCurrentCapabilities()
        {
            if (targetItem == null || string.IsNullOrWhiteSpace(targetItem.Capabilities))
            {
                activeBitwiseFlags.Clear();
                selectedThrowType = "";
                selectedCarryPosition = "";
                selectedReloadType = "";
                lastParsedCapabilitiesValue = "";
                return;
            }

            try
            {
                BigInteger value = ParseBigInteger(targetItem.Capabilities);
                lastParsedCapabilitiesValue = targetItem.Capabilities;

                // Decode all capabilities
                var decoded = ItemCapabilitiesDecoder.DecodeCapabilities(value);

                // Separate into categories
                activeBitwiseFlags.Clear();
                selectedThrowType = "";
                selectedCarryPosition = "";
                selectedReloadType = "";

                var throwTypes = GetThrowTypeOptions();
                var carryPositions = GetCarryPositionOptions();
                var reloadTypes = GetReloadTypeOptions();

                foreach (var capability in decoded)
                {
                    if (throwTypes.ContainsKey(capability))
                        selectedThrowType = capability;
                    else if (carryPositions.ContainsKey(capability))
                        selectedCarryPosition = capability;
                    else if (reloadTypes.ContainsKey(capability))
                        selectedReloadType = capability;
                    else
                        activeBitwiseFlags.Add(capability);
                }

                Repaint();
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to parse capabilities: {ex.Message}");
            }
        }

        // ENCODING (Build raw value from UI state)

        private void EncodeCapabilities()
        {
            if (targetItem == null) return;

            try
            {
                BigInteger encodedValue = 0;

                // 1. Encode bitwise flags (simple OR) - EXCLUDING force_64_bits
                var bitwiseFlags = ItemCapabilitiesDecoder.GetBitwiseFlags();
                foreach (var activeFlagName in activeBitwiseFlags)
                {
                    // Skip force_64_bits - it will be calculated automatically
                    if (activeFlagName == "itcf_force_64_bits")
                        continue;

                    if (bitwiseFlags.TryGetValue(activeFlagName, out BigInteger flagValue))
                    {
                        encodedValue |= flagValue;
                    }
                }

                // 2. Encode throw/shoot type (bits 16-23)
                if (!string.IsNullOrEmpty(selectedThrowType))
                {
                    var throwTypes = GetThrowTypeOptions();
                    if (throwTypes.TryGetValue(selectedThrowType, out int throwValue))
                    {
                        encodedValue |= (BigInteger)((ulong)throwValue << 16);
                    }
                }

                // 3. Encode carry position (bits 28-35)
                if (!string.IsNullOrEmpty(selectedCarryPosition))
                {
                    var carryPositions = GetCarryPositionOptions();
                    if (carryPositions.TryGetValue(selectedCarryPosition, out int carryValue))
                    {
                        encodedValue |= (BigInteger)((ulong)carryValue << 28);
                    }
                }

                // 4. Encode reload animation (bits 36-39)
                if (!string.IsNullOrEmpty(selectedReloadType))
                {
                    var reloadTypes = GetReloadTypeOptions();
                    if (reloadTypes.TryGetValue(selectedReloadType, out int reloadValue))
                    {
                        encodedValue |= (BigInteger)((ulong)reloadValue << 36);
                    }
                }

                // 5. NOW check if we need 64-bit flag (AFTER calculating all other values)
                const ulong MAX_32BIT = 0xFFFFFFFFUL;
                bool needs64Bit = encodedValue > MAX_32BIT;

                if (needs64Bit)
                {
                    // Automatically set the flag for proper compilation
                    encodedValue |= 0x8000000000000000UL;

                    // Update UI state to show it's active (but will be displayed as disabled)
                    if (!activeBitwiseFlags.Contains("itcf_force_64_bits"))
                        activeBitwiseFlags.Add("itcf_force_64_bits");
                }
                else
                {
                    // Remove the flag if not needed
                    activeBitwiseFlags.Remove("itcf_force_64_bits");
                }

                // 6. Write back
                string newCapabilitiesValue = FormatBigInteger(encodedValue);

                if (targetItem.Capabilities != newCapabilitiesValue)
                {
                    Undo.RecordObject(targetItem, "Modify Item Capabilities");
                    targetItem.Capabilities = newCapabilitiesValue;
                    lastParsedCapabilitiesValue = newCapabilitiesValue;
                    EditorUtility.SetDirty(targetItem);
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to encode capabilities: {ex.Message}");
            }
        }
        // DATA DICTIONARIES

        private Dictionary<string, int> GetThrowTypeOptions()
        {
            // Invert the dictionary from decoder (it's int -> string, we need string -> int)
            var throwTypes = ItemCapabilitiesDecoder.GetThrowTypes();
            var result = new Dictionary<string, int>();

            foreach (var kvp in throwTypes)
            {
                result[kvp.Value] = kvp.Key;
            }

            return result;
        }

        private Dictionary<string, int> GetCarryPositionOptions()
        {
            // Invert the dictionary from decoder
            var carryPositions = ItemCapabilitiesDecoder.GetCarryPositions();
            var result = new Dictionary<string, int>();

            foreach (var kvp in carryPositions)
            {
                result[kvp.Value] = kvp.Key;
            }

            return result;
        }

        private Dictionary<string, int> GetReloadTypeOptions()
        {
            // Invert the dictionary from decoder
            var reloadTypes = ItemCapabilitiesDecoder.GetReloadTypes();
            var result = new Dictionary<string, int>();

            foreach (var kvp in reloadTypes)
            {
                result[kvp.Value] = kvp.Key;
            }

            return result;
        }

        // UTILITY METHODS

        private string FormatBigInteger(BigInteger value)
        {
            if (value == 0)
                return "0";

            string hexString = value.ToString("X");
            return "0x" + hexString;
        }

        private BigInteger ParseBigInteger(string input)
        {
            input = input.Trim();

            if (input.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            {
                return BigInteger.Parse(
                    input.Substring(2),
                    System.Globalization.NumberStyles.HexNumber);
            }

            return BigInteger.Parse(input);
        }
    }
}
