using System;
using System.Collections.Generic;
using System.Numerics;
using MountAndBlade.Data;
using UnityEngine;
using UnityEditor;

namespace MountAndBlade.Editor
{
    public class TroopFlagPropertiesWindow : EditorWindow
    {
        private MBTroopData targetTroop;
        private UnityEngine.Vector2 scrollPosition;

        // UI State - Foldouts
        private bool showTroopType = true;
        private bool showPropertyFlags = true;
        private bool showGuaranteeFlags = true;
        private bool showSpecialFlags = true;

        // Decoded values
        private string selectedTroopType = "";
        private HashSet<string> activePropertyFlags = new HashSet<string>();
        private HashSet<string> activeGuaranteeFlags = new HashSet<string>();
        private HashSet<string> activeSpecialFlags = new HashSet<string>();

        // For real-time sync
        private string lastParsedFlagsValue = "";

        // Styles
        private GUIStyle headerStyle;
        private GUIStyle contextHintStyle;
        private bool stylesInitialized = false;
        private bool debugMode = false;

        public static void ShowWindow(MBTroopData troop)
        {
            var window = GetWindow<TroopFlagPropertiesWindow>("Troop Flag Properties Editor");
            window.targetTroop = troop;
            window.minSize = new UnityEngine.Vector2(550, 600);
            window.ParseCurrentFlags();
        }

        private void InitStyles()
        {
            if (stylesInitialized) return;

            headerStyle = new GUIStyle(EditorStyles.helpBox)
            {
                padding = new RectOffset(8, 8, 6, 6),
                margin = new RectOffset(0, 0, 2, 2)
            };

            contextHintStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                fontStyle = FontStyle.Italic,
                normal = { textColor = new Color(0.6f, 0.6f, 0.6f) }
            };

            stylesInitialized = true;
        }

        private void OnEnable()
        {
            if (targetTroop != null)
            {
                ParseCurrentFlags();
            }
        }

        private void OnGUI()
        {
            InitStyles();

            EditorGUILayout.Space(10);

            // HEADER
            EditorGUILayout.LabelField("Mount & Blade Troop Flag Properties Editor",
                new GUIStyle(EditorStyles.boldLabel) { fontSize = 16, alignment = TextAnchor.MiddleCenter });

            EditorGUILayout.Space(10);

            // TARGET TROOP SELECTION
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Target Troop", EditorStyles.boldLabel);

            var newTarget = (MBTroopData)EditorGUILayout.ObjectField(
                "Troop Data",
                targetTroop,
                typeof(MBTroopData),
                false);

            if (newTarget != targetTroop)
            {
                targetTroop = newTarget;
                if (targetTroop != null)
                {
                    ParseCurrentFlags();
                }
            }

            EditorGUILayout.EndVertical();

            if (targetTroop == null)
            {
                EditorGUILayout.HelpBox("Select an MBTroopData asset to edit its flags.", MessageType.Info);
                return;
            }

            EditorGUILayout.Space(5);

            // Check if flags value changed externally
            if (targetTroop.TroopFlags != lastParsedFlagsValue)
            {
                ParseCurrentFlags();
            }

            // RAW VALUE DISPLAY
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            // Debug mode toggle
            debugMode = EditorGUILayout.ToggleLeft(
                new GUIContent("Debug Mode",
                    "When enabled, all flags will display their internal names (tf_*) instead of formatted names."),
                debugMode);

            if (debugMode)
            {
                EditorGUILayout.LabelField("Current Raw Value:", EditorStyles.boldLabel);
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.SelectableLabel(
                    string.IsNullOrEmpty(targetTroop.TroopFlags) ? "(empty)" : targetTroop.TroopFlags,
                    GUILayout.Height(20));
                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(5);

            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

            // TROOP TYPE (bits 0-3) - Mutually Exclusive
            DrawTroopTypeSection();

            EditorGUILayout.Space(5);

            // PROPERTY FLAGS
            DrawPropertyFlagsSection();

            EditorGUILayout.Space(5);

            // GUARANTEE FLAGS
            DrawGuaranteeFlagsSection();

            EditorGUILayout.Space(5);

            // SPECIAL FLAGS
            DrawSpecialFlagsSection();

            EditorGUILayout.EndScrollView();
        }

        // TROOP TYPE SECTION
        private void DrawTroopTypeSection()
        {
            var bitsInfo = debugMode ? " (bits 0-3)" : "";
            showTroopType = EditorGUILayout.BeginFoldoutHeaderGroup(showTroopType, $"Troop Type{bitsInfo}");
            if (showTroopType)
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);

                EditorGUILayout.LabelField("Select troop gender/type (mutually exclusive):", contextHintStyle);
                EditorGUILayout.Space(3);

                var troopTypes = TroopFlagPropertyDecoder.GetTroopTypes();
                string newTroopType = selectedTroopType;

                foreach (var type in troopTypes)
                {
                    string displayName = debugMode ? type.Key : TroopFlagPropertyDecoder.FormatFriendlyName(type.Key);
                    string description = TroopFlagPropertyDecoder.GetFlagDescription(type.Key);

                    bool isSelected = selectedTroopType == type.Key;
                    bool newSelection = EditorGUILayout.ToggleLeft(
                        new GUIContent(displayName, description),
                        isSelected);

                    if (newSelection && !isSelected)
                    {
                        newTroopType = type.Key;
                    }
                    else if (!newSelection && isSelected)
                    {
                        newTroopType = "";
                    }
                }

                if (newTroopType != selectedTroopType)
                {
                    selectedTroopType = newTroopType;
                    EncodeFlags();
                }

                EditorGUILayout.EndVertical();
            }
            EditorGUILayout.EndFoldoutHeaderGroup();
        }

        // PROPERTY FLAGS SECTION
        private void DrawPropertyFlagsSection()
        {
            showPropertyFlags = EditorGUILayout.BeginFoldoutHeaderGroup(showPropertyFlags, "Property Flags");
            if (showPropertyFlags)
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);

                EditorGUILayout.LabelField("General troop behavior flags:", contextHintStyle);
                EditorGUILayout.Space(3);

                var propertyFlags = TroopFlagPropertyDecoder.GetPropertyFlags();
                bool anyChanged = false;

                // Deprecated flags to highlight
                var deprecatedFlags = new HashSet<string> { "tf_unkillable", "tf_no_capture_alive" };

                foreach (var flag in propertyFlags)
                {
                    string displayName = debugMode ? flag.Key : TroopFlagPropertyDecoder.FormatFriendlyName(flag.Key);
                    string description = TroopFlagPropertyDecoder.GetFlagDescription(flag.Key);

                    // Add warning for deprecated flags
                    if (deprecatedFlags.Contains(flag.Key))
                    {
                        displayName += " ⚠️";
                        description = "DEPRECATED: " + description;
                    }

                    bool isActive = activePropertyFlags.Contains(flag.Key);
                    
                    // Highlight deprecated flags with a different color
                    if (deprecatedFlags.Contains(flag.Key) && isActive)
                    {
                        GUI.color = new Color(1f, 0.8f, 0.6f); // Orange tint
                    }
                    
                    bool newValue = EditorGUILayout.ToggleLeft(
                        new GUIContent(displayName, description),
                        isActive);
                    
                    GUI.color = Color.white; // Reset color

                    if (newValue != isActive)
                    {
                        if (newValue)
                            activePropertyFlags.Add(flag.Key);
                        else
                            activePropertyFlags.Remove(flag.Key);
                        anyChanged = true;
                    }
                }

                // Show warning if deprecated flags are active
                if (activePropertyFlags.Contains("tf_unkillable") || activePropertyFlags.Contains("tf_no_capture_alive"))
                {
                    EditorGUILayout.Space(5);
                    EditorGUILayout.HelpBox("⚠️ You have deprecated flags enabled. These flags are not functional in Warband.", MessageType.Warning);
                }

                if (anyChanged)
                {
                    EncodeFlags();
                }

                EditorGUILayout.EndVertical();
            }
            EditorGUILayout.EndFoldoutHeaderGroup();
        }

        // GUARANTEE FLAGS SECTION
        private void DrawGuaranteeFlagsSection()
        {
            showGuaranteeFlags = EditorGUILayout.BeginFoldoutHeaderGroup(showGuaranteeFlags, "Guarantee Flags");
            if (showGuaranteeFlags)
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);

                EditorGUILayout.LabelField("Ensures troop spawns with specific equipment types:", contextHintStyle);
                EditorGUILayout.Space(3);

                var guaranteeFlags = TroopFlagPropertyDecoder.GetGuaranteeFlags();
                bool anyChanged = false;

                // Group guarantee flags into categories for better readability
                var armorFlags = new[] { "tf_guarantee_boots", "tf_guarantee_armor", "tf_guarantee_helmet", "tf_guarantee_gloves" };
                var equipmentFlags = new[] { "tf_guarantee_horse", "tf_guarantee_shield", "tf_guarantee_ranged", "tf_guarantee_polearm" };

                // Draw armor section
                EditorGUILayout.LabelField("Armor Guarantees:", EditorStyles.miniLabel);
                foreach (var flagKey in armorFlags)
                {
                    if (guaranteeFlags.TryGetValue(flagKey, out var flagValue))
                    {
                        string displayName = debugMode ? flagKey : TroopFlagPropertyDecoder.FormatFriendlyName(flagKey);
                        string description = TroopFlagPropertyDecoder.GetFlagDescription(flagKey);

                        bool isActive = activeGuaranteeFlags.Contains(flagKey);
                        bool newValue = EditorGUILayout.ToggleLeft(
                            new GUIContent("  " + displayName, description),
                            isActive);

                        if (newValue != isActive)
                        {
                            if (newValue)
                                activeGuaranteeFlags.Add(flagKey);
                            else
                                activeGuaranteeFlags.Remove(flagKey);
                            anyChanged = true;
                        }
                    }
                }

                EditorGUILayout.Space(5);

                // Draw equipment section
                EditorGUILayout.LabelField("Equipment Guarantees:", EditorStyles.miniLabel);
                foreach (var flagKey in equipmentFlags)
                {
                    if (guaranteeFlags.TryGetValue(flagKey, out var flagValue))
                    {
                        string displayName = debugMode ? flagKey : TroopFlagPropertyDecoder.FormatFriendlyName(flagKey);
                        string description = TroopFlagPropertyDecoder.GetFlagDescription(flagKey);

                        bool isActive = activeGuaranteeFlags.Contains(flagKey);
                        bool newValue = EditorGUILayout.ToggleLeft(
                            new GUIContent("  " + displayName, description),
                            isActive);

                        if (newValue != isActive)
                        {
                            if (newValue)
                                activeGuaranteeFlags.Add(flagKey);
                            else
                                activeGuaranteeFlags.Remove(flagKey);
                            anyChanged = true;
                        }
                    }
                }

                if (anyChanged)
                {
                    EncodeFlags();
                }

                EditorGUILayout.EndVertical();
            }
            EditorGUILayout.EndFoldoutHeaderGroup();
        }

        // SPECIAL FLAGS SECTION
        private void DrawSpecialFlagsSection()
        {
            showSpecialFlags = EditorGUILayout.BeginFoldoutHeaderGroup(showSpecialFlags, "Special Flags");
            if (showSpecialFlags)
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);

                EditorGUILayout.LabelField("Miscellaneous special flags:", contextHintStyle);
                EditorGUILayout.Space(3);

                var specialFlags = TroopFlagPropertyDecoder.GetSpecialFlags();
                bool anyChanged = false;

                foreach (var flag in specialFlags)
                {
                    string displayName = debugMode ? flag.Key : TroopFlagPropertyDecoder.FormatFriendlyName(flag.Key);
                    string description = TroopFlagPropertyDecoder.GetFlagDescription(flag.Key);

                    bool isActive = activeSpecialFlags.Contains(flag.Key);
                    bool newValue = EditorGUILayout.ToggleLeft(
                        new GUIContent(displayName, description),
                        isActive);

                    if (newValue != isActive)
                    {
                        if (newValue)
                            activeSpecialFlags.Add(flag.Key);
                        else
                            activeSpecialFlags.Remove(flag.Key);
                        anyChanged = true;
                    }
                }

                if (anyChanged)
                {
                    EncodeFlags();
                }

                EditorGUILayout.EndVertical();
            }
            EditorGUILayout.EndFoldoutHeaderGroup();
        }

        // PARSING (Read raw value into UI state)
        private void ParseCurrentFlags()
        {
            if (targetTroop == null) return;

            try
            {
                string value = targetTroop.TroopFlags;
                lastParsedFlagsValue = value;

                // Decode all flags
                var (troopType, propertyFlags, guaranteeFlags, specialFlags) = 
                    TroopFlagPropertyDecoder.DecodeAllFlags(value);

                selectedTroopType = troopType;
                activePropertyFlags = propertyFlags;
                activeGuaranteeFlags = guaranteeFlags;
                activeSpecialFlags = specialFlags;

                Repaint();
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to parse troop flags: {ex.Message}");
            }
        }

        // ENCODING (Build raw value from UI state)
        private void EncodeFlags()
        {
            if (targetTroop == null) return;

            try
            {
                BigInteger encodedValue = 0;

                // 1. ENCODE TROOP TYPE (bits 0-3)
                if (!string.IsNullOrEmpty(selectedTroopType))
                {
                    var troopTypes = TroopFlagPropertyDecoder.GetTroopTypes();
                    if (troopTypes.TryGetValue(selectedTroopType, out BigInteger typeValue))
                    {
                        encodedValue |= typeValue;
                    }
                }

                // 2. ENCODE PROPERTY FLAGS
                var propertyFlags = TroopFlagPropertyDecoder.GetPropertyFlags();
                foreach (var activeFlagName in activePropertyFlags)
                {
                    if (propertyFlags.TryGetValue(activeFlagName, out BigInteger flagValue))
                    {
                        encodedValue |= flagValue;
                    }
                }

                // 3. ENCODE GUARANTEE FLAGS
                var guaranteeFlags = TroopFlagPropertyDecoder.GetGuaranteeFlags();
                foreach (var activeFlagName in activeGuaranteeFlags)
                {
                    if (guaranteeFlags.TryGetValue(activeFlagName, out BigInteger flagValue))
                    {
                        encodedValue |= flagValue;
                    }
                }

                // 4. ENCODE SPECIAL FLAGS
                var specialFlags = TroopFlagPropertyDecoder.GetSpecialFlags();
                foreach (var activeFlagName in activeSpecialFlags)
                {
                    if (specialFlags.TryGetValue(activeFlagName, out BigInteger flagValue))
                    {
                        encodedValue |= flagValue;
                    }
                }

                // 5. WRITE BACK
                string newFlagsValue = FormatBigInteger(encodedValue);

                if (targetTroop.TroopFlags != newFlagsValue)
                {
                    Undo.RecordObject(targetTroop, "Modify Troop Flags");
                    targetTroop.TroopFlags = newFlagsValue;
                    lastParsedFlagsValue = newFlagsValue;
                    EditorUtility.SetDirty(targetTroop);
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to encode troop flags: {ex.Message}");
            }
        }

        // HELPER METHODS

        private string FormatBigInteger(BigInteger value)
        {
            if (value == 0)
                return "0";

            string hexString = value.ToString("X");
            return "0x" + hexString;
        }
    }
}
