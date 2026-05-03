using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

namespace MountAndBlade.Data.Editor
{
    [CustomEditor(typeof(MBMapIconData))]
    public class MBMapIconDataEditor : UnityEditor.Editor
    {
        private MBMapIconData mapIcon;
        
        // Foldout states
        private bool showBasicInfo = true;
        private bool showVisualSettings = true;
        private bool showAudioSettings = true;
        private bool showFlagPositioning = true;
        private bool showAdvanced = false;
        
        // Constants for reference
        private const float DEFAULT_AVATAR_SCALE = 0.15f;
        private const float DEFAULT_BANNER_SCALE = 0.3f;

        private void OnEnable()
        {
            mapIcon = (MBMapIconData)target;
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            
            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField("Mount & Blade Map Icon Editor", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Map icons are visual representations on the world map. Limited to 256 icons per module.", MessageType.Info);
            EditorGUILayout.Space(5);

            // Basic Information Section
            DrawBasicInfoSection();
            EditorGUILayout.Space(5);

            // Visual Settings Section
            DrawVisualSettingsSection();
            EditorGUILayout.Space(5);

            // Audio Settings Section
            DrawAudioSettingsSection();
            EditorGUILayout.Space(5);

            // Flag Positioning Section
            DrawFlagPositioningSection();
            EditorGUILayout.Space(5);

            // Advanced Section
            DrawAdvancedSection();
            EditorGUILayout.Space(10);

            // Validation Warnings
            DrawValidationWarnings();

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawBasicInfoSection()
        {
            showBasicInfo = EditorGUILayout.Foldout(showBasicInfo, "Basic Information", true, EditorStyles.foldoutHeader);
            
            if (showBasicInfo)
            {
                EditorGUI.indentLevel++;
                
                EditorGUILayout.PropertyField(serializedObject.FindProperty("iconId"), 
                    new GUIContent("Icon ID", "Unique identifier for this map icon. Prefix 'icon_' is automatically added in-game."));
                
                EditorGUILayout.PropertyField(serializedObject.FindProperty("displayName"), 
                    new GUIContent("Display Name", "Human-readable name for this icon (for editor use)."));
                
                EditorGUI.indentLevel--;
            }
        }

        private void DrawVisualSettingsSection()
        {
            showVisualSettings = EditorGUILayout.Foldout(showVisualSettings, "Visual Settings", true, EditorStyles.foldoutHeader);
            
            if (showVisualSettings)
            {
                EditorGUI.indentLevel++;
                
                EditorGUILayout.PropertyField(serializedObject.FindProperty("meshName"), 
                    new GUIContent("Mesh Name", "BRF mesh name. Found in map_icon_meshes.brf, map_icons_b.brf, or map_icons_c.brf."));
                
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.PropertyField(serializedObject.FindProperty("scale"), 
                    new GUIContent("Scale", "Visual scale of the icon on the world map."));
                if (GUILayout.Button("Avatar", GUILayout.Width(60)))
                {
                    serializedObject.FindProperty("scale").floatValue = DEFAULT_AVATAR_SCALE;
                }
                if (GUILayout.Button("Banner", GUILayout.Width(60)))
                {
                    serializedObject.FindProperty("scale").floatValue = DEFAULT_BANNER_SCALE;
                }
                EditorGUILayout.EndHorizontal();
                
                EditorGUILayout.HelpBox($"Common scales: Avatar = {DEFAULT_AVATAR_SCALE}, Banner = {DEFAULT_BANNER_SCALE}", MessageType.None);
                
                EditorGUILayout.PropertyField(serializedObject.FindProperty("noShadow"), 
                    new GUIContent("No Shadow", "If enabled, this icon will not cast shadows (mcn_no_shadow flag)."));
                
                EditorGUI.indentLevel--;
            }
        }

        private void DrawAudioSettingsSection()
        {
            showAudioSettings = EditorGUILayout.Foldout(showAudioSettings, "Audio Settings", true, EditorStyles.foldoutHeader);
            
            if (showAudioSettings)
            {
                EditorGUI.indentLevel++;
                
                EditorGUILayout.PropertyField(serializedObject.FindProperty("soundId"), 
                    new GUIContent("Sound ID", "Sound to play while icon is moving (e.g., snd_footstep_grass, snd_gallop). Must be defined in module_sounds.py."));
                
                if (!string.IsNullOrEmpty(mapIcon.soundId))
                {
                    EditorGUILayout.HelpBox("Common sounds: snd_footstep_grass, snd_gallop, snd_footstep_water", MessageType.None);
                }
                
                EditorGUI.indentLevel--;
            }
        }

        private void DrawFlagPositioningSection()
        {
            showFlagPositioning = EditorGUILayout.Foldout(showFlagPositioning, "Flag Positioning", true, EditorStyles.foldoutHeader);
            
            if (showFlagPositioning)
            {
                EditorGUI.indentLevel++;
                
                EditorGUILayout.HelpBox("Flag offset positions determine where party banners appear relative to the map icon.", MessageType.Info);
                
                EditorGUILayout.PropertyField(serializedObject.FindProperty("flagOffsetX"), 
                    new GUIContent("Flag Offset X", "X position offset for the party flag/banner."));
                
                EditorGUILayout.PropertyField(serializedObject.FindProperty("flagOffsetY"), 
                    new GUIContent("Flag Offset Y", "Y position offset for the party flag/banner."));
                
                EditorGUILayout.PropertyField(serializedObject.FindProperty("flagOffsetZ"), 
                    new GUIContent("Flag Offset Z", "Z position offset for the party flag/banner."));
                
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Reset to Default"))
                {
                    serializedObject.FindProperty("flagOffsetX").floatValue = 0.15f;
                    serializedObject.FindProperty("flagOffsetY").floatValue = 0.173f;
                    serializedObject.FindProperty("flagOffsetZ").floatValue = 0.0f;
                }
                EditorGUILayout.EndHorizontal();
                
                EditorGUI.indentLevel--;
            }
        }

        private void DrawAdvancedSection()
        {
            showAdvanced = EditorGUILayout.Foldout(showAdvanced, "Advanced Settings", true, EditorStyles.foldoutHeader);
            
            if (showAdvanced)
            {
                EditorGUI.indentLevel++;
                
                EditorGUILayout.PropertyField(serializedObject.FindProperty("triggerCode"), 
                    new GUIContent("Trigger Code", "Optional Python code for ti_on_init_map_icon trigger. Runs when icon is initialized."));
                
                if (!string.IsNullOrEmpty(mapIcon.triggerCode))
                {
                    EditorGUILayout.HelpBox("Trigger Parameter 1 = Party ID whose map icon was initialized", MessageType.Info);
                }
                
                EditorGUILayout.Space(5);
                EditorGUILayout.LabelField("Technical Notes:", EditorStyles.boldLabel);
                EditorGUILayout.HelpBox(
                    "• Map icons are limited to 256 per module\n" +
                    "• Multi-meshing is not supported\n" +
                    "• For 256+ icons, use party_set_banner_icon or party_set_extra_icon operations\n" +
                    "• Sounds must be defined in module_sounds.py",
                    MessageType.None);
                
                EditorGUI.indentLevel--;
            }
        }

        private void DrawValidationWarnings()
        {
            List<string> warnings = new List<string>();
            
            // Check for empty required fields
            if (string.IsNullOrWhiteSpace(mapIcon.iconId))
            {
                warnings.Add("Icon ID is required");
            }
            
            if (string.IsNullOrWhiteSpace(mapIcon.meshName))
            {
                warnings.Add("Mesh Name is required");
            }
            
            // Check for unusual scale values
            if (mapIcon.scale <= 0)
            {
                warnings.Add("Scale should be greater than 0");
            }
            else if (mapIcon.scale > 1.0f)
            {
                warnings.Add("Scale is unusually large (typical range: 0.05 - 0.5)");
            }
            
            // Check for sound ID format
            if (!string.IsNullOrEmpty(mapIcon.soundId) && !mapIcon.soundId.StartsWith("snd_"))
            {
                warnings.Add("Sound ID should typically start with 'snd_' prefix");
            }
            
            // Display warnings
            if (warnings.Count > 0)
            {
                EditorGUILayout.Space(5);
                foreach (string warning in warnings)
                {
                    EditorGUILayout.HelpBox(warning, MessageType.Warning);
                }
            }
        }

        // Add menu item to create map icon data assets quickly
        [MenuItem("Assets/Create/Mount & Blade/Map Icon Data", priority = 1)]
        public static void CreateMapIconData()
        {
            string path = "Assets/";
            if (Selection.activeObject != null)
            {
                string selectedPath = AssetDatabase.GetAssetPath(Selection.activeObject);
                if (!string.IsNullOrEmpty(selectedPath))
                {
                    if (System.IO.Directory.Exists(selectedPath))
                    {
                        path = selectedPath + "/";
                    }
                    else
                    {
                        path = System.IO.Path.GetDirectoryName(selectedPath) + "/";
                    }
                }
            }

            MBMapIconData asset = CreateInstance<MBMapIconData>();
            string assetPath = AssetDatabase.GenerateUniqueAssetPath(path + "NewMapIcon.asset");
            AssetDatabase.CreateAsset(asset, assetPath);
            AssetDatabase.SaveAssets();
            EditorUtility.FocusProjectWindow();
            Selection.activeObject = asset;
        }
    }
}