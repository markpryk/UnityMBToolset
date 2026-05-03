using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Static helper class for M&B terrain and scene data export operations.
/// Provides clean API for exporting splatmaps, elevation, and props.
/// </summary>
public static class MBExportHelpers
{
    #region Enums
    
    public enum SplatmapBlendMode
    {
        /// <summary>Export Unity values directly (normalized, sum to 1)</summary>
        Direct,
        /// <summary>Convert back to M&B independent layers using visibility formula</summary>
        Denormalize,
        /// <summary>Base layer = 1.0, others overlay on top (recommended)</summary>
        MBNative
    }
    
    public enum ResolutionMode
    {
        Native,
        Custom,
        MatchGenerator,
        MatchOriginalPFM,  // Match the original elevation PFM dimensions
        MatchSplatmaps     // Match exported splatmap dimensions (reads from existing PGM)
    }
    
    #endregion
    
    #region Constants
    
    /// <summary>M&B ground type names indexed by layer</summary>
    public static readonly string[] GroundTypeNames = {
        "gray_stone",   // 0
        "brown_stone",  // 1
        "turf",         // 2
        "steppe",       // 3
        "snow",         // 4
        "earth",        // 5
        "desert",       // 6
        "forest",       // 7
        "pebbles",      // 8
        "village",      // 9
        "path"          // 10
    };
    
    #endregion
    
    #region Splatmap Export Settings
    
    /// <summary>Settings for splatmap export operation</summary>
    [Serializable]
    public class SplatmapExportSettings
    {
        public SplatmapBlendMode blendMode = SplatmapBlendMode.MBNative;
        public ResolutionMode resolutionMode = ResolutionMode.MatchGenerator;
        public int customWidth = 82;
        public int customHeight = 82;
        public bool useColumnMajor = false;
        public bool flipHorizontal = false;
        public bool flipVertical = true;
        public bool applyEdgeFeathering = true;
        public float featherRadius = 1f;        
        public float featherThreshold = 0.1f;
        public float featherValue = 0.5f;
        public int selectedLayer = -1; // -1 = all layers
        
        public SplatmapExportSettings Clone()
        {
            return (SplatmapExportSettings)MemberwiseClone();
        }
    }
    
    #endregion
    
    #region Elevation Export Settings
    
    /// <summary>Settings for elevation export operation</summary>
    [Serializable]
    public class ElevationExportSettings
    {
        public ResolutionMode resolutionMode = ResolutionMode.MatchGenerator;
        public int customWidth = 82;
        public int customHeight = 82;
        public bool flipHorizontal = true; // M&B expects horizontal flip (default true)
        
        public ElevationExportSettings Clone()
        {
            return (ElevationExportSettings)MemberwiseClone();
        }
    }
    
    #endregion
    
    #region Export Results
    
    /// <summary>Result of an export operation</summary>
    public class ExportResult
    {
        public bool success;
        public string message;
        public List<string> exportedFiles = new List<string>();
        public Exception exception;
        
        public static ExportResult Success(string message, params string[] files)
        {
            return new ExportResult
            {
                success = true,
                message = message,
                exportedFiles = new List<string>(files)
            };
        }
        
        public static ExportResult Failure(string message, Exception ex = null)
        {
            return new ExportResult
            {
                success = false,
                message = message,
                exception = ex
            };
        }
    }
    
    #endregion
    
    #region Splatmap Export
    
    /// <summary>
    /// Export terrain splatmaps to PGM files for M&B.
    /// 
    /// If a decorator with a vegetation mask is provided, the mask is
    /// subtracted from all exported PGM layers. This creates holes where
    /// M&B's engine will regenerate base terrain from the hash, matching
    /// the decorator's overlay punch-through behavior.
    /// </summary>
    public static ExportResult ExportSplatmaps(
        Terrain terrain, 
        string outputDirectory, 
        SplatmapExportSettings settings,
        LayeredHeightmapGenerator generator = null,
        MBTerrainDecorator decorator = null)
    {
        if (terrain == null)
            return ExportResult.Failure("Terrain is null");
        
        if (string.IsNullOrEmpty(outputDirectory))
            return ExportResult.Failure("Output directory is empty");
        
        try
        {
            Directory.CreateDirectory(outputDirectory);
            
            TerrainData td = terrain.terrainData;
            int alphaWidth = td.alphamapWidth;
            int alphaHeight = td.alphamapHeight;
            float[,,] splatmaps = td.GetAlphamaps(0, 0, alphaWidth, alphaHeight);
            int layerCount = td.terrainLayers.Length;
            
            // Determine export resolution
            int exportWidth, exportHeight;
            GetSplatmapExportResolution(settings, generator, alphaWidth, alphaHeight, 
                out exportWidth, out exportHeight);
            
            List<string> exportedFiles = new List<string>();
            
            // Where the mask is active, we zero out PGM values so M&B 
            // regenerates base terrain from the hash in those areas.
            float[,] vegetationMask = null;
            
            if (decorator != null 
                && decorator.vegetationMaskTexture != null 
                && decorator.vegetationMaskStrength > 0f)
            {
                vegetationMask = BakeVegetationMask(
                    decorator, alphaWidth, alphaHeight);
                
                if (vegetationMask != null)
                    Debug.Log($"[MBExportHelpers] Vegetation mask loaded from decorator " +
                              $"(strength: {decorator.vegetationMaskStrength:F2}) - " +
                              $"will subtract from all PGM layers.");
            }
            
            // Export each layer
            for (int layer = 0; layer < layerCount; layer++)
            {
                // if (settings.selectedLayer >= 0 && layer != settings.selectedLayer)
                //     continue;
                
                // Extract layer data  
                // Note: GetAlphamaps returns [y, x, layer], ExtractLayerData
                // converts to [x, y] for our processing pipeline
                float[,] layerData = ExtractLayerData(splatmaps, layer, alphaWidth, alphaHeight);
                
         

                // Apply blending mode conversion
                layerData = ApplyBlendMode(layerData, splatmaps, layer, layerCount, settings.blendMode);
                
                if (vegetationMask != null)
                {
                    int w = layerData.GetLength(0);
                    int h = layerData.GetLength(1);
                    for (int y = 0; y < h; y++)
                    {
                        for (int x = 0; x < w; x++)
                        {
                            // Where vegetation mask = 1 → zero out PGM (hole → base shows)
                            // Where vegetation mask = 0 → keep painted overlay value
                            layerData[x, y] *= (1f - vegetationMask[y, x]);
                        }
                    }
                }

                // Resize if needed
                if (exportWidth != alphaWidth || exportHeight != alphaHeight)
                {
                    layerData = HeightmapHelper.ResizeBilinear(layerData, exportWidth, exportHeight);
                }
                
                // Apply transforms
                if (settings.flipHorizontal) 
                    layerData = HeightmapHelper.FlipHeightMapHorizontally(layerData);
                if (settings.flipVertical) 
                    layerData = HeightmapHelper.FlipHeightMapVertically(layerData);
                
                // Apply edge feathering
                if (settings.applyEdgeFeathering)
                {
                    layerData = ApplyEdgeFeathering(layerData, settings.featherRadius, 
                        settings.featherThreshold, settings.featherValue);
                }
                
                // Generate filename
                string groundType = layer < GroundTypeNames.Length 
                    ? GroundTypeNames[layer] : $"unknown_{layer}";
                string filename = $"layer_{groundType}.pgm";
                string filepath = Path.Combine(outputDirectory, filename);
                
                // Write PGM
                PgmHelper.WritePgm(layerData, filepath, settings.useColumnMajor);
                exportedFiles.Add(filepath);
            }
            
            return ExportResult.Success(
                $"Exported {exportedFiles.Count} splatmap(s) to {outputDirectory}" +
                (vegetationMask != null ? " (with vegetation mask subtraction)" : ""),
                exportedFiles.ToArray());
        }
        catch (Exception ex)
        {
            return ExportResult.Failure($"Splatmap export failed: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Reads the decorator's vegetation mask texture and bakes it to a
    /// float[,] array at the specified resolution. Applies channel 
    /// selection, strength, and invert settings from the decorator.
    /// 
    /// Returns float[x, y] where 1.0 = full mask (zero out PGM here),
    /// 0.0 = no mask (keep PGM value).
    /// 
    /// Array indexing matches ExtractLayerData: [x, y] where x = column, 
    /// y = row, so it can be multiplied directly against layer data.
    /// </summary>
    private static float[,] BakeVegetationMask(
        MBTerrainDecorator decorator, int width, int height)
    {
        if (decorator == null || decorator.vegetationMaskTexture == null)
            return null;
        
        var tex = decorator.vegetationMaskTexture;
        float strength = decorator.vegetationMaskStrength;
        bool invert = decorator.vegetationMaskInvert;
        
        // Determine which channel to read
        int channelIdx = 0; // default R
        switch (decorator.vegetationMaskChannel)
        {
            case MBTerrainDecorator.ImageChannel.r: channelIdx = 0; break;
            case MBTerrainDecorator.ImageChannel.g: channelIdx = 1; break;
            case MBTerrainDecorator.ImageChannel.b: channelIdx = 2; break;
            case MBTerrainDecorator.ImageChannel.a: channelIdx = 3; break;
        }
        
        float[,] mask = new float[width, height];
        
        for (int y = 0; y < height; y++)
        {
            // UV coordinates matching ExtractLayerData's [x, y] convention
            float v = (float)y / (height - 1);
            
            for (int x = 0; x < width; x++)
            {
                float u = (float)x / (width - 1);
                
                // Sample the mask texture with bilinear filtering
                Color pixel = tex.GetPixelBilinear(u, v);
                float value = pixel[channelIdx];
                
                if (invert) value = 1f - value;
                value *= strength;
                
                mask[x, y] = Mathf.Clamp01(value);
            }
        }
        
        return mask;
    }


    /// <summary>
    /// Check if a layer is fully white (value >= threshold for all pixels).
    /// Used to detect unpainted default state where layer 0 has full coverage.
    /// </summary>
    private static bool IsLayerFullyWhite(float[,] layerData, int width, int height, float threshold = 0.999f)
    {
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                if (layerData[x, y] < threshold)
                {
                    return false;
                }
            }
        }
        return true;
    }

    public static ExportResult ExportSplatmapLayer(
        Terrain terrain,
        int layerIndex,
        string outputPath,
        SplatmapExportSettings settings,
        LayeredHeightmapGenerator generator = null,
        MBTerrainDecorator decorator = null)
    {
        var layerSettings = settings.Clone();
        layerSettings.selectedLayer = layerIndex;
        
        string directory = Path.GetDirectoryName(outputPath);
        var result = ExportSplatmaps(terrain, directory, layerSettings, generator, decorator);
        
        if (result.success && result.exportedFiles.Count == 1)
        {
            string exportedFile = result.exportedFiles[0];
            if (exportedFile != outputPath && File.Exists(exportedFile))
            {
                if (File.Exists(outputPath))
                    File.Delete(outputPath);
                File.Move(exportedFile, outputPath);
                result.exportedFiles[0] = outputPath;
            }
        }
        
        return result;
    }
    
    /// <summary>
    /// Get export resolution for splatmaps based on settings.
    /// </summary>
    public static void GetSplatmapExportResolution(
        SplatmapExportSettings settings,
        LayeredHeightmapGenerator generator,
        int nativeWidth, int nativeHeight,
        out int exportWidth, out int exportHeight)
    {
        exportWidth = nativeWidth;
        exportHeight = nativeHeight;
        
        switch (settings.resolutionMode)
        {
            case ResolutionMode.Custom:
                exportWidth = settings.customWidth;
                exportHeight = settings.customHeight;
                break;
                
            case ResolutionMode.MatchGenerator:
                if (generator != null && !string.IsNullOrEmpty(generator.CurrentTerrainHash))
                {
                    var data = MBTerrainGeneratorHelpers.ParseTerrainCode(generator.CurrentTerrainHash);
                    if (data != null)
                    {
                        var geoData = MBTerrainGeneratorHelpers.CalculateTerrainGeometryData(
                            data.SizeX, data.SizeY, data.PolygonSize);
                        exportWidth = geoData.NumVerticesX;
                        exportHeight = geoData.NumVerticesY;
                        Debug.Log($"[MBExportHelpers] Resolution from generator hash: {exportWidth}x{exportHeight}");
                    }
                }
                break;
                
            case ResolutionMode.MatchOriginalPFM:
                if (generator != null)
                {
                    string pfmPath = generator.GetDefaultExportPath();
                    if (!string.IsNullOrEmpty(pfmPath))
                    {
                        #if UNITY_EDITOR
                        pfmPath = Path.GetFullPath(pfmPath);
                        #endif
                        
                        if (File.Exists(pfmPath))
                        {
                            var (pfmWidth, pfmHeight) = PfmHelper.GetDimensions(pfmPath);
                            if (pfmWidth > 0 && pfmHeight > 0)
                            {
                                exportWidth = pfmWidth;
                                exportHeight = pfmHeight;
                                Debug.Log($"[MBExportHelpers] Resolution from original PFM: {exportWidth}x{exportHeight}");
                            }
                        }
                    }
                }
                break;
        }
        
        Debug.Log($"[MBExportHelpers] Splatmap export resolution: {exportWidth}x{exportHeight} (mode: {settings.resolutionMode})");
    }
    
    private static float[,] ExtractLayerData(float[,,] splatmaps, int layer, int width, int height)
    {
        float[,] result = new float[width, height];
        
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                result[x, y] = splatmaps[y, x, layer];
            }
        }
        
        return result;
    }
    
    private static float[,] ApplyBlendMode(
        float[,] layerData, 
        float[,,] allLayers, 
        int layerIndex, 
        int layerCount,
        SplatmapBlendMode mode)
    {
        int width = layerData.GetLength(0);
        int height = layerData.GetLength(1);
        float[,] result = new float[width, height];
        
        switch (mode)
        {
            case SplatmapBlendMode.Direct:
                return layerData;
                
            case SplatmapBlendMode.MBNative:
                if (layerIndex == 0)
                {
                    for (int y = 0; y < height; y++)
                        for (int x = 0; x < width; x++)
                            result[x, y] = 1.0f;
                    return result;
                }
                return layerData;
                
            case SplatmapBlendMode.Denormalize:
                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        float visibilityProduct = 1.0f;
                        for (int i = layerCount - 1; i > layerIndex; i--)
                        {
                            visibilityProduct *= (1.0f - allLayers[y, x, i]);
                        }
                        
                        if (visibilityProduct > 0.001f)
                        {
                            result[x, y] = layerData[x, y] / visibilityProduct;
                        }
                        else
                        {
                            result[x, y] = layerData[x, y] > 0 ? 1.0f : 0.0f;
                        }
                        result[x, y] = Mathf.Clamp01(result[x, y]);
                    }
                }
                return result;
                
            default:
                return layerData;
        }
    }
    
    /// <summary>
    /// Apply edge feathering to smooth hard transitions.
    /// Uses distance-based calculation for float radius support.
    /// </summary>
    private static float[,] ApplyEdgeFeathering(float[,] data, float radius, float threshold, float featherValue)
    {
        int width = data.GetLength(0);
        int height = data.GetLength(1);
        float[,] result = (float[,])data.Clone();
    
        int radiusInt = Mathf.CeilToInt(radius);
        float radiusSq = radius * radius;
    
        for (int y = radiusInt; y < height - radiusInt; y++)
        {
            for (int x = radiusInt; x < width - radiusInt; x++)
            {
                float center = data[x, y];
            
                bool isHardEdge = false;
                float neighborSum = 0f;
                int neighborCount = 0;
            
                for (int dy = -radiusInt; dy <= radiusInt; dy++)
                {
                    for (int dx = -radiusInt; dx <= radiusInt; dx++)
                    {
                        if (dx == 0 && dy == 0) continue;
                    
                        float distSq = dx * dx + dy * dy;
                        if (distSq > radiusSq) continue;
                    
                        float neighbor = data[x + dx, y + dy];
                        neighborSum += neighbor;
                        neighborCount++;
                    
                        if (Mathf.Abs(center - neighbor) > threshold)
                        {
                            isHardEdge = true;
                        }
                    }
                }
            
                if (isHardEdge && neighborCount > 0)
                {
                    float avgNeighbor = neighborSum / neighborCount;
                    // Blend toward neighbors, not toward arbitrary 0.5
                    // This smooths edges WITHOUT expanding coverage
                    result[x, y] = Mathf.Lerp(center, avgNeighbor, featherValue);
                }
            }
        }
    
        return result;
    }
    
    #endregion
    
    #region Elevation Export
    
    /// <summary>
    /// Export terrain elevation to PFM file with resolution control.
    /// </summary>
    /// <param name="terrain">Source terrain</param>
    /// <param name="generator">Heightmap generator with terrain data</param>
    /// <param name="outputPath">Full path for output PFM file</param>
    /// <param name="settings">Export settings</param>
    /// <param name="splatmapFolder">Optional splatmap folder for MatchSplatmaps mode</param>
    /// <returns>Export result</returns>
    public static ExportResult ExportElevationWithSettings(
        Terrain terrain,
        LayeredHeightmapGenerator generator, 
        string outputPath,
        ElevationExportSettings settings,
        string splatmapFolder = null)
    {
        if (terrain == null)
            return ExportResult.Failure("Terrain is null");
        
        if (generator == null)
            return ExportResult.Failure("LayeredHeightmapGenerator is null");
        
        if (string.IsNullOrEmpty(outputPath))
            return ExportResult.Failure("Output path is empty");
        
        settings ??= new ElevationExportSettings();
        
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath));
            
            // Get terrain data
            int resolution = terrain.terrainData.heightmapResolution;
            float[,] currentHeights = terrain.terrainData.GetHeights(0, 0, resolution, resolution);
            
            float terrainHeight = generator.TerrainHeight;
            if (terrainHeight <= 0)
            {
                terrainHeight = terrain.terrainData.size.y;
                Debug.LogWarning("[MBExportHelpers] Using terrain.size.y as fallback - generator.TerrainHeight is 0");
            }
            
            float terrainY = terrain.transform.position.y;
            
            Debug.Log($"[MBExportHelpers] Elevation Export: TerrainHeight={terrainHeight:F2}m, TerrainY={terrainY:F2}m");
            
            // Convert normalized Unity heights to world heights
            float[,] worldHeights = new float[resolution, resolution];
            for (int y = 0; y < resolution; y++)
            {
                for (int x = 0; x < resolution; x++)
                {
                    worldHeights[y, x] = currentHeights[y, x] * terrainHeight + terrainY;
                }
            }
            
            // Find Generator layer
            LayeredHeightmapGenerator.Layer generatorLayer = null;
            foreach (var layer in generator.layers)
            {
                if (layer.filterType == LayeredHeightmapGenerator.FilterType.Generator && layer.active)
                {
                    generatorLayer = layer;
                    break;
                }
            }
            
            if (generatorLayer == null)
            {
                return ExportResult.Failure("No active Generator layer found. Cannot compute elevation delta.");
            }
            
            // Load generator heightmap if needed
            if (generatorLayer.heightmap == null && generatorLayer.generatorAsset != null)
            {
#if UNITY_EDITOR
                string genPath = AssetDatabase.GetAssetPath(generatorLayer.generatorAsset);
                generatorLayer.heightmap = NativeTerrainUtils.ReadHeightmap(genPath);
#endif
            }
            
            if (generatorLayer.heightmap == null)
            {
                return ExportResult.Failure("Generator heightmap data not available.");
            }
            
            // Resize generator heightmap to match current resolution
            float[,] genHeightmap = HeightmapHelper.ResizeMap(generatorLayer.heightmap, resolution, resolution);
            
            // Compute elevation delta
            float[,] elevationDelta = new float[resolution, resolution];
            for (int y = 0; y < resolution; y++)
            {
                for (int x = 0; x < resolution; x++)
                {
                    elevationDelta[y, x] = worldHeights[y, x] - genHeightmap[x, y];
                }
            }
            
            // Apply horizontal flip if enabled (M&B requires this)
            if (settings.flipHorizontal)
            {
                elevationDelta = HeightmapHelper.FlipHeightMapHorizontally(elevationDelta);
            }
            
            // Determine target resolution based on settings
            int targetWidth, targetHeight;
            GetElevationExportResolution(settings, generator, resolution, resolution,
                out targetWidth, out targetHeight, splatmapFolder);
            
            // Resize if needed
            if (targetWidth != resolution || targetHeight != resolution)
            {
                elevationDelta = HeightmapHelper.ResizeMap(elevationDelta, targetHeight, targetWidth);
                Debug.Log($"[MBExportHelpers] Elevation resized: {resolution}x{resolution} -> {targetWidth}x{targetHeight}");
            }
            
            // Write PFM file
            PfmHelper.WriteFloats(elevationDelta, outputPath);
            Debug.Log($"[MBExportHelpers] Elevation exported to: {outputPath}");
            
            return ExportResult.Success($"Elevation exported to {outputPath}", outputPath);
        }
        catch (Exception ex)
        {
            Debug.LogError($"[MBExportHelpers] Elevation export failed: {ex}");
            return ExportResult.Failure($"Elevation export failed: {ex.Message}", ex);
        }
    }
    
    /// <summary>
    /// Get export resolution for elevation based on settings.
    /// </summary>
    public static void GetElevationExportResolution(
        ElevationExportSettings settings,
        LayeredHeightmapGenerator generator,
        int nativeWidth, int nativeHeight,
        out int exportWidth, out int exportHeight,
        string splatmapFolder = null)
    {
        exportWidth = nativeWidth;
        exportHeight = nativeHeight;
        
        switch (settings.resolutionMode)
        {
            case ResolutionMode.Native:
                // Keep as-is
                break;
                
            case ResolutionMode.Custom:
                exportWidth = settings.customWidth;
                exportHeight = settings.customHeight;
                break;
                
            case ResolutionMode.MatchGenerator:
                if (generator != null && !string.IsNullOrEmpty(generator.CurrentTerrainHash))
                {
                    var data = MBTerrainGeneratorHelpers.ParseTerrainCode(generator.CurrentTerrainHash);
                    if (data != null)
                    {
                        var geoData = MBTerrainGeneratorHelpers.CalculateTerrainGeometryData(
                            data.SizeX, data.SizeY, data.PolygonSize);
                        exportWidth = geoData.NumVerticesX;
                        exportHeight = geoData.NumVerticesY;
                        Debug.Log($"[MBExportHelpers] Elevation resolution from generator hash: {exportWidth}x{exportHeight}");
                    }
                }
                break;
                
            case ResolutionMode.MatchOriginalPFM:
                if (generator != null)
                {
                    string pfmPath = generator.GetDefaultExportPath();
                    if (!string.IsNullOrEmpty(pfmPath))
                    {
#if UNITY_EDITOR
                        pfmPath = Path.GetFullPath(pfmPath);
#endif
                        if (File.Exists(pfmPath))
                        {
                            var (pfmWidth, pfmHeight) = PfmHelper.GetDimensions(pfmPath);
                            if (pfmWidth > 0 && pfmHeight > 0)
                            {
                                exportWidth = pfmWidth;
                                exportHeight = pfmHeight;
                                Debug.Log($"[MBExportHelpers] Elevation resolution from original PFM: {exportWidth}x{exportHeight}");
                            }
                        }
                    }
                }
                break;
                
            case ResolutionMode.MatchSplatmaps:
                // Try to find an existing splatmap PGM and match its dimensions
                if (!string.IsNullOrEmpty(splatmapFolder) && Directory.Exists(splatmapFolder))
                {
                    string[] pgmFiles = Directory.GetFiles(splatmapFolder, "layer_*.pgm");
                    if (pgmFiles.Length > 0)
                    {
                        var (w, h) = ReadPgmDimensions(pgmFiles[0]);
                        if (w > 0 && h > 0)
                        {
                            exportWidth = w;
                            exportHeight = h;
                            Debug.Log($"[MBExportHelpers] Elevation matched to splatmap: {exportWidth}x{exportHeight}");
                        }
                    }
                }
                break;
        }
        
        Debug.Log($"[MBExportHelpers] Elevation export resolution: {exportWidth}x{exportHeight} (mode: {settings.resolutionMode})");
    }
    
    /// <summary>
    /// Legacy export method - uses generator's built-in export
    /// </summary>
    public static ExportResult ExportElevation(
        LayeredHeightmapGenerator generator, 
        string outputPath,
        ElevationExportSettings settings = null,
        string splatmapFolder = null)
    {
        if (generator == null)
            return ExportResult.Failure("LayeredHeightmapGenerator is null");
        
        if (string.IsNullOrEmpty(outputPath))
            return ExportResult.Failure("Output path is empty");
        
        settings ??= new ElevationExportSettings();
        
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath));
            
            // Use the generator's built-in export method
            generator.ExportElevationToPFM(outputPath);
            
            return ExportResult.Success($"Elevation exported to {outputPath}", outputPath);
        }
        catch (Exception ex)
        {
            Debug.LogError($"[MBExportHelpers] Elevation export failed: {ex}");
            return ExportResult.Failure($"Elevation export failed: {ex.Message}", ex);
        }
    }
    
    /// <summary>
    /// Read dimensions from a PGM file header
    /// </summary>
    public static (int width, int height) ReadPgmDimensions(string path)
    {
        try
        {
            using (var reader = new StreamReader(path))
            {
                // Skip magic number (P5 or P2)
                string magic = reader.ReadLine();
                
                // Skip comments
                string line;
                do
                {
                    line = reader.ReadLine();
                } while (line != null && line.StartsWith("#"));
                
                // Parse dimensions
                if (line != null)
                {
                    string[] parts = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length >= 2)
                    {
                        int width = int.Parse(parts[0]);
                        int height = int.Parse(parts[1]);
                        return (width, height);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"Failed to read PGM dimensions: {ex.Message}");
        }
        
        return (0, 0);
    }
    
    /// <summary>
    /// Export elevation to the original PFM file path from generator.
    /// </summary>
    public static ExportResult ExportElevationToOriginal(
        LayeredHeightmapGenerator generator,
        ElevationExportSettings settings = null)
    {
        if (generator == null)
            return ExportResult.Failure("LayeredHeightmapGenerator is null");
        
        string originalPath = generator.GetDefaultExportPath();
        if (string.IsNullOrEmpty(originalPath))
            return ExportResult.Failure("No original PFM path found in generator");
        
        settings ??= new ElevationExportSettings { resolutionMode = ResolutionMode.MatchOriginalPFM };
        
        return ExportElevation(generator, Path.GetFullPath(originalPath), settings);
    }
    
    #endregion
    
    #region Props Export
    
    /// <summary>Settings for props export</summary>
    [Serializable]
    public class PropsExportSettings
    {
        public bool includeProps = true;
        public bool includeEntries = true;
        public bool includeItems = true;
        public bool includePassages = true;
        public bool includePlants = true;
    }
    
    /// <summary>
    /// Export scene props to JSON file.
    /// </summary>
    public static ExportResult ExportProps(MBModule module, string outputPath,Terrain sceneTerrain,MBFloraLibrary floraLib, PropsExportSettings settings = null)
    {
        if (string.IsNullOrEmpty(outputPath))
            return ExportResult.Failure("Output path is empty");
        
        settings = settings ?? new PropsExportSettings();
        
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath));
            
            if (module != null)
            {
                ExporterProps.ExportPropsToJson(module, outputPath,sceneTerrain,floraLib);
                return ExportResult.Success($"Props exported to {outputPath}", outputPath);
            }
            
            List<GameObject> allObjects = new List<GameObject>();
            
            if (settings.includeProps) allObjects.AddRange(SafeFindWithTag("prop"));
            if (settings.includeEntries) allObjects.AddRange(SafeFindWithTag("entry"));
            if (settings.includeItems) allObjects.AddRange(SafeFindWithTag("item"));
            if (settings.includePassages) allObjects.AddRange(SafeFindWithTag("passage"));
            if (settings.includePlants) allObjects.AddRange(SafeFindWithTag("plant"));
            
            WritePropsJson(allObjects, outputPath);
            
            return ExportResult.Success(
                $"Exported {allObjects.Count} objects to {outputPath}", 
                outputPath);
        }
        catch (Exception ex)
        {
            return ExportResult.Failure($"Props export failed: {ex.Message}", ex);
        }
    }
    
    private static void WritePropsJson(List<GameObject> objects, string outputPath)
    {
        StringBuilder sb = new StringBuilder();
        sb.AppendLine("{");
        sb.AppendLine("  \"exportTime\": \"" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "\",");
        sb.AppendLine("  \"objectCount\": " + objects.Count + ",");
        sb.AppendLine("  \"objects\": [");
        
        for (int i = 0; i < objects.Count; i++)
        {
            var obj = objects[i];
            Vector3 pos = obj.transform.position;
            Vector3 rot = obj.transform.eulerAngles;
            Vector3 scale = obj.transform.localScale;
            
            sb.AppendLine("    {");
            sb.AppendLine($"      \"name\": \"{EscapeJson(obj.name)}\",");
            sb.AppendLine($"      \"tag\": \"{obj.tag}\",");
            sb.AppendLine($"      \"position\": [{pos.x:F4}, {pos.y:F4}, {pos.z:F4}],");
            sb.AppendLine($"      \"rotation\": [{rot.x:F4}, {rot.y:F4}, {rot.z:F4}],");
            sb.AppendLine($"      \"scale\": [{scale.x:F4}, {scale.y:F4}, {scale.z:F4}]");
            sb.Append("    }");
            sb.AppendLine(i < objects.Count - 1 ? "," : "");
        }
        
        sb.AppendLine("  ]");
        sb.AppendLine("}");
        
        File.WriteAllText(outputPath, sb.ToString());
    }
    
    private static string EscapeJson(string str)
    {
        return str.Replace("\\", "\\\\").Replace("\"", "\\\"");
    }
    
    #endregion
    
    #region Utility Methods
    
    /// <summary>Get count of objects with specific tag</summary>
    public static int CountObjectsWithTag(string tag)
    {
        try
        {
            return GameObject.FindGameObjectsWithTag(tag).Length;
        }
        catch
        {
            return 0;
        }
    }
    
    /// <summary>Safely find objects with tag</summary>
    public static GameObject[] SafeFindWithTag(string tag)
    {
        try
        {
            return GameObject.FindGameObjectsWithTag(tag);
        }
        catch
        {
            return new GameObject[0];
        }
    }
    #endregion

}
