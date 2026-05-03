using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace MountAndBlade.Data.Editor
{
    /// <summary>
    /// Custom editor for MBScenePropData with visual flag editing.
    /// Fully integrated with ScenePropFlagsDecoder.
    /// </summary>
    [CustomEditor(typeof(MBScenePropData))]
    public class MBScenePropEditor : UnityEditor.Editor
    {
        private MBScenePropData propData;
        private ScenePropFlagsData decodedFlags;
        
        // Foldout states
        private bool showIdentification = true;
        private bool showMeshes = true;
        private bool showType = true;
        private bool showPropertyFlags = true;
        private bool showDestructible = true;
        private bool showUseable = true;
        private bool showTriggers = true;
        private bool showRawData = false;
        
        // Flag modification tracking
        private bool flagsModified = false;
        private string modifiedType;
        private HashSet<string> modifiedPropertyFlags;
        private int modifiedHitPoints;
        private int modifiedUseTime;
        
        // GUI Styles
        private GUIStyle summaryStyle;
        private GUIStyle headerStyle;
        private GUIStyle flagButtonStyle;
        private GUIStyle validationErrorStyle;
        private GUIStyle validationWarningStyle;
        
        private void OnEnable()
        {
            propData = (MBScenePropData)target;
            DecodeFlags();
        }
        
        private void InitializeStyles()
        {
            if (summaryStyle == null)
            {
                summaryStyle = new GUIStyle(EditorStyles.helpBox);
                summaryStyle.fontSize = 11;
                summaryStyle.padding = new RectOffset(10, 10, 10, 10);
                
                headerStyle = new GUIStyle(EditorStyles.boldLabel);
                headerStyle.fontSize = 12;
                
                flagButtonStyle = new GUIStyle(GUI.skin.button);
                flagButtonStyle.alignment = TextAnchor.MiddleLeft;
                
                validationErrorStyle = new GUIStyle(EditorStyles.helpBox);
                validationErrorStyle.normal.textColor = new Color(0.8f, 0.2f, 0.2f);
                
                validationWarningStyle = new GUIStyle(EditorStyles.helpBox);
                validationWarningStyle.normal.textColor = new Color(0.8f, 0.6f, 0.2f);
            }
        }
        
        public override void OnInspectorGUI()
        {
            InitializeStyles();
            
            serializedObject.Update();
            
            EditorGUILayout.Space(5);
            DrawSummaryBadge();
            
            EditorGUILayout.Space(10);
            DrawIdentificationSection();
            
            EditorGUILayout.Space(5);
            DrawMeshesSection();
            
            EditorGUILayout.Space(5);
            DrawTypeSection();
            
            EditorGUILayout.Space(5);
            DrawPropertyFlagsSection();
            
            EditorGUILayout.Space(5);
            DrawDestructibleSection();
            
            EditorGUILayout.Space(5);
            DrawUseableSection();
            
            EditorGUILayout.Space(5);
            DrawTriggersSection();
            
            EditorGUILayout.Space(5);
            DrawValidationSection();
            
            EditorGUILayout.Space(5);
            DrawRawDataSection();
            
            // Apply flag modifications
            if (flagsModified && !Application.isPlaying)
            {
                ApplyFlagModifications();
            }
            
            serializedObject.ApplyModifiedProperties();
        }
        
        private void DecodeFlags()
        {
            try
            {
                if (string.IsNullOrEmpty(propData.Flags))
                {
                    Debug.LogWarning($"[DecodeFlags] Flags field is empty for {propData.PropID}, setting to '0'");
                    propData.Flags = "0";
                }
                
                Debug.Log($"[DecodeFlags] Decoding flags for {propData.PropID}:");
                Debug.Log($"  Raw Flags: {propData.Flags}");
                
                BigInteger flags = BigInteger.Parse(propData.Flags);
                decodedFlags = ScenePropFlagsDecoder.DecodeComplete(flags);
                
                Debug.Log($"  Decoded Type: '{decodedFlags.Type}'");
                Debug.Log($"  Decoded Property Flags: {string.Join(", ", decodedFlags.PropertyFlags)}");
                Debug.Log($"  Decoded Hit Points: {decodedFlags.HitPoints}");
                Debug.Log($"  Decoded Use Time: {decodedFlags.UseTime}");
                
                // Initialize modification tracking
                modifiedType = propData.TypeName;
                modifiedPropertyFlags = new HashSet<string>(decodedFlags.PropertyFlags);
                modifiedHitPoints = propData.HitPoints;
                modifiedUseTime = decodedFlags.UseTime;
                flagsModified = false;
                
                Debug.Log($"[DecodeFlags] Initialized modifiedType to: '{modifiedType}'");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Failed to decode flags for {propData.PropID}: {e.Message}");
                Debug.LogException(e);
                decodedFlags = new ScenePropFlagsData();
            }
        }
        
        private void ApplyFlagModifications()
        {
            try
            {
                // Debug log what we're encoding
                Debug.Log($"[ApplyFlagModifications] Encoding flags:");
                Debug.Log($"  Type: '{modifiedType}'");
                Debug.Log($"  Property Flags: {string.Join(", ", modifiedPropertyFlags)}");
                Debug.Log($"  Hit Points: {modifiedHitPoints}");
                Debug.Log($"  Use Time: {modifiedUseTime}");
                
                // Encode new flags
                BigInteger newFlags = ScenePropFlagsDecoder.EncodeFlags(
                    modifiedType,
                    modifiedPropertyFlags.ToList(),
                    modifiedHitPoints,
                    modifiedUseTime
                );
                
                Debug.Log($"  Encoded Flags: {newFlags} (0x{newFlags:X})");
                
                // Update ScriptableObject
                Undo.RecordObject(propData, "Modify Scene Prop Flags");
                propData.Flags = newFlags.ToString();
                
                // Update metadata
                propData.TypeName = modifiedType;
                propData.HitPoints = modifiedHitPoints;
                propData.HitPoints = modifiedHitPoints;
                propData.UseTime = modifiedUseTime;
                
                Debug.Log($"  Updated TypeName to: '{propData.TypeName}'");
                Debug.Log($"  Updated HitPoints to: {propData.HitPoints}");
                Debug.Log($"  Updated UseTime to: {propData.UseTime}");
                
                // Decode again to refresh display
                DecodeFlags();
                
                EditorUtility.SetDirty(propData);
                flagsModified = false;
                
                Debug.Log($"[ApplyFlagModifications] Complete - Asset marked dirty");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Failed to apply flag modifications: {e.Message}");
                Debug.LogException(e);
            }
        }
        
        // SUMMARY BADGE
        
        private void DrawSummaryBadge()
        {
            EditorGUILayout.BeginVertical(summaryStyle);
            
            // Title with icon
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("📦", GUILayout.Width(20));
            EditorGUILayout.LabelField(propData.PropID, EditorStyles.boldLabel);
            EditorGUILayout.EndHorizontal();
            
            // Category and summary
            string category = GetCategory();
            string summary = decodedFlags.GetSummary();
            
            EditorGUILayout.LabelField($"Category: {category}");
            
            if (!string.IsNullOrEmpty(summary))
            {
                EditorGUILayout.LabelField(summary, EditorStyles.wordWrappedMiniLabel);
            }
            
            // Quick info badges
            EditorGUILayout.BeginHorizontal();
            
            if (decodedFlags.IsDestructible)
                DrawBadge($"💔 {decodedFlags.HitPoints} HP", new Color(0.8f, 0.3f, 0.3f));
            
            if (decodedFlags.IsMoveable)
                DrawBadge("🚚 Moveable", new Color(0.3f, 0.6f, 0.8f));
            
            if (decodedFlags.IsUseable)
                DrawBadge($"⏱️ {decodedFlags.UseTime}s Use", new Color(0.6f, 0.8f, 0.3f));
            
            if (decodedFlags.IsInvisible)
                DrawBadge("👻 Invisible", new Color(0.5f, 0.5f, 0.5f));
            
            if (!string.IsNullOrEmpty(propData.Triggers))
                DrawBadge("⚡ Has Triggers", new Color(0.8f, 0.6f, 0.2f));
            
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.EndVertical();
        }
        
        private void DrawBadge(string text, Color color)
        {
            var originalColor = GUI.backgroundColor;
            GUI.backgroundColor = color;
            GUILayout.Label(text, EditorStyles.miniButton, GUILayout.Height(18));
            GUI.backgroundColor = originalColor;
        }
        
        private string GetCategory()
        {
            if (!string.IsNullOrEmpty(decodedFlags.Type))
            {
                if (decodedFlags.Type.Contains("container")) return "Container";
                if (decodedFlags.Type.Contains("barrier")) return "Barrier";
                if (decodedFlags.Type.Contains("limiter")) return "Limiter";
                if (decodedFlags.Type.Contains("ladder")) return "Ladder";
            }
            
            if (decodedFlags.IsDestructible) return "Destructible";
            if (decodedFlags.IsMoveable) return "Moveable";
            if (decodedFlags.IsUseable) return "Useable";
            if (decodedFlags.IsInvisible) return "Invisible";
            
            return "Other";
        }
        
        // IDENTIFICATION SECTION
        
        private void DrawIdentificationSection()
        {
            showIdentification = EditorGUILayout.BeginFoldoutHeaderGroup(showIdentification, "Identification");
            
            if (showIdentification)
            {
                EditorGUI.indentLevel++;
                
                EditorGUILayout.PropertyField(serializedObject.FindProperty("PropID"), 
                    new GUIContent("Prop ID", "Scene prop identifier (spr_ prefix added automatically)"));
                
                EditorGUI.indentLevel--;
            }
            
            EditorGUILayout.EndFoldoutHeaderGroup();
        }
        
        // MESHES SECTION
        
        private void DrawMeshesSection()
        {
            showMeshes = EditorGUILayout.BeginFoldoutHeaderGroup(showMeshes, "Meshes");
            
            if (showMeshes)
            {
                EditorGUI.indentLevel++;
                
                EditorGUILayout.PropertyField(serializedObject.FindProperty("Mesh"), 
                    new GUIContent("3D Mesh", "Name of the visual mesh resource"));
                
                EditorGUILayout.PropertyField(serializedObject.FindProperty("Collision"), 
                    new GUIContent("Collision Mesh", "Name of the collision mesh (0 for none)"));
                
                // Validation for collision
                if (string.IsNullOrEmpty(propData.Collision) || propData.Collision == "0")
                {
                    if (decodedFlags.IsUseable)
                    {
                        EditorGUILayout.HelpBox("⚠️ Useable props require a collision mesh!", MessageType.Warning);
                    }
                }
                
                EditorGUI.indentLevel--;
            }
            
            EditorGUILayout.EndFoldoutHeaderGroup();
        }
        
        // TYPE SECTION
        
        private void DrawTypeSection()
        {
            showType = EditorGUILayout.BeginFoldoutHeaderGroup(showType, "Scene Prop Type");
            
            if (showType)
            {
                EditorGUI.indentLevel++;
                
                EditorGUILayout.HelpBox(
                    "Type flags are mutually exclusive - only ONE can be active.",
                    MessageType.Info
                );
                
                // Get all types in SORTED order (critical for consistent dropdown)
                var types = ScenePropFlagsDecoder.GetScenePropTypes();
                var typeOptions = new List<string> { "(No Type)" };
                
                // Sort alphabetically to ensure consistent order
                var sortedTypes = types.Keys.OrderBy(x => x).ToList();
                typeOptions.AddRange(sortedTypes);
                
                // Find current selection
                int currentIndex = 0;
                if (!string.IsNullOrEmpty(modifiedType))
                {
                    currentIndex = typeOptions.IndexOf(modifiedType);
                    if (currentIndex == -1)
                    {
                        Debug.LogWarning($"Type '{modifiedType}' not found in type options, resetting to (No Type)");
                        currentIndex = 0;
                        modifiedType = "";
                        flagsModified = true;
                    }
                }
                
                // Draw dropdown
                int newIndex = EditorGUILayout.Popup("Type", currentIndex, typeOptions.ToArray());
                
                if (newIndex != currentIndex)
                {
                    modifiedType = newIndex == 0 ? "" : typeOptions[newIndex];
                    flagsModified = true;
                    
                    // Debug log for verification
                    Debug.Log($"Type changed to: {(string.IsNullOrEmpty(modifiedType) ? "(No Type)" : modifiedType)}");
                }
                
                // Show description
                if (!string.IsNullOrEmpty(modifiedType))
                {
                    string description = ScenePropFlagsDecoder.GetFlagDescription(modifiedType);
                    if (!string.IsNullOrEmpty(description))
                    {
                        EditorGUILayout.HelpBox(description, MessageType.None);
                    }
                }
                
                EditorGUI.indentLevel--;
            }
            
            EditorGUILayout.EndFoldoutHeaderGroup();
        }
        
        // PROPERTY FLAGS SECTION
        
        private void DrawPropertyFlagsSection()
        {
            showPropertyFlags = EditorGUILayout.BeginFoldoutHeaderGroup(showPropertyFlags, 
                $"Property Flags ({modifiedPropertyFlags.Count} active)");
            
            if (showPropertyFlags)
            {
                EditorGUI.indentLevel++;
                
                EditorGUILayout.HelpBox(
                    "Property flags can be combined - check all that apply.",
                    MessageType.Info
                );
                
                // Organize flags by category
                var allFlags = ScenePropFlagsDecoder.GetPropertyFlags();
                var categories = new Dictionary<string, List<string>>();
                
                foreach (var flag in allFlags.Keys)
                {
                    string category = ScenePropFlagsDecoder.GetFlagCategory(flag);
                    if (!categories.ContainsKey(category))
                        categories[category] = new List<string>();
                    categories[category].Add(flag);
                }
                
                // Draw each category
                foreach (var category in categories.OrderBy(c => c.Key))
                {
                    EditorGUILayout.LabelField(category.Key, EditorStyles.boldLabel);
                    
                    foreach (var flag in category.Value.OrderBy(f => f))
                    {
                        DrawFlagCheckbox(flag);
                    }
                    
                    EditorGUILayout.Space(3);
                }
                
                EditorGUI.indentLevel--;
            }
            
            EditorGUILayout.EndFoldoutHeaderGroup();
        }
        
        private void DrawFlagCheckbox(string flagName)
        {
            bool currentValue = modifiedPropertyFlags.Contains(flagName);
            bool isDestructible = modifiedPropertyFlags.Contains("sokf_destructible");
            
            // Check if this is an overlapping flag
            bool isOverlapping = flagName == "sokf_enforce_shadows" ||
                                flagName == "sokf_dont_move_agent_over" ||
                                flagName == "sokf_handle_as_flora" ||
                                flagName == "sokf_static_movement";
            
            // Get description for tooltip
            string description = ScenePropFlagsDecoder.GetFlagDescription(flagName);
            
            // CRITICAL: Check if overlapping flag should be disabled
            bool shouldDisableOverlap = isOverlapping && isDestructible && modifiedHitPoints > 0;
            
            // Add detailed warning for overlapping flags
            if (isOverlapping)
            {
                if (shouldDisableOverlap)
                {
                    description += "\n\n❌ DISABLED: This flag uses bits 20-27 which are reserved for " +
                                  "hit points storage when destructible is enabled with HP > 0.\n\n" +
                                  "To use this flag: Set Hit Points to 0 OR disable destructible.";
                }
                else if (isDestructible)
                {
                    description += "\n\nℹ️ NOTE: This flag uses bits 20-27. When you set Hit Points > 0, " +
                                  "this flag will be automatically disabled to prevent hit points corruption.";
                }
            }
            
            // Add visual indicator for overlapping flags
            string displayName = flagName.Replace("sokf_", "");
            if (isOverlapping)
            {
                if (shouldDisableOverlap)
                {
                    displayName = "🔒 " + displayName;  // Locked icon for disabled overlapping flags
                }
                else
                {
                    displayName = "⚠️ " + displayName;  // Warning icon for potential overlap
                }
            }
            
            GUIContent label = new GUIContent(displayName, description);
            
            // Disable deprecated flags
            bool isDeprecated = flagName.Contains("add_fire") || 
                               flagName.Contains("add_smoke") || 
                               flagName.Contains("add_light");
            
            // Disable overlapping flags when destructible + HP > 0
            bool wasEnabled = GUI.enabled;
            if (isDeprecated || shouldDisableOverlap)
            {
                GUI.enabled = false;
                
                // Force uncheck overlapping flags if they were previously enabled
                if (shouldDisableOverlap && currentValue)
                {
                    modifiedPropertyFlags.Remove(flagName);
                    currentValue = false;
                    flagsModified = true;
                }
            }
            
            // Gray background for disabled overlapping flags to make it obvious
            if (shouldDisableOverlap)
            {
                var originalColor = GUI.backgroundColor;
                GUI.backgroundColor = new Color(0.85f, 0.85f, 0.85f); // Gray
                EditorGUILayout.Toggle(label, false); // Always show unchecked when disabled
                GUI.backgroundColor = originalColor;
            }
            else
            {
                bool newValue = EditorGUILayout.Toggle(label, currentValue);
                
                if (newValue != currentValue)
                {
                    if (newValue)
                        modifiedPropertyFlags.Add(flagName);
                    else
                        modifiedPropertyFlags.Remove(flagName);
                    
                    flagsModified = true;
                }
            }
            
            if (!wasEnabled || shouldDisableOverlap || isDeprecated)
            {
                GUI.enabled = wasEnabled;
            }
        }
        
        // DESTRUCTIBLE SECTION
        
        private void DrawDestructibleSection()
        {
            bool isDestructible = modifiedPropertyFlags.Contains("sokf_destructible");
            
            showDestructible = EditorGUILayout.BeginFoldoutHeaderGroup(showDestructible, 
                isDestructible ? "💔 Destructible Properties" : "Destructible Properties (Disabled)");
            
            if (showDestructible)
            {
                EditorGUI.indentLevel++;
                
                if (!isDestructible)
                {
                    EditorGUILayout.HelpBox(
                        "Enable 'sokf_destructible' flag to make this prop destructible.",
                        MessageType.Info
                    );
                }
                else
                {
                    // Hit points slider
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField("Hit Points", GUILayout.Width(EditorGUIUtility.labelWidth));
                    
                    int newHitPoints = EditorGUILayout.IntSlider(modifiedHitPoints, 0, 255);
                    
                    if (newHitPoints != modifiedHitPoints)
                    {
                        modifiedHitPoints = newHitPoints;
                        flagsModified = true;
                    }
                    
                    EditorGUILayout.EndHorizontal();
                    
                    // CRITICAL WARNING: Show info about bit conflict when HP > 0
                    if (modifiedHitPoints > 0)
                    {
                        EditorGUILayout.Space(5);
                        
                        // Warning box with orange background
                        var originalColor = GUI.backgroundColor;
                        GUI.backgroundColor = new Color(1.0f, 0.9f, 0.7f); // Light orange
                        
                        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                        GUI.backgroundColor = originalColor;
                        
                        EditorGUILayout.LabelField("⚠️ Hit Points Storage Priority", EditorStyles.boldLabel);
                        
                        EditorGUILayout.Space(3);
                        EditorGUILayout.LabelField(
                            "When destructible with HP > 0, bits 20-27 are RESERVED for hit points storage.",
                            EditorStyles.wordWrappedLabel
                        );
                        
                        EditorGUILayout.Space(3);
                        EditorGUILayout.LabelField(
                            "These flags are automatically DISABLED:",
                            EditorStyles.wordWrappedMiniLabel
                        );
                        
                        EditorGUILayout.LabelField(
                            "  • sokf_enforce_shadows (bit 20)",
                            EditorStyles.wordWrappedMiniLabel
                        );
                        EditorGUILayout.LabelField(
                            "  • sokf_dont_move_agent_over (bit 21)",
                            EditorStyles.wordWrappedMiniLabel
                        );
                        EditorGUILayout.LabelField(
                            "  • sokf_handle_as_flora (bit 24)",
                            EditorStyles.wordWrappedMiniLabel
                        );
                        EditorGUILayout.LabelField(
                            "  • sokf_static_movement (bit 25)",
                            EditorStyles.wordWrappedMiniLabel
                        );
                        
                        EditorGUILayout.Space(3);
                        EditorGUILayout.LabelField(
                            "To use these flags: Set HP to 0 OR disable destructible.",
                            EditorStyles.wordWrappedMiniLabel
                        );
                        
                        EditorGUILayout.EndVertical();
                        EditorGUILayout.Space(5);
                    }
                    
                    // Show health bar toggle
                    bool showBar = modifiedPropertyFlags.Contains("sokf_show_hit_point_bar");
                    bool newShowBar = EditorGUILayout.Toggle(
                        new GUIContent("Show Health Bar", "Display HP bar in-game"),
                        showBar
                    );
                    
                    if (newShowBar != showBar)
                    {
                        if (newShowBar)
                            modifiedPropertyFlags.Add("sokf_show_hit_point_bar");
                        else
                            modifiedPropertyFlags.Remove("sokf_show_hit_point_bar");
                        
                        flagsModified = true;
                    }
                    
                    // Validation
                    if (modifiedHitPoints == 0)
                    {
                        EditorGUILayout.HelpBox(
                            "⚠️ Destructible props should have hit points > 0",
                            MessageType.Warning
                        );
                    }
                }
                
                EditorGUI.indentLevel--;
            }
            
            EditorGUILayout.EndFoldoutHeaderGroup();
        }
        
        // USEABLE SECTION
        
        private void DrawUseableSection()
        {
            bool isUseable = modifiedUseTime > 0;
            
            showUseable = EditorGUILayout.BeginFoldoutHeaderGroup(showUseable, 
                isUseable ? "⏱️ Useable Properties" : "Useable Properties (Disabled)");
            
            if (showUseable)
            {
                EditorGUI.indentLevel++;
                
                EditorGUILayout.HelpBox(
                    "Useable props can be activated by the player (F key). " +
                    "Requires collision mesh and ti_on_scene_prop_use trigger. " +
                    "Set can_use_scene_props_in_single_player=1 in module.ini for SP.",
                    MessageType.Info
                );
                
                // Use time slider
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Use Time (seconds)", GUILayout.Width(EditorGUIUtility.labelWidth));
                
                int newUseTime = EditorGUILayout.IntSlider(modifiedUseTime, 0, 255);
                
                if (newUseTime != modifiedUseTime)
                {
                    modifiedUseTime = newUseTime;
                    flagsModified = true;
                }
                
                EditorGUILayout.EndHorizontal();
                
                if (modifiedUseTime == 0)
                {
                    EditorGUILayout.LabelField("0 = Instant activation (no hold required)", EditorStyles.miniLabel);
                }
                else
                {
                    EditorGUILayout.LabelField($"Player must hold F for {modifiedUseTime} seconds", EditorStyles.miniLabel);
                }
                
                // Validation
                if (isUseable)
                {
                    if (string.IsNullOrEmpty(propData.Collision) || propData.Collision == "0")
                    {
                        EditorGUILayout.HelpBox(
                            "⚠️ Useable props require a collision mesh!",
                            MessageType.Warning
                        );
                    }
                    
                    if (string.IsNullOrEmpty(propData.Triggers))
                    {
                        EditorGUILayout.HelpBox(
                            "⚠️ Useable props require ti_on_scene_prop_use trigger!",
                            MessageType.Warning
                        );
                    }
                }
                
                EditorGUI.indentLevel--;
            }
            
            EditorGUILayout.EndFoldoutHeaderGroup();
        }
        
        // TRIGGERS SECTION
        
        private void DrawTriggersSection()
        {
            bool hasTriggers = !string.IsNullOrEmpty(propData.Triggers) && propData.Triggers != "[]";
            
            showTriggers = EditorGUILayout.BeginFoldoutHeaderGroup(showTriggers, "⚡ Triggers");
            
            if (showTriggers)
            {
                EditorGUI.indentLevel++;
                
                    // Trigger code viewer (read-only for now)
                    EditorGUILayout.LabelField("Trigger Code:", EditorStyles.boldLabel);
                    
                    EditorGUILayout.HelpBox(
                        "Trigger code is imported from module system. " +
                        "Includes ti_on_init_scene_prop, ti_on_scene_prop_destroy, etc.",
                        MessageType.Info
                    );
                    
                    // Show trigger code in scrollable text area
                    EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                    
                    EditorStyles.textArea.wordWrap = true;
                    string triggerDisplay = propData.Triggers;
                    
                    // Limit display length
                    if (triggerDisplay.Length > 1000)
                    {
                        triggerDisplay = triggerDisplay.Substring(0, 1000) + "\n... (truncated)";
                    }
                    
                    EditorGUILayout.TextArea(triggerDisplay, EditorStyles.textArea, 
                        GUILayout.Height(150), GUILayout.ExpandHeight(false));
                    
                    EditorGUILayout.EndVertical();
                
                EditorGUI.indentLevel--;
            }
            
            EditorGUILayout.EndFoldoutHeaderGroup();
        }
        
        // VALIDATION SECTION
        
        private void DrawValidationSection()
        {
            try
            {
                BigInteger flags = BigInteger.Parse(propData.Flags);
                var issues = ScenePropFlagsDecoder.ValidateFlags(flags);
                
                if (issues.Count > 0)
                {
                    EditorGUILayout.Space(5);
                    EditorGUILayout.LabelField("⚠️ Validation Issues", EditorStyles.boldLabel);
                    
                    foreach (var issue in issues)
                    {
                        EditorGUILayout.HelpBox(issue, MessageType.Warning);
                    }
                }
            }
            catch (System.Exception e)
            {
                EditorGUILayout.HelpBox($"Validation error: {e.Message}", MessageType.Error);
            }
        }
        
        // RAW DATA SECTION
        
        private void DrawRawDataSection()
        {
            showRawData = EditorGUILayout.BeginFoldoutHeaderGroup(showRawData, "Raw Data (Advanced)");
            
            if (showRawData)
            {
                EditorGUI.indentLevel++;
                
                EditorGUILayout.HelpBox(
                    "These fields are the source of truth. Metadata fields above are derived for convenience.",
                    MessageType.Info
                );
                
                // Flags (read-only display)
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Flags (Hex)", GUILayout.Width(EditorGUIUtility.labelWidth));
                
                try
                {
                    BigInteger flags = BigInteger.Parse(propData.Flags);
                    EditorGUILayout.SelectableLabel($"0x{flags:X}", EditorStyles.textField, GUILayout.Height(18));
                }
                catch
                {
                    EditorGUILayout.LabelField("(Invalid)");
                }
                
                EditorGUILayout.EndHorizontal();
                
                // Flags (decimal)
                EditorGUILayout.PropertyField(serializedObject.FindProperty("Flags"), 
                    new GUIContent("Flags (Decimal)", "Raw flags value"));
                
                // Metadata (read-only)
                GUI.enabled = false;
                EditorGUILayout.PropertyField(serializedObject.FindProperty("TypeName"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("HitPoints"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("UseTime"));
                GUI.enabled = true;
                
                EditorGUILayout.LabelField("(Metadata is auto-updated from Flags)", EditorStyles.miniLabel);
                
                EditorGUI.indentLevel--;
            }
            
            EditorGUILayout.EndFoldoutHeaderGroup();
        }
    }
}
