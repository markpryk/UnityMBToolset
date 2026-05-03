using System.Collections.Generic;
using System.IO;
using System.Linq;
using MountAndBlade.Data;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Exports Unity terrain tree instances and detail layers back to M&amp;B
/// scene flora format (same JSON structure as ExporterProps).
///
/// Coordinate convention:
///   Base axis swap: Unity (X, Y, Z) → Warband (X, Z, Y)  [Y↔Z swap]
///   Then 180° rotation around Warband Z (up) → negates both X and Y
///   Final: Warband = (-Unity.X, -Unity.Z, Unity.Y)
///
/// This matches the ExporterProps flipper behavior:
///   flipper.localScale = (-1, 1, 1)  +  flipper.eulerAngles = (-90, 180, 0)
///   which produces the same (-X, -Z, Y) mapping.
/// </summary>
public static class ExporterTerrainFlora
{
    /// <summary>
    /// Export all terrain flora (trees + optionally details) to the given JSON path.
    /// </summary>
    public static void ExportTerrainFlora(
        Terrain terrain,
        MBFloraLibrary library,
        string jsonPath,
        bool exportDetails = false,
        int detailDensityThreshold = 1)
    {
        if (terrain == null)
        {
            Debug.LogError("[FloraExport] Terrain is null.");
            return;
        }

        if (library == null)
        {
            Debug.LogError("[FloraExport] Flora library is null. Build and sync the library first.");
            return;
        }

        var floraList = new List<MBScenePropEntityData>();

        var treeEntryLookup = BuildPrototypeLookup(library, FloraCategory.Tree);
        var detailEntryLookup = exportDetails ? BuildPrototypeLookup(library, FloraCategory.Detail) : null;

        int treeCount = ExportTreeInstances(terrain, treeEntryLookup, floraList);

        int detailCount = 0;
        if (exportDetails)
            detailCount = ExportDetailLayers(terrain, detailEntryLookup, floraList, detailDensityThreshold);

        if (floraList.Count == 0)
        {
            Debug.LogWarning("[FloraExport] No terrain flora found to export.");
            return;
        }

        string json = JsonConvert.SerializeObject(floraList, Formatting.Indented);
        File.WriteAllText(jsonPath, json);

        Debug.Log($"[FloraExport] Exported {treeCount} trees + {detailCount} details " +
                  $"({floraList.Count} total) to {jsonPath}");
    }

    /// <summary>
    /// Returns the flora entity list without writing to disk - useful for merging
    /// with ExporterProps output before a single write.
    /// </summary>
    public static List<MBScenePropEntityData> CollectTerrainFlora(
        Terrain terrain,
        MBFloraLibrary library,
        bool exportDetails = false,
        int detailDensityThreshold = 1)
    {
        if (terrain == null || library == null) return new List<MBScenePropEntityData>();

        var floraList = new List<MBScenePropEntityData>();
        var treeEntryLookup = BuildPrototypeLookup(library, FloraCategory.Tree);

        ExportTreeInstances(terrain, treeEntryLookup, floraList);

        if (exportDetails)
        {
            var detailEntryLookup = BuildPrototypeLookup(library, FloraCategory.Detail);
            ExportDetailLayers(terrain, detailEntryLookup, floraList, detailDensityThreshold);
        }

        return floraList;
    }

    //  Tree Instance Export

    private static int ExportTreeInstances(
        Terrain terrain,
        Dictionary<int, FloraLibraryEntry> lookup,
        List<MBScenePropEntityData> output)
    {
        var td = terrain.terrainData;
        var trees = td.treeInstances;
        Vector3 terrainPos = terrain.transform.position;
        Vector3 terrainSize = td.size;
        int count = 0;

        for (int i = 0; i < trees.Length; i++)
        {
            TreeInstance tree = trees[i];

            if (!lookup.TryGetValue(tree.prototypeIndex, out var entry))
            {
                Debug.LogWarning($"[FloraExport] Tree prototype index {tree.prototypeIndex} " +
                                 "has no matching library entry - skipping.");
                continue;
            }

            // TreeInstance.position is normalized [0,1] relative to terrain
            Vector3 unityWorldPos = new Vector3(
                terrainPos.x + tree.position.x * terrainSize.x,
                terrainPos.y + tree.position.y * terrainSize.y,
                terrainPos.z + tree.position.z * terrainSize.z
            );

            // Position: Unity → Warband with 180° global rotation
            Vector3 wbPos = UnityToWarband(unityWorldPos);

            // Scale: tree widthScale = XZ, heightScale = Y (up)
            // After swap+180°: Warband X/Y = width, Warband Z = height
            float[] wbScale = new float[]
            {
                tree.widthScale,    // Warband X (width)
                tree.widthScale,    // Warband Y (width)
                tree.heightScale    // Warband Z (height / up)
            };

            // Rotation: Unity Y-axis → Warband Z-axis, +180° offset
            float[][] wbRotMatrix = BuildWarbandZRotationMatrix(tree.rotation);

            var data = new MBScenePropEntityData
            {
                type = "plant",
                str = entry.FloraData.FloraID,
                entry_no = entry.VariantIndex,
                pos = new float[] { wbPos.x, wbPos.y, wbPos.z },
                scale = wbScale,
                rotation_matrix = wbRotMatrix
            };

            output.Add(data);
            count++;
        }

        return count;
    }

    //  Detail Layer Export

    private static int ExportDetailLayers(
        Terrain terrain,
        Dictionary<int, FloraLibraryEntry> lookup,
        List<MBScenePropEntityData> output,
        int densityThreshold)
    {
        var td = terrain.terrainData;
        Vector3 terrainPos = terrain.transform.position;
        Vector3 terrainSize = td.size;
        int detailRes = td.detailWidth;
        int count = 0;

        float cellSizeX = terrainSize.x / detailRes;
        float cellSizeZ = terrainSize.z / detailRes;

        for (int protoIdx = 0; protoIdx < td.detailPrototypes.Length; protoIdx++)
        {
            if (!lookup.TryGetValue(protoIdx, out var entry))
                continue;

            int[,] map = td.GetDetailLayer(0, 0, detailRes, detailRes, protoIdx);

            if (EditorUtility.DisplayCancelableProgressBar(
                    "Exporting Details",
                    $"Processing {entry.EntryID} (proto {protoIdx})",
                    (float)protoIdx / td.detailPrototypes.Length))
                break;

            for (int x = 0; x < detailRes; x++)
            {
                for (int y = 0; y < detailRes; y++)
                {
                    int density = map[x, y];
                    if (density < densityThreshold) continue;

                    // detail x = terrain Z, y = terrain X
                    float worldX = terrainPos.x + ((float)y + 0.5f) * cellSizeX;
                    float worldZ = terrainPos.z + ((float)x + 0.5f) * cellSizeZ;

                    float nx = (float)y / detailRes;
                    float ny = (float)x / detailRes;
                    float worldY = terrainPos.y + td.GetInterpolatedHeight(nx, ny);

                    Vector3 wbPos = UnityToWarband(new Vector3(worldX, worldY, worldZ));

                    var data = new MBScenePropEntityData
                    {
                        type = "plant",
                        str = entry.FloraData.FloraID,
                        entry_no = entry.VariantIndex,
                        pos = new float[] { wbPos.x, wbPos.y, wbPos.z },
                        scale = new float[] { 1f, 1f, 1f },
                        rotation_matrix = BuildIdentityMatrix()
                    };

                    output.Add(data);
                    count++;
                }
            }
        }

        EditorUtility.ClearProgressBar();
        return count;
    }

    //  Coordinate Conversion

    /// <summary>
    /// Unity (X, Y, Z) → Warband (X, Z, Y)
    /// Straight axis swap - the 180° offset only applies to the rotation matrix,
    /// not to positions. Positions are world-space coordinates that just need
    /// the Y↔Z swap between coordinate systems.
    /// </summary>
    private static Vector3 UnityToWarband(Vector3 unity)
    {
        return new Vector3(
            unity.x,    // X stays
            unity.z,    // Unity Z → Warband Y
            unity.y     // Unity Y (up) → Warband Z (up)
        );
    }

    //  Matrix Helpers

    /// <summary>
    /// Builds a Warband Z-axis rotation matrix from Unity tree rotation (radians).
    /// Adds 180° (π) to account for the global scene orientation offset.
    /// 
    /// Rz(θ + π):
    ///   | cos  -sin  0 |
    ///   | sin   cos  0 |
    ///   |  0     0   1 |
    /// </summary>
    private static float[][] BuildWarbandZRotationMatrix(float treeRotationRadians)
    {
        float angle = treeRotationRadians; 
        float cos = Mathf.Cos(angle);
        float sin = Mathf.Sin(angle);

        return new float[][]
        {
            new float[] {  cos, -sin, 0 },
            new float[] {  sin,  cos, 0 },
            new float[] {    0,    0, 1 }
        };
    }

    private static float[][] BuildIdentityMatrix()
    {
        return new float[][]
        {
            new float[] { 1, 0, 0 },
            new float[] { 0, 1, 0 },
            new float[] { 0, 0, 1 }
        };
    }

    //  Prototype → Library Entry Lookup

    private static Dictionary<int, FloraLibraryEntry> BuildPrototypeLookup(
        MBFloraLibrary library,
        FloraCategory category)
    {
        var lookup = new Dictionary<int, FloraLibraryEntry>();

        foreach (var entry in library.Entries)
        {
            if (entry.Category != category) continue;
            if (!entry.IsRegistered) continue;
            if (entry.PrototypeIndex < 0) continue;
            if (entry.FloraData == null) continue;

            if (lookup.ContainsKey(entry.PrototypeIndex))
            {
                Debug.LogWarning($"[FloraExport] Duplicate prototype index {entry.PrototypeIndex} " +
                                 $"for entries '{lookup[entry.PrototypeIndex].EntryID}' and '{entry.EntryID}'");
                continue;
            }

            lookup[entry.PrototypeIndex] = entry;
        }

        return lookup;
    }
}
