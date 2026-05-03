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
    public class ItemFlagPropertiesWindow : EditorWindow
    {
        private MBItemData targetItem;
        private Vector2 scrollPosition;

        // UI State - Foldouts
        private bool showItemTypes = true;
        private bool showAttachmentFlags = true;
        private bool showOverlappingFlags = true;
        private bool showUniqueFlags = true;

        // Decoded values
        private string selectedItemType = "";
        private string selectedAttachmentFlag = "";
        private HashSet<string> activeUniqueFlags = new HashSet<string>();
        private HashSet<int> activeOverlappingGroups = new HashSet<int>();

        // For real-time sync
        private string lastParsedFlagsValue = "";

        // Styles
        private GUIStyle overlappingGroupStyle;
        private GUIStyle contextHintStyle;
        private bool stylesInitialized = false;
        private bool debugMode = false;
        public static void ShowWindow(MBItemData item)
        {
            var window = GetWindow<ItemFlagPropertiesWindow>("Flag Properties Editor");
            window.targetItem = item;
            window.minSize = new Vector2(550, 700);
            window.ParseCurrentFlags();
        }

        private void InitStyles()
        {
            if (stylesInitialized) return;

            overlappingGroupStyle = new GUIStyle(EditorStyles.helpBox)
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
            if (targetItem != null)
            {
                ParseCurrentFlags();
            }
        }

        private void OnGUI()
        {
            InitStyles();

            EditorGUILayout.Space(10);

            // HEADER
            EditorGUILayout.LabelField("Mount & Blade Flag Properties Editor",
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
                    ParseCurrentFlags();
                }
            }

            EditorGUILayout.EndVertical();

            if (targetItem == null)
            {
                EditorGUILayout.HelpBox("Select an MBItemData asset to edit its flags.", MessageType.Info);
                return;
            }

            EditorGUILayout.Space(5);

            // Check if flags value changed externally
            if (targetItem.Flags != lastParsedFlagsValue)
            {
                ParseCurrentFlags();
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
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.SelectableLabel(
                    string.IsNullOrEmpty(targetItem.Flags) ? "(empty)" : targetItem.Flags,
                    GUILayout.Height(20));
                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(5);
            
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

            // ITEM TYPE (bits 0-4) - ENCODED VALUE
            DrawItemTypeSection();

            EditorGUILayout.Space(5);

            // ATTACHMENT FLAGS (bits 8-11) - ENCODED VALUE
            DrawAttachmentSection();

            EditorGUILayout.Space(5);

            // OVERLAPPING FLAGS - Grouped display
            DrawOverlappingFlagsSection();

            EditorGUILayout.Space(5);

            // UNIQUE PROPERTY FLAGS (bits 12+)
            DrawUniqueFlagsSection();

            EditorGUILayout.EndScrollView();
        }

        // ITEM TYPE SECTION
        private void DrawItemTypeSection()
        {
            var bitsInfo = debugMode ? " (bits 0-4)" : "";
            showItemTypes = EditorGUILayout.BeginFoldoutHeaderGroup(showItemTypes, $"Item Type{bitsInfo}");
            if (showItemTypes)
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);

                var itemTypes = ItemFlagPropertyDecoder.GetItemTypes();
                string newItemType = selectedItemType;

                foreach (var type in itemTypes.OrderBy(x => x.Value))
                {
                    bool isSelected = selectedItemType == type.Key;

                    string hex = debugMode ? $"0x{type.Value:X}" : "";
                    string nm = debugMode ? type.Key : FormatItemTypeName(type.Key);
                    string tooltip = ItemFlagTooltips.GetItemTypeTooltip(type.Key);
                    GUIContent content = new GUIContent(
                        $"{nm} {hex}",
                        tooltip
                    );

                    bool newSelected = EditorGUILayout.ToggleLeft(content, isSelected);

                    if (newSelected && !isSelected)
                    {
                        newItemType = type.Key;
                    }
                    else if (!newSelected && isSelected)
                    {
                        newItemType = "";
                    }
                }

                if (newItemType != selectedItemType)
                {
                    selectedItemType = newItemType;
                    EncodeFlags();
                }

                EditorGUILayout.EndVertical();
            }

            EditorGUILayout.EndFoldoutHeaderGroup();
        }

        private string FormatFlagName(string flag)
        {
            if (debugMode)
                return flag;

            // Remove "itcf_" prefix and format nicely
            string nm = flag.Replace("itp_", "");
            nm = System.Text.RegularExpressions.Regex.Replace(nm, "_", " ");
            return System.Globalization.CultureInfo.CurrentCulture.TextInfo.ToTitleCase(nm);
        }

        private string FormatItemTypeName(string iType)
        {
            if (debugMode)
                return iType;

            // Remove "itcf_" prefix and format nicely
            string nm = iType.Replace("itp_type_", "");
            nm = System.Text.RegularExpressions.Regex.Replace(nm, "_", " ");
            return System.Globalization.CultureInfo.CurrentCulture.TextInfo.ToTitleCase(nm);
        }

        // ATTACHMENT FLAGS SECTION
        private void DrawAttachmentSection()
        {
            var bitsInfo = debugMode ? "(bits 8-11) " : "";
            showAttachmentFlags = EditorGUILayout.BeginFoldoutHeaderGroup(
                showAttachmentFlags, $"Attachment Mode {bitsInfo}- Mutually Exclusive");

            if (showAttachmentFlags)
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.HelpBox(
                    "Only ONE attachment mode can be active. These occupy the same bit positions.",
                    MessageType.Info);

                var attachmentFlags = ItemFlagPropertyDecoder.GetAttachmentFlags();
                string newAttachment = selectedAttachmentFlag;

                // "None" option
                bool isNoneSelected = string.IsNullOrEmpty(selectedAttachmentFlag);
                string noneTooltip = ItemFlagTooltips.GetAttachmentFlagTooltip("");
                GUIContent noneContent = new GUIContent("None (default)", noneTooltip);
                bool newNoneSelected = EditorGUILayout.ToggleLeft(noneContent, isNoneSelected);

                if (newNoneSelected && !isNoneSelected)
                {
                    newAttachment = "";
                }

                foreach (var attach in attachmentFlags.OrderBy(x => x.Value))
                {
                    bool isSelected = selectedAttachmentFlag == attach.Key;

                    string tooltip = ItemFlagTooltips.GetAttachmentFlagTooltip(attach.Key);

                    string hex = debugMode ? $"0x{attach.Value:X}" : "";
                    string nm = debugMode ? attach.Key : FormatFlagName(attach.Key);
                    GUIContent content = new GUIContent(
                        $"{nm} {hex}",
                        tooltip
                    );

                    bool newSelected = EditorGUILayout.ToggleLeft(content, isSelected);

                    if (newSelected && !isSelected)
                    {
                        newAttachment = attach.Key;
                    }
                }

                if (newAttachment != selectedAttachmentFlag)
                {
                    selectedAttachmentFlag = newAttachment;
                    EncodeFlags();
                }

                EditorGUILayout.EndVertical();
            }

            EditorGUILayout.EndFoldoutHeaderGroup();
        }

        // OVERLAPPING FLAGS SECTION - THE KEY FEATURE
        private void DrawOverlappingFlagsSection()
        {
            var bitsInfo = debugMode ? "(Same Bit, Different Meanings)" : "";
            showOverlappingFlags = EditorGUILayout.BeginFoldoutHeaderGroup(
                showOverlappingFlags, $"Context-Dependent Flags {bitsInfo}");

            if (showOverlappingFlags)
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);

                EditorGUILayout.HelpBox(
                    "These flags share the same bit position but have different meanings depending on item type.\n" +
                    "Toggling one enables ALL listed flags for that bit - use the correct one for your item type.",
                    MessageType.Warning);

                EditorGUILayout.Space(5);

                var groups = ItemFlagPropertyDecoder.GetOverlappingGroups();

                for (int i = 0; i < groups.Count; i++)
                {
                    var group = groups[i];
                    bool isActive = activeOverlappingGroups.Contains(i);

                    EditorGUILayout.BeginVertical(overlappingGroupStyle);

                    // Main toggle with bit value
                    EditorGUILayout.BeginHorizontal();

                    bool newActive = EditorGUILayout.Toggle(isActive, GUILayout.Width(20));

                    // Display bit value header
                    EditorGUILayout.LabelField($"Bit 0x{group.BitValue:X}", EditorStyles.boldLabel,
                        GUILayout.Width(120));

                    EditorGUILayout.EndHorizontal();

                    // Show all flag names with their context AND tooltips
                    EditorGUI.indentLevel++;
                    foreach (var flagName in group.FlagNames)
                    {
                        EditorGUILayout.BeginHorizontal();

                        // Get tooltip for this specific flag
                        string tooltip = ItemFlagTooltips.GetPropertyFlagTooltip(flagName);
                        string displayName = debugMode ? flagName : FormatFlagName(flagName);

                        // Flag name with tooltip
                        GUIContent flagContent = new GUIContent($"• {displayName}", tooltip);
                        EditorGUILayout.LabelField(flagContent, GUILayout.MinWidth(200));

                        // Context hint
                        if (group.ContextHints.TryGetValue(flagName, out string hint))
                        {
                            EditorGUILayout.LabelField($"→ {hint}", contextHintStyle, GUILayout.Width(150));
                        }

                        EditorGUILayout.EndHorizontal();
                    }

                    EditorGUI.indentLevel--;

                    EditorGUILayout.EndVertical();

                    // Handle toggle change
                    if (newActive != isActive)
                    {
                        if (newActive)
                            activeOverlappingGroups.Add(i);
                        else
                            activeOverlappingGroups.Remove(i);

                        EncodeFlags();
                    }

                    EditorGUILayout.Space(2);
                }

                EditorGUILayout.EndVertical();
            }

            EditorGUILayout.EndFoldoutHeaderGroup();
        }

        // UNIQUE FLAGS SECTION
        private void DrawUniqueFlagsSection()
        {
            var bitsInfo = debugMode ? "(Unique Bits)" : "";
            showUniqueFlags = EditorGUILayout.BeginFoldoutHeaderGroup(
                showUniqueFlags, $"Property Flags {bitsInfo}");

            if (showUniqueFlags)
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);

                var uniqueFlags = ItemFlagPropertyDecoder.GetUniquePropertyFlags();
                var categorized = CategorizeFlags(uniqueFlags);

                foreach (var category in categorized)
                {
                    if (category.Value.Count == 0) continue;

                    EditorGUILayout.LabelField(category.Key, EditorStyles.boldLabel);
                    EditorGUI.indentLevel++;

                    foreach (var flag in category.Value.OrderBy(x => x.Value))
                    {
                        bool isActive = activeUniqueFlags.Contains(flag.Key);

                        string tooltip = ItemFlagTooltips.GetPropertyFlagTooltip(flag.Key);
                        string hex = debugMode ? $"0x{flag.Value:X}" : "";
                        string nm = debugMode ? flag.Key : FormatFlagName(flag.Key);
                        GUIContent content = new GUIContent(
                            $"{nm} {hex}",
                            tooltip
                        );

                        bool newActive = EditorGUILayout.ToggleLeft(content, isActive);

                        if (newActive != isActive)
                        {
                            if (newActive)
                                activeUniqueFlags.Add(flag.Key);
                            else
                                activeUniqueFlags.Remove(flag.Key);

                            EncodeFlags();
                        }
                    }

                    EditorGUI.indentLevel--;
                    EditorGUILayout.Space(5);
                }

                EditorGUILayout.EndVertical();
            }

            EditorGUILayout.EndFoldoutHeaderGroup();
        }

        // PARSING (Decode from raw value)
        private void ParseCurrentFlags()
        {
            if (targetItem == null || string.IsNullOrWhiteSpace(targetItem.Flags))
            {
                selectedItemType = "";
                selectedAttachmentFlag = "";
                activeUniqueFlags.Clear();
                activeOverlappingGroups.Clear();
                lastParsedFlagsValue = "";
                return;
            }

            try
            {
                BigInteger value = ParseBigInteger(targetItem.Flags);
                lastParsedFlagsValue = targetItem.Flags;

                // Decode item type (bits 0-4)
                selectedItemType = ItemFlagPropertyDecoder.GetItemType(value);

                // Decode attachment flag (bits 8-11)
                selectedAttachmentFlag = ItemFlagPropertyDecoder.GetAttachmentFlag(value);

                // Decode all flags using new method
                var (uniqueFlags, overlappingGroups) = ItemFlagPropertyDecoder.DecodeAllFlags(value);
                activeUniqueFlags = new HashSet<string>(uniqueFlags);
                activeOverlappingGroups = overlappingGroups;

                Repaint();
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to parse flags: {ex.Message}");
            }
        }

        // ENCODING (Build raw value from UI state)
        private void EncodeFlags()
        {
            if (targetItem == null) return;

            try
            {
                BigInteger encodedValue = 0;

                // 1. ENCODE ITEM TYPE (bits 0-4)
                if (!string.IsNullOrEmpty(selectedItemType))
                {
                    var itemTypes = ItemFlagPropertyDecoder.GetItemTypes();
                    if (itemTypes.TryGetValue(selectedItemType, out BigInteger typeValue))
                    {
                        encodedValue |= (typeValue & 0x1F);
                    }
                }

                // 2. ENCODE ATTACHMENT FLAG (bits 8-11)
                if (!string.IsNullOrEmpty(selectedAttachmentFlag))
                {
                    var attachmentFlags = ItemFlagPropertyDecoder.GetAttachmentFlags();
                    if (attachmentFlags.TryGetValue(selectedAttachmentFlag, out BigInteger attachValue))
                    {
                        encodedValue |= (attachValue & 0x0F00);
                    }
                }

                // 3. ENCODE UNIQUE PROPERTY FLAGS
                var uniqueFlags = ItemFlagPropertyDecoder.GetUniquePropertyFlags();
                foreach (var activeFlagName in activeUniqueFlags)
                {
                    if (uniqueFlags.TryGetValue(activeFlagName, out BigInteger flagValue))
                    {
                        encodedValue |= flagValue;
                    }
                }

                // 4. ENCODE OVERLAPPING GROUPS
                var groups = ItemFlagPropertyDecoder.GetOverlappingGroups();
                foreach (int groupIndex in activeOverlappingGroups)
                {
                    if (groupIndex >= 0 && groupIndex < groups.Count)
                    {
                        encodedValue |= groups[groupIndex].BitValue;
                    }
                }

                // 5. WRITE BACK
                string newFlagsValue = FormatBigInteger(encodedValue);

                if (targetItem.Flags != newFlagsValue)
                {
                    Undo.RecordObject(targetItem, "Modify Item Flags");
                    targetItem.Flags = newFlagsValue;
                    lastParsedFlagsValue = newFlagsValue;
                    EditorUtility.SetDirty(targetItem);
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to encode flags: {ex.Message}");
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

        private Dictionary<string, Dictionary<string, BigInteger>> CategorizeFlags(
            Dictionary<string, BigInteger> flags)
        {
            var categories = new Dictionary<string, Dictionary<string, BigInteger>>
            {
                { "Weapon Usage", new Dictionary<string, BigInteger>() },
                { "Combat Modifiers", new Dictionary<string, BigInteger>() },
                { "Armor & Mesh Display", new Dictionary<string, BigInteger>() },
                { "Economy & Loot", new Dictionary<string, BigInteger>() },
                { "Mounted Combat", new Dictionary<string, BigInteger>() },
                { "Ranged & Firearms", new Dictionary<string, BigInteger>() },
                { "Polearm & Lance", new Dictionary<string, BigInteger>() },
                { "WSE2 Only", new Dictionary<string, BigInteger>() },
                { "Other", new Dictionary<string, BigInteger>() }
            };

            // WSE2-specific flags (check these FIRST to avoid false matches)
            var wse2Flags = new HashSet<string>
            {
                "itp_shield_no_parry",
                "itp_offset_mortschlag",
                "itp_covers_hands",
                "itp_crush_through_any_direction",
                "itp_offset_flip"
            };

            foreach (var flag in flags)
            {
                string key = flag.Key;

                // WSE2 specific - check exact matches first
                if (wse2Flags.Contains(key))
                {
                    categories["WSE2 Only"][key] = flag.Value;
                }
                // Weapon Usage (primary/secondary marking, two-handed, attachment)
                else if (key.Contains("two_handed") || key.Contains("primary") || key.Contains("secondary") ||
                         key.Contains("cant_use") || key.Contains("remove_item") || key.Contains("next_item_as_melee"))
                {
                    categories["Weapon Usage"][key] = flag.Value;
                }
                // Combat Modifiers (damage, blocking, knockdown)
                else if (key.Contains("no_parry") || key.Contains("crush_through") || key.Contains("penetrate") ||
                         key.Contains("bonus_against") || key.Contains("knock_down") || key.Contains("unbalanced") ||
                         key.Contains("wooden_attack") || key.Contains("wooden_parry") ||
                         key.Contains("penalty_with") ||
                         key.Contains("extra_penetration") || key.Contains("upper_stab"))
                {
                    categories["Combat Modifiers"][key] = flag.Value;
                }
                // Armor & Mesh Display
                else if (key.Contains("covers_") || key.Contains("force_show") || key.Contains("fit_to_head") ||
                         key.Contains("doesnt_cover"))
                {
                    categories["Armor & Mesh Display"][key] = flag.Value;
                }
                // Economy & Loot
                else if (key.Contains("unique") || key.Contains("always_loot") || key.Contains("merchandise") ||
                         key.Contains("food") || key.Contains("consumable") || key.Contains("no_pick_up") ||
                         key.Contains("default_ammo"))
                {
                    categories["Economy & Loot"][key] = flag.Value;
                }
                // Mounted Combat
                else if (key.Contains("horseback") || key.Contains("_mounted") ||
                         key.Contains("disable_agent_sounds"))
                {
                    categories["Mounted Combat"][key] = flag.Value;
                }
                // Ranged & Firearms
                else if (key.Contains("reload") || key.Contains("bayonet") || key.Contains("offset_musket") ||
                         key.Contains("ignore_gravity") || key.Contains("ignore_friction") || key.Contains("no_blur"))
                {
                    categories["Ranged & Firearms"][key] = flag.Value;
                }
                // Polearm & Lance specific
                else if (key.Contains("couchable") || key.Contains("offset_lance") || key.Contains("is_pike"))
                {
                    categories["Polearm & Lance"][key] = flag.Value;
                }
                // Other / Misc
                else
                {
                    categories["Other"][key] = flag.Value;
                }
            }

            return categories;
        }
    }
}
