using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using UnityEditor;
using UnityEngine;
using MountAndBlade.Data;
using MountAndBladeTools;
using Vector2 = UnityEngine.Vector2;
using Vector3 = UnityEngine.Vector3;

namespace MountAndBladeEditor
{
    /// <summary>
    /// Enhanced custom editor for Mount & Blade Party Data
    /// User-friendly interface with interactive controls, sliders, and templates
    /// </summary>
    [CustomEditor(typeof(MBPartyData))]
    public class MBPartyDataEditor : Editor
    {
        // Foldout states
        private bool showBasicInfo = true;
        private bool showFlags = true;
        private bool showFactionPersonality = true;
        private bool showAIBehavior = true;
        private bool showPosition = true;
        private bool showTroops = true;
        private bool showTemplates = false;
        private bool showAdvanced = false;
        private bool showValidation = false;
        
        // Flag checkboxes state
        private Dictionary<string, bool> flagStates = new Dictionary<string, bool>();
        
        // Personality component values
        private int courageLevel = 8;
        private int aggressivenessLevel = 8;
        private bool isBandit = false;
        
        // AI behavior selection
        private int selectedBehaviorIndex = 0;
        private string[] behaviorNames;
        private int[] behaviorValues;
        
        // Decoded data cache
        private PartyFlagsResult decodedFlags;
        private PersonalityResult decodedPersonality;
        
        // Validation state
        private List<string> validationWarnings = new List<string>();
        private List<string> validationErrors = new List<string>();
        
        // Styles
        private GUIStyle headerStyle;
        private GUIStyle warningStyle;
        private GUIStyle errorStyle;
        private GUIStyle infoStyle;
        private GUIStyle successStyle;
        private GUIStyle sectionBoxStyle;
        private bool stylesInitialized = false;

        private void OnEnable()
        {
            InitializeBehaviorArrays();
        }

        private void InitializeStyles()
        {
            if (stylesInitialized) return;
            
            headerStyle = new GUIStyle(EditorStyles.foldoutHeader)
            {
                fontStyle = FontStyle.Bold,
                fontSize = 12
            };
            
            warningStyle = new GUIStyle(EditorStyles.helpBox)
            {
                normal = { textColor = new Color(0.8f, 0.6f, 0.2f) }
            };
            
            errorStyle = new GUIStyle(EditorStyles.helpBox)
            {
                normal = { textColor = new Color(0.8f, 0.2f, 0.2f) }
            };
            
            infoStyle = new GUIStyle(EditorStyles.helpBox)
            {
                normal = { textColor = new Color(0.3f, 0.6f, 0.8f) }
            };
            
            successStyle = new GUIStyle(EditorStyles.helpBox)
            {
                normal = { textColor = new Color(0.3f, 0.8f, 0.3f) }
            };
            
            sectionBoxStyle = new GUIStyle(EditorStyles.helpBox)
            {
                padding = new RectOffset(10, 10, 10, 10)
            };
            
            stylesInitialized = true;
        }

        private void InitializeBehaviorArrays()
        {
            var behaviors = PartyAIBehaviorDecoder.GetAllBehaviors();
            behaviorNames = new string[behaviors.Count];
            behaviorValues = new int[behaviors.Count];
            
            int i = 0;
            foreach (var kvp in behaviors.OrderBy(x => x.Key))
            {
                behaviorValues[i] = kvp.Key;
                behaviorNames[i] = $"{kvp.Key}: {kvp.Value}";
                i++;
            }
        }

        public override void OnInspectorGUI()
        {
            InitializeStyles();
            
            MBPartyData partyData = (MBPartyData)target;
            serializedObject.Update();
            
            // Header
            DrawHeader();
            
            // Decode current values and sync UI
            DecodeAndSyncValues(partyData);
            
            // Quick Templates Section
            DrawTemplatesSection(partyData);
            
            // Main sections
            DrawBasicInfoSection(partyData);
            DrawFlagsSection(partyData);
            DrawFactionPersonalitySection(partyData);
            DrawAIBehaviorSection(partyData);
            DrawPositionSection(partyData);
            DrawTroopsSection(partyData);
            DrawAdvancedSection(partyData);
            DrawValidationSection(partyData);
            
            serializedObject.ApplyModifiedProperties();
            
            if (GUI.changed)
            {
                EditorUtility.SetDirty(partyData);
            }
        }

        private void DrawHeader()
        {
            EditorGUILayout.Space(5);
            EditorGUILayout.BeginVertical(sectionBoxStyle);
            
            GUIStyle titleStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 14,
                alignment = TextAnchor.MiddleCenter
            };
            
            EditorGUILayout.LabelField("⚔ Mount & Blade Party Editor ⚔", titleStyle);
            EditorGUILayout.LabelField("Configure party data with interactive controls", EditorStyles.centeredGreyMiniLabel);
            EditorGUILayout.Space(3);
            EditorGUILayout.LabelField("Module System: module_parties.py tuple builder", EditorStyles.centeredGreyMiniLabel);
            EditorGUILayout.LabelField("Static parties (towns/castles/villages) and mobile parties (patrols/bandits/caravans)", EditorStyles.centeredGreyMiniLabel);
            
            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(5);
        }

        private void DecodeAndSyncValues(MBPartyData partyData)
        {
            // Decode flags
            try
            {
                decodedFlags = PartyDecoder.DecodeFlags(partyData.flags);
                SyncFlagStates();
            }
            catch
            {
                decodedFlags = null;
            }
            
            // Decode and sync personality
            try
            {
                decodedPersonality = PartyDecoder.DecodePersonality(partyData.personality);
                if (decodedPersonality != null)
                {
                    courageLevel = decodedPersonality.CourageValue;
                    aggressivenessLevel = decodedPersonality.AggressivenessLevel;
                    isBandit = decodedPersonality.IsBandit;
                }
            }
            catch
            {
                decodedPersonality = null;
            }
            
            // Sync AI behavior selection
            try
            {
                int behaviorValue = string.IsNullOrEmpty(partyData.aiBehavior) ? 0 : int.Parse(partyData.aiBehavior);
                selectedBehaviorIndex = Array.IndexOf(behaviorValues, behaviorValue);
                if (selectedBehaviorIndex < 0) selectedBehaviorIndex = 0;
            }
            catch
            {
                selectedBehaviorIndex = 0;
            }
            
            // Run validation
            ValidatePartyData(partyData);
        }

        private void SyncFlagStates()
        {
            flagStates.Clear();
            
            var allFlags = PartyFlagsDecoder.GetAllFlags();
            foreach (var flag in allFlags)
            {
                if (flag.Value == 0) continue; // Skip pf_label_small
                
                bool isSet = decodedFlags != null && decodedFlags.ActiveFlags.Contains(flag.Key);
                flagStates[flag.Key] = isSet;
            }
        }

        private void DrawTemplatesSection(MBPartyData partyData)
        {
            showTemplates = EditorGUILayout.BeginFoldoutHeaderGroup(showTemplates, "🎨 Quick Templates");
            
            if (showTemplates)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.BeginVertical(sectionBoxStyle);
                
                EditorGUILayout.HelpBox("Quick templates apply common party configurations:\n\n" +
                    "Static Locations (Towns/Castles/Villages):\n" +
                    "• Use pf_is_static | pf_always_visible | pf_show_faction\n" +
                    "• Set ai_bhvr_hold (0) and valid coordinates\n" +
                    "• Usually start as fac_neutral, assigned to factions via scripts\n\n" +
                    "Mobile Parties (Patrols/Bandits/Caravans):\n" +
                    "• Configure appropriate AI behavior and personality\n" +
                    "• Caravans use pf_auto_remove_in_town\n" +
                    "• Civilians should have pf_civilian flag", 
                    MessageType.Info);
                
                EditorGUILayout.Space(5);
                EditorGUILayout.LabelField("Location Types:", EditorStyles.boldLabel);
                
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("🏰 Town", GUILayout.Height(30)))
                {
                    ApplyTownTemplate(partyData);
                }
                if (GUILayout.Button("🏯 Castle", GUILayout.Height(30)))
                {
                    ApplyCastleTemplate(partyData);
                }
                if (GUILayout.Button("🏘️ Village", GUILayout.Height(30)))
                {
                    ApplyVillageTemplate(partyData);
                }
                EditorGUILayout.EndHorizontal();
                
                EditorGUILayout.Space(5);
                EditorGUILayout.LabelField("Mobile Parties:", EditorStyles.boldLabel);
                
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("⚔️ Patrol", GUILayout.Height(30)))
                {
                    ApplyPatrolTemplate(partyData);
                }
                if (GUILayout.Button("🏴‍☠️ Bandit", GUILayout.Height(30)))
                {
                    ApplyBanditTemplate(partyData);
                }
                if (GUILayout.Button("🛒 Caravan", GUILayout.Height(30)))
                {
                    ApplyCaravanTemplate(partyData);
                }
                EditorGUILayout.EndHorizontal();
                
                EditorGUILayout.Space(5);
                
                EditorGUILayout.BeginVertical(warningStyle);
                EditorGUILayout.LabelField("⚠️ Module System Placement", EditorStyles.boldLabel);
                EditorGUILayout.LabelField("IMPORTANT: When adding to module_parties.py:", EditorStyles.wordWrappedLabel);
                EditorGUILayout.LabelField("• DO NOT add new parties at the bottom of the file!", EditorStyles.wordWrappedLabel);
                EditorGUILayout.LabelField("• Towns: Add between 'town_1' and 'castle_1'", EditorStyles.wordWrappedLabel);
                EditorGUILayout.LabelField("• Castles: Add in the castles section", EditorStyles.wordWrappedLabel);
                EditorGUILayout.LabelField("• Villages: Add in the villages section", EditorStyles.wordWrappedLabel);
                EditorGUILayout.LabelField("• Check module_constants.py for defined ranges", EditorStyles.wordWrappedLabel);
                EditorGUILayout.LabelField("Adding in wrong location can break native code!", EditorStyles.wordWrappedLabel);
                EditorGUILayout.EndVertical();
                
                EditorGUILayout.Space(5);
                if (GUILayout.Button("Clear All Settings", GUILayout.Height(25)))
                {
                    if (EditorUtility.DisplayDialog("Clear Party Data", 
                        "This will reset all party settings to default values. Continue?", 
                        "Yes", "No"))
                    {
                        ClearPartyData(partyData);
                    }
                }
                
                EditorGUILayout.EndVertical();
                EditorGUI.indentLevel--;
            }
            
            EditorGUILayout.EndFoldoutHeaderGroup();
            EditorGUILayout.Space(3);
        }

        private void DrawBasicInfoSection(MBPartyData partyData)
        {
            showBasicInfo = EditorGUILayout.BeginFoldoutHeaderGroup(showBasicInfo, "📋 Basic Information");
            
            if (showBasicInfo)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.BeginVertical(sectionBoxStyle);
                
                // Party ID with validation indicator
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(new GUIContent("Party ID", "Unique identifier (prefix p_ added automatically)"), GUILayout.Width(150));
                
                Color originalColor = GUI.backgroundColor;
                if (string.IsNullOrEmpty(partyData.partyId))
                    GUI.backgroundColor = new Color(1f, 0.5f, 0.5f);
                
                partyData.partyId = EditorGUILayout.TextField(partyData.partyId);
                GUI.backgroundColor = originalColor;
                
                if (!string.IsNullOrEmpty(partyData.partyId))
                {
                    EditorGUILayout.LabelField("✓", GUILayout.Width(20));
                }
                else
                {
                    EditorGUILayout.LabelField("⚠", GUILayout.Width(20));
                }
                EditorGUILayout.EndHorizontal();
                
                // Party Name
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(new GUIContent("Party Name", "Display name shown in-game"), GUILayout.Width(150));
                partyData.partyName = EditorGUILayout.TextField(partyData.partyName);
                EditorGUILayout.EndHorizontal();
                
                EditorGUILayout.Space(5);
                
                // Map Icon with preview
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(new GUIContent("Map Icon", "Icon shown on world map"), GUILayout.Width(150));
                partyData.mapIcon = EditorGUILayout.TextField(partyData.mapIcon);
                EditorGUILayout.EndHorizontal();
                
                // Icon ID display
                if (decodedFlags != null)
                {
                    EditorGUI.indentLevel++;
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField("Icon ID:", GUILayout.Width(140));
                    EditorGUILayout.LabelField($"{decodedFlags.IconId}", EditorStyles.boldLabel);
                    EditorGUILayout.EndHorizontal();
                    EditorGUI.indentLevel--;
                }
                
                EditorGUILayout.EndVertical();
                EditorGUI.indentLevel--;
            }
            
            EditorGUILayout.EndFoldoutHeaderGroup();
            EditorGUILayout.Space(3);
        }

        private void DrawFlagsSection(MBPartyData partyData)
        {
            showFlags = EditorGUILayout.BeginFoldoutHeaderGroup(showFlags, "🚩 Party Flags");
            
            if (showFlags)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.BeginVertical(sectionBoxStyle);
                
                // Icon ID slider
                EditorGUILayout.LabelField(new GUIContent("Icon Configuration", 
                    "The first 8 bits (0-7) of party flags encode the icon ID (0-255). This determines which map icon is displayed. Icons must be defined in your module's icon resources."), 
                    EditorStyles.boldLabel);
                int iconId = decodedFlags?.IconId ?? 0;
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(new GUIContent("Icon ID (0-255):", 
                    "Map icon identifier. Bits 0-7 store the icon as an encoded value (not bitwise). Must match an icon entry from your module. Example: icon_town, icon_castle, icon_village."), 
                    GUILayout.Width(150));
                int newIconId = EditorGUILayout.IntSlider(iconId, 0, 255);
                if (newIconId != iconId)
                {
                    UpdateIconId(partyData, newIconId);
                }
                EditorGUILayout.EndHorizontal();
                
                EditorGUILayout.Space(10);
                
                // Basic Flags
                EditorGUILayout.LabelField("Basic Flags", EditorStyles.boldLabel);
                DrawFlagCheckbox(partyData, "pf_disabled", "Disabled", 
                    "Hides the party on the world map. Used for bandit spawnpoints or temporary parties. Locations still show up in the factions list. Scripts and try_for_parties-loops still see it.");
                DrawFlagCheckbox(partyData, "pf_is_ship", "Is Ship", 
                    "Enables the party to travel on water. Passable terrains are: rt_water, rt_river and rt_bridge. Ship speed is 15.0 * speed multiplier.");
                DrawFlagCheckbox(partyData, "pf_is_static", "Static", 
                    "Marks a party which will not move, like locations (towns, castles, villages). Essential for fixed map locations.");
                
                EditorGUILayout.Space(5);
                
                // Label Size (mutually exclusive)
                EditorGUILayout.LabelField(new GUIContent("Label Size", 
                    "Controls the size of the party name label on the world map. Small for villages, Medium for castles, Large for towns."), 
                    EditorStyles.boldLabel);
                int labelSize = GetLabelSize();
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Toggle(labelSize == 0, new GUIContent("Small", "Village-sized label (pf_label_small)"))) SetLabelSize(partyData, 0);
                if (GUILayout.Toggle(labelSize == 1, new GUIContent("Medium", "Castle-sized label (pf_label_medium)"))) SetLabelSize(partyData, 1);
                if (GUILayout.Toggle(labelSize == 2, new GUIContent("Large", "Town-sized label (pf_label_large)"))) SetLabelSize(partyData, 2);
                EditorGUILayout.EndHorizontal();
                
                EditorGUILayout.Space(5);
                
                // Visibility Flags
                EditorGUILayout.LabelField("Visibility & Display", EditorStyles.boldLabel);
                DrawFlagCheckbox(partyData, "pf_always_visible", "Always Visible", 
                    "Makes villages, castles and towns always visible on the world map to the player. AI parties ignore this flag. Without this, the player only sees the party within spotting range.");
                DrawFlagCheckbox(partyData, "pf_no_label", "No Label", 
                    "Hides the party name on the world map and prevents the player from interacting with the party directly.");
                DrawFlagCheckbox(partyData, "pf_show_faction", "Show Faction", 
                    "Shows the faction name of the party. Without this flag, the party will appear like looters and manhunters (no faction affiliation).");
                DrawFlagCheckbox(partyData, "pf_hide_defenders", "Hide Defenders", 
                    "Hides the garrison numbers on the world map tooltip. Used for villages and some quest parties.");
                
                EditorGUILayout.Space(5);
                
                // Behavior Flags
                EditorGUILayout.LabelField("Behavior Flags", EditorStyles.boldLabel);
                DrawFlagCheckbox(partyData, "pf_default_behavior", "Default Behavior", 
                    "Turns off AI behavior control. When set, the AI behavior can be overridden by scripts. When unset, the game engine controls AI decisions like attacking or retreating.");
                DrawFlagCheckbox(partyData, "pf_auto_remove_in_town", "Auto Remove in Town", 
                    "Disbands traveling parties after reaching a town. Used for caravans, survivors after battle (routed), messengers, etc.");
                DrawFlagCheckbox(partyData, "pf_quest_party", "Quest Party", 
                    "Marks this party as being involved in a quest. Used for the player party during tutorial and new game quest chain. Can be set/unset via scripts.");
                DrawFlagCheckbox(partyData, "pf_limit_members", "Limit Members", 
                    "Party size is restricted according to the leadership skill of the party leader. In Native only used for the player party. Ignored by party_force_add_members and party_force_add_prisoners operations.");
                
                EditorGUILayout.Space(5);
                
                // Combat Flags
                EditorGUILayout.LabelField("Combat & Type", EditorStyles.boldLabel);
                DrawFlagCheckbox(partyData, "pf_civilian", "Civilian", 
                    "Marks a party as civilian so that parties with pf_dont_attack_civilians will not attack it. Used for caravans, villagers, and peaceful travelers.");
                DrawFlagCheckbox(partyData, "pf_dont_attack_civilians", "Don't Attack Civilians", 
                    "Prevents this party from attacking parties marked with the pf_civilian flag. Used for honorable parties and faction armies.");
                
                EditorGUILayout.Space(10);
                
                // Raw value display
                EditorGUILayout.BeginVertical(infoStyle);
                EditorGUILayout.LabelField("Raw Flags Value:", EditorStyles.miniBoldLabel);
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.SelectableLabel(partyData.flags, GUILayout.Height(18));
                if (GUILayout.Button("Copy", GUILayout.Width(50)))
                {
                    EditorGUIUtility.systemCopyBuffer = partyData.flags;
                }
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.EndVertical();
                
                EditorGUILayout.EndVertical();
                EditorGUI.indentLevel--;
            }
            
            EditorGUILayout.EndFoldoutHeaderGroup();
            EditorGUILayout.Space(3);
        }

        private void DrawFactionPersonalitySection(MBPartyData partyData)
        {
            showFactionPersonality = EditorGUILayout.BeginFoldoutHeaderGroup(showFactionPersonality, "⚔️ Faction & Personality");
            
            if (showFactionPersonality)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.BeginVertical(sectionBoxStyle);
                
                // Faction
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(new GUIContent("Faction", 
                    "Faction from module_factions.py (e.g., fac_kingdom_1, fac_player_supporters_faction). Towns/castles/villages usually start as fac_neutral and get assigned during script_game_start. Mobile parties use their kingdom's faction."), 
                    GUILayout.Width(150));
                partyData.faction = EditorGUILayout.TextField(partyData.faction);
                EditorGUILayout.EndHorizontal();
                
                EditorGUILayout.Space(10);
                
                // Personality with sliders
                EditorGUILayout.LabelField(new GUIContent("Personality Configuration", 
                    "Personality is packed into bits: Courage (bits 0-3), Aggressiveness (bits 4-7), Banditness (bit 8). Affects party AI behavior and combat decisions."), 
                    EditorStyles.boldLabel);
                
                // Courage slider
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.LabelField(new GUIContent("Courage (4-15, neutral=8)", 
                    "Determines how likely the party is to flee from combat. Low courage = more likely to retreat. High courage = will stand and fight. Neutral (8) is standard behavior."), 
                    EditorStyles.miniBoldLabel);
                EditorGUILayout.BeginHorizontal();
                
                int newCourage = (int)EditorGUILayout.Slider(courageLevel, 4, 15);
                if (newCourage != courageLevel)
                {
                    courageLevel = newCourage;
                    UpdatePersonality(partyData);
                }
                
                string courageDesc = courageLevel < 8 ? "Cowardly" : courageLevel > 8 ? "Brave" : "Neutral";
                EditorGUILayout.LabelField($"({courageDesc})", GUILayout.Width(80));
                EditorGUILayout.EndHorizontal();
                
                // Visual courage bar
                DrawValueBar(courageLevel, 4, 15, new Color(0.8f, 0.3f, 0.3f), new Color(0.3f, 0.8f, 0.3f));
                EditorGUILayout.EndVertical();
                
                EditorGUILayout.Space(3);
                
                // Aggressiveness slider
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.LabelField(new GUIContent("Aggressiveness (0-15, neutral=8)", 
                    "Determines how actively the party seeks combat. 0 = very passive (merchants), 15 = very aggressive (raiders). Neutral (8) is standard military behavior."), 
                    EditorStyles.miniBoldLabel);
                EditorGUILayout.BeginHorizontal();
                
                int newAggro = (int)EditorGUILayout.Slider(aggressivenessLevel, 0, 15);
                if (newAggro != aggressivenessLevel)
                {
                    aggressivenessLevel = newAggro;
                    UpdatePersonality(partyData);
                }
                
                string aggroDesc = aggressivenessLevel < 8 ? "Passive" : aggressivenessLevel > 8 ? "Aggressive" : "Neutral";
                EditorGUILayout.LabelField($"({aggroDesc})", GUILayout.Width(80));
                EditorGUILayout.EndHorizontal();
                
                // Visual aggro bar
                DrawValueBar(aggressivenessLevel, 0, 15, new Color(0.3f, 0.6f, 0.8f), new Color(0.8f, 0.3f, 0.3f));
                EditorGUILayout.EndVertical();
                
                EditorGUILayout.Space(3);
                
                // Bandit flag
                EditorGUILayout.BeginHorizontal();
                bool newBandit = EditorGUILayout.Toggle(new GUIContent("Bandit Personality", 
                    "Marks this party as having bandit behavior (bit 8). Bandits are more likely to attack civilians and caravans. Used for hostile NPC parties."), 
                    isBandit);
                if (newBandit != isBandit)
                {
                    isBandit = newBandit;
                    UpdatePersonality(partyData);
                }
                if (isBandit)
                {
                    EditorGUILayout.LabelField("🏴‍☠️", GUILayout.Width(30));
                }
                EditorGUILayout.EndHorizontal();
                
                EditorGUILayout.Space(10);
                
                // Personality presets
                EditorGUILayout.LabelField("Quick Presets:", EditorStyles.boldLabel);
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Soldier"))
                {
                    courageLevel = 9;
                    aggressivenessLevel = 8;
                    isBandit = false;
                    UpdatePersonality(partyData);
                }
                if (GUILayout.Button("Merchant"))
                {
                    courageLevel = 7;
                    aggressivenessLevel = 0;
                    isBandit = false;
                    UpdatePersonality(partyData);
                }
                if (GUILayout.Button("Bandit"))
                {
                    courageLevel = 8;
                    aggressivenessLevel = 3;
                    isBandit = true;
                    UpdatePersonality(partyData);
                }
                if (GUILayout.Button("Neutral"))
                {
                    courageLevel = 8;
                    aggressivenessLevel = 8;
                    isBandit = false;
                    UpdatePersonality(partyData);
                }
                EditorGUILayout.EndHorizontal();
                
                // Display current preset name
                if (decodedPersonality != null && !string.IsNullOrEmpty(decodedPersonality.PresetName))
                {
                    EditorGUILayout.BeginVertical(infoStyle);
                    EditorGUILayout.LabelField($"Current Preset: {decodedPersonality.PresetName}", EditorStyles.boldLabel);
                    EditorGUILayout.EndVertical();
                }
                
                EditorGUILayout.Space(5);
                
                // Raw personality value
                EditorGUILayout.BeginVertical(infoStyle);
                EditorGUILayout.LabelField("Raw Personality Value:", EditorStyles.miniBoldLabel);
                EditorGUILayout.SelectableLabel(partyData.personality, GUILayout.Height(18));
                EditorGUILayout.EndVertical();
                
                EditorGUILayout.EndVertical();
                EditorGUI.indentLevel--;
            }
            
            EditorGUILayout.EndFoldoutHeaderGroup();
            EditorGUILayout.Space(3);
        }

        private void DrawAIBehaviorSection(MBPartyData partyData)
        {
            showAIBehavior = EditorGUILayout.BeginFoldoutHeaderGroup(showAIBehavior, "🤖 AI Behavior");
            
            if (showAIBehavior)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.BeginVertical(sectionBoxStyle);
                
                // Behavior dropdown
                EditorGUILayout.LabelField(new GUIContent("AI Behavior Type", 
                    "Controls how the party moves and acts on the world map. Different behaviors require different targets (party ID or coordinates)."), 
                    EditorStyles.boldLabel);
                
                EditorGUILayout.BeginHorizontal();
                int newBehaviorIndex = EditorGUILayout.Popup(new GUIContent("Behavior", 
                    "Select the AI behavior for this party. See description below for details on what each behavior does."), 
                    selectedBehaviorIndex, behaviorNames);
                if (newBehaviorIndex != selectedBehaviorIndex)
                {
                    selectedBehaviorIndex = newBehaviorIndex;
                    partyData.aiBehavior = behaviorValues[selectedBehaviorIndex].ToString();
                }
                EditorGUILayout.EndHorizontal();
                
                // Display description
                if (selectedBehaviorIndex >= 0 && selectedBehaviorIndex < behaviorValues.Length)
                {
                    EditorGUILayout.BeginVertical(infoStyle);
                    string description = PartyAIBehaviorDecoder.GetDescription(behaviorValues[selectedBehaviorIndex]);
                    EditorGUILayout.LabelField("Description:", EditorStyles.miniBoldLabel);
                    EditorGUILayout.LabelField(description, EditorStyles.wordWrappedLabel);
                    EditorGUILayout.EndVertical();
                }
                
                EditorGUILayout.Space(10);
                
                // AI Target with contextual help
                bool requiresTarget = PartyAIBehaviorDecoder.RequiresTarget(behaviorValues[selectedBehaviorIndex]);
                bool requiresCoords = PartyAIBehaviorDecoder.RequiresCoordinates(behaviorValues[selectedBehaviorIndex]);
                
                EditorGUILayout.LabelField(new GUIContent("AI Target/Destination", 
                    "For party-based behaviors: Enter party ID (e.g., p_town_1). For location-based behaviors: Enter position reference. Leave empty for behaviors that don't require targets."), 
                    EditorStyles.boldLabel);
                
                if (requiresTarget || requiresCoords)
                {
                    string helpText = requiresTarget ? 
                        "This behavior requires a target party ID (e.g., p_town_1)" : 
                        "This behavior requires a position reference or coordinates";
                    
                    EditorGUILayout.HelpBox(helpText, MessageType.Info);
                }
                
                EditorGUILayout.BeginHorizontal();
                
                Color originalColor = GUI.backgroundColor;
                if ((requiresTarget || requiresCoords) && string.IsNullOrEmpty(partyData.aiTarget))
                    GUI.backgroundColor = new Color(1f, 0.8f, 0.5f);
                
                partyData.aiTarget = EditorGUILayout.TextField(partyData.aiTarget);
                GUI.backgroundColor = originalColor;
                
                // Status indicator
                if (requiresTarget || requiresCoords)
                {
                    if (!string.IsNullOrEmpty(partyData.aiTarget))
                    {
                        EditorGUILayout.LabelField("✓", GUILayout.Width(20));
                    }
                    else
                    {
                        EditorGUILayout.LabelField("⚠", GUILayout.Width(20));
                    }
                }
                else
                {
                    EditorGUILayout.LabelField("N/A", GUILayout.Width(40));
                }
                
                EditorGUILayout.EndHorizontal();
                
                EditorGUILayout.EndVertical();
                EditorGUI.indentLevel--;
            }
            
            EditorGUILayout.EndFoldoutHeaderGroup();
            EditorGUILayout.Space(3);
        }

        private void DrawPositionSection(MBPartyData partyData)
        {
            showPosition = EditorGUILayout.BeginFoldoutHeaderGroup(showPosition, "📍 Position & Direction");
            
            if (showPosition)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.BeginVertical(sectionBoxStyle);
                
                EditorGUILayout.HelpBox("Getting Coordinates:\n" +
                    "• In-game: Enable Edit Mode, check 'Show framerate' in launcher, press Ctrl+E to see coordinates\n" +
                    "• Map Editor: Hold Z key and move cursor, release to copy coordinates to clipboard\n" +
                    "• Scripts: Use party_get_position operation with fixed_point_multiplier", MessageType.Info);
                
                // World Position
                EditorGUILayout.LabelField(new GUIContent("World Map Coordinates", 
                    "Party's starting position on the overland map (X, Y). Critical for static parties like towns/castles/villages. Use Edit Mode (Ctrl+E) in-game to find coordinates."), 
                    EditorStyles.boldLabel);
                partyData.worldPosition = EditorGUILayout.Vector2Field(new GUIContent("Position (X, Y)", 
                    "World map coordinates. Example: (-1.55, 66.45) for Sargoth. Required for static parties, optional for mobile parties."), 
                    partyData.worldPosition);
                
                EditorGUILayout.Space(10);
                
                // Direction with visual indicator
                EditorGUILayout.LabelField(new GUIContent("Party Direction", 
                    "Initial facing direction of the party in degrees (0-360). 0° = North, 90° = East, 180° = South, 270° = West. Optional field."), 
                    EditorStyles.boldLabel);
                
                EditorGUILayout.BeginHorizontal();
                partyData.direction = EditorGUILayout.Slider("Degrees", partyData.direction, 0, 359);
                EditorGUILayout.LabelField(GetDirectionLabel(partyData.direction), GUILayout.Width(50));
                EditorGUILayout.EndHorizontal();
                
                // Visual direction indicator
                DrawDirectionCompass(partyData.direction);
                
                EditorGUILayout.Space(5);
                
                // Quick direction buttons
                EditorGUILayout.LabelField("Quick Set:", EditorStyles.miniBoldLabel);
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("⬆ N\n0°")) partyData.direction = 0f;
                if (GUILayout.Button("⬈ NE\n45°")) partyData.direction = 45f;
                if (GUILayout.Button("➡ E\n90°")) partyData.direction = 90f;
                if (GUILayout.Button("⬊ SE\n135°")) partyData.direction = 135f;
                EditorGUILayout.EndHorizontal();
                
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("⬇ S\n180°")) partyData.direction = 180f;
                if (GUILayout.Button("⬋ SW\n225°")) partyData.direction = 225f;
                if (GUILayout.Button("⬅ W\n270°")) partyData.direction = 270f;
                if (GUILayout.Button("⬉ NW\n315°")) partyData.direction = 315f;
                EditorGUILayout.EndHorizontal();
                
                EditorGUILayout.EndVertical();
                EditorGUI.indentLevel--;
            }
            
            EditorGUILayout.EndFoldoutHeaderGroup();
            EditorGUILayout.Space(3);
        }

        private void DrawTroopsSection(MBPartyData partyData)
        {
            // Count total troops
            int totalMin = 0, totalMax = 0;
            foreach (var stack in partyData.troops)
            {
                totalMin += stack.Count;
            }
            
            string troopSummary = partyData.troops.Count == 0 ? 
                "No Troops" : 
                $"{partyData.troops.Count} Stacks ({totalMin}-{totalMax} troops)";
            
            showTroops = EditorGUILayout.BeginFoldoutHeaderGroup(showTroops, $"👥 {troopSummary}");
            
            if (showTroops)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.BeginVertical(sectionBoxStyle);
                
                // Stack limits info
                if (partyData.troops.Count > 6)
                {
                    EditorGUILayout.HelpBox($"Module System limit is 6 stacks. You have {partyData.troops.Count}.\n\n" +
                        "Stack Limits:\n" +
                        "• Module System (parties.py): Maximum 6 stacks\n" +
                        "• Runtime (via scripts): Up to 255 stacks using party_add_members\n" +
                        "• World map tooltip: Shows maximum 32 stacks\n" +
                        "• Save system: Upgrades for stacks 33+ won't save unless moved to position 1-32\n" +
                        "• Per stack: No limit on troop count per stack", MessageType.Warning);
                }
                else if (partyData.troops.Count == 0)
                {
                    EditorGUILayout.HelpBox("No troops defined. Towns, castles, and villages typically start with empty garrison and get troops assigned by scripts at game start.", MessageType.Info);
                }
                
                // Draw each troop stack
                for (int i = 0; i < partyData.troops.Count; i++)
                {
                    DrawEnhancedTroopStack(partyData, i);
                    EditorGUILayout.Space(3);
                }
                
                EditorGUILayout.Space(5);
                
                // Add/Remove buttons
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("➕ Add Troop Stack", GUILayout.Height(30)))
                {
                    partyData.troops.Add(new MBTroopStack
                    {
                        TroopID = "trp_",
                        Count = 0,
                        StackFlags = "0"
                    });
                }
                
                GUI.enabled = partyData.troops.Count > 0;
                if (GUILayout.Button("➖ Remove Last", GUILayout.Height(30)))
                {
                    partyData.troops.RemoveAt(partyData.troops.Count - 1);
                }
                GUI.enabled = true;
                
                EditorGUILayout.EndHorizontal();
                
                // Party speed info
                if (partyData.troops.Count > 0)
                {
                    EditorGUILayout.Space(5);
                    EditorGUILayout.BeginVertical(infoStyle);
                    EditorGUILayout.LabelField("ℹ️ Party Speed Factors", EditorStyles.boldLabel);
                    EditorGUILayout.LabelField("Party speed is affected by:", EditorStyles.wordWrappedLabel);
                    EditorGUILayout.LabelField("• Morale (leadership skill, food variety, recent events)", EditorStyles.wordWrappedLabel);
                    EditorGUILayout.LabelField("• Pathfinding skill of party leader", EditorStyles.wordWrappedLabel);
                    EditorGUILayout.LabelField("• Number of mounted troops (tf_mounted flag)", EditorStyles.wordWrappedLabel);
                    EditorGUILayout.LabelField("• Number of prisoners (slows party down)", EditorStyles.wordWrappedLabel);
                    EditorGUILayout.LabelField("• Inventory weight (trade goods, use horses as pack animals)", EditorStyles.wordWrappedLabel);
                    EditorGUILayout.LabelField("• Terrain type (forests: 70%, night: 60%)", EditorStyles.wordWrappedLabel);
                    EditorGUILayout.LabelField("• Ships travel at speed * 15.0 on water", EditorStyles.wordWrappedLabel);
                    EditorGUILayout.EndVertical();
                }
                
                EditorGUILayout.EndVertical();
                EditorGUI.indentLevel--;
            }
            
            EditorGUILayout.EndFoldoutHeaderGroup();
            EditorGUILayout.Space(3);
        }

        private void DrawEnhancedTroopStack(MBPartyData partyData, int index)
        {
            var stack = partyData.troops[index];
            
            Color bgColor = index % 2 == 0 ? new Color(0.8f, 0.8f, 0.8f, 0.1f) : new Color(0.7f, 0.7f, 0.7f, 0.1f);
            GUI.backgroundColor = bgColor;
            
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            GUI.backgroundColor = Color.white;
            
            // Header with delete button
            EditorGUILayout.BeginHorizontal();
            
            bool isPrisoner = false;
            try
            {
                int flagValue = string.IsNullOrEmpty(stack.StackFlags) ? 0 : int.Parse(stack.StackFlags);
                isPrisoner = PartyMemberFlagsDecoder.IsPrisoner(flagValue);
            }
            catch { }
            
            string stackLabel = isPrisoner ? $"🔒 Stack {index + 1} (Prisoner)" : $"⚔️ Stack {index + 1}";
            EditorGUILayout.LabelField(stackLabel, EditorStyles.boldLabel);
            
            if (GUILayout.Button("✖", GUILayout.Width(25), GUILayout.Height(20)))
            {
                if (EditorUtility.DisplayDialog("Remove Stack", 
                    $"Remove Stack {index + 1}?", "Yes", "No"))
                {
                    partyData.troops.RemoveAt(index);
                    return;
                }
            }
            EditorGUILayout.EndHorizontal();
            
            EditorGUI.indentLevel++;
            
            // Troop ID
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(new GUIContent("Troop ID:", 
                "Troop identifier from module_troops.py (e.g., trp_peasant, trp_knight_1_1). Can be regular troops or hero troops."), 
                GUILayout.Width(100));
            
            Color originalColor = GUI.backgroundColor;
            if (string.IsNullOrEmpty(stack.TroopID))
                GUI.backgroundColor = new Color(1f, 0.8f, 0.5f);
            
            stack.TroopID = EditorGUILayout.TextField(new GUIContent("", 
                "Enter the troop ID without the 'trp_' prefix (it's added automatically in the module system)."), 
                stack.TroopID);
            GUI.backgroundColor = originalColor;
            
            EditorGUILayout.EndHorizontal();
            
            // Count with sliders
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(new GUIContent("Count:", 
                "Number of troops in this stack; does not vary. The number you input here is the number of troops the town will have."), 
                GUILayout.Width(100));
            stack.Count = EditorGUILayout.IntSlider(stack.Count, 0, 1000);
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.Space(3);
            
            // Prisoner toggle
            EditorGUILayout.BeginHorizontal();
            bool newPrisoner = EditorGUILayout.Toggle(new GUIContent("Is Prisoner", 
                "Mark this troop stack as prisoners (pmf_is_prisoner flag). Prisoners affect party speed and can be sold or recruited."), 
                isPrisoner);
            if (newPrisoner != isPrisoner)
            {
                stack.StackFlags = newPrisoner ? "1" : "0";
            }
            if (isPrisoner)
            {
                EditorGUILayout.LabelField("🔒", GUILayout.Width(30));
            }
            EditorGUILayout.EndHorizontal();
            
            EditorGUI.indentLevel--;
            
            EditorGUILayout.EndVertical();
        }

        private void DrawAdvancedSection(MBPartyData partyData)
        {
            showAdvanced = EditorGUILayout.BeginFoldoutHeaderGroup(showAdvanced, "⚙️ Advanced Settings");
            
            if (showAdvanced)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.BeginVertical(sectionBoxStyle);
                
                EditorGUILayout.HelpBox("Menu field is deprecated (M&B 0.730+).\n\n" +
                    "The menu field has no effect in modern versions. Instead, use the game_event_party_encounter script which is called by the game engine whenever the player encounters a party.\n\n" +
                    "Party Template: Link to party_templates.py entry (e.g., pt_looters, pt_mountain_bandits). Use pt_none for custom static parties.", 
                    MessageType.Warning);
                
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(new GUIContent("Menu:", 
                    "DEPRECATED: No effect since M&B 0.730. Use no_menu (0) or leave as is. The game_event_party_encounter script handles encounters now."), 
                    GUILayout.Width(150));
                partyData.menu = EditorGUILayout.TextField(partyData.menu);
                EditorGUILayout.EndHorizontal();
                
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(new GUIContent("Party Template:", 
                    "ID of the party template from module_party_templates.py (e.g., pt_looters, pt_kingdom_caravan_party). Use pt_none for parties defined directly in module_parties.py."), 
                    GUILayout.Width(150));
                partyData.partyTemplate = EditorGUILayout.TextField(partyData.partyTemplate);
                EditorGUILayout.EndHorizontal();
                
                EditorGUILayout.Space(5);
                
                // Raw value editors
                EditorGUILayout.LabelField("Raw Value Editors", EditorStyles.boldLabel);
                
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.LabelField("Flags (Hex/Dec):");
                partyData.flags = EditorGUILayout.TextField(partyData.flags);
                
                EditorGUILayout.LabelField("Personality (Hex/Dec):");
                partyData.personality = EditorGUILayout.TextField(partyData.personality);
                
                EditorGUILayout.LabelField("AI Behavior (0-11):");
                partyData.aiBehavior = EditorGUILayout.TextField(partyData.aiBehavior);
                EditorGUILayout.EndVertical();
                
                EditorGUILayout.EndVertical();
                EditorGUI.indentLevel--;
            }
            
            EditorGUILayout.EndFoldoutHeaderGroup();
            EditorGUILayout.Space(3);
        }

        private void DrawValidationSection(MBPartyData partyData)
        {
            int issueCount = validationErrors.Count + validationWarnings.Count;
            
            string icon = issueCount == 0 ? "✅" : "⚠️";
            string sectionTitle = issueCount > 0 ? 
                $"{icon} Validation ({issueCount} issue{(issueCount != 1 ? "s" : "")})" : 
                $"{icon} Validation (All Clear)";
            
            showValidation = EditorGUILayout.BeginFoldoutHeaderGroup(showValidation, sectionTitle);
            
            if (showValidation)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.BeginVertical(sectionBoxStyle);
                
                if (validationErrors.Count > 0)
                {
                    EditorGUILayout.BeginVertical(errorStyle);
                    EditorGUILayout.LabelField("❌ Errors:", EditorStyles.boldLabel);
                    foreach (var error in validationErrors)
                    {
                        EditorGUILayout.LabelField($"   • {error}", EditorStyles.wordWrappedLabel);
                    }
                    EditorGUILayout.EndVertical();
                    EditorGUILayout.Space(5);
                }
                
                if (validationWarnings.Count > 0)
                {
                    EditorGUILayout.BeginVertical(warningStyle);
                    EditorGUILayout.LabelField("⚠️ Warnings:", EditorStyles.boldLabel);
                    foreach (var warning in validationWarnings)
                    {
                        EditorGUILayout.LabelField($"   • {warning}", EditorStyles.wordWrappedLabel);
                    }
                    EditorGUILayout.EndVertical();
                    EditorGUILayout.Space(5);
                }
                
                if (validationErrors.Count == 0 && validationWarnings.Count == 0)
                {
                    EditorGUILayout.BeginVertical(successStyle);
                    EditorGUILayout.LabelField("✅ No issues detected - Party data is valid!", EditorStyles.boldLabel);
                    EditorGUILayout.EndVertical();
                }
                
                EditorGUILayout.EndVertical();
                EditorGUI.indentLevel--;
            }
            
            EditorGUILayout.EndFoldoutHeaderGroup();
        }

        // Helper methods
        
        private void DrawValueBar(int value, int min, int max, Color lowColor, Color highColor)
        {
            Rect rect = GUILayoutUtility.GetRect(18, 6);
            float normalized = Mathf.InverseLerp(min, max, value);
            Color barColor = Color.Lerp(lowColor, highColor, normalized);
            
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, rect.height), Color.gray);
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width * normalized, rect.height), barColor);
        }

        private void DrawDirectionCompass(float degrees)
        {
            Rect rect = GUILayoutUtility.GetRect(100, 100);
            rect.x += (rect.width - 100) / 2; // Center it
            rect.width = 100;
            rect.height = 100;
            
            Vector2 center = rect.center;
            float radius = 45f;
            
            // Draw compass circle
            Handles.color = Color.gray;
            Handles.DrawWireDisc(center, Vector3.forward, radius);
            
            // Draw cardinal directions
            GUI.Label(new Rect(center.x - 5, center.y - radius - 15, 20, 20), "N", EditorStyles.boldLabel);
            GUI.Label(new Rect(center.x + radius, center.y - 5, 20, 20), "E", EditorStyles.boldLabel);
            GUI.Label(new Rect(center.x - 5, center.y + radius, 20, 20), "S", EditorStyles.boldLabel);
            GUI.Label(new Rect(center.x - radius - 15, center.y - 5, 20, 20), "W", EditorStyles.boldLabel);
            
            // Draw direction arrow
            float rad = degrees * Mathf.Deg2Rad;
            Vector2 direction = new Vector2(Mathf.Sin(rad), -Mathf.Cos(rad)) * (radius - 5);
            Vector2 arrowEnd = center + direction;
            
            Handles.color = Color.red;
            Handles.DrawLine(center, arrowEnd);
            
            // Draw arrowhead
            Vector2 perpendicular = new Vector2(-direction.y, direction.x).normalized * 5;
            Handles.DrawLine(arrowEnd, arrowEnd - direction.normalized * 10 + perpendicular);
            Handles.DrawLine(arrowEnd, arrowEnd - direction.normalized * 10 - perpendicular);
        }

        private string GetDirectionLabel(float degrees)
        {
            if (degrees < 22.5f || degrees >= 337.5f) return "N";
            if (degrees < 67.5f) return "NE";
            if (degrees < 112.5f) return "E";
            if (degrees < 157.5f) return "SE";
            if (degrees < 202.5f) return "S";
            if (degrees < 247.5f) return "SW";
            if (degrees < 292.5f) return "W";
            return "NW";
        }

        private void DrawFlagCheckbox(MBPartyData partyData, string flagName, string label, string tooltip = "")
        {
            bool currentValue = flagStates.ContainsKey(flagName) && flagStates[flagName];
            
            EditorGUILayout.BeginHorizontal();
            bool newValue = EditorGUILayout.Toggle(new GUIContent(label, tooltip), currentValue);
            EditorGUILayout.EndHorizontal();
            
            if (newValue != currentValue)
            {
                flagStates[flagName] = newValue;
                UpdateFlagsFromCheckboxes(partyData);
            }
        }

        private int GetLabelSize()
        {
            if (flagStates.ContainsKey("pf_label_large") && flagStates["pf_label_large"]) return 2;
            if (flagStates.ContainsKey("pf_label_medium") && flagStates["pf_label_medium"]) return 1;
            return 0;
        }

        private void SetLabelSize(MBPartyData partyData, int size)
        {
            flagStates["pf_label_small"] = false;
            flagStates["pf_label_medium"] = false;
            flagStates["pf_label_large"] = false;
            
            if (size == 1) flagStates["pf_label_medium"] = true;
            else if (size == 2) flagStates["pf_label_large"] = true;
            
            UpdateFlagsFromCheckboxes(partyData);
        }

        private void UpdateFlagsFromCheckboxes(MBPartyData partyData)
        {
            List<string> activeFlags = new List<string>();
            
            foreach (var kvp in flagStates)
            {
                if (kvp.Value)
                {
                    activeFlags.Add(kvp.Key);
                }
            }
            
            int iconId = decodedFlags?.IconId ?? 0;
            BigInteger newFlags = PartyFlagsDecoder.CombineIconAndFlags(iconId, activeFlags);
            partyData.flags = $"0x{newFlags:X}";
        }

        private void UpdateIconId(MBPartyData partyData, int newIconId)
        {
            List<string> activeFlags = new List<string>();
            
            foreach (var kvp in flagStates)
            {
                if (kvp.Value)
                {
                    activeFlags.Add(kvp.Key);
                }
            }
            
            BigInteger newFlags = PartyFlagsDecoder.CombineIconAndFlags(newIconId, activeFlags);
            partyData.flags = $"0x{newFlags:X}";
        }

        private void UpdatePersonality(MBPartyData partyData)
        {
            int encoded = PartyPersonalityDecoder.EncodePersonality(courageLevel, aggressivenessLevel, isBandit);
            partyData.personality = $"0x{encoded:X}";
        }

        // Template application methods
        
        private void ApplyTownTemplate(MBPartyData partyData)
        {
            partyData.flags = "0x00406000"; // pf_is_static|pf_always_visible|pf_show_faction|pf_label_large
            partyData.aiBehavior = "0"; // ai_bhvr_hold
            partyData.aiTarget = "";
            flagStates["pf_is_static"] = true;
            flagStates["pf_always_visible"] = true;
            flagStates["pf_show_faction"] = true;
            flagStates["pf_label_large"] = true;
        }

        private void ApplyCastleTemplate(MBPartyData partyData)
        {
            partyData.flags = "0x00405000"; // pf_is_static|pf_always_visible|pf_show_faction|pf_label_medium
            partyData.aiBehavior = "0"; // ai_bhvr_hold
            partyData.aiTarget = "";
            flagStates["pf_is_static"] = true;
            flagStates["pf_always_visible"] = true;
            flagStates["pf_show_faction"] = true;
            flagStates["pf_label_medium"] = true;
        }

        private void ApplyVillageTemplate(MBPartyData partyData)
        {
            partyData.flags = "0x00204400"; // pf_is_static|pf_always_visible|pf_hide_defenders|pf_label_small
            partyData.aiBehavior = "0"; // ai_bhvr_hold
            partyData.aiTarget = "";
            flagStates["pf_is_static"] = true;
            flagStates["pf_always_visible"] = true;
            flagStates["pf_hide_defenders"] = true;
        }

        private void ApplyPatrolTemplate(MBPartyData partyData)
        {
            partyData.flags = "0x00400000"; // pf_show_faction
            partyData.aiBehavior = "2"; // ai_bhvr_patrol_location
            courageLevel = 9;
            aggressivenessLevel = 8;
            isBandit = false;
            UpdatePersonality(partyData);
            flagStates["pf_show_faction"] = true;
        }

        private void ApplyBanditTemplate(MBPartyData partyData)
        {
            partyData.flags = "0x04000000"; // pf_civilian
            partyData.aiBehavior = "2"; // ai_bhvr_patrol_location
            courageLevel = 8;
            aggressivenessLevel = 3;
            isBandit = true;
            UpdatePersonality(partyData);
        }

        private void ApplyCaravanTemplate(MBPartyData partyData)
        {
            partyData.flags = "0x06020000"; // pf_auto_remove_in_town|pf_civilian|pf_show_faction
            partyData.aiBehavior = "1"; // ai_bhvr_travel_to_party
            courageLevel = 7;
            aggressivenessLevel = 0;
            isBandit = false;
            UpdatePersonality(partyData);
            flagStates["pf_auto_remove_in_town"] = true;
            flagStates["pf_civilian"] = true;
            flagStates["pf_show_faction"] = true;
        }

        private void ClearPartyData(MBPartyData partyData)
        {
            partyData.partyId = "";
            partyData.partyName = "";
            partyData.mapIcon = "";
            partyData.flags = "0";
            partyData.menu = "0";
            partyData.partyTemplate = "pt_none";
            partyData.aiBehavior = "0";
            partyData.aiTarget = "";
            partyData.faction = "fac_neutral";
            partyData.personality = "0";
            partyData.direction = 0;
            partyData.worldPosition = UnityEngine.Vector2.zero;
            partyData.troops.Clear();
            
            courageLevel = 8;
            aggressivenessLevel = 8;
            isBandit = false;
            flagStates.Clear();
        }

        private void ValidatePartyData(MBPartyData partyData)
        {
            validationErrors.Clear();
            validationWarnings.Clear();
            
            if (string.IsNullOrEmpty(partyData.partyId))
                validationErrors.Add("Party ID is required");
            else if (partyData.partyId.Contains(" "))
                validationErrors.Add("Party ID should not contain spaces");
            
            if (string.IsNullOrEmpty(partyData.partyName))
                validationWarnings.Add("Party name is empty");
            
            bool requiresTarget = PartyAIBehaviorDecoder.RequiresTarget(behaviorValues[selectedBehaviorIndex]);
            bool requiresCoords = PartyAIBehaviorDecoder.RequiresCoordinates(behaviorValues[selectedBehaviorIndex]);
            
            if (requiresTarget && string.IsNullOrEmpty(partyData.aiTarget))
                validationErrors.Add("AI behavior requires a target party");
            
            if (requiresCoords && string.IsNullOrEmpty(partyData.aiTarget))
                validationErrors.Add("AI behavior requires coordinate data");
            
            if (partyData.direction < 0 || partyData.direction >= 360)
                validationWarnings.Add($"Direction should be 0-360° (current: {partyData.direction})");
            
            if (partyData.troops.Count > 255)
                validationErrors.Add($"Party has {partyData.troops.Count} stacks (engine limit: 255)");
            else if (partyData.troops.Count > 6)
                validationWarnings.Add($"{partyData.troops.Count} stacks exceeds Module System limit (6)");
            
            for (int i = 0; i < partyData.troops.Count; i++)
            {
                var stack = partyData.troops[i];
                
                if (string.IsNullOrEmpty(stack.TroopID))
                    validationWarnings.Add($"Stack {i + 1}: Troop ID is empty");
            }
            
            if (decodedFlags != null && decodedFlags.ActiveFlags.Contains("pf_is_static"))
            {
                if (partyData.worldPosition == UnityEngine.Vector2.zero)
                    validationWarnings.Add("Static party at zero coordinates");
            }
        }
    }
}