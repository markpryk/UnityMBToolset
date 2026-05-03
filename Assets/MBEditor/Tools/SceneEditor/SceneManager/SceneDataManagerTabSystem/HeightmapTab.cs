using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

/// <summary>
/// Heightmap tab: Layered heightmap generation, terrain metrics, painted layers, and height expansion.
/// Integrates LayeredHeightmapGenerator functionality into the Scene Data Manager.
/// </summary>
public class HeightmapTab : SceneDataManagerTabBase
{
    public override string TabName => "Heightmap";
    public override int Order => 5; // Between General (0) and Export (10)
    
    // Subtab navigation
    private enum HeightmapSubTab { Layers, Settings }
    private HeightmapSubTab currentSubTab = HeightmapSubTab.Layers;
    
    // Layer foldout tracking
    private List<bool> layerFoldouts = new List<bool>();
    
    // Foldout states
    private bool showTerrainMetrics = true;
    private bool showHeightExpansion = true;
    private bool showPaintedLayer = true;
    private bool showLayerList = true;
    private bool showPreview = true;
    
    // Preview texture cache
    private Texture2D cachedPreviewTexture;
    private float[,] lastGeneratedHeightmap;
    
    // Reference to the heightmap generator
    private LayeredHeightmapGenerator Generator => manager?.HeightmapGenerator;
    
    public override void OnEnable(MBSceneDataManager manager, SerializedObject serializedObject)
    {
        base.OnEnable(manager, serializedObject);
        SyncFoldoutList();
    }
    
    public override void OnDisable()
    {
        base.OnDisable();
        
        if (cachedPreviewTexture != null)
        {
            UnityEngine.Object.DestroyImmediate(cachedPreviewTexture);
            cachedPreviewTexture = null;
        }
    }
    
    public override void OnManagerChanged(MBSceneDataManager manager)
    {
        base.OnManagerChanged(manager);
        SyncFoldoutList();
    }
    
    public override void DrawTab()
    {
        if (Generator == null)
        {
            DrawNoGeneratorWarning();
            return;
        }
        
        // Sub-tab bar
        DrawSubTabBar();
        EditorGUILayout.Space(10);
        
        // Draw current sub-tab content
        switch (currentSubTab)
        {
            case HeightmapSubTab.Layers:
                DrawLayersTab();
                break;
            case HeightmapSubTab.Settings:
                DrawSettingsTab();
                break;
        }
    }
    
    #region Sub-Tab Navigation
    
    private void DrawSubTabBar()
    {
        EditorGUILayout.BeginHorizontal();
        
        string[] subTabNames = { "Layers", "Settings" };
        
        for (int i = 0; i < subTabNames.Length; i++)
        {
            GUI.backgroundColor = (int)currentSubTab == i 
                ? new Color(0.6f, 0.85f, 0.95f) 
                : Color.white;
            
            if (GUILayout.Button(subTabNames[i], EditorStyles.miniButton, GUILayout.Height(20)))
            {
                currentSubTab = (HeightmapSubTab)i;
            }
        }
        
        GUI.backgroundColor = Color.white;
        EditorGUILayout.EndHorizontal();
    }
    
    #endregion
    
    #region Layers Tab
    
    private void DrawLayersTab()
    {
        // Generate button at top
        EditorGUILayout.BeginHorizontal();
        
        GUI.backgroundColor = new Color(0.4f, 0.9f, 0.4f);
        if (GUILayout.Button("Generate Heightmap", GUILayout.Height(28)))
        {
            GenerateHeightmap();
        }
        GUI.backgroundColor = Color.white;
        
        if (GUILayout.Button("Add Layer", GUILayout.Width(80), GUILayout.Height(28)))
        {
            AddNewLayer();
        }
        
        EditorGUILayout.EndHorizontal();
        
        EditorGUILayout.Space(5);
        DrawUILine(Color.grey);
        EditorGUILayout.Space(5);
        
        // Layer list
        SyncFoldoutList();
        
        for (int i = 0; i < Generator.layers.Count; i++)
        {
            DrawLayerEntry(i);
        }
        
        EditorGUILayout.Space(10);
        
        // Final preview
        if (cachedPreviewTexture != null)
        {
            DrawHeader("Final Blend");
            GUILayout.Label(cachedPreviewTexture, GUILayout.Width(100), GUILayout.Height(100));
        }
    }
    
    private void DrawLayerEntry(int index)
    {
        if (index >= Generator.layers.Count || index >= layerFoldouts.Count)
            return;
        
        var layer = Generator.layers[index];
        
        BeginBox();
        
        // Header row with foldout
        EditorGUILayout.BeginHorizontal();
        
        // Foldout with layer info
        string indicator = GetLayerTypeIndicator(layer.filterType);
        string displayName = $"{indicator} {layer.name}";
        
        if (!layer.active)
        {
            GUI.color = new Color(1, 1, 1, 0.5f);
            displayName = "[OFF] " + displayName;
        }
        
        layerFoldouts[index] = EditorGUILayout.Foldout(layerFoldouts[index], displayName, true);
        
        GUI.color = Color.white;
        
        GUILayout.FlexibleSpace();
        
        // Reorder buttons
        GUI.enabled = index > 0;
        if (GUILayout.Button("↑", GUILayout.Width(22), GUILayout.Height(18)))
        {
            SwapLayers(index, index - 1);
        }
        GUI.enabled = index < Generator.layers.Count - 1;
        if (GUILayout.Button("↓", GUILayout.Width(22), GUILayout.Height(18)))
        {
            SwapLayers(index, index + 1);
        }
        GUI.enabled = true;
        
        // Delete button (not for base layers)
        bool isBaseLayer = layer.filterType == LayeredHeightmapGenerator.FilterType.Generator ||
                          layer.filterType == LayeredHeightmapGenerator.FilterType.PFM;
        
        GUI.enabled = !isBaseLayer;
        GUI.backgroundColor = new Color(1f, 0.6f, 0.6f);
        if (GUILayout.Button("X", GUILayout.Width(22), GUILayout.Height(18)))
        {
            if (EditorUtility.DisplayDialog("Remove Layer", 
                $"Remove layer '{layer.name}'?", "Yes", "No"))
            {
                RemoveLayer(index);
                GUI.enabled = true;
                GUI.backgroundColor = Color.white;
                EditorGUILayout.EndHorizontal();
                EndBox();
                return;
            }
        }
        GUI.enabled = true;
        GUI.backgroundColor = Color.white;
        
        EditorGUILayout.EndHorizontal();
        
        // Expanded content
        if (layerFoldouts[index])
        {
            EditorGUI.indentLevel++;
            DrawLayerContents(layer, index);
            EditorGUI.indentLevel--;
        }
        
        EndBox();
    }
    
    private void DrawLayerContents(LayeredHeightmapGenerator.Layer layer, int index)
    {
        EditorGUILayout.Space(3);

        if (layer.filterType == LayeredHeightmapGenerator.FilterType.Generator)
        {
            EditorGUI.BeginDisabledGroup(true);
        }
        
        // Active toggle
        bool newActive = EditorGUILayout.Toggle("Active", layer.active);
        if (newActive != layer.active)
        {
            Undo.RecordObject(Generator, "Toggle Layer Active");
            layer.active = newActive;
            EditorUtility.SetDirty(Generator);
        }
        
        string newName = EditorGUILayout.TextField("Name", layer.name);
        if (newName != layer.name)
        {
            Undo.RecordObject(Generator, "Rename Layer");
            layer.name = newName;
            EditorUtility.SetDirty(Generator);
        }
        
        bool isBaseLayer = layer.filterType == LayeredHeightmapGenerator.FilterType.Generator ||
                          layer.filterType == LayeredHeightmapGenerator.FilterType.PFM;
        bool isPaintedLayer = layer.filterType == LayeredHeightmapGenerator.FilterType.Painted;
        
        // Special handling for Painted layer
        if (isPaintedLayer)
        {
            DrawPaintedLayerContents(layer);
            return;
        }
        
        // Filter type (disabled for base layers)
        EditorGUI.BeginDisabledGroup(isBaseLayer);
        var newFilterType = (LayeredHeightmapGenerator.FilterType)EditorGUILayout.EnumPopup(
            "Filter Type", layer.filterType);
        if (!isBaseLayer && newFilterType != layer.filterType)
        {
            Undo.RecordObject(Generator, "Change Filter Type");
            layer.filterType = newFilterType;
            EditorUtility.SetDirty(Generator);
        }
        EditorGUI.EndDisabledGroup();
        
        // Blend type
        var newBlendType = (LayeredHeightmapGenerator.BlendType)EditorGUILayout.EnumPopup(
            "Blend Type", layer.blendType);
        if (newBlendType != layer.blendType)
        {
            Undo.RecordObject(Generator, "Change Blend Type");
            layer.blendType = newBlendType;
            EditorUtility.SetDirty(Generator);
        }
        
        EditorGUILayout.Space(3);
        
        // Height control for Height/Noise layers
        if (layer.filterType == LayeredHeightmapGenerator.FilterType.Height ||
            layer.filterType == LayeredHeightmapGenerator.FilterType.Noise)
        {
            DrawHeightControlSection(layer);
        }
        
        // Common controls
        EditorGUILayout.Space(3);
        
        bool newInvert = EditorGUILayout.Toggle("Invert", layer.invert);
        if (newInvert != layer.invert)
        {
            Undo.RecordObject(Generator, "Toggle Invert");
            layer.invert = newInvert;
            EditorUtility.SetDirty(Generator);
        }
        
        // Intensity (for non-meters mode)
        if (!layer.useMetersHeight || isBaseLayer)
        {
            float newIntensity = EditorGUILayout.FloatField("Intensity", layer.intensity);
            if (!Mathf.Approximately(newIntensity, layer.intensity))
            {
                Undo.RecordObject(Generator, "Change Intensity");
                layer.intensity = newIntensity;
                EditorUtility.SetDirty(Generator);
            }
        }
        
        // Transformations
        var newFlip = (HeightmapHelper.Flip)EditorGUILayout.EnumPopup("Flip", layer.flip);
        if (newFlip != layer.flip)
        {
            Undo.RecordObject(Generator, "Change Flip");
            layer.flip = newFlip;
            EditorUtility.SetDirty(Generator);
        }
        
        var newRotation = (HeightmapHelper.Rotation)EditorGUILayout.EnumPopup("Rotation", layer.rotation);
        if (newRotation != layer.rotation)
        {
            Undo.RecordObject(Generator, "Change Rotation");
            layer.rotation = newRotation;
            EditorUtility.SetDirty(Generator);
        }
        
        EditorGUILayout.Space(3);
        
        // Type-specific settings
        DrawLayerTypeSpecificSettings(layer);
        
        if (layer.filterType == LayeredHeightmapGenerator.FilterType.Generator)
        {
            EditorGUI.EndDisabledGroup();   
        }
        
        // Preview
        if (layer.GeneratedTexture != null)
        {
            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField("Preview:", EditorStyles.miniLabel);
            GUILayout.Label(layer.GeneratedTexture, GUILayout.Width(80), GUILayout.Height(80));
        }
    }
    
    private void DrawPaintedLayerContents(LayeredHeightmapGenerator.Layer layer)
    {
        EditorGUILayout.Space(5);
        
        EditorGUI.BeginDisabledGroup(true);
        EditorGUILayout.EnumPopup("Filter Type", layer.filterType);
        EditorGUILayout.EnumPopup("Blend Type", layer.blendType);
        EditorGUI.EndDisabledGroup();
        
        bool hasPaintedData = layer.HasPaintedData();
        
        EditorGUILayout.Space(5);
        
        if (hasPaintedData)
        {
            EditorGUILayout.LabelField("Height Range:", EditorStyles.miniBoldLabel);
            EditorGUI.indentLevel++;
            EditorGUILayout.LabelField($"Max Height: {layer.heightInMeters:F2}m");
            EditorGUILayout.LabelField($"Max Depth: {layer.depthInMeters:F2}m");
            EditorGUI.indentLevel--;
        }
        else
        {
            EditorGUILayout.HelpBox("No painted data stored.\nUse 'Capture Painted' after making terrain edits.", MessageType.Info);
        }
        
        EditorGUILayout.Space(5);
        
        EditorGUILayout.BeginHorizontal();
        
        if (GUILayout.Button("Capture Now", GUILayout.Height(22)))
        {
            Undo.RegisterCompleteObjectUndo(Generator, "Capture Painted Layer");
            Generator.CapturePaintedLayer();
            SyncFoldoutList();
        }
        
        GUI.enabled = hasPaintedData;
        if (GUILayout.Button("Clear Data", GUILayout.Height(22)))
        {
            if (EditorUtility.DisplayDialog("Clear Painted Data", 
                "Clear all painted layer data?", "Yes", "No"))
            {
                Undo.RegisterCompleteObjectUndo(Generator, "Clear Painted Data");
                layer.ClearPaintedData();
                layer.heightmap = null;
                layer.heightmapInMeters = null;
                layer.GeneratedTexture = null;
                EditorUtility.SetDirty(Generator);
            }
        }
        GUI.enabled = true;
        
        EditorGUILayout.EndHorizontal();
        
        if (layer.GeneratedTexture != null)
        {
            EditorGUILayout.Space(5);
            GUILayout.Label(layer.GeneratedTexture, GUILayout.Width(80), GUILayout.Height(80));
        }
    }
    
    private void DrawHeightControlSection(LayeredHeightmapGenerator.Layer layer)
    {
        EditorGUILayout.LabelField("Height Control", EditorStyles.miniBoldLabel);
        
        bool newUseMeters = EditorGUILayout.Toggle(
            new GUIContent("Use Meters Mode", "Specify height in real meters instead of 0-1 range"),
            layer.useMetersHeight);
            
        if (newUseMeters != layer.useMetersHeight)
        {
            Undo.RecordObject(Generator, "Toggle Meters Mode");
            layer.useMetersHeight = newUseMeters;
            EditorUtility.SetDirty(Generator);
        }
        
        if (layer.useMetersHeight)
        {
            EditorGUI.indentLevel++;
            
            float newHeightMeters = EditorGUILayout.FloatField("Height (m)", layer.heightInMeters);
            newHeightMeters = Mathf.Max(0, newHeightMeters);
            
            float newDepthMeters = EditorGUILayout.FloatField("Depth (m)", layer.depthInMeters);
            newDepthMeters = Mathf.Max(0, newDepthMeters);
            
            if (!Mathf.Approximately(newHeightMeters, layer.heightInMeters) ||
                !Mathf.Approximately(newDepthMeters, layer.depthInMeters))
            {
                Undo.RecordObject(Generator, "Change Height/Depth");
                layer.heightInMeters = newHeightMeters;
                layer.depthInMeters = newDepthMeters;
                EditorUtility.SetDirty(Generator);
            }
            
            float totalRange = layer.heightInMeters + layer.depthInMeters;
            EditorGUILayout.LabelField($"Range: -{layer.depthInMeters}m to +{layer.heightInMeters}m ({totalRange}m)", 
                EditorStyles.miniLabel);
            
            EditorGUI.indentLevel--;
        }
    }
    
    private void DrawLayerTypeSpecificSettings(LayeredHeightmapGenerator.Layer layer)
    {
        switch (layer.filterType)
        {
            case LayeredHeightmapGenerator.FilterType.Height:
                EditorGUILayout.LabelField("Height Texture", EditorStyles.miniBoldLabel);
                var newTexture = (Texture2D)EditorGUILayout.ObjectField(
                    "Texture", layer.inputHeightTexture, typeof(Texture2D), false);
                if (newTexture != layer.inputHeightTexture)
                {
                    Undo.RecordObject(Generator, "Change Height Texture");
                    layer.inputHeightTexture = newTexture;
                    EditorUtility.SetDirty(Generator);
                }
                break;
                
            case LayeredHeightmapGenerator.FilterType.Noise:
                EditorGUILayout.LabelField("Noise Settings", EditorStyles.miniBoldLabel);
                
                float newFreq = EditorGUILayout.FloatField("Frequency", layer.frequency);
                if (!Mathf.Approximately(newFreq, layer.frequency))
                {
                    Undo.RecordObject(Generator, "Change Frequency");
                    layer.frequency = newFreq;
                    EditorUtility.SetDirty(Generator);
                }
                
                Vector2 newOffset = EditorGUILayout.Vector2Field("Offset", layer.noiseOffset);
                if (newOffset != layer.noiseOffset)
                {
                    Undo.RecordObject(Generator, "Change Noise Offset");
                    layer.noiseOffset = newOffset;
                    EditorUtility.SetDirty(Generator);
                }
                
                int newOctaves = EditorGUILayout.IntSlider("Octaves", layer.octaves, 1, 8);
                if (newOctaves != layer.octaves)
                {
                    Undo.RecordObject(Generator, "Change Octaves");
                    layer.octaves = newOctaves;
                    EditorUtility.SetDirty(Generator);
                }
                
                float newPersistence = EditorGUILayout.Slider("Persistence", layer.persistence, 0f, 1f);
                if (!Mathf.Approximately(newPersistence, layer.persistence))
                {
                    Undo.RecordObject(Generator, "Change Persistence");
                    layer.persistence = newPersistence;
                    EditorUtility.SetDirty(Generator);
                }
                
                float newLacunarity = EditorGUILayout.Slider("Lacunarity", layer.lacunarity, 1f, 4f);
                if (!Mathf.Approximately(newLacunarity, layer.lacunarity))
                {
                    Undo.RecordObject(Generator, "Change Lacunarity");
                    layer.lacunarity = newLacunarity;
                    EditorUtility.SetDirty(Generator);
                }
                break;
                
            case LayeredHeightmapGenerator.FilterType.Generator:
                EditorGUILayout.LabelField("Generator Settings", EditorStyles.miniBoldLabel);
                EditorGUILayout.ObjectField("Asset", layer.generatorAsset, typeof(TextAsset), false);
                EditorGUILayout.TextField("Hash", layer.generatorHash);
                break;
                
            case LayeredHeightmapGenerator.FilterType.PFM:
                EditorGUILayout.LabelField("PFM Settings", EditorStyles.miniBoldLabel);
                EditorGUI.BeginDisabledGroup(true);
                EditorGUILayout.ObjectField("Asset", layer.pfmAsset, typeof(DefaultAsset), false);
                EditorGUI.EndDisabledGroup();
                break;
        }
    }
    
    #endregion
    
    #region Settings Tab
    
    private void DrawSettingsTab()
    {
        // Terrain Settings
        DrawTerrainSettingsSection();
        EditorGUILayout.Space(5);
        
        // Resolution
        // DrawResolutionSection();
        // EditorGUILayout.Space(5);
        
        // Height Expansion
        DrawHeightExpansionSection();
        EditorGUILayout.Space(5);
        
        // Painted Layer Settings
        DrawPaintedLayerSettingsSection();
    }
    
    private void DrawTerrainSettingsSection()
    {
        DrawHeader("Terrain Size");
        
        BeginBox();
        
        GUI.enabled = false;
        // Manual terrain size
        Vector2 newSize = EditorGUILayout.Vector2Field("Size (m)", Generator.TerrainSize);
        if (newSize != Generator.TerrainSize)
        {
            Undo.RegisterCompleteObjectUndo(Generator, "Change Terrain Size");
            Undo.RegisterCompleteObjectUndo(Generator.terrain.terrainData, "Change Terrain Size");
            
            Generator.TerrainSize = newSize;
            Generator.terrain.terrainData.size = new Vector3(
                Generator.TerrainSize.x,
                Generator.TerrainHeight,
                Generator.TerrainSize.y);
            
            EditorUtility.SetDirty(Generator);
        }
        
        GUI.enabled = true;
        // Manual terrain height
        float newHeight = EditorGUILayout.FloatField("Height (m)", Generator.TerrainHeight);
        if (!Mathf.Approximately(newHeight, Generator.TerrainHeight))
        {
            Undo.RegisterCompleteObjectUndo(Generator, "Change Terrain Height");
            Undo.RegisterCompleteObjectUndo(Generator.terrain.terrainData, "Change Terrain Height");
            
            Generator.TerrainHeight = newHeight;
            Generator.terrain.terrainData.size = new Vector3(
                Generator.terrain.terrainData.size.x,
                Generator.TerrainHeight,
                Generator.terrain.terrainData.size.z);
            
            EditorUtility.SetDirty(Generator);
        }
        
        EndBox();
    }
    
    private void DrawResolutionSection()
    {
        DrawHeader("Heightmap Resolution");
        
        BeginBox();
        
        int[] resolutionOptions = { 33, 65, 129, 257, 513, 1025, 2049, 4097 };
        string[] resolutionLabels = { "33", "65", "129", "257", "513", "1025", "2049", "4097" };
        int currentResolution = Generator.terrain.terrainData.heightmapResolution;
        int selectedIndex = Array.IndexOf(resolutionOptions, currentResolution);
        
        if (selectedIndex == -1) selectedIndex = 4; // Default to 513
        
        int newSelectedIndex = EditorGUILayout.Popup("Resolution", selectedIndex, resolutionLabels);
        
        if (newSelectedIndex != selectedIndex)
        {
            Undo.RegisterCompleteObjectUndo(Generator.terrain.terrainData, "Change Heightmap Resolution");
            Generator.terrain.terrainData.heightmapResolution = resolutionOptions[newSelectedIndex];
            EditorUtility.SetDirty(Generator.terrain.terrainData);
        }
        
        EditorGUILayout.LabelField($"Current: {currentResolution} x {currentResolution}", EditorStyles.miniLabel);
        
        EndBox();
    }
    
    private void DrawHeightExpansionSection()
    {
        if (!DrawFoldout(ref showHeightExpansion, "Height Expansion"))
            return;
        
        BeginBox();
        
        EditorGUILayout.HelpBox(
            "Add extra headroom for painting mountains/valleys. Applied automatically on Generate.", 
            MessageType.Info);
        
        EditorGUILayout.Space(5);
        
        float newExpandUp = EditorGUILayout.FloatField(
            new GUIContent("Expand Up (m)", "Extra meters above terrain max for painting"),
            Generator.expandHeightAbove);
        newExpandUp = Mathf.Max(0, newExpandUp);
        
        float newExpandDown = EditorGUILayout.FloatField(
            new GUIContent("Expand Down (m)", "Extra meters below terrain min for digging"),
            Generator.expandDepthBelow);
        newExpandDown = Mathf.Max(0, newExpandDown);
        
        if (!Mathf.Approximately(newExpandUp, Generator.expandHeightAbove) || 
            !Mathf.Approximately(newExpandDown, Generator.expandDepthBelow))
        {
            Undo.RecordObject(Generator, "Change Height Expansion");
            Generator.expandHeightAbove = newExpandUp;
            Generator.expandDepthBelow = newExpandDown;
            EditorUtility.SetDirty(Generator);
        }
        
        bool hasExpansion = Generator.expandHeightAbove > 0 || Generator.expandDepthBelow > 0;
        
        if (hasExpansion)
        {
            EditorGUILayout.Space(3);
            EditorGUILayout.LabelField(
                $"Configured: +{Generator.expandHeightAbove}m up, +{Generator.expandDepthBelow}m down", 
                EditorStyles.miniLabel);
        }
        
        EditorGUILayout.Space(5);
        
        EditorGUILayout.BeginHorizontal();
        
        GUI.enabled = hasExpansion;
        if (DrawButton("Apply Now", new Color(0.7f, 0.85f, 1f), 22))
        {
            Undo.RegisterCompleteObjectUndo(Generator, "Apply Height Expansion");
            Undo.RegisterCompleteObjectUndo(Generator.terrain.terrainData, "Apply Height Expansion");
            Undo.RegisterCompleteObjectUndo(Generator.terrain, "Apply Height Expansion");
            
            Generator.ApplyHeightExpansion();
        }
        GUI.enabled = true;
        
        if (DrawButton("Reset", 22, 50))
        {
            Undo.RecordObject(Generator, "Reset Height Expansion");
            Generator.expandHeightAbove = 0f;
            Generator.expandDepthBelow = 0f;
            EditorUtility.SetDirty(Generator);
        }
        
        EditorGUILayout.EndHorizontal();
        
        EndBox();
    }
    
    private void DrawPaintedLayerSettingsSection()
    {
        if (!DrawFoldout(ref showPaintedLayer, "Painted Layer Settings"))
            return;
        
        BeginBox();
        
        bool newAutoPreserve = EditorGUILayout.Toggle(
            new GUIContent("Auto Preserve", "Automatically capture painted edits when regenerating"),
            Generator.autoPreservePaintedLayer);
            
        if (newAutoPreserve != Generator.autoPreservePaintedLayer)
        {
            Undo.RecordObject(Generator, "Toggle Auto Preserve");
            Generator.autoPreservePaintedLayer = newAutoPreserve;
            EditorUtility.SetDirty(Generator);
        }
        
        var paintedLayer = Generator.layers.Find(l => l.filterType == LayeredHeightmapGenerator.FilterType.Painted);
        bool hasPaintedData = paintedLayer != null && paintedLayer.HasPaintedData();
        
        EditorGUILayout.Space(5);
        
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Status:", GUILayout.Width(50));
        if (hasPaintedData)
        {
            EditorGUILayout.LabelField(
                $"Stored ({paintedLayer.heightInMeters:F1}m up, {paintedLayer.depthInMeters:F1}m down)", 
                EditorStyles.boldLabel);
        }
        else
        {
            EditorGUILayout.LabelField("No painted data", EditorStyles.miniLabel);
        }
        EditorGUILayout.EndHorizontal();
        
        EditorGUILayout.Space(5);
        
        EditorGUILayout.BeginHorizontal();
        
        if (DrawButton("Capture Now", 25))
        {
            Undo.RegisterCompleteObjectUndo(Generator, "Capture Painted Layer");
            Generator.CapturePaintedLayer();
            SyncFoldoutList();
        }
        
        GUI.enabled = hasPaintedData;
        if (DrawButton("Clear Painted", new Color(1f, 0.8f, 0.8f), 25))
        {
            if (EditorUtility.DisplayDialog("Clear Painted Layer", 
                "Clear all painted layer data?", "Yes", "No"))
            {
                Undo.RegisterCompleteObjectUndo(Generator, "Clear Painted Layer");
                Generator.ClearPaintedLayer();
                SyncFoldoutList();
            }
        }
        
        if (DrawButton("Remove Layer", new Color(1f, 0.7f, 0.7f), 25))
        {
            if (EditorUtility.DisplayDialog("Remove Painted Layer", 
                "Remove the painted layer entirely? All data will be lost.", "Yes", "No"))
            {
                Undo.RegisterCompleteObjectUndo(Generator, "Remove Painted Layer");
                Generator.RemovePaintedLayer();
                SyncFoldoutList();
            }
        }
        GUI.enabled = true;
        
        EditorGUILayout.EndHorizontal();
        
        EndBox();
    }
    
    #endregion
    
    #region Helper Methods
    
    private void DrawNoGeneratorWarning()
    {
        EditorGUILayout.HelpBox(
            "LayeredHeightmapGenerator not found on terrain.\n\n" +
            "Add a LayeredHeightmapGenerator component to your terrain to enable heightmap editing.",
            MessageType.Warning);
        
        EditorGUILayout.Space(10);
        
        if (manager.Terrain != null)
        {
            if (DrawButton("Add LayeredHeightmapGenerator", new Color(0.7f, 0.9f, 0.7f), 30))
            {
                Undo.AddComponent<LayeredHeightmapGenerator>(manager.Terrain.gameObject);
                manager.RefreshComponents();
            }
        }
        else
        {
            EditorGUILayout.HelpBox("Assign a terrain in the General tab first.", MessageType.Info);
        }
    }
    
    private void GenerateHeightmap()
    {
        Undo.RegisterCompleteObjectUndo(Generator, "Generate Heightmap");
        Undo.RegisterCompleteObjectUndo(Generator.terrain.terrainData, "Generate Heightmap");
        Undo.RegisterCompleteObjectUndo(Generator.terrain, "Generate Heightmap");
        
        Undo.SetCurrentGroupName("Generate Heightmap");
        int undoGroup = Undo.GetCurrentGroup();
        
        lastGeneratedHeightmap = Generator.GenerateHeightmap();
        UpdateCachedPreviewTexture();
        SyncFoldoutList();
        
        Undo.CollapseUndoOperations(undoGroup);
        
        Debug.Log($"[HeightmapTab] Generated heightmap. Final: {Generator.TerrainHeight:F2}m, Offset: {Generator.TerrainOffset:F2}m");
    }
    
    private void UpdateCachedPreviewTexture()
    {
        if (lastGeneratedHeightmap == null) return;
        
        if (cachedPreviewTexture != null)
        {
            UnityEngine.Object.DestroyImmediate(cachedPreviewTexture);
        }
        
        cachedPreviewTexture = Map2D.ConvertFloatArrayToTexture2D(lastGeneratedHeightmap);
    }
    
    private void AddNewLayer()
    {
        Undo.RegisterCompleteObjectUndo(Generator, "Add Layer");
        
        var newLayer = new LayeredHeightmapGenerator.Layer
        {
            name = "New Layer",
            filterType = LayeredHeightmapGenerator.FilterType.Noise,
            blendType = LayeredHeightmapGenerator.BlendType.Add,
            useMetersHeight = true,
            heightInMeters = 10f,
            depthInMeters = 0f
        };
        
        Generator.layers.Add(newLayer);
        SyncFoldoutList();
        layerFoldouts[layerFoldouts.Count - 1] = true; // Expand new layer
        
        EditorUtility.SetDirty(Generator);
    }
    
    private void RemoveLayer(int index)
    {
        Undo.RegisterCompleteObjectUndo(Generator, "Remove Layer");
        Generator.layers.RemoveAt(index);
        SyncFoldoutList();
        EditorUtility.SetDirty(Generator);
    }
    
    private void SwapLayers(int indexA, int indexB)
    {
        Undo.RegisterCompleteObjectUndo(Generator, "Reorder Layers");
        
        var temp = Generator.layers[indexA];
        Generator.layers[indexA] = Generator.layers[indexB];
        Generator.layers[indexB] = temp;
        
        bool tempFoldout = layerFoldouts[indexA];
        layerFoldouts[indexA] = layerFoldouts[indexB];
        layerFoldouts[indexB] = tempFoldout;
        
        EditorUtility.SetDirty(Generator);
    }
    
    private void SyncFoldoutList()
    {
        if (Generator == null) return;
        
        int targetCount = Generator.layers.Count;
        
        while (layerFoldouts.Count < targetCount)
        {
            layerFoldouts.Add(false);
        }
        
        while (layerFoldouts.Count > targetCount)
        {
            layerFoldouts.RemoveAt(layerFoldouts.Count - 1);
        }
    }
    
    private string GetLayerTypeIndicator(LayeredHeightmapGenerator.FilterType type)
    {
        switch (type)
        {
            case LayeredHeightmapGenerator.FilterType.Generator: return "[G]";
            case LayeredHeightmapGenerator.FilterType.PFM: return "[P]";
            case LayeredHeightmapGenerator.FilterType.Height: return "[H]";
            case LayeredHeightmapGenerator.FilterType.Noise: return "[N]";
            case LayeredHeightmapGenerator.FilterType.Painted: return "[*]";
            default: return "[ ]";
        }
    }
    
    private string TruncateName(string name, int maxLength)
    {
        if (string.IsNullOrEmpty(name)) return "";
        if (name.Length <= maxLength) return name;
        return name.Substring(0, maxLength - 2) + "..";
    }
    
    #endregion
}
