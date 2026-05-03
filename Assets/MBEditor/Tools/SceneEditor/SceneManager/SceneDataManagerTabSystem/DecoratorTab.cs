using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditorInternal;
using BDT.GUI.Helpers;

/// <summary>
/// Decorator tab: Splatmap decoration with texture layers and rules.
/// Integrates MBTerrainDecorator functionality into the Scene Data Manager.
/// 
/// Features:
/// - Texture-only layers (no tree functionality in UI)
/// - Fully clickable headers for expand/collapse
/// - Per-layer rule expand/collapse buttons
/// - Color-coded visual design
/// - Slope algorithm settings
/// - Curvature algorithm settings (Standard, Profile, Plan)
/// </summary>
public class DecoratorTab : SceneDataManagerTabBase
{
    public override string TabName => "Decorator";
    public override int Order => 7;
    
    #region Visual Constants
    
    private static readonly Color TextureLayerColor = new Color(0.4f, 0.65f, 0.9f);
    private static readonly Color InvalidLayerColor = new Color(0.9f, 0.4f, 0.4f);
    private static readonly Color DisabledLayerColor = new Color(0.5f, 0.5f, 0.5f);
    
    private static readonly Color BaseBlockColor = new Color(0.3f, 0.55f, 0.8f);
    private static readonly Color OverlayBlockColor = new Color(0.9f, 0.6f, 0.2f);
    private static readonly Color VegetationBlockColor = new Color(0.4f, 0.8f, 0.4f);
    
    private static readonly Color HeightRuleColor = new Color(0.6f, 0.8f, 1f);
    private static readonly Color SlopeRuleColor = new Color(1f, 0.8f, 0.6f);
    private static readonly Color CurvatureRuleColor = new Color(0.9f, 0.6f, 0.9f);  // Purple-ish for curvature
    private static readonly Color WaterLevelRuleColor = new Color(0.3f, 0.65f, 0.95f); // Blue for water
    private static readonly Color NoiseRuleColor = new Color(0.8f, 0.6f, 1f);
    private static readonly Color TextureRuleColor = new Color(0.6f, 1f, 0.8f);
    private static readonly Color LayerRuleColor = new Color(1f, 0.9f, 0.6f);
    private static readonly Color GeneratorRuleColor = new Color(1f, 0.7f, 0.7f);
    private static readonly Color PGMRuleColor = new Color(0.7f, 0.7f, 1f);
    
    private const float LayerHeaderHeight = 32f;
    private const float RuleHeaderHeight = 24f;
    private const float TexturePreviewSize = 28f;
    private const float BadgeHeight = 16f;
    private const float IndentGuideWidth = 3f;
    
    #endregion
    
    #region State
    
    private enum DecoratorSubTab { Layers, Settings }
    private DecoratorSubTab currentSubTab = DecoratorSubTab.Layers;
    
    private List<bool> layerFoldouts = new List<bool>();
    private List<List<bool>> ruleFoldouts = new List<List<bool>>();
    
    private bool showSettings = true;
    private bool showSlopeSettings = true;
    private bool showCurvatureSettings = true;
    private bool showWaterSettings = true;
    private bool showJobSettings = true;
    
    private string[] textureChoices;
    private int layerCount;
    private double lastDecorateTime = 0;
    
    private Dictionary<int, Texture2D> layerTextureCache = new Dictionary<int, Texture2D>();
    
    private GUIStyle _badgeStyle;
    private GUIStyle _miniLabelCentered;
    private GUIStyle _clickableHeaderStyle;
    
    #endregion
    
    #region Presets
    
    private static readonly Dictionary<string, PresetData> presets = new Dictionary<string, PresetData>
    {
        { "M&B Default", new PresetData { normMode = MBTerrainDecorator.NormalizationMode.MBNormalized, falloff = 2f, perlinOctaves = 5, threshold = 0.01f } },
        { "Unity Standard", new PresetData { normMode = MBTerrainDecorator.NormalizationMode.SimpleNormalize, falloff = 5f, perlinOctaves = 4, threshold = 0.001f } },
        { "Sharp Edges", new PresetData { normMode = MBTerrainDecorator.NormalizationMode.MBNormalized, falloff = 0.5f, perlinOctaves = 3, threshold = 0.05f } },
        { "Soft Blend", new PresetData { normMode = MBTerrainDecorator.NormalizationMode.SimpleNormalize, falloff = 10f, perlinOctaves = 6, threshold = 0.001f } }
    };
    
    private struct PresetData
    {
        public MBTerrainDecorator.NormalizationMode normMode;
        public float falloff;
        public int perlinOctaves;
        public float threshold;
    }
    
    #endregion
    
    private MBTerrainDecorator Decorator => manager?.Decorator;
    
    #region Initialization
    
    public override void OnEnable(MBSceneDataManager manager, SerializedObject serializedObject)
    {
        base.OnEnable(manager, serializedObject);
        RefreshLayerChoices();
        SyncFoldoutLists();
    }
    
    public override void OnManagerChanged(MBSceneDataManager manager)
    {
        base.OnManagerChanged(manager);
        RefreshLayerChoices();
        SyncFoldoutLists();
        layerTextureCache.Clear();
    }
    
    public override void OnDisable()
    {
        base.OnDisable();
        layerTextureCache.Clear();
    }
    
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
    
    #endregion
    
    #region Main Draw
    
    public override void DrawTab()
    {
        EnsureStyles();
        
        if (Decorator == null)
        {
            DrawNoDecoratorWarning();
            return;
        }
        
        RefreshLayerChoices();
        DrawSubTabBar();
        EditorGUILayout.Space(10);
        
        switch (currentSubTab)
        {
            case DecoratorSubTab.Layers:
                DrawLayersTab();
                break;
            case DecoratorSubTab.Settings:
                DrawSettingsTab();
                break;
        }
    }
    
    private void DrawSubTabBar()
    {
        EditorGUILayout.BeginHorizontal();
        
        string[] subTabNames = { "Layers", "Settings" };
        Color[] subTabColors = { UIColors.VividCerulean, UIColors.Orange };
        
        for (int i = 0; i < subTabNames.Length; i++)
        {
            bool isSelected = (int)currentSubTab == i;
            GUI.backgroundColor = isSelected ? subTabColors[i] : Color.white;
            
            if (GUILayout.Button(subTabNames[i], isSelected ? EditorStyles.toolbarButton : EditorStyles.miniButton, GUILayout.Height(22)))
            {
                currentSubTab = (DecoratorSubTab)i;
            }
        }
        
        GUI.backgroundColor = Color.white;
        EditorGUILayout.EndHorizontal();
    }
    
    #endregion
    
    #region Layers Tab
    
    private void DrawLayersTab()
    {
        DecoratorTabPresetExtension.DrawPresetSection(Decorator);
        RGLDecoratorStatusUI.DrawRGLStatus(Decorator);
        
        EditorGUILayout.Space(5);
        UIHelpers.DrawUILine(UIColors.GrayLine, 1);
        EditorGUILayout.Space(5);
        
        DrawLayersActionBar();
        
        EditorGUILayout.Space(5);
        UIHelpers.DrawUILine(UIColors.GrayLine, 2);
        EditorGUILayout.Space(5);
        
        SyncFoldoutLists();
        
        // Split layers by block
        var baseLayers = new List<int>();
        var overlayLayers = new List<int>();
        for (int i = 0; i < Decorator.layers.Count; i++)
        {
            if (Decorator.layers[i].block == MBTerrainDecorator.LayerBlock.Overlay)
                overlayLayers.Add(i);
            else
                baseLayers.Add(i);
        }
        
        DrawBlockHeader("BASE", "Engine-generated layers (rebuilt on regeneration)", BaseBlockColor, baseLayers.Count, true);
        
        if (baseLayers.Count > 0)
        {
            for (int bi = 0; bi < baseLayers.Count; bi++)
            {
                DrawLayerEntry(baseLayers[bi], isBaseLocked: true);
                EditorGUILayout.Space(2);
            }
        }
        else
        {
            EditorGUILayout.HelpBox("No base layers. Run regeneration to create them from terrain hash.", MessageType.None);
        }
        
        EditorGUILayout.Space(8);
        
        DrawBlockHeader("OVERLAY", "Hand-painted layers (persist across regeneration)", OverlayBlockColor, overlayLayers.Count, false);
        
        if (overlayLayers.Count > 0)
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(12);
            bool newRenorm = EditorGUILayout.Toggle(
                new GUIContent("Renormalize", "Renormalize splatmap after overlay compositing"),
                Decorator.renormalizeAfterOverlay);
            if (newRenorm != Decorator.renormalizeAfterOverlay)
            {
                Undo.RecordObject(Decorator, "Toggle Renormalize");
                Decorator.renormalizeAfterOverlay = newRenorm;
                EditorUtility.SetDirty(Decorator);
            }
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space(4);
            
            for (int oi = 0; oi < overlayLayers.Count; oi++)
            {
                DrawLayerEntry(overlayLayers[oi], isBaseLocked: false);
                EditorGUILayout.Space(2);
            }
        }
        else
        {
            EditorGUILayout.HelpBox("No overlay layers. Click '+ Overlay' to add custom painting layers.", MessageType.None);
        }
        
        EditorGUILayout.Space(8);
        
        DrawVegetationMaskSection();
        
        if (Decorator.layers.Count == 0)
            EditorGUILayout.HelpBox("No layers. Run regeneration to create base layers, or add overlay layers manually.", MessageType.Info);
    }
    
    private void DrawBlockHeader(string title, string subtitle, Color color, int count, bool isLocked)
    {
        Rect rect = EditorGUILayout.GetControlRect(GUILayout.Height(28));
        EditorGUI.DrawRect(rect, color * 0.2f);
        EditorGUI.DrawRect(new Rect(rect.x, rect.y, 4, rect.height), color);
        EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, 1), color * 0.6f);
        
        float x = rect.x + 10;
        GUI.color = color;
        EditorGUI.LabelField(new Rect(x, rect.y + 4, 80, 20), title, EditorStyles.boldLabel);
        GUI.color = Color.white;
        x += 70;
        
        if (isLocked)
        {
            DrawBadge(new Rect(x, rect.y + 7, 16, 14), "🔒", color * 0.5f);
            x += 20;
        }
        
        GUI.color = new Color(1, 1, 1, 0.5f);
        EditorGUI.LabelField(new Rect(x, rect.y + 6, 300, 16), subtitle, EditorStyles.miniLabel);
        GUI.color = Color.white;
        
        DrawBadge(new Rect(rect.xMax - 50, rect.y + 6, 40, 16), $"{count}L", count > 0 ? color : DisabledLayerColor);
    }
    
    private void DrawVegetationMaskSection()
    {
        // Header
        Rect rect = EditorGUILayout.GetControlRect(GUILayout.Height(28));
        EditorGUI.DrawRect(rect, VegetationBlockColor * 0.2f);
        EditorGUI.DrawRect(new Rect(rect.x, rect.y, 4, rect.height), VegetationBlockColor);
        EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, 1), VegetationBlockColor * 0.6f);
        
        float hx = rect.x + 10;
        GUI.color = VegetationBlockColor;
        EditorGUI.LabelField(new Rect(hx, rect.y + 4, 120, 20), "VEGETATION MASK", EditorStyles.boldLabel);
        GUI.color = Color.white;
        hx += 130;
        
        GUI.color = new Color(1, 1, 1, 0.5f);
        EditorGUI.LabelField(new Rect(hx, rect.y + 6, 300, 16), "Global mask - punches through overlay to reveal base", EditorStyles.miniLabel);
        GUI.color = Color.white;
        
        bool hasVeg = Decorator.vegetationMaskTexture != null;
        DrawBadge(new Rect(rect.xMax - 50, rect.y + 6, 40, 16), hasVeg ? "ON" : "OFF", hasVeg ? VegetationBlockColor : DisabledLayerColor);
        
        // Content
        EditorGUILayout.BeginHorizontal();
        GUILayout.Space(8);
        
        Color prevBg = GUI.backgroundColor;
        GUI.backgroundColor = VegetationBlockColor * 0.3f;
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        GUI.backgroundColor = prevBg;
        
        EditorGUILayout.Space(2);
        
        // Mask texture
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Mask Texture", GUILayout.Width(90));
        var newTex = (Texture2D)EditorGUILayout.ObjectField(Decorator.vegetationMaskTexture, typeof(Texture2D), false);
        if (newTex != Decorator.vegetationMaskTexture)
        {
            Undo.RecordObject(Decorator, "Change Vegetation Mask");
            Decorator.vegetationMaskTexture = newTex;
            EditorUtility.SetDirty(Decorator);
        }
        EditorGUILayout.EndHorizontal();
        
        if (Decorator.vegetationMaskTexture != null)
        {
            // Channel
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Channel", GUILayout.Width(90));
            var newChannel = (MBTerrainDecorator.ImageChannel)EditorGUILayout.EnumPopup(Decorator.vegetationMaskChannel);
            if (newChannel != Decorator.vegetationMaskChannel)
            {
                Undo.RecordObject(Decorator, "Change Veg Mask Channel");
                Decorator.vegetationMaskChannel = newChannel;
                EditorUtility.SetDirty(Decorator);
            }
            EditorGUILayout.EndHorizontal();
            
            // Strength
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Strength", GUILayout.Width(90));
            float newStr = EditorGUILayout.Slider(Decorator.vegetationMaskStrength, 0f, 1f);
            if (!Mathf.Approximately(newStr, Decorator.vegetationMaskStrength))
            {
                Undo.RecordObject(Decorator, "Change Veg Mask Strength");
                Decorator.vegetationMaskStrength = newStr;
                EditorUtility.SetDirty(Decorator);
            }
            EditorGUILayout.EndHorizontal();
            
            // Invert
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Invert", GUILayout.Width(90));
            bool newInvert = EditorGUILayout.Toggle(Decorator.vegetationMaskInvert);
            if (newInvert != Decorator.vegetationMaskInvert)
            {
                Undo.RecordObject(Decorator, "Toggle Veg Mask Invert");
                Decorator.vegetationMaskInvert = newInvert;
                EditorUtility.SetDirty(Decorator);
            }
            EditorGUILayout.EndHorizontal();
            
            // Texture preview
            EditorGUILayout.Space(2);
            Rect previewRect = EditorGUILayout.GetControlRect(GUILayout.Height(64));
            previewRect.width = 64;
            previewRect.x += 90;
            GUI.DrawTexture(previewRect, Decorator.vegetationMaskTexture, ScaleMode.ScaleToFit);
        }
        else
        {
            EditorGUILayout.HelpBox("Assign a texture to mask overlay layers. White = base shows through, Black = overlay intact.", MessageType.None);
        }
        
        EditorGUILayout.Space(2);
        EditorGUILayout.EndVertical();
        EditorGUILayout.EndHorizontal();
    }
    
    private void DrawLayersActionBar()
    {
        EditorGUILayout.BeginHorizontal();
        
        GUI.backgroundColor = UIColors.Green;
        GUI.enabled = !Decorator.calculating;
        if (GUILayout.Button("▶ Decorate", GUILayout.Height(28), GUILayout.Width(90)))
            DecorateWithUndo();
        GUI.enabled = true;
        GUI.backgroundColor = Color.white;
        
        GUI.backgroundColor = UIColors.Rose;
        if (GUILayout.Button("↺ Reset", GUILayout.Height(28), GUILayout.Width(70)))
        {
            if (EditorUtility.DisplayDialog("Reset Terrain", "Reset all splatmaps to default?", "Reset", "Cancel"))
                ResetTerrain();
        }
        GUI.backgroundColor = Color.white;
        
        GUILayout.FlexibleSpace();
        
        if (lastDecorateTime > 0)
        {
            EditorGUILayout.LabelField($"{lastDecorateTime:F2}s", EditorStyles.miniLabel, GUILayout.Width(45));
        }
        
        GUI.backgroundColor = OverlayBlockColor;
        if (GUILayout.Button("+ Overlay", GUILayout.Width(80), GUILayout.Height(28)))
            AddNewOverlayLayer();
        GUI.backgroundColor = Color.white;
        
        if (GUILayout.Button("⊞", GUILayout.Width(28), GUILayout.Height(28)))
        {
            for (int i = 0; i < layerFoldouts.Count; i++)
                layerFoldouts[i] = true;
        }
        
        if (GUILayout.Button("⊟", GUILayout.Width(28), GUILayout.Height(28)))
        {
            for (int i = 0; i < layerFoldouts.Count; i++)
                layerFoldouts[i] = false;
        }
        
        EditorGUILayout.EndHorizontal();
        
        // Progress bar when calculating
        if (Decorator.calculating)
        {
            EditorGUILayout.Space(5);
            Rect progressRect = EditorGUILayout.GetControlRect(GUILayout.Height(20));
            EditorGUI.ProgressBar(progressRect, Decorator.calculatingPercent, $"Decorating... {Decorator.calculatingPercent * 100:F0}%");
        }
    }
    
    private void DrawLayerEntry(int index, bool isBaseLocked = false)
    {
        if (index >= Decorator.layers.Count || index >= layerFoldouts.Count) return;
        
        var layer = Decorator.layers[index];
        bool isInvalid = layer.layerIndex < 0 || layer.layerIndex >= layerCount;
        bool isOverlay = layer.block == MBTerrainDecorator.LayerBlock.Overlay;
        Color layerColor = isInvalid ? InvalidLayerColor : (!layer.active ? DisabledLayerColor : (isOverlay ? OverlayBlockColor : BaseBlockColor));
        
        EditorGUILayout.BeginVertical();
        
        Rect headerRect = EditorGUILayout.GetControlRect(GUILayout.Height(LayerHeaderHeight));
        if (DrawClickableLayerHeader(headerRect, layer, index, layerColor, isInvalid))
            layerFoldouts[index] = !layerFoldouts[index];
        
        if (layerFoldouts[index])
            DrawLayerContent(layer, index, layerColor);

        // TODO: draw texture preview 
        // DrawTexturePreview();
        
        EditorGUILayout.EndVertical();
    }
    
    private bool DrawClickableLayerHeader(Rect rect, MBTerrainDecorator.Layers layer, int index, Color layerColor, bool isInvalid)
    {
        bool clicked = false;
        
        Color bgColor = layerFoldouts[index] ? layerColor * 0.25f : layerColor * 0.15f;
        EditorGUI.DrawRect(rect, bgColor);
        
        float barWidth = layerFoldouts[index] ? 6 : 4;
        EditorGUI.DrawRect(new Rect(rect.x, rect.y, barWidth, rect.height), layer.active ? layerColor : DisabledLayerColor);
        
        if (layerFoldouts[index])
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, 2), layerColor);
        
        float x = rect.x + barWidth + 4;
        
        Rect toggleRect = new Rect(x, rect.y + 6, 20, 20);
        EditorGUI.BeginChangeCheck();
        bool newActive = EditorGUI.Toggle(toggleRect, layer.active);
        if (EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(Decorator, "Toggle Layer Active");
            layer.active = newActive;
            EditorUtility.SetDirty(Decorator);
        }
        x += 24;
        
        if (!isInvalid)
        {
            Rect previewRect = new Rect(x, rect.y + 2, TexturePreviewSize, TexturePreviewSize);
            Texture2D preview = GetLayerTexturePreview(layer.layerIndex);
            if (preview != null)
                GUI.DrawTexture(previewRect, preview, ScaleMode.ScaleToFit);
            else
                EditorGUI.DrawRect(previewRect, new Color(0.3f, 0.3f, 0.3f));
            x += TexturePreviewSize + 4;
        }
        
        DrawBadge(new Rect(x, rect.y + 8, 28, 16), "TEX", isInvalid ? InvalidLayerColor : TextureLayerColor);
        x += 32;
        
        // Block badge
        bool isOverlayLayer = layer.block == MBTerrainDecorator.LayerBlock.Overlay;
        Color blockBadgeColor = isOverlayLayer ? OverlayBlockColor : BaseBlockColor;
        string blockBadgeText;
        float blockBadgeWidth;
        if (!isOverlayLayer)
        {
            blockBadgeText = "BASE";
            blockBadgeWidth = 36;
        }
        else if (layer.isUserCreated)
        {
            blockBadgeText = "USER";
            blockBadgeWidth = 36;
        }
        else
        {
            blockBadgeText = "OVR";
            blockBadgeWidth = 30;
        }
        DrawBadge(new Rect(x, rect.y + 8, blockBadgeWidth, 16), blockBadgeText, layer.active ? blockBadgeColor : DisabledLayerColor);
        x += (blockBadgeWidth + 4);
        
        float buttonsWidth = 120;
        Rect clickableArea = new Rect(x, rect.y, rect.xMax - x - buttonsWidth, rect.height);
        
        if (!layer.active) GUI.color = new Color(1, 1, 1, 0.5f);
        string indicator = layerFoldouts[index] ? "▼" : "▶";
        string displayName = layer.active ? $"{indicator} {layer.name}" : $"{indicator} [OFF] {layer.name}";
        EditorGUI.LabelField(new Rect(x, rect.y + 6, clickableArea.width - 10, 20), displayName, _clickableHeaderStyle);
        GUI.color = Color.white;
        
        if (Event.current.type == EventType.MouseDown && clickableArea.Contains(Event.current.mousePosition))
        {
            clicked = true;
            Event.current.Use();
        }
        EditorGUIUtility.AddCursorRect(clickableArea, MouseCursor.Link);
        
        float rightX = rect.xMax - 120;
        
        int activeRules = layer.rules.Count(r => r.active);
        DrawBadge(new Rect(rightX, rect.y + 8, 50, BadgeHeight), $"{activeRules}R", activeRules > 0 ? UIColors.Cyan : DisabledLayerColor);
        rightX += 54;
        
        GUI.enabled = index > 0;
        if (GUI.Button(new Rect(rightX, rect.y + 6, 20, 20), "↑"))
            SwapLayers(index, index - 1);
        rightX += 22;
        
        GUI.enabled = index < Decorator.layers.Count - 1;
        if (GUI.Button(new Rect(rightX, rect.y + 6, 20, 20), "↓"))
            SwapLayers(index, index + 1);
        GUI.enabled = true;
        rightX += 22;
        
        GUI.backgroundColor = InvalidLayerColor;
        if (GUI.Button(new Rect(rightX, rect.y + 6, 20, 20), "×"))
        {
            if (EditorUtility.DisplayDialog("Remove Layer", $"Remove layer '{layer.name}'?", "Yes", "No"))
                RemoveLayer(index);
        }
        GUI.backgroundColor = Color.white;
        
        return clicked;
    }
    
    private void DrawLayerContent(MBTerrainDecorator.Layers layer, int layerIndex, Color layerColor)
    {
        bool isOverlay = layer.block == MBTerrainDecorator.LayerBlock.Overlay;
        bool isBaseLocked = !isOverlay;
        bool isAutoOverlay = isOverlay && !layer.isUserCreated;
        
        EditorGUILayout.BeginHorizontal();
        
        Rect guideRect = EditorGUILayout.GetControlRect(GUILayout.Width(IndentGuideWidth), GUILayout.ExpandHeight(true));
        
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.Space(3);
        
        // Base layers show info as read-only
        if (isBaseLocked)
        {
            GUI.enabled = false;
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Name", GUILayout.Width(80));
            EditorGUILayout.TextField(layer.name);
            EditorGUILayout.EndHorizontal();
            
            if (textureChoices != null && textureChoices.Length > 0)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Target", GUILayout.Width(80));
                int currentIndex = Mathf.Clamp(layer.layerIndex, 0, textureChoices.Length - 1);
                EditorGUILayout.Popup(currentIndex, textureChoices);
                EditorGUILayout.EndHorizontal();
            }
            GUI.enabled = true;
            
            EditorGUILayout.HelpBox("Base layer - auto-generated by regenerator. Cannot be edited or deleted.", MessageType.None);
        }
        else
        {
            // Overlay layers are editable
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Name", GUILayout.Width(80));
            string newName = EditorGUILayout.TextField(layer.name);
            if (newName != layer.name)
            {
                Undo.RecordObject(Decorator, "Rename Layer");
                layer.name = newName;
                EditorUtility.SetDirty(Decorator);
            }
            EditorGUILayout.EndHorizontal();
            
            if (textureChoices != null && textureChoices.Length > 0)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Target", GUILayout.Width(80));
                int currentIndex = Mathf.Clamp(layer.layerIndex, 0, textureChoices.Length - 1);
                int newIndex = EditorGUILayout.Popup(currentIndex, textureChoices);
                if (newIndex != layer.layerIndex)
                {
                    Undo.RecordObject(Decorator, "Change Target Layer");
                    layer.layerIndex = newIndex;
                    EditorUtility.SetDirty(Decorator);
                }
                EditorGUILayout.EndHorizontal();
            }
            
            // Auto-generated overlay hint
            if (isAutoOverlay)
            {
                EditorGUILayout.HelpBox("Auto-generated overlay - rebuilt on regeneration. Edits to rules/settings are preserved until next regeneration.", MessageType.None);
            }
            
            // Overlay-specific settings
            EditorGUILayout.Space(3);
            Color prevBg = GUI.backgroundColor;
            GUI.backgroundColor = OverlayBlockColor * 0.3f;
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            GUI.backgroundColor = prevBg;
            
            EditorGUILayout.LabelField("Overlay Settings", EditorStyles.miniBoldLabel);
            
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Blend Mode", GUILayout.Width(80));
            var newBlendMode = (MBTerrainDecorator.OverlayBlendMode)EditorGUILayout.EnumPopup(layer.overlayBlendMode);
            if (newBlendMode != layer.overlayBlendMode)
            {
                Undo.RecordObject(Decorator, "Change Overlay Blend");
                layer.overlayBlendMode = newBlendMode;
                EditorUtility.SetDirty(Decorator);
            }
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Opacity", GUILayout.Width(80));
            float newOpacity = EditorGUILayout.Slider(layer.overlayOpacity, 0f, 1f);
            if (!Mathf.Approximately(newOpacity, layer.overlayOpacity))
            {
                Undo.RecordObject(Decorator, "Change Overlay Opacity");
                layer.overlayOpacity = newOpacity;
                EditorUtility.SetDirty(Decorator);
            }
            EditorGUILayout.EndHorizontal();
            
            string blendHelp = layer.overlayBlendMode switch
            {
                MBTerrainDecorator.OverlayBlendMode.Additive => "Adds weight to target, then renormalizes.",
                MBTerrainDecorator.OverlayBlendMode.Lerp => "Fades from base toward full overlay.",
                MBTerrainDecorator.OverlayBlendMode.Replace => "Replaces base at overlay strength.",
                MBTerrainDecorator.OverlayBlendMode.PainterOcclusion => "M&B painter: overlay occludes base.",
                _ => ""
            };
            EditorGUILayout.HelpBox(blendHelp, MessageType.None);
            EditorGUILayout.EndVertical();
        }
        
        EditorGUILayout.Space(5);
        
        // Rules section - base layers show rules read-only, overlay layers are editable
        if (isBaseLocked)
            GUI.enabled = false;
        
        DrawRulesSection(layer, layerIndex);
        
        if (isBaseLocked)
            GUI.enabled = true;
        
        EditorGUILayout.EndVertical();
        EditorGUILayout.EndHorizontal();
        
        if (Event.current.type == EventType.Repaint)
            EditorGUI.DrawRect(new Rect(guideRect.x + 2, guideRect.y, IndentGuideWidth, guideRect.height), layerColor * 0.6f);
    }
    
    #endregion
    
    #region Rules Section
    
    private void DrawRulesSection(MBTerrainDecorator.Layers layer, int layerIndex)
    {
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField($"Rules ({layer.rules.Count})", EditorStyles.miniBoldLabel);
        
        GUILayout.FlexibleSpace();
        
        DrawRuleTypeSummary(layer);
        
        if (GUILayout.Button("⊞", GUILayout.Width(22), GUILayout.Height(18)))
        {
            EnsureRuleFoldouts(layerIndex, layer.rules.Count);
            for (int i = 0; i < ruleFoldouts[layerIndex].Count; i++)
                ruleFoldouts[layerIndex][i] = true;
        }
        
        if (GUILayout.Button("⊟", GUILayout.Width(22), GUILayout.Height(18)))
        {
            EnsureRuleFoldouts(layerIndex, layer.rules.Count);
            for (int i = 0; i < ruleFoldouts[layerIndex].Count; i++)
                ruleFoldouts[layerIndex][i] = false;
        }
        
        GUI.backgroundColor = UIColors.Cyan;
        if (GUILayout.Button("+", GUILayout.Width(24), GUILayout.Height(18)))
        {
            AddNewRule(layer);
            EnsureRuleFoldouts(layerIndex, layer.rules.Count);
        }
        GUI.backgroundColor = Color.white;
        
        EditorGUILayout.EndHorizontal();
        
        EnsureRuleFoldouts(layerIndex, layer.rules.Count);
        
        for (int i = 0; i < layer.rules.Count; i++)
            DrawRuleEntry(layer, layerIndex, i);
        
        if (layer.rules.Count == 0)
            EditorGUILayout.HelpBox("No rules. Add rules to define where this layer appears.", MessageType.None);
    }
    
    private void DrawRuleTypeSummary(MBTerrainDecorator.Layers layer)
    {
        var typeCounts = layer.rules.Where(r => r.active)
            .GroupBy(r => r.filter)
            .ToDictionary(g => g.Key, g => g.Count());
        
        foreach (var kvp in typeCounts)
        {
            Rect badgeRect = EditorGUILayout.GetControlRect(GUILayout.Width(28), GUILayout.Height(14));
            DrawBadge(badgeRect, GetRuleFilterShortName(kvp.Key), GetRuleFilterColor(kvp.Key) * 0.8f);
        }
    }
    
    private void DrawRuleEntry(MBTerrainDecorator.Layers layer, int layerIndex, int ruleIndex)
    {
        var rule = layer.rules[ruleIndex];
        Color ruleColor = GetRuleFilterColor(rule.filter);
        
        EditorGUILayout.BeginVertical();
        
        Rect headerRect = EditorGUILayout.GetControlRect(GUILayout.Height(RuleHeaderHeight));
        if (DrawClickableRuleHeader(headerRect, layer, layerIndex, ruleIndex, rule, ruleColor))
            ruleFoldouts[layerIndex][ruleIndex] = !ruleFoldouts[layerIndex][ruleIndex];
        
        if (ruleFoldouts[layerIndex][ruleIndex])
            DrawRuleContent(rule, ruleColor);
        
        EditorGUILayout.EndVertical();
    }
    
    private bool DrawClickableRuleHeader(Rect rect, MBTerrainDecorator.Layers layer, int layerIndex, 
        int ruleIndex, MBTerrainDecorator.Rules rule, Color ruleColor)
    {
        bool clicked = false;
        
        Color bgColor = rule.active ? ruleColor * 0.2f : new Color(0.3f, 0.3f, 0.3f, 0.2f);
        EditorGUI.DrawRect(rect, bgColor);
        EditorGUI.DrawRect(new Rect(rect.x, rect.y, 3, rect.height), rule.active ? ruleColor : DisabledLayerColor);
        
        float x = rect.x + 6;
        
        EditorGUI.BeginChangeCheck();
        bool newActive = EditorGUI.Toggle(new Rect(x, rect.y + 3, 18, 18), rule.active);
        if (EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(Decorator, "Toggle Rule Active");
            rule.active = newActive;
            EditorUtility.SetDirty(Decorator);
        }
        x += 20;
        
        DrawBadge(new Rect(x, rect.y + 4, 40, BadgeHeight), GetRuleFilterShortName(rule.filter), rule.active ? ruleColor : DisabledLayerColor);
        x += 44;
        
        if (!rule.active) GUI.color = new Color(1, 1, 1, 0.5f);
        
        // Show blend mode (greyed out for first rule)
        bool isFirstActiveRule = IsFirstActiveRule(layer, ruleIndex);
        if (isFirstActiveRule)
        {
            GUI.color = new Color(1, 1, 1, 0.35f);
            EditorGUI.LabelField(new Rect(x, rect.y + 4, 38, RuleHeaderHeight - 8), "normal", EditorStyles.miniLabel);
        }
        else
        {
            GUI.color = rule.active ? Color.white : new Color(1, 1, 1, 0.5f);
            EditorGUI.LabelField(new Rect(x, rect.y + 4, 30, RuleHeaderHeight - 8), rule.blend.ToString(), EditorStyles.miniLabel);
        }
        GUI.color = rule.active ? Color.white : new Color(1, 1, 1, 0.5f);
        x += 42;
        
        float buttonsWidth = 68;
        Rect clickableArea = new Rect(x, rect.y, rect.xMax - x - buttonsWidth-DecoratorClipboard.GetRuleButtonsWidth(), rect.height);
        
        string indicator = ruleFoldouts[layerIndex][ruleIndex] ? "▼" : "▶";
        EditorGUI.LabelField(new Rect(x, rect.y + 3, clickableArea.width - 10, RuleHeaderHeight - 6), 
            $"{indicator} {GetRuleSummary(rule)}", EditorStyles.miniLabel);
        GUI.color = Color.white;
        
        if (Event.current.type == EventType.MouseDown && clickableArea.Contains(Event.current.mousePosition))
        {
            clicked = true;
            Event.current.Use();
        }
        EditorGUIUtility.AddCursorRect(clickableArea, MouseCursor.Link);
        
        float rightX = rect.xMax - 68-DecoratorClipboard.GetRuleButtonsWidth();
        GUI.enabled = true;
        DecoratorClipboard.DrawRuleCopyPasteButtons(
            new Rect(rightX, rect.y, DecoratorClipboard.GetRuleButtonsWidth()+4, 18),
            Decorator, layer, rule, ruleIndex);
        rightX += DecoratorClipboard.GetRuleButtonsWidth() + 4;
        
        GUI.enabled = ruleIndex > 0;
        if (GUI.Button(new Rect(rightX, rect.y + 3, 18, 18), "↑", EditorStyles.miniButton))
        {
            SwapRules(layer, ruleIndex, ruleIndex - 1);
            SwapRuleFoldouts(layerIndex, ruleIndex, ruleIndex - 1);
        }
        rightX += 20;
        
        GUI.enabled = ruleIndex < layer.rules.Count - 1;
        if (GUI.Button(new Rect(rightX, rect.y + 3, 18, 18), "↓", EditorStyles.miniButton))
        {
            SwapRules(layer, ruleIndex, ruleIndex + 1);
            SwapRuleFoldouts(layerIndex, ruleIndex, ruleIndex + 1);
        }
        GUI.enabled = true;
        rightX += 20;
        
        GUI.backgroundColor = new Color(1f, 0.6f, 0.6f);
        if (GUI.Button(new Rect(rightX, rect.y + 3, 18, 18), "×", EditorStyles.miniButton))
            RemoveRule(layer, ruleIndex);
        GUI.backgroundColor = Color.white;
        
        return clicked;
    }
    
    private bool IsFirstActiveRule(MBTerrainDecorator.Layers layer, int ruleIndex)
    {
        for (int i = 0; i < ruleIndex; i++)
        {
            if (layer.rules[i].active)
                return false;
        }
        return layer.rules[ruleIndex].active;
    }
    private void DrawRuleContent(MBTerrainDecorator.Rules rule, Color ruleColor)
{
    EditorGUILayout.BeginHorizontal();
    
    Rect guideRect = EditorGUILayout.GetControlRect(GUILayout.Width(6), GUILayout.ExpandHeight(true));
    if (Event.current.type == EventType.Repaint)
        EditorGUI.DrawRect(new Rect(guideRect.x + 2, guideRect.y, 2, guideRect.height), ruleColor * 0.4f);
    
    EditorGUILayout.BeginVertical(EditorStyles.helpBox);
    EditorGUILayout.Space(2);
    
    var newFilter = (MBTerrainDecorator.FilterType)EditorGUILayout.EnumPopup("Filter", rule.filter);
    if (newFilter != rule.filter)
    {
        Undo.RecordObject(Decorator, "Change Filter Type");
        rule.filter = newFilter;
        EditorUtility.SetDirty(Decorator);
    }
    
    // Find if this is the first active rule in its layer
    bool isFirstActiveRule = IsFirstActiveRuleForRule(rule);
    
    
    // Disable blend dropdown for first active rule
    using (new EditorGUI.DisabledGroupScope(isFirstActiveRule))
    {
        MBTerrainDecorator.BlendType newBlend;
        if (isFirstActiveRule)
        {
             newBlend = (MBTerrainDecorator.BlendType)EditorGUILayout.EnumPopup(
                new GUIContent( "Blend","First rule always uses Normal blend" ),
                MBTerrainDecorator.BlendType.normal);
        }
        else
        {
             newBlend = (MBTerrainDecorator.BlendType)EditorGUILayout.EnumPopup(
                new GUIContent("Blend",  "How to combine with previous rules"),
                rule.blend);
        }
        
        if (!isFirstActiveRule && newBlend != rule.blend)
        {
            Undo.RecordObject(Decorator, "Change Blend Type");
            rule.blend = newBlend;
            EditorUtility.SetDirty(Decorator);
        }
    }
    
    EditorGUILayout.Space(3);
    DrawFilterSpecificSettings(rule);
    EditorGUILayout.Space(3);
    DrawRuleCommonSettings(rule);
    
    EditorGUILayout.EndVertical();
    EditorGUILayout.EndHorizontal();
}
    
    private void DrawFilterSpecificSettings(MBTerrainDecorator.Rules rule)
    {
        switch (rule.filter)
        {
            case MBTerrainDecorator.FilterType.height:
                DrawMinMaxSlider(rule, "Height", 0f, 1f);
                break;
                
            case MBTerrainDecorator.FilterType.slope:
                if (Decorator.slopeInDegrees)
                {
                    DrawMinMaxSlider(rule, "Slope (°)", 0f, 90f);
                    EditorGUILayout.HelpBox(
                        Decorator.useAccurateSlope 
                            ? "Using Horn's algorithm (accurate)" 
                            : "Using Unity GetSteepness (fast)", 
                        MessageType.None);
                }
                else
                {
                    DrawMinMaxSlider(rule, "Slope", 0f, 1f);
                    EditorGUILayout.HelpBox(
                        $"Normalized: 0 = flat, 1 = 90°\n" +
                        (Decorator.useAccurateSlope 
                            ? "Using Horn's algorithm" 
                            : "Using Unity GetSteepness"), 
                        MessageType.None);
                }
                break;
                
            case MBTerrainDecorator.FilterType.curvature:
                // Curvature type selector
                var newCurvatureType = (CurvatureCalculator.CurvatureType)EditorGUILayout.EnumPopup(
                    new GUIContent("Curvature Type", GetCurvatureTypeTooltip(rule.curvatureType)), 
                    rule.curvatureType);
                if (newCurvatureType != rule.curvatureType)
                {
                    Undo.RecordObject(Decorator, "Change Curvature Type");
                    rule.curvatureType = newCurvatureType;
                    EditorUtility.SetDirty(Decorator);
                }
                
                // Min/Max for curvature (normalized 0-1)
                DrawMinMaxSlider(rule, "Curvature", 0f, 1f);
                
                // Info box explaining the curvature type
                string curvatureInfo = GetCurvatureTypeDescription(rule.curvatureType);
                EditorGUILayout.HelpBox(curvatureInfo, MessageType.None);
                break;
                
            case MBTerrainDecorator.FilterType.waterLevel:
                DrawWaterLevelFilterSettings(rule);
                break;
                
            case MBTerrainDecorator.FilterType.noise:
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Frequency", GUILayout.Width(70));
                float newFreq = EditorGUILayout.FloatField(rule.frequency);
                EditorGUILayout.LabelField("Lacunarity", GUILayout.Width(70));
                float newLac = EditorGUILayout.FloatField(rule.lacunarity);
                EditorGUILayout.LabelField("Perlin Octaves", GUILayout.Width(70));
                int newOctaves = EditorGUILayout.IntSlider(rule.perlinOctaves, 1, 8);
                EditorGUILayout.EndHorizontal();
                if (!Mathf.Approximately(newFreq, rule.frequency) || !Mathf.Approximately(newLac, rule.lacunarity) || !Mathf.Approximately(newOctaves, rule.perlinOctaves))
                {
                    Undo.RecordObject(Decorator, "Change Noise Settings");
                    rule.frequency = newFreq;
                    rule.lacunarity = newLac;
                    rule.perlinOctaves = newOctaves;
                    EditorUtility.SetDirty(Decorator);
                }
                break;
                
            case MBTerrainDecorator.FilterType.texture:
                var newTex = (Texture2D)EditorGUILayout.ObjectField("Texture", rule.texture, typeof(Texture2D), false);
                if (newTex != rule.texture)
                {
                    Undo.RecordObject(Decorator, "Change Texture");
                    rule.texture = newTex;
                    EditorUtility.SetDirty(Decorator);
                }
                var newChannel = (MBTerrainDecorator.ImageChannel)EditorGUILayout.EnumPopup("Channel", rule.imageChannel);
                if (newChannel != rule.imageChannel)
                {
                    Undo.RecordObject(Decorator, "Change Channel");
                    rule.imageChannel = newChannel;
                    EditorUtility.SetDirty(Decorator);
                }
                break;
                
            case MBTerrainDecorator.FilterType.pgm:
                var newPgm = (DefaultAsset)EditorGUILayout.ObjectField("PGM File", rule.pgmAsset, typeof(DefaultAsset), false);
                if (newPgm != rule.pgmAsset)
                {
                    Undo.RecordObject(Decorator, "Change PGM");
                    rule.pgmAsset = newPgm;
                    EditorUtility.SetDirty(Decorator);
                }
                break;
                
            case MBTerrainDecorator.FilterType.generator:
                var newGen = (TextAsset)EditorGUILayout.ObjectField("Generator", rule.generatorAsset, typeof(TextAsset), false);
                if (newGen != rule.generatorAsset)
                {
                    Undo.RecordObject(Decorator, "Change Generator");
                    rule.generatorAsset = newGen;
                    EditorUtility.SetDirty(Decorator);
                }
                break;
                
            case MBTerrainDecorator.FilterType.layer:
                if (textureChoices != null && textureChoices.Length > 0)
                {
                    int currentIdx = Mathf.Clamp(rule.targetLayerIndex, 0, textureChoices.Length - 1);
                    int newIdx = EditorGUILayout.Popup("Source Layer", currentIdx, textureChoices);
                    if (newIdx != rule.targetLayerIndex)
                    {
                        Undo.RecordObject(Decorator, "Change Source Layer");
                        rule.targetLayerIndex = newIdx;
                        EditorUtility.SetDirty(Decorator);
                    }
                }
                break;
        }
    }
    
    private void DrawWaterLevelFilterSettings(MBTerrainDecorator.Rules rule)
    {
        // Water level display (read-only, global setting)
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Water Level", GUILayout.Width(EditorGUIUtility.labelWidth));
        EditorGUI.BeginDisabledGroup(true);
        EditorGUILayout.FloatField(Decorator.waterLevel);
        EditorGUI.EndDisabledGroup();
        if (GUILayout.Button("⚙", GUILayout.Width(22)))
        {
            currentSubTab = DecoratorSubTab.Settings;
            showWaterSettings = true;
        }
        EditorGUILayout.EndHorizontal();
        
        // Get terrain height range for context
        float terrainBase = 0f;
        float terrainTop = 0f;
        if (Decorator.t != null && Decorator.t.terrainData != null)
        {
            terrainBase = Decorator.t.transform.position.y;
            terrainTop = terrainBase + Decorator.t.terrainData.size.y;
        }
        
        // Min/Max relative height (world units relative to water level)
        float heightRange = Mathf.Max(terrainTop - terrainBase, 1f);
        float minRel = terrainBase - Decorator.waterLevel;
        float maxRel = terrainTop - Decorator.waterLevel;
        
        DrawMinMaxSlider(rule, "Relative Height", minRel, maxRel);
        
        EditorGUILayout.HelpBox(
            $"Range is height relative to water level ({Decorator.waterLevel:F1}m).\n" +
            $"Negative = underwater, Positive = above water.\n" +
            $"Terrain: {terrainBase:F1}m → {terrainTop:F1}m (rel: {minRel:F1} → {maxRel:F1})",
            MessageType.None);
        
        EditorGUILayout.Space(3);
        
        // Per-rule shoreline distance toggle
        bool newUseShoreline = EditorGUILayout.Toggle(
            new GUIContent("Shoreline Attenuation",
                "Apply distance-based falloff from the water edge.\n" +
                "Uses Jump Flood Algorithm (JFA) to compute distance to nearest water/land boundary.\n" +
                "Weight = 1.0 at shore, falling to 0.0 at max distance."),
            rule.useShorelineDistance);
        if (newUseShoreline != rule.useShorelineDistance)
        {
            Undo.RecordObject(Decorator, "Toggle Shoreline Distance");
            rule.useShorelineDistance = newUseShoreline;
            EditorUtility.SetDirty(Decorator);
        }
        
        if (rule.useShorelineDistance)
        {
            EditorGUI.indentLevel++;
            
            float newMaxDist = EditorGUILayout.Slider(
                new GUIContent("Max Distance (m)",
                    "Maximum distance from shore for attenuation.\n" +
                    "At this distance, the shoreline weight becomes 0."),
                rule.maxShorelineDistance, 1f, 200f);
            if (!Mathf.Approximately(newMaxDist, rule.maxShorelineDistance))
            {
                Undo.RecordObject(Decorator, "Change Shoreline Distance");
                rule.maxShorelineDistance = newMaxDist;
                EditorUtility.SetDirty(Decorator);
            }
            
            // Mini falloff preview
            Rect previewRect = GUILayoutUtility.GetRect(0, 16, GUILayout.ExpandWidth(true));
            previewRect = EditorGUI.IndentedRect(previewRect);
            if (Event.current.type == EventType.Repaint)
            {
                EditorGUI.DrawRect(previewRect, new Color(0.15f, 0.15f, 0.15f));
                for (int px = 0; px < (int)previewRect.width; px++)
                {
                    float t = px / previewRect.width;
                    float atten = 1f - t; // Linear falloff
                    Color c = Color.Lerp(new Color(0.1f, 0.2f, 0.4f), new Color(0.3f, 0.65f, 0.95f), atten);
                    EditorGUI.DrawRect(new Rect(previewRect.x + px, previewRect.y, 1, previewRect.height), c);
                }
                // Labels
                var miniStyle = new GUIStyle(EditorStyles.miniLabel) { alignment = TextAnchor.MiddleLeft };
                miniStyle.normal.textColor = Color.white;
                UnityEngine.GUI.Label(new Rect(previewRect.x + 2, previewRect.y, 60, previewRect.height), "Shore", miniStyle);
                miniStyle.alignment = TextAnchor.MiddleRight;
                UnityEngine.GUI.Label(new Rect(previewRect.xMax - 62, previewRect.y, 60, previewRect.height), $"{rule.maxShorelineDistance:F0}m", miniStyle);
            }
            
            EditorGUILayout.HelpBox(
                "JFA shoreline detection finds the nearest water/land edge per heightmap cell.\n" +
                "Weight is multiplied by (1 - distance/maxDistance), creating a smooth\n" +
                "gradient from shoreline outward.",
                MessageType.Info);
            
            EditorGUI.indentLevel--;
        }
    }
    
    private string GetCurvatureTypeTooltip(CurvatureCalculator.CurvatureType type)
    {
        switch (type)
        {
            case CurvatureCalculator.CurvatureType.Standard:
                return "Total surface curvature (Laplacian). Identifies ridges and valleys.";
            case CurvatureCalculator.CurvatureType.Profile:
                return "Curvature in direction of maximum slope. Affects flow acceleration/deceleration.";
            case CurvatureCalculator.CurvatureType.Plan:
                return "Curvature perpendicular to slope. Affects flow convergence/divergence.";
            default:
                return "";
        }
    }
    
    private string GetCurvatureTypeDescription(CurvatureCalculator.CurvatureType type)
    {
        switch (type)
        {
            case CurvatureCalculator.CurvatureType.Standard:
                return "Standard: 0 = concave (valleys), 0.5 = flat, 1 = convex (ridges)\n" +
                       "Good for: rock on ridges, grass in valleys";
            case CurvatureCalculator.CurvatureType.Profile:
                return "Profile: 0 = concave slope (accelerating flow), 0.5 = linear, 1 = convex slope (decelerating)\n" +
                       "Good for: erosion patterns, sediment deposits";
            case CurvatureCalculator.CurvatureType.Plan:
                return "Plan: 0 = converging flow, 0.5 = parallel, 1 = diverging flow\n" +
                       "Good for: water channels, vegetation near streams";
            default:
                return "";
        }
    }
    
    private void DrawMinMaxSlider(MBTerrainDecorator.Rules rule, string label, float minLimit, float maxLimit)
    {
        float min = rule.min, max = rule.max;
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField(label, GUILayout.Width(70));
        min = EditorGUILayout.FloatField(min, GUILayout.Width(50));
        EditorGUILayout.MinMaxSlider(ref min, ref max, minLimit, maxLimit);
        max = EditorGUILayout.FloatField(max, GUILayout.Width(50));
        EditorGUILayout.EndHorizontal();
        
        if (!Mathf.Approximately(min, rule.min) || !Mathf.Approximately(max, rule.max))
        {
            Undo.RecordObject(Decorator, $"Change {label} Range");
            rule.min = min;
            rule.max = max;
            EditorUtility.SetDirty(Decorator);
        }
    }
    
    private void DrawRuleCommonSettings(MBTerrainDecorator.Rules rule)
    {
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Intensity", GUILayout.Width(55));
        float newIntensity = EditorGUILayout.Slider(rule.intensity, -10f, 10f);
        EditorGUILayout.EndHorizontal();
        
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Contrast", GUILayout.Width(55));
        float newContrast = EditorGUILayout.Slider(rule.contrast, -1f, 1f);
        EditorGUILayout.EndHorizontal();
        
        if (!Mathf.Approximately(newIntensity, rule.intensity) || !Mathf.Approximately(newContrast, rule.contrast))
        {
            Undo.RecordObject(Decorator, "Change Rule Settings");
            rule.intensity = newIntensity;
            rule.contrast = newContrast;
            EditorUtility.SetDirty(Decorator);
        }
        
        bool newInvert = EditorGUILayout.Toggle("Invert", rule.invert);
        if (newInvert != rule.invert)
        {
            Undo.RecordObject(Decorator, "Toggle Invert");
            rule.invert = newInvert;
            EditorUtility.SetDirty(Decorator);
        }
    }
    
    private string GetRuleSummary(MBTerrainDecorator.Rules rule)
    {
        switch (rule.filter)
        {
            case MBTerrainDecorator.FilterType.height: 
                return $"{rule.min:F2}-{rule.max:F2}";
            case MBTerrainDecorator.FilterType.slope: 
                return Decorator.slopeInDegrees ? $"{rule.min:F0}°-{rule.max:F0}°" : $"{rule.min:F2}-{rule.max:F2}";
            case MBTerrainDecorator.FilterType.curvature:
                string typeShort = rule.curvatureType switch
                {
                    CurvatureCalculator.CurvatureType.Standard => "Std",
                    CurvatureCalculator.CurvatureType.Profile => "Prof",
                    CurvatureCalculator.CurvatureType.Plan => "Plan",
                    _ => "?"
                };
                return $"{typeShort}:{rule.min:F2}-{rule.max:F2}";
            case MBTerrainDecorator.FilterType.waterLevel:
                string jfa = rule.useShorelineDistance ? $" +JFA:{rule.maxShorelineDistance:F0}m" : "";
                return $"wtr:{rule.min:F1}→{rule.max:F1}{jfa}";
            case MBTerrainDecorator.FilterType.noise: 
                return $"f:{rule.frequency:F1}";
            case MBTerrainDecorator.FilterType.texture: 
                return rule.texture != null ? rule.texture.name : "(none)";
            case MBTerrainDecorator.FilterType.layer: 
                return $"layer:{rule.targetLayerIndex}";
            default: 
                return "";
        }
    }
    
    #endregion
    
    #region Settings Tab
    
    private void DrawSettingsTab()
    {
        DrawMainSettingsSection();
        EditorGUILayout.Space(5);
        DrawWaterLevelSettingsSection();
        EditorGUILayout.Space(5);
        DrawSlopeSettingsSection();
        EditorGUILayout.Space(5);
        DrawCurvatureSettingsSection();
        EditorGUILayout.Space(5);
        DrawJobSettingsSection();
    }
    
    private void DrawMainSettingsSection()
    {
        if (!DrawFoldout(ref showSettings, "⚙ General Settings")) return;
        
        GUIHelpers.BeginContents();
        
        float newFalloff = EditorGUILayout.FloatField("Transition Smoothness", Decorator.fallOffDistance);
        if (!Mathf.Approximately(newFalloff, Decorator.fallOffDistance))
        {
            Undo.RecordObject(Decorator, "Change Falloff");
            Decorator.fallOffDistance = newFalloff;
            EditorUtility.SetDirty(Decorator);
        }
        
       
        GUIHelpers.EndContents();
    }
    
    private void DrawWaterLevelSettingsSection()
    {
        if (!DrawFoldout(ref showWaterSettings, "💧 Water Level")) return;
        
        GUIHelpers.BeginContents();
        
        float newWaterLevel = EditorGUILayout.FloatField(
            new GUIContent("Water Level (Y)",
                "Global water level in world-space Y.\n" +
                "All waterLevel filter rules reference this value.\n" +
                "Heights below this are considered underwater."),
            Decorator.waterLevel);
        if (!Mathf.Approximately(newWaterLevel, Decorator.waterLevel))
        {
            Undo.RecordObject(Decorator, "Change Water Level");
            Decorator.waterLevel = newWaterLevel;
            EditorUtility.SetDirty(Decorator);
        }
        
        // Terrain height context
        if (Decorator.t != null && Decorator.t.terrainData != null)
        {
            float terrainBase = Decorator.t.transform.position.y;
            float terrainTop = terrainBase + Decorator.t.terrainData.size.y;
            
            EditorGUILayout.LabelField(
                $"Terrain: {terrainBase:F1}m → {terrainTop:F1}m  |  Water: {Decorator.waterLevel:F1}m",
                EditorStyles.miniLabel);
            
            // Visual bar
            Rect barRect = GUILayoutUtility.GetRect(0, 12, GUILayout.ExpandWidth(true));
            barRect = EditorGUI.IndentedRect(barRect);
            if (Event.current.type == EventType.Repaint)
            {
                float range = terrainTop - terrainBase;
                if (range > 0.001f)
                {
                    EditorGUI.DrawRect(barRect, new Color(0.25f, 0.25f, 0.25f));
                    float waterT = Mathf.Clamp01((Decorator.waterLevel - terrainBase) / range);
                    float waterWidth = waterT * barRect.width;
                    EditorGUI.DrawRect(new Rect(barRect.x, barRect.y, waterWidth, barRect.height),
                        new Color(0.2f, 0.4f, 0.8f, 0.6f));
                    EditorGUI.DrawRect(new Rect(barRect.x + waterWidth - 1, barRect.y, 2, barRect.height),
                        new Color(0.4f, 0.7f, 1f));
                }
            }
        }
        
        EditorGUILayout.HelpBox(
            "Water level is a global setting shared by all waterLevel filter rules.\n" +
            "Shoreline distance (JFA) is configured per-rule in the rule settings.",
            MessageType.Info);
        
        GUIHelpers.EndContents();
    }
    
    private void DrawSlopeSettingsSection()
    {
        if (!DrawFoldout(ref showSlopeSettings, "📐 Slope Calculation")) return;
        
        GUIHelpers.BeginContents();
        
        // Main toggle
        bool newUseAccurate = EditorGUILayout.Toggle(
            new GUIContent("Use Accurate Algorithm", 
                "Enable Horn's algorithm for more accurate slope calculation.\n" +
                "Disable to use Unity's faster but less accurate GetSteepness."),
            Decorator.useAccurateSlope);
        if (newUseAccurate != Decorator.useAccurateSlope)
        {
            Undo.RecordObject(Decorator, "Toggle Accurate Slope");
            Decorator.useAccurateSlope = newUseAccurate;
            EditorUtility.SetDirty(Decorator);
        }
        
        if (Decorator.useAccurateSlope)
        {
            EditorGUI.indentLevel++;
            
            EditorGUILayout.HelpBox(
                "Horn's Algorithm\n" +
                "Uses 3×3 weighted kernel for accurate slope calculation.", 
                MessageType.Info);
            
            bool newInterpolate = EditorGUILayout.Toggle(
                new GUIContent("Interpolate Slope", 
                    "Use bilinear interpolation for smoother slope values.\n" +
                    "Slightly slower but produces smoother results."),
                Decorator.interpolateSlope);
            if (newInterpolate != Decorator.interpolateSlope)
            {
                Undo.RecordObject(Decorator, "Toggle Slope Interpolation");
                Decorator.interpolateSlope = newInterpolate;
                EditorUtility.SetDirty(Decorator);
            }
            
            EditorGUI.indentLevel--;
        }
        else
        {
            EditorGUILayout.HelpBox(
                "Unity GetSteepness\n" +
                "Faster but less accurate slope calculation.",
                MessageType.None);
        }
        
        EditorGUILayout.Space(5);
        
        bool newInDegrees = EditorGUILayout.Toggle(
            new GUIContent("Slope in Degrees", 
                "When enabled, slope rules use degrees (0-90).\n" +
                "When disabled, slope rules use normalized values (0-1)."),
            Decorator.slopeInDegrees);
        if (newInDegrees != Decorator.slopeInDegrees)
        {
            Undo.RecordObject(Decorator, "Toggle Slope Unit");
            Decorator.slopeInDegrees = newInDegrees;
            EditorUtility.SetDirty(Decorator);
        }
        
        GUIHelpers.EndContents();
    }
    
    private void DrawCurvatureSettingsSection()
    {
        if (!DrawFoldout(ref showCurvatureSettings, "〰 Curvature Calculation")) return;
        
        GUIHelpers.BeginContents();
        
        // Main toggle
        bool newUseAccurate = EditorGUILayout.Toggle(
            new GUIContent("Use Accurate Algorithm", 
                "Enable Zeverbergen & Thorne algorithm for curvature calculation.\n" +
                "Calculates Standard, Profile, and Plan curvature types."),
            Decorator.useAccurateCurvature);
        if (newUseAccurate != Decorator.useAccurateCurvature)
        {
            Undo.RecordObject(Decorator, "Toggle Accurate Curvature");
            Decorator.useAccurateCurvature = newUseAccurate;
            EditorUtility.SetDirty(Decorator);
        }
        
        if (Decorator.useAccurateCurvature)
        {
            EditorGUI.indentLevel++;
            
            EditorGUILayout.HelpBox(
                "Zeverbergen & Thorne Algorithm\n" +
                "Calculates surface curvature using polynomial fitting.\n" +
                "• Standard: Total curvature (ridges/valleys)\n" +
                "• Profile: Flow acceleration/deceleration\n" +
                "• Plan: Flow convergence/divergence", 
                MessageType.Info);
            
            bool newInterpolate = EditorGUILayout.Toggle(
                new GUIContent("Interpolate Curvature", 
                    "Use bilinear interpolation for smoother curvature values.\n" +
                    "Slightly slower but produces smoother results."),
                Decorator.interpolateCurvature);
            if (newInterpolate != Decorator.interpolateCurvature)
            {
                Undo.RecordObject(Decorator, "Toggle Curvature Interpolation");
                Decorator.interpolateCurvature = newInterpolate;
                EditorUtility.SetDirty(Decorator);
            }
            EditorGUI.indentLevel--;
        }
        else
        {
            EditorGUILayout.HelpBox(
                "Curvature calculation is disabled.\n" +
                "Enable to use curvature-based decoration rules.", 
                MessageType.None);
        }
        GUIHelpers.EndContents();
    }
    private void DrawJobSettingsSection()
    {
        if (!DrawFoldout(ref showJobSettings, "⚡ Performance")) return;
        
        GUIHelpers.BeginContents();
        
        EditorGUILayout.HelpBox("Uses Unity Job System with Burst compilation for parallel processing.", MessageType.Info);
        
        int newBatchSize = EditorGUILayout.IntSlider("Batch Size", Decorator.jobBatchSize, 16, 256);
        if (newBatchSize != Decorator.jobBatchSize)
        {
            Undo.RecordObject(Decorator, "Change Batch Size");
            Decorator.jobBatchSize = newBatchSize;
            EditorUtility.SetDirty(Decorator);
        }
        
        EditorGUILayout.LabelField("Larger batch sizes may improve performance on multi-core CPUs.", EditorStyles.miniLabel);
        
        GUIHelpers.EndContents();
    }
    #endregion
    
    #region Helper Methods
    
    private void DrawNoDecoratorWarning()
    {
        EditorGUILayout.HelpBox("MBTerrainDecorator not found on terrain.\n\nAdd a MBTerrainDecorator component to enable splatmap decoration.", MessageType.Warning);
        EditorGUILayout.Space(10);
        
        if (manager.Terrain != null)
        {
            GUI.backgroundColor = UIColors.Green;
            if (GUILayout.Button("Add MBTerrainDecorator", GUILayout.Height(30)))
            {
                Undo.AddComponent<MBTerrainDecorator>(manager.Terrain.gameObject);
                manager.RefreshComponents();
            }
            GUI.backgroundColor = Color.white;
        }
        else
        {
            EditorGUILayout.HelpBox("Assign a terrain in the General tab first.", MessageType.Info);
        }
    }
    
    private bool DrawFoldout(ref bool foldout, string title)
    {
        foldout = EditorGUILayout.Foldout(foldout, title, true, EditorStyles.foldoutHeader);
        return foldout;
    }
    
    private Color GetLayerColor(bool active, bool isInvalid)
    {
        if (isInvalid) return InvalidLayerColor;
        if (!active) return DisabledLayerColor;
        return TextureLayerColor;
    }
    
    private Color GetRuleFilterColor(MBTerrainDecorator.FilterType filter)
    {
        switch (filter)
        {
            case MBTerrainDecorator.FilterType.height: return HeightRuleColor;
            case MBTerrainDecorator.FilterType.slope: return SlopeRuleColor;
            case MBTerrainDecorator.FilterType.curvature: return CurvatureRuleColor;
            case MBTerrainDecorator.FilterType.waterLevel: return WaterLevelRuleColor;
            case MBTerrainDecorator.FilterType.noise: return NoiseRuleColor;
            case MBTerrainDecorator.FilterType.texture: return TextureRuleColor;
            case MBTerrainDecorator.FilterType.layer: return LayerRuleColor;
            case MBTerrainDecorator.FilterType.generator: return GeneratorRuleColor;
            case MBTerrainDecorator.FilterType.pgm: return PGMRuleColor;
            default: return Color.gray;
        }
    }
    
    private string GetRuleFilterShortName(MBTerrainDecorator.FilterType filter)
    {
        switch (filter)
        {
            case MBTerrainDecorator.FilterType.height: return "HGT";
            case MBTerrainDecorator.FilterType.slope: return "SLP";
            case MBTerrainDecorator.FilterType.curvature: return "CRV";
            case MBTerrainDecorator.FilterType.waterLevel: return "WTR";
            case MBTerrainDecorator.FilterType.noise: return "NOS";
            case MBTerrainDecorator.FilterType.texture: return "TEX";
            case MBTerrainDecorator.FilterType.layer: return "LYR";
            case MBTerrainDecorator.FilterType.generator: return "GEN";
            case MBTerrainDecorator.FilterType.pgm: return "PGM";
            default: return "???";
        }
    }
    
    private void DrawBadge(Rect rect, string text, Color color)
    {
        EditorGUI.DrawRect(rect, color);
        GUI.color = GetContrastTextColor(color);
        EditorGUI.LabelField(rect, text, _badgeStyle);
        GUI.color = Color.white;
    }
    
    private Color GetContrastTextColor(Color bg)
    {
        float luminance = 0.299f * bg.r + 0.587f * bg.g + 0.114f * bg.b;
        return luminance > 0.5f ? Color.black : Color.white;
    }
    
    private Texture2D GetLayerTexturePreview(int layerIndex)
    {
        if (Decorator?.t?.terrainData == null || layerIndex < 0 || layerIndex >= layerCount) return null;
        
        if (layerTextureCache.TryGetValue(layerIndex, out Texture2D cached)) return cached;
        
        var terrainLayers = Decorator.t.terrainData.terrainLayers;
        if (terrainLayers == null || layerIndex >= terrainLayers.Length) return null;
        
        var layer = terrainLayers[layerIndex];
        if (layer?.diffuseTexture != null)
        {
            layerTextureCache[layerIndex] = layer.diffuseTexture;
            return layer.diffuseTexture;
        }
        return null;
    }
    
    private void RefreshLayerChoices()
    {
        if (Decorator?.t?.terrainData == null) return;
        
        var terrainData = Decorator.t.terrainData;
        layerCount = terrainData.terrainLayers?.Length ?? 0;
        
        textureChoices = new string[layerCount];
        for (int i = 0; i < layerCount; i++)
        {
            var layer = terrainData.terrainLayers[i];
            textureChoices[i] = layer != null && layer.diffuseTexture != null 
                ? $"[{i}] {layer.diffuseTexture.name}" 
                : $"[{i}] (empty)";
        }
        
        Decorator.textureLayerCount = layerCount;
    }
    
    private void DecorateWithUndo()
    {
        Undo.RegisterCompleteObjectUndo(Decorator.t.terrainData, "Decorate Terrain");
        double startTime = EditorApplication.timeSinceStartup;
        Decorator.Decorate();
        lastDecorateTime = EditorApplication.timeSinceStartup - startTime;
        Debug.Log($"[DecoratorTab] Decoration completed in {lastDecorateTime:F2}s");
    }
    
    private void ResetTerrain()
    {
        Undo.RegisterCompleteObjectUndo(Decorator.t.terrainData, "Reset Terrain");
        Decorator.calculating = false;
        Decorator.calculatingPercent = 0;
        
        var terrainData = Decorator.t.terrainData;
        int width = terrainData.alphamapWidth, height = terrainData.alphamapHeight, layers = terrainData.alphamapLayers;
        float[,,] splatmapData = new float[width, height, layers];
        
        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                splatmapData[x, y, 0] = 1f;
                for (int l = 1; l < layers; l++)
                    splatmapData[x, y, l] = 0f;
            }
        
        terrainData.SetAlphamaps(0, 0, splatmapData);
        EditorUtility.ClearProgressBar();
    }
    
    private void AddNewLayer()
    {
        Undo.RecordObject(Decorator, "Add Layer");
        Decorator.layers.Add(new MBTerrainDecorator.Layers
        {
            name = "New Layer",
            active = true,
            layerIndex = 0,
            layerType = MBTerrainDecorator.LayerType.texture,
            block = MBTerrainDecorator.LayerBlock.Base,
            rules = new List<MBTerrainDecorator.Rules>()
        });
        SyncFoldoutLists();
        layerFoldouts[layerFoldouts.Count - 1] = true;
        EditorUtility.SetDirty(Decorator);
    }
    
    private void AddNewOverlayLayer()
    {
        Undo.RecordObject(Decorator, "Add Overlay Layer");
        Decorator.layers.Add(new MBTerrainDecorator.Layers
        {
            name = "New Overlay",
            active = true,
            layerIndex = 0,
            layerType = MBTerrainDecorator.LayerType.texture,
            block = MBTerrainDecorator.LayerBlock.Overlay,
            overlayBlendMode = MBTerrainDecorator.OverlayBlendMode.PainterOcclusion,
            overlayOpacity = 1f,
            isUserCreated = true,
            rules = new List<MBTerrainDecorator.Rules>()
        });
        SyncFoldoutLists();
        layerFoldouts[layerFoldouts.Count - 1] = true;
        EditorUtility.SetDirty(Decorator);
    }
    
    private void RemoveLayer(int index)
    {
        if (index >= Decorator.layers.Count) return;
        
        var layer = Decorator.layers[index];
        
        // Base layers are never deletable
        if (layer.block == MBTerrainDecorator.LayerBlock.Base)
        {
            EditorUtility.DisplayDialog("Cannot Delete", 
                "Base layers are auto-generated by the regenerator and cannot be deleted.", "OK");
            return;
        }
        
        // Auto-generated overlay layers are rebuilt on regeneration - warn but allow
        if (layer.block == MBTerrainDecorator.LayerBlock.Overlay && !layer.isUserCreated)
        {
            if (!EditorUtility.DisplayDialog("Delete Auto-Generated Layer?", 
                $"'{layer.name}' is auto-generated and will be recreated on next regeneration.\n\nDelete anyway?", 
                "Delete", "Cancel"))
                return;
        }
        
        Undo.RecordObject(Decorator, "Remove Layer");
        Decorator.layers.RemoveAt(index);
        SyncFoldoutLists();
        EditorUtility.SetDirty(Decorator);
    }
    
    private void SwapLayers(int a, int b)
    {
        Undo.RecordObject(Decorator, "Reorder Layers");
        var temp = Decorator.layers[a];
        Decorator.layers[a] = Decorator.layers[b];
        Decorator.layers[b] = temp;
        
        bool tf = layerFoldouts[a];
        layerFoldouts[a] = layerFoldouts[b];
        layerFoldouts[b] = tf;
        
        var trf = ruleFoldouts[a];
        ruleFoldouts[a] = ruleFoldouts[b];
        ruleFoldouts[b] = trf;
        
        EditorUtility.SetDirty(Decorator);
    }
    
    private void AddNewRule(MBTerrainDecorator.Layers layer)
    {
        Undo.RecordObject(Decorator, "Add Rule");
        layer.rules.Add(new MBTerrainDecorator.Rules
        {
            active = true,
            filter = MBTerrainDecorator.FilterType.height,
            blend = MBTerrainDecorator.BlendType.mul,
            min = 0f, max = 1f,
            intensity = 1f, contrast = 0f,
            frequency = 1f, lacunarity = 2f,
            perlinOctaves = 4,
            curvatureType = CurvatureCalculator.CurvatureType.Standard,
            invert = false
        });
        EditorUtility.SetDirty(Decorator);
    }
    
    private void RemoveRule(MBTerrainDecorator.Layers layer, int ruleIndex)
    {
        Undo.RecordObject(Decorator, "Remove Rule");
        layer.rules.RemoveAt(ruleIndex);
        EditorUtility.SetDirty(Decorator);
    }
    
    private void SwapRules(MBTerrainDecorator.Layers layer, int a, int b)
    {
        Undo.RecordObject(Decorator, "Reorder Rules");
        var temp = layer.rules[a];
        layer.rules[a] = layer.rules[b];
        layer.rules[b] = temp;
        EditorUtility.SetDirty(Decorator);
    }
    
    private void SwapRuleFoldouts(int layerIndex, int a, int b)
    {
        if (layerIndex < ruleFoldouts.Count && a < ruleFoldouts[layerIndex].Count && b < ruleFoldouts[layerIndex].Count)
        {
            bool temp = ruleFoldouts[layerIndex][a];
            ruleFoldouts[layerIndex][a] = ruleFoldouts[layerIndex][b];
            ruleFoldouts[layerIndex][b] = temp;
        }
    }
    
    private void SyncFoldoutLists()
    {
        if (Decorator == null) return;
        int targetCount = Decorator.layers.Count;
        
        while (layerFoldouts.Count < targetCount) layerFoldouts.Add(false);
        while (layerFoldouts.Count > targetCount) layerFoldouts.RemoveAt(layerFoldouts.Count - 1);
        while (ruleFoldouts.Count < targetCount) ruleFoldouts.Add(new List<bool>());
        while (ruleFoldouts.Count > targetCount) ruleFoldouts.RemoveAt(ruleFoldouts.Count - 1);
    }
    
    private void EnsureRuleFoldouts(int layerIndex, int ruleCount)
    {
        while (ruleFoldouts.Count <= layerIndex) ruleFoldouts.Add(new List<bool>());
        while (ruleFoldouts[layerIndex].Count < ruleCount) ruleFoldouts[layerIndex].Add(false);
        while (ruleFoldouts[layerIndex].Count > ruleCount) ruleFoldouts[layerIndex].RemoveAt(ruleFoldouts[layerIndex].Count - 1);
    }
    
    private bool IsFirstActiveRuleForRule(MBTerrainDecorator.Rules rule)
    {
        foreach (var layer in Decorator.layers)
        {
            int ruleIndex = layer.rules.IndexOf(rule);
            if (ruleIndex >= 0)
            {
                return IsFirstActiveRule(layer, ruleIndex);
            }
        }
        return false;
    }
    
    #endregion
}
