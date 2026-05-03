using System;
using System.Collections;
using System.IO;
using System.Text;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Helper class for regenerating M&B terrain from hash.
/// Orchestrates the full pipeline: hash extraction → heightmap generation → decoration.
/// 
/// Based on Swyter's terrain hash generator analysis and M&B TerrainGenerator.cpp source.
/// </summary>
public static class MBTerrainRegenerator
{
    #region Hash Structure Constants
    
    // From Swyter's analysis - the hash is 48 hex digits (192 bits) split into 6 x 32-bit blocks
    // Block layout (from LSB to MSB when reading the hex string right-to-left):
    // Block 0: terrain_seed (bits 0-30)
    // Block 1: river_seed (bits 0-30), deep_water (bit 31)
    // Block 2: flora_seed (bits 0-30)
    // Block 3: size_x (bits 0-9), size_y (bits 10-19), shade_occlude (bit 30), place_river (bit 31)
    // Block 4: valley (bits 0-6), hill_height (bits 7-13), ruggedness (bits 14-20), vegetation (bits 21-27), region_type (bits 28-31)
    // Block 5: region_detail/polygon_size-2 (bits 0-1), disable_grass (bit 2)
    
    // M&B Engine constraints from TerrainGenerator.cpp
    public const int MIN_NUM_TERRAIN_FACES_PER_AXIS = 40;
    public const int MAX_NUM_TERRAIN_FACES_PER_AXIS = 250;
    
    // Size constraints (10-bit values = 0-1023)
    public const int MIN_SIZE = 80;   // Practical minimum for usable terrain
    public const int MAX_SIZE = 1023; // 10-bit maximum
    
    // Feature constraints (7-bit values = 0-127)
    public const int MIN_FEATURE = 0;
    public const int MAX_FEATURE = 127;
    
    // Polygon size options (2-bit + 2 = 2-5)
    public const int MIN_POLYGON_SIZE = 2;
    public const int MAX_POLYGON_SIZE = 5;
    
    // Seed constraints (31-bit values)
    public const uint MAX_SEED = 0x7FFFFFFF; // 2147483647
    
    #endregion
    
    #region Result Classes
    
    /// <summary>
    /// Result of a terrain regeneration operation
    /// </summary>
    public class RegenerationResult
    {
        public bool success;
        public string message;
        public string hashExtractPath;
        public float heightmapTime;
        public float decoratorTime;
        public float totalTime;
        
        public static RegenerationResult Success(string msg, string extractPath = null)
        {
            return new RegenerationResult { success = true, message = msg, hashExtractPath = extractPath };
        }
        
        public static RegenerationResult Failure(string msg)
        {
            return new RegenerationResult { success = false, message = msg };
        }
    }
    
    /// <summary>
    /// Settings for terrain regeneration
    /// </summary>
    [System.Serializable]
    public class RegenerationSettings
    {
        [Tooltip("Extract fresh hash data from TerrainGenerator tool")]
        public bool extractHashData = true;
        
        [Tooltip("Regenerate the heightmap using LayeredHeightmapGenerator")]
        public bool regenerateHeightmap = true;
        
        [Tooltip("Regenerate splatmaps using MBTerrainDecorator")]
        public bool regenerateDecorator = true;
        
        [Tooltip("Clear existing painted layer before regeneration")]
        public bool clearPaintedLayer = false;
        
        [Tooltip("Directory to extract hash data to (auto-set if empty)")]
        public string hashExtractDirectory = "";
        
        [Tooltip("Show progress dialogs during regeneration")]
        public bool showProgress = true;
    }
    
    #endregion
    
    #region Hash Generation
    
    /// <summary>
    /// Generates a terrain hash from MBTerrainGeneratorData with proper bit packing.
    /// Uses the exact algorithm from Swyter's generator.
    /// </summary>
    public static string GenerateHash(MBTerrainGeneratorData data)
    {
        if (data == null)
            return null;
        
        // Clamp all values to valid ranges
        int sizeX = Mathf.Clamp(data.SizeX, 0, MAX_SIZE);
        int sizeY = Mathf.Clamp(data.SizeY, 0, MAX_SIZE);
        int polygonSize = Mathf.Clamp(data.PolygonSize, MIN_POLYGON_SIZE, MAX_POLYGON_SIZE);
        int terrainType = Mathf.Clamp(data.TerrainType, 0, 15); // 4-bit
        int vegetation = Mathf.Clamp(data.Vegetation, MIN_FEATURE, MAX_FEATURE);
        int ruggedness = Mathf.Clamp(data.Ruggedness, MIN_FEATURE, MAX_FEATURE);
        int valley = Mathf.Clamp(data.Valley, MIN_FEATURE, MAX_FEATURE);
        int hillHeight = Mathf.Clamp(data.HillHeight, MIN_FEATURE, MAX_FEATURE);
        uint terrainSeed = data.TerrainSeed & MAX_SEED;
        uint riverSeed = data.RiverSeed & MAX_SEED;
        uint floraSeed = data.FloraSeed & MAX_SEED;
        
        // Build the 6 x 32-bit blocks
        uint[] blocks = new uint[6];
        
        // Block 0: terrain_seed (bits 0-30)
        blocks[0] = terrainSeed & 0x7FFFFFFF;
        
        // Block 1: river_seed (bits 0-30), deep_water (bit 31)
        blocks[1] = (riverSeed & 0x7FFFFFFF) | (data.DeepWater ? 0x80000000u : 0u);
        
        // Block 2: flora_seed (bits 0-30)
        blocks[2] = floraSeed & 0x7FFFFFFF;
        
        // Block 3: size_x (bits 0-9), size_y (bits 10-19), shade_occlude (bit 30), place_river (bit 31)
        blocks[3] = (uint)((sizeX & 0x3FF) | 
                          ((sizeY & 0x3FF) << 10) | 
                          (data.ShadeOcclude ? (1u << 30) : 0u) | 
                          (data.PlaceRiver ? (1u << 31) : 0u));
        
        // Block 4: valley (bits 0-6), hill_height (bits 7-13), ruggedness (bits 14-20), 
        //          vegetation (bits 21-27), region_type (bits 28-31)
        blocks[4] = (uint)((valley & 0x7F) | 
                          ((hillHeight & 0x7F) << 7) | 
                          ((ruggedness & 0x7F) << 14) | 
                          ((vegetation & 0x7F) << 21) | 
                          ((terrainType & 0xF) << 28));
        
        // Block 5: region_detail/polygon_size-2 (bits 0-1), disable_grass (bit 2)
        blocks[5] = (uint)(((polygonSize - 2) & 0x3) | 
                          (data.DisableGrass ? (1u << 2) : 0u));
        
        // Build the hash string (blocks in reverse order, MSB first)
        StringBuilder sb = new StringBuilder("0x");
        for (int i = 5; i >= 0; i--)
        {
            sb.Append(blocks[i].ToString("x8"));
        }
        
        return sb.ToString();
    }
    
    /// <summary>
    /// Parses a terrain hash into MBTerrainGeneratorData.
    /// Uses the exact algorithm from Swyter's generator.
    /// </summary>
    public static MBTerrainGeneratorData ParseHash(string hash)
    {
        if (string.IsNullOrEmpty(hash))
            return null;
        
        // Remove 0x prefix
        string hex = hash.StartsWith("0x") ? hash.Substring(2) : hash;
        
        // Pad to 48 characters if needed
        hex = hex.PadLeft(48, '0');
        
        if (hex.Length != 48)
            return null;
        
        // Parse the 6 x 32-bit blocks (reading from right to left)
        uint[] blocks = new uint[6];
        for (int i = 0; i < 6; i++)
        {
            string blockHex = hex.Substring(48 - 8 * (i + 1), 8);
            blocks[i] = Convert.ToUInt32(blockHex, 16);
        }
        
        // Extract values using bit masks and shifts
        var data = new MBTerrainGeneratorData
        {
            // Block 0
            TerrainSeed = blocks[0] & 0x7FFFFFFF,
            
            // Block 1
            RiverSeed = blocks[1] & 0x7FFFFFFF,
            DeepWater = (blocks[1] & 0x80000000) != 0,
            
            // Block 2
            FloraSeed = blocks[2] & 0x7FFFFFFF,
            
            // Block 3
            SizeX = (int)(blocks[3] & 0x3FF),
            SizeY = (int)((blocks[3] >> 10) & 0x3FF),
            ShadeOcclude = (blocks[3] & (1u << 30)) != 0,
            PlaceRiver = (blocks[3] & (1u << 31)) != 0,
            
            // Block 4
            Valley = (int)(blocks[4] & 0x7F),
            HillHeight = (int)((blocks[4] >> 7) & 0x7F),
            Ruggedness = (int)((blocks[4] >> 14) & 0x7F),
            Vegetation = (int)((blocks[4] >> 21) & 0x7F),
            TerrainType = (int)((blocks[4] >> 28) & 0xF),
            
            // Block 5
            PolygonSize = (int)((blocks[5] & 0x3) + 2),
            DisableGrass = (blocks[5] & (1u << 2)) != 0
        };
        
        data.FullHash = hash;
        
        return data;
    }
    
    /// <summary>
    /// Validates a terrain hash format.
    /// </summary>
    public static bool ValidateHash(string hash)
    {
        if (string.IsNullOrEmpty(hash))
            return false;
        
        if (!hash.StartsWith("0x"))
            return false;
        
        string hex = hash.Substring(2);
        
        // Must be 40-48 hex characters (can be shorter if leading zeros are omitted)
        if (hex.Length < 40 || hex.Length > 48)
            return false;
        
        // Must be valid hex
        foreach (char c in hex)
        {
            if (!Uri.IsHexDigit(c))
                return false;
        }
        
        return true;
    }
    
    /// <summary>
    /// Calculates the actual in-game terrain size based on hash parameters.
    /// This matches the M&B engine's TerrainGenerator algorithm.
    /// </summary>
    public static (int sizeX, int sizeY, int verticesX, int verticesY, int facesX, int facesY) 
        CalculateActualSize(int scoSizeX, int scoSizeY, int polygonSize)
    {
        // From TerrainGenerator.cpp - clamp faces to valid range
        int numFacesX = Mathf.Clamp(scoSizeX / polygonSize, MIN_NUM_TERRAIN_FACES_PER_AXIS, MAX_NUM_TERRAIN_FACES_PER_AXIS);
        int numFacesY = Mathf.Clamp(scoSizeY / polygonSize, MIN_NUM_TERRAIN_FACES_PER_AXIS, MAX_NUM_TERRAIN_FACES_PER_AXIS);
        
        // Vertices = faces + 1 (like pixels in a heightmap)
        int numVerticesX = numFacesX + 1;
        int numVerticesY = numFacesY + 1;
        
        // Actual terrain size in meters
        int actualSizeX = numFacesX * polygonSize;
        int actualSizeY = numFacesY * polygonSize;
        
        return (actualSizeX, actualSizeY, numVerticesX, numVerticesY, numFacesX, numFacesY);
    }
    
    /// <summary>
    /// Generates a random seed value within valid M&B range.
    /// </summary>
    public static uint GenerateRandomSeed()
    {
        // M&B uses 31-bit seeds (0 to 2147483647)
        return (uint)UnityEngine.Random.Range(0, int.MaxValue);
    }
    
    #endregion
    
    #region Terrain Type Helpers
    
    public static readonly (int id, string name)[] TerrainTypes = new[]
    {
        (2, "Steppe"),
        (3, "Plain"),
        (4, "Snow"),
        (5, "Desert"),
        (10, "Steppe Forest"),
        (11, "Plain Forest"),
        (12, "Snow Forest"),
        (13, "Desert Palms")
    };
    
    public static string GetTerrainTypeName(int typeId)
    {
        foreach (var t in TerrainTypes)
        {
            if (t.id == typeId)
                return t.name;
        }
        return $"Unknown ({typeId})";
    }
    
    public static int GetTerrainTypeIndex(int typeId)
    {
        for (int i = 0; i < TerrainTypes.Length; i++)
        {
            if (TerrainTypes[i].id == typeId)
                return i;
        }
        return 1; // Default to Plain
    }
    
    #endregion
    
    #region Regeneration
    
    /// <summary>
    /// Validates that all required components are present for regeneration.
    /// </summary>
    public static (bool isValid, string message) ValidateForRegeneration(Terrain terrain)
    {
        if (terrain == null)
            return (false, "No terrain assigned");
        
        var heightmapGenerator = terrain.GetComponent<LayeredHeightmapGenerator>();
        if (heightmapGenerator == null)
            return (false, "LayeredHeightmapGenerator not found");
        
        string hash = heightmapGenerator.CurrentTerrainHash;
        if (string.IsNullOrEmpty(hash))
            return (false, "No terrain hash configured");
        
        if (!ValidateHash(hash))
            return (false, "Invalid terrain hash");
        
        string toolPath = MBPathHelpers.MBTerrainGeneratorToolPath();
        if (!File.Exists(toolPath))
            return (false, $"TerrainGenerator tool not found at: {toolPath}");
        
        var decorator = terrain.GetComponent<MBTerrainDecorator>();
        string decoratorStatus = decorator != null ? "present" : "not found (optional)";
        
        return (true, $"Ready to regenerate. Hash: {hash.Substring(0, Math.Min(18, hash.Length))}..., Decorator: {decoratorStatus}");
    }
    
    #endregion
}