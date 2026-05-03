using UnityEngine;
using UnityEditor;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using MountAndBladeTools;

/// <summary>
/// Enhanced editor window for managing Mount & Blade troop skill knowledge.
/// Provides UI for setting skill levels with presets, tooltips, and visual improvements.
/// </summary>
public class TroopSkillsWindow : EditorWindow
{
    private MBTroopData selectedTroop;
    private Dictionary<int, int> skillLevels = new Dictionary<int, int>();
    private UnityEngine.Vector2 scrollPosition;
    private bool isDirty = false;
    private string currentKnowsValue = "0";
    private bool showRawValue = false;
    private bool showPresets = true;
    private bool showAdvancedOptions = false;
    private string searchFilter = "";
    
    // Visual constants
    private const float SKILL_NAME_WIDTH = 180f;
    private const float SLIDER_WIDTH = 200f;
    private const float LEVEL_LABEL_WIDTH = 30f;
    private const float REFERENCE_WIDTH = 180f;
    
    // Colors for skill types
    private static readonly Color COLOR_PARTY_SKILL = new Color(0.6f, 0.8f, 1f);
    private static readonly Color COLOR_LEADER_SKILL = new Color(1f, 0.8f, 0.6f);
    private static readonly Color COLOR_PERSONAL_SKILL = new Color(0.8f, 1f, 0.6f);
    
    // Skill information database
    private static readonly Dictionary<int, SkillData> SkillDatabase = new Dictionary<int, SkillData>
    {
        {0, new SkillData("Trade", "Charisma", "Party", "Every level of this skill reduces your trade penalty by 5%.")},
        {1, new SkillData("Leadership", "Charisma", "Leader", "Every point increases maximum number of troops you can command by 5, increases your party morale and reduces troop wages by 5%.")},
        {2, new SkillData("Prisoner Management", "Charisma", "Leader", "Every level of this skill increases maximum number of prisoners by 5.")},
        {7, new SkillData("Persuasion", "Intelligence", "Personal", "This skill helps you make other people accept your point of view. It also lowers the minimum level of relationship needed to get NPCs to do what you want.")},
        {8, new SkillData("Engineer", "Intelligence", "Party", "This skill allows you to construct siege equipment and fief improvements more efficiently.")},
        {9, new SkillData("First Aid", "Intelligence", "Party", "Heroes regain 5% per skill level of hit-points lost during mission.")},
        {10, new SkillData("Surgery", "Intelligence", "Party", "Each point to this skill gives a 4% chance that a mortally struck party member will be wounded rather than killed.")},
        {11, new SkillData("Wound Treatment", "Intelligence", "Party", "Party healing speed is increased by 20% per level of this skill.")},
        {12, new SkillData("Inventory Management", "Intelligence", "Leader", "Increases inventory capacity by +6 per skill level.")},
        {13, new SkillData("Spotting", "Intelligence", "Party", "Party seeing range is increased by 10% per skill level.")},
        {14, new SkillData("Path-finding", "Intelligence", "Party", "Party map speed is increased by 3% per skill level.")},
        {15, new SkillData("Tactics", "Intelligence", "Party", "Every two levels of this skill increases starting battle advantage by 1.")},
        {16, new SkillData("Tracking", "Intelligence", "Party", "Tracks become more informative.")},
        {17, new SkillData("Trainer", "Intelligence", "Personal", "Every day, each hero with this skill adds some experience to every other member of the party whose level is lower than his/hers. Experience gained: {0,4,10,16,23,30,38,46,55,65,80}.")},
        {22, new SkillData("Looting", "Agility", "Party", "This skill increases the amount of loot obtained by 10% per skill level.")},
        {23, new SkillData("Horse Archery", "Agility", "Personal", "Reduces damage and accuracy penalties for archery and throwing from horseback.")},
        {24, new SkillData("Riding", "Agility", "Personal", "Enables you to ride horses of higher difficulty levels and increases your riding speed and maneuver.")},
        {25, new SkillData("Athletics", "Agility", "Personal", "Improves your running speed.")},
        {26, new SkillData("Shield", "Agility", "Personal", "Reduces damage to shields (by 8% per skill level) and improves shield speed and coverage.")},
        {27, new SkillData("Weapon Master", "Agility", "Personal", "Makes it easier to learn weapon proficiencies and increases the proficiency limits. Limits: 60, 100, 140, 180, 220, 260, 300, 340, 380, 420.")},
        {33, new SkillData("Power Draw", "Strength", "Personal", "Lets character use more powerful bows. Each point to this skill (up to four plus power-draw requirement of the bow) increases bow damage by 14%.")},
        {34, new SkillData("Power Throw", "Strength", "Personal", "Each point to this skill increases throwing damage by 10%.")},
        {35, new SkillData("Power Strike", "Strength", "Personal", "Each point to this skill increases melee damage by 8%.")},
        {36, new SkillData("Ironflesh", "Strength", "Personal", "Each point to this skill increases hit points by +2.")},
    };
    
    // Preset configurations
    private static readonly Dictionary<string, SkillPreset> Presets = new Dictionary<string, SkillPreset>
    {
        {"Common", new SkillPreset("Common Skills", "Basic skills for most troops",
            new Dictionary<int, int> {{24, 1}, {0, 2}, {12, 2}, {2, 1}, {1, 1}})},
        
        {"Common MP", new SkillPreset("Common Multiplayer", "Full skills for multiplayer",
            new Dictionary<int, int> {{0, 10}, {12, 10}, {2, 10}, {1, 10}, {13, 10}, {14, 10}, {16, 10}, {8, 10}, {9, 10}, {10, 10}, {11, 10}, {15, 10}, {17, 10}, {22, 10}})},
        
        {"Lord 1", new SkillPreset("Lord Level 1", "Skills for a basic lord",
            new Dictionary<int, int> {{24, 3}, {0, 2}, {12, 2}, {15, 4}, {2, 4}, {1, 7}})},
        
        {"Warrior", new SkillPreset("Warrior NPC", "Combat-focused companion",
            new Dictionary<int, int> {{27, 2}, {36, 1}, {25, 1}, {35, 2}, {24, 2}, {26, 1}, {12, 2}})},
        
        {"Merchant", new SkillPreset("Merchant NPC", "Trade-focused companion",
            new Dictionary<int, int> {{24, 2}, {0, 3}, {12, 3}})},
        
        {"Tracker", new SkillPreset("Tracker NPC", "Scouting-focused companion",
            new Dictionary<int, int> {{27, 1}, {25, 2}, {13, 2}, {14, 2}, {16, 2}, {36, 1}, {12, 2}})},
    };
    
    // Use skill categories from MBSkillDecoder
    private Dictionary<string, List<int>> SkillCategories
    {
        get { return MBSkillDecoder.GetSkillCategories(); }
    }
    public static void ShowWindow(MBTroopData troop)
    {
        var window = GetWindow<TroopSkillsWindow>("Troop Skills");
        window.selectedTroop = troop;
        window.minSize = new UnityEngine.Vector2(550, 700);
        window.ParseTroopSkills();
    }
    
    private void OnEnable()
    {
        // Load selected troop if one is selected
        if (Selection.activeObject is MBTroopData)
        {
            LoadTroop(Selection.activeObject as MBTroopData);
        }
    }
    
    private void OnSelectionChange()
    {
        if (Selection.activeObject is MBTroopData)
        {
            LoadTroop(Selection.activeObject as MBTroopData);
            Repaint();
        }
    }
    
    private void LoadTroop(MBTroopData troop)
    {
        selectedTroop = troop;
        ParseTroopSkills();
        isDirty = false;
    }
    
    private void ParseTroopSkills()
    {
        skillLevels.Clear();
        
        if (selectedTroop == null || string.IsNullOrEmpty(selectedTroop.TroopSkills))
        {
            currentKnowsValue = "0";
            return;
        }
        
        try
        {
            // Try to parse as direct BigInteger value
            if (BigInteger.TryParse(selectedTroop.TroopSkills, out BigInteger combinedValue))
            {
                currentKnowsValue = combinedValue.ToString();
                skillLevels = MBSkillKnowledgeDecoder.DecodeAllKnowledge(combinedValue);
            }
            else
            {
                // Try to parse as Python-style expression (e.g., "knows_trade_2|knows_riding_3")
                currentKnowsValue = "0";
                ParsePythonExpression(selectedTroop.TroopSkills);
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"Error parsing troop skills: {e.Message}");
            currentKnowsValue = "0";
        }
    }
    
    private void ParsePythonExpression(string expression)
    {
        // Parse expressions like "knows_trade_2|knows_riding_3"
        string[] parts = expression.Split('|');
        BigInteger combined = 0;
        
        foreach (string part in parts)
        {
            string trimmed = part.Trim();
            if (trimmed.StartsWith("knows_"))
            {
                // Extract skill and level from "knows_skillname_level"
                string[] components = trimmed.Split('_');
                if (components.Length >= 3)
                {
                    string skillId = string.Join("_", components.Skip(1).Take(components.Length - 2));
                    if (int.TryParse(components[components.Length - 1], out int level))
                    {
                        // Find skill index by ID
                        int skillIndex = FindSkillIndexByName(skillId);
                        if (skillIndex >= 0)
                        {
                            BigInteger value = MBSkillKnowledgeDecoder.CalculateKnowsValue(skillIndex, level);
                            combined += value;
                        }
                    }
                }
            }
        }
        
        if (combined > 0)
        {
            currentKnowsValue = combined.ToString();
            skillLevels = MBSkillKnowledgeDecoder.DecodeAllKnowledge(combined);
        }
    }
    
    private int FindSkillIndexByName(string skillName)
    {
        return MBSkillDecoder.GetSkillIndex(skillName);
    }
    
    private void OnGUI()
    {
        EditorGUILayout.Space(5);
        
        // Header with style
        DrawHeader();
        
        EditorGUILayout.Space(5);
        
        // Troop selection
        DrawTroopSelection();
        
        if (selectedTroop == null)
        {
            EditorGUILayout.HelpBox("Select a Troop Data asset to edit skills.", MessageType.Info);
            return;
        }
        
        // Troop info panel
        DrawTroopInfo();
        
        // Presets section
        if (showPresets)
        {
            DrawPresetsSection();
        }
        
        // Quick actions toolbar
        DrawQuickActionsToolbar();
        
        // Skills editor with search
        DrawSkillsSection();
        
        EditorGUILayout.Space(5);
        
        // Save button
        DrawSaveButton();
        
        if (isDirty)
        {
            EditorGUILayout.HelpBox("You have unsaved changes.", MessageType.Warning);
        }
    }
    
    private void DrawHeader()
    {
        GUIStyle headerStyle = new GUIStyle(EditorStyles.boldLabel)
        {
            fontSize = 16,
            alignment = TextAnchor.MiddleCenter
        };
        
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField("⚔ Mount & Blade Troop Skills Editor", headerStyle, GUILayout.Height(30));
        EditorGUILayout.EndVertical();
    }
    
    private void DrawTroopSelection()
    {
        EditorGUILayout.BeginHorizontal();
        
        EditorGUI.BeginChangeCheck();
        selectedTroop = (MBTroopData)EditorGUILayout.ObjectField(
            new GUIContent("Selected Troop", "The troop asset to edit"),
            selectedTroop, 
            typeof(MBTroopData), 
            false
        );
        if (EditorGUI.EndChangeCheck() && selectedTroop != null)
        {
            LoadTroop(selectedTroop);
        }
        
        EditorGUILayout.EndHorizontal();
    }
    
    private void DrawTroopInfo()
    {
        EditorGUILayout.Space(5);
        
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        
        GUIStyle boldStyle = new GUIStyle(EditorStyles.boldLabel);
        
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Troop Information", boldStyle);
        
        // Toggle buttons
        showRawValue = GUILayout.Toggle(showRawValue, new GUIContent("Raw", "Show raw numeric value"), EditorStyles.miniButton, GUILayout.Width(40));
        showPresets = GUILayout.Toggle(showPresets, new GUIContent("Presets", "Show skill presets"), EditorStyles.miniButton, GUILayout.Width(60));
        showAdvancedOptions = GUILayout.Toggle(showAdvancedOptions, new GUIContent("Advanced", "Show advanced options"), EditorStyles.miniButton, GUILayout.Width(70));
        
        EditorGUILayout.EndHorizontal();
        
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("ID:", GUILayout.Width(80));
        EditorGUILayout.LabelField(selectedTroop.TroopID, EditorStyles.wordWrappedLabel);
        EditorGUILayout.EndHorizontal();
        
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Name:", GUILayout.Width(80));
        EditorGUILayout.LabelField(selectedTroop.TroopName, EditorStyles.wordWrappedLabel);
        EditorGUILayout.EndHorizontal();
        
        // Show skill count
        int skillCount = skillLevels.Count;
        int totalLevels = skillLevels.Sum(kvp => kvp.Value);
        
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Skills Set:", GUILayout.Width(80));
        EditorGUILayout.LabelField($"{skillCount} skills ({totalLevels} total levels)", EditorStyles.wordWrappedLabel);
        EditorGUILayout.EndHorizontal();
        
        if (showRawValue)
        {
            EditorGUILayout.Space(3);
            EditorGUILayout.LabelField("Combined Knows Value:", EditorStyles.miniBoldLabel);
            EditorGUILayout.SelectableLabel(currentKnowsValue, EditorStyles.textField, GUILayout.Height(20));
        }
        
        if (showAdvancedOptions)
        {
            EditorGUILayout.Space(3);
            EditorGUILayout.LabelField("Python Expression:", EditorStyles.miniBoldLabel);
            EditorGUILayout.SelectableLabel(GeneratePythonExpression(), EditorStyles.textField, GUILayout.Height(20));
        }
        
        EditorGUILayout.EndVertical();
    }
    
    private void DrawPresetsSection()
    {
        EditorGUILayout.Space(5);
        
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        
        EditorGUILayout.LabelField("Skill Presets", EditorStyles.boldLabel);
        
        EditorGUILayout.BeginHorizontal();
        
        int buttonCount = 0;
        foreach (var preset in Presets)
        {
            if (GUILayout.Button(new GUIContent(preset.Value.Name, preset.Value.Description), GUILayout.Height(25)))
            {
                ApplyPreset(preset.Value);
            }
            
            buttonCount++;
            if (buttonCount % 3 == 0)
            {
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.BeginHorizontal();
            }
        }
        
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.EndVertical();
    }
    
    private void DrawQuickActionsToolbar()
    {
        EditorGUILayout.Space(5);
        
        EditorGUILayout.BeginHorizontal();
        
        // Search field
        GUILayout.Label("Search:", GUILayout.Width(55));
        searchFilter = EditorGUILayout.TextField(searchFilter, EditorStyles.toolbarSearchField);
        
        if (GUILayout.Button("Clear All", EditorStyles.miniButtonLeft, GUILayout.Width(70)))
        {
            if (EditorUtility.DisplayDialog("Clear All Skills", 
                "Are you sure you want to clear all skill knowledge?", "Yes", "Cancel"))
            {
                skillLevels.Clear();
                UpdateKnowsValue();
                isDirty = true;
            }
        }
        
        if (GUILayout.Button("Reset", EditorStyles.miniButtonMid, GUILayout.Width(50)))
        {
            ParseTroopSkills();
            isDirty = false;
        }
        
        if (GUILayout.Button("+1 All", EditorStyles.miniButtonMid, GUILayout.Width(50)))
        {
            ModifyAllSkills(1);
        }
        
        if (GUILayout.Button("-1 All", EditorStyles.miniButtonRight, GUILayout.Width(50)))
        {
            ModifyAllSkills(-1);
        }
        
        EditorGUILayout.EndHorizontal();
    }
    
    private void DrawSkillsSection()
    {
        EditorGUILayout.Space(5);
        
        // Legend
        EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
        DrawLegendItem("Party Skill", COLOR_PARTY_SKILL);
        DrawLegendItem("Leader Skill", COLOR_LEADER_SKILL);
        DrawLegendItem("Personal Skill", COLOR_PERSONAL_SKILL);
        EditorGUILayout.EndHorizontal();
        
        EditorGUILayout.Space(3);
        
        // Skills editor
        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
        DrawSkillsEditor();
        EditorGUILayout.EndScrollView();
    }
    
    private void DrawLegendItem(string label, Color color)
    {
        Color oldColor = GUI.backgroundColor;
        GUI.backgroundColor = color;
        GUILayout.Label("", EditorStyles.helpBox, GUILayout.Width(15), GUILayout.Height(15));
        GUI.backgroundColor = oldColor;
        GUILayout.Label(label, EditorStyles.miniLabel);
        GUILayout.Space(10);
    }
    
    private void DrawSaveButton()
    {
        Color oldColor = GUI.backgroundColor;
        GUI.backgroundColor = isDirty ? new Color(0.5f, 1f, 0.5f) : Color.white;
        
        GUI.enabled = isDirty;
        if (GUILayout.Button(isDirty ? "💾 Save Changes" : "✓ Saved", GUILayout.Height(35)))
        {
            SaveChanges();
        }
        GUI.enabled = true;
        
        GUI.backgroundColor = oldColor;
    }
    
    private void DrawSkillsEditor()
    {
        bool hasFilter = !string.IsNullOrEmpty(searchFilter);
        
        foreach (var category in SkillCategories)
        {
            // Check if any skills in this category match the filter
            if (hasFilter)
            {
                bool hasMatch = false;
                foreach (int skillIndex in category.Value)
                {
                    if (SkillDatabase.ContainsKey(skillIndex))
                    {
                        var skillData = SkillDatabase[skillIndex];
                        if (skillData.Name.IndexOf(searchFilter, StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            hasMatch = true;
                            break;
                        }
                    }
                }
                
                if (!hasMatch) continue;
            }
            
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField(category.Key + " Skills", EditorStyles.boldLabel);
            
            foreach (int skillIndex in category.Value)
            {
                if (hasFilter && SkillDatabase.ContainsKey(skillIndex))
                {
                    var skillData = SkillDatabase[skillIndex];
                    if (skillData.Name.IndexOf(searchFilter, StringComparison.OrdinalIgnoreCase) < 0)
                        continue;
                }
                
                DrawSkillSlider(skillIndex);
            }
            
            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(3);
        }
        
        // Additional skills (if any exist beyond the categorized ones)
        var categorizedSkills = SkillCategories.Values.SelectMany(x => x).ToHashSet();
        bool hasUncategorized = false;
        
        for (int i = 0; i <= 41; i++)
        {
            if (!categorizedSkills.Contains(i))
            {
                if (hasFilter && SkillDatabase.ContainsKey(i))
                {
                    var skillData = SkillDatabase[i];
                    if (skillData.Name.IndexOf(searchFilter, StringComparison.OrdinalIgnoreCase) < 0)
                        continue;
                }
                
                if (!hasUncategorized)
                {
                    EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                    EditorGUILayout.LabelField("Other Skills", EditorStyles.boldLabel);
                    hasUncategorized = true;
                }
                DrawSkillSlider(i);
            }
        }
        
        if (hasUncategorized)
        {
            EditorGUILayout.EndVertical();
        }
    }
    
    private void DrawSkillSlider(int skillIndex)
    {
        try
        {
            var skillInfo = MBSkillKnowledgeDecoder.GetKnowledgeInfo(skillIndex, 1);
            string skillName = skillInfo.SkillName ?? $"Skill {skillIndex}";
            
            // Get skill data for tooltip and color
            SkillData skillData = null;
            if (SkillDatabase.ContainsKey(skillIndex))
            {
                skillData = SkillDatabase[skillIndex];
                skillName = skillData.Name;
            }
            
            int currentLevel = skillLevels.ContainsKey(skillIndex) ? skillLevels[skillIndex] : 0;
            
            // Color the background based on skill type
            Color oldColor = GUI.backgroundColor;
            if (skillData != null && currentLevel > 0)
            {
                GUI.backgroundColor = GetSkillColor(skillData.Type);
            }
            
            EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
            
            // Skill name with tooltip
            GUIContent skillLabel = new GUIContent(
                skillName,
                skillData != null ? $"{skillData.Type} Skill - {skillData.BaseAttribute}\n\n{skillData.Description}" : skillName
            );
            
            EditorGUILayout.LabelField(skillLabel, GUILayout.Width(SKILL_NAME_WIDTH));
            
            // Level display
            GUIStyle levelStyle = new GUIStyle(EditorStyles.boldLabel);
            levelStyle.alignment = TextAnchor.MiddleCenter;
            if (currentLevel > 0)
            {
                levelStyle.normal.textColor = Color.white;
            }
            EditorGUILayout.LabelField(currentLevel.ToString(), levelStyle, GUILayout.Width(LEVEL_LABEL_WIDTH));
            
            // Slider
            EditorGUI.BeginChangeCheck();
            int newLevel = (int)GUILayout.HorizontalSlider(currentLevel, 0, MBSkillKnowledgeDecoder.MaxSkillLevel, GUILayout.Width(SLIDER_WIDTH));
            
            // Quick buttons
            if (GUILayout.Button("-", EditorStyles.miniButtonLeft, GUILayout.Width(20)))
            {
                newLevel = Mathf.Max(0, currentLevel - 1);
            }
            if (GUILayout.Button("+", EditorStyles.miniButtonRight, GUILayout.Width(20)))
            {
                newLevel = Mathf.Min(MBSkillKnowledgeDecoder.MaxSkillLevel, currentLevel + 1);
            }
            
            if (EditorGUI.EndChangeCheck() || newLevel != currentLevel)
            {
                if (newLevel > 0)
                {
                    skillLevels[skillIndex] = newLevel;
                }
                else
                {
                    skillLevels.Remove(skillIndex);
                }
                UpdateKnowsValue();
                isDirty = true;
            }
            
            // Show the knows_ reference for non-zero levels
            if (newLevel > 0)
            {
                string knowsRef = MBSkillKnowledgeDecoder.GetKnowsReference(skillIndex, newLevel);
                EditorGUILayout.LabelField(knowsRef, EditorStyles.miniLabel, GUILayout.Width(REFERENCE_WIDTH));
            }
            
            EditorGUILayout.EndHorizontal();
            
            GUI.backgroundColor = oldColor;
        }
        catch (Exception e)
        {
            EditorGUILayout.LabelField($"Skill {skillIndex}: Error - {e.Message}");
        }
    }
    
    private Color GetSkillColor(string skillType)
    {
        switch (skillType)
        {
            case "Party": return COLOR_PARTY_SKILL;
            case "Leader": return COLOR_LEADER_SKILL;
            case "Personal": return COLOR_PERSONAL_SKILL;
            default: return Color.white;
        }
    }
    
    private void ApplyPreset(SkillPreset preset)
    {
        if (EditorUtility.DisplayDialog("Apply Preset", 
            $"Apply '{preset.Name}' preset?\n\n{preset.Description}\n\nThis will replace current skills.", 
            "Apply", "Cancel"))
        {
            skillLevels.Clear();
            foreach (var kvp in preset.Skills)
            {
                skillLevels[kvp.Key] = kvp.Value;
            }
            UpdateKnowsValue();
            isDirty = true;
        }
    }
    
    private void ModifyAllSkills(int delta)
    {
        var keysToModify = skillLevels.Keys.ToList();
        foreach (var key in keysToModify)
        {
            int newValue = Mathf.Clamp(skillLevels[key] + delta, 0, MBSkillKnowledgeDecoder.MaxSkillLevel);
            if (newValue > 0)
            {
                skillLevels[key] = newValue;
            }
            else
            {
                skillLevels.Remove(key);
            }
        }
        UpdateKnowsValue();
        isDirty = true;
    }
    
    private void UpdateKnowsValue()
    {
        if (skillLevels.Count == 0)
        {
            currentKnowsValue = "0";
            return;
        }
        
        BigInteger combined = 0;
        foreach (var kvp in skillLevels)
        {
            combined += MBSkillKnowledgeDecoder.CalculateKnowsValue(kvp.Key, kvp.Value);
        }
        
        currentKnowsValue = combined.ToString();
    }
    
    private void SaveChanges()
    {
        if (selectedTroop == null) return;
        
        Undo.RecordObject(selectedTroop, "Update Troop Skills");
        
        selectedTroop.TroopSkills = currentKnowsValue;
        
        EditorUtility.SetDirty(selectedTroop);
        AssetDatabase.SaveAssets();
        
        isDirty = false;
        
        Debug.Log($"Saved skills for {selectedTroop.TroopName}: {currentKnowsValue}");
    }
    
    // Utility method to generate Python-style expression
    private string GeneratePythonExpression()
    {
        if (skillLevels.Count == 0)
            return "0";
        
        var parts = new List<string>();
        foreach (var kvp in skillLevels.OrderBy(x => x.Key))
        {
            string knowsRef = MBSkillKnowledgeDecoder.GetKnowsReference(kvp.Key, kvp.Value);
            parts.Add(knowsRef);
        }
        
        return string.Join("|", parts);
    }
    
    // Helper classes
    private class SkillData
    {
        public string Name { get; }
        public string BaseAttribute { get; }
        public string Type { get; }
        public string Description { get; }
        
        public SkillData(string name, string baseAttribute, string type, string description)
        {
            Name = name;
            BaseAttribute = baseAttribute;
            Type = type;
            Description = description;
        }
    }
    
    private class SkillPreset
    {
        public string Name { get; }
        public string Description { get; }
        public Dictionary<int, int> Skills { get; }
        
        public SkillPreset(string name, string description, Dictionary<int, int> skills)
        {
            Name = name;
            Description = description;
            Skills = skills;
        }
    }
}