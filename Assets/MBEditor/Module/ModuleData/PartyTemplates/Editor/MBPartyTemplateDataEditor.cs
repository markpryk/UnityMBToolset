using UnityEngine;
using UnityEditor;
using MountAndBlade.Data;
using MountAndBladeTools;
using System.Numerics;
using System.Collections.Generic;
using System.Linq;

namespace MountAndBlade.Editor
{
    [CustomEditor(typeof(MBPartyTemplateData))]
    public class MBPartyTemplateDataEditor_Advanced : UnityEditor.Editor
    {
        private bool showBasicInfo = true;
        private bool showFlagsEditor = true;
        private bool showPersonalityEditor = true;
        private bool showTroopStacks = true;
        private bool showRawData = false;
        private bool showValidation = true;
        
        // UI state for flags
        private int selectedIconId = 0;
        private Dictionary<string, bool> selectedFlags = new Dictionary<string, bool>();
        
        // UI state for personality
        private int selectedCourage = 8;  // Neutral default
        private int selectedAggressiveness = 8;  // Neutral default
        private bool isBandit = false;
        private int personalityPreset = 0;  // 0=custom, 1=soldier, 2=merchant, 3=escorted, 4=bandit
        
        // UI state for member flags per stack
        private Dictionary<int, bool> stackIsPrisoner = new Dictionary<int, bool>();
        
        private void OnEnable()
        {
            LoadCurrentData();
        }
        
        public override void OnInspectorGUI()
        {
            var data = (MBPartyTemplateData)target;
            
            EditorGUI.BeginChangeCheck();
            
            // Title
            EditorGUILayout.Space(5);
            GUIStyle titleStyle = new GUIStyle(EditorStyles.boldLabel);
            titleStyle.fontSize = 14;
            titleStyle.alignment = TextAnchor.MiddleCenter;
            EditorGUILayout.LabelField("Party Template Editor", titleStyle);
            EditorGUILayout.Space(10);
            
            // Basic Information
            DrawBasicInfo(data);
            
            // Flags Editor (Visual)
            DrawFlagsEditor(data);
            
            // Personality Editor (Visual)
            DrawPersonalityEditor(data);
            
            // Troop Stacks (Enhanced)
            DrawTroopStacksEditor(data);
            
            // Raw Data View (for reference/debug)
            DrawRawDataView(data);
            
            // Validation
            DrawValidation(data);
            
            // Action Buttons
            DrawActionButtons(data);
            
            if (EditorGUI.EndChangeCheck())
            {
                EditorUtility.SetDirty(target);
            }
        }
        
        private void DrawBasicInfo(MBPartyTemplateData data)
        {
            showBasicInfo = EditorGUILayout.BeginFoldoutHeaderGroup(showBasicInfo, "Basic Information");
            if (showBasicInfo)
            {
                EditorGUI.indentLevel++;
                
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                
                data.ID = EditorGUILayout.TextField(
                    new GUIContent("Template ID", "Unique identifier (prefix pt_ added automatically in Module System)"),
                    data.ID);
                
                data.TemplateName = EditorGUILayout.TextField(
                    new GUIContent("Template Name", "Display name shown in-game"),
                    data.TemplateName);
                
                data.Menu = EditorGUILayout.TextField(
                    new GUIContent("Menu ID", "Menu to show when encountered (0 = default encounter system)"),
                    data.Menu);
                
                // Menu helper
                EditorGUI.indentLevel++;
                if (string.IsNullOrEmpty(data.Menu) || data.Menu == "0")
                {
                    EditorGUILayout.LabelField("Using default party encounter system", EditorStyles.miniLabel);
                }
                EditorGUI.indentLevel--;
                
                data.Faction = EditorGUILayout.TextField(
                    new GUIContent("Faction", "Faction ID (e.g., fac_kingdom_1, fac_outlaws)"),
                    data.Faction);
                
                EditorGUILayout.EndVertical();
                
                EditorGUI.indentLevel--;
                EditorGUILayout.Space(5);
            }
            EditorGUILayout.EndFoldoutHeaderGroup();
        }
        
        private void DrawFlagsEditor(MBPartyTemplateData data)
        {
            showFlagsEditor = EditorGUILayout.BeginFoldoutHeaderGroup(showFlagsEditor, "Party Flags (Visual Editor)");
            if (showFlagsEditor)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                
                // Icon ID selector
                EditorGUILayout.LabelField("Map Icon", EditorStyles.boldLabel);
                selectedIconId = EditorGUILayout.IntSlider(
                    new GUIContent("Icon ID", "Map icon (0-255). Maps to your icon assets."),
                    selectedIconId, 0, 255);
                
                EditorGUILayout.Space(10);
                
                // Flag checkboxes
                EditorGUILayout.LabelField("Party Flags", EditorStyles.boldLabel);
                
                // Initialize flags dictionary if needed
                if (selectedFlags.Count == 0)
                {
                    var allFlags = PartyFlagsDecoder.GetAllFlags();
                    foreach (var flag in allFlags)
                    {
                        if (!selectedFlags.ContainsKey(flag.Key))
                            selectedFlags[flag.Key] = false;
                    }
                }
                
                // Basic flags
                EditorGUILayout.LabelField("Basic Properties:", EditorStyles.miniBoldLabel);
                DrawFlagCheckbox("pf_disabled", "Disabled - Party is inactive");
                DrawFlagCheckbox("pf_is_ship", "Is Ship - Party is a ship");
                DrawFlagCheckbox("pf_is_static", "Is Static - Party doesn't move");
                
                EditorGUILayout.Space(5);
                
                // Label size (radio buttons)
                EditorGUILayout.LabelField("Label Size:", EditorStyles.miniBoldLabel);
                EditorGUI.indentLevel++;
                
                int labelSize = 0; // 0=small, 1=medium, 2=large
                if (selectedFlags.ContainsKey("pf_label_large") && selectedFlags["pf_label_large"])
                    labelSize = 2;
                else if (selectedFlags.ContainsKey("pf_label_medium") && selectedFlags["pf_label_medium"])
                    labelSize = 1;
                
                int newLabelSize = GUILayout.SelectionGrid(labelSize, new string[] { "Small", "Medium", "Large" }, 3);
                
                // Update flags based on selection
                if (selectedFlags.ContainsKey("pf_label_small"))
                    selectedFlags["pf_label_small"] = (newLabelSize == 0);
                if (selectedFlags.ContainsKey("pf_label_medium"))
                    selectedFlags["pf_label_medium"] = (newLabelSize == 1);
                if (selectedFlags.ContainsKey("pf_label_large"))
                    selectedFlags["pf_label_large"] = (newLabelSize == 2);
                
                EditorGUI.indentLevel--;
                EditorGUILayout.Space(5);
                
                // Behavior flags
                EditorGUILayout.LabelField("Behavior Flags:", EditorStyles.miniBoldLabel);
                DrawFlagCheckbox("pf_always_visible", "Always Visible - Always shown on map");
                DrawFlagCheckbox("pf_default_behavior", "Default Behavior - Use default AI");
                DrawFlagCheckbox("pf_auto_remove_in_town", "Auto Remove in Town - Removed when entering settlement");
                DrawFlagCheckbox("pf_quest_party", "Quest Party - Party is quest-related");
                DrawFlagCheckbox("pf_no_label", "No Label - Don't show name label");
                DrawFlagCheckbox("pf_limit_members", "Limit Members - Enforce member limits");
                DrawFlagCheckbox("pf_hide_defenders", "Hide Defenders - Don't show defender count");
                DrawFlagCheckbox("pf_show_faction", "Show Faction - Display faction banner");
                
                EditorGUILayout.Space(5);
                
                // Advanced flags
                EditorGUILayout.LabelField("Advanced Flags:", EditorStyles.miniBoldLabel);
                DrawFlagCheckbox("pf_dont_attack_civilians", "Don't Attack Civilians");
                DrawFlagCheckbox("pf_civilian", "Civilian - Party is civilian");
                
                EditorGUILayout.Space(10);
                
                // Auto-encode to data
                if (GUILayout.Button("Apply Flags to Data"))
                {
                    EncodeAndApplyFlags(data);
                }
                
                // Show preview
                EditorGUILayout.Space(5);
                EditorGUI.indentLevel++;
                EditorGUILayout.LabelField("Preview:", GetFlagsPreview(), EditorStyles.wordWrappedMiniLabel);
                EditorGUI.indentLevel--;
                
                EditorGUILayout.EndVertical();
                EditorGUI.indentLevel--;
                EditorGUILayout.Space(5);
            }
            EditorGUILayout.EndFoldoutHeaderGroup();
        }
        
        private void DrawPersonalityEditor(MBPartyTemplateData data)
        {
            showPersonalityEditor = EditorGUILayout.BeginFoldoutHeaderGroup(showPersonalityEditor, "Personality (Visual Editor)");
            if (showPersonalityEditor)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                
                // Preset selector
                EditorGUILayout.LabelField("Personality Presets", EditorStyles.boldLabel);
                
                string[] presetNames = new string[] { "Custom", "Soldier", "Merchant", "Escorted Merchant", "Bandit" };
                int newPreset = GUILayout.SelectionGrid(personalityPreset, presetNames, 5);
                
                if (newPreset != personalityPreset)
                {
                    personalityPreset = newPreset;
                    ApplyPersonalityPreset(personalityPreset);
                }
                
                EditorGUILayout.Space(10);
                
                // Custom personality controls
                EditorGUILayout.LabelField("Custom Settings", EditorStyles.boldLabel);
                EditorGUILayout.HelpBox(
                    "Courage: How willing to fight larger enemies (4=cowardly, 8=neutral, 15=brave)\n" +
                    "Aggressiveness: How likely to attack (0=never attacks, 8=neutral, 15=very aggressive)",
                    MessageType.Info);
                
                EditorGUI.BeginChangeCheck();
                
                // Courage slider
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Courage", GUILayout.Width(100));
                selectedCourage = EditorGUILayout.IntSlider(selectedCourage, 4, 15);
                EditorGUILayout.LabelField(GetCourageDescription(selectedCourage), GUILayout.Width(100));
                EditorGUILayout.EndHorizontal();
                
                // Aggressiveness slider
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Aggressiveness", GUILayout.Width(100));
                selectedAggressiveness = EditorGUILayout.IntSlider(selectedAggressiveness, 0, 15);
                EditorGUILayout.LabelField(GetAggressivenessDescription(selectedAggressiveness), GUILayout.Width(100));
                EditorGUILayout.EndHorizontal();
                
                // Banditness flag
                isBandit = EditorGUILayout.Toggle(
                    new GUIContent("Banditness", "Party will hunt prey carrying gold/goods"),
                    isBandit);
                
                if (EditorGUI.EndChangeCheck() && personalityPreset != 0)
                {
                    personalityPreset = 0; // Switch to custom if manual changes
                }
                
                EditorGUILayout.Space(10);
                
                // Auto-encode to data
                if (GUILayout.Button("Apply Personality to Data"))
                {
                    EncodeAndApplyPersonality(data);
                }
                
                // Show preview
                EditorGUILayout.Space(5);
                EditorGUI.indentLevel++;
                EditorGUILayout.LabelField("Preview:", GetPersonalityPreview(), EditorStyles.wordWrappedMiniLabel);
                EditorGUI.indentLevel--;
                
                EditorGUILayout.EndVertical();
                EditorGUI.indentLevel--;
                EditorGUILayout.Space(5);
            }
            EditorGUILayout.EndFoldoutHeaderGroup();
        }
        
        private void DrawTroopStacksEditor(MBPartyTemplateData data)
        {
            showTroopStacks = EditorGUILayout.BeginFoldoutHeaderGroup(showTroopStacks, "Troop Stacks (Templates)");
            if (showTroopStacks)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                
                // Stack count info
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField($"Stacks: {data.TemplateStacks.Count} / {MBPartyTemplateData.MAX_TROOP_STACKS}", EditorStyles.boldLabel);
                
                if (data.TemplateStacks.Count >= MBPartyTemplateData.MAX_TROOP_STACKS)
                {
                    GUIStyle warningStyle = new GUIStyle(EditorStyles.label);
                    warningStyle.normal.textColor = Color.yellow;
                    EditorGUILayout.LabelField("(At Maximum)", warningStyle);
                }
                
                EditorGUILayout.EndHorizontal();
                
                EditorGUILayout.HelpBox(
                    "Templates define min/max spawn ranges. Actual parties spawned from this template will have random troop counts within these ranges.",
                    MessageType.Info);
                
                EditorGUILayout.Space(10);
                
                // Draw each stack
                for (int i = 0; i < data.TemplateStacks.Count; i++)
                {
                    DrawTroopStackVisual(data, data.TemplateStacks[i], i);
                    EditorGUILayout.Space(5);
                }
                
                EditorGUILayout.Space(10);
                
                // Add stack button
                EditorGUILayout.BeginHorizontal();
                GUI.enabled = data.TemplateStacks.Count < MBPartyTemplateData.MAX_TROOP_STACKS;
                if (GUILayout.Button("+ Add Troop Stack", GUILayout.Height(30)))
                {
                    data.TemplateStacks.Add(new MBTemplateStack 
                    { 
                        MinCount = 1, 
                        MaxCount = 5,
                        StackFlags = "0"
                    });
                    stackIsPrisoner[data.TemplateStacks.Count - 1] = false;
                }
                GUI.enabled = true;
                EditorGUILayout.EndHorizontal();
                
                if (data.TemplateStacks.Count >= MBPartyTemplateData.MAX_TROOP_STACKS)
                {
                    EditorGUILayout.HelpBox("Maximum 6 stacks (engine limitation)", MessageType.Warning);
                }
                
                EditorGUILayout.EndVertical();
                EditorGUI.indentLevel--;
                EditorGUILayout.Space(5);
            }
            EditorGUILayout.EndFoldoutHeaderGroup();
        }
        
        private void DrawTroopStackVisual(MBPartyTemplateData data, MBTemplateStack stack, int index)
        {
            EditorGUILayout.BeginVertical(GUI.skin.box);
            
            // Header
            EditorGUILayout.BeginHorizontal();
            GUIStyle headerStyle = new GUIStyle(EditorStyles.boldLabel);
            headerStyle.normal.textColor = new Color(0.3f, 0.7f, 1f);
            EditorGUILayout.LabelField($"Stack {index + 1}", headerStyle);
            
            // Delete button
            GUIStyle deleteStyle = new GUIStyle(GUI.skin.button);
            deleteStyle.normal.textColor = Color.red;
            if (GUILayout.Button("✖ Remove", deleteStyle, GUILayout.Width(80)))
            {
                data.TemplateStacks.RemoveAt(index);
                if (stackIsPrisoner.ContainsKey(index))
                    stackIsPrisoner.Remove(index);
                return;
            }
            EditorGUILayout.EndHorizontal();
            
            EditorGUI.indentLevel++;
            
            // Troop ID
            stack.TroopID = EditorGUILayout.TextField(
                new GUIContent("Troop ID", "Troop type (e.g., trp_farmer, trp_bandit)"),
                stack.TroopID);
            
            // Min/Max counts with visual slider
            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField("Spawn Count Range", EditorStyles.miniBoldLabel);
            
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Min", GUILayout.Width(30));
            stack.MinCount = EditorGUILayout.IntField(stack.MinCount, GUILayout.Width(50));
            // EditorGUILayout.MinMaxSlider((float)stack.MinCount, (float)stack.MaxCount, 0, 100);
            EditorGUILayout.LabelField("Max", GUILayout.Width(30));
            stack.MaxCount = EditorGUILayout.IntField(stack.MaxCount, GUILayout.Width(50));
            EditorGUILayout.EndHorizontal();
            
            // Visual representation of range
            EditorGUI.indentLevel++;
            EditorGUILayout.LabelField($"Spawns: {stack.MinCount} to {stack.MaxCount} troops", EditorStyles.miniLabel);
            EditorGUI.indentLevel--;
            
            // Validation warnings
            if (stack.MinCount < 0)
            {
                EditorGUILayout.HelpBox("Min count cannot be negative!", MessageType.Error);
            }
            if (stack.MaxCount < stack.MinCount)
            {
                EditorGUILayout.HelpBox("Max must be >= Min!", MessageType.Error);
            }
            
            EditorGUILayout.Space(5);
            
            // Member flags (visual checkbox for prisoner)
            EditorGUILayout.LabelField("Member Flags", EditorStyles.miniBoldLabel);
            
            // Initialize prisoner state if needed
            if (!stackIsPrisoner.ContainsKey(index))
            {
                stackIsPrisoner[index] = PartyDecoder.IsPrisoner(ParseMemberFlags(stack.StackFlags));
            }
            
            EditorGUI.BeginChangeCheck();
            bool isPrisoner = EditorGUILayout.Toggle(
                new GUIContent("Is Prisoner", "This troop stack consists of prisoners"),
                stackIsPrisoner[index]);
            
            if (EditorGUI.EndChangeCheck())
            {
                stackIsPrisoner[index] = isPrisoner;
                EncodeMemberFlags(stack, index);
            }
            
            EditorGUI.indentLevel--;
            EditorGUILayout.EndVertical();
        }
        
        private void DrawRawDataView(MBPartyTemplateData data)
        {
            showRawData = EditorGUILayout.BeginFoldoutHeaderGroup(showRawData, "Raw Data (Read-Only Reference)");
            if (showRawData)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                
                EditorGUILayout.HelpBox(
                    "This shows the actual encoded values that will be exported to Module System. " +
                    "Use the visual editors above to modify these values.",
                    MessageType.Info);
                
                GUI.enabled = false;
                
                EditorGUILayout.TextField("Flags (Encoded)", data.Flags);
                
                // Decode and show
                if (!string.IsNullOrEmpty(data.Flags))
                {
                    try
                    {
                        var decoded = PartyDecoder.DecodeFlags(data.Flags);
                        EditorGUI.indentLevel++;
                        EditorGUILayout.LabelField("Decoded:", decoded.ToString(), EditorStyles.wordWrappedLabel);
                        EditorGUI.indentLevel--;
                    }
                    catch { }
                }
                
                EditorGUILayout.TextField("Personality (Encoded)", data.Personality);
                
                // Decode and show
                if (!string.IsNullOrEmpty(data.Personality))
                {
                    try
                    {
                        var decoded = PartyDecoder.DecodePersonality(data.Personality);
                        EditorGUI.indentLevel++;
                        EditorGUILayout.LabelField("Decoded:", decoded.ToString(), EditorStyles.wordWrappedLabel);
                        EditorGUI.indentLevel--;
                    }
                    catch { }
                }
                
                GUI.enabled = true;
                
                EditorGUILayout.EndVertical();
                EditorGUI.indentLevel--;
                EditorGUILayout.Space(5);
            }
            EditorGUILayout.EndFoldoutHeaderGroup();
        }
        
        private void DrawValidation(MBPartyTemplateData data)
        {
            showValidation = EditorGUILayout.BeginFoldoutHeaderGroup(showValidation, "Validation");
            if (showValidation)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                
                List<string> errors = new List<string>();
                List<string> warnings = new List<string>();
                
                // Validate template
                if (string.IsNullOrEmpty(data.ID))
                    errors.Add("Template ID is required");
                
                if (string.IsNullOrEmpty(data.TemplateName))
                    warnings.Add("Template name is recommended");
                
                if (data.TemplateStacks.Count == 0)
                    warnings.Add("No troop stacks defined");
                
                if (data.TemplateStacks.Count > MBPartyTemplateData.MAX_TROOP_STACKS)
                    errors.Add($"Too many stacks! Maximum is {MBPartyTemplateData.MAX_TROOP_STACKS}");
                
                // Validate each stack
                for (int i = 0; i < data.TemplateStacks.Count; i++)
                {
                    var stack = data.TemplateStacks[i];
                    
                    if (string.IsNullOrEmpty(stack.TroopID))
                        errors.Add($"Stack {i + 1}: Missing troop ID");
                    
                    if (stack.MinCount < 0)
                        errors.Add($"Stack {i + 1}: Min count cannot be negative");
                    
                    if (stack.MaxCount < stack.MinCount)
                        errors.Add($"Stack {i + 1}: Max count must be >= min count");
                }
                
                // Display results
                if (errors.Count > 0)
                {
                    foreach (var error in errors)
                        EditorGUILayout.HelpBox(error, MessageType.Error);
                }
                
                if (warnings.Count > 0)
                {
                    foreach (var warning in warnings)
                        EditorGUILayout.HelpBox(warning, MessageType.Warning);
                }
                
                if (errors.Count == 0 && warnings.Count == 0)
                {
                    EditorGUILayout.HelpBox("✓ Template is valid and ready to export", MessageType.Info);
                }
                
                EditorGUILayout.EndVertical();
                EditorGUI.indentLevel--;
            }
            EditorGUILayout.EndFoldoutHeaderGroup();
        }
        
        private void DrawActionButtons(MBPartyTemplateData data)
        {
            EditorGUILayout.Space(10);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            
            EditorGUILayout.LabelField("Quick Actions", EditorStyles.boldLabel);
            
            EditorGUILayout.BeginHorizontal();
            
            if (GUILayout.Button("Apply All Settings", GUILayout.Height(30)))
            {
                EncodeAndApplyFlags(data);
                EncodeAndApplyPersonality(data);
                for (int i = 0; i < data.TemplateStacks.Count; i++)
                {
                    EncodeMemberFlags(data.TemplateStacks[i], i);
                }
                EditorUtility.SetDirty(data);
                Debug.Log("All settings applied to data!");
            }
            
            if (GUILayout.Button("Reload from Data", GUILayout.Height(30)))
            {
                LoadCurrentData();
                Debug.Log("Reloaded settings from data!");
            }
            
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.EndVertical();
        }
        
        // Helper Methods
        
        private void DrawFlagCheckbox(string flagKey, string description)
        {
            if (!selectedFlags.ContainsKey(flagKey))
                selectedFlags[flagKey] = false;
            
            selectedFlags[flagKey] = EditorGUILayout.Toggle(
                new GUIContent(FormatFlagName(flagKey), description),
                selectedFlags[flagKey]);
        }
        
        private string FormatFlagName(string flagKey)
        {
            // Convert pf_is_ship to "Is Ship"
            string name = flagKey.Replace("pf_", "").Replace("_", " ");
            return System.Globalization.CultureInfo.CurrentCulture.TextInfo.ToTitleCase(name);
        }
        
        private void EncodeAndApplyFlags(MBPartyTemplateData data)
        {
            try
            {
                // Get active flags
                List<string> activeFlags = selectedFlags.Where(kvp => kvp.Value).Select(kvp => kvp.Key).ToList();
                
                // Combine icon + flags
                BigInteger encoded = PartyFlagsDecoder.CombineIconAndFlags(selectedIconId, activeFlags);
                
                // Store as hex string
                data.Flags = "0x" + encoded.ToString("X");
                
                Debug.Log($"Flags encoded: {data.Flags}");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Failed to encode flags: {e.Message}");
            }
        }
        
        private void EncodeAndApplyPersonality(MBPartyTemplateData data)
        {
            try
            {
                int encoded = PartyPersonalityDecoder.EncodePersonality(selectedCourage, selectedAggressiveness, isBandit);
                data.Personality = "0x" + encoded.ToString("X");
                
                Debug.Log($"Personality encoded: {data.Personality}");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Failed to encode personality: {e.Message}");
            }
        }
        
        private void EncodeMemberFlags(MBTemplateStack stack, int index)
        {
            try
            {
                bool isPrisoner = stackIsPrisoner.ContainsKey(index) && stackIsPrisoner[index];
                
                if (isPrisoner)
                {
                    int encoded = PartyMemberFlagsDecoder.CreatePrisonerFlag();
                    stack.StackFlags = encoded.ToString();
                }
                else
                {
                    stack.StackFlags = "0";
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Failed to encode member flags for stack {index}: {e.Message}");
            }
        }
        
        private void LoadCurrentData()
        {
            var data = (MBPartyTemplateData)target;
            
            // Load flags
            if (!string.IsNullOrEmpty(data.Flags))
            {
                try
                {
                    var decoded = PartyDecoder.DecodeFlags(data.Flags);
                    selectedIconId = decoded.IconId;
                    
                    // Reset all flags
                    var allFlags = PartyFlagsDecoder.GetAllFlags();
                    foreach (var flag in allFlags.Keys)
                    {
                        selectedFlags[flag] = decoded.ActiveFlags.Contains(flag);
                    }
                }
                catch (System.Exception e)
                {
                    Debug.LogWarning($"Failed to decode flags: {e.Message}");
                }
            }
            
            // Load personality
            if (!string.IsNullOrEmpty(data.Personality))
            {
                try
                {
                    var decoded = PartyDecoder.DecodePersonality(data.Personality);
                    selectedCourage = decoded.CourageValue;
                    selectedAggressiveness = decoded.AggressivenessLevel;
                    isBandit = decoded.IsBandit;
                    
                    // Detect preset
                    personalityPreset = DetectPersonalityPreset(decoded);
                }
                catch (System.Exception e)
                {
                    Debug.LogWarning($"Failed to decode personality: {e.Message}");
                }
            }
            
            // Load member flags for each stack
            stackIsPrisoner.Clear();
            for (int i = 0; i < data.TemplateStacks.Count; i++)
            {
                int flags = ParseMemberFlags(data.TemplateStacks[i].StackFlags);
                stackIsPrisoner[i] = PartyDecoder.IsPrisoner(flags);
            }
        }
        
        private int ParseMemberFlags(string flagsValue)
        {
            if (string.IsNullOrEmpty(flagsValue))
                return 0;
            
            try
            {
                if (flagsValue.StartsWith("0x"))
                    return System.Convert.ToInt32(flagsValue, 16);
                return int.Parse(flagsValue);
            }
            catch
            {
                return 0;
            }
        }
        
        private void ApplyPersonalityPreset(int preset)
        {
            switch (preset)
            {
                case 1: // Soldier
                    selectedCourage = 9;
                    selectedAggressiveness = 8;
                    isBandit = false;
                    break;
                case 2: // Merchant
                    selectedCourage = 7;
                    selectedAggressiveness = 0;
                    isBandit = false;
                    break;
                case 3: // Escorted Merchant
                    selectedCourage = 11;
                    selectedAggressiveness = 0;
                    isBandit = false;
                    break;
                case 4: // Bandit
                    selectedCourage = 8;
                    selectedAggressiveness = 3;
                    isBandit = true;
                    break;
            }
        }
        
        private int DetectPersonalityPreset(PersonalityResult personality)
        {
            if (!string.IsNullOrEmpty(personality.PresetName))
            {
                if (personality.PresetName == "soldier_personality") return 1;
                if (personality.PresetName == "merchant_personality") return 2;
                if (personality.PresetName == "escorted_merchant_personality") return 3;
                if (personality.PresetName == "bandit_personality") return 4;
            }
            return 0; // Custom
        }
        
        private string GetCourageDescription(int courage)
        {
            if (courage < 6) return "Cowardly";
            if (courage < 8) return "Low";
            if (courage == 8) return "Neutral";
            if (courage < 12) return "High";
            return "Brave";
        }
        
        private string GetAggressivenessDescription(int aggressiveness)
        {
            if (aggressiveness == 0) return "Passive";
            if (aggressiveness < 5) return "Low";
            if (aggressiveness < 8) return "Medium";
            if (aggressiveness == 8) return "Neutral";
            if (aggressiveness < 12) return "High";
            return "Aggressive";
        }
        
        private string GetFlagsPreview()
        {
            List<string> parts = new List<string> { $"icon_{selectedIconId}" };
            parts.AddRange(selectedFlags.Where(kvp => kvp.Value).Select(kvp => kvp.Key));
            return string.Join(" | ", parts);
        }
        
        private string GetPersonalityPreview()
        {
            string[] presetNames = new string[] { "Custom", "soldier_personality", "merchant_personality", "escorted_merchant_personality", "bandit_personality" };
            
            if (personalityPreset > 0)
                return presetNames[personalityPreset];
            
            List<string> parts = new List<string>();
            parts.Add($"courage_{selectedCourage}");
            parts.Add($"aggressiveness_{selectedAggressiveness}");
            if (isBandit) parts.Add("banditness");
            
            return string.Join(" | ", parts);
        }
    }
}