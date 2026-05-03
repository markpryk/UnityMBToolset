using System;
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using BDT.GUI.Helpers;

[CustomEditor(typeof(LayeredHeightmapGenerator))]
public class LayeredHeightmapEditor : Editor
{
    private LayeredHeightmapGenerator generator;
    private List<bool> layerFoldouts = new List<bool>();
    private float[,] _generatedHeightmap;
    private Texture2D _cachedPreviewTexture;
    
    // Change tracking
    private bool isDirty = false;
    private double lastChangeTime = 0;
    private const double DEBOUNCE_TIME = 0.3;
    
    // Hash tracking for change detection
    private int lastLayerConfigHash = 0;
    
    private void OnEnable()
    {
        generator = (LayeredHeightmapGenerator)target;
        if (generator.terrain == null)
            generator.terrain = generator.GetComponent<Terrain>();
        
        SyncFoldoutList(generator.layers.Count);
        lastLayerConfigHash = CalculateConfigHash();
        Undo.undoRedoPerformed += OnUndoRedo;
    }
    
    private void OnDisable()
    {
        Undo.undoRedoPerformed -= OnUndoRedo;
        
        if (_cachedPreviewTexture != null)
        {
            DestroyImmediate(_cachedPreviewTexture);
            _cachedPreviewTexture = null;
        }
    }
    
    private void OnUndoRedo()
    {
        isDirty = true;
        SyncFoldoutList(generator.layers.Count);
        
        // Sync terrain data with generator values after undo/redo
        if (generator.terrain != null && generator.terrain.terrainData != null)
        {
            generator.terrain.terrainData.size = new Vector3(
                generator.TerrainSize.x,
                generator.TerrainHeight,
                generator.TerrainSize.y);
            
            generator.terrain.transform.position = new Vector3(
                generator.terrain.transform.position.x,
                generator.TerrainOffset,
                generator.terrain.transform.position.z);
        }
        
        Repaint();
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        
        EditorGUI.BeginChangeCheck();

        DrawTerrainSection();
        DrawResolutionSection();
        DrawSizeSection();
        DrawTerrainMetricsSection();
        DrawHeightExpansionSection();
        DrawPaintedLayerSection();
        DrawLayersSection();
        DrawControlButtons();
        DrawPreview();

        if (EditorGUI.EndChangeCheck())
        {
            isDirty = true;
            lastChangeTime = EditorApplication.timeSinceStartup;
            serializedObject.ApplyModifiedProperties();
            EditorUtility.SetDirty(generator);
        }
        else
        {
            serializedObject.ApplyModifiedProperties();
        }
    }
    
    private int CalculateConfigHash()
    {
        unchecked
        {
            int hash = 17;
            hash = hash * 31 + generator.layers.Count;
            hash = hash * 31 + generator.expandHeightAbove.GetHashCode();
            hash = hash * 31 + generator.expandDepthBelow.GetHashCode();
            
            foreach (var layer in generator.layers)
            {
                hash = hash * 31 + layer.active.GetHashCode();
                hash = hash * 31 + layer.filterType.GetHashCode();
                hash = hash * 31 + layer.blendType.GetHashCode();
                hash = hash * 31 + layer.intensity.GetHashCode();
            }
            
            return hash;
        }
    }

    private void DrawTerrainSection()
    {
        EditorGUILayout.LabelField("Terrain", StylesHelpers.BoxHeader());

        float newHeight = EditorGUILayout.FloatField("Terrain Height", generator.TerrainHeight);
        if (!Mathf.Approximately(newHeight, generator.TerrainHeight))
        {
            Undo.RegisterCompleteObjectUndo(generator, "Change Terrain Height");
            Undo.RegisterCompleteObjectUndo(generator.terrain.terrainData, "Change Terrain Height");
            
            generator.TerrainHeight = newHeight;
            generator.terrain.terrainData.size = new Vector3(
                generator.terrain.terrainData.size.x, 
                generator.TerrainHeight,
                generator.terrain.terrainData.size.z);
        }
        
        GUIHelpers.DrawUILine(Color.grey, padding: 0);
    }

    private void DrawResolutionSection()
    {
        EditorGUILayout.LabelField("Heightmap Resolution", EditorStyles.boldLabel);

        int[] resolutionOptions = { 33, 65, 129, 257, 513, 1025, 2049, 4097 };
        string[] resolutionLabels = { "33", "65", "129", "257", "513", "1025", "2049", "4097" };
        int currentResolution = generator.terrain.terrainData.heightmapResolution;
        int selectedIndex = Array.IndexOf(resolutionOptions, currentResolution);

        if (selectedIndex == -1) selectedIndex = 0;

        int newSelectedIndex = EditorGUILayout.Popup("Resolution", selectedIndex, resolutionLabels);
        
        if (newSelectedIndex != selectedIndex)
        {
            Undo.RegisterCompleteObjectUndo(generator.terrain.terrainData, "Change Heightmap Resolution");
            generator.terrain.terrainData.heightmapResolution = resolutionOptions[newSelectedIndex];
            EditorUtility.SetDirty(generator.terrain.terrainData);
        }

        GUIHelpers.DrawUILine(Color.grey, padding: 0);
    }

    private void DrawSizeSection()
    {
        EditorGUILayout.LabelField("Terrain Size", EditorStyles.boldLabel);
        
        Vector2 newSize = EditorGUILayout.Vector2Field("Size", generator.TerrainSize);
        if (newSize != generator.TerrainSize)
        {
            Undo.RegisterCompleteObjectUndo(generator, "Change Terrain Size");
            Undo.RegisterCompleteObjectUndo(generator.terrain.terrainData, "Change Terrain Size");
            
            generator.TerrainSize = newSize;
            generator.terrain.terrainData.size = new Vector3(
                generator.TerrainSize.x,
                generator.TerrainHeight,
                generator.TerrainSize.y);
        }
        
        GUIHelpers.DrawUILine(Color.grey, padding: 0);
    }

    private void DrawTerrainMetricsSection()
    {
        EditorGUILayout.LabelField("Terrain Metrics", EditorStyles.boldLabel);
        
        EditorGUI.BeginDisabledGroup(true);
        EditorGUILayout.FloatField("Base Height (m)", generator.BaseTerrainHeight);
        EditorGUILayout.FloatField("Base Offset (m)", generator.BaseTerrainOffset);
        EditorGUILayout.Space(4);
        EditorGUILayout.FloatField("Final Height (m)", generator.TerrainHeight);
        EditorGUILayout.FloatField("Final Offset (m)", generator.TerrainOffset);
        EditorGUI.EndDisabledGroup();
        
        GUIHelpers.DrawUILine(Color.grey, padding: 0);
    }

    private void DrawHeightExpansionSection()
    {
        EditorGUILayout.LabelField("Height Expansion", StylesHelpers.BoxHeader());
        
        GUIHelpers.BeginContents();
        
        EditorGUILayout.HelpBox(
            "Configure extra headroom for painting. Applied automatically on every Generate.", 
            MessageType.Info);
        
        EditorGUILayout.Space(5);
        
        float newExpandUp = EditorGUILayout.FloatField(
            new GUIContent("Expand Up (m)", "Extra meters above terrain max for painting mountains"),
            generator.expandHeightAbove);
        newExpandUp = Mathf.Max(0, newExpandUp);
        
        float newExpandDown = EditorGUILayout.FloatField(
            new GUIContent("Expand Down (m)", "Extra meters below terrain min for digging valleys"),
            generator.expandDepthBelow);
        newExpandDown = Mathf.Max(0, newExpandDown);
        
        if (!Mathf.Approximately(newExpandUp, generator.expandHeightAbove) || 
            !Mathf.Approximately(newExpandDown, generator.expandDepthBelow))
        {
            Undo.RecordObject(generator, "Change Height Expansion");
            generator.expandHeightAbove = newExpandUp;
            generator.expandDepthBelow = newExpandDown;
        }
        
        bool hasExpansion = generator.expandHeightAbove > 0 || generator.expandDepthBelow > 0;
        
        if (hasExpansion)
        {
            EditorGUILayout.Space(5);
            string expansionInfo = string.Format("Configured: +{0}m up, +{1}m down (applied on Generate)", 
                generator.expandHeightAbove, generator.expandDepthBelow);
            EditorGUILayout.LabelField(expansionInfo, EditorStyles.miniLabel);
        }
        
        EditorGUILayout.Space(5);
        
        EditorGUILayout.BeginHorizontal();
        
        GUI.enabled = hasExpansion;
        if (GUILayout.Button(new GUIContent("Apply Now", 
            "Apply expansion to current terrain without regenerating (preserves painted edits)"), 
            GUILayout.Height(22)))
        {
            // Register complete object undo for terrain modifications
            Undo.RegisterCompleteObjectUndo(generator, "Apply Height Expansion");
            Undo.RegisterCompleteObjectUndo(generator.terrain.terrainData, "Apply Height Expansion");
            Undo.RegisterCompleteObjectUndo(generator.terrain, "Apply Height Expansion");
            
            Undo.SetCurrentGroupName("Apply Height Expansion");
            int undoGroup = Undo.GetCurrentGroup();
            
            generator.ApplyHeightExpansion();
            
            Undo.CollapseUndoOperations(undoGroup);
        }
        GUI.enabled = true;
        
        if (GUILayout.Button(new GUIContent("Reset", "Set expansion to zero"), 
            GUILayout.Width(50), GUILayout.Height(22)))
        {
            Undo.RecordObject(generator, "Reset Height Expansion");
            generator.expandHeightAbove = 0f;
            generator.expandDepthBelow = 0f;
        }
        
        EditorGUILayout.EndHorizontal();
        
        GUIHelpers.EndContents();
        GUIHelpers.DrawUILine(Color.grey, padding: 0);
    }

    private void DrawPaintedLayerSection()
    {
        EditorGUILayout.LabelField("Painted Layer", StylesHelpers.BoxHeader());
        
        GUIHelpers.BeginContents();
        
        bool newAutoPreserve = EditorGUILayout.Toggle(
            new GUIContent("Auto Preserve", "Automatically capture and restore painted edits when regenerating"),
            generator.autoPreservePaintedLayer);
            
        if (newAutoPreserve != generator.autoPreservePaintedLayer)
        {
            Undo.RecordObject(generator, "Toggle Auto Preserve");
            generator.autoPreservePaintedLayer = newAutoPreserve;
        }
        
        var paintedLayer = generator.layers.Find(l => l.filterType == LayeredHeightmapGenerator.FilterType.Painted);
        bool hasPaintedData = paintedLayer != null && paintedLayer.HasPaintedData();
        
        EditorGUILayout.Space(5);
        
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.PrefixLabel("Status");
        if (hasPaintedData)
        {
            string info = string.Format("Stored ({0:F1}m up, {1:F1}m down)", 
                paintedLayer.heightInMeters, paintedLayer.depthInMeters);
            EditorGUILayout.LabelField(info, EditorStyles.miniLabel);
        }
        else
        {
            EditorGUILayout.LabelField("No painted data", EditorStyles.miniLabel);
        }
        EditorGUILayout.EndHorizontal();
        
        EditorGUILayout.Space(5);
        
        EditorGUILayout.BeginHorizontal();
        
        if (GUILayout.Button("Capture Now", GUILayout.Height(25)))
        {
            Undo.RegisterCompleteObjectUndo(generator, "Capture Painted Layer");
            generator.CapturePaintedLayer();
            SyncFoldoutList(generator.layers.Count);
        }
        
        GUI.enabled = hasPaintedData;
        if (GUILayout.Button("Clear Painted", GUILayout.Height(25)))
        {
            if (EditorUtility.DisplayDialog("Clear Painted Layer", 
                "Are you sure you want to clear the painted layer data?", "Yes", "No"))
            {
                Undo.RegisterCompleteObjectUndo(generator, "Clear Painted Layer");
                generator.ClearPaintedLayer();
                SyncFoldoutList(generator.layers.Count);
            }
        }
        GUI.enabled = true;
        
        EditorGUILayout.EndHorizontal();
        
        if (hasPaintedData && paintedLayer.GeneratedTexture != null)
        {
            EditorGUILayout.Space(5);
            GUILayout.Label("Preview:", EditorStyles.miniLabel);
            GUILayout.Label(paintedLayer.GeneratedTexture, GUILayout.Width(80), GUILayout.Height(80));
        }
        
        GUIHelpers.EndContents();
        GUIHelpers.DrawUILine(Color.grey, padding: 0);
    }

    private void DrawLayersSection()
    {
        EditorGUILayout.LabelField("Heightmap Layers", StylesHelpers.BoxHeader());

        SyncFoldoutList(generator.layers.Count);

        for (int i = 0; i < generator.layers.Count; i++)
        {
            if (i >= layerFoldouts.Count)
            {
                SyncFoldoutList(generator.layers.Count);
                break;
            }
            
            DrawLayerSection(i);
        }

        EditorGUILayout.Space(10);

        if (GUILayout.Button("Add New Layer", GUILayout.Height(30)))
        {
            Undo.RegisterCompleteObjectUndo(generator, "Add Layer");
            var newLayer = new LayeredHeightmapGenerator.Layer
            {
                name = "New Layer",
                filterType = LayeredHeightmapGenerator.FilterType.Noise,
                blendType = LayeredHeightmapGenerator.BlendType.Add,
                useMetersHeight = true,
                heightInMeters = 10f,
                depthInMeters = 0f
            };
            generator.layers.Add(newLayer);
            SyncFoldoutList(generator.layers.Count);
            EditorUtility.SetDirty(generator);
        }
    }

    private void DrawControlButtons()
    {
        EditorGUILayout.Space(5);
        
        if (GUILayout.Button("Generate Heightmap", GUILayout.Height(30)))
        {
            // Register undo for all affected objects
            Undo.RegisterCompleteObjectUndo(generator, "Generate Heightmap");
            Undo.RegisterCompleteObjectUndo(generator.terrain.terrainData, "Generate Heightmap");
            Undo.RegisterCompleteObjectUndo(generator.terrain, "Generate Heightmap");
            
            // Group all changes together
            Undo.SetCurrentGroupName("Generate Heightmap");
            int undoGroup = Undo.GetCurrentGroup();
            
            _generatedHeightmap = generator.GenerateHeightmap();
            UpdateCachedPreviewTexture();
            lastLayerConfigHash = CalculateConfigHash();
            
            // Sync foldouts after generation (painted layer might have been added)
            SyncFoldoutList(generator.layers.Count);
            
            Undo.CollapseUndoOperations(undoGroup);
        }
    }

    private void UpdateCachedPreviewTexture()
    {
        if (_generatedHeightmap == null) return;
        
        if (_cachedPreviewTexture != null)
        {
            DestroyImmediate(_cachedPreviewTexture);
        }
        
        _cachedPreviewTexture = Map2D.ConvertFloatArrayToTexture2D(_generatedHeightmap);
    }

    private void DrawPreview()
    {
        if (_cachedPreviewTexture != null)
        {
            EditorGUILayout.Space(10);
            GUILayout.Label("Final Blend:", EditorStyles.boldLabel);
            GUILayout.Label(_cachedPreviewTexture, GUILayout.Width(100), GUILayout.Height(100));
        }
        else if (_generatedHeightmap != null)
        {
            UpdateCachedPreviewTexture();
            if (_cachedPreviewTexture != null)
            {
                EditorGUILayout.Space(10);
                GUILayout.Label("Final Blend:", EditorStyles.boldLabel);
                GUILayout.Label(_cachedPreviewTexture, GUILayout.Width(100), GUILayout.Height(100));
            }
        }
    }

    private void DrawLayerSection(int index)
    {
        if (index >= generator.layers.Count || index >= layerFoldouts.Count)
        {
            return;
        }
        
        var layer = generator.layers[index];

        GUIHelpers.BeginContents();
        
        bool foldoutResult = DrawBoldFoldoutWithButton(index, layer.name);
        
        if (index >= layerFoldouts.Count)
        {
            GUIHelpers.EndContents();
            return;
        }
        
        layerFoldouts[index] = foldoutResult;

        if (layerFoldouts[index])
        {
            EditorGUI.indentLevel++;
            DrawLayerContents(layer, index);
            EditorGUI.indentLevel--;
        }
        
        GUIHelpers.EndContents();
    }
    
    private void DrawLayerContents(LayeredHeightmapGenerator.Layer layer, int index)
    {
        bool newActive = EditorGUILayout.Toggle("Active", layer.active);
        if (newActive != layer.active)
        {
            Undo.RecordObject(generator, "Toggle Layer Active");
            layer.active = newActive;
        }
        
        string newName = EditorGUILayout.TextField("Name", layer.name);
        if (newName != layer.name)
        {
            Undo.RecordObject(generator, "Rename Layer");
            layer.name = newName;
        }
        
        bool isBaseLayer = layer.filterType == LayeredHeightmapGenerator.FilterType.Generator ||
                           layer.filterType == LayeredHeightmapGenerator.FilterType.PFM;
        bool isPaintedLayer = layer.filterType == LayeredHeightmapGenerator.FilterType.Painted;
        
        if (isPaintedLayer)
        {
            DrawPaintedLayerContent(layer);
            return;
        }
        
        EditorGUI.BeginDisabledGroup(isBaseLayer);
        var newFilterType = (LayeredHeightmapGenerator.FilterType)EditorGUILayout.EnumPopup(
            "Filter Type", layer.filterType);
        if (!isBaseLayer && newFilterType != layer.filterType)
        {
            Undo.RecordObject(generator, "Change Filter Type");
            layer.filterType = newFilterType;
        }
        EditorGUI.EndDisabledGroup();
        
        var newBlendType = (LayeredHeightmapGenerator.BlendType)EditorGUILayout.EnumPopup(
            "Blend Type", layer.blendType);
        if (newBlendType != layer.blendType)
        {
            Undo.RecordObject(generator, "Change Blend Type");
            layer.blendType = newBlendType;
        }

        EditorGUILayout.Space(5);
        
        if (layer.filterType == LayeredHeightmapGenerator.FilterType.Height ||
            layer.filterType == LayeredHeightmapGenerator.FilterType.Noise)
        {
            DrawHeightControlSection(layer);
        }

        EditorGUILayout.Space(5);
        
        bool newInvert = EditorGUILayout.Toggle("Invert", layer.invert);
        if (newInvert != layer.invert)
        {
            Undo.RecordObject(generator, "Toggle Invert");
            layer.invert = newInvert;
        }
        
        float minVal = layer.minValue;
        float maxVal = layer.maxValue;
        EditorGUILayout.MinMaxSlider(new GUIContent("Remap Range"), ref minVal, ref maxVal, 0, 1);
        
        EditorGUILayout.BeginHorizontal();
        minVal = EditorGUILayout.FloatField("Min", minVal);
        maxVal = EditorGUILayout.FloatField("Max", maxVal);
        EditorGUILayout.EndHorizontal();
        
        minVal = Mathf.Clamp01(minVal);
        maxVal = Mathf.Clamp01(maxVal);
        
        if (!Mathf.Approximately(minVal, layer.minValue) || !Mathf.Approximately(maxVal, layer.maxValue))
        {
            Undo.RecordObject(generator, "Change Remap Range");
            layer.minValue = minVal;
            layer.maxValue = maxVal;
        }
        
        var newFlip = (HeightmapHelper.Flip)EditorGUILayout.EnumPopup("Flip", layer.flip);
        if (newFlip != layer.flip)
        {
            Undo.RecordObject(generator, "Change Flip");
            layer.flip = newFlip;
        }
        
        var newRotation = (HeightmapHelper.Rotation)EditorGUILayout.EnumPopup("Rotation", layer.rotation);
        if (newRotation != layer.rotation)
        {
            Undo.RecordObject(generator, "Change Rotation");
            layer.rotation = newRotation;
        }
        
        if (!layer.useMetersHeight || isBaseLayer)
        {
            float newIntensity = EditorGUILayout.FloatField("Intensity", layer.intensity);
            if (!Mathf.Approximately(newIntensity, layer.intensity))
            {
                Undo.RecordObject(generator, "Change Intensity");
                layer.intensity = newIntensity;
            }
        }
        
        float newContrast = EditorGUILayout.FloatField("Contrast", layer.contrast);
        if (!Mathf.Approximately(newContrast, layer.contrast))
        {
            Undo.RecordObject(generator, "Change Contrast");
            layer.contrast = newContrast;
        }
        
        EditorGUILayout.Space(5);
        
        DrawLayerSpecificSettings(layer);
        
        if (layer.GeneratedTexture != null)
        {
            DrawTexturePreview(layer.GeneratedTexture);
        }
    }

    private void DrawPaintedLayerContent(LayeredHeightmapGenerator.Layer layer)
    {
        EditorGUILayout.Space(5);
        
        EditorGUI.BeginDisabledGroup(true);
        EditorGUILayout.EnumPopup("Filter Type", layer.filterType);
        EditorGUILayout.EnumPopup("Blend Type", layer.blendType);
        EditorGUI.EndDisabledGroup();
        
        EditorGUILayout.Space(5);
        
        bool hasPaintedData = layer.HasPaintedData();
        
        if (hasPaintedData)
        {
            EditorGUILayout.LabelField("Height Range", EditorStyles.boldLabel);
            EditorGUI.BeginDisabledGroup(true);
            EditorGUILayout.FloatField("Max Height (m)", layer.heightInMeters);
            EditorGUILayout.FloatField("Max Depth (m)", layer.depthInMeters);
            EditorGUI.EndDisabledGroup();
            
            EditorGUILayout.Space(5);
            
            string info = string.Format("Painted data: {0:F1}m up, {1:F1}m down", 
                layer.heightInMeters, layer.depthInMeters);
            EditorGUILayout.HelpBox(info, MessageType.Info);
        }
        else
        {
            EditorGUILayout.HelpBox("No painted data stored yet.\nUse 'Capture Now' or enable 'Auto Preserve' and regenerate after painting.", MessageType.Info);
        }
        
        EditorGUILayout.Space(5);
        
        EditorGUILayout.BeginHorizontal();
        
        if (GUILayout.Button("Capture Now", GUILayout.Height(22)))
        {
            Undo.RegisterCompleteObjectUndo(generator, "Capture Painted Layer");
            generator.CapturePaintedLayer();
            SyncFoldoutList(generator.layers.Count);
        }
        
        GUI.enabled = hasPaintedData;
        if (GUILayout.Button("Clear Data", GUILayout.Height(22)))
        {
            if (EditorUtility.DisplayDialog("Clear Painted Data", 
                "Are you sure you want to clear the painted layer data?", "Yes", "No"))
            {
                Undo.RegisterCompleteObjectUndo(generator, "Clear Painted Data");
                layer.ClearPaintedData();
                layer.heightmap = null;
                layer.heightmapInMeters = null;
                layer.GeneratedTexture = null;
                EditorUtility.SetDirty(generator);
            }
        }
        GUI.enabled = true;
        
        EditorGUILayout.EndHorizontal();
        
        if (layer.GeneratedTexture != null)
        {
            DrawTexturePreview(layer.GeneratedTexture);
        }
    }

    private void DrawHeightControlSection(LayeredHeightmapGenerator.Layer layer)
    {
        EditorGUILayout.LabelField("Height Control", EditorStyles.boldLabel);
        
        GUIHelpers.BeginContents();
        
        bool newUseMeters = EditorGUILayout.Toggle(
            new GUIContent("Use Meters Mode", 
                "When enabled, specify height contribution in real meters"),
            layer.useMetersHeight);
            
        if (newUseMeters != layer.useMetersHeight)
        {
            Undo.RecordObject(generator, "Toggle Meters Mode");
            layer.useMetersHeight = newUseMeters;
        }

        if (layer.useMetersHeight)
        {
            EditorGUI.indentLevel++;
            
            float newHeightMeters = EditorGUILayout.FloatField(
                new GUIContent("Height (m)", 
                    "Maximum elevation this layer adds above the base terrain"),
                layer.heightInMeters);
            newHeightMeters = Mathf.Max(0, newHeightMeters);
            
            float newDepthMeters = EditorGUILayout.FloatField(
                new GUIContent("Depth (m)", 
                    "Maximum depth this layer digs below the base terrain"),
                layer.depthInMeters);
            newDepthMeters = Mathf.Max(0, newDepthMeters);
            
            if (!Mathf.Approximately(newHeightMeters, layer.heightInMeters) ||
                !Mathf.Approximately(newDepthMeters, layer.depthInMeters))
            {
                Undo.RecordObject(generator, "Change Height/Depth");
                layer.heightInMeters = newHeightMeters;
                layer.depthInMeters = newDepthMeters;
            }

            float totalRange = layer.heightInMeters + layer.depthInMeters;
            string rangeInfo = string.Format("Layer range: -{0}m to +{1}m ({2}m total)", 
                layer.depthInMeters, layer.heightInMeters, totalRange);
            EditorGUILayout.HelpBox(rangeInfo, MessageType.Info);
            
            EditorGUI.indentLevel--;
        }
        else
        {
            EditorGUILayout.HelpBox(
                "Legacy mode: Layer values (0-1) are multiplied by Intensity", 
                MessageType.None);
        }
        
        GUIHelpers.EndContents();
    }

    private void DrawLayerSpecificSettings(LayeredHeightmapGenerator.Layer layer)
    {
        switch (layer.filterType)
        {
            case LayeredHeightmapGenerator.FilterType.Height:
                EditorGUILayout.LabelField("Height Texture", EditorStyles.boldLabel);
                var newTexture = (Texture2D)EditorGUILayout.ObjectField(
                    "Texture", layer.inputHeightTexture, typeof(Texture2D), false);
                if (newTexture != layer.inputHeightTexture)
                {
                    Undo.RecordObject(generator, "Change Height Texture");
                    layer.inputHeightTexture = newTexture;
                }
                break;
                
            case LayeredHeightmapGenerator.FilterType.Noise:
                EditorGUILayout.LabelField("Noise Settings", EditorStyles.boldLabel);
                
                float newFreq = EditorGUILayout.FloatField("Frequency", layer.frequency);
                if (!Mathf.Approximately(newFreq, layer.frequency))
                {
                    Undo.RecordObject(generator, "Change Frequency");
                    layer.frequency = newFreq;
                }
                
                Vector2 newOffset = EditorGUILayout.Vector2Field("Offset", layer.noiseOffset);
                if (newOffset != layer.noiseOffset)
                {
                    Undo.RecordObject(generator, "Change Noise Offset");
                    layer.noiseOffset = newOffset;
                }
                
                int newOctaves = EditorGUILayout.IntSlider("Octaves", layer.octaves, 1, 8);
                if (newOctaves != layer.octaves)
                {
                    Undo.RecordObject(generator, "Change Octaves");
                    layer.octaves = newOctaves;
                }
                
                float newPersistence = EditorGUILayout.Slider("Persistence", layer.persistence, 0f, 1f);
                if (!Mathf.Approximately(newPersistence, layer.persistence))
                {
                    Undo.RecordObject(generator, "Change Persistence");
                    layer.persistence = newPersistence;
                }
                
                float newLacunarity = EditorGUILayout.Slider("Lacunarity", layer.lacunarity, 1f, 4f);
                if (!Mathf.Approximately(newLacunarity, layer.lacunarity))
                {
                    Undo.RecordObject(generator, "Change Lacunarity");
                    layer.lacunarity = newLacunarity;
                }
                break;
                
            case LayeredHeightmapGenerator.FilterType.Generator:
                EditorGUILayout.LabelField("Generator Settings", EditorStyles.boldLabel);
                // EditorGUI.BeginDisabledGroup(true);
                EditorGUILayout.ObjectField("Generator Asset", layer.generatorAsset, typeof(TextAsset), false);
                EditorGUILayout.TextField("Hash", layer.generatorHash);
                // EditorGUI.EndDisabledGroup();
                break;
                
            case LayeredHeightmapGenerator.FilterType.PFM:
                EditorGUILayout.LabelField("PFM Settings", EditorStyles.boldLabel);
                EditorGUI.BeginDisabledGroup(true);
                EditorGUILayout.ObjectField("PFM Asset", layer.pfmAsset, typeof(DefaultAsset), false);
                EditorGUI.EndDisabledGroup();
                break;
        }
    }

    private void DrawTexturePreview(Texture2D texture)
    {
        if (texture != null)
        {
            EditorGUILayout.Space(5);
            GUILayout.Label("Preview:", EditorStyles.boldLabel);
            GUILayout.Label(texture, GUILayout.Width(100), GUILayout.Height(100));
        }
    }

    private void SyncFoldoutList(int newSize)
    {
        while (layerFoldouts.Count < newSize)
        {
            layerFoldouts.Add(false);
        }

        while (layerFoldouts.Count > newSize)
        {
            layerFoldouts.RemoveAt(layerFoldouts.Count - 1);
        }
    }

    public bool DrawBoldFoldoutWithButton(int index, string content)
    {
        if (index >= layerFoldouts.Count)
        {
            SyncFoldoutList(generator.layers.Count);
        }
        
        if (index >= layerFoldouts.Count || index >= generator.layers.Count)
        {
            return false;
        }
        
        var foldout = layerFoldouts[index];
        var layer = generator.layers[index];
        GUIHelpers.InitGUIStyles();

        EditorGUILayout.BeginHorizontal(GUIHelpers.foldoutBackgroundStyle);

        string typeIndicator = GetLayerTypeIndicator(layer.filterType);
        string displayName = typeIndicator + " " + content;
        
        if (!layer.active)
        {
            GUI.color = new Color(1, 1, 1, 0.5f);
            displayName = "[OFF] " + displayName;
        }

        bool result = EditorGUILayout.Foldout(foldout, displayName, GUIHelpers.foldoutStyle);
        
        GUI.color = Color.white;

        GUILayout.FlexibleSpace();

        Color originalColor = GUI.backgroundColor;

        GUI.backgroundColor = GUIHelpers.HelperColor(GUIHelpers.GUIHelperColors.VividCerulean);
        if (GUILayout.Button(EditorGUIUtility.IconContent("d_scrollup"), GUILayout.Width(25), GUILayout.Height(15)))
        {
            if (index > 0)
            {
                SwapLayers(index, index - 1);
            }
        }

        if (GUILayout.Button(EditorGUIUtility.IconContent("d_scrolldown"), GUILayout.Width(25), GUILayout.Height(15)))
        {
            if (index < generator.layers.Count - 1)
            {
                SwapLayers(index, index + 1);
            }
        }

        bool isBaseLayer = layer.filterType == LayeredHeightmapGenerator.FilterType.Generator ||
                          layer.filterType == LayeredHeightmapGenerator.FilterType.PFM;
        bool isPaintedLayer = layer.filterType == LayeredHeightmapGenerator.FilterType.Painted;
        
        GUI.backgroundColor = GUIHelpers.HelperColor(GUIHelpers.GUIHelperColors.AdobeBrown);
        GUI.enabled = !isBaseLayer;
        
        if (GUILayout.Button("X", GUILayout.Width(25), GUILayout.Height(15)))
        {
            string confirmMessage = isPaintedLayer 
                ? "Are you sure you want to remove the Painted layer? All painted data will be lost."
                : "Are you sure you want to remove this layer?";
                
            if (EditorUtility.DisplayDialog("Remove Layer", confirmMessage, "Yes", "No"))
            {
                Undo.RegisterCompleteObjectUndo(generator, "Remove Layer");
                generator.layers.RemoveAt(index);
                SyncFoldoutList(generator.layers.Count);
                EditorUtility.SetDirty(generator);
                
                GUI.enabled = true;
                GUI.backgroundColor = originalColor;
                EditorGUILayout.EndHorizontal();
                return false;
            }
        }

        GUI.enabled = true;
        GUI.backgroundColor = originalColor;

        EditorGUILayout.EndHorizontal();

        return result;
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

    private void SwapLayers(int indexA, int indexB)
    {
        Undo.RegisterCompleteObjectUndo(generator, "Reorder Layers");
        
        var temp = generator.layers[indexA];
        generator.layers[indexA] = generator.layers[indexB];
        generator.layers[indexB] = temp;
        
        bool tempFoldout = layerFoldouts[indexA];
        layerFoldouts[indexA] = layerFoldouts[indexB];
        layerFoldouts[indexB] = tempFoldout;
        
        EditorUtility.SetDirty(generator);
    }
}