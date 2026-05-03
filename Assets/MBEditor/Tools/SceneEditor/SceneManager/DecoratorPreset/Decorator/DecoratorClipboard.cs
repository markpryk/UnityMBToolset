using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

/// <summary>
/// Clipboard functionality for DecoratorTab layer and rule copy/paste.
/// Provides static methods and cached clipboard data for copying layers and rules.
/// </summary>
public static class DecoratorClipboard
{
    #region Clipboard Data
    
    private static LayerClipboardData copiedLayer;
    private static RuleClipboardData copiedRule;
    
    /// <summary>
    /// Returns true if there's a layer in the clipboard.
    /// </summary>
    public static bool HasLayerData => copiedLayer != null;
    
    /// <summary>
    /// Returns true if there's a rule in the clipboard.
    /// </summary>
    public static bool HasRuleData => copiedRule != null;
    
    /// <summary>
    /// Get the name of the copied layer (for UI display).
    /// </summary>
    public static string CopiedLayerName => copiedLayer?.name ?? "(empty)";
    
    /// <summary>
    /// Get the filter type of the copied rule (for UI display).
    /// </summary>
    public static string CopiedRuleSummary => copiedRule != null 
        ? $"{copiedRule.filter} ({copiedRule.min:F2}-{copiedRule.max:F2})" 
        : "(empty)";
    
    #endregion
    
    #region Layer Clipboard Data Structure
    
    private class LayerClipboardData
    {
        public string name;
        public bool active;
        public int layerIndex;
        public MBTerrainDecorator.LayerType layerType;
        
        // Tree settings
        public float probability;
        public int maximumTreeCount;
        public float width;
        public float height;
        public float randomPosition;
        public float randomRotation;
        public float randomSize;
        public float randomHealth;
        public float offset;
        
        // Rules
        public List<RuleClipboardData> rules = new List<RuleClipboardData>();
    }
    
    private class RuleClipboardData
    {
        public bool active;
        public MBTerrainDecorator.FilterType filter;
        public MBTerrainDecorator.BlendType blend;
        public float min;
        public float max;
        public float frequency;
        public float lacunarity;
        public int perlinOctaves;
        public float intensity;
        public float contrast;
        public bool invert;
        
        public Texture2D texture;
        public DefaultAsset pgmAsset;
        public TextAsset generatorAsset;
        public MBTerrainDecorator.ImageChannel imageChannel;
        public int targetLayerIndex;
        public CurvatureCalculator.CurvatureType curvatureType;
    }
    
    #endregion
    
    #region Layer Copy/Paste
    
    /// <summary>
    /// Copy a layer to the clipboard.
    /// </summary>
    public static void CopyLayer(MBTerrainDecorator.Layers layer)
    {
        if (layer == null) return;
        
        copiedLayer = new LayerClipboardData
        {
            name = layer.name,
            active = layer.active,
            layerIndex = layer.layerIndex,
            layerType = layer.layerType,
            probability = layer.probability,
            maximumTreeCount = layer.maximumTreeCount,
            width = layer.width,
            height = layer.height,
            randomPosition = layer.randomPosition,
            randomRotation = layer.randomRotation,
            randomSize = layer.randomSize,
            randomHealth = layer.randomHealth,
            offset = layer.offset,
            rules = new List<RuleClipboardData>()
        };
        
        foreach (var rule in layer.rules)
        {
            copiedLayer.rules.Add(CopyRuleToData(rule));
        }
        
        Debug.Log($"[DecoratorClipboard] Copied layer '{layer.name}' with {layer.rules.Count} rules");
    }
    
    /// <summary>
    /// Paste clipboard data to a layer (overwrites settings and rules).
    /// </summary>
    public static void PasteLayer(MBTerrainDecorator decorator, MBTerrainDecorator.Layers targetLayer, bool includeRules = true)
    {
        if (copiedLayer == null || targetLayer == null) return;
        
        Undo.RecordObject(decorator, "Paste Layer Settings");
        
        targetLayer.name = copiedLayer.name + " (Copy)";
        targetLayer.active = copiedLayer.active;
        targetLayer.layerIndex = copiedLayer.layerIndex;
        targetLayer.layerType = copiedLayer.layerType;
        targetLayer.probability = copiedLayer.probability;
        targetLayer.maximumTreeCount = copiedLayer.maximumTreeCount;
        targetLayer.width = copiedLayer.width;
        targetLayer.height = copiedLayer.height;
        targetLayer.randomPosition = copiedLayer.randomPosition;
        targetLayer.randomRotation = copiedLayer.randomRotation;
        targetLayer.randomSize = copiedLayer.randomSize;
        targetLayer.randomHealth = copiedLayer.randomHealth;
        targetLayer.offset = copiedLayer.offset;
        
        if (includeRules)
        {
            targetLayer.rules.Clear();
            foreach (var ruleData in copiedLayer.rules)
            {
                targetLayer.rules.Add(PasteRuleFromData(ruleData));
            }
        }
        
        EditorUtility.SetDirty(decorator);
        Debug.Log($"[DecoratorClipboard] Pasted layer settings to '{targetLayer.name}'");
    }
    
    /// <summary>
    /// Paste only the rules from clipboard to a layer (keeps layer settings).
    /// </summary>
    public static void PasteRulesToLayer(MBTerrainDecorator decorator, MBTerrainDecorator.Layers targetLayer, bool append = false)
    {
        if (copiedLayer == null || targetLayer == null) return;
        
        Undo.RecordObject(decorator, "Paste Layer Rules");
        
        if (!append)
            targetLayer.rules.Clear();
        
        foreach (var ruleData in copiedLayer.rules)
        {
            targetLayer.rules.Add(PasteRuleFromData(ruleData));
        }
        
        EditorUtility.SetDirty(decorator);
        Debug.Log($"[DecoratorClipboard] Pasted {copiedLayer.rules.Count} rules to '{targetLayer.name}'");
    }
    
    /// <summary>
    /// Create a duplicate of a layer and add it to the decorator.
    /// </summary>
    public static MBTerrainDecorator.Layers DuplicateLayer(MBTerrainDecorator decorator, MBTerrainDecorator.Layers sourceLayer)
    {
        if (decorator == null || sourceLayer == null) return null;
        
        Undo.RecordObject(decorator, "Duplicate Layer");
        
        var newLayer = new MBTerrainDecorator.Layers
        {
            name = sourceLayer.name + " (Copy)",
            active = sourceLayer.active,
            layerIndex = sourceLayer.layerIndex,
            layerType = sourceLayer.layerType,
            probability = sourceLayer.probability,
            maximumTreeCount = sourceLayer.maximumTreeCount,
            width = sourceLayer.width,
            height = sourceLayer.height,
            randomPosition = sourceLayer.randomPosition,
            randomRotation = sourceLayer.randomRotation,
            randomSize = sourceLayer.randomSize,
            randomHealth = sourceLayer.randomHealth,
            offset = sourceLayer.offset,
            rules = new List<MBTerrainDecorator.Rules>()
        };
        
        foreach (var rule in sourceLayer.rules)
        {
            newLayer.rules.Add(DuplicateRule(rule));
        }
        
        decorator.layers.Add(newLayer);
        EditorUtility.SetDirty(decorator);
        
        Debug.Log($"[DecoratorClipboard] Duplicated layer '{sourceLayer.name}'");
        return newLayer;
    }
    
    #endregion
    
    #region Rule Copy/Paste
    
    /// <summary>
    /// Copy a rule to the clipboard.
    /// </summary>
    public static void CopyRule(MBTerrainDecorator.Rules rule)
    {
        if (rule == null) return;
        
        copiedRule = CopyRuleToData(rule);
        Debug.Log($"[DecoratorClipboard] Copied rule: {rule.filter}");
    }
    
    /// <summary>
    /// Paste clipboard data to a rule (overwrites all settings).
    /// </summary>
    public static void PasteRule(MBTerrainDecorator decorator, MBTerrainDecorator.Rules targetRule)
    {
        if (copiedRule == null || targetRule == null) return;
        
        Undo.RecordObject(decorator, "Paste Rule Settings");
        
        targetRule.active = copiedRule.active;
        targetRule.filter = copiedRule.filter;
        targetRule.blend = copiedRule.blend;
        targetRule.min = copiedRule.min;
        targetRule.max = copiedRule.max;
        targetRule.frequency = copiedRule.frequency;
        targetRule.lacunarity = copiedRule.lacunarity;
        targetRule.perlinOctaves = copiedRule.perlinOctaves;
        targetRule.intensity = copiedRule.intensity;
        targetRule.contrast = copiedRule.contrast;
        targetRule.invert = copiedRule.invert;
        targetRule.texture = copiedRule.texture;
        targetRule.pgmAsset = copiedRule.pgmAsset;
        targetRule.generatorAsset = copiedRule.generatorAsset;
        targetRule.imageChannel = copiedRule.imageChannel;
        targetRule.targetLayerIndex = copiedRule.targetLayerIndex;
        targetRule.curvatureType = copiedRule.curvatureType;
        
        EditorUtility.SetDirty(decorator);
        Debug.Log($"[DecoratorClipboard] Pasted rule settings");
    }
    
    /// <summary>
    /// Add a copy of the clipboard rule to a layer.
    /// </summary>
    public static MBTerrainDecorator.Rules PasteRuleAsNew(MBTerrainDecorator decorator, MBTerrainDecorator.Layers targetLayer)
    {
        if (copiedRule == null || targetLayer == null) return null;
        
        Undo.RecordObject(decorator, "Paste Rule as New");
        
        var newRule = PasteRuleFromData(copiedRule);
        targetLayer.rules.Add(newRule);
        
        EditorUtility.SetDirty(decorator);
        Debug.Log($"[DecoratorClipboard] Added copied rule to '{targetLayer.name}'");
        return newRule;
    }
    
    /// <summary>
    /// Create a duplicate of a rule within the same layer.
    /// </summary>
    public static MBTerrainDecorator.Rules DuplicateRule(MBTerrainDecorator.Rules sourceRule)
    {
        if (sourceRule == null) return null;
        
        return new MBTerrainDecorator.Rules
        {
            active = sourceRule.active,
            filter = sourceRule.filter,
            blend = sourceRule.blend,
            min = sourceRule.min,
            max = sourceRule.max,
            frequency = sourceRule.frequency,
            lacunarity = sourceRule.lacunarity,
            perlinOctaves = sourceRule.perlinOctaves,
            intensity = sourceRule.intensity,
            contrast = sourceRule.contrast,
            invert = sourceRule.invert,
            texture = sourceRule.texture,
            pgmAsset = sourceRule.pgmAsset,
            generatorAsset = sourceRule.generatorAsset,
            imageChannel = sourceRule.imageChannel,
            targetLayerIndex = sourceRule.targetLayerIndex,
            curvatureType = sourceRule.curvatureType
        };
    }
    
    #endregion
    
    #region Helper Methods
    
    private static RuleClipboardData CopyRuleToData(MBTerrainDecorator.Rules rule)
    {
        return new RuleClipboardData
        {
            active = rule.active,
            filter = rule.filter,
            blend = rule.blend,
            min = rule.min,
            max = rule.max,
            frequency = rule.frequency,
            lacunarity = rule.lacunarity,
            perlinOctaves = rule.perlinOctaves,
            intensity = rule.intensity,
            contrast = rule.contrast,
            invert = rule.invert,
            texture = rule.texture,
            pgmAsset = rule.pgmAsset,
            generatorAsset = rule.generatorAsset,
            imageChannel = rule.imageChannel,
            targetLayerIndex = rule.targetLayerIndex,
            curvatureType = rule.curvatureType
        };
    }
    
    private static MBTerrainDecorator.Rules PasteRuleFromData(RuleClipboardData data)
    {
        return new MBTerrainDecorator.Rules
        {
            active = data.active,
            filter = data.filter,
            blend = data.blend,
            min = data.min,
            max = data.max,
            frequency = data.frequency,
            lacunarity = data.lacunarity,
            perlinOctaves = data.perlinOctaves,
            intensity = data.intensity,
            contrast = data.contrast,
            invert = data.invert,
            texture = data.texture,
            pgmAsset = data.pgmAsset,
            generatorAsset = data.generatorAsset,
            imageChannel = data.imageChannel,
            targetLayerIndex = data.targetLayerIndex,
            curvatureType = data.curvatureType
        };
    }
    
    /// <summary>
    /// Clear all clipboard data.
    /// </summary>
    public static void ClearClipboard()
    {
        copiedLayer = null;
        copiedRule = null;
        Debug.Log("[DecoratorClipboard] Clipboard cleared");
    }
    
    #endregion
    
    #region UI Drawing Helpers
    
    /// <summary>
    /// Draw micro copy/paste buttons for a layer header.
    /// Returns true if the layer list was modified and needs refresh.
    /// </summary>
    public static bool DrawLayerCopyPasteButtons(Rect rect, MBTerrainDecorator decorator, MBTerrainDecorator.Layers layer, int layerIndex)
    {
        bool modified = false;
        float buttonWidth = 18f;
        float buttonHeight = 16f;
        float spacing = 2f;
        float x = rect.x;
        float y = rect.y + (rect.height - buttonHeight) / 2f;
        
        // Copy button
        GUI.backgroundColor = new Color(0.6f, 0.8f, 1f);
        if (GUI.Button(new Rect(x, y, buttonWidth, buttonHeight), new GUIContent("C", "Copy layer"), EditorStyles.miniButtonLeft))
        {
            CopyLayer(layer);
        }
        x += buttonWidth;
        
        // Paste button
        GUI.enabled = HasLayerData;
        GUI.backgroundColor = new Color(0.8f, 1f, 0.6f);
        if (GUI.Button(new Rect(x, y, buttonWidth, buttonHeight), new GUIContent("P", $"Paste layer settings\n({CopiedLayerName})"), EditorStyles.miniButtonMid))
        {
            GenericMenu menu = new GenericMenu();
            menu.AddItem(new GUIContent("Paste All (Settings + Rules)"), false, () => {
                PasteLayer(decorator, layer, true);
            });
            menu.AddItem(new GUIContent("Paste Settings Only"), false, () => {
                PasteLayer(decorator, layer, false);
            });
            menu.AddItem(new GUIContent("Paste Rules Only (Replace)"), false, () => {
                PasteRulesToLayer(decorator, layer, false);
            });
            menu.AddItem(new GUIContent("Paste Rules Only (Append)"), false, () => {
                PasteRulesToLayer(decorator, layer, true);
            });
            menu.ShowAsContext();
            modified = true;
        }
        GUI.enabled = true;
        x += buttonWidth;
        
        // Duplicate button
        GUI.backgroundColor = new Color(1f, 0.9f, 0.6f);
        if (GUI.Button(new Rect(x, y, buttonWidth, buttonHeight), new GUIContent("D", "Duplicate layer"), EditorStyles.miniButtonRight))
        {
            DuplicateLayer(decorator, layer);
            modified = true;
        }
        
        GUI.backgroundColor = Color.white;
        return modified;
    }
    
    /// <summary>
    /// Draw micro copy/paste buttons for a rule header.
    /// Returns true if the rule list was modified and needs refresh.
    /// </summary>
    public static bool DrawRuleCopyPasteButtons(Rect rect, MBTerrainDecorator decorator, MBTerrainDecorator.Layers layer, MBTerrainDecorator.Rules rule, int ruleIndex)
    {
        bool modified = false;
        float buttonWidth = 16f;
        float buttonHeight = 14f;
        float x = rect.x;
        float y = rect.y + (rect.height - buttonHeight) / 2f;
        
        // Copy button
        GUI.backgroundColor = new Color(0.6f, 0.8f, 1f);
        if (GUI.Button(new Rect(x, y, buttonWidth, buttonHeight), new GUIContent("C", "Copy rule"), EditorStyles.miniButtonLeft))
        {
            CopyRule(rule);
        }
        x += buttonWidth;
        
        // Paste button
        GUI.enabled = HasRuleData;
        GUI.backgroundColor = new Color(0.8f, 1f, 0.6f);
        if (GUI.Button(new Rect(x, y, buttonWidth, buttonHeight), new GUIContent("P", $"Paste rule\n({CopiedRuleSummary})"), EditorStyles.miniButtonMid))
        {
            GenericMenu menu = new GenericMenu();
            menu.AddItem(new GUIContent("Paste to This Rule"), false, () => {
                PasteRule(decorator, rule);
            });
            menu.AddItem(new GUIContent("Paste as New Rule"), false, () => {
                PasteRuleAsNew(decorator, layer);
            });
            menu.ShowAsContext();
            modified = true;
        }
        // GUI.enabled = true;
        // x += buttonWidth;
        //
        // // Duplicate button  
        // GUI.backgroundColor = new Color(1f, 0.9f, 0.6f);
        // if (GUI.Button(new Rect(x, y, buttonWidth, buttonHeight), new GUIContent("D", "Duplicate rule"), EditorStyles.miniButtonRight))
        // {
        //     Undo.RecordObject(decorator, "Duplicate Rule");
        //     var newRule = DuplicateRule(rule);
        //     layer.rules.Insert(ruleIndex + 1, newRule);
        //     EditorUtility.SetDirty(decorator);
        //     modified = true;
        // }
        //
        GUI.backgroundColor = Color.white;
        return modified;
    }
    
    /// <summary>
    /// Get the total width needed for layer copy/paste buttons.
    /// </summary>
    public static float GetLayerButtonsWidth() => 18f * 2; // 3 buttons
    
    /// <summary>
    /// Get the total width needed for rule copy/paste buttons.
    /// </summary>
    public static float GetRuleButtonsWidth() => 16f * 2; // 3 buttons
    
    #endregion
}