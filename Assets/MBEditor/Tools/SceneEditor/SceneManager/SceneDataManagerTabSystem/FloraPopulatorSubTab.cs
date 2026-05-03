using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using UnityEditor;
using UnityEngine;
using BDT.GUI.Helpers;
using Debug = UnityEngine.Debug;
using Object = UnityEngine.Object;
using Random = UnityEngine.Random;

//  FloraDecoratorSubTab - UI + Spawn Logic

/// <summary>
/// Decorator subtab for the Flora tab. Procedurally places terrain
/// details and trees using a rule-based layer stack.
///
/// Spawn approach follows the VegetationSpawner patterns:
///   • Detail entries → per-texel grid iteration → <c>SetDetailLayer</c>
///   • Tree entries   → Poisson disc sampling    → <c>SetTreeInstances</c>
///   • Splatmap, height, slope, curvature, noise filters per texel / point
/// </summary>
internal class FloraPopulatorSubTab : IFloraSubTab
{
    public string SubTabName => "Flora Populator";

    #region Visual Constants

    // Flora-specific palette
    private static readonly Color FloraGreen      = new Color(0.4f, 0.8f, 0.4f);
    private static readonly Color FloraLayerColor = new Color(0.45f, 0.75f, 0.45f);
    private static readonly Color RGLLayerColor   = new Color(0.4f, 0.65f, 1f);
    private static readonly Color DisabledColor   = new Color(0.5f, 0.5f, 0.5f);
    private static readonly Color InvalidColor    = new Color(0.9f, 0.4f, 0.4f);

    // Decorator-matched layout constants
    private const float LayerHeaderHeight = 28f;
    private const float BadgeHeight = 16f;
    private const float IndentGuideWidth = 3f;

    #endregion

    #region State

    private FloraPopulatorConfig _config;
    private Vector2 _layerScroll;
    private Vector2 _detailScroll;
    private Vector2 _thumbnailScroll;
    private int _selectedMaskIdx;

    // Preview Mask State
    private bool _previewMaskActive;
    private Texture2D _previewTexture;
    private Texture _originalOverlayTexture;

    // Cached terrain min/max for height slider
    private Vector2 _terrainMinMaxHeight = new Vector2(-100f, 2000f);

    // Cached styles (initialized once, like DecoratorTab)
    private GUIStyle _badgeStyle;
    private GUIStyle _clickableHeaderStyle;
    private GUIStyle _miniLabelCentered;

    #endregion

    #region Session State

    private static int SelectedLayerIdx
    {
        get => SessionState.GetInt("FloraDecorator_SelectedLayer", -1);
        set => SessionState.SetInt("FloraDecorator_SelectedLayer", value);
    }
    private static bool ShowGlobalSettings
    {
        get => SessionState.GetBool("FloraDecorator_ShowGlobal", true);
        set => SessionState.SetBool("FloraDecorator_ShowGlobal", value);
    }
    private static bool ShowSpawnRules
    {
        get => SessionState.GetBool("FloraDecorator_ShowRules", true);
        set => SessionState.SetBool("FloraDecorator_ShowRules", value);
    }
    private static bool ShowFilters
    {
        get => SessionState.GetBool("FloraDecorator_ShowFilters", false);
        set => SessionState.SetBool("FloraDecorator_ShowFilters", value);
    }
    private static bool ShowMasks
    {
        get => SessionState.GetBool("FloraDecorator_ShowMasks", false);
        set => SessionState.SetBool("FloraDecorator_ShowMasks", value);
    }

    #endregion

    #region Log

    private static void LogAdd(string text)
    {
        Debug.Log($"[Flora] {text}");
    }

    #endregion
    
    //  Live Preview Mask

    private void TogglePreviewMask(FloraDecoratorLayer layer, Terrain terrain)
    {
        if (terrain == null || terrain.materialTemplate == null) return;
        
        _previewMaskActive = !_previewMaskActive;
        if (_previewMaskActive)
        {
            _originalOverlayTexture = terrain.materialTemplate.GetTexture("_OverlayTex");
            GeneratePreviewMask(layer, terrain);
        }
        else
        {
            ClearPreviewMask(terrain);
        }
    }
    
    private void GeneratePreviewMask(FloraDecoratorLayer layer, Terrain terrain)
    {
        if (!_previewMaskActive || terrain == null || terrain.materialTemplate == null) return;
        
        var td = terrain.terrainData;
        int w = 256;
        int h = 256;
        
        if (_previewTexture == null || _previewTexture.width != w)
        {
            if (_previewTexture != null) Object.DestroyImmediate(_previewTexture);
            _previewTexture = new Texture2D(w, h, TextureFormat.RGBA32, false);
            _previewTexture.hideFlags = HideFlags.HideAndDontSave;
            _previewTexture.wrapMode = TextureWrapMode.Clamp;
        }

        if (layer.SplatmapMasks.Count > 0 && (_cachedAlphamaps == null || _cachedAlphamaps.GetLength(2) != td.alphamapLayers))
            BuildAlphamapCache(td);

        int globalSeed = _config.Seed;
        Color[] pixels = new Color[w * h];
        Color clear = new Color(0, 0, 0, 0);
        Color redTint = new Color(1, 0, 0, 1f);

        for (int x = 0; x < w; x++)
        {
            for (int y = 0; y < h; y++)
            {
                int idx = y * w + x;
                // Normalize so nx is x/w and ny is y/h (note coordinates are often mapped ny -> x, nx -> y depending on unity terrain)
                // VegSpawner maps nx = y/h, ny = x/w internally for details, but for this preview we just use standard nx,ny and match GetInterpolatedHeight
                float nx = (float)x / (w - 1);
                float ny = (float)y / (h - 1);

                float height = td.GetInterpolatedHeight(nx, ny);
                float worldHeight = height + terrain.transform.position.y;

                if (worldHeight < layer.HeightRange.x || worldHeight > layer.HeightRange.y) { pixels[idx] = clear; continue; }

                if (layer.WaterFilter == FloraWaterFilter.RejectUnderwater && worldHeight <= _config.WaterHeight) { pixels[idx] = clear; continue; }
                if (layer.WaterFilter == FloraWaterFilter.OnlyUnderwater && worldHeight > _config.WaterHeight) { pixels[idx] = clear; continue; }

                if (layer.SlopeRange.x > 0 || layer.SlopeRange.y < 90f)
                {
                    float slope = td.GetSteepness(nx, ny);
                    if (slope < layer.SlopeRange.x || slope > layer.SlopeRange.y) { pixels[idx] = clear; continue; }
                }

                if (layer.CurvatureRange.x > 0 || layer.CurvatureRange.y < 1f)
                {
                    float curv = SampleCurvature(td, nx, ny);
                    if (curv < layer.CurvatureRange.x || curv > layer.CurvatureRange.y) { pixels[idx] = clear; continue; }
                }

                float noise = Mathf.PerlinNoise(nx * td.size.x * layer.NoiseScale + layer.Seed * 0.1f, ny * td.size.z * layer.NoiseScale);
                if (noise < layer.NoiseThreshold) { pixels[idx] = clear; continue; }

                if (layer.SplatmapMasks.Count > 0 && !PassesSplatmapCheck(layer, td, nx, ny)) { pixels[idx] = clear; continue; }

                pixels[idx] = redTint;
            }
        }
        
        _previewTexture.SetPixels(pixels);
        _previewTexture.Apply();
        terrain.materialTemplate.SetTexture("_OverlayTex", _previewTexture);
        
        // Ensure scene repaints to show the new overlay
        SceneView.RepaintAll();
    }
    
    private void ClearPreviewMask(Terrain terrain)
    {
        _previewMaskActive = false;
        
        if (terrain != null && terrain.materialTemplate != null)
        {
            if (terrain.materialTemplate.GetTexture("_OverlayTex") == _previewTexture)
                terrain.materialTemplate.SetTexture("_OverlayTex", _originalOverlayTexture);
            
            // Ensure scene repaints after reverting
            SceneView.RepaintAll();
        }
        
        if (_previewTexture != null)
        {
            Object.DestroyImmediate(_previewTexture);
            _previewTexture = null;
        }
    }

    private readonly Dictionary<string, Texture2D> _previewCache = new Dictionary<string, Texture2D>();
    private FloraPresetToolbar _presetToolbar = new FloraPresetToolbar();

    private float[,,] _cachedAlphamaps;
    private int _cachedAlphamapW, _cachedAlphamapH, _cachedAlphamapLayers;
    

    public void OnEnable(FloraTab parent)
    {
        RecalcTerrainMinMax(parent);
        _previewCache.Clear();
    }

    public void OnDisable()
    {
        ClearPreviewMask(null);
        _previewCache.Clear();
    }

    public void OnManagerChanged(FloraTab parent)
    {
        ClearPreviewMask(parent.SceneTerrain);
        RecalcTerrainMinMax(parent);
        SelectedLayerIdx = -1;
        _previewCache.Clear();
    }
    
    /// <summary>
    /// Reads all alphamaps from TerrainData into a managed float[,,] array.
    /// Call once before any spawn loop that uses splatmap masks.
    /// GetAlphamaps returns [y, x, layer] ordering.
    /// </summary>
    private void BuildAlphamapCache(TerrainData td)
    {
        _cachedAlphamapW = td.alphamapWidth;
        _cachedAlphamapH = td.alphamapHeight;
        _cachedAlphamapLayers = td.alphamapLayers;
        _cachedAlphamaps = td.GetAlphamaps(0, 0, _cachedAlphamapW, _cachedAlphamapH);
    }

    private void ClearAlphamapCache()
    {
        _cachedAlphamaps = null;
    }

    //  Style & UI Helpers (matching DecoratorTab patterns)

    private void EnsureStyles()
    {
        if (_badgeStyle != null) return;

        _badgeStyle = new GUIStyle(EditorStyles.miniLabel)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 9,
            fontStyle = FontStyle.Bold,
            normal = { textColor = Color.white }
        };

        _miniLabelCentered = new GUIStyle(EditorStyles.miniLabel)
        {
            alignment = TextAnchor.MiddleCenter
        };

        _clickableHeaderStyle = new GUIStyle(EditorStyles.boldLabel)
        {
            fontSize = 11,
            alignment = TextAnchor.MiddleLeft
        };
    }

    private void DrawBadge(Rect rect, string text, Color color)
    {
        EditorGUI.DrawRect(rect, color);
        GUI.color = GetContrastTextColor(color);
        EditorGUI.LabelField(rect, text, _badgeStyle);
        GUI.color = Color.white;
    }

    private static Color GetContrastTextColor(Color bg)
    {
        float luminance = 0.299f * bg.r + 0.587f * bg.g + 0.114f * bg.b;
        return luminance > 0.5f ? Color.black : Color.white;
    }

    /// <summary>
    /// Block section header matching DecoratorTab's DrawBlockHeader.
    /// Colored accent bar, title, subtitle, count badge.
    /// </summary>
    private void DrawBlockHeader(string title, string subtitle, Color color, string badgeText = null)
    {
        Rect rect = EditorGUILayout.GetControlRect(GUILayout.Height(24));
        EditorGUI.DrawRect(rect, color * 0.2f);
        EditorGUI.DrawRect(new Rect(rect.x, rect.y, 4, rect.height), color);
        EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, 1), color * 0.6f);

        float x = rect.x + 10;
        GUI.color = color;
        EditorGUI.LabelField(new Rect(x, rect.y + 2, 120, 20), title, EditorStyles.boldLabel);
        GUI.color = Color.white;
        x += Mathf.Max(70, _badgeStyle.CalcSize(new GUIContent(title)).x + 8);

        GUI.color = new Color(1, 1, 1, 0.5f);
        EditorGUI.LabelField(new Rect(x, rect.y + 4, 300, 16), subtitle, EditorStyles.miniLabel);
        GUI.color = Color.white;

        if (!string.IsNullOrEmpty(badgeText))
            DrawBadge(new Rect(rect.xMax - 50, rect.y + 4, 40, 16), badgeText, color);
    }

    //  UI Drawing

    public void DrawSubTab(FloraTab parent)
    {
        EnsureStyles();
        EditorGUILayout.Space(4);

        _config = parent.manager.FloraPopulationConfig;

        if (_config == null)
        {
            EditorGUILayout.HelpBox(
                "Create or assign a Flora Populator Config to define per-scene vegetation rules.",
                MessageType.Info);

            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            GUI.backgroundColor = FloraGreen;
            if (GUILayout.Button("Create Config", GUILayout.Width(130), GUILayout.Height(24)))
            {
                var go = parent.manager.gameObject;
                Undo.RecordObject(go, "Add FloraPopulatorConfig");
                go.AddComponent<FloraPopulatorConfig>();
                EditorUtility.SetDirty(go);
            }
            GUI.backgroundColor = Color.white;
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
            return;
        }

        if (parent.SceneTerrain != null)
        {
            RecalcTerrainMinMax(parent);
            foreach (var layer in _config.Layers)
            {
                // Old default was (0, 1000) - migrate to actual terrain bounds
                if (layer.HeightRange.x == 0f && layer.HeightRange.y == 1000f)
                {
                    layer.HeightRange = _terrainMinMaxHeight;
                    EditorUtility.SetDirty(_config);
                }
            }
        }

        _presetToolbar.DrawPopulatorToolbar(
            _config,
            parent.Library,
            parent.manager.Module?.ID,
            parent.manager.SceneName);

        EditorGUILayout.Space(4);
        DrawGlobalSettings(parent);

        EditorGUILayout.Space(5);
        UIHelpers.DrawUILine(UIColors.GrayLine, 1);
        EditorGUILayout.Space(5);

        DrawLayerStack(parent);

        EditorGUILayout.Space(5);
        UIHelpers.DrawUILine(UIColors.GrayLine, 1);
        EditorGUILayout.Space(5);

        if (SelectedLayerIdx >= 0 && SelectedLayerIdx < _config.Layers.Count)
        {
            _detailScroll = EditorGUILayout.BeginScrollView(_detailScroll);
            DrawSelectedLayer(parent);
            EditorGUILayout.EndScrollView();
        }
        else
        {
            EditorGUILayout.HelpBox("Select a layer from the list above to edit its rules.", MessageType.Info);
        }

        EditorGUILayout.Space(5);
        UIHelpers.DrawUILine(UIColors.GrayLine, 2);
        EditorGUILayout.Space(5);

        DrawSpawnActions(parent);
    }


    private void DrawGlobalSettings(FloraTab parent)
    {
        // Clickable block header with foldout
        Color settingsColor = UIColors.Orange;
        Rect hRect = EditorGUILayout.GetControlRect(GUILayout.Height(24));
        EditorGUI.DrawRect(hRect, ShowGlobalSettings ? settingsColor * 0.2f : settingsColor * 0.12f);
        EditorGUI.DrawRect(new Rect(hRect.x, hRect.y, ShowGlobalSettings ? 5 : 3, hRect.height), settingsColor);
        if (ShowGlobalSettings)
            EditorGUI.DrawRect(new Rect(hRect.x, hRect.y, hRect.width, 1), settingsColor * 0.6f);

        string indicator = ShowGlobalSettings ? "▼" : "▶";
        GUI.color = settingsColor;
        EditorGUI.LabelField(new Rect(hRect.x + 10, hRect.y + 2, 200, 20),
            $"{indicator}  SETTINGS", EditorStyles.boldLabel);
        GUI.color = Color.white;

        GUI.color = new Color(1, 1, 1, 0.4f);
        EditorGUI.LabelField(new Rect(hRect.x + 130, hRect.y + 5, 300, 16),
            "Global spawning parameters", EditorStyles.miniLabel);
        GUI.color = Color.white;

        if (Event.current.type == EventType.MouseDown && hRect.Contains(Event.current.mousePosition))
        {
            ShowGlobalSettings = !ShowGlobalSettings;
            Event.current.Use();
        }
        EditorGUIUtility.AddCursorRect(hRect, MouseCursor.Link);

        if (!ShowGlobalSettings) return;

        // Content with indent guide
        EditorGUILayout.BeginHorizontal();
        Rect guideRect = EditorGUILayout.GetControlRect(GUILayout.Width(IndentGuideWidth), GUILayout.ExpandHeight(true));

        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUI.BeginChangeCheck();
        EditorGUILayout.Space(2);

        DrawSeedField("Seed", ref _config.Seed);
        _config.WarbandTerrainCode = EditorGUILayout.TextField("Warband Code", _config.WarbandTerrainCode);

        _config.GlobalDensityMultiplier = EditorGUILayout.Slider(
            "Density Multiplier", _config.GlobalDensityMultiplier, 0f, 2f);
        _config.WaterHeight = EditorGUILayout.FloatField("Water Height", _config.WaterHeight);

        _config.AutoRespawn = EditorGUILayout.Toggle(
            new GUIContent("Auto Respawn", "Automatically respawn the selected layer when parameters change"),
            _config.AutoRespawn);

        if (parent.SceneTerrain != null)
        {
            parent.SceneTerrain.drawTreesAndFoliage = EditorGUILayout.Toggle(
                new GUIContent("Draw Vegetation", "Toggle rendering of trees and details on the scene terrain"),
                parent.SceneTerrain.drawTreesAndFoliage);
        }

        _config.UseCollisionMask = EditorGUILayout.Toggle("Collision Masking", _config.UseCollisionMask);
        if (_config.UseCollisionMask)
        {
            EditorGUI.indentLevel++;
            _config.CollisionLayer = EditorGUILayout.MaskField("Collision Layers",
                _config.CollisionLayer, UnityEditorInternal.InternalEditorUtility.layers);
            _config.CollisionCellSize = EditorGUILayout.IntSlider("Cell Size", _config.CollisionCellSize, 16, 128);
            _config.CollisionSubdivisions = EditorGUILayout.IntSlider("Subdivisions", _config.CollisionSubdivisions, 1, 8);
            EditorGUI.indentLevel--;
        }

        // Terrain info
        if (parent.SceneTerrain != null)
        {
            var td = parent.SceneTerrain.terrainData;
            EditorGUILayout.Space(2);
            GUI.color = new Color(1, 1, 1, 0.5f);
            EditorGUILayout.LabelField(
                $"Terrain: {td.size.x:F0}×{td.size.z:F0} m  ·  Detail: {td.detailResolution}px  ·  " +
                $"Layers: {td.terrainLayers.Length}  ·  Height: {_terrainMinMaxHeight.x:F0}..{_terrainMinMaxHeight.y:F0}",
                EditorStyles.miniLabel);
            GUI.color = Color.white;
        }

        if (EditorGUI.EndChangeCheck())
            EditorUtility.SetDirty(_config);

        EditorGUILayout.Space(2);
        EditorGUILayout.EndVertical();
        EditorGUILayout.EndHorizontal();

        // Draw indent guide on repaint
        if (Event.current.type == EventType.Repaint)
            EditorGUI.DrawRect(new Rect(guideRect.x + 1, guideRect.y, IndentGuideWidth, guideRect.height), settingsColor * 0.5f);
    }


    private void DrawLayerStack(FloraTab parent)
    {
        // Block header
        DrawBlockHeader("LAYERS", "Click to select · Right-click for actions",
            FloraLayerColor, $"{_config.Layers.Count}L");

        // Action bar (matches DecoratorTab.DrawLayersActionBar)
        EditorGUILayout.Space(4);
        EditorGUILayout.BeginHorizontal();

        GUI.backgroundColor = FloraGreen;
        if (GUILayout.Button("+ Add Layer", GUILayout.Width(90), GUILayout.Height(24)))
        {
            Undo.RecordObject(_config, "Add Decorator Layer");
            var layer = new FloraDecoratorLayer
            {
                Name = $"Layer {_config.Layers.Count}",
                Seed = Random.Range(0, 9999),
                HeightRange = _terrainMinMaxHeight // init from actual terrain
            };
            _config.Layers.Add(layer);
            SelectedLayerIdx = _config.Layers.Count - 1;
            EditorUtility.SetDirty(_config);
        }
        GUI.backgroundColor = Color.white;

        GUILayout.FlexibleSpace();

        // Expand/collapse all (matching decorator ⊞⊟)
        if (_config.Layers.Count > 0)
        {
            // Select all / deselect
            if (GUILayout.Button("All On", EditorStyles.miniButton, GUILayout.Width(46), GUILayout.Height(24)))
            {
                Undo.RecordObject(_config, "Enable All Layers");
                foreach (var l in _config.Layers) l.Enabled = true;
                EditorUtility.SetDirty(_config);
            }
            if (GUILayout.Button("All Off", EditorStyles.miniButton, GUILayout.Width(46), GUILayout.Height(24)))
            {
                Undo.RecordObject(_config, "Disable All Layers");
                foreach (var l in _config.Layers) l.Enabled = false;
                EditorUtility.SetDirty(_config);
            }
        }

        EditorGUILayout.EndHorizontal();

        if (_config.Layers.Count == 0)
        {
            EditorGUILayout.HelpBox("Add a layer to define spawn rules.", MessageType.Info);
            return;
        }

        // Layer list (dynamic height - fits content, scrolls when > max)
        EditorGUILayout.Space(4);
        float rowHeight = LayerHeaderHeight + 2; // 28 + 2 spacing
        float listHeight = Mathf.Min(_config.Layers.Count * rowHeight + 4, 260);
        _layerScroll = EditorGUILayout.BeginScrollView(_layerScroll, GUILayout.Height(listHeight));

        for (int i = 0; i < _config.Layers.Count; i++)
        {
            DrawLayerRow(i, parent);
            EditorGUILayout.Space(1);
        }

        EditorGUILayout.EndScrollView();
    }

    private void DrawLayerRow(int index, FloraTab parent)
    {
        var layer = _config.Layers[index];
        bool isSelected = SelectedLayerIdx == index;
        bool isRGL = layer.LayerType == FloraLayerType.RGL_Automatic;

        Color layerColor = !layer.Enabled ? DisabledColor
            : isRGL ? RGLLayerColor
            : FloraLayerColor;

        Rect rect = EditorGUILayout.GetControlRect(GUILayout.Height(LayerHeaderHeight));

        // Background
        Color bgColor = isSelected ? layerColor * 0.3f : layerColor * 0.15f;
        EditorGUI.DrawRect(rect, bgColor);

        // Left accent bar
        float barWidth = isSelected ? 6 : 4;
        EditorGUI.DrawRect(new Rect(rect.x, rect.y, barWidth, rect.height),
            layer.Enabled ? layerColor : DisabledColor);

        // Top accent when selected
        if (isSelected)
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, 2), layerColor);

        float x = rect.x + barWidth + 4;

        Rect toggleRect = new Rect(x, rect.y + 4, 18, 18);
        EditorGUI.BeginChangeCheck();
        bool newEnabled = EditorGUI.Toggle(toggleRect, layer.Enabled);
        if (EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(_config, "Toggle Layer");
            layer.Enabled = newEnabled;
            EditorUtility.SetDirty(_config);
        }
        x += 22;

        bool hasDetails = HasRegisteredEntriesOfCategory(layer, parent, FloraCategory.Detail);
        bool hasTrees = HasRegisteredEntriesOfCategory(layer, parent, FloraCategory.Tree);

        string catText;
        Color catBadgeColor;
        if (isRGL)
        {
            catText = "RGL";
            catBadgeColor = RGLLayerColor;
        }
        else if (hasTrees && hasDetails)
        {
            catText = "DT";
            catBadgeColor = new Color(0.5f, 0.8f, 0.5f);
        }
        else if (hasTrees)
        {
            catText = "T";
            catBadgeColor = new Color(0.6f, 0.85f, 0.6f);
        }
        else if (hasDetails)
        {
            catText = "D";
            catBadgeColor = new Color(0.7f, 0.9f, 0.5f);
        }
        else
        {
            catText = "·";
            catBadgeColor = DisabledColor;
        }

        DrawBadge(new Rect(x, rect.y + 6, 28, BadgeHeight),
            catText, layer.Enabled ? catBadgeColor : DisabledColor);
        x += 32;

        float buttonsWidth = 86; // space for badges + delete button on the right
        Rect clickArea = new Rect(x, rect.y, rect.xMax - x - buttonsWidth, rect.height);

        if (!layer.Enabled) GUI.color = new Color(1, 1, 1, 0.5f);
        string displayName = isSelected ? $"▸ {layer.Name}" : $"  {layer.Name}";
        EditorGUI.LabelField(new Rect(x, rect.y + 4, clickArea.width, 20),
            displayName, isSelected ? _clickableHeaderStyle : EditorStyles.label);
        GUI.color = Color.white;

        if (Event.current.type == EventType.MouseDown && clickArea.Contains(Event.current.mousePosition))
        {
            if (SelectedLayerIdx != index)
                ClearPreviewMask(parent.SceneTerrain);
                
            SelectedLayerIdx = index;
            Event.current.Use();
        }
        EditorGUIUtility.AddCursorRect(clickArea, MouseCursor.Link);

        float rightX = rect.xMax - buttonsWidth;

        // Entry count badge
        int entryCount = layer.EntryIDs?.Count ?? 0;
        if (entryCount > 0 || isRGL)
        {
            string entryBadge = isRGL ? "AUTO" : $"{entryCount}E";
            DrawBadge(new Rect(rightX, rect.y + 6, 36, BadgeHeight),
                entryBadge, entryCount > 0 || isRGL ? UIColors.Cyan * 0.7f : DisabledColor);
        }
        rightX += 40;

        // Instance count
        if (layer.LastInstanceCount > 0)
        {
            GUI.color = new Color(1, 1, 1, 0.45f);
            EditorGUI.LabelField(new Rect(rightX - 4, rect.y + 6, 42, 16),
                layer.LastInstanceCount.ToString("##,#"), _miniLabelCentered);
            GUI.color = Color.white;
        }

        // Delete button
        float delX = rect.xMax - 22;
        GUI.backgroundColor = InvalidColor;
        if (GUI.Button(new Rect(delX, rect.y + 4, 18, 18), "×", EditorStyles.miniButton))
        {
            if (_config.Layers.Count == 1 || EditorUtility.DisplayDialog("Remove Layer",
                    $"Remove layer \"{layer.Name}\"?", "Remove", "Cancel"))
            {
                Undo.RecordObject(_config, "Remove Decorator Layer");
                _config.Layers.RemoveAt(index);
                SelectedLayerIdx = Mathf.Clamp(SelectedLayerIdx, 0, _config.Layers.Count - 1);
                if (_config.Layers.Count == 0) SelectedLayerIdx = -1;
                EditorUtility.SetDirty(_config);
            }
        }
        GUI.backgroundColor = Color.white;

        if (Event.current.type == EventType.ContextClick && rect.Contains(Event.current.mousePosition))
        {
            SelectedLayerIdx = index;
            ShowLayerContextMenu(index, parent);
            Event.current.Use();
        }
    }

    private void ShowLayerContextMenu(int index, FloraTab parent)
    {
        var layer = _config.Layers[index];
        var menu = new GenericMenu();

        // Spawn / Clear
        bool canSpawn = parent.Library != null && parent.SceneTerrain != null;
        if (canSpawn)
        {
            menu.AddItem(new GUIContent("▶ Spawn This Layer"), false, () => SpawnSingleLayer(parent, layer));
            menu.AddItem(new GUIContent("Clear This Layer"), false, () =>
            {
                if (EditorUtility.DisplayDialog("Clear Layer", $"Clear all instances for \"{layer.Name}\"?", "Clear", "Cancel"))
                    ClearLayerInstances(parent, layer);
            });
            menu.AddSeparator("");
        }

        // Reorder
        if (index > 0)
            menu.AddItem(new GUIContent("Move Up"), false, () =>
            {
                Undo.RecordObject(_config, "Move Layer Up");
                Swap(index, index - 1);
                if (SelectedLayerIdx == index) SelectedLayerIdx--;
            });
        else
            menu.AddDisabledItem(new GUIContent("Move Up"));

        if (index < _config.Layers.Count - 1)
            menu.AddItem(new GUIContent("Move Down"), false, () =>
            {
                Undo.RecordObject(_config, "Move Layer Down");
                Swap(index, index + 1);
                if (SelectedLayerIdx == index) SelectedLayerIdx++;
            });
        else
            menu.AddDisabledItem(new GUIContent("Move Down"));

        menu.AddSeparator("");

        // Duplicate
        menu.AddItem(new GUIContent("Duplicate"), false, () =>
        {
            Undo.RecordObject(_config, "Duplicate Decorator Layer");
            var copy = layer.Duplicate();
            _config.Layers.Insert(index + 1, copy);
            SelectedLayerIdx = index + 1;
            EditorUtility.SetDirty(_config);
        });

        // Toggle
        menu.AddItem(new GUIContent(layer.Enabled ? "Disable" : "Enable"), false, () =>
        {
            Undo.RecordObject(_config, "Toggle Layer");
            layer.Enabled = !layer.Enabled;
            EditorUtility.SetDirty(_config);
        });

        menu.AddSeparator("");

        // Delete
        menu.AddItem(new GUIContent("Delete"), false, () =>
        {
            if (EditorUtility.DisplayDialog("Remove Layer", $"Remove layer \"{layer.Name}\"?", "Remove", "Cancel"))
            {
                Undo.RecordObject(_config, "Remove Decorator Layer");
                _config.Layers.RemoveAt(index);
                SelectedLayerIdx = Mathf.Clamp(SelectedLayerIdx, 0, _config.Layers.Count - 1);
                if (_config.Layers.Count == 0) SelectedLayerIdx = -1;
                EditorUtility.SetDirty(_config);
            }
        });

        menu.ShowAsContext();
    }


    private void DrawSelectedLayer(FloraTab parent)
    {
        var layer = _config.Layers[SelectedLayerIdx];

        // Header with stats
        string stats = layer.LastInstanceCount > 0
            ? $"  ({layer.LastInstanceCount:##,#} instances, {layer.LastSpawnTimeMs:F0}ms)"
            : "";
        EditorGUILayout.LabelField($"▸ {layer.Name}{stats}", EditorStyles.boldLabel);

        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUI.BeginChangeCheck();

        layer.Name = EditorGUILayout.TextField("Name", layer.Name);
        layer.LayerType = (FloraLayerType)EditorGUILayout.EnumPopup("Layer Type", layer.LayerType);

        //  RGL AUTOMATIC - Streamlined UI (unchanged)
        if (layer.LayerType == FloraLayerType.RGL_Automatic)
        {
            EditorGUILayout.HelpBox(
                "RGL Mode: Automatically spawns ALL flora defined by the " +
                "Warband engine for the current biome.",
                MessageType.Info);

            EditorGUILayout.Space(4);
            ShowMasks = EditorGUILayout.Foldout(ShowMasks, "Splatmap Masks", true, EditorStyles.foldoutHeader);
            if (ShowMasks) DrawSplatmapMasks(layer, parent);

            RGLDecoratorIntegrationUI.DrawRGLDecoratorSection(parent, layer, _config);
        }
        //  STANDARD - Organized with foldouts
        else
        {
            // Flora entries (thumbnail strip)
            EditorGUILayout.Space(4);
            DrawEntryThumbnails(layer, parent);

            EditorGUILayout.Space(4);
            ShowSpawnRules = EditorGUILayout.Foldout(ShowSpawnRules, "Spawn Rules", true, EditorStyles.foldoutHeader);
            if (ShowSpawnRules)
            {
                EditorGUI.indentLevel++;
                DrawSeedField("Seed", ref layer.Seed);
                layer.Probability = EditorGUILayout.Slider("Spawn Chance %", layer.Probability, 0f, 100f);

                DrawRangeSlider("Height Range", ref layer.HeightRange,
                    _terrainMinMaxHeight.x, _terrainMinMaxHeight.y);
                DrawRangeSlider("Slope Range (°)", ref layer.SlopeRange, 0f, 90f);
                DrawRangeSlider("Curvature", ref layer.CurvatureRange, 0f, 1f);

                // Tree-specific (auto-shown when trees exist)
                bool hasTreeEntries = HasRegisteredEntriesOfCategory(layer, parent, FloraCategory.Tree);
                if (hasTreeEntries)
                {
                    EditorGUILayout.Space(2);
                    EditorGUILayout.LabelField("Tree Placement", EditorStyles.miniBoldLabel);
                    layer.TreeDistance = EditorGUILayout.Slider("Distance Between", layer.TreeDistance, 0.5f, 50f);
                    DrawRangeSlider("Scale Range", ref layer.TreeScaleRange, 0f, 2f);
                    layer.TreeSinkAmount = EditorGUILayout.Slider(
                        new GUIContent("Sink Amount", "Lowers the Y position of the tree"),
                        layer.TreeSinkAmount, 0f, 1f);
                }
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space(2);
            ShowFilters = EditorGUILayout.Foldout(ShowFilters, "Filters", true, EditorStyles.foldoutHeader);
            if (ShowFilters)
            {
                EditorGUI.indentLevel++;

                // Noise
                layer.NoiseScale = EditorGUILayout.Slider("Noise Scale", layer.NoiseScale, 0.001f, 0.5f);
                layer.NoiseThreshold = EditorGUILayout.Slider("Noise Threshold", layer.NoiseThreshold, 0f, 1f);

                EditorGUILayout.Space(2);

                layer.CollisionCheck = EditorGUILayout.Toggle(
                    new GUIContent("Collision Check", "Avoid spawning inside colliders (configure in Global Settings)"),
                    layer.CollisionCheck);
                
                if (layer.CollisionCheck)
                {
                    EditorGUI.indentLevel++;
                    layer.CollisionClearance = EditorGUILayout.Slider(
                        new GUIContent("Collision Clearance", "Extra minimum distance (padding) from colliders"),
                        layer.CollisionClearance, 0f, 10f);
                    EditorGUI.indentLevel--;
                }
                
                layer.WaterFilter = (FloraWaterFilter)EditorGUILayout.EnumPopup(
                    new GUIContent("Water Filter", "None, Reject Underwater, or Only Underwater"),
                    layer.WaterFilter);

                EditorGUILayout.Space(2);

                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space(2);
            ShowMasks = EditorGUILayout.Foldout(ShowMasks, "Splatmap Masks", true, EditorStyles.foldoutHeader);
            if (ShowMasks) DrawSplatmapMasks(layer, parent);
        }

        bool changed = EditorGUI.EndChangeCheck();
        if (changed)
        {
            EditorUtility.SetDirty(_config);

            // Auto-respawn if enabled (standard layers only - RGL uses Apply button)
            if (layer.LayerType != FloraLayerType.RGL_Automatic
                && _config.AutoRespawn && parent.SceneTerrain != null && parent.Library != null)
            {
                SpawnSingleLayer(parent, layer);
            }

            if (_previewMaskActive)
            {
                GeneratePreviewMask(layer, parent.SceneTerrain);
            }
        }

        EditorGUILayout.Space(4);
        EditorGUILayout.BeginHorizontal();

        bool canSpawn = parent.Library != null && parent.SceneTerrain != null;
        EditorGUI.BeginDisabledGroup(!canSpawn);

        Color orig = GUI.backgroundColor;
        
        // Preview button
        GUI.backgroundColor = _previewMaskActive ? FloraGreen : orig;
        if (GUILayout.Button(new GUIContent(_previewMaskActive ? "👁 Previewing" : "👁 Preview Mask", "Temporarily overrides terrain tint to show valid spawn zone"), EditorStyles.miniButton, GUILayout.Width(85), GUILayout.Height(22)))
        {
            TogglePreviewMask(layer, parent.SceneTerrain);
        }
        GUI.backgroundColor = orig;

        GUI.backgroundColor = UIColors.Green;
        if (GUILayout.Button("▶ Spawn Layer", GUILayout.Height(22)))
            SpawnSingleLayer(parent, layer);
        GUI.backgroundColor = orig;

        GUI.backgroundColor = UIColors.Red * 0.8f;
        if (GUILayout.Button("Clear", EditorStyles.miniButton, GUILayout.Width(50), GUILayout.Height(22)))
        {
            if (EditorUtility.DisplayDialog("Clear Layer",
                    $"Clear all instances for \"{layer.Name}\"?", "Clear", "Cancel"))
                ClearLayerInstances(parent, layer);
        }
        GUI.backgroundColor = orig;

        EditorGUI.EndDisabledGroup();

        // Duplicate (quick access alongside spawn/clear)
        if (GUILayout.Button("⊕ Dup", EditorStyles.miniButton, GUILayout.Width(50), GUILayout.Height(22)))
        {
            Undo.RecordObject(_config, "Duplicate Decorator Layer");
            var copy = layer.Duplicate();
            _config.Layers.Insert(SelectedLayerIdx + 1, copy);
            SelectedLayerIdx = SelectedLayerIdx + 1;
            EditorUtility.SetDirty(_config);
        }

        EditorGUILayout.EndHorizontal();

        EditorGUILayout.EndVertical();
    }




    private void DrawEntryThumbnails(FloraDecoratorLayer layer, FloraTab parent)
    {
        // Mini block header for entries section
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Flora Entries", EditorStyles.miniBoldLabel);
        GUILayout.FlexibleSpace();

        int totalEntries = layer.EntryIDs?.Count ?? 0;
        if (totalEntries > 0)
        {
            Rect badgeRect = EditorGUILayout.GetControlRect(GUILayout.Width(28), GUILayout.Height(14));
            DrawBadge(badgeRect, $"{totalEntries}", UIColors.Cyan * 0.7f);
        }

        GUI.backgroundColor = FloraGreen;
        if (GUILayout.Button("+ Add ▾", EditorStyles.miniButton, GUILayout.Width(55), GUILayout.Height(16)))
            ShowUnifiedAddMenu(layer, parent);
        GUI.backgroundColor = Color.white;

        EditorGUILayout.EndHorizontal();

        if (parent.Library == null)
        {
            EditorGUILayout.HelpBox("Build a library first (Populator tab).", MessageType.Warning);
            return;
        }

        // Count stale entries (referenced but no longer registered)
        int staleCount = 0;
        int validCount = 0;
        foreach (var id in layer.EntryIDs)
        {
            var e = parent.Library.FindByID(id);
            if (IsSpawnable(e)) validCount++;
            else staleCount++;
        }

        // Thumbnail grid (responsive - wraps based on available width)
        if (layer.EntryIDs.Count > 0)
        {
            const float cellSize = 58f;
            const float cellPad = 2f;
            const float maxGridHeight = 180f;

            // Calculate columns from available width
            float availWidth = EditorGUIUtility.currentViewWidth - 40f;
            int columns = Mathf.Max(1, Mathf.FloorToInt(availWidth / (cellSize + cellPad)));
            int rows = Mathf.CeilToInt((float)layer.EntryIDs.Count / columns);
            float gridHeight = Mathf.Min(rows * (cellSize + cellPad + 6) + 4, maxGridHeight);

            _thumbnailScroll = EditorGUILayout.BeginScrollView(_thumbnailScroll,
                EditorStyles.helpBox, GUILayout.Height(gridHeight));

            int idx = 0;
            for (int row = 0; row < rows && idx < layer.EntryIDs.Count; row++)
            {
                EditorGUILayout.BeginHorizontal();
                for (int col = 0; col < columns && idx < layer.EntryIDs.Count; col++, idx++)
                {
                    var entry = parent.Library.FindByID(layer.EntryIDs[idx]);
                    bool isStale = !IsSpawnable(entry);
                    Texture2D thumb = GetEntryPreview(entry) ?? Texture2D.grayTexture;

                    // Stale entries are dimmed/red
                    if (isStale) GUI.color = new Color(1f, 0.4f, 0.4f, 0.7f);

                    string tooltip = isStale
                        ? $"{layer.EntryIDs[idx]} - NOT REGISTERED (will be skipped)"
                        : $"{entry.EntryID} [{entry.Category}] Proto#{entry.PrototypeIndex}";

                    Rect cellRect = GUILayoutUtility.GetRect(cellSize, cellSize + 6,
                        GUILayout.Width(cellSize), GUILayout.Height(cellSize + 6));

                    // Cell background
                    EditorGUI.DrawRect(cellRect, new Color(0.15f, 0.15f, 0.15f));

                    // Thumbnail image
                    Rect imgRect = new Rect(cellRect.x + 1, cellRect.y + 1,
                        cellSize - 2, cellSize - 2);
                    GUI.DrawTexture(imgRect, thumb, ScaleMode.ScaleToFit);

                    // Click handler
                    if (Event.current.type == EventType.MouseDown
                        && cellRect.Contains(Event.current.mousePosition)
                        && Event.current.button == 1)
                    {
                        int removeIdx = idx;
                        var menu = new GenericMenu();
                        menu.AddItem(new GUIContent("Remove Entry"), false, () =>
                        {
                            Undo.RecordObject(_config, "Remove Entry");
                            layer.EntryIDs.RemoveAt(removeIdx);
                            EditorUtility.SetDirty(_config);
                        });
                        if (isStale)
                        {
                            menu.AddSeparator("");
                            menu.AddDisabledItem(new GUIContent("⚠ Not registered - will not spawn"));
                        }
                        menu.ShowAsContext();
                        Event.current.Use();
                    }

                    // Category badge (bottom-right corner)
                    if (!isStale)
                    {
                        string catLabel = entry.Category == FloraCategory.Tree ? "T" : "D";
                        Color catColor = entry.Category == FloraCategory.Tree
                            ? new Color(0.5f, 0.75f, 0.5f) : new Color(0.6f, 0.8f, 0.4f);
                        DrawBadge(new Rect(cellRect.xMax - 14, cellRect.yMax - 14, 13, 13),
                            catLabel, catColor);
                    }

                    // Tooltip
                    EditorGUI.LabelField(cellRect, new GUIContent("", tooltip));
                    GUI.color = Color.white;
                }
                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.EndScrollView();
        }

        // Stale entry warning with auto-cleanup
        if (staleCount > 0)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.HelpBox(
                $"{staleCount} stale entries (no longer registered). ",
                MessageType.Warning);

            if (GUILayout.Button("Prune", EditorStyles.miniButton, GUILayout.Width(50), GUILayout.Height(32)))
            {
                Undo.RecordObject(_config, "Prune Stale Entries");
                layer.EntryIDs.RemoveAll(id => !IsSpawnable(parent.Library.FindByID(id)));
                EditorUtility.SetDirty(_config);
                LogAdd($"Pruned {staleCount} stale entries from '{layer.Name}'");
            }
            EditorGUILayout.EndHorizontal();
        }

        // Footer: just the count summary (Add button is now in the header)
        GUI.color = new Color(1, 1, 1, 0.5f);
        EditorGUILayout.LabelField(
            $"{validCount} registered" + (staleCount > 0 ? $"  ·  {staleCount} stale" : ""),
            EditorStyles.centeredGreyMiniLabel);
        GUI.color = Color.white;
    }

    /// <summary>
    /// Unified dropdown for adding entries to a layer - replaces the old
    /// Browse / Quick Add / Biome / Clear All button row.
    /// </summary>
    private void ShowUnifiedAddMenu(FloraDecoratorLayer layer, FloraTab parent)
    {
        var menu = new GenericMenu();

        // Browse library window
        menu.AddItem(new GUIContent("Browse Library..."), false, () =>
        {
            FloraEntryBrowser.Open(parent.Library, _config, layer, () =>
            {
                _previewCache.Clear();
                Repaint(parent);
            });
        });

        menu.AddSeparator("");

        // Quick add by category (inline items)
        if (parent.Library != null)
        {
            int detailCount = 0, treeCount = 0;
            foreach (var entry in parent.Library.Entries)
            {
                if (!IsSpawnable(entry) || layer.EntryIDs.Contains(entry.EntryID)) continue;
                string catLabel = entry.Category == FloraCategory.Detail ? "Detail" : "Tree";
                if (entry.Category == FloraCategory.Detail) detailCount++;
                else treeCount++;

                string id = entry.EntryID;
                menu.AddItem(new GUIContent($"Quick Add/{catLabel}/{entry.EntryID}"), false, () =>
                {
                    Undo.RecordObject(_config, "Add Flora Entry");
                    layer.EntryIDs.Add(id);
                    EditorUtility.SetDirty(_config);
                });
            }
            if (detailCount == 0 && treeCount == 0)
                menu.AddDisabledItem(new GUIContent("Quick Add/No entries available"));
        }

        // Add by biome
        foreach (FloraBiome biome in Enum.GetValues(typeof(FloraBiome)))
        {
            if (biome == FloraBiome.None) continue;
            FloraBiome b = biome;

            int available = parent.Library != null
                ? parent.Library.Entries.Count(e =>
                    IsSpawnable(e) && e.MatchesBiome(b) && !layer.EntryIDs.Contains(e.EntryID))
                : 0;

            if (available == 0)
            {
                menu.AddDisabledItem(new GUIContent($"Add Biome/{biome} (0)"));
                continue;
            }

            menu.AddItem(new GUIContent($"Add Biome/{biome} ({available})"), false, () =>
            {
                Undo.RecordObject(_config, "Add Biome Entries");
                int added = 0;
                foreach (var entry in parent.Library.Entries)
                {
                    if (!IsSpawnable(entry) || !entry.MatchesBiome(b)) continue;
                    if (layer.EntryIDs.Contains(entry.EntryID)) continue;
                    layer.EntryIDs.Add(entry.EntryID);
                    added++;
                }
                EditorUtility.SetDirty(_config);
                LogAdd($"Added {added} entries for biome {b}");
            });
        }

        // Clear all
        if (layer.EntryIDs.Count > 0)
        {
            menu.AddSeparator("");
            menu.AddItem(new GUIContent("Clear All Entries"), false, () =>
            {
                Undo.RecordObject(_config, "Clear Layer Entries");
                layer.EntryIDs.Clear();
                EditorUtility.SetDirty(_config);
            });
        }

        menu.ShowAsContext();
    }


    /// <summary>
    /// Gets the best available preview for a registered entry.
    /// Delegates to FloraSharedUtils for unified thumbnail logic.
    /// </summary>
    private Texture2D GetEntryPreview(FloraLibraryEntry entry)
    {
        return FloraSharedUtils.GetEntryPreview(entry, _previewCache);
    }

    /// <summary>
    /// Returns true if this entry is fully registered with the terrain and ready to spawn.
    /// Delegates to FloraSharedUtils - single source of truth.
    /// </summary>
    private static bool IsSpawnable(FloraLibraryEntry entry)
    {
        return FloraSharedUtils.IsSpawnable(entry);
    }

    private void ShowEntryPickerMenu(FloraDecoratorLayer layer, MBFloraLibrary library)
    {
        var menu = new GenericMenu();
        int detailCount = 0, treeCount = 0;

        foreach (var entry in library.Entries)
        {
            if (entry == null || string.IsNullOrEmpty(entry.EntryID)) continue;
            if (!IsSpawnable(entry)) continue;
            if (layer.EntryIDs.Contains(entry.EntryID)) continue;

            string catLabel = entry.Category == FloraCategory.Detail ? "Detail" : "Tree";
            string path = $"{catLabel}/{entry.EntryID}";
            string id = entry.EntryID;

            if (entry.Category == FloraCategory.Detail) detailCount++;
            else treeCount++;

            menu.AddItem(new GUIContent(path), false, () =>
            {
                Undo.RecordObject(_config, "Add Flora Entry");
                layer.EntryIDs.Add(id);
                EditorUtility.SetDirty(_config);
            });
        }

        if (menu.GetItemCount() == 0)
            menu.AddDisabledItem(new GUIContent("No registered entries available (sync prototypes first)"));
        else
            menu.AddDisabledItem(new GUIContent($"- {detailCount} details, {treeCount} trees available -"));

        menu.ShowAsContext();
    }

    private void ShowBiomePickerMenu(FloraDecoratorLayer layer, FloraTab parent)
    {
        if (parent.Library == null) return;
        var menu = new GenericMenu();

        foreach (FloraBiome biome in Enum.GetValues(typeof(FloraBiome)))
        {
            if (biome == FloraBiome.None) continue;
            FloraBiome b = biome;

            // Preview count
            int available = parent.Library.Entries.Count(e =>
                IsSpawnable(e) && e.MatchesBiome(b) && !layer.EntryIDs.Contains(e.EntryID));

            if (available == 0)
            {
                menu.AddDisabledItem(new GUIContent($"{biome} (0 registered)"));
                continue;
            }

            menu.AddItem(new GUIContent($"{biome} ({available})"), false, () =>
            {
                Undo.RecordObject(_config, "Add Biome Entries");
                int added = 0;
                foreach (var entry in parent.Library.Entries)
                {
                    if (!IsSpawnable(entry)) continue;
                    if (!entry.MatchesBiome(b)) continue;
                    if (layer.EntryIDs.Contains(entry.EntryID)) continue;
                    layer.EntryIDs.Add(entry.EntryID);
                    added++;
                }
                EditorUtility.SetDirty(_config);
                LogAdd($"Added {added} registered entries for biome {b}");
            });
        }
        menu.ShowAsContext();
    }


    private void DrawSplatmapMasks(FloraDecoratorLayer layer, FloraTab parent)
    {
        EditorGUILayout.LabelField(
            new GUIContent("Splatmap Masks", "Only spawn where terrain layers have sufficient strength"),
            EditorStyles.boldLabel);

        _selectedMaskIdx = Mathf.Clamp(_selectedMaskIdx, 0, Mathf.Max(0, layer.SplatmapMasks.Count - 1));

        // Thumbnail row
        if (layer.SplatmapMasks.Count > 0)
        {
            EditorGUILayout.BeginScrollView(Vector2.zero, EditorStyles.textArea,
                GUILayout.MaxHeight(54));
            EditorGUILayout.BeginHorizontal();

            for (int i = 0; i < layer.SplatmapMasks.Count; i++)
            {
                var mask = layer.SplatmapMasks[i];
                Texture2D tex = GetTerrainLayerTex(parent, mask.LayerID);
                GUIStyle style = (_selectedMaskIdx == i)
                    ? new GUIStyle("SelectionRect") { imagePosition = ImagePosition.ImageAbove }
                    : new GUIStyle("box") { imagePosition = ImagePosition.ImageAbove };

                if (GUILayout.Button(tex ?? Texture2D.whiteTexture, style,
                        GUILayout.Width(44), GUILayout.Height(44)))
                    _selectedMaskIdx = i;
            }

            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndScrollView();
        }

        // Add / Remove buttons
        EditorGUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();

        if (GUILayout.Button("Add", EditorStyles.miniButtonLeft, GUILayout.Width(50)))
            ShowSplatmapMaskMenu(layer, parent);

        EditorGUI.BeginDisabledGroup(layer.SplatmapMasks.Count == 0);
        if (GUILayout.Button("Remove", EditorStyles.miniButtonRight, GUILayout.Width(60)))
        {
            layer.SplatmapMasks.RemoveAt(_selectedMaskIdx);
            _selectedMaskIdx = Mathf.Clamp(_selectedMaskIdx, 0, Mathf.Max(0, layer.SplatmapMasks.Count - 1));
        }
        EditorGUI.EndDisabledGroup();
        EditorGUILayout.EndHorizontal();

        // Selected mask settings
        if (_selectedMaskIdx >= 0 && _selectedMaskIdx < layer.SplatmapMasks.Count)
        {
            var selected = layer.SplatmapMasks[_selectedMaskIdx];
            EditorGUILayout.LabelField($"{selected.LayerName} Settings", EditorStyles.miniLabel);
            selected.Threshold = EditorGUILayout.Slider(
                new GUIContent("Minimum Strength",
                    "The minimum splatmap strength the material must have before spawning"),
                selected.Threshold, 0f, 1f);
        }
    }

    private void ShowSplatmapMaskMenu(FloraDecoratorLayer layer, FloraTab parent)
    {
        if (parent.SceneTerrain == null) return;
        var terrainLayers = parent.SceneTerrain.terrainData.terrainLayers;

        var menu = new GenericMenu();
        for (int i = 0; i < terrainLayers.Length; i++)
        {
            if (terrainLayers[i] == null) continue;
            if (layer.SplatmapMasks.Any(m => m.LayerID == i)) continue;

            int idx = i;
            menu.AddItem(new GUIContent(terrainLayers[i].name), false, () =>
            {
                Undo.RecordObject(_config, "Add Splatmap Mask");
                layer.SplatmapMasks.Add(new DecoratorSplatmapMask
                {
                    LayerID = idx,
                    LayerName = terrainLayers[idx].name,
                    Threshold = 0f
                });
                _selectedMaskIdx = layer.SplatmapMasks.Count - 1;
                EditorUtility.SetDirty(_config);
            });
        }
        if (menu.GetItemCount() == 0)
            menu.AddDisabledItem(new GUIContent("All layers already added"));
        menu.ShowAsContext();
    }

    private Texture2D GetTerrainLayerTex(FloraTab parent, int layerID)
    {
        if (parent.SceneTerrain == null) return null;
        var layers = parent.SceneTerrain.terrainData.terrainLayers;
        if (layerID < 0 || layerID >= layers.Length || layers[layerID] == null) return null;
        return layers[layerID].diffuseTexture;
    }


    private void DrawSpawnActions(FloraTab parent)
    {
        bool canSpawn = _config != null && parent.Library != null && parent.SceneTerrain != null;

        if (_config != null && parent.Library != null)
        {
            int enabledLayers = _config.Layers.Count(l => l.Enabled);
            int registeredEntries = _config.Layers
                .Where(l => l.Enabled)
                .SelectMany(l => l.EntryIDs)
                .Distinct()
                .Count(id => IsSpawnable(parent.Library.FindByID(id)));
            int totalInstances = _config.Layers.Sum(l => l.LastInstanceCount);

            GUI.color = new Color(1, 1, 1, 0.5f);
            EditorGUILayout.LabelField(
                $"{enabledLayers} layers  ·  {registeredEntries} entries  ·  {totalInstances:##,#} instances",
                EditorStyles.centeredGreyMiniLabel);
            GUI.color = Color.white;
        }

        EditorGUILayout.BeginHorizontal();

        Color orig = GUI.backgroundColor;

        // Spawn All (primary action)
        GUI.backgroundColor = UIColors.Green;
        EditorGUI.BeginDisabledGroup(!canSpawn);
        if (GUILayout.Button("▶ Spawn All", GUILayout.Height(26)))
            SpawnAll(parent);
        EditorGUI.EndDisabledGroup();
        GUI.backgroundColor = orig;

        // Apply to Decorator (RGL integration)
        RGLDecoratorIntegrationUI.DrawApplyToDecoratorButton(parent, _config);

        // Clear (dropdown)
        GUI.backgroundColor = UIColors.Red * 0.7f;
        EditorGUI.BeginDisabledGroup(parent.SceneTerrain == null);
        if (GUILayout.Button("Clear ▾", EditorStyles.miniButton, GUILayout.Width(60), GUILayout.Height(26)))
        {
            ShowClearMenu(parent);
        }
        EditorGUI.EndDisabledGroup();
        GUI.backgroundColor = orig;

        EditorGUILayout.EndHorizontal();
    }

    private void ShowClearMenu(FloraTab parent)
    {
        var menu = new GenericMenu();
        menu.AddItem(new GUIContent("Clear Details (keep trees)"), false, () =>
        {
            if (EditorUtility.DisplayDialog("Clear Detail Maps",
                    "Clear all detail density maps? Trees are kept.", "Clear", "Cancel"))
                ClearDetailMaps(parent);
        });
        menu.AddItem(new GUIContent("Clear Trees (keep details)"), false, () =>
        {
            if (EditorUtility.DisplayDialog("Clear Trees",
                    "Remove all tree instances?", "Clear", "Cancel"))
                ClearTreeInstances(parent);
        });
        menu.AddSeparator("");
        menu.AddItem(new GUIContent("Clear Everything"), false, () =>
        {
            if (EditorUtility.DisplayDialog("Clear All",
                    "Remove ALL details AND trees from this terrain?", "Clear All", "Cancel"))
            {
                ClearDetailMaps(parent);
                ClearTreeInstances(parent);
            }
        });
        menu.ShowAsContext();
    }





    //  Spawn Logic

    private void SpawnAll(FloraTab parent)
    {
        if (_config == null || parent.Library == null || parent.SceneTerrain == null) return;

        var terrain = parent.SceneTerrain;
        int totalSpawned = 0;
        var sw = new Stopwatch();
        sw.Start();

        try
        {
            Undo.RecordObject(terrain.terrainData, "Flora Decorator Spawn All");

            for (int i = 0; i < _config.Layers.Count; i++)
            {
                var layer = _config.Layers[i];
                if (!layer.Enabled) continue;

                if (EditorUtility.DisplayCancelableProgressBar("Flora Decorator",
                        $"Spawning layer: {layer.Name} ({i + 1}/{_config.Layers.Count})",
                        (float)i / _config.Layers.Count))
                    break;

                var layerSw = new Stopwatch();
                layerSw.Start();
                int count = SpawnLayerInternal(parent, layer, terrain);
                layerSw.Stop();
                layer.LastSpawnTimeMs = (float)layerSw.Elapsed.TotalMilliseconds;
                totalSpawned += count;
            }

            terrain.Flush();
            EditorUtility.SetDirty(terrain.terrainData);
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }

        sw.Stop();
        ClearAlphamapCache();
        
        string msg = $"Spawned {totalSpawned:##,#} instances across " +
                     $"{_config.Layers.Count(l => l.Enabled)} layers in {sw.Elapsed.TotalSeconds:F2}s";
        Debug.Log($"[FloraDecorator] {msg}");
        LogAdd(msg);
    }

    private void SpawnSingleLayer(FloraTab parent, FloraDecoratorLayer layer)
    {
        if (parent.SceneTerrain == null || parent.Library == null) return;

        var terrain = parent.SceneTerrain;
        var sw = new Stopwatch();
        sw.Start();

        try
        {
            Undo.RecordObject(terrain.terrainData, $"Flora Decorator: {layer.Name}");
            int count = SpawnLayerInternal(parent, layer, terrain);
            terrain.Flush();
            EditorUtility.SetDirty(terrain.terrainData);
            sw.Stop();
            layer.LastSpawnTimeMs = (float)sw.Elapsed.TotalMilliseconds;
            string msg = $"Layer '{layer.Name}': {count:##,#} instances in {sw.Elapsed.TotalSeconds:F2}s";
            Debug.Log($"[FloraDecorator] {msg}");
            LogAdd(msg);
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }
    }

    private int SpawnLayerInternal(FloraTab parent, FloraDecoratorLayer layer, Terrain terrain)
    {
        if (layer.LayerType == FloraLayerType.RGL_Automatic)
        {
            if (!string.IsNullOrEmpty(_config.WarbandTerrainCode))
            {
                 // Prepare Mask
                 float[,] mask = null;
                 float maskThreshold = 0f;
                 
                 // Splatmap Mask (Combine if exists)
                 if (layer.SplatmapMasks.Count > 0)
                 {
                     BuildAlphamapCache(terrain.terrainData);
                     int w = terrain.terrainData.detailWidth;
                     int h = terrain.terrainData.detailHeight;
                     
                     // If no warband mask, init ones
                     if (mask == null)
                     {
                         mask = new float[w, h];
                         for(int x=0; x<w; x++)
                             for(int y=0; y<h; y++) mask[x,y] = 1f;
                         maskThreshold = 0.5f; // Logic: Mask value must be >= threshold. If we set map to 1, it passes.
                     }
                     
                     // Multiply by Splatmap validity
                     // This is expensive to sample per pixel if we do it completely manually.
                     // But we have PassesSplatmapCheck.
                     // Note: GenerateDetails assumes a single float mask.
                     // We can bake the splatmap check into the mask.
                     
                     // Optimization: Use Alphamaps directly.
                     var td = terrain.terrainData;
                     float[,,] alphamaps = td.GetAlphamaps(0, 0, td.alphamapWidth, td.alphamapHeight);
                     
                     // Resize alphamaps to detail resolution? Or sample.
                     // For simplicity, let's iterate detail grid and sample.
                     for (int y = 0; y < h; y++)
                     {
                         for (int x = 0; x < w; x++)
                         {
                             float nx = (float)y / w;
                             float ny = (float)x / h;
                             
                             // Check splatmaps
                             bool pass = PassesSplatmapCheck(layer, td, nx, ny);
                             if (!pass)
                             {
                                 mask[x, y] = 0f; // Force fail
                             }
                         }
                     }
                 }
                 if (mask != null)
                 {
                     // Export grass mask for Decorator consumption
                     var sceneData = Object.FindFirstObjectByType<MBSceneDataManager>(); // or however you access module/scene names
                     if (sceneData != null)
                     {
                         string relPath = FloraSharedUtils.ExportGrassMask(mask, sceneData.Module.ID, sceneData.SceneName);
                         Debug.Log($"[RGL] Exported grass mask: {relPath}");
                     }
                 }
                 // Spawns EVERYTHING for the biome.
                 MBEditor.Tools.Flora.Logic.WarbandFloraGenerator.GenerateDetails(terrain, parent.Library, _config.WarbandTerrainCode, null, mask, maskThreshold);
            }
            // Count difficult to track here without changes to generator return type, assuming success
            layer.LastInstanceCount = 1; 
            return 1;
        }

        if (layer.EntryIDs.Count == 0) return 0;

        // Resolve entries - only registered entries can spawn
        var entries = new List<FloraLibraryEntry>();
        foreach (var id in layer.EntryIDs)
        {
            var entry = parent.Library.FindByID(id);
            if (IsSpawnable(entry))
                entries.Add(entry);
        }
        if (entries.Count == 0) return 0;

        var detailEntries = entries.Where(e => e.Category == FloraCategory.Detail).ToList();
        var treeEntries = entries.Where(e => e.Category == FloraCategory.Tree).ToList();

        int count = 0;


        if (layer.CollisionCheck && _config.UseCollisionMask)
            BuildCollisionGrid(layer, terrain);

        float[,] warbandMap = null;

        // Each detail prototype needs its own density map, but we evaluate
        // terrain filters once per texel and randomly assign to one entry.
        if (detailEntries.Count > 0)
            count += SpawnDetailEntries(layer, detailEntries, terrain, warbandMap);

        // Mirrors VegetationSpawner pattern: one set of spawn points, GetProbableTree
        if (treeEntries.Count > 0)
            count += SpawnTreeEntries(layer, treeEntries, terrain, warbandMap);

        ClearCollisionGrid();

        layer.LastInstanceCount = count;
        return count;
    }

    //  Collision Grid Cache
    //  Pre-bakes a 2D grid of physics overlap checks so per-texel
    //  collision testing is O(1). Matches VegetationSpawner's approach.

    private bool[,] _collisionGrid;
    private int _collisionGridW, _collisionGridH;
    private float _collisionCellWorldX, _collisionCellWorldZ;
    private Vector3 _collisionOrigin;

    /// <summary>
    /// Builds a 2D boolean grid where true = blocked by a collider.
    /// Uses Physics.OverlapBox per cell with the configured layer mask and clearance padding.
    /// </summary>
    private void BuildCollisionGrid(FloraDecoratorLayer layer, Terrain terrain)
    {
        if (!_config.UseCollisionMask) return;

        var td = terrain.terrainData;
        int cellSize = _config.CollisionCellSize;
        int subdivisions = _config.CollisionSubdivisions;

        _collisionGridW = Mathf.CeilToInt(td.size.x / cellSize) * subdivisions;
        _collisionGridH = Mathf.CeilToInt(td.size.z / cellSize) * subdivisions;
        _collisionGrid = new bool[_collisionGridW, _collisionGridH];

        _collisionCellWorldX = td.size.x / _collisionGridW;
        _collisionCellWorldZ = td.size.z / _collisionGridH;
        _collisionOrigin = terrain.GetPosition();

        float halfX = _collisionCellWorldX * 0.5f;
        float halfZ = _collisionCellWorldZ * 0.5f;
        
        // Add layer's clearance to the overlap box half extents
        float pad = layer.CollisionClearance;
        Vector3 halfExtents = new Vector3(halfX + pad, td.size.y * 0.5f, halfZ + pad);

        int layerMask = _config.CollisionLayer;

        for (int gx = 0; gx < _collisionGridW; gx++)
        {
            for (int gz = 0; gz < _collisionGridH; gz++)
            {
                float wx = _collisionOrigin.x + gx * _collisionCellWorldX + halfX;
                float wz = _collisionOrigin.z + gz * _collisionCellWorldZ + halfZ;
                float wy = _collisionOrigin.y + td.size.y * 0.5f;

                var hits = Physics.OverlapBox(
                    new Vector3(wx, wy, wz), halfExtents,
                    Quaternion.identity, layerMask, QueryTriggerInteraction.Ignore);

                // Ignore the terrain collider itself
                _collisionGrid[gx, gz] = hits.Any(c => !(c is TerrainCollider));
            }
        }
    }

    /// <summary>
    /// Returns true if the normalized terrain position (0-1, 0-1) is blocked by a collider.
    /// </summary>
    private bool IsCollisionBlocked(float nx, float ny)
    {
        if (_collisionGrid == null) return false;

        int gx = Mathf.Clamp(Mathf.FloorToInt(nx * _collisionGridW), 0, _collisionGridW - 1);
        int gz = Mathf.Clamp(Mathf.FloorToInt(ny * _collisionGridH), 0, _collisionGridH - 1);
        return _collisionGrid[gx, gz];
    }

    private void ClearCollisionGrid()
    {
        _collisionGrid = null;
    }

    //
    // VegetationSpawner processes each GrassPrefab independently because
    // each has its own seed (so they get different random rolls). We do the
    // same: iterate the grid once, and for each valid texel randomly pick
    // ONE detail entry to receive the instance. This prevents all entries
    // from stacking on the same texel.

    private int SpawnDetailEntries(FloraDecoratorLayer layer, List<FloraLibraryEntry> detailEntries, Terrain terrain, float[,] warbandMap)
    {
        var td = terrain.terrainData;

        if (layer.SplatmapMasks.Count > 0)
            BuildAlphamapCache(td);
        
        int w = td.detailWidth;
        int h = td.detailHeight;
        int entryCount = detailEntries.Count;

        // One map per detail prototype
        var maps = new Dictionary<int, int[,]>();
        foreach (var entry in detailEntries)
            maps[entry.PrototypeIndex] = new int[w, h];

        int globalSeed = _config.Seed;
        float densityMul = _config.GlobalDensityMultiplier;
        int instanceCount = 0;

        int cellCount = w * h;
        int progressInterval = Mathf.Max(1, cellCount / 20);
        
        // Warband map assumption: matches detail resolution (w, h)
        bool useWarband = warbandMap != null && warbandMap.GetLength(0) == w && warbandMap.GetLength(1) == h;

        for (int x = 0; x < w; x++)
        {
            for (int y = 0; y < h; y++)
            {
                int counter = x * h + y;
                if (counter % progressInterval == 0)
                {
                    if (EditorUtility.DisplayCancelableProgressBar("Flora Decorator",
                            $"Spawning details ({counter}/{cellCount})",
                            (float)counter / cellCount))
                        goto DetailsDone;
                }
                
                // Deterministic seed per texel (matches VegSpawner: x * y + item.seed)
                Random.InitState(globalSeed + layer.Seed + x * h + y);

                // Probability
                if (Random.value * 100f > layer.Probability) continue;

                // Normalized position
                float nx = (float)y / w;
                float ny = (float)x / h;

                // Height
                float height = td.GetInterpolatedHeight(nx, ny);
                float worldHeight = height + terrain.transform.position.y;

                if (worldHeight < layer.HeightRange.x || worldHeight > layer.HeightRange.y)
                    continue;

                if (layer.WaterFilter == FloraWaterFilter.RejectUnderwater && worldHeight <= _config.WaterHeight)
                    continue;
                if (layer.WaterFilter == FloraWaterFilter.OnlyUnderwater && worldHeight > _config.WaterHeight)
                    continue;

                // Slope
                if (layer.SlopeRange.x > 0 || layer.SlopeRange.y < 90f)
                {
                    float slope = td.GetSteepness(nx, ny);
                    if (slope < layer.SlopeRange.x || slope > layer.SlopeRange.y)
                        continue;
                }

                // Curvature
                if (layer.CurvatureRange.x > 0 || layer.CurvatureRange.y < 1f)
                {
                    float curv = SampleCurvature(td, nx, ny);
                    if (curv < layer.CurvatureRange.x || curv > layer.CurvatureRange.y)
                        continue;
                }

                // Noise
                float noise = Mathf.PerlinNoise(
                    nx * td.size.x * layer.NoiseScale + layer.Seed * 0.1f,
                    ny * td.size.z * layer.NoiseScale);
                if (noise < layer.NoiseThreshold)
                    continue;

                // Splatmap
                if (layer.SplatmapMasks.Count > 0 && !PassesSplatmapCheck(layer, td, nx, ny))
                    continue;

                // Collision
                if (layer.CollisionCheck && IsCollisionBlocked(nx, ny))
                    continue;

                FloraLibraryEntry picked = PickOneEntry(detailEntries);
                if (picked == null) continue;

                int value = Mathf.Max(1, Mathf.RoundToInt(densityMul * picked.DetailDensity));
                #if UNITY_2022_2_OR_NEWER
                if (td.detailScatterMode == DetailScatterMode.CoverageMode)
                    value = Mathf.Clamp(Mathf.RoundToInt(255 * densityMul * picked.DetailDensity), 1, 255);
                #endif

                maps[picked.PrototypeIndex][x, y] = value;
                instanceCount++;
            }
        }

        DetailsDone:

        // Commit all maps
        foreach (var kvp in maps)
            td.SetDetailLayer(0, 0, kvp.Key, kvp.Value);

        return instanceCount;
    }

    //
    // Generates ONE set of Poisson disc points for the layer, then
    // picks ONE tree entry per valid point - exactly like VegetationSpawner's
    // GetProbableTree pattern. No more 4 trees stacked at the same spot.

    private int SpawnTreeEntries(FloraDecoratorLayer layer, List<FloraLibraryEntry> treeEntries, Terrain terrain, float[,] warbandMap)
    {
        var td = terrain.terrainData;
        
         if (layer.SplatmapMasks.Count > 0)
             BuildAlphamapCache(td);

        int combinedSeed = _config.Seed + layer.Seed;
        int instanceCount = 0;

        // ONE set of Poisson disc points for the whole layer
        List<Vector3> spawnPoints = PoissonDiscSample(td.bounds, td.size, layer.TreeDistance, combinedSeed);

        // Collect existing tree instances, removing all prototypes owned by this layer
        var treeInstances = new List<TreeInstance>(td.treeInstances);
        var ownedProtoIndices = new HashSet<int>(treeEntries.Select(e => e.PrototypeIndex));
        treeInstances.RemoveAll(t => ownedProtoIndices.Contains(t.prototypeIndex));
        
        int totalPoints = spawnPoints.Count;
        int progressInterval = Mathf.Max(1, totalPoints / 20);

        for (int i = 0; i < totalPoints; i++)
        {
            if (i % progressInterval == 0)
            {
                if (EditorUtility.DisplayCancelableProgressBar("Flora Decorator",
                        $"Spawning trees ({i}/{totalPoints})",
                        (float)i / totalPoints))
                    break;
            }

            Vector3 pos = spawnPoints[i];

            float nx = (pos.x - terrain.GetPosition().x) / td.size.x;
            float ny = (pos.z - terrain.GetPosition().z) / td.size.z;

            if (nx < 0 || nx > 1 || ny < 0 || ny > 1) continue;
            
            // Deterministic per-point seed
            Random.InitState(combinedSeed + (int)(pos.x * 97) + (int)(pos.z * 131));

            // Global probability
            if (Random.value * 100f > layer.Probability) continue;

            // Height
            float height = td.GetInterpolatedHeight(nx, ny);
            float worldHeight = height + terrain.transform.position.y;
            if (worldHeight < layer.HeightRange.x || worldHeight > layer.HeightRange.y) continue;
            if (layer.WaterFilter == FloraWaterFilter.RejectUnderwater && worldHeight <= _config.WaterHeight) continue;
            if (layer.WaterFilter == FloraWaterFilter.OnlyUnderwater && worldHeight > _config.WaterHeight) continue;

            // Slope
            if (layer.SlopeRange.x > 0 || layer.SlopeRange.y < 90f)
            {
                float slope = td.GetSteepness(nx, ny);
                if (slope < layer.SlopeRange.x || slope > layer.SlopeRange.y) continue;
            }

            // Curvature
            if (layer.CurvatureRange.x > 0 || layer.CurvatureRange.y < 1f)
            {
                float curv = SampleCurvature(td, nx, ny);
                if (curv < layer.CurvatureRange.x || curv > layer.CurvatureRange.y) continue;
            }

            // Noise
            float noise = Mathf.PerlinNoise(
                nx * td.size.x * layer.NoiseScale + layer.Seed * 0.1f,
                ny * td.size.z * layer.NoiseScale);
            if (noise < layer.NoiseThreshold) continue;

            // Splatmap
            if (layer.SplatmapMasks.Count > 0 && !PassesSplatmapCheck(layer, td, nx, ny))
                continue;

            // Collision
            if (layer.CollisionCheck && IsCollisionBlocked(nx, ny))
                continue;

            FloraLibraryEntry picked = PickOneEntry(treeEntries);
            if (picked == null) continue;

            float normalizedHeight = height / td.size.y;
            float scale = Random.Range(layer.TreeScaleRange.x, layer.TreeScaleRange.y);
            float sinkNormalized = layer.TreeSinkAmount / (td.size.y + 0.01f);

            var tree = new TreeInstance
            {
                prototypeIndex = picked.PrototypeIndex,
                position = new Vector3(nx, normalizedHeight - sinkNormalized, ny),
                rotation = Random.Range(0f, 359f) * Mathf.Deg2Rad,
                widthScale = scale,
                heightScale = scale,
                color = Color.white,
                lightmapColor = Color.white
            };

            treeInstances.Add(tree);
            instanceCount++;
        }

        td.SetTreeInstances(treeInstances.ToArray(), false);
        return instanceCount;
    }


    //  Entry Picking (weighted random, single selection)
    //  Mirrors VegetationSpawner's GetProbableTree pattern

    private const int PICK_MAX_ATTEMPTS = 4;

    /// <summary>
    /// Randomly picks ONE entry from the list using equal-weight random selection.
    /// If the list has a single entry, returns it directly.
    /// Uses recursive retry (up to <see cref="PICK_MAX_ATTEMPTS"/>) to mirror
    /// VegetationSpawner's GetProbableTree pattern.
    /// </summary>
    private static FloraLibraryEntry PickOneEntry(List<FloraLibraryEntry> entries)
    {
        if (entries.Count == 0) return null;
        if (entries.Count == 1) return entries[0];

        // Simple uniform pick - each entry has equal chance
        // The per-entry DetailDensity/coverage is applied AFTER selection
        // (for details it scales the density value, not the probability)
        return entries[Random.Range(0, entries.Count)];
    }


    //  Poisson Disc Sampling (2D XZ)
    //  Adapted from VegetationSpawner's PoissonDisc.cs

    private const int POISSON_MAX_ATTEMPTS = 10;

    private static List<Vector3> PoissonDiscSample(Bounds bounds, Vector3 terrainSize, float radius, int seed)
    {
        float cellSize = radius / Mathf.Sqrt(2f);
        int xCells = Mathf.CeilToInt(terrainSize.x / cellSize);
        int zCells = Mathf.CeilToInt(terrainSize.z / cellSize);
        int[,] grid = new int[xCells, zCells];

        var samples = new List<Vector2>();
        var points = new List<Vector2>();
        var result = new List<Vector3>();

        Random.InitState(seed);

        Vector2 startPos = new Vector2(Random.value * terrainSize.x, Random.value * terrainSize.z);
        samples.Add(startPos);

        while (samples.Count > 0)
        {
            int i = Random.Range(0, samples.Count);
            Vector2 center = samples[i];
            bool valid = false;

            for (int s = 0; s < POISSON_MAX_ATTEMPTS; s++)
            {
                Random.InitState(seed + s + i);

                float angle = 2f * Mathf.PI * Random.value;
                float dist = Random.Range(radius, radius * 2f);
                Vector2 sample = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * dist;

                if (sample.x < 0 || sample.x >= terrainSize.x || sample.y < 0 || sample.y >= terrainSize.z)
                    continue;

                Vector2Int gp = new Vector2Int((int)(sample.x / cellSize), (int)(sample.y / cellSize));
                bool tooClose = false;

                int xmin = Mathf.Max(gp.x - 2, 0);
                int xmax = Mathf.Min(gp.x + 2, xCells - 1);
                int ymin = Mathf.Max(gp.y - 2, 0);
                int ymax = Mathf.Min(gp.y + 2, zCells - 1);

                for (int cy = ymin; cy <= ymax && !tooClose; cy++)
                    for (int cx = xmin; cx <= xmax && !tooClose; cx++)
                    {
                        int idx = grid[cx, cy] - 1;
                        if (idx >= 0 && (sample - points[idx]).sqrMagnitude < radius * radius)
                            tooClose = true;
                    }

                if (tooClose) continue;

                result.Add(new Vector3(
                    sample.x + bounds.min.x,
                    0f,
                    sample.y + bounds.min.z));

                points.Add(sample);
                samples.Add(sample);

                gp = new Vector2Int((int)(sample.x / cellSize), (int)(sample.y / cellSize));
                grid[gp.x, gp.y] = points.Count;

                valid = true;
                break;
            }

            if (!valid)
                samples.RemoveAt(i);
        }

        return result;
    }


    //  Terrain Sampling Helpers

    /// <summary>
    /// Splatmap mask check using cached alphamap data.
    /// Must call BuildAlphamapCache() before using.
    ///
    /// NOTE on indexing:
    ///   td.GetAlphamaps returns float[y, x, layer] where:
    ///     - first index  = row (maps to terrain Z axis)
    ///     - second index = column (maps to terrain X axis)
    ///     - third index  = terrain layer index (0..n)
    ///
    ///   nx = normalized X position (terrain X axis → column)
    ///   ny = normalized Y position (terrain Z axis → row)
    ///
    ///   So: alphamaps[row, col, layer] = alphamaps[nyIdx, nxIdx, layer]
    /// </summary>
    private bool PassesSplatmapCheck(FloraDecoratorLayer layer, TerrainData td, float nx, float ny)
    {
        if (_cachedAlphamaps == null)
        {
            Debug.LogWarning("[FloraPopulator] Alphamap cache not built - call BuildAlphamapCache() first.");
            return true; // Fail open so we don't silently block everything
        }

        // nx → column (X axis), ny → row (Z axis)
        int col = Mathf.Clamp(Mathf.RoundToInt(nx * (_cachedAlphamapW - 1)), 0, _cachedAlphamapW - 1);
        int row = Mathf.Clamp(Mathf.RoundToInt(ny * (_cachedAlphamapH - 1)), 0, _cachedAlphamapH - 1);

        float spawnChance = 0f;
        foreach (var mask in layer.SplatmapMasks)
        {
            int layerIdx = mask.LayerID;
            if (layerIdx < 0 || layerIdx >= _cachedAlphamapLayers) continue;

            // Direct array lookup - no GPU readback
            float value = _cachedAlphamaps[row, col, layerIdx];

            if (value > mask.Threshold)
                spawnChance += Mathf.Clamp01(value - mask.Threshold);
        }

        return Random.value <= spawnChance;
    }

    private static float SampleCurvature(TerrainData td, float nx, float ny, float radius = 3f)
    {
        float texelSize = (1f / td.heightmapResolution) * radius;

        float posX = td.GetInterpolatedNormal(nx + texelSize, ny).x;
        float negX = td.GetInterpolatedNormal(nx - texelSize, ny).x;
        float x = (posX - negX) + 0.5f;

        float posY = td.GetInterpolatedNormal(nx, ny + texelSize).z;
        float negY = td.GetInterpolatedNormal(nx, ny - texelSize).z;
        float y = (posY - negY) + 0.5f;

        float convexity = (y < 0.5f) ? 2f * x * y : 1f - 2f * (1f - x) * (1f - y);
        return (convexity - (1f - convexity)) * 0.5f + 0.5f;
    }


    //  Clear Operations

    private static void ClearDetailMaps(FloraTab parent)
    {
        if (parent.SceneTerrain == null) return;
        var td = parent.SceneTerrain.terrainData;
        Undo.RecordObject(td, "Clear Detail Maps");

        int res = td.detailWidth;
        int[,] empty = new int[res, res];
        for (int i = 0; i < td.detailPrototypes.Length; i++)
            td.SetDetailLayer(0, 0, i, empty);

        parent.SceneTerrain.Flush();
        EditorUtility.SetDirty(td);
        LogAdd("Cleared all detail maps");
    }

    private static void ClearTreeInstances(FloraTab parent)
    {
        if (parent.SceneTerrain == null) return;
        var td = parent.SceneTerrain.terrainData;
        Undo.RecordObject(td, "Clear Tree Instances");

        td.SetTreeInstances(new TreeInstance[0], false);

        parent.SceneTerrain.Flush();
        EditorUtility.SetDirty(td);
        LogAdd("Cleared all tree instances");
    }

    /// <summary>
    /// Clear only the instances belonging to a specific layer's entries.
    /// Details are zeroed out; trees of matching prototype indices are removed.
    /// </summary>
    private void ClearLayerInstances(FloraTab parent, FloraDecoratorLayer layer)
    {
        if (parent.SceneTerrain == null || parent.Library == null) return;
        var td = parent.SceneTerrain.terrainData;
        Undo.RecordObject(td, $"Clear Layer: {layer.Name}");

        int cleared = 0;
        foreach (var id in layer.EntryIDs)
        {
            var entry = parent.Library.FindByID(id);
            if (!IsSpawnable(entry)) continue;

            if (entry.Category == FloraCategory.Detail)
            {
                int res = td.detailWidth;
                int[,] empty = new int[res, res];
                td.SetDetailLayer(0, 0, entry.PrototypeIndex, empty);
                cleared++;
            }
            else if (entry.Category == FloraCategory.Tree)
            {
                var trees = new List<TreeInstance>(td.treeInstances);
                int before = trees.Count;
                trees.RemoveAll(t => t.prototypeIndex == entry.PrototypeIndex);
                td.SetTreeInstances(trees.ToArray(), false);
                cleared += before - trees.Count;
            }
        }

        parent.SceneTerrain.Flush();
        EditorUtility.SetDirty(td);
        layer.LastInstanceCount = 0;
        layer.LastSpawnTimeMs = 0;
        LogAdd($"Cleared layer '{layer.Name}': {cleared} items removed");
    }


    //  UI Helpers

    /// <summary>
    /// MinMaxSlider with editable float fields - matches VegetationSpawnerEditor.DrawRangeSlider
    /// </summary>
    private static void DrawRangeSlider(string label, ref Vector2 range, float min, float max)
    {
        float lo = range.x;
        float hi = range.y;

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField(label, GUILayout.MaxWidth(EditorGUIUtility.labelWidth));
        lo = EditorGUILayout.FloatField(lo, GUILayout.MaxWidth(50f));
        EditorGUILayout.MinMaxSlider(ref lo, ref hi, min, max);
        hi = EditorGUILayout.FloatField(hi, GUILayout.MaxWidth(50f));
        EditorGUILayout.EndHorizontal();

        range.x = lo;
        range.y = hi;
    }

    /// <summary>
    /// Seed field with randomize button - matches VegetationSpawnerEditor.DrawSeedField
    /// </summary>
    private static void DrawSeedField(string label, ref int seed)
    {
        EditorGUILayout.BeginHorizontal();
        seed = EditorGUILayout.IntField(label, seed,
            GUILayout.MaxWidth(EditorGUIUtility.labelWidth + 60));
        if (GUILayout.Button("Randomize", EditorStyles.miniButton, GUILayout.MaxWidth(80)))
            seed = Random.Range(0, 99999);
        EditorGUILayout.EndHorizontal();
    }

    private void Swap(int a, int b)
    {
        (_config.Layers[a], _config.Layers[b]) = (_config.Layers[b], _config.Layers[a]);
        EditorUtility.SetDirty(_config);
    }

    /// <summary>
    /// Checks if the layer has any registered (spawnable) entries of the given category.
    /// </summary>
    private bool HasRegisteredEntriesOfCategory(FloraDecoratorLayer layer, FloraTab parent, FloraCategory category)
    {
        if (parent.Library == null) return false;
        return layer.EntryIDs.Any(id =>
        {
            var e = parent.Library.FindByID(id);
            return IsSpawnable(e) && e.Category == category;
        });
    }

    private void RecalcTerrainMinMax(FloraTab parent)
    {
        if (parent.SceneTerrain == null) return;
        var t = parent.SceneTerrain;
        _terrainMinMaxHeight = new Vector2(
            t.GetPosition().y + t.terrainData.bounds.min.y,
            t.GetPosition().y + t.terrainData.bounds.size.y);
    }

    /// <summary>
    /// Request the parent editor window to repaint (e.g. after browser adds entries).
    /// </summary>
    private static void Repaint(FloraTab parent)
    {
        // FloraTab lives inside an EditorWindow - find and repaint it
        foreach (var w in Resources.FindObjectsOfTypeAll<EditorWindow>())
        {
            if (w.GetType().Name.Contains("Flora") || w.GetType().Name.Contains("MBEditor"))
            {
                w.Repaint();
                break;
            }
        }
    }
}
