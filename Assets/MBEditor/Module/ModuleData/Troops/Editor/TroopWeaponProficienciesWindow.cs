using System;
using System.Collections.Generic;
using System.Numerics;
using MountAndBlade.Data;
using UnityEngine;
using UnityEditor;

namespace MountAndBlade.Editor
{
    public class TroopWeaponProficienciesWindow : EditorWindow
    {
        private MBTroopData targetTroop;
        private UnityEngine.Vector2 scrollPosition;

        // UI State - Foldouts
        private bool showProficiencies = true;
        private bool showValidation = true;

        // Decoded proficiency values
        private Dictionary<TroopWeaponProficiencyDecoder.ProficiencyType, int> proficiencies = 
            new Dictionary<TroopWeaponProficiencyDecoder.ProficiencyType, int>();

        // For real-time sync
        private string lastParsedProficienciesValue = "";

        // Styles
        private GUIStyle headerStyle;
        private GUIStyle infoStyle;
        private bool stylesInitialized = false;
        private bool debugMode = false;

        // For validation display
        private int troopLevel = 1;
        private int troopAgility = 5;
        public static void ShowWindow(MBTroopData troop)
        {
            var window = GetWindow<TroopWeaponProficienciesWindow>("Weapon Proficiencies");
            window.targetTroop = troop;
            window.minSize = new UnityEngine.Vector2(550, 700);
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
            EditorGUILayout.LabelField("Mount & Blade Weapon Proficiencies Editor",
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
                EditorGUILayout.HelpBox("Select an MBTroopData asset to edit its weapon proficiencies.", MessageType.Info);
                return;
            }

            EditorGUILayout.Space(5);

            // Check if values changed externally
            if (targetTroop.WeaponProficiencies != lastParsedProficienciesValue)
            {
                ParseCurrentValues();
            }

            // DEBUG MODE & RAW VALUES
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            debugMode = EditorGUILayout.ToggleLeft(
                new GUIContent("Debug Mode",
                    "Show raw hex values and internal names (wp_xxx format)"),
                debugMode);

            if (debugMode)
            {
                EditorGUILayout.LabelField("Raw Proficiencies Value:", EditorStyles.miniLabel);
                EditorGUILayout.SelectableLabel(
                    string.IsNullOrEmpty(targetTroop.WeaponProficiencies) ? "(empty)" : targetTroop.WeaponProficiencies,
                    GUILayout.Height(18));
            }

            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(5);

            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

            // WEAPON PROFICIENCIES SECTION
            DrawWeaponProficienciesSection();

            EditorGUILayout.Space(5);

            // VALIDATION SECTION
            DrawValidationSection();

            EditorGUILayout.Space(5);

            // QUICK PRESETS
            DrawQuickPresets();

            EditorGUILayout.EndScrollView();
        }

        // WEAPON PROFICIENCIES SECTION
        private void DrawWeaponProficienciesSection()
        {
            showProficiencies = EditorGUILayout.BeginFoldoutHeaderGroup(showProficiencies, "Weapon Proficiencies");
            if (showProficiencies)
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);

                EditorGUILayout.LabelField("Weapon proficiency affects damage, speed, and accuracy:", infoStyle);
                EditorGUILayout.LabelField($"Game maximum: {TroopWeaponProficiencyDecoder.MAX_PROFICIENCY}", infoStyle);
                EditorGUILayout.Space(3);

                bool anyChanged = false;

                // Draw sliders for each proficiency
                foreach (var type in new[] {
                    TroopWeaponProficiencyDecoder.ProficiencyType.OneHanded,
                    TroopWeaponProficiencyDecoder.ProficiencyType.TwoHanded,
                    TroopWeaponProficiencyDecoder.ProficiencyType.Polearm,
                    TroopWeaponProficiencyDecoder.ProficiencyType.Archery,
                    TroopWeaponProficiencyDecoder.ProficiencyType.Crossbow,
                    TroopWeaponProficiencyDecoder.ProficiencyType.Throwing,
                    TroopWeaponProficiencyDecoder.ProficiencyType.Firearm
                })
                {
                    int currentValue = proficiencies.ContainsKey(type) ? proficiencies[type] : 0;
                    string label = debugMode ? 
                        TroopWeaponProficiencyDecoder.GetInternalName(type) : 
                        TroopWeaponProficiencyDecoder.GetFriendlyName(type);
                    string description = TroopWeaponProficiencyDecoder.GetProficiencyDescription(type);

                    int newValue = EditorGUILayout.IntSlider(
                        new GUIContent(label, description),
                        currentValue,
                        TroopWeaponProficiencyDecoder.MIN_PROFICIENCY,
                        TroopWeaponProficiencyDecoder.MAX_PROFICIENCY);

                    if (newValue != currentValue)
                    {
                        proficiencies[type] = newValue;
                        anyChanged = true;
                    }
                }

                if (anyChanged)
                {
                    EncodeProficiencies();
                }

                // Show formatted string
                EditorGUILayout.Space(5);
                EditorGUILayout.LabelField("Module System Format:", infoStyle);
                string formatted = TroopWeaponProficiencyDecoder.FormatProficienciesForDisplay(proficiencies, debugMode);
                if (string.IsNullOrEmpty(formatted))
                    formatted = "wp(0)";
                EditorGUILayout.SelectableLabel(formatted, GUILayout.Height(18));

                EditorGUILayout.EndVertical();
            }
            EditorGUILayout.EndFoldoutHeaderGroup();
        }

        // VALIDATION SECTION
        private void DrawValidationSection()
        {
            showValidation = EditorGUILayout.BeginFoldoutHeaderGroup(showValidation, "Proficiency Info & Validation");
            if (showValidation)
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);

                // Input fields for validation calculation
                EditorGUILayout.LabelField("Troop Stats (for validation):", EditorStyles.boldLabel);
                EditorGUILayout.BeginHorizontal();
                
                int newLevel = EditorGUILayout.IntField(
                    new GUIContent("Level", "Troop level for proficiency point calculation"),
                    troopLevel);
                
                int newAgility = EditorGUILayout.IntField(
                    new GUIContent("Agility", "AGI attribute for proficiency point calculation"),
                    troopAgility);
                
                EditorGUILayout.EndHorizontal();

                if (newLevel != troopLevel || newAgility != troopAgility)
                {
                    troopLevel = Math.Max(0, newLevel);
                    troopAgility = Math.Max(0, newAgility);
                }

                EditorGUILayout.Space(5);

                // Proficiency points info
                int actual = TroopWeaponProficiencyDecoder.CalculateActualProficiencyPoints(proficiencies);
                int expected = TroopWeaponProficiencyDecoder.CalculateExpectedProficiencyPoints(troopLevel, troopAgility);
                
                string profValidation = TroopWeaponProficiencyDecoder.GetProficiencyPointsValidation(
                    proficiencies, troopLevel, troopAgility, 0);
                
                EditorGUILayout.HelpBox(profValidation, MessageType.Info);

                EditorGUILayout.Space(3);
                EditorGUILayout.LabelField($"Total Points Spent: {actual}", infoStyle);
                EditorGUILayout.LabelField($"Suggested Points (Level {troopLevel}, AGI {troopAgility}): ~{expected}", infoStyle);
                EditorGUILayout.LabelField("Note: Unlike attributes, proficiencies don't auto-assign", infoStyle);

                EditorGUILayout.Space(5);

                // Weapon Master caps
                EditorGUILayout.LabelField("Weapon Master Skill Caps:", EditorStyles.boldLabel);
                EditorGUILayout.LabelField("The Weapon Master skill limits how high proficiencies can be raised:", infoStyle);
                
                EditorGUILayout.BeginVertical(headerStyle);
                for (int wm = 0; wm <= 10; wm++)
                {
                    int cap = TroopWeaponProficiencyDecoder.GetWeaponMasterCap(wm);
                    EditorGUILayout.LabelField($"  Weapon Master {wm}: Cap {cap}", infoStyle);
                }
                EditorGUILayout.EndVertical();

                EditorGUILayout.Space(5);

                // Proficiency effects
                EditorGUILayout.LabelField("Proficiency Effects:", EditorStyles.boldLabel);
                EditorGUILayout.LabelField("• Higher proficiency = more damage, faster attacks, better accuracy", infoStyle);
                EditorGUILayout.LabelField("• Ranged weapons: mainly affects accuracy and reload/draw speed", infoStyle);
                EditorGUILayout.LabelField("• Melee weapons: affects damage, attack speed, and block effectiveness", infoStyle);
                EditorGUILayout.LabelField("• Proficiency increases with use in combat", infoStyle);
                EditorGUILayout.LabelField("• Each level grants 10 proficiency points (Native default)", infoStyle);
                EditorGUILayout.LabelField("• Each AGI point grants 5 proficiency points", infoStyle);

                EditorGUILayout.EndVertical();
            }
            EditorGUILayout.EndFoldoutHeaderGroup();
        }

        // QUICK PRESETS
        private void DrawQuickPresets()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Quick Presets:", EditorStyles.boldLabel);

            // Row 1
            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button("Clear All"))
            {
                SetAllProficiencies(0);
            }

            if (GUILayout.Button("Recruit (60)"))
            {
                SetAllProficiencies(60);
            }

            if (GUILayout.Button("Soldier (100)"))
            {
                SetAllProficiencies(100);
            }

            EditorGUILayout.EndHorizontal();

            // Row 2
            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button("Veteran (150)"))
            {
                SetAllProficiencies(150);
            }

            if (GUILayout.Button("Elite (200)"))
            {
                SetAllProficiencies(200);
            }

            if (GUILayout.Button("Master (250)"))
            {
                SetAllProficiencies(250);
            }

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(5);

            // Specialized builds
            EditorGUILayout.LabelField("Specialized Builds:", EditorStyles.miniLabel);

            // Row 3
            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button("Infantry"))
            {
                SetProficienciesPreset(new Dictionary<TroopWeaponProficiencyDecoder.ProficiencyType, int>
                {
                    { TroopWeaponProficiencyDecoder.ProficiencyType.OneHanded, 180 },
                    { TroopWeaponProficiencyDecoder.ProficiencyType.TwoHanded, 150 },
                    { TroopWeaponProficiencyDecoder.ProficiencyType.Polearm, 100 },
                    { TroopWeaponProficiencyDecoder.ProficiencyType.Archery, 40 },
                    { TroopWeaponProficiencyDecoder.ProficiencyType.Crossbow, 40 },
                    { TroopWeaponProficiencyDecoder.ProficiencyType.Throwing, 80 },
                    { TroopWeaponProficiencyDecoder.ProficiencyType.Firearm, 0 }
                });
            }

            if (GUILayout.Button("Archer"))
            {
                SetProficienciesPreset(new Dictionary<TroopWeaponProficiencyDecoder.ProficiencyType, int>
                {
                    { TroopWeaponProficiencyDecoder.ProficiencyType.OneHanded, 80 },
                    { TroopWeaponProficiencyDecoder.ProficiencyType.TwoHanded, 40 },
                    { TroopWeaponProficiencyDecoder.ProficiencyType.Polearm, 60 },
                    { TroopWeaponProficiencyDecoder.ProficiencyType.Archery, 220 },
                    { TroopWeaponProficiencyDecoder.ProficiencyType.Crossbow, 40 },
                    { TroopWeaponProficiencyDecoder.ProficiencyType.Throwing, 60 },
                    { TroopWeaponProficiencyDecoder.ProficiencyType.Firearm, 0 }
                });
            }

            if (GUILayout.Button("Crossbowman"))
            {
                SetProficienciesPreset(new Dictionary<TroopWeaponProficiencyDecoder.ProficiencyType, int>
                {
                    { TroopWeaponProficiencyDecoder.ProficiencyType.OneHanded, 100 },
                    { TroopWeaponProficiencyDecoder.ProficiencyType.TwoHanded, 60 },
                    { TroopWeaponProficiencyDecoder.ProficiencyType.Polearm, 80 },
                    { TroopWeaponProficiencyDecoder.ProficiencyType.Archery, 40 },
                    { TroopWeaponProficiencyDecoder.ProficiencyType.Crossbow, 200 },
                    { TroopWeaponProficiencyDecoder.ProficiencyType.Throwing, 60 },
                    { TroopWeaponProficiencyDecoder.ProficiencyType.Firearm, 0 }
                });
            }

            EditorGUILayout.EndHorizontal();

            // Row 4
            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button("Cavalry"))
            {
                SetProficienciesPreset(new Dictionary<TroopWeaponProficiencyDecoder.ProficiencyType, int>
                {
                    { TroopWeaponProficiencyDecoder.ProficiencyType.OneHanded, 180 },
                    { TroopWeaponProficiencyDecoder.ProficiencyType.TwoHanded, 80 },
                    { TroopWeaponProficiencyDecoder.ProficiencyType.Polearm, 160 },
                    { TroopWeaponProficiencyDecoder.ProficiencyType.Archery, 60 },
                    { TroopWeaponProficiencyDecoder.ProficiencyType.Crossbow, 40 },
                    { TroopWeaponProficiencyDecoder.ProficiencyType.Throwing, 100 },
                    { TroopWeaponProficiencyDecoder.ProficiencyType.Firearm, 0 }
                });
            }

            if (GUILayout.Button("Horse Archer"))
            {
                SetProficienciesPreset(new Dictionary<TroopWeaponProficiencyDecoder.ProficiencyType, int>
                {
                    { TroopWeaponProficiencyDecoder.ProficiencyType.OneHanded, 120 },
                    { TroopWeaponProficiencyDecoder.ProficiencyType.TwoHanded, 40 },
                    { TroopWeaponProficiencyDecoder.ProficiencyType.Polearm, 80 },
                    { TroopWeaponProficiencyDecoder.ProficiencyType.Archery, 240 },
                    { TroopWeaponProficiencyDecoder.ProficiencyType.Crossbow, 40 },
                    { TroopWeaponProficiencyDecoder.ProficiencyType.Throwing, 100 },
                    { TroopWeaponProficiencyDecoder.ProficiencyType.Firearm, 0 }
                });
            }

            if (GUILayout.Button("Skirmisher"))
            {
                SetProficienciesPreset(new Dictionary<TroopWeaponProficiencyDecoder.ProficiencyType, int>
                {
                    { TroopWeaponProficiencyDecoder.ProficiencyType.OneHanded, 140 },
                    { TroopWeaponProficiencyDecoder.ProficiencyType.TwoHanded, 80 },
                    { TroopWeaponProficiencyDecoder.ProficiencyType.Polearm, 100 },
                    { TroopWeaponProficiencyDecoder.ProficiencyType.Archery, 80 },
                    { TroopWeaponProficiencyDecoder.ProficiencyType.Crossbow, 60 },
                    { TroopWeaponProficiencyDecoder.ProficiencyType.Throwing, 180 },
                    { TroopWeaponProficiencyDecoder.ProficiencyType.Firearm, 0 }
                });
            }

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(3);
            EditorGUILayout.LabelField("Note: Presets are suggestions. Adjust based on your troop design.", infoStyle);

            EditorGUILayout.EndVertical();
        }

        // PRESET HELPERS
        private void SetAllProficiencies(int value)
        {
            foreach (var key in new List<TroopWeaponProficiencyDecoder.ProficiencyType>(proficiencies.Keys))
            {
                proficiencies[key] = value;
            }
            EncodeProficiencies();
        }

        private void SetProficienciesPreset(Dictionary<TroopWeaponProficiencyDecoder.ProficiencyType, int> preset)
        {
            foreach (var kvp in preset)
            {
                proficiencies[kvp.Key] = kvp.Value;
            }
            EncodeProficiencies();
        }

        // PARSING (Read raw values into UI state)
        private void ParseCurrentValues()
        {
            if (targetTroop == null) return;

            try
            {
                // Parse proficiencies
                string proficienciesValue = targetTroop.WeaponProficiencies;
                lastParsedProficienciesValue = proficienciesValue;

                proficiencies = TroopWeaponProficiencyDecoder.DecodeAllProficiencies(proficienciesValue);

                // Try to get troop's actual level and agility if attributes are set
                if (!string.IsNullOrEmpty(targetTroop.TroopAttributes))
                {
                    try
                    {
                        var (str, agi, intel, cha, lvl) = TroopAttributeDecoder.DecodeAllAttributes(targetTroop.TroopAttributes);
                        troopLevel = lvl;
                        troopAgility = agi;
                    }
                    catch
                    {
                        // If parsing fails, keep current values
                    }
                }

                Repaint();
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to parse weapon proficiencies: {ex.Message}");
            }
        }

        // ENCODING (Build raw values from UI state)
        private void EncodeProficiencies()
        {
            if (targetTroop == null) return;

            try
            {
                BigInteger encoded = TroopWeaponProficiencyDecoder.EncodeAllProficiencies(proficiencies);

                string newValue = TroopWeaponProficiencyDecoder.FormatToHex(encoded);

                if (targetTroop.WeaponProficiencies != newValue)
                {
                    Undo.RecordObject(targetTroop, "Modify Weapon Proficiencies");
                    targetTroop.WeaponProficiencies = newValue;
                    lastParsedProficienciesValue = newValue;
                    EditorUtility.SetDirty(targetTroop);
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to encode weapon proficiencies: {ex.Message}");
            }
        }
    }
}
