using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Numerics;
using System.Linq;

namespace MountAndBlade.Data.Editor
{
    /// <summary>
    /// Enhanced custom editor for MBSceneData with flag decoder integration
    /// </summary>
    [CustomEditor(typeof(MBSceneData))]
    public class MBSceneDataEditor : UnityEditor.Editor
    {
        private bool showIdentification = true;
        private bool showFlags = true;
        private bool showMeshInfo = true;
        private bool showBoundaries = true;
        private bool showEnvironment = true;
        private bool showAccessibility = false;
        private bool showChests = false;
        
        private SerializedProperty sceneIdProp;
        private SerializedProperty flagsProp;
        private SerializedProperty meshNameProp;
        private SerializedProperty bodyNameProp;
        private SerializedProperty minPosProp;
        private SerializedProperty maxPosProp;
        private SerializedProperty waterLevelProp;
        private SerializedProperty terrainCodeProp;
        private SerializedProperty accessibleScenesProp;
        private SerializedProperty chestsProp;
        private SerializedProperty outerTerrainProp;
        
        // Flag editing state
        private Dictionary<string, bool> flagStates = new Dictionary<string, bool>();
        private bool flagsInitialized = false;

        private void OnEnable()
        {
            sceneIdProp = serializedObject.FindProperty("SceneID");
            flagsProp = serializedObject.FindProperty("Flags");
            meshNameProp = serializedObject.FindProperty("MeshName");
            bodyNameProp = serializedObject.FindProperty("BodyName");
            minPosProp = serializedObject.FindProperty("MinPos");
            maxPosProp = serializedObject.FindProperty("MaxPos");
            waterLevelProp = serializedObject.FindProperty("WaterLevel");
            terrainCodeProp = serializedObject.FindProperty("TerrainCode");
            accessibleScenesProp = serializedObject.FindProperty("AccessibleScenes");
            chestsProp = serializedObject.FindProperty("Chests");
            outerTerrainProp = serializedObject.FindProperty("OuterTerrainMesh");
            
            InitializeFlagStates();
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            MBSceneData sceneData = (MBSceneData)target;

            // Header
            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField("Mount & Blade Scene Data", EditorStyles.boldLabel);
            EditorGUILayout.Space(5);

            EditorGUILayout.Space(10);

            // Identification Section
            showIdentification = EditorGUILayout.Foldout(showIdentification, "Identification", true);
            if (showIdentification)
            {
                EditorGUI.indentLevel++;
                DrawIdentificationSection();
                EditorGUI.indentLevel--;
                EditorGUILayout.Space(5);
            }

            // Flags Section (Enhanced with decoder)
            showFlags = EditorGUILayout.Foldout(showFlags, "Scene Flags", true);
            if (showFlags)
            {
                EditorGUI.indentLevel++;
                DrawEnhancedFlagsSection(sceneData);
                EditorGUI.indentLevel--;
                EditorGUILayout.Space(5);
            }

            // Mesh Info Section
            showMeshInfo = EditorGUILayout.Foldout(showMeshInfo, "Mesh Information", true);
            if (showMeshInfo)
            {
                EditorGUI.indentLevel++;
                DrawMeshInfoSection(sceneData);
                EditorGUI.indentLevel--;
                EditorGUILayout.Space(5);
            }

            // Boundaries Section
            showBoundaries = EditorGUILayout.Foldout(showBoundaries, "Scene Boundaries", true);
            if (showBoundaries)
            {
                EditorGUI.indentLevel++;
                DrawBoundariesSection(sceneData);
                EditorGUI.indentLevel--;
                EditorGUILayout.Space(5);
            }

            // Environment Section
            showEnvironment = EditorGUILayout.Foldout(showEnvironment, "Environment", true);
            if (showEnvironment)
            {
                EditorGUI.indentLevel++;
                DrawEnvironmentSection();
                EditorGUI.indentLevel--;
                EditorGUILayout.Space(5);
            }

            // Accessibility Section (Deprecated)
            showAccessibility = EditorGUILayout.Foldout(showAccessibility, "Accessible Scenes (Deprecated)", true);
            if (showAccessibility)
            {
                EditorGUI.indentLevel++;
                DrawAccessibilitySection();
                EditorGUI.indentLevel--;
                EditorGUILayout.Space(5);
            }

            // Chests Section
            showChests = EditorGUILayout.Foldout(showChests, "Chest Troops", true);
            if (showChests)
            {
                EditorGUI.indentLevel++;
                DrawChestsSection();
                EditorGUI.indentLevel--;
                EditorGUILayout.Space(5);
            }

            serializedObject.ApplyModifiedProperties();
        }

        private void InitializeFlagStates()
        {
            if (flagsInitialized)
                return;
            
            var allFlags = SceneFlagDecoder.GetAllFlagNames();
            BigInteger currentValue = ParseFlagValue(flagsProp.stringValue);
            
            flagStates.Clear();
            foreach (var flagName in allFlags)
            {
                flagStates[flagName] = SceneFlagDecoder.HasFlag(currentValue, flagName);
            }
            
            flagsInitialized = true;
        }

        private void DrawIdentificationSection()
        {
            EditorGUILayout.PropertyField(sceneIdProp, new GUIContent("Scene ID", 
                "Unique identifier for this scene. The prefix 'scn_' is automatically added in-game."));
            
            EditorGUILayout.Space(3);
            EditorGUILayout.HelpBox(
                "In-game scene reference: scn_" + sceneIdProp.stringValue,
                MessageType.None
            );
        }

        private void DrawEnhancedFlagsSection(MBSceneData sceneData)
        {
            // Raw flags field
            EditorGUILayout.PropertyField(flagsProp, new GUIContent("Flags (Raw Value)", 
                "Scene flags as string (decimal or hex). Edit checkboxes below for easier flag management."));
            
            BigInteger currentValue = ParseFlagValue(flagsProp.stringValue);
            
            // Show formatted value
            EditorGUILayout.LabelField("Formatted Value:", SceneFlagDecoder.FormatFlagValue(currentValue));
            
            EditorGUILayout.Space(5);
            
            // Interactive flag checkboxes
            EditorGUILayout.LabelField("Edit Flags:", EditorStyles.boldLabel);
            
            EditorGUI.BeginChangeCheck();
            
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            
            var allFlags = SceneFlagDecoder.GetAllFlagNames();
            foreach (var flagName in allFlags)
            {
                bool currentState = flagStates.ContainsKey(flagName) ? flagStates[flagName] : false;
                string description = SceneFlagDecoder.GetFlagDescription(flagName);
                
                bool newState = EditorGUILayout.ToggleLeft(
                    new GUIContent(flagName, description),
                    currentState
                );
                
                flagStates[flagName] = newState;
                
                // Show description below checkbox
                if (newState)
                {
                    EditorGUI.indentLevel++;
                    EditorGUILayout.LabelField(description, EditorStyles.wordWrappedMiniLabel);
                    EditorGUI.indentLevel--;
                }
            }
            
            EditorGUILayout.EndVertical();
            
            if (EditorGUI.EndChangeCheck())
            {
                // Rebuild flags value from checkboxes
                var activeFlags = flagStates.Where(kvp => kvp.Value).Select(kvp => kvp.Key);
                BigInteger newValue = SceneFlagDecoder.EncodeFlags(activeFlags);
                flagsProp.stringValue = newValue.ToString();
                serializedObject.ApplyModifiedProperties();
            }
            
            EditorGUILayout.Space(5);
            
            // Quick preset buttons
            EditorGUILayout.LabelField("Quick Presets:", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            
            if (GUILayout.Button("Indoor"))
            {
                ApplyPreset(new[] { "sf_indoors" });
            }
            
            if (GUILayout.Button("Outdoor Generated"))
            {
                ApplyPreset(new[] { "sf_generate" });
            }
            
            if (GUILayout.Button("Random Battle"))
            {
                ApplyPreset(new[] { "sf_generate", "sf_randomize", "sf_auto_entry_points" });
            }
            
            if (GUILayout.Button("Clear All"))
            {
                ApplyPreset(new string[0]);
            }
            
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.Space(5);
            
            // Decoded flags display
            var decodedFlags = SceneFlagDecoder.DecodeFlags(currentValue);
            if (decodedFlags.Count > 0)
            {
                EditorGUILayout.LabelField("Active Flags:", EditorStyles.boldLabel);
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                
                foreach (var flag in decodedFlags)
                {
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField("✓", GUILayout.Width(20));
                    EditorGUILayout.LabelField(flag, EditorStyles.boldLabel);
                    EditorGUILayout.EndHorizontal();
                }
                
                EditorGUILayout.EndVertical();
            }
            else
            {
                EditorGUILayout.HelpBox("No flags set", MessageType.None);
            }
        }

        private void ApplyPreset(string[] flagNames)
        {
            // Clear all flags
            foreach (var key in flagStates.Keys.ToList())
            {
                flagStates[key] = false;
            }
            
            // Set preset flags
            foreach (var flagName in flagNames)
            {
                if (flagStates.ContainsKey(flagName))
                {
                    flagStates[flagName] = true;
                }
            }
            
            // Update flags value
            BigInteger newValue = SceneFlagDecoder.EncodeFlags(flagNames);
            flagsProp.stringValue = newValue.ToString();
            serializedObject.ApplyModifiedProperties();
        }

        private void DrawMeshInfoSection(MBSceneData sceneData)
        {
            BigInteger flagValue = ParseFlagValue(sceneData.Flags);
            bool isIndoor = SceneFlagDecoder.IsIndoor(flagValue);

            EditorGUILayout.PropertyField(meshNameProp, new GUIContent("Mesh Name",
                isIndoor ? "Interior mesh to use for this indoor scene." : 
                "Should be 'none' for outdoor scenes."));

            EditorGUILayout.PropertyField(bodyNameProp, new GUIContent("Body Name",
                isIndoor ? "Collision body mesh for this indoor scene." :
                "Should be 'none' for outdoor scenes."));

            EditorGUILayout.Space(3);
            
            if (isIndoor)
            {
                EditorGUILayout.HelpBox(
                    "Indoor scenes use mesh and body names. Outdoor scenes should use 'none'.",
                    MessageType.Info
                );
            }
            else
            {
                if (sceneData.MeshName != "none" || sceneData.BodyName != "none")
                {
                    EditorGUILayout.HelpBox(
                        "Outdoor scenes should have mesh and body set to 'none'.",
                        MessageType.Warning
                    );
                }
            }
        }

        private void DrawBoundariesSection(MBSceneData sceneData)
        {
            EditorGUILayout.PropertyField(minPosProp, new GUIContent("Min Position",
                "Minimum (x,y) boundary. Players cannot move beyond this limit."));

            EditorGUILayout.PropertyField(maxPosProp, new GUIContent("Max Position",
                "Maximum (x,y) boundary. Players cannot move beyond this limit."));

            EditorGUILayout.Space(5);

            // Calculate and display scene info
            UnityEngine.Vector2 size = sceneData.MaxPos - sceneData.MinPos;
            UnityEngine.Vector2 center = (sceneData.MinPos + sceneData.MaxPos) * 0.5f;

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Calculated Values:", EditorStyles.boldLabel);
            EditorGUILayout.LabelField($"Size: {size.x} × {size.y}");
            EditorGUILayout.LabelField($"Center: ({center.x}, {center.y})");
            EditorGUILayout.LabelField($"Area: {size.x * size.y} square units");
            EditorGUILayout.EndVertical();

            // Validate boundaries
            if (sceneData.MinPos.x >= sceneData.MaxPos.x || sceneData.MinPos.y >= sceneData.MaxPos.y)
            {
                EditorGUILayout.HelpBox(
                    "Invalid boundaries: Min position must be less than max position!",
                    MessageType.Error
                );
            }

            EditorGUILayout.Space(3);
            EditorGUILayout.HelpBox(
                "Common sizes:\n" +
                "• Small (Meeting/Indoor): 80×80\n" +
                "• Medium (Training): 200×200\n" +
                "• Large (Battle): 240×240",
                MessageType.None
            );
        }

        private void DrawEnvironmentSection()
        {
            EditorGUILayout.PropertyField(waterLevelProp, new GUIContent("Water Level",
                "Water surface level. -100 = no water, -0.5 = at ground level, 0 = deep water."));

            // Terrain Code with Edit button
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PropertyField(terrainCodeProp, new GUIContent("Terrain Code",
                "Hex string from terrain generator. Use '0' for indoor scenes."));
            
            // Button to open terrain editor (placeholder for future implementation)
            if (GUILayout.Button(new GUIContent("Edit", "Open Terrain Editor (Coming Soon)"), GUILayout.Width(50)))
            {
                OpenTerrainEditor();
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.PropertyField(outerTerrainProp, new GUIContent("Outer Terrain Border",
                "Optional: Outer terrain mesh (e.g., 'outer_terrain_plain', 'outer_terrain_snow'). " +
                "Leave empty if not used."));

            EditorGUILayout.Space(3);
            EditorGUILayout.HelpBox(
                "Terrain code is generated by the in-game terrain generator. " +
                "Indoor scenes should use '0'. Outdoor scenes need a valid terrain code.",
                MessageType.Info
            );
        }

        private void OpenTerrainEditor()
        {
            // Open the terrain editor window
            // MBTerrainEditorWindow.ShowWindow((MBSceneData)target);
        }

        private void DrawAccessibilitySection()
        {
            EditorGUILayout.HelpBox(
                "This field is DEPRECATED. Modern mods use the passage system instead.",
                MessageType.Warning
            );

            EditorGUILayout.PropertyField(accessibleScenesProp, new GUIContent("Other Scenes",
                "Deprecated: List of scenes accessible from this scene."));
        }

        private void DrawChestsSection()
        {
            EditorGUILayout.PropertyField(chestsProp, new GUIContent("Chest Troops",
                "List of troop IDs whose inventories become accessible chests in this scene."));

            EditorGUILayout.Space(3);
            EditorGUILayout.HelpBox(
                "In scene editor: Place chest props with variation numbers.\n" +
                "• Variation 0 = First troop in list\n" +
                "• Variation 1 = Second troop in list\n" +
                "• etc.",
                MessageType.Info
            );
        }

        private BigInteger ParseFlagValue(string flagString)
        {
            if (string.IsNullOrEmpty(flagString))
                return 0;
            
            flagString = flagString.Trim();
            
            // Try hex
            if (flagString.StartsWith("0x") || flagString.StartsWith("0X"))
            {
                if (BigInteger.TryParse(flagString.Substring(2), 
                    System.Globalization.NumberStyles.HexNumber, null, out var hexResult))
                {
                    return hexResult;
                }
            }
            
            // Try decimal
            if (BigInteger.TryParse(flagString, out var decResult))
            {
                return decResult;
            }
            
            return 0;
        }
    }
}