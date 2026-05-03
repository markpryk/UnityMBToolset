using System.IO;
using BDT.GUI.Helpers;
using MountAndBlade.Data;
using UnityEngine;
using UnityEditor;
using UnityEngine.UIElements;

/// <summary>
    /// General tab: Scene references, paths, terrain info, and statistics.
    /// </summary>
    public class GeneralTab : SceneDataManagerTabBase
    {
        public override string TabName => "General";
        public override int Order => 0;
        
        // Serialized properties
        private SerializedProperty terrainProp;
        private SerializedProperty moduleProp;
        private SerializedProperty floraGeneratorProp;
        private SerializedProperty waterPlaneProp;
        private SerializedProperty tintDataProp;
        private SerializedProperty sceneDataProp;

        private Editor _sceneDataEditor;
        private MBSceneData _mbSceneData;
        
        // Foldout states
        private bool showTerrainInfo = false;
        private bool showSceneData = false;
        
        public override void OnEnable(MBSceneDataManager manager, SerializedObject serializedObject)
        {
            base.OnEnable(manager, serializedObject);
            
            terrainProp = serializedObject.FindProperty("_terrain");
            moduleProp = serializedObject.FindProperty("_module");
            floraGeneratorProp = serializedObject.FindProperty("_floraGeneratorRGL");
            waterPlaneProp = serializedObject.FindProperty("_waterPlane");
            tintDataProp = serializedObject.FindProperty("_tintData");
            sceneDataProp = serializedObject.FindProperty("_sceneData");

            _mbSceneData = manager.SceneData;
            
            if (manager.SceneData != null)
            {
                _sceneDataEditor = Editor.CreateEditor(manager.SceneData);
            }
        }
        
        public override void DrawTab()
        {
            DrawSceneReferences();
            UIHelpers.DrawUILine(Color.gray);
            DrawTerrainInfo();
            UIHelpers.DrawUILine(Color.gray);
            DrawSceneDataSection();
        }
        
        private void DrawSceneReferences()
        {
            EditorGUILayout.LabelField(_mbSceneData.SceneID, StylesHelpers.BoxHeader());
            
            BeginBox();
            EditorGUI.BeginChangeCheck();
            EditorGUILayout.PropertyField(terrainProp);
            if (EditorGUI.EndChangeCheck())
            {
                serializedObject.ApplyModifiedProperties();
                manager.RefreshComponents();
            }
            EditorGUI.BeginDisabledGroup(true);
            EditorGUILayout.PropertyField(moduleProp);
            EditorGUILayout.PropertyField(waterPlaneProp);
            EditorGUILayout.PropertyField(floraGeneratorProp);
            EditorGUILayout.PropertyField(tintDataProp);
            EditorGUILayout.PropertyField(sceneDataProp);
            EditorGUILayout.ObjectField("Decorator", manager.Decorator, typeof(MBTerrainDecorator), true);
            EditorGUILayout.ObjectField("Heightmap Generator", manager.HeightmapGenerator, typeof(LayeredHeightmapGenerator), true);
            EditorGUI.EndDisabledGroup();
            
            EndBox();
            
            if (manager.Module == null)
            {
                EditorGUILayout.HelpBox("Assign a Module to enable export and regeneration features.", MessageType.Warning);
            }
        }
        private void DrawTerrainInfo()
        {
            if (!DrawFoldout(ref showTerrainInfo, "Terrain Info"))
                return;
            
            if (manager.Terrain == null)
            {
                EditorGUILayout.HelpBox("No terrain assigned.", MessageType.Info);
                return;
            }
            
            BeginBox();
            
            var td = manager.Terrain.terrainData;
            
            EditorGUILayout.LabelField($"Heightmap Resolution: {td.heightmapResolution}");
            EditorGUILayout.LabelField($"Alphamap Resolution: {td.alphamapWidth} x {td.alphamapHeight}");
            EditorGUILayout.LabelField($"Terrain Layers: {td.terrainLayers.Length}");
            EditorGUILayout.LabelField($"World Size: {td.size.x:F0} x {td.size.z:F0} x {td.size.y:F0}m");
            
            EditorGUILayout.Space(5);
            
            // Hash info
            string currentHash = manager.GetCurrentTerrainHash();
            if (!string.IsNullOrEmpty(currentHash))
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Hash:", GUILayout.Width(40));
                EditorGUILayout.SelectableLabel(currentHash, EditorStyles.textField, GUILayout.Height(18));
                if (GUILayout.Button("Copy", GUILayout.Width(45)))
                {
                    EditorGUIUtility.systemCopyBuffer = currentHash;
                }
                EditorGUILayout.EndHorizontal();
                
                var data = manager.GetTerrainDataFromHash();
                if (data != null)
                {
                    EditorGUILayout.LabelField($"Type: {MBTerrainRegenerator.GetTerrainTypeName(data.TerrainType)}", 
                        EditorStyles.miniLabel);
                }
            }
            else
            {
                EditorGUILayout.LabelField("Hash: None", EditorStyles.miniLabel);
            }
            
            EndBox();
        }
        
        private void DrawSceneDataSection()
        {
            if (!DrawFoldout(ref showSceneData, "Scene Data"))
                return;
        
            if (_mbSceneData == null)
            {
                EditorGUILayout.HelpBox("No Scene Data assigned.", MessageType.Info);
                return;
            }
        
            // Recreate editor if target changed
            if (_sceneDataEditor == null || _sceneDataEditor.target != _mbSceneData)
            {
                if (_sceneDataEditor != null)
                    Object.DestroyImmediate(_sceneDataEditor);
            
                _sceneDataEditor = Editor.CreateEditor(_mbSceneData);
            }
        
            BeginBox();
        
            // Draw the inspector with proper change tracking
            EditorGUI.BeginChangeCheck();
            _sceneDataEditor.OnInspectorGUI();
            if (EditorGUI.EndChangeCheck())
            {
                // Mark dirty if changes were made
                EditorUtility.SetDirty(_mbSceneData);
            }
        
            EndBox();
        }
        public override void OnDisable()
        {
            // Clean up to prevent memory leaks
            if (_sceneDataEditor != null)
            {
                Object.DestroyImmediate(_sceneDataEditor);
                _sceneDataEditor = null;
            }
        
            base.OnDisable();
        }
    }