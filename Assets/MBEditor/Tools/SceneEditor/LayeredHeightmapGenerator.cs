using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEditor;
using UnityEngine.Serialization;

public class LayeredHeightmapGenerator : MonoBehaviour
{
    public enum FilterType
    {
        Height,
        Noise,
        Generator,
        PFM,
        Painted  // NEW: Special layer for storing manual paint edits
    }

    public enum BlendType
    {
        Add,
        Subtract,
        Multiply,
        Max,
        Min,
        SoftLight,
        HardLight,
        Overlay,
        Screen,
        Lighten,
        LinearLight,
        PinLight,
        Exclusion,
        Lerp,
        InverseLerp,
        Premultiply
    }

    [System.Serializable]
    public class Layer
    {
        public string name = "Layer";
        public bool active = true;
        public bool invert = false;
        public FilterType filterType;
        public BlendType blendType;
        public HeightmapHelper.Flip flip = HeightmapHelper.Flip.None;
        public HeightmapHelper.Rotation rotation = HeightmapHelper.Rotation.None;
        
        [Header("Height Control")]
        [Tooltip("When enabled, heightInMeters defines the maximum elevation this layer contributes")]
        public bool useMetersHeight = false;
        
        [Tooltip("Maximum height contribution in meters (e.g., 50 = layer adds up to 50m)")]
        public float heightInMeters = 10f;
        
        [Tooltip("For digging/valleys - height below base in meters")]
        public float depthInMeters = 0f;
        
        [Header("Layer Settings")]
        public float intensity = 1f;
        public float minValue = 0f;
        public float maxValue = 1f;
        public float frequency = 1f;
        public float contrast = 1f;
        
        [Header("Assets")]
        public TextAsset generatorAsset;
        public string generatorHash;
        public DefaultAsset pfmAsset;
        public Texture2D inputHeightTexture;

        public Texture2D GeneratedTexture;

        [System.NonSerialized] public float[,] heightmap;
        [System.NonSerialized] public float[,] heightmapInMeters;
        
        [Header("Painted Layer Data")]
        [Tooltip("Serialized painted heightmap data (compressed)")]
        [SerializeField] private byte[] serializedPaintedData;
        [SerializeField] private int paintedDataWidth;
        [SerializeField] private int paintedDataHeight;
        [SerializeField] private float paintedMinValue;
        [SerializeField] private float paintedMaxValue;
        
        [Header("Noise Settings")]
        public Vector2 noiseOffset = Vector2.zero;
        public int octaves = 4;
        public float persistence = 0.5f;
        public float lacunarity = 2f;

        /// <summary>
        /// Stores painted heightmap data in a serializable format
        /// </summary>
        public void StorePaintedData(float[,] paintedHeightmap)
        {
            if (paintedHeightmap == null) return;
            
            paintedDataWidth = paintedHeightmap.GetLength(0);
            paintedDataHeight = paintedHeightmap.GetLength(1);
            
            // Find min/max for normalization
            paintedMinValue = float.MaxValue;
            paintedMaxValue = float.MinValue;
            
            for (int y = 0; y < paintedDataHeight; y++)
            {
                for (int x = 0; x < paintedDataWidth; x++)
                {
                    paintedMinValue = Mathf.Min(paintedMinValue, paintedHeightmap[x, y]);
                    paintedMaxValue = Mathf.Max(paintedMaxValue, paintedHeightmap[x, y]);
                }
            }
            
            // Handle edge case where all values are the same
            if (Mathf.Abs(paintedMaxValue - paintedMinValue) < 0.0001f)
            {
                paintedMaxValue = paintedMinValue + 1f;
            }
            
            // Convert to 16-bit normalized values for storage (better precision than 8-bit)
            serializedPaintedData = new byte[paintedDataWidth * paintedDataHeight * 2];
            float range = paintedMaxValue - paintedMinValue;
            
            int index = 0;
            for (int y = 0; y < paintedDataHeight; y++)
            {
                for (int x = 0; x < paintedDataWidth; x++)
                {
                    float normalized = (paintedHeightmap[x, y] - paintedMinValue) / range;
                    ushort value = (ushort)(Mathf.Clamp01(normalized) * 65535);
                    serializedPaintedData[index++] = (byte)(value & 0xFF);
                    serializedPaintedData[index++] = (byte)((value >> 8) & 0xFF);
                }
            }
            
            Debug.Log($"Painted layer stored: {paintedDataWidth}x{paintedDataHeight}, range: {paintedMinValue} to {paintedMaxValue}");
        }

        /// <summary>
        /// Retrieves stored painted data, optionally resizing to target resolution
        /// </summary>
        public float[,] GetPaintedData(int targetResolution = -1)
        {
            if (serializedPaintedData == null || serializedPaintedData.Length == 0)
                return null;
            
            // Reconstruct from serialized data
            float[,] paintedHeightmap = new float[paintedDataWidth, paintedDataHeight];
            float range = paintedMaxValue - paintedMinValue;
            
            int index = 0;
            for (int y = 0; y < paintedDataHeight; y++)
            {
                for (int x = 0; x < paintedDataWidth; x++)
                {
                    ushort value = (ushort)(serializedPaintedData[index] | (serializedPaintedData[index + 1] << 8));
                    index += 2;
                    float normalized = value / 65535f;
                    paintedHeightmap[x, y] = normalized * range + paintedMinValue;
                }
            }
            
            // Resize if needed
            if (targetResolution > 0 && targetResolution != paintedDataWidth)
            {
                paintedHeightmap = HeightmapHelper.ResizeMap(paintedHeightmap, targetResolution, targetResolution);
            }
            
            return paintedHeightmap;
        }

        /// <summary>
        /// Check if this layer has stored painted data
        /// </summary>
        public bool HasPaintedData()
        {
            return serializedPaintedData != null && serializedPaintedData.Length > 0;
        }

        /// <summary>
        /// Clear stored painted data
        /// </summary>
        public void ClearPaintedData()
        {
            serializedPaintedData = null;
            paintedDataWidth = 0;
            paintedDataHeight = 0;
            paintedMinValue = 0;
            paintedMaxValue = 0;
        }
    }

    public Terrain terrain;
    public MBTerrainGeneratorData generatorData;
    public List<Layer> layers = new List<Layer>();
    
    [Header("Terrain Metrics")]
    public float TerrainHeight = 0f;
    public float TerrainOffset = 0f;
    public Vector2 TerrainSize;
    
    [Header("Base Terrain Info (Read Only)")]
    [Tooltip("Original height range from Generator + PFM layers")]
    public float BaseTerrainHeight = 0f;
    [Tooltip("Original minimum elevation from Generator + PFM layers")]
    public float BaseTerrainOffset = 0f;

    [Header("Painted Layer Settings")]
    [Tooltip("Automatically capture and restore painted edits on regenerate")]
    public bool autoPreservePaintedLayer = true;

    [Header("Height Expansion")]
    [Tooltip("Extra height headroom above terrain for painting (meters)")]
    public float expandHeightAbove = 0f;
    [Tooltip("Extra depth headroom below terrain for digging (meters)")]
    public float expandDepthBelow = 0f;

    private int heightmapResolution;
    private string hash;
    public string CurrentTerrainHash
    {
        get => hash;
        set => hash = value;
    }

    // Cache for the last generated heightmap (before painted layer)
    private float[,] lastGeneratedHeightmap;

    public void Initialize(Terrain tr, MBTerrainGeneratorData genData, DefaultAsset heightmap, TextAsset baseNoise)
    {
        terrain = tr;
        generatorData = genData;
        layers = new List<Layer>();

        var generatorFloatMap = NativeTerrainUtils.ReadHeightmap(AssetDatabase.GetAssetPath(baseNoise));
        var pfmFloatMap = PfmHelper.ReadFloats(AssetDatabase.GetAssetPath(heightmap));

        var terrainGeoData = MBTerrainGeneratorHelpers.CalculateTerrainGeometryData(
            genData.SizeX,
            genData.SizeY,
            genData.PolygonSize);
        
        var res = Mathf.Max(terrainGeoData.NumVerticesX,terrainGeoData.NumVerticesY);
        terrain.terrainData.heightmapResolution = HeightmapHelper.NextPowerOfTwo(terrainGeoData.TerrainSizeX) + 1;
        terrain.terrainData.alphamapResolution = res;
        terrain.terrainData.SetDetailResolution(res,res / 4);
        terrain.detailObjectDensity = 1;
        terrain.detailObjectDistance = 1000;
        terrain.treeDistance = 5000;
        terrain.terrainData.SetDetailScatterMode(DetailScatterMode.InstanceCountMode);
        var genLayer = new Layer
        {
            name = "Generator Hash",
            active = true,
            filterType = FilterType.Generator,
            blendType = BlendType.Add,
            rotation = HeightmapHelper.Rotation.R90,
            flip = HeightmapHelper.Flip.Horizontal,
            GeneratedTexture = Map2D.ConvertFloatArrayToTexture2D(generatorFloatMap),
            generatorAsset = baseNoise,
            generatorHash = generatorData.FullHash,
            useMetersHeight = false
        };

        var pfmLayer = new Layer
        {
            name = "Elevation (PFM)",
            active = true,
            filterType = FilterType.PFM,
            blendType = BlendType.Add,
            rotation = HeightmapHelper.Rotation.R90,
            flip = HeightmapHelper.Flip.Horizontal,
            GeneratedTexture = Map2D.ConvertFloatArrayToTexture2D(pfmFloatMap),
            pfmAsset = heightmap,
            useMetersHeight = false
        };

        layers.Add(genLayer);
        layers.Add(pfmLayer);
    }

    /// <summary>
    /// Captures current terrain paint edits by comparing current heights to last generated heights
    /// </summary>
    public void CapturePaintedLayer()
    {
        if (lastGeneratedHeightmap == null)
        {
            Debug.LogWarning("No generated heightmap cached. Generate heightmap first before capturing painted layer.");
            return;
        }
        
        int resolution = terrain.terrainData.heightmapResolution;
        float[,] unityHeights = terrain.terrainData.GetHeights(0, 0, resolution, resolution);
        
        // Unity terrain uses [y,x] indexing, transpose to our [x,y] format
        float[,] currentHeights = TransposeHeightmap(unityHeights);
        
        // Convert current normalized heights to world units
        float terrainHeight = terrain.terrainData.size.y;
        float terrainOffset = terrain.transform.position.y;
        
        float[,] currentWorldHeights = new float[resolution, resolution];
        for (int y = 0; y < resolution; y++)
        {
            for (int x = 0; x < resolution; x++)
            {
                currentWorldHeights[x, y] = currentHeights[x, y] * terrainHeight + terrainOffset;
            }
        }
        
        // Calculate painted delta (difference from generated)
        float[,] paintedDelta = new float[resolution, resolution];
        bool hasPaintedData = false;
        
        for (int y = 0; y < resolution; y++)
        {
            for (int x = 0; x < resolution; x++)
            {
                paintedDelta[x, y] = currentWorldHeights[x, y] - lastGeneratedHeightmap[x, y];
                if (Mathf.Abs(paintedDelta[x, y]) > 0.001f)
                {
                    hasPaintedData = true;
                }
            }
        }
        
        if (!hasPaintedData)
        {
            Debug.Log("No painted changes detected.");
            return;
        }
        
        // Find or create painted layer
        Layer paintedLayer = layers.Find(l => l.filterType == FilterType.Painted);
        
        if (paintedLayer == null)
        {
            paintedLayer = new Layer
            {
                name = "Painted",
                active = true,
                filterType = FilterType.Painted,
                blendType = BlendType.Add,
                useMetersHeight = true
            };
            layers.Add(paintedLayer);
        }
        
        // Store the painted data
        paintedLayer.StorePaintedData(paintedDelta);
        paintedLayer.heightmap = paintedDelta;
        paintedLayer.heightmapInMeters = paintedDelta;
        paintedLayer.GeneratedTexture = Map2D.ConvertFloatArrayToTexture2D(
            NormalizeForPreview(paintedDelta));
        
        // Calculate height/depth range for the painted layer
        float minDelta = float.MaxValue;
        float maxDelta = float.MinValue;
        for (int y = 0; y < resolution; y++)
        {
            for (int x = 0; x < resolution; x++)
            {
                minDelta = Mathf.Min(minDelta, paintedDelta[x, y]);
                maxDelta = Mathf.Max(maxDelta, paintedDelta[x, y]);
            }
        }
        
        paintedLayer.heightInMeters = Mathf.Max(0, maxDelta);
        paintedLayer.depthInMeters = Mathf.Max(0, -minDelta);
        
        Debug.Log($"Painted layer captured: range {minDelta}m to {maxDelta}m");
        
        #if UNITY_EDITOR
        EditorUtility.SetDirty(this);
        #endif
    }

    /// <summary>
    /// Normalizes heightmap to 0-1 range for texture preview
    /// </summary>
    private float[,] NormalizeForPreview(float[,] heightmap)
    {
        int width = heightmap.GetLength(0);
        int height = heightmap.GetLength(1);
        
        float min = float.MaxValue;
        float max = float.MinValue;
        
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                min = Mathf.Min(min, heightmap[x, y]);
                max = Mathf.Max(max, heightmap[x, y]);
            }
        }
        
        float range = max - min;
        if (range < 0.0001f) range = 1f;
        
        float[,] result = new float[width, height];
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                result[x, y] = (heightmap[x, y] - min) / range;
            }
        }
        
        return result;
    }
// REPLACE the GenerateHeightmap() method in LayeredHeightmapGenerator.cs
// This version correctly calculates terrain height based on ACTIVE layers only

    public float[,] GenerateHeightmap()
    {
        heightmapResolution = terrain.terrainData.heightmapResolution;
        
        // Auto-capture painted layer before regenerating if enabled
        if (autoPreservePaintedLayer && lastGeneratedHeightmap != null)
        {
            CapturePaintedLayer();
        }
        
        // Check which base layers are active
        Layer generatorLayer = layers.Find(l => l.filterType == FilterType.Generator);
        Layer pfmLayer = layers.Find(l => l.filterType == FilterType.PFM);
        
        bool generatorActive = generatorLayer != null && generatorLayer.active;
        bool pfmActive = pfmLayer != null && pfmLayer.active;
        
        Debug.Log($"[LayeredHeightmapGenerator] Active layers: Generator={generatorActive}, PFM={pfmActive}");
        
        // PHASE 1: Process base layers (Generator + PFM) - only active ones
        float[,] baseHeightmap = new float[heightmapResolution, heightmapResolution];
        
        foreach (var layer in layers)
        {
            if (!layer.active) continue;
            if (layer.filterType != FilterType.Generator && layer.filterType != FilterType.PFM) continue;

            float[,] layerHeightmap = ApplyBaseLayer(layer);
            layerHeightmap = ApplyLayerTransformations(layerHeightmap, layer);

            if (layer.invert)
            {
                layerHeightmap = HeightmapHelper.InverseHeightmap(layerHeightmap);
            }

            layer.heightmap = layerHeightmap;
            layer.heightmapInMeters = layerHeightmap;
            layer.GeneratedTexture = Map2D.ConvertFloatArrayToTexture2D(
                HeightmapHelper.NormalizeHeightmap(layerHeightmap, 0, 1));
            
            BlendHeightmapRaw(ref baseHeightmap, layerHeightmap, layer.blendType, layer.intensity);
        }

        // Calculate base terrain metrics from ONLY active base layers
        BaseTerrainHeight = GetHeightRange(baseHeightmap);
        BaseTerrainOffset = GetMinValue(baseHeightmap);
        
        // Log the source of terrain height for debugging
        if (generatorActive && pfmActive)
        {
            Debug.Log($"[LayeredHeightmapGenerator] Base terrain from Generator+PFM: Height={BaseTerrainHeight:F2}m, Offset={BaseTerrainOffset:F2}m");
        }
        else if (generatorActive)
        {
            Debug.Log($"[LayeredHeightmapGenerator] Base terrain from Generator ONLY: Height={BaseTerrainHeight:F2}m, Offset={BaseTerrainOffset:F2}m");
        }
        else if (pfmActive)
        {
            Debug.Log($"[LayeredHeightmapGenerator] Base terrain from PFM ONLY: Height={BaseTerrainHeight:F2}m, Offset={BaseTerrainOffset:F2}m");
        }
        else
        {
            Debug.LogWarning("[LayeredHeightmapGenerator] No base layers active! Terrain will be flat.");
        }
        
        var data = MBTerrainGeneratorHelpers.ParseTerrainCode(hash);
        var geometryData = MBTerrainGeneratorHelpers.CalculateTerrainGeometryData(data.SizeX, data.SizeY, data.PolygonSize);
        TerrainSize = new Vector2(geometryData.TerrainSizeX, geometryData.TerrainSizeY);

        // PHASE 2: Process additional layers (Height, Noise) - NOT Painted yet
        float[,] finalHeightmap = (float[,])baseHeightmap.Clone();
        
        foreach (var layer in layers)
        {
            if (!layer.active) continue;
            if (layer.filterType == FilterType.Generator || 
                layer.filterType == FilterType.PFM ||
                layer.filterType == FilterType.Painted) continue;

            float[,] layerHeightmap = ApplyAdditionalLayer(layer);
            layerHeightmap = ApplyLayerTransformations(layerHeightmap, layer);

            if (layer.invert)
            {
                layerHeightmap = InvertNormalized(layerHeightmap);
            }

            float[,] layerInMeters;
            if (layer.useMetersHeight)
            {
                layerInMeters = ConvertToMetersRange(layerHeightmap, layer.depthInMeters, layer.heightInMeters);
            }
            else
            {
                layerInMeters = ScaleHeightmap(layerHeightmap, layer.intensity);
            }

            layer.heightmap = layerHeightmap;
            layer.heightmapInMeters = layerInMeters;
            layer.GeneratedTexture = Map2D.ConvertFloatArrayToTexture2D(layerHeightmap);

            BlendHeightmapRaw(ref finalHeightmap, layerInMeters, layer.blendType, 
                layer.useMetersHeight ? 1f : layer.intensity);
        }

        // Cache the generated heightmap BEFORE painted layer is applied
        lastGeneratedHeightmap = (float[,])finalHeightmap.Clone();

        // PHASE 3: Apply Painted layer (always last, always Add blend)
        Layer paintedLayer = layers.Find(l => l.filterType == FilterType.Painted && l.active);
        if (paintedLayer != null && paintedLayer.HasPaintedData())
        {
            float[,] paintedData = paintedLayer.GetPaintedData(heightmapResolution);
            if (paintedData != null)
            {
                paintedLayer.heightmap = paintedData;
                paintedLayer.heightmapInMeters = paintedData;
                paintedLayer.GeneratedTexture = Map2D.ConvertFloatArrayToTexture2D(
                    NormalizeForPreview(paintedData));
                
                // Always use Add blend for painted layer
                BlendHeightmapRaw(ref finalHeightmap, paintedData, BlendType.Add, 1f);
                
                Debug.Log("Painted layer restored.");
            }
        }

        // PHASE 4: Calculate final bounds and apply with expansion
        float actualMinHeight = GetMinValue(finalHeightmap);
        float actualMaxHeight = GetMaxValue(finalHeightmap);
        float actualRange = actualMaxHeight - actualMinHeight;
        
        // Apply expansion - extend the range to allow more painting headroom
        float expandedMinHeight = actualMinHeight - expandDepthBelow;
        float expandedMaxHeight = actualMaxHeight + expandHeightAbove;
        float expandedRange = expandedMaxHeight - expandedMinHeight;
        
        // Store the actual terrain metrics (before expansion)
        TerrainHeight = expandedRange;
        TerrainOffset = expandedMinHeight;
        
        Debug.Log($"[LayeredHeightmapGenerator] Final terrain: Height={TerrainHeight:F2}m, Offset={TerrainOffset:F2}m " +
                  $"(actual range: {actualMinHeight:F2} to {actualMaxHeight:F2}, expansion: +{expandHeightAbove}/-{expandDepthBelow})");

        // Normalize to the expanded range (not just actual range)
        // This leaves room at top/bottom for painting
        float[,] normalizedHeightmap = new float[heightmapResolution, heightmapResolution];
        for (int y = 0; y < heightmapResolution; y++)
        {
            for (int x = 0; x < heightmapResolution; x++)
            {
                // Map from world height to 0-1 within expanded range
                normalizedHeightmap[x, y] = (finalHeightmap[x, y] - expandedMinHeight) / expandedRange;
            }
        }
        
        // Unity terrain uses [y,x] indexing, our internal format uses [x,y]
        // Transpose for SetHeights
        float[,] unityHeightmap = TransposeHeightmap(normalizedHeightmap);
        terrain.terrainData.SetHeights(0, 0, unityHeightmap);

        terrain.terrainData.size = new Vector3(geometryData.TerrainSizeX, TerrainHeight, geometryData.TerrainSizeY);
        terrain.transform.position = new Vector3(terrain.transform.position.x, TerrainOffset, terrain.transform.position.z);

        return normalizedHeightmap;
    }
    /// <summary>
    /// Clears the painted layer data
    /// </summary>
    public void ClearPaintedLayer()
    {
        Layer paintedLayer = layers.Find(l => l.filterType == FilterType.Painted);
        if (paintedLayer != null)
        {
            paintedLayer.ClearPaintedData();
            paintedLayer.heightmap = null;
            paintedLayer.heightmapInMeters = null;
            paintedLayer.GeneratedTexture = null;
            Debug.Log("Painted layer cleared.");
        }
    }

    /// <summary>
    /// Removes the painted layer entirely
    /// </summary>
    public void RemovePaintedLayer()
    {
        layers.RemoveAll(l => l.filterType == FilterType.Painted);
        Debug.Log("Painted layer removed.");
    }

    /// <summary>
    /// Applies height expansion to existing terrain without regenerating layers.
    /// Use this to add headroom for painting without losing current painted edits.
    /// </summary>
    public void ApplyHeightExpansion()
    {
        if (terrain == null || terrain.terrainData == null) return;
        
        int resolution = terrain.terrainData.heightmapResolution;
        float[,] unityHeights = terrain.terrainData.GetHeights(0, 0, resolution, resolution);
        float[,] currentHeights = TransposeHeightmap(unityHeights);
        
        // Get current terrain metrics
        float currentTerrainHeight = terrain.terrainData.size.y;
        float currentOffset = terrain.transform.position.y;
        
        // Convert normalized heights back to world heights
        float[,] worldHeights = new float[resolution, resolution];
        float worldMin = float.MaxValue;
        float worldMax = float.MinValue;
        
        for (int y = 0; y < resolution; y++)
        {
            for (int x = 0; x < resolution; x++)
            {
                worldHeights[x, y] = currentHeights[x, y] * currentTerrainHeight + currentOffset;
                worldMin = Mathf.Min(worldMin, worldHeights[x, y]);
                worldMax = Mathf.Max(worldMax, worldHeights[x, y]);
            }
        }
        
        // Calculate new expanded range
        float newMin = worldMin - expandDepthBelow;
        float newMax = worldMax + expandHeightAbove;
        float newRange = newMax - newMin;
        
        if (newRange <= 0) newRange = 1f;
        
        // Renormalize to new range
        float[,] newNormalized = new float[resolution, resolution];
        for (int y = 0; y < resolution; y++)
        {
            for (int x = 0; x < resolution; x++)
            {
                newNormalized[x, y] = (worldHeights[x, y] - newMin) / newRange;
            }
        }
        
        // Apply to terrain
        float[,] unityHeightmap = TransposeHeightmap(newNormalized);
        terrain.terrainData.SetHeights(0, 0, unityHeightmap);
        
        // Update terrain size and position
        terrain.terrainData.size = new Vector3(terrain.terrainData.size.x, newRange, terrain.terrainData.size.z);
        terrain.transform.position = new Vector3(terrain.transform.position.x, newMin, terrain.transform.position.z);
        
        // Update stored metrics
        TerrainHeight = newRange;
        TerrainOffset = newMin;
        
        Debug.Log($"Height expansion applied. New range: {newMin}m to {newMax}m ({newRange}m total)");
        
        #if UNITY_EDITOR
        EditorUtility.SetDirty(this);
        #endif
    }

    private float[,] ApplyBaseLayer(Layer layer)
    {
        LoadTexture(layer);
        return layer.heightmap ?? new float[heightmapResolution, heightmapResolution];
    }

    private float[,] ApplyAdditionalLayer(Layer layer)
    {
        float[,] heightmap = new float[heightmapResolution, heightmapResolution];

        switch (layer.filterType)
        {
            case FilterType.Height:
                LoadTexture(layer);
                heightmap = layer.heightmap ?? new float[heightmapResolution, heightmapResolution];
                heightmap = NormalizeIfNeeded(heightmap);
                break;
                
            case FilterType.Noise:
                heightmap = GeneratePerlinNoise(layer.frequency, layer.noiseOffset, layer.octaves,
                    layer.persistence, layer.lacunarity);
                break;
        }

        return heightmap;
    }

    private float[,] ConvertToMetersRange(float[,] normalized, float depthMeters, float heightMeters)
    {
        int width = normalized.GetLength(0);
        int height = normalized.GetLength(1);
        float[,] result = new float[width, height];
        
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                float value = normalized[x, y];
                result[x, y] = Mathf.Lerp(-depthMeters, heightMeters, value);
            }
        }
        
        return result;
    }

    private float[,] NormalizeIfNeeded(float[,] heightmap)
    {
        int width = heightmap.GetLength(0);
        int height = heightmap.GetLength(1);
        
        float min = float.MaxValue;
        float max = float.MinValue;
        
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                min = Mathf.Min(min, heightmap[x, y]);
                max = Mathf.Max(max, heightmap[x, y]);
            }
        }
        
        if (min >= 0f && max <= 1f && max > min)
            return heightmap;
            
        float[,] result = new float[width, height];
        float range = max - min;
        if (range < 0.0001f) range = 1f;
        
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                result[x, y] = (heightmap[x, y] - min) / range;
            }
        }
        
        return result;
    }

    private float[,] InvertNormalized(float[,] heightmap)
    {
        int width = heightmap.GetLength(0);
        int height = heightmap.GetLength(1);
        float[,] result = new float[width, height];
        
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                result[x, y] = 1f - heightmap[x, y];
            }
        }
        
        return result;
    }

    private float GetHeightRange(float[,] heightmap)
    {
        float min = float.MaxValue;
        float max = float.MinValue;
        
        int width = heightmap.GetLength(0);
        int height = heightmap.GetLength(1);
        
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                min = Mathf.Min(min, heightmap[x, y]);
                max = Mathf.Max(max, heightmap[x, y]);
            }
        }
        
        return max - min;
    }

    private float GetMinValue(float[,] heightmap)
    {
        float min = float.MaxValue;
        
        int width = heightmap.GetLength(0);
        int height = heightmap.GetLength(1);
        
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                min = Mathf.Min(min, heightmap[x, y]);
            }
        }
        
        return min;
    }

    private float GetMaxValue(float[,] heightmap)
    {
        float max = float.MinValue;
        
        int width = heightmap.GetLength(0);
        int height = heightmap.GetLength(1);
        
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                max = Mathf.Max(max, heightmap[x, y]);
            }
        }
        
        return max;
    }

    /// <summary>
    /// Transposes a heightmap array (swaps x and y indices).
    /// Used to convert between Unity's [y,x] terrain format and our internal [x,y] format.
    /// </summary>
    private float[,] TransposeHeightmap(float[,] heightmap)
    {
        int width = heightmap.GetLength(0);
        int height = heightmap.GetLength(1);
        float[,] transposed = new float[height, width];
        
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                transposed[y, x] = heightmap[x, y];
            }
        }
        
        return transposed;
    }

    private float[,] GeneratePerlinNoise(float frequency, Vector2 offset, int octaves, float persistence, float lacunarity)
    {
        float[,] noise = new float[heightmapResolution, heightmapResolution];
    
        for (int y = 0; y < heightmapResolution; y++)
        {
            for (int x = 0; x < heightmapResolution; x++)
            {
                float amplitude = 1f;
                float freq = frequency;
                float noiseValue = 0f;
                float maxValue = 0f;
            
                for (int octave = 0; octave < octaves; octave++)
                {
                    float xCoord = ((float)x / heightmapResolution + offset.x) * freq;
                    float yCoord = ((float)y / heightmapResolution + offset.y) * freq;
                
                    noiseValue += Mathf.PerlinNoise(xCoord, yCoord) * amplitude;
                    maxValue += amplitude;
                
                    amplitude *= persistence;
                    freq *= lacunarity;
                }
            
                noise[x, y] = noiseValue / maxValue;
            }
        }
    
        return noise;
    }

    private float[,] ApplyLayerTransformations(float[,] heightmap, Layer layer)
    {
        if (layer.rotation != HeightmapHelper.Rotation.None)
        {
            heightmap = HeightmapHelper.RotateHeightMap(heightmap, layer.rotation);
        }

        if (layer.flip != HeightmapHelper.Flip.None)
        {
            heightmap = HeightmapHelper.FlipHeightMap(heightmap, layer.flip);
        }

        if (layer.minValue > 0f || layer.maxValue < 1f)
        {
            heightmap = RemapHeightmap(heightmap, 0, 1, layer.minValue, layer.maxValue);
        }

        if (Mathf.Abs(layer.contrast - 1f) > 0.01f)
        {
            heightmap = ApplyContrast(heightmap, layer.contrast);
        }

        return heightmap;
    }

    private float[,] ApplyContrast(float[,] heightmap, float contrast)
    {
        int width = heightmap.GetLength(0);
        int height = heightmap.GetLength(1);
        float[,] result = new float[width, height];
        
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                float value = heightmap[x, y];
                result[x, y] = Mathf.Clamp01((value - 0.5f) * contrast + 0.5f);
            }
        }
        
        return result;
    }

    private float[,] ScaleHeightmap(float[,] heightmap, float scale)
    {
        int width = heightmap.GetLength(0);
        int height = heightmap.GetLength(1);
        float[,] result = new float[width, height];
        
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                result[x, y] = heightmap[x, y] * scale;
            }
        }
        
        return result;
    }

    private void LoadTexture(Layer layer)
    {
        string texturePath = "";

        switch (layer.filterType)
        {
            case FilterType.Height:
                if (layer.inputHeightTexture != null)
                {
                    layer.heightmap = Map2D.ConvertTexture2DToFloatArray(layer.inputHeightTexture);
                    layer.heightmap = HeightmapHelper.ResizeMap(layer.heightmap, 
                        heightmapResolution, heightmapResolution);
                }
                else
                {
                    layer.heightmap = new float[heightmapResolution, heightmapResolution];
                }
                break;
                
            case FilterType.Noise:
                layer.heightmap = new float[heightmapResolution, heightmapResolution];
                break;
                
            case FilterType.Generator:
                texturePath = AssetDatabase.GetAssetPath(layer.generatorAsset);
                var gen = NativeTerrainUtils.ReadHeightmap(texturePath);
                hash = layer.generatorHash;
                layer.heightmap = HeightmapHelper.ResizeMap(gen, heightmapResolution, heightmapResolution);
                break;
                
            case FilterType.PFM:
                texturePath = AssetDatabase.GetAssetPath(layer.pfmAsset);
                var heightmap = PfmHelper.ReadFloats(texturePath);
                
                // if (!string.IsNullOrEmpty(hash))
                // {
                //     var dataHash = MBTerrainGeneratorHelpers.ParseTerrainCode(hash);
                //     var geoData = MBTerrainGeneratorHelpers.CalculateTerrainGeometryData(dataHash.SizeX, dataHash.SizeY, dataHash.PolygonSize);
                //     heightmap = HeightmapHelper.ClampToGeometryData(heightmap, geoData);
                // }

                if (heightmap != null)
                {
                    heightmap = HeightmapHelper.FlipHeightMapHorizontally(heightmap);
                    
                    var data = MBTerrainGeneratorHelpers.ParseTerrainCode(hash);
                    var geoData = MBTerrainGeneratorHelpers.CalculateTerrainGeometryData(
                        data.SizeX, data.SizeY, data.PolygonSize);
                        
                    heightmap = HeightmapHelper.ClampToGeometryData(heightmap, geoData);
                }
                else
                {
                    heightmap = new float[heightmapResolution, heightmapResolution];
                }
                
                
              
                layer.heightmap = HeightmapHelper.ResizeMap(heightmap, 
                    heightmapResolution, heightmapResolution);
                break;
                
            case FilterType.Painted:
                // Painted layer data is loaded from serialized storage
                break;
        }
    }

    private void BlendHeightmapRaw(ref float[,] baseMap, float[,] layerMap, BlendType blendType, float intensity)
    {
        int width = baseMap.GetLength(0);
        int height = baseMap.GetLength(1);
        
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                float baseValue = baseMap[x, y];
                float layerValue = layerMap[x, y];
                float result = 0f;

                switch (blendType)
                {
                    case BlendType.Add:
                        result = baseValue + layerValue;
                        break;

                    case BlendType.Subtract:
                        result = baseValue - layerValue;
                        break;

                    case BlendType.Multiply:
                        result = baseValue * layerValue;
                        break;

                    case BlendType.Max:
                        result = Mathf.Max(baseValue, layerValue);
                        break;

                    case BlendType.Min:
                        result = Mathf.Min(baseValue, layerValue);
                        break;

                    case BlendType.SoftLight:
                        result = SoftLightBlend(baseValue, layerValue);
                        break;

                    case BlendType.HardLight:
                        result = HardLightBlend(baseValue, layerValue);
                        break;

                    case BlendType.Overlay:
                        result = OverlayBlend(baseValue, layerValue);
                        break;

                    case BlendType.Screen:
                        result = ScreenBlend(baseValue, layerValue);
                        break;

                    case BlendType.Lighten:
                        result = Mathf.Max(baseValue, layerValue);
                        break;

                    case BlendType.LinearLight:
                        result = LinearLightBlend(baseValue, layerValue);
                        break;

                    case BlendType.PinLight:
                        result = PinLightBlend(baseValue, layerValue);
                        break;

                    case BlendType.Exclusion:
                        result = ExclusionBlend(baseValue, layerValue);
                        break;

                    case BlendType.Lerp:
                        result = Mathf.Lerp(baseValue, layerValue, intensity);
                        break;

                    case BlendType.InverseLerp:
                        result = Mathf.InverseLerp(baseValue, layerValue, baseValue);
                        break;

                    case BlendType.Premultiply:
                        result = (baseValue * layerValue) * intensity;
                        break;
                        
                    default:
                        result = baseValue;
                        break;
                }

                baseMap[x, y] = result;
            }
        }
    }

    private float SoftLightBlend(float baseValue, float layerValue)
    {
        return (layerValue < 0.5f)
            ? baseValue - (1 - 2 * layerValue) * baseValue * (1 - baseValue)
            : baseValue + (2 * layerValue - 1) * (Mathf.Sqrt(baseValue) - baseValue);
    }

    private float HardLightBlend(float baseValue, float layerValue)
    {
        return (layerValue < 0.5f)
            ? 2 * baseValue * layerValue
            : 1 - 2 * (1 - baseValue) * (1 - layerValue);
    }

    private float OverlayBlend(float baseValue, float layerValue)
    {
        return (baseValue < 0.5f)
            ? 2 * baseValue * layerValue
            : 1 - 2 * (1 - baseValue) * (1 - layerValue);
    }

    private float ScreenBlend(float baseValue, float layerValue)
    {
        return 1 - (1 - baseValue) * (1 - layerValue);
    }

    private float LinearLightBlend(float baseValue, float layerValue)
    {
        return baseValue + 2 * layerValue - 1;
    }

    private float PinLightBlend(float baseValue, float layerValue)
    {
        return (layerValue > 0.5f)
            ? Mathf.Max(baseValue, 2 * (layerValue - 0.5f))
            : Mathf.Min(baseValue, 2 * layerValue);
    }

    private float ExclusionBlend(float baseValue, float layerValue)
    {
        return baseValue + layerValue - 2 * baseValue * layerValue;
    }

    public static float[,] RemapHeightmap(float[,] heightmap, float oldMin, float oldMax, float newMin, float newMax)
    {
        int width = heightmap.GetLength(0);
        int height = heightmap.GetLength(1);
        float[,] remappedHeightmap = new float[width, height];

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                float value = heightmap[x, y];
                float normalized = Mathf.InverseLerp(oldMin, oldMax, value);
                remappedHeightmap[x, y] = Mathf.Lerp(newMin, newMax, normalized);
            }
        }

        return remappedHeightmap;
    }

/// Replace the ExportElevationToPFM method in LayeredHeightmapGenerator.cs with this version:
//
// IMPORTANT CONCEPT:
// The Generator layer represents M&B's base terrain which is generated at ORIGIN (Y=0).
// The generator heightmap values ARE the actual world heights - no offset needed.
//
// Our Unity terrain may be positioned anywhere (terrain.transform.position.y) 
// and has a height range (TerrainHeight).
//
// Formula:
//   worldHeight = normalizedUnityHeight * TerrainHeight + terrain.transform.position.y
//   elevationDelta = worldHeight - generatorHeight
//
// The elevationDelta is what M&B adds on top of its generated terrain to recreate our scene.

    public void ExportElevationToPFM(string outputPath)
    {
        int resolution = terrain.terrainData.heightmapResolution;
        float[,] currentHeights = terrain.terrainData.GetHeights(0, 0, resolution, resolution);

        // TerrainHeight = the vertical range of our terrain
        // Use stored value as it accounts for height expansion
        float terrainHeight = TerrainHeight;
        
        // Fallback if not yet generated
        if (terrainHeight <= 0)
        {
            terrainHeight = terrain.terrainData.size.y;
            Debug.LogWarning("[LayeredHeightmapGenerator] Using terrain.size.y as fallback - TerrainHeight is 0");
        }
        
        // The terrain's Y position in world space (where the "bottom" sits)
        float terrainY = terrain.transform.position.y;
        
        Debug.Log($"[LayeredHeightmapGenerator] Export: TerrainHeight={terrainHeight:F2}m, TerrainY={terrainY:F2}m");

        // Convert normalized Unity heights [0-1] to world heights
        float[,] worldHeights = new float[resolution, resolution];
        for (int y = 0; y < resolution; y++)
        {
            for (int x = 0; x < resolution; x++)
            {
                // normalizedHeight * heightRange + baseY = worldHeight
                worldHeights[y, x] = currentHeights[y, x] * terrainHeight + terrainY;
            }
        }

        Layer generatorLayer = layers.Find(l => l.filterType == FilterType.Generator && l.active);
        if (generatorLayer == null)
        {
            Debug.LogError("No active Generator layer found. Cannot compute elevation delta.");
            return;
        }

        if (generatorLayer.heightmap == null && generatorLayer.generatorAsset != null)
        {
            string genPath = UnityEditor.AssetDatabase.GetAssetPath(generatorLayer.generatorAsset);
            generatorLayer.heightmap = NativeTerrainUtils.ReadHeightmap(genPath);
        }

        // Generator heightmap values ARE world heights (generator terrain is at origin Y=0)
        float[,] genHeightmap = HeightmapHelper.ResizeMap(generatorLayer.heightmap, resolution, resolution);

        // Compute elevation delta: what we need to ADD to generator to get our terrain
        // Generator is at origin, so its values are direct world heights
        float[,] elevationDelta = new float[resolution, resolution];
        for (int y = 0; y < resolution; y++)
        {
            for (int x = 0; x < resolution; x++)
            {
                elevationDelta[y, x] = worldHeights[y, x] - genHeightmap[x, y];
            }
        }

        // elevationDelta = HeightmapHelper.FlipHeightMapHorizontally(elevationDelta);
        //
        // Layer pfmLayer = layers.Find(l => l.filterType == FilterType.PFM);
        // if (pfmLayer?.pfmAsset != null)
        // {
        //     string originalPath = UnityEditor.AssetDatabase.GetAssetPath(pfmLayer.pfmAsset);
        //     var (origWidth, origHeight) = PFMImageHandler.GetDimensions(originalPath);
        //
        //     if (origWidth > 0 && origHeight > 0 &&
        //         (origWidth != resolution || origHeight != resolution))
        //     {
        //         elevationDelta = HeightmapHelper.ResizeMap(elevationDelta, origHeight, origWidth);
        //     }
        // }

        PfmHelper.WriteFloats(elevationDelta, outputPath);
    }

    public string GetDefaultExportPath()
    {
        Layer pfmLayer = layers.Find(l => l.filterType == FilterType.PFM);
        if (pfmLayer?.pfmAsset != null)
        {
            return UnityEditor.AssetDatabase.GetAssetPath(pfmLayer.pfmAsset);
        }

        return null;
    }
}
