using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using System.IO;
using Object = UnityEngine.Object;

/// <summary>
/// Simplified Tint Tab with unified UI.
/// Features:
/// - Single-view layout (no sub-tabs)
/// - Auto-sync with generator resolution
/// - Real-time GPU preview while painting
/// - Auto-bake to PNG on exit paint mode
/// </summary>
public class TintTab : SceneDataManagerTabBase
{
    public override string TabName => "Tint";
    public override int Order => 8;

    #region State

    private TerrainTintData _data;
    private Terrain _terrain;

    // Painting state
    private bool _isPainting;
    private bool _isMouseDown;
    private float _lastPaintTime;

    // Resolution from generator
    private int _generatorResX;
    private int _generatorResY;
    private bool _hasGeneratorRes;

    // GPU Compositing
    private ComputeShader _compositeShader;
    private RenderTexture _livePreviewRT;
    private int _kernelAll = -1;
    private bool _previewDirty = true;

    // UI state
    private bool _showLayers = true;
    private bool _showBrush = true;
    private Vector2 _layerScroll;

    private Texture2D _previewTexture;

    // Constants
    private const float PAINT_INTERVAL = 0.016f;

    private const string COMPUTE_SHADER_PATH =
        "Assets/MBEditor/Tools/SceneEditor/SceneManager/SceneDataManagerTabSystem/TintComposite.compute";

    #endregion

    #region Lifecycle

    public override void OnEnable(MBSceneDataManager manager, SerializedObject serializedObject)
    {
        base.OnEnable(manager, serializedObject);

        RefreshReferences();
        LoadComputeShader();
        RefreshGeneratorResolution();
        UpdateOverlayScale();

        SceneView.duringSceneGui += OnSceneGUI;
    }

    public override void OnDisable()
    {
        if (_isPainting)
        {
            ExitPaintMode();
        }

        SceneView.duringSceneGui -= OnSceneGUI;
        CleanupRT();

        base.OnDisable();
    }

    public override void OnManagerChanged(MBSceneDataManager manager)
    {
        base.OnManagerChanged(manager);
        RefreshReferences();
        RefreshGeneratorResolution();
        _previewDirty = true;
    }

    private void RefreshReferences()
    {
        _data = manager?.TintData;
        _terrain = manager?.Terrain;
    }

    #endregion

    #region Main Draw

    public override void DrawTab()
    {
        if (manager == null || _terrain == null)
        {
            EditorGUILayout.HelpBox("Assign a Terrain in General tab first.", MessageType.Info);
            return;
        }

        if (_data == null)
        {
            DrawNoDataWarning();
            return;
        }

        EditorGUILayout.Space(8);

        // Layer list
        DrawLayerSection();

        EditorGUILayout.Space(8);

        // Brush settings (always visible when painting or has layers)
        if (_data.HasLayers)
        {
            DrawBrushSection();
        }

        EditorGUILayout.Space(8);

        // Paint controls
        DrawPaintControls();

        EditorGUILayout.Space(8);

        // Preview
        DrawPreview();
    }

    #endregion

    #region Layer Section

    private void DrawLayerSection()
    {
        // Header with add buttons
        EditorGUILayout.BeginHorizontal();

        _showLayers = EditorGUILayout.Foldout(_showLayers, $"Layers ({_data.layers.Count})", true);

        GUILayout.FlexibleSpace();

        // Add Paint Layer
        GUI.backgroundColor = new Color(0.5f, 0.9f, 0.5f);
        if (GUILayout.Button("+ Paint", GUILayout.Width(50), GUILayout.Height(20)))
        {
            AddPaintLayer();
        }

        // Add Texture Layer
        GUI.backgroundColor = new Color(0.7f, 0.8f, 1f);
        if (GUILayout.Button("+ Tex", GUILayout.Width(40), GUILayout.Height(20)))
        {
            AddTextureLayer();
        }

        // Add AO Layer
        GUI.backgroundColor = new Color(0.9f, 0.8f, 0.9f);
        if (GUILayout.Button("+ AO", GUILayout.Width(35), GUILayout.Height(20)))
        {
            AddAOLayer();
        }

        // Add PPM Layer
        GUI.backgroundColor = new Color(1f, 0.9f, 0.7f);
        if (GUILayout.Button("+ PPM", GUILayout.Width(45), GUILayout.Height(20)))
        {
            AddPPMLayer();
        }

        GUI.backgroundColor = Color.white;

        EditorGUILayout.EndHorizontal();

        if (!_showLayers) return;

        EditorGUILayout.BeginVertical(EditorStyles.helpBox);

        if (_data.layers.Count == 0)
        {
            EditorGUILayout.HelpBox(
                "No layers. Add a Paint layer to start painting, Texture/PPM layer to import, or AO layer to bake ambient occlusion.",
                MessageType.Info);
        }
        else
        {
            _layerScroll = EditorGUILayout.BeginScrollView(_layerScroll, GUILayout.MaxHeight(350));

            for (int i = 0; i < _data.layers.Count; i++)
            {
                DrawLayerEntry(i);
            }

            EditorGUILayout.EndScrollView();
        }

        EditorGUILayout.EndVertical();
    }

    private void DrawLayerEntry(int index)
    {
        var layer = _data.layers[index];
        bool isActive = _data.activeLayerIndex == index;
        bool isPaintLayer = layer.type == TerrainTintData.LayerType.Paint;
        bool isTextureLayer = layer.type == TerrainTintData.LayerType.Texture;
        bool isAOLayer = layer.type == TerrainTintData.LayerType.AO;
        bool isPPMLayer = layer.type == TerrainTintData.LayerType.PPM;
        bool hasTexture = layer.HasTexture;
        bool canPaint = layer.CanPaint;

        // Layer type color coding
        Color headerColor;
        if (isPaintLayer)
            headerColor = isActive ? new Color(1f, 0.85f, 0.6f) : new Color(0.9f, 0.95f, 0.9f);
        else if (isAOLayer)
            headerColor = new Color(0.95f, 0.9f, 0.98f);
        else if (isPPMLayer)
            headerColor = new Color(1f, 0.95f, 0.85f);
        else
            headerColor = new Color(0.85f, 0.9f, 1f);

        GUI.backgroundColor = headerColor;
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        GUI.backgroundColor = Color.white;

        // === Header Row ===
        EditorGUILayout.BeginHorizontal();

        // Active toggle
        EditorGUI.BeginChangeCheck();
        layer.active = EditorGUILayout.Toggle(layer.active, GUILayout.Width(16));
        if (EditorGUI.EndChangeCheck())
        {
            MarkDirty();
            _previewDirty = true;
        }

        // Type icon
        string typeIcon = isPaintLayer ? "🖌" : (isAOLayer ? "◐" : (isPPMLayer ? "📄" : "🖼"));
        GUI.color = layer.active ? Color.white : Color.gray;
        EditorGUILayout.LabelField(typeIcon, GUILayout.Width(20));

        // Status indicator (has texture?)
        GUI.color = hasTexture ? (layer.active ? Color.green : Color.gray) : Color.yellow;
        EditorGUILayout.LabelField(hasTexture ? "●" : "○", GUILayout.Width(14));
        GUI.color = Color.white;

        // Foldout with name
        layer.foldout = EditorGUILayout.Foldout(layer.foldout, "", true);

        // Editable name
        EditorGUI.BeginChangeCheck();
        layer.name = EditorGUILayout.TextField(layer.name, GUILayout.MinWidth(60));
        if (EditorGUI.EndChangeCheck())
        {
            MarkDirty();
        }

        GUILayout.FlexibleSpace();

        // Layer-type specific buttons
        if (isPaintLayer)
        {
            if (canPaint)
            {
                GUI.backgroundColor = isActive ? new Color(1f, 0.7f, 0.4f) : new Color(0.6f, 0.9f, 0.6f);
                if (GUILayout.Button(isActive ? "● Active" : "Select", GUILayout.Width(60), GUILayout.Height(18)))
                {
                    _data.activeLayerIndex = index;
                    MarkDirty();
                }

                GUI.backgroundColor = Color.white;
            }
            else if (!hasTexture)
            {
                GUI.backgroundColor = new Color(0.5f, 0.9f, 0.5f);
                if (GUILayout.Button("Create", GUILayout.Width(50), GUILayout.Height(18)))
                {
                    RefreshGeneratorResolution();
                    CreateTextureForLayer(index);
                }

                GUI.backgroundColor = Color.white;
            }
        }
        else if (isAOLayer)
        {
            GUI.backgroundColor = new Color(0.9f, 0.7f, 0.95f);
            if (GUILayout.Button("Bake", GUILayout.Width(50), GUILayout.Height(18)))
            {
                BakeAOLayer(index);
            }

            GUI.backgroundColor = Color.white;
        }

        // Reorder buttons
        GUI.enabled = index > 0;
        if (GUILayout.Button("↑", GUILayout.Width(20), GUILayout.Height(18)))
        {
            MoveLayer(index, -1);
        }

        GUI.enabled = index < _data.layers.Count - 1;
        if (GUILayout.Button("↓", GUILayout.Width(20), GUILayout.Height(18)))
        {
            MoveLayer(index, 1);
        }

        GUI.enabled = true;

        // Delete
        GUI.backgroundColor = new Color(1f, 0.6f, 0.6f);
        if (GUILayout.Button("×", GUILayout.Width(20), GUILayout.Height(18)))
        {
            if (EditorUtility.DisplayDialog("Remove Layer",
                    $"Remove '{layer.name}'?\n\nTexture asset won't be deleted.", "Yes", "No"))
            {
                RemoveLayer(index);
                return; // Exit early, layer is gone
            }
        }

        GUI.backgroundColor = Color.white;

        EditorGUILayout.EndHorizontal();

        // === Expanded Content ===
        if (layer.foldout)
        {
            EditorGUI.indentLevel++;
            DrawLayerDetails(layer, index);
            EditorGUI.indentLevel--;
        }

        EditorGUILayout.EndVertical();

        EditorGUILayout.Space(2);
    }

    private void DrawLayerDetails(TerrainTintData.TintLayer layer, int index)
    {
        EditorGUILayout.Space(4);

        bool isPaintLayer = layer.type == TerrainTintData.LayerType.Paint;
        bool isTextureLayer = layer.type == TerrainTintData.LayerType.Texture;
        bool isAOLayer = layer.type == TerrainTintData.LayerType.AO;
        bool isPPMLayer = layer.type == TerrainTintData.LayerType.PPM;

        // Layer Type (read-only display)
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Type", GUILayout.Width(60));
        GUI.enabled = false;
        EditorGUILayout.EnumPopup(layer.type, GUILayout.Width(80));
        GUI.enabled = true;

        if (isPaintLayer)
            EditorGUILayout.LabelField("(Paintable)", EditorStyles.miniLabel);
        else if (isAOLayer)
            EditorGUILayout.LabelField("(Baked AO)", EditorStyles.miniLabel);
        else if (isPPMLayer)
            EditorGUILayout.LabelField("(M&B Native)", EditorStyles.miniLabel);
        else
            EditorGUILayout.LabelField("(Read-only)", EditorStyles.miniLabel);
        EditorGUILayout.EndHorizontal();

        // Opacity
        EditorGUI.BeginChangeCheck();
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Opacity", GUILayout.Width(60));
        layer.opacity = EditorGUILayout.Slider(layer.opacity, 0f, 1f);
        EditorGUILayout.EndHorizontal();
        if (EditorGUI.EndChangeCheck())
        {
            MarkDirty();
            _previewDirty = true;
        }

        // Blend Mode
        EditorGUI.BeginChangeCheck();
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Blend", GUILayout.Width(60));
        layer.blendMode = (TerrainTintData.BlendMode)EditorGUILayout.EnumPopup(layer.blendMode);
        EditorGUILayout.EndHorizontal();
        if (EditorGUI.EndChangeCheck())
        {
            MarkDirty();
            _previewDirty = true;
        }

        // AO-specific settings
        if (isAOLayer)
        {
            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("AO Settings", EditorStyles.boldLabel);

            EditorGUI.BeginChangeCheck();

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Rays", GUILayout.Width(60));
            layer.aoRayCount = EditorGUILayout.IntSlider(layer.aoRayCount, 16, 512);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Range", GUILayout.Width(60));
            layer.aoRange = EditorGUILayout.Slider(layer.aoRange, 0.1f, 50f);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Intensity", GUILayout.Width(60));
            layer.aoIntensity = EditorGUILayout.Slider(layer.aoIntensity, 0f, 10f);
            EditorGUILayout.EndHorizontal();

            layer.aoIncludeTerrainSelf = EditorGUILayout.Toggle("Include Terrain Self", layer.aoIncludeTerrainSelf);
            layer.aoUseBlur = EditorGUILayout.Toggle("Apply Blur", layer.aoUseBlur);

            if (EditorGUI.EndChangeCheck())
            {
                MarkDirty();
            }

            EditorGUILayout.Space(4);

            // Bake button (larger, in details)
            GUI.backgroundColor = new Color(0.8f, 0.6f, 0.9f);
            if (GUILayout.Button("Bake Ambient Occlusion", GUILayout.Height(24)))
            {
                RefreshGeneratorResolution();
                BakeAOLayer(index);
            }

            GUI.backgroundColor = Color.white;
        }

        // PPM-specific settings
        if (isPPMLayer)
        {
            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("PPM Source", EditorStyles.boldLabel);

            // DefaultAsset field for PPM file
            EditorGUI.BeginChangeCheck();
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("PPM File", GUILayout.Width(60));
            var newPpmAsset = EditorGUILayout.ObjectField(layer.ppmAsset, typeof(UnityEngine.Object), false);
            EditorGUILayout.EndHorizontal();

            if (EditorGUI.EndChangeCheck())
            {
                // Validate it's a PPM file
                if (newPpmAsset != null)
                {
                    string assetPath = AssetDatabase.GetAssetPath(newPpmAsset);
                    if (!string.IsNullOrEmpty(assetPath) && assetPath.ToLower().EndsWith(".ppm"))
                    {
                        Undo.RecordObject(_data, "Assign PPM Asset");
                        layer.ppmAsset = newPpmAsset;
                        layer.ppmSourcePath = System.IO.Path.GetFullPath(assetPath);
                        MarkDirty();

                        // Auto-load the PPM
                        LoadPPMFromAsset(index);
                    }
                    else
                    {
                        EditorUtility.DisplayDialog("Invalid File",
                            "Please assign a .ppm file.", "OK");
                    }
                }
                else
                {
                    // Cleared the field
                    Undo.RecordObject(_data, "Clear PPM Asset");
                    layer.ppmAsset = null;
                    layer.ppmSourcePath = "";

                    // Clear cached texture
                    if (layer.ppmCachedTexture != null)
                    {
                        Object.DestroyImmediate(layer.ppmCachedTexture);
                        layer.ppmCachedTexture = null;
                    }

                    MarkDirty();
                    _previewDirty = true;
                }
            }

            // Show cached texture info
            if (layer.ppmCachedTexture != null)
            {
                EditorGUILayout.LabelField($"Loaded: {layer.ppmCachedTexture.width}×{layer.ppmCachedTexture.height}",
                    EditorStyles.miniLabel);
            }
        }

        EditorGUILayout.Space(4);

        if (isPPMLayer)
        {
            if (layer.ppmCachedTexture == null && layer.ppmAsset != null)
            {
                LoadPPMFromAsset(index);
            }

            _previewTexture = layer.ppmCachedTexture;
        }
        else
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Texture", GUILayout.Width(60));
            layer.texture = (Texture2D)EditorGUILayout.ObjectField(layer.texture, typeof(Texture2D), false);
            EditorGUILayout.EndHorizontal();

            _previewTexture = layer.texture;
        }

        EditorGUILayout.Space(4);

        // Texture field
        EditorGUILayout.BeginHorizontal();
        if (_previewTexture != null)
        {
            float maxHeight = 100f;
            float aspect = (float)_previewTexture.width / _previewTexture.height;
            Rect rect = GUILayoutUtility.GetRect(maxHeight * aspect, maxHeight, GUILayout.ExpandWidth(true));

            float width = Mathf.Min(rect.width, rect.height * aspect);
            float height = width / aspect;
            Rect texRect = new Rect(rect.x + (rect.width - width) * 0.5f, rect.y, width, height);

            EditorGUI.DrawPreviewTexture(texRect, _previewTexture);
        }

        // Texture info
        EditorGUILayout.BeginVertical();
        if (layer.texture != null)
        {
            EditorGUILayout.LabelField($"{layer.texture.width}×{layer.texture.height}", EditorStyles.miniLabel);

            // Resolution warning
            if (_hasGeneratorRes && (layer.texture.width != _generatorResX || layer.texture.height != _generatorResY))
            {
                GUI.color = Color.yellow;
                EditorGUILayout.LabelField($"⚠ Gen: {_generatorResX}×{_generatorResY}", EditorStyles.miniLabel);
                GUI.color = Color.white;
            }
        }
        else
        {
            if (isPaintLayer)
            {
                if (GUILayout.Button("Create Texture", GUILayout.Height(20)))
                {
                    RefreshGeneratorResolution();
                    CreateTextureForLayer(index);
                }
            }
            else if (isAOLayer)
            {
                EditorGUILayout.LabelField("Click 'Bake' to generate", EditorStyles.miniLabel);
            }
        }

        EditorGUILayout.EndVertical();
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(4);
    }

    #endregion

    #region Brush Section

    private void DrawBrushSection()
    {
        _showBrush = EditorGUILayout.Foldout(_showBrush, "Brush Settings", true);

        if (!_showBrush) return;

        EditorGUILayout.BeginVertical(EditorStyles.helpBox);

        EditorGUI.BeginChangeCheck();

        // Brush color with large preview
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Color", GUILayout.Width(50));
        _data.brushColor = EditorGUILayout.ColorField(_data.brushColor, GUILayout.Height(24));

        // Erase toggle
        GUI.backgroundColor = _data.eraseMode ? new Color(1f, 0.5f, 0.5f) : Color.white;
        if (GUILayout.Button(_data.eraseMode ? "ERASE" : "Paint", GUILayout.Width(60), GUILayout.Height(24)))
        {
            _data.eraseMode = !_data.eraseMode;
        }

        GUI.backgroundColor = Color.white;
        EditorGUILayout.EndHorizontal();

        // Size
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Size", GUILayout.Width(50));
        _data.brushSize = EditorGUILayout.Slider(_data.brushSize, 1f, 500f);
        EditorGUILayout.EndHorizontal();

        // Strength
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Strength", GUILayout.Width(50));
        _data.brushStrength = EditorGUILayout.Slider(_data.brushStrength, 0f, 1f);
        EditorGUILayout.EndHorizontal();

        // Falloff
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Falloff", GUILayout.Width(50));
        _data.brushFalloff = EditorGUILayout.Slider(_data.brushFalloff, 0f, 1f);
        EditorGUILayout.EndHorizontal();

        if (EditorGUI.EndChangeCheck())
        {
            MarkDirty();
        }

        EditorGUILayout.EndVertical();
    }

    #endregion

    #region Paint Controls

    private void DrawPaintControls()
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);

        // Active layer info
        var layer = _data.ActiveLayer;
        if (layer != null)
        {
            bool isPaintLayer = layer.type == TerrainTintData.LayerType.Paint;
            string typeLabel = isPaintLayer ? "🖌 Paint" : "🖼 Texture";
            string texInfo = layer.texture != null
                ? $"{layer.texture.width}×{layer.texture.height}"
                : "No texture";

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"Active: {layer.name}", EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();

            GUI.color = isPaintLayer ? new Color(0.5f, 0.9f, 0.5f) : new Color(0.7f, 0.8f, 1f);
            EditorGUILayout.LabelField(typeLabel, EditorStyles.miniLabel, GUILayout.Width(60));
            GUI.color = Color.white;

            EditorGUILayout.LabelField($"({texInfo})", EditorStyles.miniLabel, GUILayout.Width(100));
            EditorGUILayout.EndHorizontal();
        }

        EditorGUILayout.Space(4);

        // Determine if we can paint
        bool canPaint = _data.CanPaint;
        bool isTextureLayer = layer != null && layer.type == TerrainTintData.LayerType.Texture;
        bool noTexture = layer != null && layer.texture == null;

        // Main paint toggle
        EditorGUILayout.BeginHorizontal();

        GUI.enabled = canPaint;

        Color buttonColor = _isPainting
            ? new Color(1f, 0.5f, 0.3f)
            : new Color(0.4f, 0.9f, 0.4f);
        string buttonLabel = _isPainting ? "■ Stop Painting" : "▶ Start Painting";

        GUI.backgroundColor = buttonColor;
        if (GUILayout.Button(buttonLabel, GUILayout.Height(32)))
        {
            if (_isPainting)
                ExitPaintMode();
            else
                EnterPaintMode();
        }

        GUI.backgroundColor = Color.white;
        GUI.enabled = true;

        // Help text for why painting is disabled
        if (!canPaint)
        {
            string reason = "";
            if (layer == null)
                reason = "No layer selected";
            else if (isTextureLayer)
                reason = "Texture layers are read-only";
            else if (noTexture)
                reason = "Create texture first";

            EditorGUILayout.LabelField($"← {reason}", EditorStyles.centeredGreyMiniLabel, GUILayout.Width(160));
        }

        EditorGUILayout.EndHorizontal();

        // Quick actions when not painting (only for paint layers)
        if (!_isPainting && layer != null && layer.type == TerrainTintData.LayerType.Paint && layer.texture != null)
        {
            EditorGUILayout.Space(4);

            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button("Clear to White", GUILayout.Height(22)))
            {
                ClearActiveLayer();
            }

            if (GUILayout.Button("Fill with Color", GUILayout.Height(22)))
            {
                FillActiveLayer();
            }

            EditorGUILayout.EndHorizontal();
        }

        // Instructions when painting
        if (_isPainting)
        {
            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("LMB: Paint | Shift+Click: Toggle Erase | [ ]: Size | Esc: Stop",
                EditorStyles.centeredGreyMiniLabel);
        }

        EditorGUILayout.EndVertical();

        // Bake controls (separate box)
        EditorGUILayout.Space(4);
        DrawBakeControls();
    }

    private void DrawBakeControls()
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);

        GUI.enabled = false;

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Output", EditorStyles.boldLabel, GUILayout.Width(50));

        EditorGUI.BeginChangeCheck();
        _data.bakedTintMap = (Texture2D)EditorGUILayout.ObjectField(
            _data.bakedTintMap, typeof(Texture2D), false, GUILayout.Height(18));
        if (EditorGUI.EndChangeCheck())
        {
            MarkDirty();
        }

        EditorGUILayout.EndHorizontal();

        // PNG export buttons
        GUI.enabled = _data.HasLayers;

        GUI.backgroundColor = new Color(0.5f, 0.9f, 0.5f);
        if (GUILayout.Button("Bake PNG", GUILayout.Height(24)))
        {
            RefreshGeneratorResolution();
            BakeAndApply();
        }

        // PPM export button

        GUI.enabled = _data.HasLayers;
        GUI.backgroundColor = new Color(1f, 0.9f, 0.7f);
        if (GUILayout.Button("Export to PPM (M&B)", GUILayout.Height(22)))
        {
            ExportToPPM();
        }

        GUI.backgroundColor = Color.white;
        GUI.enabled = true;

        EditorGUILayout.EndVertical();
    }

    #endregion

    #region Preview

    private void DrawPreview()
    {
        EditorGUILayout.LabelField("Preview", EditorStyles.boldLabel);

        EditorGUILayout.BeginVertical(EditorStyles.helpBox);

        // Update preview if dirty
        if (_previewDirty && _compositeShader != null && _data.HasLayers)
        {
            CompositeGPU();
            _previewDirty = false;
        }

        // Get display texture
        Texture displayTex = _livePreviewRT;
        if (displayTex == null) displayTex = _data.bakedTintMap;

        if (displayTex != null)
        {
            float maxHeight = 150f;
            float aspect = (float)displayTex.width / displayTex.height;
            Rect rect = GUILayoutUtility.GetRect(maxHeight * aspect, maxHeight, GUILayout.ExpandWidth(true));

            float width = Mathf.Min(rect.width, rect.height * aspect);
            float height = width / aspect;
            Rect texRect = new Rect(rect.x + (rect.width - width) * 0.5f, rect.y, width, height);

            EditorGUI.DrawPreviewTexture(texRect, displayTex);

            string label = _livePreviewRT != null ? "Live Preview" : "Baked";
            EditorGUILayout.LabelField($"{label}: {displayTex.width}×{displayTex.height}",
                EditorStyles.centeredGreyMiniLabel);
        }
        else
        {
            EditorGUILayout.HelpBox("No preview. Add layers and create textures.", MessageType.Info);
        }

        EditorGUILayout.EndVertical();
    }

    #endregion

    #region Scene GUI (Painting)

    private void OnSceneGUI(SceneView sceneView)
    {
        if (!_isPainting || _data == null || _terrain == null)
            return;

        Event e = Event.current;

        // Let Alt pass through to Unity's scene orbit/pan navigation
        if (e.alt)
            return;

        HandleKeyboard(e);

        // Raycast terrain
        Ray ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);
        if (!Physics.Raycast(ray, out RaycastHit hit, 10000f))
        {
            return;
        }

        if (hit.collider.GetComponent<Terrain>() != _terrain)
            return;

        Vector3 worldPos = hit.point;

        DrawBrushPreview(worldPos);
        HandlePaintInput(e, worldPos);

        if (e.type == EventType.Layout)
        {
            HandleUtility.AddDefaultControl(GUIUtility.GetControlID(FocusType.Passive));
        }
    }

    private void HandleKeyboard(Event e)
    {
        if (e.type != EventType.KeyDown) return;

        switch (e.keyCode)
        {
            case KeyCode.LeftBracket:
                _data.brushSize = Mathf.Max(1f, _data.brushSize - 10f);
                e.Use();
                break;
            case KeyCode.RightBracket:
                _data.brushSize = Mathf.Min(500f, _data.brushSize + 10f);
                e.Use();
                break;
            case KeyCode.Escape:
                ExitPaintMode();
                e.Use();
                break;
        }
    }

    private void HandlePaintInput(Event e, Vector3 worldPos)
    {
        bool isLeftMouse = e.button == 0;

        // Shift+click toggles erase
        if (e.type == EventType.MouseDown && isLeftMouse && e.shift)
        {
            _data.eraseMode = !_data.eraseMode;
            MarkDirty();
            e.Use();
            return;
        }

        if (e.type == EventType.MouseDown && isLeftMouse)
        {
            _isMouseDown = true;
            PaintAt(worldPos);
            e.Use();
        }
        else if (e.type == EventType.MouseDrag && isLeftMouse && _isMouseDown)
        {
            PaintAt(worldPos);
            e.Use();
        }
        else if (e.type == EventType.MouseUp && isLeftMouse)
        {
            _isMouseDown = false;
            SaveActiveTexture();
            e.Use();
        }
    }

    private void PaintAt(Vector3 worldPos)
    {
        // Throttle
        float time = Time.realtimeSinceStartup;
        if (time - _lastPaintTime < PAINT_INTERVAL)
            return;
        _lastPaintTime = time;

        var layer = _data.ActiveLayer;
        if (layer == null || !layer.CanPaint) return;

        var tex = layer.texture;
        var terrainPos = _terrain.transform.position;
        var terrainSize = _terrain.terrainData.size;

        // World to UV
        float u = Mathf.Clamp01((worldPos.x - terrainPos.x) / terrainSize.x);
        float v = Mathf.Clamp01((worldPos.z - terrainPos.z) / terrainSize.z);

        // UV to pixel
        int centerX = Mathf.RoundToInt(u * (tex.width - 1));
        int centerY = Mathf.RoundToInt(v * (tex.height - 1));

        // Brush radius in pixels
        float pixelsPerUnit = tex.width / terrainSize.x;
        int radiusPixels = Mathf.CeilToInt(_data.brushSize * pixelsPerUnit * 0.5f);

        Color paintColor = _data.EffectiveBrushColor;

        // Paint pixels
        int minX = Mathf.Max(0, centerX - radiusPixels);
        int maxX = Mathf.Min(tex.width - 1, centerX + radiusPixels);
        int minY = Mathf.Max(0, centerY - radiusPixels);
        int maxY = Mathf.Min(tex.height - 1, centerY + radiusPixels);

        for (int y = minY; y <= maxY; y++)
        {
            for (int x = minX; x <= maxX; x++)
            {
                float dist = Vector2.Distance(new Vector2(x, y), new Vector2(centerX, centerY));
                if (dist > radiusPixels) continue;

                float normalizedDist = dist / radiusPixels;
                float falloff = 1f - Mathf.Pow(normalizedDist, 1f / Mathf.Max(0.01f, _data.brushFalloff));
                float strength = _data.brushStrength * Mathf.Clamp01(falloff);

                if (strength <= 0f) continue;

                Color existing = tex.GetPixel(x, y);
                Color blended = Color.Lerp(existing, paintColor, strength);
                tex.SetPixel(x, y, blended);
            }
        }

        tex.Apply();
        layer.isDirty = true;

        // Update GPU composite
        if (_compositeShader != null)
        {
            CompositeGPU();
        }
    }

    private void DrawBrushPreview(Vector3 worldPos)
    {
        float radius = _data.brushSize * 0.5f;

        Handles.color = _data.eraseMode ? Color.white : _data.brushColor;
        Handles.DrawWireDisc(worldPos, Vector3.up, radius);

        float innerRadius = radius * (1f - _data.brushFalloff);
        Handles.color = new Color(Handles.color.r, Handles.color.g, Handles.color.b, 0.4f);
        Handles.DrawWireDisc(worldPos, Vector3.up, innerRadius);

        Handles.color = _data.eraseMode ? Color.red : _data.brushColor;
        Handles.DrawSolidDisc(worldPos, Vector3.up, radius * 0.02f);

        SceneView.RepaintAll();
    }

    #endregion

    #region Paint Mode

    private void EnterPaintMode()
    {
        var layer = _data.ActiveLayer;

        if (layer == null)
        {
            Debug.LogWarning("[TintTab] Cannot paint: no layer selected.");
            return;
        }

        if (layer.type != TerrainTintData.LayerType.Paint)
        {
            Debug.LogWarning("[TintTab] Cannot paint: selected layer is a Texture layer (read-only).");
            EditorUtility.DisplayDialog("Cannot Paint",
                "Texture layers are read-only. Select a Paint layer to paint.", "OK");
            return;
        }

        if (layer.texture == null)
        {
            Debug.LogWarning("[TintTab] Cannot paint: layer has no texture.");
            return;
        }

        // Check resolution
        var tex = layer.texture;
        if (_hasGeneratorRes && (tex.width != _generatorResX || tex.height != _generatorResY))
        {
            int choice = EditorUtility.DisplayDialogComplex("Resolution Mismatch",
                $"Texture ({tex.width}×{tex.height}) doesn't match generator ({_generatorResX}×{_generatorResY}).",
                "Resize", "Paint Anyway", "Cancel");

            if (choice == 2) return; // Cancel
            if (choice == 0) ResizeTexture(tex, _generatorResX, _generatorResY);
        }

        _isPainting = true;

        UpdateOverlayScale();

        // Initialize GPU preview
        if (_compositeShader != null)
        {
            CompositeGPU();
            ApplyPreviewToMaterial();
        }

        Tools.current = Tool.None;
        SceneView.RepaintAll();

        Debug.Log($"[TintTab] Started painting on: {layer.name}");
    }

    private void ExitPaintMode()
    {
        _isPainting = false;
        _isMouseDown = false;

        // Auto-bake on exit
        if (_data.HasLayers)
        {
            BakeAndApply();
        }

        SceneView.RepaintAll();

        Debug.Log("[TintTab] Stopped painting. Auto-baked to PNG.");
    }

    #endregion

    #region Layer Operations

    private void AddPaintLayer()
    {
        Undo.RecordObject(_data, "Add Paint Layer");
        _data.AddPaintLayer();
        MarkDirty();
        _previewDirty = true;
    }

    private void AddTextureLayer()
    {
        Undo.RecordObject(_data, "Add Texture Layer");
        _data.AddTextureLayer();
        MarkDirty();
        _previewDirty = true;
    }

    private void AddAOLayer()
    {
        Undo.RecordObject(_data, "Add AO Layer");
        _data.AddAOLayer();
        MarkDirty();
        _previewDirty = true;
    }

    private void AddPPMLayer()
    {
        Undo.RecordObject(_data, "Add PPM Layer");
        _data.AddPPMLayer();
        MarkDirty();
        _previewDirty = true;
    }

    private void RemoveLayer(int index)
    {
        Undo.RecordObject(_data, "Remove Tint Layer");
        _data.RemoveLayer(index);
        MarkDirty();
        _previewDirty = true;
    }

    private void MoveLayer(int index, int direction)
    {
        Undo.RecordObject(_data, "Move Tint Layer");
        if (direction < 0)
            _data.MoveLayerUp(index);
        else
            _data.MoveLayerDown(index);
        MarkDirty();
        _previewDirty = true;
    }

    private void CreateTextureForLayer(int index)
    {
        var layer = _data.GetLayer(index);
        if (layer == null) return;

        // Only paint layers can have textures created
        if (layer.type != TerrainTintData.LayerType.Paint)
        {
            EditorUtility.DisplayDialog("Cannot Create",
                "Texture layers use existing textures. Assign one in the texture field.\n" +
                "AO layers use the Bake function to generate textures.", "OK");
            return;
        }

        if (!_hasGeneratorRes)
        {
            EditorUtility.DisplayDialog("No Resolution",
                "Set terrain hash in Heightmap Generator first to determine resolution.", "OK");
            return;
        }

        string folder = manager.EditorSceneTintDataPath;

        if (!Directory.Exists(folder))
        {
            Directory.CreateDirectory(folder);
        }

        Undo.RecordObject(_data, "Create Layer Texture");

        // Create white texture
        var tex = new Texture2D(_generatorResX, _generatorResY, TextureFormat.RGB24, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;

        Color[] pixels = new Color[_generatorResX * _generatorResY];
        for (int i = 0; i < pixels.Length; i++)
            pixels[i] = Color.white;
        tex.SetPixels(pixels);
        tex.Apply();

        // Save as PNG
        string safeName = layer.name.Replace(" ", "_").Replace("/", "_");
        string path = $"{folder}/{safeName}.png";

        File.WriteAllBytes(path, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);

        AssetDatabase.ImportAsset(path);

        // Configure import
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer != null)
        {
            importer.isReadable = true;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.sRGBTexture = false;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.SaveAndReimport();
        }

        layer.texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        MarkDirty();
        _previewDirty = true;

        Debug.Log($"[TintTab] Created texture: {path}");
    }

    private void BakeAOLayer(int index)
    {
        var layer = _data.GetLayer(index);
        if (layer == null || layer.type != TerrainTintData.LayerType.AO)
        {
            Debug.LogError("[TintTab] Invalid layer for AO baking.");
            return;
        }

        if (_terrain == null)
        {
            EditorUtility.DisplayDialog("No Terrain", "No terrain assigned.", "OK");
            return;
        }

        if (!_hasGeneratorRes)
        {
            EditorUtility.DisplayDialog("No Resolution",
                "Set terrain hash in Heightmap Generator first to determine resolution.", "OK");
            return;
        }

        string folder = manager.EditorSceneTintDataPath;

        if (!Directory.Exists(folder))
        {
            Directory.CreateDirectory(folder);
        }

        // Bake AO
        var aoTex = TintAOBaker.BakeAO(
            _terrain,
            _generatorResX,
            _generatorResY,
            layer.aoRayCount,
            layer.aoRange,
            layer.aoIntensity,
            layer.aoIncludeTerrainSelf,
            layer.aoUseBlur
        );

        if (aoTex == null)
        {
            Debug.LogWarning("[TintTab] AO baking was cancelled or failed.");
            return;
        }

        // Save texture
        string path = TintAOBaker.SaveAOTexture(aoTex, folder, layer.name);
        Object.DestroyImmediate(aoTex);

        if (!string.IsNullOrEmpty(path))
        {
            Undo.RecordObject(_data, "Bake AO Layer");
            layer.texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            MarkDirty();
            _previewDirty = true;

            Debug.Log($"[TintTab] AO baked for layer '{layer.name}': {path}");
        }
    }

    private void LoadPPMLayer(int index)
    {
        var layer = _data.GetLayer(index);
        if (layer == null || layer.type != TerrainTintData.LayerType.PPM)
        {
            Debug.LogError("[TintTab] Invalid layer for PPM loading.");
            return;
        }

        // Open file dialog for PPM
        string ppmPath = EditorUtility.OpenFilePanel("Load PPM File", "", "ppm");
        if (string.IsNullOrEmpty(ppmPath)) return;

        LoadPPMFromPath(index, ppmPath);
    }

    private void ReloadPPMLayer(int index)
    {
        var layer = _data.GetLayer(index);
        if (layer == null || layer.type != TerrainTintData.LayerType.PPM)
        {
            Debug.LogError("[TintTab] Invalid layer for PPM reload.");
            return;
        }

        if (string.IsNullOrEmpty(layer.ppmSourcePath))
        {
            EditorUtility.DisplayDialog("No Source", "No PPM source path stored.", "OK");
            return;
        }

        if (!File.Exists(layer.ppmSourcePath))
        {
            EditorUtility.DisplayDialog("File Not Found",
                $"Source file not found:\n{layer.ppmSourcePath}", "OK");
            return;
        }

        LoadPPMFromPath(index, layer.ppmSourcePath);
    }

    private void LoadPPMFromAsset(int index)
    {
        var layer = _data.GetLayer(index);
        if (layer == null || layer.type != TerrainTintData.LayerType.PPM)
        {
            Debug.LogError("[TintTab] Invalid layer for PPM asset loading.");
            return;
        }

        if (layer.ppmAsset == null)
        {
            Debug.LogWarning("[TintTab] No PPM asset assigned.");
            return;
        }

        string assetPath = AssetDatabase.GetAssetPath(layer.ppmAsset);
        if (string.IsNullOrEmpty(assetPath))
        {
            Debug.LogError("[TintTab] Could not get asset path.");
            return;
        }

        // Get full path
        string fullPath = Path.GetFullPath(assetPath);

        if (!File.Exists(fullPath))
        {
            Debug.LogError($"[TintTab] PPM file not found: {fullPath}");
            return;
        }

        // Load directly into cached texture (no PNG conversion needed)
        LoadPPMToCachedTexture(index, fullPath);
    }

    private void LoadPPMToCachedTexture(int index, string ppmPath)
    {
        var layer = _data.GetLayer(index);
        if (layer == null) return;

        try
        {
            EditorUtility.DisplayProgressBar("Loading PPM", "Reading...", 0.5f);

            Color[,] ppmData = PpmHelper.ReadPpm(ppmPath);
        
            if (layer.ppmCachedTexture != null)
                Object.DestroyImmediate(layer.ppmCachedTexture);

            // Single call handles all M&B coordinate conversion
            layer.ppmCachedTexture = PpmHelper.MBTerrainPpmToTexture(ppmData, convertToLinear: false);
            layer.ppmCachedTexture.hideFlags = HideFlags.HideAndDontSave;
            layer.ppmSourcePath = ppmPath;

            MarkDirty();
            _previewDirty = true;
            EditorUtility.ClearProgressBar();
        }
        catch (Exception e)
        {
            EditorUtility.ClearProgressBar();
            Debug.LogError($"[TintTab] PPM load error: {e}");
        }
    }

    private void LoadPPMFromPath(int index, string ppmPath)
    {
        var layer = _data.GetLayer(index);
        if (layer == null) return;

        try
        {
            EditorUtility.DisplayProgressBar("Loading PPM", "Reading PPM file...", 0.3f);

            // Read PPM data
            Color[,] ppmData = PpmHelper.ReadPpm(ppmPath);
            ppmData = PpmHelper.FlipVertical(ppmData);
            // ppmData = PpmHelper.SRGBToLinear(ppmData);   
            ppmData = PpmHelper.LinearToSRGB(ppmData);

            int height = ppmData.GetLength(0);
            int width = ppmData.GetLength(1);

            EditorUtility.DisplayProgressBar("Loading PPM", "Creating texture...", 0.6f);

            // Create texture
            var tex = new Texture2D(width, height, TextureFormat.RGB24, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;

            // Copy pixels (PPM uses [y,x], we need to convert)
            Color[] pixels = new Color[width * height];
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    pixels[y * width + x] = ppmData[y, x];
                }
            }

            tex.SetPixels(pixels);
            tex.Apply();

            EditorUtility.DisplayProgressBar("Loading PPM", "Saving texture...", 0.8f);

            // Ask where to save
            string folder = EditorUtility.OpenFolderPanel("Save Imported PPM Texture", "Assets", "");
            if (string.IsNullOrEmpty(folder))
            {
                Object.DestroyImmediate(tex);
                EditorUtility.ClearProgressBar();
                return;
            }

            if (folder.StartsWith(Application.dataPath))
                folder = "Assets" + folder.Substring(Application.dataPath.Length);

            // Save as PNG
            string fileName = Path.GetFileNameWithoutExtension(ppmPath);
            string savePath = AssetDatabase.GenerateUniqueAssetPath(
                $"{folder}/PPM_{fileName}.png");

            File.WriteAllBytes(savePath, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);

            AssetDatabase.ImportAsset(savePath);

            // Configure import
            var importer = AssetImporter.GetAtPath(savePath) as TextureImporter;
            if (importer != null)
            {
                importer.isReadable = true;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.mipmapEnabled = false;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.npotScale = TextureImporterNPOTScale.None;
                importer.SaveAndReimport();
            }

            // Update layer
            Undo.RecordObject(_data, "Load PPM Layer");
            layer.texture = AssetDatabase.LoadAssetAtPath<Texture2D>(savePath);
            layer.ppmSourcePath = ppmPath;
            MarkDirty();
            _previewDirty = true;

            EditorUtility.ClearProgressBar();

            Debug.Log($"[TintTab] Loaded PPM for layer '{layer.name}': {ppmPath} -> {savePath}");
        }
        catch (System.Exception e)
        {
            EditorUtility.ClearProgressBar();
            EditorUtility.DisplayDialog("PPM Load Error",
                $"Failed to load PPM file:\n{e.Message}", "OK");
            Debug.LogError($"[TintTab] PPM load error: {e}");
        }
    }

    private void ClearLayer(int index)
    {
        var layer = _data.GetLayer(index);
        if (layer?.texture == null) return;

        // Only paint layers can be cleared
        if (layer.type != TerrainTintData.LayerType.Paint)
        {
            EditorUtility.DisplayDialog("Cannot Clear",
                "Only Paint layers can be cleared. AO and Texture layers are read-only.", "OK");
            return;
        }

        if (!EditorUtility.DisplayDialog("Clear", $"Clear '{layer.name}' to white?", "Yes", "No"))
            return;

        Undo.RecordObject(layer.texture, "Clear Layer");
        FillTexture(layer.texture, Color.white);
        SaveTexture(layer.texture);
        _previewDirty = true;
    }

    private void ClearActiveLayer()
    {
        var layer = _data.ActiveLayer;
        if (layer == null) return;

        int index = _data.activeLayerIndex;
        ClearLayer(index);
    }

    private void FillActiveLayer()
    {
        var layer = _data.ActiveLayer;
        if (layer?.texture == null) return;

        // Only paint layers can be filled
        if (layer.type != TerrainTintData.LayerType.Paint)
        {
            EditorUtility.DisplayDialog("Cannot Fill",
                "Only Paint layers can be filled. AO and Texture layers are read-only.", "OK");
            return;
        }

        if (!EditorUtility.DisplayDialog("Fill", $"Fill '{layer.name}' with brush color?", "Yes", "No"))
            return;

        Undo.RecordObject(layer.texture, "Fill Layer");
        FillTexture(layer.texture, _data.brushColor);
        SaveTexture(layer.texture);
        _previewDirty = true;
    }

    #endregion

    #region GPU Compositing

    private void LoadComputeShader()
    {
        _compositeShader = AssetDatabase.LoadAssetAtPath<ComputeShader>(COMPUTE_SHADER_PATH);

        if (_compositeShader == null)
        {
            // Try alternative paths
            string[] searchPaths = new[]
            {
                COMPUTE_SHADER_PATH,
                "Assets/MBEditor/TintComposite.compute",
                "Assets/Editor/TintComposite.compute",
                "Assets/TintComposite.compute"
            };

            foreach (var path in searchPaths)
            {
                _compositeShader = AssetDatabase.LoadAssetAtPath<ComputeShader>(path);
                if (_compositeShader != null)
                {
                    Debug.Log($"[TintTab] Found compute shader at: {path}");
                    break;
                }
            }

            // Last resort - search entire project
            if (_compositeShader == null)
            {
                string[] guids = AssetDatabase.FindAssets("TintComposite t:ComputeShader");
                if (guids.Length > 0)
                {
                    string foundPath = AssetDatabase.GUIDToAssetPath(guids[0]);
                    _compositeShader = AssetDatabase.LoadAssetAtPath<ComputeShader>(foundPath);
                    Debug.Log($"[TintTab] Found compute shader via search at: {foundPath}");
                }
            }
        }

        if (_compositeShader != null)
        {
            // Validate kernel exists
            if (_compositeShader.HasKernel("CompositeAll"))
            {
                _kernelAll = _compositeShader.FindKernel("CompositeAll");
                Debug.Log($"[TintTab] Compute shader loaded. Kernel 'CompositeAll' = {_kernelAll}");
            }
            else
            {
                Debug.LogError("[TintTab] Compute shader found but missing 'CompositeAll' kernel!");
                _compositeShader = null;
            }
        }
        else
        {
            Debug.LogWarning("[TintTab] TintComposite.compute not found. GPU compositing disabled. " +
                             "Place the compute shader in your project.");
        }
    }

    private void CompositeGPU()
    {
        if (_compositeShader == null || !_data.HasLayers) return;

        // Determine size
        Vector2Int size = GetOutputSize();
        if (size.x == 0 || size.y == 0) return;

        // Ensure RT
        EnsureRT(size.x, size.y);

        // Validate RT was created
        if (_livePreviewRT == null || !_livePreviewRT.IsCreated())
        {
            Debug.LogError("[TintTab] Failed to create RenderTexture for compositing.");
            return;
        }

        // Validate kernel
        if (_kernelAll < 0)
        {
            Debug.LogError("[TintTab] Invalid kernel index. Cannot composite.");
            return;
        }

        // Get or create fallback white texture
        Texture2D white = GetWhiteTexture();
        if (white == null)
        {
            Debug.LogError("[TintTab] Failed to get white texture fallback.");
            return;
        }

        try
        {
            // Setup shader
            _compositeShader.SetTexture(_kernelAll, "_Output", _livePreviewRT);
            _compositeShader.SetInts("_OutputSize", size.x, size.y);

            int layerCount = Mathf.Min(_data.layers.Count, 8);
            _compositeShader.SetInt("_LayerCount", layerCount);

            Vector4[] settings = new Vector4[8];

            for (int i = 0; i < 8; i++)
            {
                Texture2D tex = white;
                float opacity = 0f;
                float blendMode = 0f;
                float active = 0f;

                if (i < _data.layers.Count)
                {
                    var layer = _data.layers[i];
                    // Use EffectiveTexture to handle PPM cached textures
                    var layerTex = layer.EffectiveTexture;
                    if (layerTex != null)
                    {
                        tex = layerTex;
                    }

                    opacity = layer.opacity;
                    blendMode = (float)layer.blendMode;
                    active = layer.active ? 1f : 0f;
                }

                _compositeShader.SetTexture(_kernelAll, $"_Layer{i}", tex);
                settings[i] = new Vector4(opacity, blendMode, active, 0);
            }

            _compositeShader.SetVectorArray("_LayerSettings", settings);

            // Dispatch
            int groupsX = Mathf.CeilToInt(size.x / 8f);
            int groupsY = Mathf.CeilToInt(size.y / 8f);
            _compositeShader.Dispatch(_kernelAll, groupsX, groupsY, 1);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[TintTab] GPU composite failed: {e.Message}");
        }
    }

    // Cached white texture for fallback
    private Texture2D _fallbackWhite;

    private Texture2D GetWhiteTexture()
    {
        // Try built-in first
        if (Texture2D.whiteTexture != null)
            return Texture2D.whiteTexture;

        // Create our own fallback
        if (_fallbackWhite == null)
        {
            _fallbackWhite = new Texture2D(4, 4, TextureFormat.RGBA32, false);
            Color[] pixels = new Color[16];
            for (int i = 0; i < 16; i++)
                pixels[i] = Color.white;
            _fallbackWhite.SetPixels(pixels);
            _fallbackWhite.Apply();
            _fallbackWhite.hideFlags = HideFlags.HideAndDontSave;
        }

        return _fallbackWhite;
    }

    private void EnsureRT(int width, int height)
    {
        if (_livePreviewRT != null && _livePreviewRT.IsCreated() &&
            _livePreviewRT.width == width && _livePreviewRT.height == height)
            return;

        CleanupRT();

        try
        {
            _livePreviewRT =
                new RenderTexture(width, height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
            _livePreviewRT.enableRandomWrite = true;
            _livePreviewRT.filterMode = FilterMode.Bilinear;
            _livePreviewRT.wrapMode = TextureWrapMode.Clamp;

            if (!_livePreviewRT.Create())
            {
                Debug.LogError($"[TintTab] Failed to create RenderTexture {width}x{height}");
                Object.DestroyImmediate(_livePreviewRT);
                _livePreviewRT = null;
                return;
            }

            // Init to white
            RenderTexture prev = RenderTexture.active;
            RenderTexture.active = _livePreviewRT;
            GL.Clear(true, true, Color.white);
            RenderTexture.active = prev;

            Debug.Log($"[TintTab] Created preview RT: {width}x{height}");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[TintTab] Exception creating RenderTexture: {e.Message}");
            _livePreviewRT = null;
        }
    }

    private void CleanupRT()
    {
        if (_livePreviewRT != null)
        {
            RemovePreviewFromMaterial();
            _livePreviewRT.Release();
            Object.DestroyImmediate(_livePreviewRT);
            _livePreviewRT = null;
        }

        if (_fallbackWhite != null)
        {
            Object.DestroyImmediate(_fallbackWhite);
            _fallbackWhite = null;
        }
    }

    private void ApplyPreviewToMaterial()
    {
        if (_livePreviewRT == null || _terrain?.materialTemplate == null) return;

        var mat = _terrain.materialTemplate;
        if (mat.HasProperty(_data.shaderProperty))
        {
            mat.SetTexture(_data.shaderProperty, _livePreviewRT);
        }

        UpdateOverlayScale();
    }

    private void RemovePreviewFromMaterial()
    {
        if (_terrain?.materialTemplate == null) return;

        var mat = _terrain.materialTemplate;
        if (mat.HasProperty(_data.shaderProperty))
        {
            // Restore baked texture or null
            mat.SetTexture(_data.shaderProperty, _data.bakedTintMap);
        }
    }

    private Vector2Int GetOutputSize()
    {
        if (_hasGeneratorRes)
            return new Vector2Int(_generatorResX, _generatorResY);


        // Use first layer with texture (including PPM cached)
        foreach (var layer in _data.layers)
        {
            var tex = layer.EffectiveTexture;
            if (tex != null)
                return new Vector2Int(tex.width, tex.height);
        }

        return new Vector2Int(1024, 1024);
    }

    #endregion

    #region Baking

    private void BakeAndApply()
    {
        if (!_data.HasLayers) return;

        // Ensure we have an output path
        if (_data.bakedTintMap == null)
        {
            BakeAsNew();
            return;
        }

        // Bake from RT or CPU
        Texture2D baked = BakeToTexture();
        if (baked == null) return;

        // Save
        string path = AssetDatabase.GetAssetPath(_data.bakedTintMap);
        File.WriteAllBytes(path, baked.EncodeToPNG());
        Object.DestroyImmediate(baked);

        AssetDatabase.ImportAsset(path);

        // Apply to material
        ApplyBakedToMaterial();

        Debug.Log($"[TintTab] Baked to: {path}");
    }

    private void BakeAsNew()
    {
        string path = manager.EditorSceneTintDataPath;

        if (!Directory.Exists(path))
        {
            Directory.CreateDirectory(path);
        }

        if (string.IsNullOrEmpty(path)) return;

        Texture2D baked = BakeToTexture();
        if (baked == null) return;

        path = $"{path}/Tint_{manager.SceneData.SceneID}.png";

        File.WriteAllBytes(path, baked.EncodeToPNG());
        Object.DestroyImmediate(baked);

        AssetDatabase.ImportAsset(path);

        // Configure import
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer != null)
        {
            importer.isReadable = true;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled = false;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.sRGBTexture = false;
            importer.SaveAndReimport();
        }

        _data.bakedTintMap = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        MarkDirty();

        ApplyBakedToMaterial();

        Debug.Log($"[TintTab] Created tint map: {path}");
    }

    private Texture2D BakeToTexture()
    {
        // If we have RT, read from it
        if (_livePreviewRT != null && _livePreviewRT.IsCreated())
        {
            RenderTexture prev = RenderTexture.active;
            RenderTexture.active = _livePreviewRT;

            var tex = new Texture2D(_livePreviewRT.width, _livePreviewRT.height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, _livePreviewRT.width, _livePreviewRT.height), 0, 0);
            tex.Apply();

            RenderTexture.active = prev;
            return tex;
        }

        // CPU fallback
        return BakeCPU();
    }

    private Texture2D BakeCPU()
    {
        Vector2Int size = GetOutputSize();
        var result = new Texture2D(size.x, size.y, TextureFormat.RGB24, false);

        Color[] pixels = new Color[size.x * size.y];
        for (int i = 0; i < pixels.Length; i++)
            pixels[i] = Color.white;

        foreach (var layer in _data.layers)
        {
            // Use EffectiveTexture to handle PPM cached textures
            var layerTex = layer.EffectiveTexture;
            if (!layer.active || layer.opacity <= 0f || layerTex == null)
                continue;

            for (int y = 0; y < size.y; y++)
            {
                float v = (float)y / (size.y - 1);
                for (int x = 0; x < size.x; x++)
                {
                    float u = (float)x / (size.x - 1);
                    int idx = y * size.x + x;

                    Color layerColor = layerTex.GetPixelBilinear(u, v);

                    // Skip white (no tint effect)
                    if (layerColor.r >= 0.999f && layerColor.g >= 0.999f && layerColor.b >= 0.999f)
                        continue;

                    // Apply opacity
                    layerColor = Color.Lerp(Color.white, layerColor, layer.opacity);

                    // Apply blend mode
                    pixels[idx] = ApplyBlendMode(pixels[idx], layerColor, layer.blendMode);
                }
            }
        }

        result.SetPixels(pixels);
        result.Apply();
        return result;
    }

    private Color ApplyBlendMode(Color baseColor, Color layerColor, TerrainTintData.BlendMode mode)
    {
        switch (mode)
        {
            case TerrainTintData.BlendMode.Multiply:
                return new Color(
                    baseColor.r * layerColor.r,
                    baseColor.g * layerColor.g,
                    baseColor.b * layerColor.b);

            case TerrainTintData.BlendMode.Screen:
                return new Color(
                    1f - (1f - baseColor.r) * (1f - layerColor.r),
                    1f - (1f - baseColor.g) * (1f - layerColor.g),
                    1f - (1f - baseColor.b) * (1f - layerColor.b));

            case TerrainTintData.BlendMode.Overlay:
                return new Color(
                    BlendOverlay(baseColor.r, layerColor.r),
                    BlendOverlay(baseColor.g, layerColor.g),
                    BlendOverlay(baseColor.b, layerColor.b));

            case TerrainTintData.BlendMode.SoftLight:
                return new Color(
                    BlendSoftLight(baseColor.r, layerColor.r),
                    BlendSoftLight(baseColor.g, layerColor.g),
                    BlendSoftLight(baseColor.b, layerColor.b));

            case TerrainTintData.BlendMode.Normal:
                return layerColor;

            default:
                return new Color(
                    baseColor.r * layerColor.r,
                    baseColor.g * layerColor.g,
                    baseColor.b * layerColor.b);
        }
    }

    private float BlendOverlay(float b, float l)
    {
        return b < 0.5f ? (2f * b * l) : (1f - 2f * (1f - b) * (1f - l));
    }

    private float BlendSoftLight(float b, float l)
    {
        float d = b <= 0.25f ? ((16f * b - 12f) * b + 4f) * b : Mathf.Sqrt(b);
        return l < 0.5f ? b - (1f - 2f * l) * b * (1f - b) : b + (2f * l - 1f) * (d - b);
    }

    private void ApplyBakedToMaterial()
    {
        if (_data.bakedTintMap == null || _terrain?.materialTemplate == null)
            return;

        var mat = _terrain.materialTemplate;
        if (mat.HasProperty(_data.shaderProperty))
        {
            mat.SetTexture(_data.shaderProperty, _data.bakedTintMap);
            Debug.Log($"[TintTab] Applied baked tint to {_data.shaderProperty}");
        }

        UpdateOverlayScale();
    }

    
    private void ExportToPPM()
    {
        if (!_data.HasLayers) return;

        string savePath = manager.TintExportPath;
        if (string.IsNullOrEmpty(savePath)) return;

        try
        {
            EditorUtility.DisplayProgressBar("Exporting PPM", "Baking...", 0.3f);

            Texture2D baked = BakeToTexture();
            if (baked == null) return;

            EditorUtility.DisplayProgressBar("Exporting PPM", "Converting...", 0.6f);

            // Single call handles all M&B coordinate conversion
            Color[,] ppmData = PpmHelper.TextureToMBTerrainPpm(baked, convertToSRGB: false);
            Object.DestroyImmediate(baked);

            EditorUtility.DisplayProgressBar("Exporting PPM", "Writing...", 0.9f);
            PpmHelper.WritePpm(ppmData, savePath);

            EditorUtility.ClearProgressBar();
            Debug.Log($"[TintTab] Exported: {savePath}");
        }
        catch (Exception e)
        {
            EditorUtility.ClearProgressBar();
            Debug.LogError($"[TintTab] Export error: {e}");
        }
    }

    #endregion

    #region Resolution Sync

    private void RefreshGeneratorResolution()
    {
        _hasGeneratorRes = false;
        _generatorResX = 0;
        _generatorResY = 0;

        if (manager?.HeightmapGenerator == null) return;

        string hash = manager.GetCurrentTerrainHash();
        if (string.IsNullOrEmpty(hash)) return;

        var data = MBTerrainGeneratorHelpers.ParseTerrainCode(hash);
        if (data == null) return;

        var geo = MBTerrainGeneratorHelpers.CalculateTerrainGeometryData(
            data.SizeX, data.SizeY, data.PolygonSize);

        _generatorResX = geo.NumVerticesX;
        _generatorResY = geo.NumVerticesY;
        _hasGeneratorRes = true;
    }

    private void ResizeTexture(Texture2D tex, int newW, int newH)
    {
        if (tex == null) return;

        // Ensure readable
        string path = AssetDatabase.GetAssetPath(tex);
        if (!string.IsNullOrEmpty(path))
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer != null && !importer.isReadable)
            {
                importer.isReadable = true;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.npotScale = TextureImporterNPOTScale.None;
                importer.SaveAndReimport();
            }
        }

        // Resize via RT
        var rt = RenderTexture.GetTemporary(newW, newH, 0, RenderTextureFormat.ARGB32);
        rt.filterMode = FilterMode.Bilinear;

        RenderTexture.active = rt;
        Graphics.Blit(tex, rt);

        var temp = new Texture2D(newW, newH, TextureFormat.RGB24, false);
        temp.ReadPixels(new Rect(0, 0, newW, newH), 0, 0);
        temp.Apply();

        RenderTexture.active = null;
        RenderTexture.ReleaseTemporary(rt);

        tex.Reinitialize(newW, newH, TextureFormat.RGB24, false);
        tex.SetPixels(temp.GetPixels());
        tex.Apply();

        Object.DestroyImmediate(temp);

        // Save
        SaveTexture(tex);
    }

    #endregion

    #region Utilities

    private void MarkDirty()
    {
        if (_data != null)
            EditorUtility.SetDirty(_data);
    }

    private void SaveActiveTexture()
    {
        var layer = _data?.ActiveLayer;
        if (layer?.texture != null && layer.type == TerrainTintData.LayerType.Paint)
        {
            SaveTexture(layer.texture);
            layer.isDirty = false;
        }
    }

    private void SaveTexture(Texture2D tex)
    {
        if (tex == null) return;

        string path = AssetDatabase.GetAssetPath(tex);
        if (!string.IsNullOrEmpty(path))
        {
            File.WriteAllBytes(path, tex.EncodeToPNG());
            // dont reimport during painting - too slow
        }
    }

    private void FillTexture(Texture2D tex, Color color)
    {
        if (tex == null) return;

        Color[] pixels = new Color[tex.width * tex.height];
        for (int i = 0; i < pixels.Length; i++)
            pixels[i] = color;

        tex.SetPixels(pixels);
        tex.Apply();
    }

    private void DrawNoDataWarning()
    {
        EditorGUILayout.HelpBox(
            "TerrainTintData not found.\nAdd this component to store tint layers.",
            MessageType.Warning);

        EditorGUILayout.Space(10);

        GUI.backgroundColor = new Color(0.6f, 0.9f, 0.6f);
        if (GUILayout.Button("Add TerrainTintData", GUILayout.Height(30)))
        {
            Undo.AddComponent<TerrainTintData>(manager.Terrain.gameObject);
            manager.RefreshComponents();
            RefreshReferences();
        }

        GUI.backgroundColor = Color.white;
    }

    private void UpdateOverlayScale()
    {
        if (_terrain == null || _terrain.materialTemplate == null) return;

        var mat = _terrain.materialTemplate;
        Vector3 terrainSize = _terrain.terrainData.size;

        mat.SetFloat("_OverlayScaleX", 1.0f / terrainSize.x);
        mat.SetFloat("_OverlayScaleZ", 1.0f / terrainSize.z);
    }

    #endregion
}