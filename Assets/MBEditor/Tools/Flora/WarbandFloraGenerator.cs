using System.Collections.Generic;
using System.Linq;
using MBEditor.Tools.Flora.Core;
using UnityEngine;

namespace MBEditor.Tools.Flora.Logic
{
    public static class WarbandFloraGenerator
    {
        // Internal struct to mimic Warband's mbFaunaRec
        private class FaunaRecord
        {
            public FloraLibraryEntry Entry;
            public float Radius;
            public float Threshold;
            public Vector4 Randomness;
            public float Density;
        }

        public static void GenerateDetails(Terrain terrain, MBFloraLibrary library, string terrainCode, List<FloraLibraryEntry> limitToEntries = null, float[,] mask = null, float maskThreshold = 0f)
        {
#if UNITY_EDITOR
            UnityEditor.Undo.RecordObject(terrain.terrainData, "Warband Flora Generation");
#endif
            // 1. Parse Code
            var data = MBTerrainRegenerator.ParseHash(terrainCode);
            
            // 2. Setup PRNG
            RglRandom.Srand((int)data.FloraSeed);
            
            // 3. Derived Properties
            float barrenness = 0.19f; // Default from mbTerrainGenerator::generateLayers
            int regionType = data.TerrainType;
            if (regionType == 3 || regionType == 4) // Desert / Desert Forest (approx based on enum order)
            {
                 // Check exact Warband enum values if possible. 
                 // rt_desert = 3, rt_desert_forest = 4 in some versions?
                 // Let's rely on standard map below.
            }

            // Map Region to Biome for filtering
            // Warband Regions: 
            // 0=Plain, 1=Steppe, 2=Snow, 3=Desert, 4=Forest (Plain), 5=SnowForest, 6=DesertForest, 7=SteppeForest?
            // Need to verify standard Warband enum. 
            // Based on mbTerrainGenerator.cpp:
            // rt_plain=0, rt_steppe=1, rt_snow=2, rt_desert=3, rt_hunter=4?? 
            // Switch case in cpp:
            // rt_plain, rt_steppe, rt_snow, rt_snow_forest, rt_desert, rt_desert_forest, rt_forest, rt_steppe_forest.
            // Let's use a safe mapping.
            
            FloraBiome biomeFilter = MapRegionToBiome(regionType);

            // 4. Generate Flora Sets (Fauna Records)
            RglRandom.Rand(); // mbTerrainGenerator.cpp: generateFloraSets calls rand() at start
            
            int grassPasses = DetermineGrassPasses(regionType);
            if (data.DisableGrass) grassPasses = 0;

            List<FaunaRecord> faunaRecs = new List<FaunaRecord>();
            if (grassPasses > 0)
            {
                GenerateFaunaRecords(faunaRecs, library, biomeFilter, grassPasses, limitToEntries);
            }

            // 5. Populate Details
            var maps = PopulateDetails(terrain, faunaRecs, data, mask, maskThreshold);
            
            foreach (var kvp in maps)
            {
                 terrain.terrainData.SetDetailLayer(0, 0, kvp.Key, kvp.Value);
            }
            
#if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(terrain.terrainData);
#endif
        }

        private static FloraBiome MapRegionToBiome(int regionType)
        {
            // Mappings based on MBTerrainRegenerator.TerrainTypes
            switch (regionType)
            {
                case 3: return FloraBiome.Plain;        // Plain
                case 2: return FloraBiome.Steppe;       // Steppe
                case 4: return FloraBiome.Snow;         // Snow
                case 5: return FloraBiome.Desert;       // Desert
                case 11: return FloraBiome.Plain;       // Plain Forest
                case 12: return FloraBiome.Snow;        // Snow Forest
                case 10: return FloraBiome.Steppe;      // Steppe Forest
                case 13: return FloraBiome.Desert;      // Desert Palms
                default: return FloraBiome.Plain;
            }
        }

        private static int DetermineGrassPasses(int regionType)
        {
            // Logic from mbTerrainGenerator::generateFloraSets
            switch (regionType)
            {
                case 3: return 4;  // Plain
                case 2: return 4;  // Steppe
                case 11: return 4; // Plain Forest
                case 10: return 2; // Steppe Forest
                case 4: return 0;  // Snow
                case 12: return 0; // Snow Forest
                case 5: return 0;  // Desert
                case 13: return 0; // Desert Palms
                default: return 4;
            }
        }

        private static void GenerateFaunaRecords(List<FaunaRecord> records, MBFloraLibrary library, FloraBiome biome, int passes, List<FloraLibraryEntry> limitToEntries)
        {
            // Filter library for Grass + Biome
            IEnumerable<FloraLibraryEntry> candidates = library.GetByCategory(FloraCategory.Detail);
            
            if (limitToEntries != null && limitToEntries.Count > 0)
            {
                // Only consider entries that are in the limit list
                var limitIds = new HashSet<string>(limitToEntries.Select(e => e.EntryID));
                candidates = candidates.Where(e => limitIds.Contains(e.EntryID));
            }

            var suitableEntries = candidates
                .Where(e => e.MatchesBiome(biome))
                .ToList();

            if (suitableEntries.Count == 0) return;

            float densityFactor = 3.0f / (passes + 2.0f);

            for (int i = 0; i < passes; ++i)
            {
                // Simple weighted random pick (Warband logic is complex, approximating with uniform pick from suitable)
                var entry = suitableEntries[RglRandom.Rand(suitableEntries.Count)];
                
                var rec = new FaunaRecord
                {
                    Entry = entry,
                    Randomness = new Vector4(RglRandom.Randf(10000f), RglRandom.Randf(10000f), RglRandom.Randf(10000f), 0),
                    Radius = RglRandom.Randf(50f, 100f),
                    Threshold = RglRandom.Randf(0.05f)
                };

                // Grass specific modifiers from mbTerrainGenerator.cpp
                rec.Radius *= 0.45f;
                rec.Threshold -= 0.11f;

                // Density calc
                // density = (threshold + 0.4f) * rand(1, 2) * (kindDensity / 1000) * 15 * densityFactor
                // We use Entry.DetailDensity as kindDensity proxy (0-1 range vs Warband 0-???)
                // Assuming Entry.DetailDensity 1.0 ~= Warband "Default"
                float kindDensity = entry.DetailDensity * 1000f; 
                rec.Density = (rec.Threshold + 0.4f) * RglRandom.Randf(1.0f, 2.0f) * (kindDensity / 1000.0f) * 15.0f * densityFactor;

                records.Add(rec);
            }
        }

           

        private static Dictionary<int, int[,]> PopulateDetails(Terrain terrain, List<FaunaRecord> records, MBTerrainGeneratorData data, float[,] mask, float maskThreshold)
        {
            TerrainData td = terrain.terrainData;
            int w = td.detailWidth;
            int h = td.detailHeight;
            
            // Map Prototype Index -> 2D Array
            Dictionary<int, int[,]> detailMaps = new Dictionary<int, int[,]>();

            // Initialize maps for used prototypes
            foreach (var rec in records)
            {
                if (rec.Entry.PrototypeIndex >= 0 && !detailMaps.ContainsKey(rec.Entry.PrototypeIndex))
                {
                    detailMaps[rec.Entry.PrototypeIndex] = new int[w, h];
                }
            }

            // Vegetation global params
            float cellSize = (float)data.SizeX / w; // Approximate cell size in world units
            if (cellSize <= 0.01f) cellSize = 2.0f; // Default
            
            float vegetation = data.Vegetation / 100.0f; // Scale 0-127 -> 0.0-1.27
            float vegetationDensity = Mathf.Pow(cellSize, 1.7f) * 0.3f;
            int numPasses = (int)(cellSize * cellSize * 0.8f);

            // Iterate Grid
            for (int y = 0; y < h; ++y)
            {
                for (int x = 0; x < w; ++x)
                {
                    float nx = (float)x / w;
                    float ny = (float)y / h;
                    
                    Vector3 normal = td.GetInterpolatedNormal(nx, ny);
                    float height = td.GetInterpolatedHeight(nx, ny) + terrain.transform.position.y; 

                    float earthIntensity = WarbandFloraMath.ComputeEarthIntensity(normal, 0.19f); // barrenness 0.19 default
                    float greenIntensity = WarbandFloraMath.ComputeGreenIntensity(x, y, earthIntensity);

                    // Mask Check
                    if (mask != null)
                    {
                        // Ensure bounds safety
                        if (x < mask.GetLength(0) && y < mask.GetLength(1))
                        {
                             if (mask[x, y] < maskThreshold) continue;
                        }
                    }

                    // Iterate Records
                    foreach (var rec in records)
                    {
                        if (rec.Entry.PrototypeIndex < 0) continue;

                        Vector4 noise = RglPerlin.Octave(rec.Radius, 0.6f, 3, false, rec.Randomness + new Vector4(x, y, 0, 0));
                        float threshold = (Mathf.Min(noise.x + noise.y + noise.z, 0.5f) - rec.Threshold) * rec.Density * vegetationDensity;

                        // Grass specific:
                        threshold *= 2.0f; // Warband grass density multiplier? Using 2.0 as per cpp
                        if (data.DisableGrass) threshold = 0f;

                        // Warband loop: for (k=0; threshold > rand(); k++)
                        // We interpret this as "add 1 to density for each success"
                        int density = 0;
                        while (threshold > RglRandom.Randf())
                        {
                            // Checks
                            // if (active and green ground check)
                            // Warband: check face->m_layerIntensities[tlt_green] + 0.3f <= rand()
                            // If green intensity is low, harder to spawn
                            if (greenIntensity + 0.3f <= RglRandom.Randf())
                                break;
                            
                            density++;
                            threshold -= 1.0f;
                            
                            if (density >= numPasses) break;
                        }

                        if (density > 0)
                        {
                            detailMaps[rec.Entry.PrototypeIndex][x, y] += density;
                        }
                    }
                }
            }

            return detailMaps;
        }

        
        

        public enum WarbandChannel
        {
            Earth,
            Green,
            RiverBed
        }

        public static float[,] GenerateIntensityMap(int width, int height, string terrainCode, WarbandChannel channel)
        {
            var data = MBTerrainRegenerator.ParseHash(terrainCode);
            RglRandom.Srand((int)data.FloraSeed);

            // Setup
            float[,] map = new float[width, height];
            float cellSize = (float)data.SizeX / width; 
            if (cellSize <= 0.01f) cellSize = 2.0f;
            
            // Need derived props for correct context?
            // Warband calculates standard layers during vertex generation.
            // We can approximate by iterating the grid.
            
            // Note: Use TerrainData from active terrain if possible for normals? 
            // For now, we assume flat-ish projection or need to access the terrain to get normals.
            // Ideally we'd pass the Terrain object, but for a pure map generator we might lack it.
            // Let's rely on MBEditor.Tools.MBTerrainDecorator passing the terrain via its instance if needed,
            // or we accept we might need normals. 
            
            // Actually, intensity depends on normals (Earth). Green depends on Earth + Noise.
            // We can't generate accurate Earth without the heightmap normals.
            // So we really should ask for the Terrain or Heightmap data.
            // But MBTerrainDecorator generates masks *for* the terrain.
            
            // Let's assume we can get the active terrain or pass it. 
            // For the signature `GenerateIntensityMap`, we'll add a Terrain overload or fetch active.
            
            return map; 
        }

        /// <summary>
        /// Generates an intensity map for a specific Warband channel.
        /// Requires the target terrain to calculate slope-based intensities (Earth).
        /// </summary>
        public static float[,] GenerateIntensityMap(Terrain terrain, int width, int height, string terrainCode, WarbandChannel channel)
        {
            float[,] map = new float[width, height];
            
            var data = MBTerrainRegenerator.ParseHash(terrainCode);
            var td = terrain.terrainData;
            
            // Recalculate derived
            float barrenness = 0.19f; 
            
            for (int y = 0; y < height; ++y)
            {
                for (int x = 0; x < width; ++x)
                {
                    float nx = (float)x / width;
                    float ny = (float)y / height;
                    
                    Vector3 normal = td.GetInterpolatedNormal(nx, ny);
                    
                    float earth = WarbandFloraMath.ComputeEarthIntensity(normal, barrenness);
                    
                    if (channel == WarbandChannel.Earth)
                    {
                        map[x, y] = earth;
                    }
                    else if (channel == WarbandChannel.Green)
                    {
                        map[x, y] = WarbandFloraMath.ComputeGreenIntensity(x, y, earth);
                    }
                    else if (channel == WarbandChannel.RiverBed)
                    {
                        float hVal = td.GetInterpolatedHeight(nx, ny) + terrain.transform.position.y;
                        map[x, y] = WarbandFloraMath.IsRiverbed(hVal, data.PlaceRiver) ? 1.0f : 0.0f;
                    }
                }
            }
            
            return map;
        }
    }
}

