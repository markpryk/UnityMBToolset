using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Simplified terrain tint layer system with multiple layer types.
/// - Paint layers: Can be painted in editor
/// - Texture layers: Reference existing textures (read-only)
/// - AO layers: Baked ambient occlusion from scene geometry
/// Resolution is always synced with the heightmap generator.
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("M&B Tools/Terrain Tint Data")]
public class TerrainTintData : MonoBehaviour
{
    #region Enums
    
    public enum LayerType
    {
        Paint,      // Paintable layer - can edit in scene view
        Texture,    // Texture reference - read only, for importing existing tints
        AO,         // Ambient Occlusion - baked from scene geometry
        PPM         // PPM texture - M&B native format, read only
    }
    
    public enum BlendMode
    {
        Multiply,   // Darkens (default for tinting)
        Screen,     // Lightens
        Overlay,    // Contrast
        SoftLight,  // Subtle
        Normal      // Direct replacement based on alpha/opacity
    }
    
    #endregion
    
    #region TintLayer Class
    
    [Serializable]
    public class TintLayer
    {
        [Header("Layer Settings")]
        public bool active = true;
        public string name = "Tint Layer";
        public LayerType type = LayerType.Paint;
        
        [Range(0f, 1f)]
        public float opacity = 1f;
        
        public BlendMode blendMode = BlendMode.Multiply;
        
        [Header("Texture")]
        [Tooltip("RGB texture storing tint colors. White = no tint.")]
        public Texture2D texture;
        
        [Header("AO Settings")]
        [Range(16, 512)]
        public int aoRayCount = 64;
        
        [Range(0.1f, 50f)]
        public float aoRange = 5f;
        
        [Range(0f, 2f)]
        public float aoIntensity = 1f;
        
        public bool aoIncludeTerrainSelf = true;
        public bool aoUseBlur = true;
        
        [Header("PPM Settings")]
        [Tooltip("PPM file asset (drag .ppm file here)")]
        public UnityEngine.Object ppmAsset;
        
        [Tooltip("Path to the source PPM file (auto-filled or manual)")]
        public string ppmSourcePath = "";
        
        // Runtime - cached texture from PPM file
        [NonSerialized]
        public Texture2D ppmCachedTexture;
        
        [Header("Editor State")]
        [HideInInspector]
        public bool foldout = false;
        
        // Runtime - is this layer's texture dirty and needs saving?
        [NonSerialized]
        public bool isDirty = false;
        
        public bool CanPaint => type == LayerType.Paint && texture != null;
        public bool HasTexture => texture != null || (type == LayerType.PPM && ppmCachedTexture != null);
        public bool IsAO => type == LayerType.AO;
        public bool IsPPM => type == LayerType.PPM;
        
        /// <summary>Get the effective texture for compositing (regular or PPM cached)</summary>
        public Texture2D EffectiveTexture => type == LayerType.PPM ? ppmCachedTexture : texture;
        
        public static TintLayer CreatePaintLayer(string name)
        {
            return new TintLayer
            {
                active = true,
                name = name,
                type = LayerType.Paint,
                opacity = 1f,
                blendMode = BlendMode.Multiply,
                texture = null,
                foldout = true
            };
        }
        
        public static TintLayer CreateTextureLayer(string name, Texture2D tex = null)
        {
            return new TintLayer
            {
                active = true,
                name = name,
                type = LayerType.Texture,
                opacity = 1f,
                blendMode = BlendMode.Multiply,
                texture = tex,
                foldout = false
            };
        }
        
        public static TintLayer CreateAOLayer(string name)
        {
            return new TintLayer
            {
                active = true,
                name = name,
                type = LayerType.AO,
                opacity = 1f,
                blendMode = BlendMode.Multiply,
                texture = null,
                aoRayCount = 64,
                aoRange = 5f,
                aoIntensity = 1f,
                aoIncludeTerrainSelf = true,
                aoUseBlur = true,
                foldout = true
            };
        }
        
        public static TintLayer CreatePPMLayer(string name, string ppmPath = "")
        {
            DefaultAsset asset = null;
            
            if (!string.IsNullOrEmpty(ppmPath))
            {
                var path = MBPathHelpers.FullToUnityPath(ppmPath);
                asset = AssetDatabase.LoadAssetAtPath<DefaultAsset>(path);
            }
            
            return new TintLayer
            {
                active = true,
                name = name,
                type = LayerType.PPM,
                opacity = 1f,
                blendMode = BlendMode.Multiply,
                texture = null,
                ppmSourcePath = ppmPath,
                foldout = true,
                ppmAsset = asset
            };
        }
    }
    
    #endregion
    
    #region Data Fields
    
    [Header("Layers")]
    public List<TintLayer> layers = new List<TintLayer>();
    
    [Header("Output")]
    [Tooltip("Final baked tint map PNG")]
    public Texture2D bakedTintMap;
    
    [Tooltip("Shader property name for the baked tint map")]
    public string shaderProperty = "_OverlayTex";
    
    [Header("Brush")]
    public int activeLayerIndex = 0;
    public Color brushColor = new Color(0.6f, 0.5f, 0.4f);
    
    [Range(1f, 500f)]
    public float brushSize = 50f;
    
    [Range(0f, 1f)]
    public float brushStrength = 0.5f;
    
    [Range(0f, 1f)]
    public float brushFalloff = 0.5f;
    
    public bool eraseMode = false;
    
    [Header("References")]
    public Terrain terrain;

    #endregion
    
    #region Properties
    
    public TintLayer ActiveLayer
    {
        get
        {
            if (layers == null || layers.Count == 0) return null;
            int idx = Mathf.Clamp(activeLayerIndex, 0, layers.Count - 1);
            return layers[idx];
        }
    }
    
    public Texture2D ActiveTexture => ActiveLayer?.texture;
    public Color EffectiveBrushColor => eraseMode ? Color.white : brushColor;
    public bool HasLayers => layers != null && layers.Count > 0;
    
    /// <summary>Can paint on active layer (must be Paint type with texture)</summary>
    public bool CanPaint => ActiveLayer != null && ActiveLayer.CanPaint;
    
    /// <summary>Get first paintable layer index, or -1 if none</summary>
    public int FirstPaintableLayerIndex
    {
        get
        {
            if (layers == null) return -1;
            for (int i = 0; i < layers.Count; i++)
            {
                if (layers[i].CanPaint) return i;
            }
            return -1;
        }
    }
    
    /// <summary>Count of paint-type layers</summary>
    public int PaintLayerCount
    {
        get
        {
            if (layers == null) return 0;
            int count = 0;
            foreach (var l in layers)
                if (l.type == LayerType.Paint) count++;
            return count;
        }
    }
    
    #endregion
    
    #region Unity Lifecycle
    
    private void Reset()
    {
        terrain = GetComponent<Terrain>() ?? GetComponentInParent<Terrain>();
        
        if (layers == null || layers.Count == 0)
        {
            layers = new List<TintLayer>();
        }
    }
    
    private void OnValidate()
    {
        if (terrain == null)
        {
            terrain = GetComponent<Terrain>() ?? GetComponentInParent<Terrain>();
        }
        
        brushSize = Mathf.Max(1f, brushSize);
        activeLayerIndex = Mathf.Clamp(activeLayerIndex, 0, Mathf.Max(0, layers.Count - 1));
    }
    
    #endregion
    
    #region Public API
    
    public TintLayer AddPaintLayer(string name = null)
    {
        var layer = TintLayer.CreatePaintLayer(name ?? $"Paint Layer {PaintLayerCount}");
        layers.Add(layer);
        return layer;
    }
    
    public TintLayer AddTextureLayer(string name = null, Texture2D tex = null)
    {
        var layer = TintLayer.CreateTextureLayer(name ?? $"Texture Layer {layers.Count}", tex);
        layers.Add(layer);
        return layer;
    }
    
    public TintLayer AddAOLayer(string name = null)
    {
        var layer = TintLayer.CreateAOLayer(name ?? $"AO Layer {layers.Count}");
        layers.Add(layer);
        return layer;
    }
    
    public TintLayer AddPPMLayer(string name = null, string ppmPath = "")
    {
        var layer = TintLayer.CreatePPMLayer(name ?? $"PPM Layer {layers.Count}", ppmPath);
        layers.Add(layer);
        return layer;
    }
    
    public void RemoveLayer(int index)
    {
        if (index >= 0 && index < layers.Count)
        {
            layers.RemoveAt(index);
            activeLayerIndex = Mathf.Clamp(activeLayerIndex, 0, Mathf.Max(0, layers.Count - 1));
        }
    }
    
    public void MoveLayerUp(int index)
    {
        if (index > 0 && index < layers.Count)
        {
            (layers[index], layers[index - 1]) = (layers[index - 1], layers[index]);
            
            if (activeLayerIndex == index) activeLayerIndex = index - 1;
            else if (activeLayerIndex == index - 1) activeLayerIndex = index;
        }
    }
    
    public void MoveLayerDown(int index)
    {
        if (index >= 0 && index < layers.Count - 1)
        {
            (layers[index], layers[index + 1]) = (layers[index + 1], layers[index]);
            
            if (activeLayerIndex == index) activeLayerIndex = index + 1;
            else if (activeLayerIndex == index + 1) activeLayerIndex = index;
        }
    }
    
    public TintLayer GetLayer(int index)
    {
        if (layers == null || index < 0 || index >= layers.Count) return null;
        return layers[index];
    }
    
    /// <summary>Select the next paintable layer</summary>
    public bool SelectNextPaintableLayer()
    {
        if (layers == null || layers.Count == 0) return false;
        
        // Start from current and wrap around
        for (int i = 1; i <= layers.Count; i++)
        {
            int idx = (activeLayerIndex + i) % layers.Count;
            if (layers[idx].CanPaint)
            {
                activeLayerIndex = idx;
                return true;
            }
        }
        return false;
    }
    
    #endregion

}