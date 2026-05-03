using UnityEngine;
using System;

#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Terrain erosion processor using Hatchling's algorithm.
/// Attach to a terrain or use statically via ProcessTerrain().
/// 
/// Algorithm based on: https://www.proceduralpixels.com/blog/terrain-hack-fastest-erosion-algorithm-ever
/// Original discovery by Hatchling (Shadertoy)
/// </summary>
[AddComponentMenu("M&B Tools/Terrain Erosion Processor")]
public class TerrainErosionProcessor : MonoBehaviour
{
    #region Settings
    
    [Header("Erosion Parameters")]
    [Tooltip("Search window radius in pixels. Larger = more erosion spread")]
    [Range(1, 32)]
    public int kernelSize = 7;
    
    [Tooltip("Number of threshold layers. More layers = smoother results, slower processing")]
    [Range(8, 256)]
    public int layersCount = 120;
    
    [Tooltip("Blend strength of erosion effect (0 = no change, 1 = full erosion)")]
    [Range(0f, 1f)]
    public float erosionStrength = 1f;
    
    [Header("Multi-Pass Settings")]
    [Tooltip("Number of erosion passes. Multiple passes create deeper erosion")]
    [Range(1, 10)]
    public int passes = 1;
    
    [Tooltip("Strength multiplier per pass (compounds with each iteration)")]
    [Range(0.1f, 1f)]
    public float passStrengthMultiplier = 0.5f;
    
    [Header("Adaptive Kernel")]
    [Tooltip("Use variable kernel size based on height")]
    public bool useAdaptiveKernel = false;
    
    [Tooltip("Kernel scale at lowest points")]
    [Range(0.1f, 2f)]
    public float kernelMinScale = 0.5f;
    
    [Tooltip("Kernel scale at highest points")]
    [Range(0.5f, 3f)]
    public float kernelMaxScale = 1.5f;
    
    [Header("Height Range")]
    [Tooltip("Override automatic height detection")]
    public bool useCustomHeightRange = false;
    
    [Tooltip("Minimum height for erosion processing")]
    public float customMinHeight = 0f;
    
    [Tooltip("Maximum height for erosion processing")]
    public float customMaxHeight = 100f;
    
    [Header("Resources")]
    [Tooltip("Compute shader for GPU erosion (auto-found if null)")]
    public ComputeShader erosionShader;
    
    #endregion
    
    #region Private Fields
    
    private Terrain _terrain;
    private int _csErosionKernel;
    private int _csMultiPassKernel;
    private bool _shaderInitialized;
    
    #endregion
    
    #region Properties
    
    /// <summary>Target terrain (auto-discovered from GameObject)</summary>
    public Terrain Terrain
    {
        get
        {
            if (_terrain == null)
                _terrain = GetComponent<Terrain>();
            return _terrain;
        }
        set => _terrain = value;
    }
    
    #endregion
    
    #region Unity Lifecycle
    
    private void Reset()
    {
        _terrain = GetComponent<Terrain>();
        FindShader();
    }
    
    private void OnValidate()
    {
        if (_terrain == null)
            _terrain = GetComponent<Terrain>();
    }
    
    #endregion
    
    #region Public API
    
    /// <summary>
    /// Apply erosion to the attached terrain using current settings.
    /// </summary>
    /// <returns>Processing time in milliseconds</returns>
    public float ApplyErosion()
    {
        if (Terrain == null)
        {
            Debug.LogError("[TerrainErosionProcessor] No terrain assigned!");
            return 0f;
        }
        
        return ApplyErosion(Terrain);
    }
    
    /// <summary>
    /// Apply erosion to a specific terrain.
    /// </summary>
    /// <param name="terrain">Target terrain</param>
    /// <returns>Processing time in milliseconds</returns>
    public float ApplyErosion(Terrain terrain)
    {
        if (terrain == null)
        {
            Debug.LogError("[TerrainErosionProcessor] Terrain is null!");
            return 0f;
        }
        
        var settings = new ErosionSettings
        {
            kernelSize = this.kernelSize,
            layersCount = this.layersCount,
            erosionStrength = this.erosionStrength,
            passes = this.passes,
            passStrengthMultiplier = this.passStrengthMultiplier,
            useAdaptiveKernel = this.useAdaptiveKernel,
            kernelMinScale = this.kernelMinScale,
            kernelMaxScale = this.kernelMaxScale,
            useCustomHeightRange = this.useCustomHeightRange,
            customMinHeight = this.customMinHeight,
            customMaxHeight = this.customMaxHeight
        };
        
        return ProcessTerrain(terrain, settings, erosionShader);
    }
    
    /// <summary>
    /// Static method to process any terrain with erosion.
    /// </summary>
    /// <param name="terrain">Target terrain</param>
    /// <param name="settings">Erosion settings</param>
    /// <param name="shader">Compute shader (will auto-find if null)</param>
    /// <returns>Processing time in milliseconds</returns>
    public static float ProcessTerrain(Terrain terrain, ErosionSettings settings, ComputeShader shader = null)
    {
        if (terrain == null)
        {
            Debug.LogError("[TerrainErosionProcessor] Cannot process null terrain!");
            return 0f;
        }
        
        // Find shader if not provided
        if (shader == null)
        {
            shader = FindErosionShader();
            if (shader == null)
            {
                Debug.LogError("[TerrainErosionProcessor] Could not find HatchlingErosion compute shader!");
                return 0f;
            }
        }
        
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        
        TerrainData terrainData = terrain.terrainData;
        int resolution = terrainData.heightmapResolution;
        
        // Get height data
        float[,] heights = terrainData.GetHeights(0, 0, resolution, resolution);
        
        // Calculate height range
        float minHeight, maxHeight;
        if (settings.useCustomHeightRange)
        {
            minHeight = settings.customMinHeight / terrainData.size.y;
            maxHeight = settings.customMaxHeight / terrainData.size.y;
        }
        else
        {
            CalculateHeightRange(heights, out minHeight, out maxHeight);
        }
        
        // Process with GPU
        float[,] result = ProcessHeightmapGPU(heights, settings, shader, minHeight, maxHeight);
        
        // Apply back to terrain
        terrainData.SetHeights(0, 0, result);
        
        stopwatch.Stop();
        float timeMs = (float)stopwatch.Elapsed.TotalMilliseconds;
        
        Debug.Log($"[TerrainErosionProcessor] Erosion complete in {timeMs:F1}ms " +
                  $"(Resolution: {resolution}, Kernel: {settings.kernelSize}, Layers: {settings.layersCount}, Passes: {settings.passes})");
        
        return timeMs;
    }
    
    /// <summary>
    /// Process a raw heightmap array without applying to terrain.
    /// </summary>
    /// <param name="heights">Input heightmap (normalized 0-1)</param>
    /// <param name="settings">Erosion settings</param>
    /// <param name="shader">Compute shader (will auto-find if null)</param>
    /// <returns>Processed heightmap</returns>
    public static float[,] ProcessHeightmap(float[,] heights, ErosionSettings settings, ComputeShader shader = null)
    {
        if (shader == null)
        {
            shader = FindErosionShader();
            if (shader == null)
            {
                Debug.LogError("[TerrainErosionProcessor] Could not find HatchlingErosion compute shader!");
                return heights;
            }
        }
        
        float minHeight, maxHeight;
        if (settings.useCustomHeightRange)
        {
            minHeight = settings.customMinHeight;
            maxHeight = settings.customMaxHeight;
        }
        else
        {
            CalculateHeightRange(heights, out minHeight, out maxHeight);
        }
        
        return ProcessHeightmapGPU(heights, settings, shader, minHeight, maxHeight);
    }
    
    #endregion
    
    #region GPU Processing
    
    private static float[,] ProcessHeightmapGPU(float[,] heights, ErosionSettings settings, 
        ComputeShader shader, float minHeight, float maxHeight)
    {
        int resolution = heights.GetLength(0);
        
        // Find kernel
        int erosionKernel = shader.FindKernel("CSErosion");
        
        // Create textures
        RenderTexture inputRT = CreateHeightmapRT(resolution);
        RenderTexture outputRT = CreateHeightmapRT(resolution);
        
        // Upload heightmap to GPU
        Texture2D inputTex = HeightmapToTexture(heights);
        Graphics.Blit(inputTex, inputRT);
        
        // Set shader parameters
        shader.SetInt("Resolution", resolution);
        shader.SetInt("KernelSize", settings.kernelSize);
        shader.SetInt("LayersCount", settings.layersCount);
        shader.SetFloat("MinHeight", minHeight);
        shader.SetFloat("MaxHeight", maxHeight);
        shader.SetVector("TexelSize", new Vector4(1f / resolution, 1f / resolution, 0, 0));
        shader.SetInt("UseVariableKernel", settings.useAdaptiveKernel ? 1 : 0);
        shader.SetFloat("KernelMinScale", settings.kernelMinScale);
        shader.SetFloat("KernelMaxScale", settings.kernelMaxScale);
        
        // Process passes
        RenderTexture currentInput = inputRT;
        RenderTexture currentOutput = outputRT;
        
        float currentStrength = settings.erosionStrength;
        
        for (int pass = 0; pass < settings.passes; pass++)
        {
            shader.SetFloat("ErosionStrength", currentStrength);
            shader.SetTexture(erosionKernel, "HeightmapIn", currentInput);
            shader.SetTexture(erosionKernel, "HeightmapOut", currentOutput);
            
            int threadGroups = Mathf.CeilToInt(resolution / 8f);
            shader.Dispatch(erosionKernel, threadGroups, threadGroups, 1);
            
            // Swap buffers for next pass
            if (pass < settings.passes - 1)
            {
                RenderTexture temp = currentInput;
                currentInput = currentOutput;
                currentOutput = temp;
                
                currentStrength *= settings.passStrengthMultiplier;
            }
        }
        
        // Read back result
        float[,] result = TextureToHeightmap(currentOutput, resolution);
        
        // Cleanup
        UnityEngine.Object.DestroyImmediate(inputTex);
        inputRT.Release();
        outputRT.Release();
        UnityEngine.Object.DestroyImmediate(inputRT);
        UnityEngine.Object.DestroyImmediate(outputRT);
        
        return result;
    }
    
    private static RenderTexture CreateHeightmapRT(int resolution)
    {
        RenderTexture rt = new RenderTexture(resolution, resolution, 0, RenderTextureFormat.RFloat);
        rt.enableRandomWrite = true;
        rt.filterMode = FilterMode.Bilinear;
        rt.wrapMode = TextureWrapMode.Clamp;
        rt.Create();
        return rt;
    }
    
    private static Texture2D HeightmapToTexture(float[,] heights)
    {
        int resolution = heights.GetLength(0);
        Texture2D tex = new Texture2D(resolution, resolution, TextureFormat.RFloat, false);
        
        Color[] pixels = new Color[resolution * resolution];
        for (int y = 0; y < resolution; y++)
        {
            for (int x = 0; x < resolution; x++)
            {
                pixels[y * resolution + x] = new Color(heights[y, x], 0, 0, 1);
            }
        }
        
        tex.SetPixels(pixels);
        tex.Apply();
        return tex;
    }
    
    private static float[,] TextureToHeightmap(RenderTexture rt, int resolution)
    {
        RenderTexture previous = RenderTexture.active;
        RenderTexture.active = rt;
        
        Texture2D tex = new Texture2D(resolution, resolution, TextureFormat.RFloat, false);
        tex.ReadPixels(new Rect(0, 0, resolution, resolution), 0, 0);
        tex.Apply();
        
        RenderTexture.active = previous;
        
        float[,] heights = new float[resolution, resolution];
        Color[] pixels = tex.GetPixels();
        
        for (int y = 0; y < resolution; y++)
        {
            for (int x = 0; x < resolution; x++)
            {
                heights[y, x] = pixels[y * resolution + x].r;
            }
        }
        
        UnityEngine.Object.DestroyImmediate(tex);
        return heights;
    }
    
    #endregion
    
    #region Helpers
    
    private static void CalculateHeightRange(float[,] heights, out float minHeight, out float maxHeight)
    {
        minHeight = float.MaxValue;
        maxHeight = float.MinValue;
        
        int resolution = heights.GetLength(0);
        for (int y = 0; y < resolution; y++)
        {
            for (int x = 0; x < resolution; x++)
            {
                float h = heights[y, x];
                if (h < minHeight) minHeight = h;
                if (h > maxHeight) maxHeight = h;
            }
        }
        
        // Add small padding to prevent edge artifacts
        float range = maxHeight - minHeight;
        float padding = range * 0.01f;
        minHeight -= padding;
        maxHeight += padding;
    }
    
    private void FindShader()
    {
#if UNITY_EDITOR
        if (erosionShader == null)
        {
            erosionShader = FindErosionShader();
        }
#endif
    }
    
    private static ComputeShader FindErosionShader()
    {
#if UNITY_EDITOR
        string[] guids = AssetDatabase.FindAssets("HatchlingErosion t:ComputeShader");
        if (guids.Length > 0)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[0]);
            return AssetDatabase.LoadAssetAtPath<ComputeShader>(path);
        }
#endif
        return Resources.Load<ComputeShader>("HatchlingErosion");
    }
    
    #endregion
    
    #region Settings Struct
    
    /// <summary>
    /// Settings for erosion processing.
    /// </summary>
    [Serializable]
    public struct ErosionSettings
    {
        [Range(1, 32)]
        public int kernelSize;
        
        [Range(8, 256)]
        public int layersCount;
        
        [Range(0f, 1f)]
        public float erosionStrength;
        
        [Range(1, 10)]
        public int passes;
        
        [Range(0.1f, 1f)]
        public float passStrengthMultiplier;
        
        public bool useAdaptiveKernel;
        
        [Range(0.1f, 2f)]
        public float kernelMinScale;
        
        [Range(0.5f, 3f)]
        public float kernelMaxScale;
        
        public bool useCustomHeightRange;
        public float customMinHeight;
        public float customMaxHeight;
        
        /// <summary>Default erosion settings</summary>
        public static ErosionSettings Default => new ErosionSettings
        {
            kernelSize = 7,
            layersCount = 120,
            erosionStrength = 1f,
            passes = 1,
            passStrengthMultiplier = 0.5f,
            useAdaptiveKernel = false,
            kernelMinScale = 0.5f,
            kernelMaxScale = 1.5f,
            useCustomHeightRange = false,
            customMinHeight = 0f,
            customMaxHeight = 100f
        };
        
        /// <summary>Light erosion preset</summary>
        public static ErosionSettings Light => new ErosionSettings
        {
            kernelSize = 4,
            layersCount = 60,
            erosionStrength = 0.5f,
            passes = 1,
            passStrengthMultiplier = 0.5f,
            useAdaptiveKernel = false,
            kernelMinScale = 0.5f,
            kernelMaxScale = 1.5f,
            useCustomHeightRange = false,
            customMinHeight = 0f,
            customMaxHeight = 100f
        };
        
        /// <summary>Medium erosion preset</summary>
        public static ErosionSettings Medium => new ErosionSettings
        {
            kernelSize = 8,
            layersCount = 120,
            erosionStrength = 1f,
            passes = 2,
            passStrengthMultiplier = 0.6f,
            useAdaptiveKernel = true,
            kernelMinScale = 0.5f,
            kernelMaxScale = 1.2f,
            useCustomHeightRange = false,
            customMinHeight = 0f,
            customMaxHeight = 100f
        };
        
        /// <summary>Heavy erosion preset</summary>
        public static ErosionSettings Heavy => new ErosionSettings
        {
            kernelSize = 12,
            layersCount = 180,
            erosionStrength = 1f,
            passes = 3,
            passStrengthMultiplier = 0.7f,
            useAdaptiveKernel = true,
            kernelMinScale = 0.3f,
            kernelMaxScale = 1.5f,
            useCustomHeightRange = false,
            customMinHeight = 0f,
            customMaxHeight = 100f
        };
    }
    
    #endregion
    
    #region Context Menu
    
#if UNITY_EDITOR
    [ContextMenu("Apply Erosion")]
    private void ContextApplyErosion()
    {
        if (Terrain == null)
        {
            Debug.LogError("No terrain assigned!");
            return;
        }
        
        Undo.RegisterCompleteObjectUndo(Terrain.terrainData, "Apply Erosion");
        ApplyErosion();
    }
    
    [ContextMenu("Apply Light Erosion")]
    private void ContextApplyLight()
    {
        ApplyPreset(ErosionSettings.Light);
    }
    
    [ContextMenu("Apply Medium Erosion")]
    private void ContextApplyMedium()
    {
        ApplyPreset(ErosionSettings.Medium);
    }
    
    [ContextMenu("Apply Heavy Erosion")]
    private void ContextApplyHeavy()
    {
        ApplyPreset(ErosionSettings.Heavy);
    }
    
    private void ApplyPreset(ErosionSettings preset)
    {
        if (Terrain == null)
        {
            Debug.LogError("No terrain assigned!");
            return;
        }
        
        Undo.RegisterCompleteObjectUndo(Terrain.terrainData, "Apply Erosion Preset");
        ProcessTerrain(Terrain, preset, erosionShader);
    }
#endif
    
    #endregion
}