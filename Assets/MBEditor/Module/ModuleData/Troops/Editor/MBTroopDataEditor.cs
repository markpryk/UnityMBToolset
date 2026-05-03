using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace MountAndBlade.Editor
{
    [CustomEditor(typeof(MBTroopData))]
    public class MBTroopDataEditor : UnityEditor.Editor
    {
        private MBTroopData troopData;
        
        // Foldout states
        private bool showBasicInfo = true;
        private bool showSceneInfo = true;
        private bool showAttributes = true;
        private bool showFaceCodes = true;
        private bool showVisuals = true;
        private bool showInventory = true;
        private bool showUpgrades = true;
        
        // Editor GUI styles
        private GUIStyle headerStyle;
        private GUIStyle boxStyle;
        private GUIStyle helpBoxStyle;
        private GUIStyle warningStyle;
        private GUIStyle buttonStyle;
        
        private void OnEnable()
        {
            troopData = (MBTroopData)target;
            
            // Initialize arrays if null
            if (troopData.Inventory == null)
                troopData.Inventory = new string[0];
            if (troopData.UpgradePaths == null)
                troopData.UpgradePaths = new string[0];
        }
        
        private void InitStyles()
        {
            if (headerStyle == null)
            {
                headerStyle = new GUIStyle(EditorStyles.boldLabel)
                {
                    fontSize = 13,
                    margin = new RectOffset(0, 0, 10, 5)
                };
            }
            
            if (boxStyle == null)
            {
                boxStyle = new GUIStyle(EditorStyles.helpBox)
                {
                    padding = new RectOffset(10, 10, 10, 10)
                };
            }
            
            if (helpBoxStyle == null)
            {
                helpBoxStyle = new GUIStyle(EditorStyles.helpBox)
                {
                    richText = true,
                    fontSize = 11
                };
            }

            if (warningStyle == null)
            {
                warningStyle = new GUIStyle(EditorStyles.helpBox)
                {
                    richText = true,
                    fontSize = 11,
                    normal = { textColor = new Color(0.8f, 0.5f, 0.1f) }
                };
            }
            
            if (buttonStyle == null)
            {
                buttonStyle = new GUIStyle(GUI.skin.button)
                {
                    fontSize = 12,
                    fontStyle = FontStyle.Bold,
                    fixedHeight = 28
                };
            }
        }
        
        public override void OnInspectorGUI()
        {
            InitStyles();
            serializedObject.Update();
            
            // Title Bar
            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField("Mount & Blade Troop Editor", new GUIStyle(EditorStyles.boldLabel) 
            { 
                fontSize = 16, 
                alignment = TextAnchor.MiddleCenter 
            });
            EditorGUILayout.Space(10);
            
            // Quick Info Bar
            DrawQuickInfoBar();
            
            EditorGUILayout.Space(5);
            
            // BASIC INFORMATION (Tuple Fields 1-3, 7)
            showBasicInfo = EditorGUILayout.BeginFoldoutHeaderGroup(showBasicInfo, "📋 Basic Information");
            if (showBasicInfo)
            {
                EditorGUILayout.BeginVertical(boxStyle);
                
                EditorGUILayout.LabelField("Troop Identity", EditorStyles.boldLabel);
                troopData.TroopID = EditorGUILayout.TextField(
                    new GUIContent("Troop ID", "Unique identifier. Prefix 'trp_' is automatically added."), 
                    troopData.TroopID);
                
                troopData.TroopName = EditorGUILayout.TextField(
                    new GUIContent("Troop Name", "Display name for singular form."), 
                    troopData.TroopName);
                
                troopData.TroopNamePlural = EditorGUILayout.TextField(
                    new GUIContent("Plural Name", "Display name for multiple units (e.g., 'Knights' for 'Knight')."), 
                    troopData.TroopNamePlural);
                
                EditorGUILayout.Space(5);
                EditorGUILayout.LabelField("Faction", EditorStyles.boldLabel);
                troopData.FactionID = EditorGUILayout.TextField(
                    new GUIContent("Faction ID", "Faction this troop belongs to (e.g., fac_kingdom_1)."), 
                    troopData.FactionID);
                
                EditorGUILayout.Space(3);
                EditorGUILayout.HelpBox(
                    "Basic troop identification and faction assignment.\n" +
                    "Heroes (unique NPCs) typically use the same name for both singular and plural.",
                    MessageType.None);
                
                EditorGUILayout.EndVertical();
            }
            EditorGUILayout.EndFoldoutHeaderGroup();
            
            EditorGUILayout.Space(5);
            
            // TROOP FLAGS (Tuple Field 4)
            showBasicInfo = EditorGUILayout.BeginFoldoutHeaderGroup(showBasicInfo, "🚩 Troop Flags");
            if (showBasicInfo)
            {
                EditorGUILayout.BeginVertical(boxStyle);
                
                EditorGUILayout.LabelField("Raw Flags Value:", EditorStyles.boldLabel);
                troopData.TroopFlags = EditorGUILayout.TextField(
                    new GUIContent("Flags", "Combined flag values (e.g., tf_hero|tf_guarantee_armor)"), 
                    troopData.TroopFlags);
                
                EditorGUILayout.Space(5);
                
                if (GUILayout.Button("⚙️ Flags Editor", buttonStyle))
                {
                    TroopFlagPropertiesWindow.ShowWindow(troopData);
                }
                
                EditorGUILayout.EndVertical();
            }
            EditorGUILayout.EndFoldoutHeaderGroup();
            
            EditorGUILayout.Space(5);
            
            // SCENE & ENTRY POINT (Tuple Fields 5-6)
            showSceneInfo = EditorGUILayout.BeginFoldoutHeaderGroup(showSceneInfo, "🏰 Scene Assignment");
            if (showSceneInfo)
            {
                EditorGUILayout.BeginVertical(boxStyle);
                
                troopData.Scene = EditorGUILayout.TextField(
                    new GUIContent("Scene", "Scene where hero spawns (e.g., scn_reyvadin_castle). Use 'no_scene' for regular troops."), 
                    troopData.Scene);
                
                troopData.EntryPoint = EditorGUILayout.IntField(
                    new GUIContent("Entry Point", "Scene entry point number. Use -1 or 0 for none."), 
                    troopData.EntryPoint);
                
                EditorGUILayout.EndVertical();
            }
            EditorGUILayout.EndFoldoutHeaderGroup();
            
            EditorGUILayout.Space(5);
            
            // ATTRIBUTES, PROFICIENCIES & SKILLS (Tuple Fields 9-11)
            showAttributes = EditorGUILayout.BeginFoldoutHeaderGroup(showAttributes, "💪 Attributes & Skills");
            if (showAttributes)
            {
                EditorGUILayout.BeginVertical(boxStyle);
                
                // ATTRIBUTES
                EditorGUILayout.LabelField("Attributes", EditorStyles.boldLabel);
                troopData.TroopAttributes = EditorGUILayout.TextField(
                    new GUIContent("Attributes", "Format: str_X|agi_X|int_X|cha_X|level(X)"), 
                    troopData.TroopAttributes);
                
                if (GUILayout.Button("⚙️ Attributes Editor", buttonStyle))
                {
                    TroopAttributesWindow.ShowWindow(troopData);
                }
                
                EditorGUILayout.Space(5);
                
                // WEAPON PROFICIENCIES
                EditorGUILayout.LabelField("Weapon Proficiencies", EditorStyles.boldLabel);
                troopData.WeaponProficiencies = EditorGUILayout.TextField(
                    new GUIContent("Proficiencies", "Format: wp_one_handed(X)|wp_two_handed(X)|wp_polearm(X)|..."), 
                    troopData.WeaponProficiencies);
                
                if (GUILayout.Button("⚙️ Proficiencies Editor", buttonStyle))
                {
                    TroopWeaponProficienciesWindow.ShowWindow(troopData);
                }
                
                EditorGUILayout.Space(5);
                
                // SKILLS
                EditorGUILayout.LabelField("Skills", EditorStyles.boldLabel);
                troopData.TroopSkills = EditorGUILayout.TextField(
                    new GUIContent("Skills", "Format: knows_ironflesh_X|knows_power_strike_X|..."), 
                    troopData.TroopSkills);
                
                if (GUILayout.Button("⚙️ Skills Editor", buttonStyle))
                {
                    TroopSkillsWindow.ShowWindow(troopData);
                }
                
                EditorGUILayout.EndVertical();
            }
            EditorGUILayout.EndFoldoutHeaderGroup();
            
            EditorGUILayout.Space(5);
            
            // FACE CODES (Tuple Fields 12-13)
            showFaceCodes = EditorGUILayout.BeginFoldoutHeaderGroup(showFaceCodes, "👤 Face Codes");
            if (showFaceCodes)
            {
                EditorGUILayout.BeginVertical(boxStyle);
                
                EditorGUILayout.LabelField("Face Randomization", EditorStyles.boldLabel);
                
                troopData.FaceCode1 = EditorGUILayout.TextField(
                    new GUIContent("Face Code 1", "Primary face code. For heroes, this is the only face used."), 
                    troopData.FaceCode1);
                
                troopData.FaceCode2 = EditorGUILayout.TextField(
                    new GUIContent("Face Code 2", "Secondary face code. Game randomizes between Face 1 and Face 2 for regular troops."), 
                    troopData.FaceCode2);
                
                EditorGUILayout.Space(3);
                
                if (GUILayout.Button("🎲 Generate Face Code (In-Game)"))
                {
                }
                
                EditorGUILayout.Space(3);
                EditorGUILayout.HelpBox(
                    "Heroes: Only use Face Code 1 (set Face Code 2 to same value or 0).\n" +
                    "Regular Troops: Game randomizes faces between Code 1 and Code 2.\n" +
                    "Note: Randomization requires more than 2 skins in module_skins.py.",
                    MessageType.Info);
                
                EditorGUILayout.EndVertical();
            }
            EditorGUILayout.EndFoldoutHeaderGroup();
            
            EditorGUILayout.Space(5);
            
            // TROOP IMAGE (Tuple Field 14)
            showVisuals = EditorGUILayout.BeginFoldoutHeaderGroup(showVisuals, "🎨 Visual Settings");
            if (showVisuals)
            {
                EditorGUILayout.BeginVertical(boxStyle);
                
                EditorGUILayout.LabelField("Dialogue Portrait", EditorStyles.boldLabel);
                troopData.TroopImageMesh = EditorGUILayout.TextField(
                    new GUIContent("Troop Image Mesh", "Optional: 2D mesh to display during dialogue instead of 3D character."), 
                    troopData.TroopImageMesh);
                
                EditorGUILayout.Space(3);
                EditorGUILayout.HelpBox(
                    "Leave empty to show 3D character in dialogue.\n" +
                    "Set to mesh name (from .brf) to display static portrait instead.\n" +
                    "Mesh should be 2D planar, rendered orthographically.",
                    MessageType.Info);
                
                EditorGUILayout.EndVertical();
            }
            EditorGUILayout.EndFoldoutHeaderGroup();
            
            EditorGUILayout.Space(5);
            
            // INVENTORY (Tuple Field 8)
            showInventory = EditorGUILayout.BeginFoldoutHeaderGroup(showInventory, "🎒 Inventory");
            if (showInventory)
            {
                EditorGUILayout.BeginVertical(boxStyle);
                
                DrawInventoryList();
                
                EditorGUILayout.Space(3);
                EditorGUILayout.HelpBox(
                    "List of item IDs the troop can spawn with (max 64 items).\n" +
                    "Equipment selection is influenced by troop flags and DNA.\n" +
                    "Guarantee flags (tf_guarantee_armor, etc.) ensure specific item types are equipped.",
                    MessageType.None);
                
                EditorGUILayout.EndVertical();
            }
            EditorGUILayout.EndFoldoutHeaderGroup();
            
            EditorGUILayout.Space(5);
            
            // UPGRADE PATHS
            showUpgrades = EditorGUILayout.BeginFoldoutHeaderGroup(showUpgrades, "⬆️ Upgrade Paths");
            if (showUpgrades)
            {
                EditorGUILayout.BeginVertical(boxStyle);
                
                DrawUpgradePathsList();
                
                EditorGUILayout.Space(3);
                EditorGUILayout.HelpBox(
                    "Troops this unit can upgrade into (max 2 paths).\n" +
                    "First path: primary upgrade\n" +
                    "Second path: alternative upgrade choice\n" +
                    "Leave empty if this is a top-tier troop.",
                    MessageType.None);
                
                EditorGUILayout.EndVertical();
            }
            EditorGUILayout.EndFoldoutHeaderGroup();
            
            EditorGUILayout.Space(10);
            
            // Save changes
            if (GUI.changed)
            {
                EditorUtility.SetDirty(troopData);
                serializedObject.ApplyModifiedProperties();
            }
        }
        
        // QUICK INFO BAR
        private void DrawQuickInfoBar()
        {
            EditorGUILayout.BeginVertical(boxStyle);
            
            GUIStyle infoStyle = new GUIStyle(EditorStyles.label)
            {
                fontSize = 11,
                richText = true
            };
            
            string troopType = "Regular Troop";
            if (!string.IsNullOrEmpty(troopData.TroopFlags))
            {
                if (troopData.TroopFlags.Contains("tf_hero"))
                    troopType = "Hero/Unique NPC";
                else if (troopData.TroopFlags.Contains("tf_mounted"))
                    troopType = "Cavalry";
                else if (troopData.TroopFlags.Contains("tf_guarantee_ranged"))
                    troopType = "Archer/Ranged";
            }
            
            EditorGUILayout.LabelField(
                $"ID: {troopData.TroopID} | " +
                $"Type: {troopType} | " +
                $"Faction: {(string.IsNullOrEmpty(troopData.FactionID) ? "None" : troopData.FactionID)}",
                infoStyle);
            
            EditorGUILayout.LabelField(
                $"Inventory Items: {troopData.Inventory.Length} | " +
                $"Upgrade Paths: {troopData.UpgradePaths.Length}",
                infoStyle);
            
            EditorGUILayout.EndVertical();
        }
        
        // INVENTORY LIST DRAWER
        private void DrawInventoryList()
        {
            EditorGUILayout.LabelField($"Items ({troopData.Inventory.Length}/64):", EditorStyles.boldLabel);
            
            if (troopData.Inventory.Length == 0)
            {
                EditorGUILayout.HelpBox("No items in inventory. Troop will spawn unarmed and unarmored.", MessageType.Warning);
            }
            
            if (troopData.Inventory.Length >= 64)
            {
                EditorGUILayout.HelpBox("Maximum inventory size reached (64 items).", MessageType.Warning);
            }
            
            // Draw item list
            for (int i = 0; i < troopData.Inventory.Length; i++)
            {
                EditorGUILayout.BeginHorizontal();
                
                EditorGUILayout.LabelField($"{i + 1}.", GUILayout.Width(30));
                
                troopData.Inventory[i] = EditorGUILayout.TextField(
                    troopData.Inventory[i],
                    GUILayout.ExpandWidth(true));
                
                // Move up button
                GUI.enabled = i > 0;
                if (GUILayout.Button("▲", GUILayout.Width(25)))
                {
                    string temp = troopData.Inventory[i];
                    troopData.Inventory[i] = troopData.Inventory[i - 1];
                    troopData.Inventory[i - 1] = temp;
                }
                GUI.enabled = true;
                
                // Move down button
                GUI.enabled = i < troopData.Inventory.Length - 1;
                if (GUILayout.Button("▼", GUILayout.Width(25)))
                {
                    string temp = troopData.Inventory[i];
                    troopData.Inventory[i] = troopData.Inventory[i + 1];
                    troopData.Inventory[i + 1] = temp;
                }
                GUI.enabled = true;
                
                // Remove button
                if (GUILayout.Button("✖", GUILayout.Width(25)))
                {
                    RemoveInventoryItem(i);
                    break;
                }
                
                EditorGUILayout.EndHorizontal();
            }
            
            EditorGUILayout.Space(3);
            
            // Add item button
            GUI.enabled = troopData.Inventory.Length < 64;
            if (GUILayout.Button("➕ Add Item"))
            {
                AddInventoryItem();
            }
            GUI.enabled = true;
            
            // Bulk operations
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Clear All") && troopData.Inventory.Length > 0)
            {
                if (EditorUtility.DisplayDialog("Clear Inventory", 
                    "Remove all items from inventory?", "Yes", "Cancel"))
                {
                    troopData.Inventory = new string[0];
                }
            }
            
            if (GUILayout.Button("Add Multiple"))
            {
                // TODO: Could open a multi-item picker window
                for (int i = 0; i < 5 && troopData.Inventory.Length < 64; i++)
                {
                    AddInventoryItem();
                }
            }
            EditorGUILayout.EndHorizontal();
        }
        
        private void AddInventoryItem()
        {
            var newList = new string[troopData.Inventory.Length + 1];
            troopData.Inventory.CopyTo(newList, 0);
            newList[newList.Length - 1] = "";
            troopData.Inventory = newList;
        }
        
        private void RemoveInventoryItem(int index)
        {
            var newList = new string[troopData.Inventory.Length - 1];
            for (int i = 0, j = 0; i < troopData.Inventory.Length; i++)
            {
                if (i != index)
                {
                    newList[j++] = troopData.Inventory[i];
                }
            }
            troopData.Inventory = newList;
        }
        
        // UPGRADE PATHS LIST DRAWER
        private void DrawUpgradePathsList()
        {
            EditorGUILayout.LabelField($"Upgrade Options ({troopData.UpgradePaths.Length}/2):", EditorStyles.boldLabel);
            
            if (troopData.UpgradePaths.Length == 0)
            {
                EditorGUILayout.HelpBox("No upgrade paths defined. This is a top-tier troop.", MessageType.Info);
            }
            
            if (troopData.UpgradePaths.Length >= 2)
            {
                EditorGUILayout.HelpBox("Maximum upgrade paths reached (2). Engine limitation.", MessageType.Info);
            }
            
            // Draw upgrade paths
            for (int i = 0; i < troopData.UpgradePaths.Length; i++)
            {
                EditorGUILayout.BeginHorizontal();
                
                string label = i == 0 ? "Primary:" : "Alternative:";
                EditorGUILayout.LabelField(label, GUILayout.Width(80));
                
                troopData.UpgradePaths[i] = EditorGUILayout.TextField(
                    troopData.UpgradePaths[i],
                    GUILayout.ExpandWidth(true));
                
                // Remove button
                if (GUILayout.Button("✖", GUILayout.Width(25)))
                {
                    RemoveUpgradePath(i);
                    break;
                }
                
                EditorGUILayout.EndHorizontal();
            }
            
            EditorGUILayout.Space(3);
            
            // Add upgrade path button
            GUI.enabled = troopData.UpgradePaths.Length < 2;
            if (GUILayout.Button("➕ Add Upgrade Path"))
            {
                AddUpgradePath();
            }
            GUI.enabled = true;
            
            // Clear button
            if (troopData.UpgradePaths.Length > 0 && GUILayout.Button("Clear All Paths"))
            {
                if (EditorUtility.DisplayDialog("Clear Upgrade Paths", 
                    "Remove all upgrade paths?", "Yes", "Cancel"))
                {
                    troopData.UpgradePaths = new string[0];
                }
            }
        }
        
        private void AddUpgradePath()
        {
            var newList = new string[troopData.UpgradePaths.Length + 1];
            troopData.UpgradePaths.CopyTo(newList, 0);
            newList[newList.Length - 1] = "";
            troopData.UpgradePaths = newList;
        }
        
        private void RemoveUpgradePath(int index)
        {
            var newList = new string[troopData.UpgradePaths.Length - 1];
            for (int i = 0, j = 0; i < troopData.UpgradePaths.Length; i++)
            {
                if (i != index)
                {
                    newList[j++] = troopData.UpgradePaths[i];
                }
            }
            troopData.UpgradePaths = newList;
        }
    }
}
