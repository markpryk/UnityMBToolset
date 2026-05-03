using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using MountAndBlade.Data;
using UnityEngine;
using UnityEditor;
using Vector2 = UnityEngine.Vector2;

namespace MountAndBlade.Editor
{
    /// <summary>
    /// Unity Editor Window for viewing and editing Mount & Blade item modifier bits.
    /// All modifiers are simple bitwise flags that can be combined.
    /// </summary>
    public class ItemModifierBitsWindow : EditorWindow
    {
        private MBItemData targetItem;
        private Vector2 scrollPosition;

        // UI State - Foldouts
        private bool showWeaponModifiers = true;
        private bool showArmorModifiers = true;
        private bool showHorseModifiers = true;
        private bool showFoodModifiers = true;
        private bool showMiscModifiers = true;
        private bool showPresets = true;

        // Decoded values - all modifiers are bitwise flags
        private HashSet<string> activeModifiers = new HashSet<string>();

        // For real-time sync
        private string lastParsedModifierValue = "";

        private bool debugMode = false;

        // Tooltip style
        private GUIStyle tooltipStyle;
        private GUIStyle presetButtonStyle;

        public static void ShowWindow(MBItemData item)
        {
            var window = GetWindow<ItemModifierBitsWindow>("Modifier Bits Editor");
            window.targetItem = item;
            window.minSize = new Vector2(500, 600);
            window.ParseCurrentModifiers();
        }

        private void OnEnable()
        {
            if (targetItem != null)
            {
                ParseCurrentModifiers();
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
            EditorGUILayout.LabelField("Mount & Blade Modifier Bits Editor",
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
                    ParseCurrentModifiers();
                }
            }

            EditorGUILayout.EndVertical();

            if (targetItem == null)
            {
                EditorGUILayout.HelpBox("Select an MBItemData asset to edit its modifier bits.", MessageType.Info);
                return;
            }

            EditorGUILayout.Space(5);

            // Check if modifier value changed externally
            if (targetItem.ModifierBits != lastParsedModifierValue)
            {
                ParseCurrentModifiers();
            }

            // RAW VALUE DISPLAY & DEBUG
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            debugMode = EditorGUILayout.ToggleLeft(
                new GUIContent("Debug Mode",
                    "When enabled, displays raw values and internal names."),
                debugMode);

            if (debugMode)
            {
                EditorGUILayout.LabelField("Current Raw Value:", EditorStyles.boldLabel);
                EditorGUILayout.SelectableLabel(
                    string.IsNullOrEmpty(targetItem.ModifierBits) ? "(empty)" : targetItem.ModifierBits,
                    GUILayout.Height(20));

                // Active modifiers count
                EditorGUILayout.LabelField($"Active Modifiers: {activeModifiers.Count}");
            }

            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(5);

            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

            // PRESET BUTTONS
            showPresets = EditorGUILayout.BeginFoldoutHeaderGroup(showPresets, "Common Presets");
            if (showPresets)
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                DrawPresetButtons();
                EditorGUILayout.EndVertical();
            }
            EditorGUILayout.EndFoldoutHeaderGroup();

            EditorGUILayout.Space(5);

            // WEAPON MODIFIERS
            showWeaponModifiers = EditorGUILayout.BeginFoldoutHeaderGroup(showWeaponModifiers, "Weapon Modifiers");
            if (showWeaponModifiers)
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);

                DrawModifierSection("Damage Quality", new[]
                {
                    "imodbit_plain",
                    "imodbit_chipped",
                    "imodbit_rusty",
                    "imodbit_bent",
                    "imodbit_cracked"
                }, "Negative modifiers that reduce weapon damage");

                EditorGUILayout.Space(5);

                DrawModifierSection("Damage Bonuses", new[]
                {
                    "imodbit_fine",
                    "imodbit_sharp",
                    "imodbit_balanced",
                    "imodbit_tempered",
                    "imodbit_masterwork"
                }, "Positive modifiers that increase weapon damage");

                EditorGUILayout.Space(5);

                DrawModifierSection("Weight Modifiers", new[]
                {
                    "imodbit_heavy",
                    "imodbit_strong",
                    "imodbit_powerful"
                }, "Modifiers affecting weapon weight and damage");

                EditorGUILayout.Space(5);

                DrawModifierSection("Special Weapon", new[]
                {
                    "imodbit_deadly",
                    "imodbit_exquisite"
                }, "Special weapon modifiers (unused in vanilla)");

                EditorGUILayout.EndVertical();
            }
            EditorGUILayout.EndFoldoutHeaderGroup();

            EditorGUILayout.Space(5);

            // ARMOR MODIFIERS
            showArmorModifiers = EditorGUILayout.BeginFoldoutHeaderGroup(showArmorModifiers, "Armor Modifiers");
            if (showArmorModifiers)
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);

                DrawModifierSection("Cloth/Leather (Negative)", new[]
                {
                    "imodbit_tattered",
                    "imodbit_ragged"
                }, "Negative modifiers for cloth and leather armor");

                EditorGUILayout.Space(5);

                DrawModifierSection("Cloth/Leather (Positive)", new[]
                {
                    "imodbit_sturdy",
                    "imodbit_thick",
                    "imodbit_hardened"
                }, "Positive modifiers for cloth and leather armor");

                EditorGUILayout.Space(5);

                DrawModifierSection("Metal Armor (Negative)", new[]
                {
                    "imodbit_rusty",
                    "imodbit_battered",
                    "imodbit_crude",
                    "imodbit_cracked"
                }, "Negative modifiers for metal armor");

                EditorGUILayout.Space(5);

                DrawModifierSection("Metal Armor (Positive)", new[]
                {
                    "imodbit_thick",
                    "imodbit_reinforced",
                    "imodbit_lordly"
                }, "Positive modifiers for metal armor");

                EditorGUILayout.Space(5);

                DrawModifierSection("Quality (Unused)", new[]
                {
                    "imodbit_poor",
                    "imodbit_old",
                    "imodbit_cheap",
                    "imodbit_well_made",
                    "imodbit_rough",
                    "imodbit_superb"
                }, "Mostly unused quality modifiers");

                EditorGUILayout.EndVertical();
            }
            EditorGUILayout.EndFoldoutHeaderGroup();

            EditorGUILayout.Space(5);

            // HORSE MODIFIERS
            showHorseModifiers = EditorGUILayout.BeginFoldoutHeaderGroup(showHorseModifiers, "Horse Modifiers");
            if (showHorseModifiers)
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);

                DrawModifierSection("Negative", new[]
                {
                    "imodbit_lame",
                    "imodbit_swaybacked"
                }, "Negative horse modifiers");

                EditorGUILayout.Space(5);

                DrawModifierSection("Neutral/Mixed", new[]
                {
                    "imodbit_stubborn",
                    "imodbit_timid",
                    "imodbit_meek"
                }, "Neutral horse modifiers (timid/meek unused in vanilla)");

                EditorGUILayout.Space(5);

                DrawModifierSection("Positive", new[]
                {
                    "imodbit_spirited",
                    "imodbit_heavy",
                    "imodbit_champion"
                }, "Positive horse modifiers");

                EditorGUILayout.EndVertical();
            }
            EditorGUILayout.EndFoldoutHeaderGroup();

            EditorGUILayout.Space(5);

            // FOOD MODIFIERS
            showFoodModifiers = EditorGUILayout.BeginFoldoutHeaderGroup(showFoodModifiers, "Food Modifiers");
            if (showFoodModifiers)
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);

                DrawModifierSection("Food Freshness", new[]
                {
                    "imodbit_fresh",
                    "imodbit_day_old",
                    "imodbit_two_day_old",
                    "imodbit_smelling",
                    "imodbit_rotten"
                }, "Food freshness modifiers (auto-decay over time)");

                EditorGUILayout.EndVertical();
            }
            EditorGUILayout.EndFoldoutHeaderGroup();

            EditorGUILayout.Space(5);

            // AMMUNITION MODIFIERS
            showMiscModifiers = EditorGUILayout.BeginFoldoutHeaderGroup(showMiscModifiers, "Ammunition Modifiers");
            if (showMiscModifiers)
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);

                DrawModifierSection("Ammunition", new[]
                {
                    "imodbit_large_bag",
                    "imodbit_bent"
                }, "Ammunition quantity and quality modifiers");

                EditorGUILayout.EndVertical();
            }
            EditorGUILayout.EndFoldoutHeaderGroup();

            EditorGUILayout.Space(10);

            // ACTIVE MODIFIERS SUMMARY
            if (activeModifiers.Count > 0)
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.LabelField("Active Modifiers Summary", EditorStyles.boldLabel);

                string modifierList = string.Join(" | ", activeModifiers.OrderBy(m => m).Select(m => FormatModifierName(m)));
                EditorGUILayout.LabelField(modifierList, EditorStyles.wordWrappedLabel);

                EditorGUILayout.EndVertical();
            }

            EditorGUILayout.EndScrollView();
        }

        // PRESET BUTTONS

        private void DrawPresetButtons()
        {
            EditorGUILayout.LabelField("Click to apply common modifier bit presets:", EditorStyles.miniLabel);
            EditorGUILayout.Space(3);

            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button("None", GUILayout.Height(25)))
            {
                ApplyPreset(new string[] { });
            }

            if (GUILayout.Button("Sword", GUILayout.Height(25)))
            {
                ApplyPreset(new[] { "imodbit_rusty", "imodbit_chipped", "imodbit_balanced", "imodbit_tempered" });
            }

            if (GUILayout.Button("Sword (High)", GUILayout.Height(25)))
            {
                ApplyPreset(new[] { "imodbit_rusty", "imodbit_chipped", "imodbit_balanced", "imodbit_tempered", "imodbit_masterwork" });
            }

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button("Axe", GUILayout.Height(25)))
            {
                ApplyPreset(new[] { "imodbit_rusty", "imodbit_chipped", "imodbit_heavy" });
            }

            if (GUILayout.Button("Mace", GUILayout.Height(25)))
            {
                ApplyPreset(new[] { "imodbit_rusty", "imodbit_chipped", "imodbit_heavy" });
            }

            if (GUILayout.Button("Polearm", GUILayout.Height(25)))
            {
                ApplyPreset(new[] { "imodbit_cracked", "imodbit_bent", "imodbit_balanced" });
            }

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button("Bow", GUILayout.Height(25)))
            {
                ApplyPreset(new[] { "imodbit_cracked", "imodbit_bent", "imodbit_strong", "imodbit_masterwork" });
            }

            if (GUILayout.Button("Crossbow", GUILayout.Height(25)))
            {
                ApplyPreset(new[] { "imodbit_cracked", "imodbit_bent", "imodbit_masterwork" });
            }

            if (GUILayout.Button("Thrown", GUILayout.Height(25)))
            {
                ApplyPreset(new[] { "imodbit_bent", "imodbit_heavy", "imodbit_balanced", "imodbit_large_bag" });
            }

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button("Cloth Armor", GUILayout.Height(25)))
            {
                ApplyPreset(new[] { "imodbit_tattered", "imodbit_ragged", "imodbit_sturdy", "imodbit_thick", "imodbit_hardened" });
            }

            if (GUILayout.Button("Metal Armor", GUILayout.Height(25)))
            {
                ApplyPreset(new[] { "imodbit_rusty", "imodbit_battered", "imodbit_crude", "imodbit_thick", "imodbit_reinforced", "imodbit_lordly" });
            }

            if (GUILayout.Button("Plate Armor", GUILayout.Height(25)))
            {
                ApplyPreset(new[] { "imodbit_cracked", "imodbit_rusty", "imodbit_battered", "imodbit_crude", "imodbit_thick", "imodbit_reinforced", "imodbit_lordly" });
            }

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button("Shield", GUILayout.Height(25)))
            {
                ApplyPreset(new[] { "imodbit_cracked", "imodbit_battered", "imodbit_thick", "imodbit_reinforced" });
            }

            if (GUILayout.Button("Horse (Basic)", GUILayout.Height(25)))
            {
                ApplyPreset(new[] { "imodbit_swaybacked", "imodbit_lame", "imodbit_spirited", "imodbit_heavy", "imodbit_stubborn" });
            }

            if (GUILayout.Button("Horse (Good)", GUILayout.Height(25)))
            {
                ApplyPreset(new[] { "imodbit_spirited", "imodbit_heavy" });
            }

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button("Missile", GUILayout.Height(25)))
            {
                ApplyPreset(new[] { "imodbit_bent", "imodbit_large_bag" });
            }

            if (GUILayout.Button("Good Quality", GUILayout.Height(25)))
            {
                ApplyPreset(new[] { "imodbit_sturdy", "imodbit_thick", "imodbit_hardened", "imodbit_reinforced" });
            }

            if (GUILayout.Button("Bad Quality", GUILayout.Height(25)))
            {
                ApplyPreset(new[] { "imodbit_rusty", "imodbit_chipped", "imodbit_tattered", "imodbit_ragged", "imodbit_cracked", "imodbit_bent" });
            }

            EditorGUILayout.EndHorizontal();
        }

        private void ApplyPreset(string[] modifiers)
        {
            activeModifiers.Clear();
            foreach (var mod in modifiers)
            {
                activeModifiers.Add(mod);
            }
            EncodeModifiers();
        }

        // DRAWING MODIFIER SECTIONS

        private void DrawModifierSection(string sectionTitle, string[] modifierNames, string tooltip = "")
        {
            EditorGUILayout.LabelField(new GUIContent(sectionTitle, tooltip), EditorStyles.boldLabel);

            EditorGUI.indentLevel++;

            // Draw in a grid layout (2 columns)
            int columns = 2;
            int itemsPerColumn = (modifierNames.Length + columns - 1) / columns;

            EditorGUILayout.BeginHorizontal();

            for (int col = 0; col < columns; col++)
            {
                EditorGUILayout.BeginVertical();

                int start = col * itemsPerColumn;
                int end = Mathf.Min(start + itemsPerColumn, modifierNames.Length);

                for (int i = start; i < end; i++)
                {
                    string modName = modifierNames[i];
                    bool isActive = activeModifiers.Contains(modName);

                    string displayName = debugMode ? modName : FormatModifierName(modName);

                    EditorGUI.BeginChangeCheck();
                    bool newValue = EditorGUILayout.ToggleLeft(
                        new GUIContent(displayName, GetModifierTooltip(modName)),
                        isActive);

                    if (EditorGUI.EndChangeCheck())
                    {
                        if (newValue)
                            activeModifiers.Add(modName);
                        else
                            activeModifiers.Remove(modName);

                        EncodeModifiers();
                    }
                }

                EditorGUILayout.EndVertical();
            }

            EditorGUILayout.EndHorizontal();

            EditorGUI.indentLevel--;
        }

        // PARSING (Decode raw value to UI state)

        private void ParseCurrentModifiers()
        {
            if (targetItem == null || string.IsNullOrWhiteSpace(targetItem.ModifierBits))
            {
                activeModifiers.Clear();
                lastParsedModifierValue = "";
                return;
            }

            try
            {
                BigInteger value = ParseBigInteger(targetItem.ModifierBits);
                lastParsedModifierValue = targetItem.ModifierBits;

                // Decode all modifiers
                var decoded = ItemModifierBitsDecoder.DecodeModifiers(value);

                activeModifiers.Clear();
                foreach (var mod in decoded)
                {
                    activeModifiers.Add(mod);
                }

                Repaint();
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to parse modifier bits: {ex.Message}");
            }
        }

        // ENCODING (Build raw value from UI state)

        private void EncodeModifiers()
        {
            if (targetItem == null) return;

            try
            {
                BigInteger encodedValue = 0;

                // All modifiers are simple bitwise flags
                var modifierBits = ItemModifierBitsDecoder.GetModifierBits();
                foreach (var activeModifier in activeModifiers)
                {
                    if (modifierBits.TryGetValue(activeModifier, out BigInteger bitValue))
                    {
                        encodedValue |= bitValue;
                    }
                }

                // Write back
                string newModifierValue = FormatBigInteger(encodedValue);

                if (targetItem.ModifierBits != newModifierValue)
                {
                    Undo.RecordObject(targetItem, "Modify Item Modifier Bits");
                    targetItem.ModifierBits = newModifierValue;
                    lastParsedModifierValue = newModifierValue;
                    EditorUtility.SetDirty(targetItem);
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to encode modifier bits: {ex.Message}");
            }
        }

        // UTILITY METHODS

        private string FormatModifierName(string modifierName)
        {
            // Convert "imodbit_well_made" to "Well Made"
            string name = modifierName.Replace("imodbit_", "");
            name = name.Replace("_", " ");

            // Capitalize each word
            var words = name.Split(' ');
            for (int i = 0; i < words.Length; i++)
            {
                if (words[i].Length > 0)
                {
                    words[i] = char.ToUpper(words[i][0]) + words[i].Substring(1);
                }
            }

            return string.Join(" ", words);
        }

        private string GetModifierTooltip(string modifierName)
        {
            // Provide helpful tooltips for each modifier
            switch (modifierName)
            {
                // Weapon negative
                case "imodbit_plain": return "Default item, no modifier";
                case "imodbit_cracked": return "-5 damage, -50% price";
                case "imodbit_rusty": return "-3 damage, -45% price";
                case "imodbit_bent": return "-3 damage, -3 speed, -35% price";
                case "imodbit_chipped": return "-1 damage, -28% price";

                // Weapon positive
                case "imodbit_fine": return "+1 damage";
                case "imodbit_balanced": return "+3 damage, +3 speed, +250% price";
                case "imodbit_tempered": return "+4 damage, +670% price";
                case "imodbit_masterwork": return "+5 damage, +1 speed, +4 requirement, +1650% price";
                case "imodbit_heavy": return "+2 damage, -2 speed, +1 requirement, +90% price";
                case "imodbit_strong": return "+3 damage, -3 speed, +2 requirement, +360% price";

                // Armor negative
                case "imodbit_tattered": return "-3 armor, -50% price (cloth)";
                case "imodbit_ragged": return "-2 armor, -30% price (cloth)";
                case "imodbit_battered": return "-2 armor, -30% price (metal)";
                case "imodbit_crude": return "-1 armor, -17% price";

                // Armor positive
                case "imodbit_sturdy": return "+1 armor, +70% price";
                case "imodbit_thick": return "+2 armor, +160% price";
                case "imodbit_hardened": return "+3 armor, +290% price (cloth)";
                case "imodbit_reinforced": return "+4 armor, +550% price";
                case "imodbit_lordly": return "+6 armor, +1050% price";

                // Horse modifiers
                case "imodbit_lame": return "-10 speed, -5 maneuver, -60% price";
                case "imodbit_swaybacked": return "-4 speed, -2 maneuver, -40% price";
                case "imodbit_stubborn": return "+5 HP, +1 requirement, -10% price";
                case "imodbit_spirited": return "+2 speed, +1 maneuver, +1 charge, +550% price";
                case "imodbit_champion": return "+4 speed, +2 maneuver, +2 charge, +2 req, +1350% price";
                case "imodbit_timid": return "Unused - can be activated in module.ini";
                case "imodbit_meek": return "Unused - can be activated in module.ini";

                // Food
                case "imodbit_fresh": return "Food freshness - decays to day_old";
                case "imodbit_day_old": return "Food freshness - decays to two_day_old";
                case "imodbit_two_day_old": return "Food freshness - decays to smelling";
                case "imodbit_smelling": return "Food freshness - decays to rotten";
                case "imodbit_rotten": return "Food is rotten - final decay state";

                // Ammunition
                case "imodbit_large_bag": return "+13% quantity, +90% price";

                // Unused
                case "imodbit_poor": return "Unused modifier";
                case "imodbit_old": return "Unused modifier";
                case "imodbit_cheap": return "Unused modifier";
                case "imodbit_well_made": return "Unused modifier";
                case "imodbit_sharp": return "Unused modifier";
                case "imodbit_deadly": return "Unused - can be used for custom effects";
                case "imodbit_exquisite": return "Unused modifier";
                case "imodbit_powerful": return "Unused modifier";
                case "imodbit_rough": return "Unused modifier";
                case "imodbit_superb": return "Unused modifier";

                default: return modifierName;
            }
        }

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
