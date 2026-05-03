using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MountAndBlade.Data;
using MountAndBlade.Flora;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace MountAndBlade.Flora
{
    /// <summary>
    /// Saves baked grass chunks as mesh assets and prefabs.
    /// Integrates with ExporterProps for Warband .sco export.
    ///
    /// Output structure:
    ///   {outputFolder}/
    ///     {floraKindId}_flora.asset    (MBFloraData)
    ///     {floraKindId}_chunks.asset   (GrassChunksData)
    ///     {floraKindId}_scene.prefab   (scene prefab with MBFlora components)
    ///     data.json                    (BRF mesh manifest)
    ///     flora_kinds_entry.py         (module_flora_kinds.py snippet)
    ///     Meshes/
    ///       grass_chunk_0_0.obj        (used by both Unity and BRF pipeline)
    ///       grass_chunk_0_0.1.obj      (submesh variants)
    ///       ...
    /// </summary>
    public static class GrassChunkBakerUtility
    {
        /// <summary>
        /// Bake and save all grass chunks from terrain.
        /// 
        /// Flow:
        ///   1. Bake terrain details into chunk meshes (in memory)
        ///   2. Export each chunk/submesh as OBJ into Meshes/ subfolder
        ///   3. Import OBJs via AssetDatabase
        ///   4. Build scene prefab using the imported OBJ meshes
        ///   5. Create GrassChunksData + MBFloraData ScriptableObjects
        ///   6. Generate data.json + flora_kinds_entry.py for BRF pipeline
        ///
        /// The OBJ files in Meshes/ serve double duty: Unity reimports them
        /// as mesh assets for the scene prefab, and the BRF pipeline reads
        /// them directly for Warband export.
        /// </summary>
        public static GrassChunksData BakeAndSave(
            Terrain terrain,
            GrassChunkSettings settings,
            string outputFolder,
            string floraKindId)
        {
            if (terrain == null)
            {
                Debug.LogError("[GrassChunkBaker] No terrain provided.");
                return null;
            }

            if (string.IsNullOrEmpty(outputFolder))
                outputFolder = "Assets/MBMod/GrassChunks";

            string meshesFolder = outputFolder + "/Meshes";
            EnsureFolder(outputFolder);
            EnsureFolder(meshesFolder);

            var baker = new GrassChunkBaker(terrain, settings);
            var chunks = baker.Bake();

            if (chunks.Count == 0)
            {
                Debug.LogWarning("[GrassChunkBaker] No chunks generated.");
                return null;
            }

            // Create or load MBFloraData
            string floraDataPath = $"{outputFolder}/{floraKindId}_flora.asset";
            var floraData = AssetDatabase.LoadAssetAtPath<MBFloraData>(floraDataPath);
            if (floraData == null)
            {
                floraData = ScriptableObject.CreateInstance<MBFloraData>();
                floraData.FloraID = floraKindId;
                floraData.Flags = "524288"; // fkf_grass (0x00080000)
                AssetDatabase.CreateAsset(floraData, floraDataPath);
            }
            else
            {
                floraData.FloraID = floraKindId;
                floraData.Meshes.Clear();
            }

            // Create or load GrassChunksData
            string chunksDataPath = $"{outputFolder}/{floraKindId}_chunks.asset";
            var chunksData = AssetDatabase.LoadAssetAtPath<GrassChunksData>(chunksDataPath);
            if (chunksData == null)
            {
                chunksData = ScriptableObject.CreateInstance<GrassChunksData>();
                AssetDatabase.CreateAsset(chunksData, chunksDataPath);
            }

            chunksData.FloraData = floraData;
            chunksData.ChunkSize = settings.ChunkSize;
            chunksData.Clear();

            var chunkOBJMap = new List<ChunkOBJInfo>();
            var meshEntries = new List<BRFMeshEntry>();

            for (int i = 0; i < chunks.Count; i++)
            {
                var chunk = chunks[i];
                if (chunk.Mesh == null) continue;

                EditorUtility.DisplayProgressBar(
                    "Saving Grass Chunks",
                    $"Exporting OBJ {i + 1}/{chunks.Count}",
                    (float)i / chunks.Count * 0.4f);

                var mesh = chunk.Mesh;
                var info = new ChunkOBJInfo
                {
                    Chunk = chunk,
                    OBJPaths = new List<string>(),
                    Materials = new List<Material>()
                };

                if (mesh.subMeshCount <= 1)
                {
                    string objPath = $"{meshesFolder}/{mesh.name}.obj";
                    ExportSubmeshToOBJ(mesh, 0, objPath, mesh.name);
                    info.OBJPaths.Add(objPath);

                    var mat = chunk.Materials != null && chunk.Materials.Length > 0
                        ? chunk.Materials[0] : null;
                    info.Materials.Add(mat);

                    meshEntries.Add(new BRFMeshEntry
                    {
                        name = mesh.name,
                        source = "Meshes/" + mesh.name + ".obj",
                        material = mat != null ? mat.name : "default",
                        flags = 196608,
                        lod_level = 0
                    });
                }
                else
                {
                    for (int sub = 0; sub < mesh.subMeshCount; sub++)
                    {
                        string meshName = sub == 0 ? mesh.name : mesh.name + "." + sub;
                        string objPath = $"{meshesFolder}/{meshName}.obj";
                        ExportSubmeshToOBJ(mesh, sub, objPath, meshName);
                        info.OBJPaths.Add(objPath);

                        var mat = chunk.Materials != null && sub < chunk.Materials.Length
                            ? chunk.Materials[sub] : null;
                        info.Materials.Add(mat);

                        meshEntries.Add(new BRFMeshEntry
                        {
                            name = meshName,
                            source = "Meshes/" + meshName + ".obj",
                            material = mat != null ? mat.name : "default",
                            flags = 196608,
                            lod_level = 0
                        });
                    }
                }

                chunkOBJMap.Add(info);
            }

            EditorUtility.DisplayProgressBar("Saving Grass Chunks", "Importing OBJs...", 0.45f);
            AssetDatabase.Refresh();

            foreach (var info in chunkOBJMap)
            {
                foreach (string objPath in info.OBJPaths)
                {
                    var importer = AssetImporter.GetAtPath(objPath) as ModelImporter;
                    if (importer != null)
                    {
                        importer.isReadable = true;
                        importer.importNormals = ModelImporterNormals.Import;
                        importer.importTangents = ModelImporterTangents.CalculateMikk;
                        importer.materialImportMode = ModelImporterMaterialImportMode.None;
                        importer.SaveAndReimport();
                    }
                }
            }

            EditorUtility.DisplayProgressBar("Saving Grass Chunks", "Building scene prefab...", 0.6f);

            var root = new GameObject(floraKindId + "_scene");

            for (int i = 0; i < chunkOBJMap.Count; i++)
            {
                var info = chunkOBJMap[i];
                var chunk = info.Chunk;

                EditorUtility.DisplayProgressBar(
                    "Saving Grass Chunks",
                    $"Assembling chunk {i + 1}/{chunkOBJMap.Count}",
                    0.6f + (float)i / chunkOBJMap.Count * 0.3f);

                int variantIndex = chunksData.Chunks.Count;

                var chunkGO = new GameObject($"grass_chunk_{chunk.ChunkKey.x}_{chunk.ChunkKey.y}");
                chunkGO.transform.SetParent(root.transform);
                chunkGO.transform.localPosition = chunk.Center;
                chunkGO.transform.localRotation = Quaternion.identity;
                chunkGO.transform.localScale = Vector3.one;

                var flora = chunkGO.AddComponent<MBFlora>();
                flora.FloraData = floraData;
                flora.FloraVariantID = variantIndex;

                if (info.OBJPaths.Count == 1)
                {
                    var objMesh = LoadMeshFromOBJ(info.OBJPaths[0]);
                    if (objMesh != null)
                    {
                        var mf = chunkGO.AddComponent<MeshFilter>();
                        mf.sharedMesh = objMesh;
                        var mr = chunkGO.AddComponent<MeshRenderer>();
                        mr.sharedMaterial = info.Materials[0];
                        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                        mr.receiveShadows = true;
                    }
                }
                else
                {
                    for (int sub = 0; sub < info.OBJPaths.Count; sub++)
                    {
                        var objMesh = LoadMeshFromOBJ(info.OBJPaths[sub]);
                        if (objMesh == null) continue;

                        var child = new GameObject($"submesh_{sub}");
                        child.transform.SetParent(chunkGO.transform);
                        child.transform.localPosition = Vector3.zero;
                        child.transform.localRotation = Quaternion.identity;
                        child.transform.localScale = Vector3.one;

                        var mf = child.AddComponent<MeshFilter>();
                        mf.sharedMesh = objMesh;
                        var mr = child.AddComponent<MeshRenderer>();
                        mr.sharedMaterial = info.Materials[sub];
                        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                        mr.receiveShadows = true;
                    }
                }

                // Register in chunks data
                var refMesh = LoadMeshFromOBJ(info.OBJPaths[0]);
                chunksData.AddChunk(new GrassChunkEntry
                {
                    ChunkKey = chunk.ChunkKey,
                    WorldPosition = chunk.Center,
                    Mesh = refMesh,
                    Materials = info.Materials.ToArray(),
                    InstanceCount = chunk.InstanceCount,
                    VertexCount = chunk.VertexCount
                });
            }

            // Save scene prefab
            string scenePrefabPath = $"{outputFolder}/{floraKindId}_scene.prefab";
            PrefabUtility.SaveAsPrefabAsset(root, scenePrefabPath);
            Object.DestroyImmediate(root);

            EditorUtility.DisplayProgressBar("Saving Grass Chunks", "Writing BRF manifest...", 0.95f);
            WriteBRFManifest(chunksData, meshEntries, outputFolder);

            EditorUtility.SetDirty(floraData);
            EditorUtility.SetDirty(chunksData);
            AssetDatabase.SaveAssets();
            EditorUtility.ClearProgressBar();

            Debug.Log($"[GrassChunkBaker] Saved {chunks.Count} chunks. " +
                      $"FloraData: {floraDataPath}, ChunksData: {chunksDataPath}, " +
                      $"Scene: {scenePrefabPath}");

            return chunksData;
        }

        //  BRF Manifest (data.json + flora_kinds_entry.py)

        /// <summary>
        /// Writes data.json and flora_kinds_entry.py into the output folder.
        /// These reference the OBJ files already present in the Meshes/ subfolder.
        /// </summary>
        private static void WriteBRFManifest(
            GrassChunksData chunksData,
            List<BRFMeshEntry> meshEntries,
            string outputFolder)
        {
            var brfData = new Dictionary<string, object>
            {
                { "version", "1.0" },
                { "materials", new List<object>() },
                { "textures", new List<object>() },
                { "meshes", meshEntries },
                { "shaders", new List<object>() },
                { "skeletons", new List<object>() },
                { "animations", new List<object>() },
                { "bodies", new List<object>() }
            };

            string json = Newtonsoft.Json.JsonConvert.SerializeObject(brfData, Newtonsoft.Json.Formatting.Indented);
            File.WriteAllText(Path.Combine(outputFolder, "data.json"), json);

            WriteFloraKindsPython(chunksData, meshEntries, outputFolder);

            Debug.Log($"[GrassChunkBaker] Wrote data.json + flora_kinds_entry.py → {outputFolder}");
        }

        /// <summary>
        /// Generates a flora_kinds.py tuple entry for the chunk set.
        /// Only lists root mesh names - submeshes (.1, .2) are picked up
        /// automatically by the engine.
        /// </summary>
        private static void WriteFloraKindsPython(
            GrassChunksData chunksData,
            List<BRFMeshEntry> allMeshEntries,
            string outputFolder)
        {
            string floraId = chunksData.FloraData.FloraID;
            int density = chunksData.FloraData.Density > 0 ? chunksData.FloraData.Density : 1500;

            var rootMeshNames = new List<string>();
            foreach (var entry in allMeshEntries)
            {
                if (!IsSubmeshName(entry.name))
                    rootMeshNames.Add(entry.name);
            }

            var sb = new System.Text.StringBuilder();
            sb.AppendLine("# Auto-generated by GrassChunkBaker");
            sb.AppendLine("# Add to module_flora_kinds.py");
            sb.AppendLine();
            sb.Append($"(\"{floraId}\", fkf_grass|density({density}), [");

            for (int i = 0; i < rootMeshNames.Count; i++)
            {
                if (i > 0) sb.Append(", ");
                sb.Append($"[\"{rootMeshNames[i]}\", \"0\"]");
            }

            sb.Append("]),");

            File.WriteAllText(Path.Combine(outputFolder, "flora_kinds_entry.py"), sb.ToString());
            Debug.Log($"[GrassChunkBaker] Flora kinds entry: {rootMeshNames.Count} variants");
        }

        //  Export Integration (.sco)

        /// <summary>
        /// Collects chunk placement data from GrassChunksData for the ExporterProps pipeline.
        /// Each chunk becomes a flora "plant" entry at its world-space center.
        /// Coordinate conversion: Unity (X, Y, Z) → Warband (X, Z, Y).
        /// </summary>
        public static List<MBScenePropEntityData> CollectChunksForExport(GrassChunksData chunksData)
        {
            var output = new List<MBScenePropEntityData>();

            if (chunksData == null || chunksData.FloraData == null || chunksData.Chunks.Count == 0)
                return output;

            string floraId = chunksData.FloraData.FloraID;

            for (int i = 0; i < chunksData.Chunks.Count; i++)
            {
                var entry = chunksData.Chunks[i];

                Vector3 wbPos = new Vector3(
                    entry.WorldPosition.x,
                    entry.WorldPosition.z,
                    entry.WorldPosition.y);

                var data = new MBScenePropEntityData
                {
                    type = "plant",
                    str = floraId,
                    entry_no = entry.VariantIndex,
                    pos = new float[] { wbPos.x, wbPos.y, wbPos.z },
                    scale = new float[] { 1f, 1f, 1f },
                    rotation_matrix = new float[][]
                    {
                        new float[] { 1, 0, 0 },
                        new float[] { 0, 1, 0 },
                        new float[] { 0, 0, 1 }
                    }
                };

                output.Add(data);
            }

            return output;
        }

        //  OBJ Export

        /// <summary>
        /// Exports a single submesh as OBJ in Unity coordinates (Y-up).
        /// </summary>
        private static void ExportSubmeshToOBJ(Mesh mesh, int submeshIndex, string path, string objectName)
        {
            var triangles = mesh.GetTriangles(submeshIndex);

            var usedSet = new HashSet<int>(triangles);
            var usedIndices = new List<int>(usedSet);
            usedIndices.Sort();

            var oldToNew = new Dictionary<int, int>();
            for (int i = 0; i < usedIndices.Count; i++)
                oldToNew[usedIndices[i]] = i + 1;

            var srcVerts = mesh.vertices;
            var srcNorms = mesh.normals;
            var srcUVs = mesh.uv;

            bool hasNormals = srcNorms != null && srcNorms.Length > 0;
            bool hasUVs = srcUVs != null && srcUVs.Length > 0;

            var sb = new System.Text.StringBuilder();
            sb.AppendLine("# Exported by GrassChunkBaker");
            sb.AppendLine($"o {objectName}");

            // Unity's OBJ importer flips the forward axis (Z), which results in
            // a mirrored mesh. To compensate we negate X on positions and normals,
            // then reverse triangle winding to fix face orientation.
            foreach (int idx in usedIndices)
            {
                var v = srcVerts[idx];
                sb.AppendLine($"v {FormatFloat(-v.x)} {FormatFloat(v.y)} {FormatFloat(v.z)}");
            }

            if (hasUVs)
            {
                foreach (int idx in usedIndices)
                {
                    var uv = idx < srcUVs.Length ? srcUVs[idx] : Vector2.zero;
                    sb.AppendLine($"vt {FormatFloat(uv.x)} {FormatFloat(uv.y)}");
                }
            }

            if (hasNormals)
            {
                foreach (int idx in usedIndices)
                {
                    var n = idx < srcNorms.Length ? srcNorms[idx] : Vector3.up;
                    sb.AppendLine($"vn {FormatFloat(-n.x)} {FormatFloat(n.y)} {FormatFloat(n.z)}");
                }
            }

            // Reversed winding (c, b, a) to compensate for the X-axis mirror
            for (int i = 0; i < triangles.Length; i += 3)
            {
                int a = oldToNew[triangles[i]];
                int b = oldToNew[triangles[i + 1]];
                int c = oldToNew[triangles[i + 2]];

                if (hasNormals && hasUVs)
                    sb.AppendLine($"f {c}/{c}/{c} {b}/{b}/{b} {a}/{a}/{a}");
                else if (hasUVs)
                    sb.AppendLine($"f {c}/{c} {b}/{b} {a}/{a}");
                else if (hasNormals)
                    sb.AppendLine($"f {c}//{c} {b}//{b} {a}//{a}");
                else
                    sb.AppendLine($"f {c} {b} {a}");
            }

            File.WriteAllText(path, sb.ToString());
        }

        private static string FormatFloat(float value)
        {
            return value.ToString("G8", System.Globalization.CultureInfo.InvariantCulture);
        }

        //  Helpers

        private static Mesh LoadMeshFromOBJ(string assetPath)
        {
            var assets = AssetDatabase.LoadAllAssetsAtPath(assetPath);
            foreach (var asset in assets)
            {
                if (asset is Mesh mesh)
                    return mesh;
            }

            Debug.LogWarning($"[GrassChunkBaker] No mesh found in OBJ: {assetPath}");
            return null;
        }

        private static bool IsSubmeshName(string name)
        {
            int lastDot = name.LastIndexOf('.');
            if (lastDot < 0 || lastDot >= name.Length - 1)
                return false;

            string suffix = name.Substring(lastDot + 1);
            foreach (char c in suffix)
            {
                if (!char.IsDigit(c))
                    return false;
            }

            return true;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;

            var parts = path.Split('/');
            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }

        private class ChunkOBJInfo
        {
            public BakedGrassChunk Chunk;
            public List<string> OBJPaths;
            public List<Material> Materials;
        }

        [Serializable]
        private class BRFMeshEntry
        {
            public int flags;
            public int lod_level;
            public string material;
            public string name;
            public string source;
        }
    }
}
