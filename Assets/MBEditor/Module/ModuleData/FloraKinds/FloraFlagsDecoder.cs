using System.Numerics;
using UnityEngine;

namespace MountAndBlade.Data
{
    /// <summary>
    /// Decoder for Mount & Blade flora flags.
    /// Converts raw hex flags into human-readable boolean values.
    /// Based on header_flora.py constant definitions.
    /// </summary>
    public static class FloraFlagsDecoder
    {
        // Terrain Condition Flags (where flora spawns)
        private const long fkf_plain = 0x00000004;
        private const long fkf_steppe = 0x00000008;
        private const long fkf_snow = 0x00000010;
        private const long fkf_desert = 0x00000020;
        private const long fkf_plain_forest = 0x00000400;
        private const long fkf_steppe_forest = 0x00000800;
        private const long fkf_snow_forest = 0x00001000;
        private const long fkf_desert_forest = 0x00002000;
        
        // Behavior Flags
        private const long fkf_realtime_lighting = 0x00010000; // Deprecated
        private const long fkf_point_up = 0x00020000;          // Auto-generated quad geometry
        private const long fkf_align_with_ground = 0x00040000; // Align with terrain normal
        private const long fkf_grass = 0x00080000;
        private const long fkf_on_green_ground = 0x00100000;   // Populate on vegetation
        private const long fkf_rock = 0x00200000;
        private const long fkf_tree = 0x00400000;
        private const long fkf_snowy = 0x00800000;
        private const long fkf_guarantee = 0x01000000;         // Always spawn
        private const long fkf_speedtree = 0x02000000;         // Deprecated/non-functional
        private const long fkf_has_colony_props = 0x04000000;  // Colony system enabled
        
        // Density extraction
        private const int density_bits = 32;
        private const long density_mask = 0xFFFF;
        
        /// <summary>
        /// Decodes raw flags string into terrain condition booleans
        /// </summary>
        public static void DecodeTerrainConditions(string rawFlags, out FloraTerrainConditions conditions)
        {
            conditions = new FloraTerrainConditions();
            
            if (string.IsNullOrEmpty(rawFlags))
            {
                Debug.LogWarning("FloraFlagsDecoder: Empty flags string");
                return;
            }
            
            BigInteger flags;
            if (!BigInteger.TryParse(rawFlags, out flags))
            {
                Debug.LogError($"FloraFlagsDecoder: Failed to parse flags: {rawFlags}");
                return;
            }
            
            long flagsLong = (long)(flags & 0xFFFFFFFFFFFFFFFF);
            
            conditions.Plain = (flagsLong & fkf_plain) != 0;
            conditions.Steppe = (flagsLong & fkf_steppe) != 0;
            conditions.Snow = (flagsLong & fkf_snow) != 0;
            conditions.Desert = (flagsLong & fkf_desert) != 0;
            conditions.PlainForest = (flagsLong & fkf_plain_forest) != 0;
            conditions.SteppeForest = (flagsLong & fkf_steppe_forest) != 0;
            conditions.SnowForest = (flagsLong & fkf_snow_forest) != 0;
            conditions.DesertForest = (flagsLong & fkf_desert_forest) != 0;
        }
        
        /// <summary>
        /// Decodes raw flags string into behavior booleans
        /// </summary>
        public static void DecodeBehaviorFlags(string rawFlags, out FloraBehaviorFlags behavior)
        {
            behavior = new FloraBehaviorFlags();
            
            if (string.IsNullOrEmpty(rawFlags))
            {
                Debug.LogWarning("FloraFlagsDecoder: Empty flags string");
                return;
            }
            
            BigInteger flags;
            if (!BigInteger.TryParse(rawFlags, out flags))
            {
                Debug.LogError($"FloraFlagsDecoder: Failed to parse flags: {rawFlags}");
                return;
            }
            
            long flagsLong = (long)(flags & 0xFFFFFFFFFFFFFFFF);
            
            behavior.AlignWithGround = (flagsLong & fkf_align_with_ground) != 0;
            behavior.PointUp = (flagsLong & fkf_point_up) != 0;
            behavior.OnGreenGround = (flagsLong & fkf_on_green_ground) != 0;
            behavior.Guarantee = (flagsLong & fkf_guarantee) != 0;
            behavior.Snowy = (flagsLong & fkf_snowy) != 0;
            behavior.RealtimeLighting = (flagsLong & fkf_realtime_lighting) != 0;
            behavior.SpeedTree = (flagsLong & fkf_speedtree) != 0;
            behavior.HasColonyProps = (flagsLong & fkf_has_colony_props) != 0;
        }
        
        /// <summary>
        /// Decodes raw flags string into type classification booleans
        /// </summary>
        public static void DecodeTypeFlags(string rawFlags, out FloraTypeFlags types)
        {
            types = new FloraTypeFlags();
            
            if (string.IsNullOrEmpty(rawFlags))
            {
                Debug.LogWarning("FloraFlagsDecoder: Empty flags string");
                return;
            }
            
            BigInteger flags;
            if (!BigInteger.TryParse(rawFlags, out flags))
            {
                Debug.LogError($"FloraFlagsDecoder: Failed to parse flags: {rawFlags}");
                return;
            }
            
            long flagsLong = (long)(flags & 0xFFFFFFFFFFFFFFFF);
            
            types.IsGrass = (flagsLong & fkf_grass) != 0;
            types.IsTree = (flagsLong & fkf_tree) != 0;
            types.IsRock = (flagsLong & fkf_rock) != 0;
        }
        
        /// <summary>
        /// Extracts density value from upper bits of flags
        /// </summary>
        public static int ExtractDensity(string rawFlags)
        {
            if (string.IsNullOrEmpty(rawFlags))
            {
                return 0;
            }
            
            BigInteger flags;
            if (!BigInteger.TryParse(rawFlags, out flags))
            {
                Debug.LogError($"FloraFlagsDecoder: Failed to parse flags: {rawFlags}");
                return 0;
            }
            
            return (int)((flags >> density_bits) & density_mask);
        }
        
        /// <summary>
        /// Full decode of all flags into a complete structure
        /// </summary>
        public static FloraDecodedFlags DecodeAll(string rawFlags)
        {
            FloraDecodedFlags decoded = new FloraDecodedFlags();
            
            DecodeTerrainConditions(rawFlags, out decoded.Terrain);
            DecodeBehaviorFlags(rawFlags, out decoded.Behavior);
            DecodeTypeFlags(rawFlags, out decoded.Type);
            decoded.Density = ExtractDensity(rawFlags);
            
            return decoded;
        }
        
        /// <summary>
        /// Gets a human-readable description of what terrain types this flora spawns on
        /// </summary>
        public static string GetTerrainDescription(FloraTerrainConditions conditions)
        {
            var terrains = new System.Collections.Generic.List<string>();
            
            if (conditions.Plain) terrains.Add("Plain");
            if (conditions.Steppe) terrains.Add("Steppe");
            if (conditions.Snow) terrains.Add("Snow");
            if (conditions.Desert) terrains.Add("Desert");
            if (conditions.PlainForest) terrains.Add("Plain Forest");
            if (conditions.SteppeForest) terrains.Add("Steppe Forest");
            if (conditions.SnowForest) terrains.Add("Snow Forest");
            if (conditions.DesertForest) terrains.Add("Desert Forest");
            
            return terrains.Count > 0 ? string.Join(", ", terrains) : "None";
        }
        
        /// <summary>
        /// Gets the primary flora type as a string
        /// </summary>
        public static string GetFloraTypeString(FloraTypeFlags types)
        {
            if (types.IsTree) return "Tree";
            if (types.IsGrass) return "Grass";
            if (types.IsRock) return "Rock";
            return "Other";
        }
    }
    
    // DATA STRUCTURES
    
    [System.Serializable]
    public struct FloraTerrainConditions
    {
        public bool Plain;
        public bool Steppe;
        public bool Snow;
        public bool Desert;
        public bool PlainForest;
        public bool SteppeForest;
        public bool SnowForest;
        public bool DesertForest;
    }
    
    [System.Serializable]
    public struct FloraBehaviorFlags
    {
        public bool AlignWithGround;
        public bool PointUp;
        public bool OnGreenGround;
        public bool Guarantee;
        public bool Snowy;
        public bool RealtimeLighting; // Deprecated
        public bool SpeedTree;        // Deprecated/non-functional
        public bool HasColonyProps;
    }
    
    [System.Serializable]
    public struct FloraTypeFlags
    {
        public bool IsGrass;
        public bool IsTree;
        public bool IsRock;
    }
    
    [System.Serializable]
    public struct FloraDecodedFlags
    {
        public FloraTerrainConditions Terrain;
        public FloraBehaviorFlags Behavior;
        public FloraTypeFlags Type;
        public int Density;
    }
}
