using System;
using System.Numerics;
using MountAndBlade.Data;
using UnityEngine;
using UnityEditor;

namespace MountAndBlade.Editor
{
    public class TroopAttributesWindow : EditorWindow
    {
        private MBTroopData targetTroop;
        private UnityEngine.Vector2 scrollPosition;

        // UI State - Foldouts
        private bool showAttributes = true;
        private bool showValidation = true;

        // Decoded attribute values
        private int strength = TroopAttributeDecoder.DEFAULT_ATTRIBUTE_VALUE;
        private int agility = TroopAttributeDecoder.DEFAULT_ATTRIBUTE_VALUE;
        private int intelligence = TroopAttributeDecoder.DEFAULT_ATTRIBUTE_VALUE;
        private int charisma = TroopAttributeDecoder.DEFAULT_ATTRIBUTE_VALUE;
        private int level = TroopAttributeDecoder.DEFAULT_LEVEL;

        // For real-time sync
        private string lastParsedAttributesValue = "";

        // Styles
        private GUIStyle headerStyle;
        private GUIStyle warningStyle;
        private GUIStyle infoStyle;
        private bool stylesInitialized = false;
        private bool debugMode = false;
        public static void ShowWindow(MBTroopData troop)
        {
            var window = GetWindow<TroopAttributesWindow>("Troop Attributes");
            window.targetTroop = troop;
            window.minSize = new UnityEngine.Vector2(500, 600);
            window.ParseCurrentValues();
        }

        private void InitStyles()
        {
            if (stylesInitialized) return;

            headerStyle = new GUIStyle(EditorStyles.helpBox)
            {
                padding = new RectOffset(8, 8, 6, 6),
                margin = new RectOffset(0, 0, 2, 2)
            };

            warningStyle = new GUIStyle(EditorStyles.helpBox)
            {
                normal = { textColor = new Color(1f, 0.6f, 0f) }
            };

            infoStyle = new GUIStyle(EditorStyles.miniLabel)
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
                ParseCurrentValues();
            }
        }

        private void OnGUI()
        {
            InitStyles();

            EditorGUILayout.Space(10);

            // HEADER
            EditorGUILayout.LabelField("Mount & Blade Troop Attributes Editor",
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
                    ParseCurrentValues();
                }
            }

            EditorGUILayout.EndVertical();

            if (targetTroop == null)
            {
                EditorGUILayout.HelpBox("Select an MBTroopData asset to edit its attributes.", MessageType.Info);
                return;
            }

            EditorGUILayout.Space(5);

            // Check if values changed externally
            if (targetTroop.TroopAttributes != lastParsedAttributesValue)
            {
                ParseCurrentValues();
            }

            // DEBUG MODE & RAW VALUES
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            debugMode = EditorGUILayout.ToggleLeft(
                new GUIContent("Debug Mode",
                    "Show raw hex values and internal details"),
                debugMode);

            if (debugMode)
            {
                EditorGUILayout.LabelField("Raw Attributes Value:", EditorStyles.miniLabel);
                EditorGUILayout.SelectableLabel(
                    string.IsNullOrEmpty(targetTroop.TroopAttributes) ? "(empty)" : targetTroop.TroopAttributes,
                    GUILayout.Height(18));

                EditorGUILayout.Space(3);
                EditorGUILayout.LabelField("Debug Info:", EditorStyles.miniLabel);
                if (!string.IsNullOrEmpty(targetTroop.TroopAttributes))
                {
                    try
                    {
                        BigInteger value = TroopAttributeDecoder.ParseBigInteger(targetTroop.TroopAttributes);
                        bool hasBignum = TroopAttributeDecoder.HasBignumFlag(value);
                        EditorGUILayout.LabelField($"Has Bignum Flag: {hasBignum}", infoStyle);
                    }
                    catch { }
                }
            }

            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(5);

            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

            // ATTRIBUTES SECTION
            DrawAttributesSection();

            EditorGUILayout.Space(5);

            // VALIDATION SECTION
            DrawValidationSection();

            EditorGUILayout.Space(10);

            // QUICK PRESETS
            DrawQuickPresets();

            EditorGUILayout.EndScrollView();
        }

        // ATTRIBUTES SECTION
        private void DrawAttributesSection()
        {
            showAttributes = EditorGUILayout.BeginFoldoutHeaderGroup(showAttributes, "Attributes & Level");
            if (showAttributes)
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);

                // Level (should be set first as it affects validation)
                EditorGUILayout.LabelField("Level", EditorStyles.boldLabel);
                int newLevel = EditorGUILayout.IntSlider(
                    new GUIContent("Level", TroopAttributeDecoder.GetAttributeDescription("level")),
                    level,
                    TroopAttributeDecoder.MIN_LEVEL,
                    TroopAttributeDecoder.MAX_LEVEL);

                EditorGUILayout.Space(8);

                // Attributes
                EditorGUILayout.LabelField("Character Attributes", EditorStyles.boldLabel);

                int newStr = EditorGUILayout.IntSlider(
                    new GUIContent("Strength (STR)", TroopAttributeDecoder.GetAttributeDescription("str")),
                    strength,
                    TroopAttributeDecoder.MIN_ATTRIBUTE_VALUE,
                    TroopAttributeDecoder.MAX_ATTRIBUTE_VALUE);

                int newAgi = EditorGUILayout.IntSlider(
                    new GUIContent("Agility (AGI)", TroopAttributeDecoder.GetAttributeDescription("agi")),
                    agility,
                    TroopAttributeDecoder.MIN_ATTRIBUTE_VALUE,
                    TroopAttributeDecoder.MAX_ATTRIBUTE_VALUE);

                int newInt = EditorGUILayout.IntSlider(
                    new GUIContent("Intelligence (INT)", TroopAttributeDecoder.GetAttributeDescription("int")),
                    intelligence,
                    TroopAttributeDecoder.MIN_ATTRIBUTE_VALUE,
                    TroopAttributeDecoder.MAX_ATTRIBUTE_VALUE);

                int newCha = EditorGUILayout.IntSlider(
                    new GUIContent("Charisma (CHA)", TroopAttributeDecoder.GetAttributeDescription("cha")),
                    charisma,
                    TroopAttributeDecoder.MIN_ATTRIBUTE_VALUE,
                    TroopAttributeDecoder.MAX_ATTRIBUTE_VALUE);

                // Check if any values changed
                if (newLevel != level || newStr != strength || newAgi != agility || 
                    newInt != intelligence || newCha != charisma)
                {
                    level = newLevel;
                    strength = newStr;
                    agility = newAgi;
                    intelligence = newInt;
                    charisma = newCha;
                    EncodeAttributes();
                }

                // Show formatted string
                EditorGUILayout.Space(5);
                EditorGUILayout.LabelField("Module System Format:", infoStyle);
                string formatted = TroopAttributeDecoder.FormatAttributesForDisplay(
                    strength, agility, intelligence, charisma, level);
                EditorGUILayout.SelectableLabel(formatted, GUILayout.Height(18));

                EditorGUILayout.EndVertical();
            }
            EditorGUILayout.EndFoldoutHeaderGroup();
        }

        // VALIDATION SECTION
        private void DrawValidationSection()
        {
            showValidation = EditorGUILayout.BeginFoldoutHeaderGroup(showValidation, "Validation & Info");
            if (showValidation)
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);

                // Attribute points validation
                string attrValidation = TroopAttributeDecoder.GetAttributePointsValidation(
                    strength, agility, intelligence, charisma, level);
                
                bool hasAttributeWarning = attrValidation.Contains("⚠️");
                if (hasAttributeWarning)
                {
                    EditorGUILayout.HelpBox(attrValidation, MessageType.Warning);
                }
                else
                {
                    EditorGUILayout.HelpBox(attrValidation, MessageType.Info);
                }

                EditorGUILayout.Space(5);

                // Calculated values
                EditorGUILayout.LabelField("Calculated Values:", EditorStyles.boldLabel);

                int expectedSkills = TroopAttributeDecoder.CalculateExpectedSkillPoints(level, intelligence);
                EditorGUILayout.LabelField($"Expected Skill Points: {expectedSkills}", infoStyle);

                int hitPoints = TroopAttributeDecoder.CalculateHitPoints(strength, 0);
                EditorGUILayout.LabelField($"Hit Points (base, Ironflesh 0): {hitPoints}", infoStyle);

                int hitPointsIron3 = TroopAttributeDecoder.CalculateHitPoints(strength, 3);
                EditorGUILayout.LabelField($"Hit Points (with Ironflesh 3): {hitPointsIron3}", infoStyle);

                int partySize = charisma;
                EditorGUILayout.LabelField($"Party Size Bonus: +{partySize}", infoStyle);

                // Weapon proficiency points from agility
                int weaponProfPoints = agility * 5;
                EditorGUILayout.LabelField($"Weapon Proficiency Points from AGI: {weaponProfPoints}", infoStyle);

                EditorGUILayout.Space(5);

                // Attribute breakdown
                EditorGUILayout.LabelField("Attribute Effects:", EditorStyles.boldLabel);
                EditorGUILayout.LabelField($"• Strength: +{strength} HP, increases melee/bow/thrown damage", infoStyle);
                EditorGUILayout.LabelField($"• Agility: +{weaponProfPoints} weapon proficiency points, movement speed", infoStyle);
                EditorGUILayout.LabelField($"• Intelligence: +{intelligence} skill points", infoStyle);
                EditorGUILayout.LabelField($"• Charisma: +{charisma} party size limit", infoStyle);

                EditorGUILayout.EndVertical();
            }
            EditorGUILayout.EndFoldoutHeaderGroup();
        }

        // QUICK PRESETS
        private void DrawQuickPresets()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Quick Presets:", EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button("Default (Level 1)"))
            {
                strength = 5;
                agility = 5;
                intelligence = 4;
                charisma = 4;
                level = 1;
                EncodeAttributes();
            }

            if (GUILayout.Button("Balanced (Level 10)"))
            {
                strength = 10;
                agility = 10;
                intelligence = 8;
                charisma = 8;
                level = 10;
                EncodeAttributes();
            }

            if (GUILayout.Button("Warrior (Level 20)"))
            {
                strength = 18;
                agility = 15;
                intelligence = 8;
                charisma = 12;
                level = 20;
                EncodeAttributes();
            }

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button("Archer (Level 20)"))
            {
                strength = 12;
                agility = 21;
                intelligence = 8;
                charisma = 12;
                level = 20;
                EncodeAttributes();
            }

            if (GUILayout.Button("Commander (Level 25)"))
            {
                strength = 15;
                agility = 14;
                intelligence = 12;
                charisma = 22;
                level = 25;
                EncodeAttributes();
            }

            if (GUILayout.Button("Hero (Level 30)"))
            {
                strength = 21;
                agility = 18;
                intelligence = 15;
                charisma = 15;
                level = 30;
                EncodeAttributes();
            }

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(3);
            EditorGUILayout.LabelField("Note: Presets will automatically balance attribute points for the level.", infoStyle);

            EditorGUILayout.EndVertical();
        }

        // PARSING (Read raw values into UI state)
        private void ParseCurrentValues()
        {
            if (targetTroop == null) return;

            try
            {
                // Parse attributes
                string attributesValue = targetTroop.TroopAttributes;
                lastParsedAttributesValue = attributesValue;

                var (str, agi, intel, cha, lvl) = TroopAttributeDecoder.DecodeAllAttributes(attributesValue);
                strength = str;
                agility = agi;
                intelligence = intel;
                charisma = cha;
                level = lvl;

                Repaint();
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to parse troop attributes: {ex.Message}");
            }
        }

        // ENCODING (Build raw values from UI state)
        private void EncodeAttributes()
        {
            if (targetTroop == null) return;

            try
            {
                BigInteger encoded = TroopAttributeDecoder.EncodeAllAttributes(
                    strength, agility, intelligence, charisma, level);

                string newValue = TroopAttributeDecoder.FormatToHex(encoded);

                if (targetTroop.TroopAttributes != newValue)
                {
                    Undo.RecordObject(targetTroop, "Modify Troop Attributes");
                    targetTroop.TroopAttributes = newValue;
                    lastParsedAttributesValue = newValue;
                    EditorUtility.SetDirty(targetTroop);
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to encode troop attributes: {ex.Message}");
            }
        }
    }
}
