using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// ScriptableObject preset for MBTerrainDecorator overlay layer configurations.
/// Stores only overlay layers and their rules - base layers and terrain settings 
/// are managed by the regenerator and not part of presets.
/// </summary>
[CreateAssetMenu(fileName = "DecoratorPreset", menuName = "MB Terrain/Decorator Preset", order = 100)]
public class DecoratorPreset : ScriptableObject
{
    [Header("Preset Info")]
    [Tooltip("Display name for this preset")]
    public string presetName = "New Preset";
    
    [TextArea(2, 4)]
    [Tooltip("Description of what this preset is for")]
    public string description;
    
    [Tooltip("Author or source of this preset")]
    public string author;
    
    [Tooltip("Version string for tracking changes")]
    public string version = "1.0";
    
    [Tooltip("Date this preset was created/modified")]
    public string lastModified;

    [Header("Overlay Layers")]
    [SerializeField]
    public List<PresetLayer> layers = new List<PresetLayer>();
    
    /// <summary>
    /// Serializable overlay layer data for presets.
    /// Only stores overlay-specific fields from MBTerrainDecorator.Layers.
    /// </summary>
    [Serializable]
    public class PresetLayer
    {
        public string name = "New Layer";
        public bool active = true;
        
        [Tooltip("Target texture layer index in terrain")]
        public int layerIndex;
        
        [Tooltip("Layer type: texture or tree")]
        public MBTerrainDecorator.LayerType layerType = MBTerrainDecorator.LayerType.texture;
        
        [Header("Overlay Settings")]
        public MBTerrainDecorator.OverlayBlendMode overlayBlendMode = MBTerrainDecorator.OverlayBlendMode.PainterOcclusion;
        
        [Range(0f, 1f)]
        public float overlayOpacity = 1f;
        
        [Header("Tree Settings (if layerType is tree)")]
        public float probability = 0.5f;
        public int maximumTreeCount = 100;
        public float width = 1f;
        public float height = 1f;
        public float randomPosition = 0.5f;
        public float randomRotation = 1f;
        public float randomSize = 0.2f;
        public float randomHealth = 0.1f;
        public float offset = 0f;
        
        [Header("Rules")]
        public List<PresetRule> rules = new List<PresetRule>();
    }
    
    /// <summary>
    /// Serializable rule data for presets.
    /// </summary>
    [Serializable]
    public class PresetRule
    {
        public bool active = true;
        public MBTerrainDecorator.FilterType filter = MBTerrainDecorator.FilterType.height;
        public MBTerrainDecorator.BlendType blend = MBTerrainDecorator.BlendType.mul;
        
        [Header("Range Settings")]
        public float min = 0f;
        public float max = 1f;
        
        [Header("Noise Settings")]
        public float frequency = 1f;
        public float lacunarity = 2f;
        public int perlinOctaves = 4;
        
        [Header("Adjustment Settings")]
        public float intensity = 1f;
        public float contrast = 0f;
        public bool invert = false;
        
        [Header("Texture/Asset References")]
        [Tooltip("For texture filter - store texture directly")]
        public Texture2D texture;
        
        [Tooltip("For texture filter - or store texture name for lookup")]
        public string textureName;
        
        [Tooltip("For pgm filter - store path relative to project")]
        public string pgmAssetPath;
        
        [Tooltip("For generator filter - store path relative to project")]
        public string generatorAssetPath;
        
        public MBTerrainDecorator.ImageChannel imageChannel = MBTerrainDecorator.ImageChannel.r;
        
        [Header("Layer Reference")]
        [Tooltip("For layer filter - source layer index")]
        public int targetLayerIndex;
        
        [Header("Curvature Settings")]
        public CurvatureCalculator.CurvatureType curvatureType = CurvatureCalculator.CurvatureType.Standard;
        
        [Header("Shoreline Settings")]
        public bool useShorelineDistance;
        public float maxShorelineDistance;
    }
    
    #region Save/Load Methods
    
    /// <summary>
    /// Populate this preset from a decorator's current overlay layers only.
    /// Base layers are skipped - they belong to the regenerator pipeline.
    /// </summary>
    public void SaveFromDecorator(MBTerrainDecorator decorator)
    {
        if (decorator == null)
        {
            Debug.LogError("[DecoratorPreset] Cannot save from null decorator");
            return;
        }
        
        layers.Clear();
        
        foreach (var srcLayer in decorator.layers)
        {
            // Only save overlay layers
            if (srcLayer.block != MBTerrainDecorator.LayerBlock.Overlay)
                continue;
            
            var presetLayer = new PresetLayer
            {
                name = srcLayer.name,
                active = srcLayer.active,
                layerIndex = srcLayer.layerIndex,
                layerType = srcLayer.layerType,
                overlayBlendMode = srcLayer.overlayBlendMode,
                overlayOpacity = srcLayer.overlayOpacity,
                probability = srcLayer.probability,
                maximumTreeCount = srcLayer.maximumTreeCount,
                width = srcLayer.width,
                height = srcLayer.height,
                randomPosition = srcLayer.randomPosition,
                randomRotation = srcLayer.randomRotation,
                randomSize = srcLayer.randomSize,
                randomHealth = srcLayer.randomHealth,
                offset = srcLayer.offset,
                rules = new List<PresetRule>()
            };
            
            foreach (var srcRule in srcLayer.rules)
            {
                var presetRule = new PresetRule
                {
                    active = srcRule.active,
                    filter = srcRule.filter,
                    blend = srcRule.blend,
                    min = srcRule.min,
                    max = srcRule.max,
                    frequency = srcRule.frequency,
                    lacunarity = srcRule.lacunarity,
                    perlinOctaves = srcRule.perlinOctaves,
                    intensity = srcRule.intensity,
                    contrast = srcRule.contrast,
                    invert = srcRule.invert,
                    texture = srcRule.texture,
                    textureName = srcRule.texture != null ? srcRule.texture.name : "",
                    imageChannel = srcRule.imageChannel,
                    targetLayerIndex = srcRule.targetLayerIndex,
                    curvatureType = srcRule.curvatureType,
                    useShorelineDistance = srcRule.useShorelineDistance,
                    maxShorelineDistance = srcRule.maxShorelineDistance
                };
                
#if UNITY_EDITOR
                if (srcRule.pgmAsset != null)
                    presetRule.pgmAssetPath = UnityEditor.AssetDatabase.GetAssetPath(srcRule.pgmAsset);
                if (srcRule.generatorAsset != null)
                    presetRule.generatorAssetPath = UnityEditor.AssetDatabase.GetAssetPath(srcRule.generatorAsset);
#endif
                
                presetLayer.rules.Add(presetRule);
            }
            
            layers.Add(presetLayer);
        }
        
        lastModified = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        
#if UNITY_EDITOR
        UnityEditor.EditorUtility.SetDirty(this);
#endif
        
        Debug.Log($"[DecoratorPreset] Saved {layers.Count} overlay layers to preset '{presetName}'");
    }
    
    /// <summary>
    /// Apply this preset's overlay layers to a decorator.
    /// Only touches overlay layers - base layers are left untouched.
    /// </summary>
    /// <param name="decorator">Target decorator</param>
    /// <param name="mergeMode">If true, adds layers to existing overlays; if false, replaces all overlay layers</param>
    public void LoadToDecorator(MBTerrainDecorator decorator, bool mergeMode = false)
    {
        if (decorator == null)
        {
            Debug.LogError("[DecoratorPreset] Cannot load to null decorator");
            return;
        }
        
#if UNITY_EDITOR
        UnityEditor.Undo.RecordObject(decorator, "Load Decorator Preset");
#endif
        
        if (!mergeMode)
        {
            // Remove only overlay layers, preserve base layers
            decorator.layers.RemoveAll(l => l.block == MBTerrainDecorator.LayerBlock.Overlay);
        }
        
        foreach (var presetLayer in layers)
        {
            var newLayer = new MBTerrainDecorator.Layers
            {
                name = presetLayer.name,
                active = presetLayer.active,
                layerIndex = presetLayer.layerIndex,
                layerType = presetLayer.layerType,
                block = MBTerrainDecorator.LayerBlock.Overlay,
                overlayBlendMode = presetLayer.overlayBlendMode,
                overlayOpacity = presetLayer.overlayOpacity,
                isUserCreated = true,
                probability = presetLayer.probability,
                maximumTreeCount = presetLayer.maximumTreeCount,
                width = presetLayer.width,
                height = presetLayer.height,
                randomPosition = presetLayer.randomPosition,
                randomRotation = presetLayer.randomRotation,
                randomSize = presetLayer.randomSize,
                randomHealth = presetLayer.randomHealth,
                offset = presetLayer.offset,
                rules = new List<MBTerrainDecorator.Rules>()
            };
            
            foreach (var presetRule in presetLayer.rules)
            {
                var newRule = new MBTerrainDecorator.Rules
                {
                    active = presetRule.active,
                    filter = presetRule.filter,
                    blend = presetRule.blend,
                    min = presetRule.min,
                    max = presetRule.max,
                    frequency = presetRule.frequency,
                    lacunarity = presetRule.lacunarity,
                    perlinOctaves = presetRule.perlinOctaves,
                    intensity = presetRule.intensity,
                    contrast = presetRule.contrast,
                    invert = presetRule.invert,
                    texture = presetRule.texture,
                    imageChannel = presetRule.imageChannel,
                    targetLayerIndex = presetRule.targetLayerIndex,
                    curvatureType = presetRule.curvatureType,
                    useShorelineDistance = presetRule.useShorelineDistance,
                    maxShorelineDistance = presetRule.maxShorelineDistance
                };
                
#if UNITY_EDITOR
                if (!string.IsNullOrEmpty(presetRule.pgmAssetPath))
                    newRule.pgmAsset = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEditor.DefaultAsset>(presetRule.pgmAssetPath);
                if (!string.IsNullOrEmpty(presetRule.generatorAssetPath))
                    newRule.generatorAsset = UnityEditor.AssetDatabase.LoadAssetAtPath<TextAsset>(presetRule.generatorAssetPath);
#endif
                
                newLayer.rules.Add(newRule);
            }
            
            decorator.layers.Add(newLayer);
        }
        
#if UNITY_EDITOR
        UnityEditor.EditorUtility.SetDirty(decorator);
#endif
        
        Debug.Log($"[DecoratorPreset] Loaded {layers.Count} overlay layers from preset '{presetName}'" + 
                  (mergeMode ? " (merge mode)" : ""));
    }
    
    #endregion
    
    #region Utility Methods
    
    /// <summary>
    /// Create a deep copy of this preset.
    /// </summary>
    public DecoratorPreset Clone()
    {
        var clone = CreateInstance<DecoratorPreset>();
        
        clone.presetName = presetName + " (Copy)";
        clone.description = description;
        clone.author = author;
        clone.version = version;
        clone.lastModified = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        
        clone.layers = new List<PresetLayer>();
        foreach (var layer in layers)
        {
            var cloneLayer = new PresetLayer
            {
                name = layer.name,
                active = layer.active,
                layerIndex = layer.layerIndex,
                layerType = layer.layerType,
                overlayBlendMode = layer.overlayBlendMode,
                overlayOpacity = layer.overlayOpacity,
                probability = layer.probability,
                maximumTreeCount = layer.maximumTreeCount,
                width = layer.width,
                height = layer.height,
                randomPosition = layer.randomPosition,
                randomRotation = layer.randomRotation,
                randomSize = layer.randomSize,
                randomHealth = layer.randomHealth,
                offset = layer.offset,
                rules = new List<PresetRule>()
            };
            
            foreach (var rule in layer.rules)
            {
                cloneLayer.rules.Add(new PresetRule
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
                    textureName = rule.textureName,
                    pgmAssetPath = rule.pgmAssetPath,
                    generatorAssetPath = rule.generatorAssetPath,
                    imageChannel = rule.imageChannel,
                    targetLayerIndex = rule.targetLayerIndex,
                    curvatureType = rule.curvatureType,
                    useShorelineDistance = rule.useShorelineDistance,
                    maxShorelineDistance = rule.maxShorelineDistance
                });
            }
            
            clone.layers.Add(cloneLayer);
        }
        
        return clone;
    }
    
    /// <summary>
    /// Get a summary string for display.
    /// </summary>
    public string GetSummary()
    {
        int totalRules = 0;
        int activeRules = 0;
        foreach (var layer in layers)
        {
            totalRules += layer.rules.Count;
            foreach (var rule in layer.rules)
                if (rule.active) activeRules++;
        }
        
        return $"{layers.Count} overlay layers, {activeRules}/{totalRules} rules active";
    }
    
    /// <summary>
    /// Validate the preset for potential issues.
    /// </summary>
    public List<string> Validate()
    {
        var issues = new List<string>();
        
        if (string.IsNullOrEmpty(presetName))
            issues.Add("Preset has no name");
        
        if (layers.Count == 0)
            issues.Add("Preset has no overlay layers");
        
        for (int i = 0; i < layers.Count; i++)
        {
            var layer = layers[i];
            
            if (string.IsNullOrEmpty(layer.name))
                issues.Add($"Layer {i} has no name");
            
            if (layer.rules.Count == 0)
                issues.Add($"Layer '{layer.name}' has no rules");
            
            for (int j = 0; j < layer.rules.Count; j++)
            {
                var rule = layer.rules[j];
                
                if (rule.filter == MBTerrainDecorator.FilterType.texture && 
                    rule.texture == null && string.IsNullOrEmpty(rule.textureName))
                {
                    issues.Add($"Layer '{layer.name}' Rule {j}: Texture filter has no texture assigned");
                }
                
                if (rule.min > rule.max)
                {
                    issues.Add($"Layer '{layer.name}' Rule {j}: Min ({rule.min}) > Max ({rule.max})");
                }
            }
        }
        
        return issues;
    }
    
    /// <summary>
    /// Count how many overlay layers exist in the given decorator.
    /// </summary>
    public static int CountOverlayLayers(MBTerrainDecorator decorator)
    {
        if (decorator == null) return 0;
        int count = 0;
        foreach (var layer in decorator.layers)
            if (layer.block == MBTerrainDecorator.LayerBlock.Overlay) count++;
        return count;
    }
    
    #endregion
}