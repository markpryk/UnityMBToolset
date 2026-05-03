using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;

namespace MountAndBlade.Flora
{
    /// <summary>
    /// Bakes Unity terrain detail layers into chunked meshes for Warband export.
    ///
    /// Uses the ACTUAL detail prototype meshes - preserves original geometry,
    /// UVs, normals, tangents, vertex colors, and materials.
    ///
    /// For each detail map cell with density N, spawns N instances scattered
    /// within the cell using random position jitter, Y-axis rotation, and
    /// scale variation from the prototype's min/max width/height settings.
    /// This replicates Unity's runtime detail rendering behavior.
    ///
    /// Within each chunk, instances sharing the same material are merged
    /// into a single submesh. One chunk = one Warband flora variant.
    /// </summary>
    public class GrassChunkBaker
    {
        private readonly Terrain _terrain;
        private readonly GrassChunkSettings _settings;

        public GrassChunkBaker(Terrain terrain, GrassChunkSettings settings)
        {
            _terrain = terrain ?? throw new ArgumentNullException(nameof(terrain));
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        }

        /// <summary>
        /// Bake all terrain detail layers into chunked meshes.
        /// </summary>
        public List<BakedGrassChunk> Bake()
        {
            var terrainData = _terrain.terrainData;
            var terrainPos = _terrain.transform.position;
            var terrainSize = terrainData.size;

            int detailRes = terrainData.detailWidth;
            float cellSizeX = terrainSize.x / detailRes;
            float cellSizeZ = terrainSize.z / detailRes;

            int chunksX = Mathf.CeilToInt(terrainSize.x / _settings.ChunkSize);
            int chunksZ = Mathf.CeilToInt(terrainSize.z / _settings.ChunkSize);

            // Resolve prototype meshes and materials
            var prototypes = terrainData.detailPrototypes;
            var protoData = ResolvePrototypes(prototypes);

            // Spatial buckets: chunkKey → instances
            var buckets = new Dictionary<Vector2Int, List<GrassInstance>>();

            for (int protoIdx = 0; protoIdx < prototypes.Length; protoIdx++)
            {
                if (protoData[protoIdx] == null)
                    continue;

                var proto = prototypes[protoIdx];
                int[,] map = terrainData.GetDetailLayer(0, 0, detailRes, detailRes, protoIdx);

                if (EditorUtility.DisplayCancelableProgressBar(
                        "Baking Grass Chunks",
                        $"Reading detail layer {protoIdx + 1}/{prototypes.Length}",
                        (float)protoIdx / prototypes.Length))
                {
                    EditorUtility.ClearProgressBar();
                    return new List<BakedGrassChunk>();
                }

                for (int dx = 0; dx < detailRes; dx++)
                {
                    for (int dz = 0; dz < detailRes; dz++)
                    {
                        int density = map[dx, dz];
                        if (density < 1) continue;

                        // Deterministic seed from cell coords + prototype index
                        // so re-baking produces identical results
                        uint seed = Hash(dx, dz, protoIdx);

                        // Scatter N instances within this cell
                        for (int inst = 0; inst < density; inst++)
                        {
                            // Random jitter within cell (0..1 range)
                            float jitterX = NextFloat(ref seed);
                            float jitterZ = NextFloat(ref seed);

                            // Detail map: dx = terrain Z, dz = terrain X
                            float worldX = terrainPos.x + (dz + jitterX) * cellSizeX;
                            float worldZ = terrainPos.z + (dx + jitterZ) * cellSizeZ;

                            // Sample terrain height at jittered position
                            float nx = (worldX - terrainPos.x) / terrainSize.x;
                            float nz = (worldZ - terrainPos.z) / terrainSize.z;
                            float worldY = terrainPos.y + terrainData.GetInterpolatedHeight(nx, nz);

                            // Random Y rotation (0 - 360)
                            float rotY = NextFloat(ref seed) * 360f;

                            // Random scale from prototype min/max
                            float scaleWidth = Mathf.Lerp(proto.minWidth, proto.maxWidth, NextFloat(ref seed));
                            float scaleHeight = Mathf.Lerp(proto.minHeight, proto.maxHeight, NextFloat(ref seed));

                            // Bucket into chunk
                            float localX = worldX - terrainPos.x;
                            float localZ = worldZ - terrainPos.z;
                            int cx = Mathf.Clamp(Mathf.FloorToInt(localX / _settings.ChunkSize), 0, chunksX - 1);
                            int cz = Mathf.Clamp(Mathf.FloorToInt(localZ / _settings.ChunkSize), 0, chunksZ - 1);

                            var key = new Vector2Int(cx, cz);
                            if (!buckets.ContainsKey(key))
                                buckets[key] = new List<GrassInstance>();

                            buckets[key].Add(new GrassInstance
                            {
                                WorldPosition = new Vector3(worldX, worldY, worldZ),
                                RotationY = rotY,
                                Scale = new Vector3(scaleWidth, scaleHeight, scaleWidth),
                                PrototypeIndex = protoIdx
                            });
                        }
                    }
                }
            }

            // Build chunk meshes
            var result = new List<BakedGrassChunk>();
            int chunkIdx = 0;
            int totalChunks = buckets.Count;

            foreach (var kvp in buckets)
            {
                var key = kvp.Key;
                var instances = kvp.Value;

                if (EditorUtility.DisplayCancelableProgressBar(
                        "Baking Grass Chunks",
                        $"Building chunk [{key.x},{key.y}] ({chunkIdx + 1}/{totalChunks})",
                        (float)chunkIdx / totalChunks))
                {
                    EditorUtility.ClearProgressBar();
                    return result;
                }

                float centerX = terrainPos.x + (key.x + 0.5f) * _settings.ChunkSize;
                float centerZ = terrainPos.z + (key.y + 0.5f) * _settings.ChunkSize;
                float centerY = terrainPos.y + terrainData.GetInterpolatedHeight(
                    (centerX - terrainPos.x) / terrainSize.x,
                    (centerZ - terrainPos.z) / terrainSize.z);
                Vector3 chunkCenter = new Vector3(centerX, centerY, centerZ);

                var chunk = BuildChunkMesh(key, chunkCenter, instances, protoData);
                if (chunk != null)
                    result.Add(chunk);

                chunkIdx++;
            }

            EditorUtility.ClearProgressBar();

            int totalInst = result.Sum(c => c.InstanceCount);
            int totalVerts = result.Sum(c => c.VertexCount);
            Debug.Log($"[GrassChunkBaker] Baked {result.Count} chunks " +
                      $"({totalInst} instances, {totalVerts} verts, grid {chunksX}×{chunksZ})");

            return result;
        }

        //  Deterministic RNG

        /// <summary>
        /// Deterministic hash from cell coordinates and prototype index.
        /// Ensures identical scattering across re-bakes.
        /// </summary>
        private static uint Hash(int x, int z, int proto)
        {
            uint h = (uint)(x * 73856093 ^ z * 19349663 ^ proto * 83492791);
            h ^= h >> 16;
            h *= 0x85ebca6b;
            h ^= h >> 13;
            h *= 0xc2b2ae35;
            h ^= h >> 16;
            return h;
        }

        /// <summary>
        /// xorshift32 - fast deterministic float in [0, 1).
        /// </summary>
        private static float NextFloat(ref uint state)
        {
            state ^= state << 13;
            state ^= state >> 17;
            state ^= state << 5;
            return (state & 0x7FFFFF) / (float)0x800000; // 23-bit mantissa → [0, 1)
        }

        //  Mesh Stamping & Combining

        private BakedGrassChunk BuildChunkMesh(
            Vector2Int chunkKey,
            Vector3 chunkCenter,
            List<GrassInstance> instances,
            ResolvedPrototype[] protoData)
        {
            // Group by material across all instances and all submeshes
            var materialBuckets = new Dictionary<Material, MeshAccumulator>();
            int totalInstances = 0;

            foreach (var inst in instances)
            {
                var proto = protoData[inst.PrototypeIndex];
                if (proto == null) continue;

                Vector3 localPos = inst.WorldPosition - chunkCenter;

                // Full TRS matrix: position + Y rotation + non-uniform scale
                Matrix4x4 instanceMatrix = Matrix4x4.TRS(
                    localPos,
                    Quaternion.Euler(0f, inst.RotationY, 0f),
                    inst.Scale);

                for (int sub = 0; sub < proto.SubMeshes.Length; sub++)
                {
                    var subData = proto.SubMeshes[sub];

                    if (!materialBuckets.TryGetValue(subData.Material, out var acc))
                    {
                        acc = new MeshAccumulator();
                        materialBuckets[subData.Material] = acc;
                    }

                    StampSubmesh(acc, subData, instanceMatrix);
                }

                totalInstances++;
            }

            if (totalInstances == 0 || materialBuckets.Count == 0)
                return null;

            // Combine into final mesh
            var allVertices = new List<Vector3>();
            var allNormals = new List<Vector3>();
            var allTangents = new List<Vector4>();
            var allUV0 = new List<Vector2>();
            var allUV1 = new List<Vector2>();
            var allColors = new List<Color>();
            var submeshTris = new List<int[]>();
            var materials = new List<Material>();

            // Check which channels exist across any bucket
            bool hasUV1 = materialBuckets.Values.Any(a => a.UV1.Count > 0);
            bool hasTangents = materialBuckets.Values.Any(a => a.Tangents.Count > 0);
            bool hasColors = materialBuckets.Values.Any(a => a.Colors.Count > 0);

            foreach (var kvp in materialBuckets)
            {
                var acc = kvp.Value;
                int vertOffset = allVertices.Count;
                int vertCount = acc.Vertices.Count;

                allVertices.AddRange(acc.Vertices);
                allNormals.AddRange(acc.Normals);

                // Pad missing channels to keep arrays aligned
                AppendOrPad(allTangents, acc.Tangents, vertCount, hasTangents,
                    new Vector4(1, 0, 0, 1));
                AppendOrPad(allUV0, acc.UV0, vertCount, true, Vector2.zero);
                AppendOrPad(allUV1, acc.UV1, vertCount, hasUV1, Vector2.zero);
                AppendOrPad(allColors, acc.Colors, vertCount, hasColors, Color.white);

                var tris = new int[acc.Triangles.Count];
                for (int i = 0; i < acc.Triangles.Count; i++)
                    tris[i] = acc.Triangles[i] + vertOffset;

                submeshTris.Add(tris);
                materials.Add(kvp.Key);
            }

            int totalVerts = allVertices.Count;

            if (totalVerts > 65535)
            {
                Debug.LogWarning($"[GrassChunkBaker] Chunk [{chunkKey.x},{chunkKey.y}] " +
                                 $"has {totalVerts} verts - exceeds DX9 16-bit index limit.");
            }

            var mesh = new Mesh();
            mesh.name = $"grass_chunk_{chunkKey.x}_{chunkKey.y}";
            mesh.indexFormat = totalVerts > 65535
                ? UnityEngine.Rendering.IndexFormat.UInt32
                : UnityEngine.Rendering.IndexFormat.UInt16;

            mesh.SetVertices(allVertices);
            mesh.SetNormals(allNormals);
            if (hasTangents) mesh.SetTangents(allTangents);
            mesh.SetUVs(0, allUV0);
            if (hasUV1) mesh.SetUVs(1, allUV1);
            if (hasColors) mesh.SetColors(allColors);

            mesh.subMeshCount = submeshTris.Count;
            for (int i = 0; i < submeshTris.Count; i++)
                mesh.SetTriangles(submeshTris[i], i);

            mesh.RecalculateBounds();
            if (!hasTangents) mesh.RecalculateTangents();

            return new BakedGrassChunk
            {
                ChunkKey = chunkKey,
                Center = chunkCenter,
                Mesh = mesh,
                Materials = materials.ToArray(),
                InstanceCount = totalInstances,
                VertexCount = totalVerts
            };
        }

        /// <summary>
        /// Stamps prototype submesh geometry into accumulator.
        /// Transforms positions/normals/tangents by instance matrix.
        /// UVs and vertex colors are copied as-is.
        /// </summary>
        private void StampSubmesh(
            MeshAccumulator acc,
            PrototypeSubMesh subData,
            Matrix4x4 instanceMatrix)
        {
            int vertOffset = acc.Vertices.Count;

            // Extract the inverse-transpose for correct normal transformation
            // under non-uniform scale (width ≠ height)
            Matrix4x4 normalMatrix = instanceMatrix.inverse.transpose;

            for (int v = 0; v < subData.Vertices.Length; v++)
            {
                acc.Vertices.Add(instanceMatrix.MultiplyPoint3x4(subData.Vertices[v]));
                acc.Normals.Add(normalMatrix.MultiplyVector(subData.Normals[v]).normalized);
            }

            // UVs: direct copy, no transformation
            acc.UV0.AddRange(subData.UV0);

            if (subData.UV1 != null && subData.UV1.Length > 0)
                acc.UV1.AddRange(subData.UV1);

            // Tangents: transform direction, preserve w handedness
            if (subData.Tangents != null && subData.Tangents.Length > 0)
            {
                for (int v = 0; v < subData.Tangents.Length; v++)
                {
                    Vector4 t = subData.Tangents[v];
                    Vector3 dir = instanceMatrix.MultiplyVector(
                        new Vector3(t.x, t.y, t.z)).normalized;
                    acc.Tangents.Add(new Vector4(dir.x, dir.y, dir.z, t.w));
                }
            }

            // Vertex colors: direct copy
            if (subData.Colors != null && subData.Colors.Length > 0)
                acc.Colors.AddRange(subData.Colors);

            for (int i = 0; i < subData.Triangles.Length; i++)
                acc.Triangles.Add(subData.Triangles[i] + vertOffset);
        }

        //  Prototype Resolution

        /// <summary>
        /// Resolves each detail prototype to cached submesh data.
        /// Reads from the prototype prefab's MeshFilter/MeshRenderer hierarchy.
        /// Billboard prototypes return null.
        /// </summary>
        private ResolvedPrototype[] ResolvePrototypes(DetailPrototype[] prototypes)
        {
            var resolved = new ResolvedPrototype[prototypes.Length];

            for (int i = 0; i < prototypes.Length; i++)
            {
                var proto = prototypes[i];

                if (!proto.usePrototypeMesh || proto.prototype == null)
                {
                    Debug.LogWarning($"[GrassChunkBaker] Prototype {i} is billboard or null - skipping.");
                    continue;
                }

                var go = proto.prototype;
                var filters = go.GetComponentsInChildren<MeshFilter>(false);
                var renderers = go.GetComponentsInChildren<MeshRenderer>(false);

                if (filters.Length == 0 || renderers.Length == 0)
                {
                    Debug.LogWarning($"[GrassChunkBaker] Prototype {i} ({go.name}) has no mesh data.");
                    continue;
                }

                var filterByGO = new Dictionary<GameObject, MeshFilter>();
                foreach (var mf in filters)
                    if (mf.sharedMesh != null)
                        filterByGO[mf.gameObject] = mf;

                var subMeshes = new List<PrototypeSubMesh>();

                foreach (var mr in renderers)
                {
                    if (!filterByGO.TryGetValue(mr.gameObject, out var mf))
                        continue;

                    var mesh = mf.sharedMesh;
                    if (mesh == null) continue;

                    if (!mesh.isReadable)
                    {
                        mesh = MakeReadableCopy(mesh);
                        if (mesh == null)
                        {
                            Debug.LogError($"[GrassChunkBaker] Mesh '{mf.sharedMesh.name}' on prototype {i} " +
                                           "is not readable and could not be copied.");
                            continue;
                        }
                    }

                    // Local transform relative to prototype root
                    Matrix4x4 localMatrix = go.transform.worldToLocalMatrix
                                            * mr.transform.localToWorldMatrix;

                    var sharedMats = mr.sharedMaterials;

                    // Cache source arrays once per mesh
                    var srcVerts = mesh.vertices;
                    var srcNorms = mesh.normals;
                    var srcUV0 = mesh.uv;
                    var srcUV1 = mesh.uv2;
                    var srcTangents = mesh.tangents;
                    var srcColors = mesh.colors;

                    for (int sub = 0; sub < mesh.subMeshCount; sub++)
                    {
                        var mat = sub < sharedMats.Length ? sharedMats[sub] : sharedMats[0];
                        if (mat == null) continue;

                        var triangles = mesh.GetTriangles(sub);
                        if (triangles.Length == 0) continue;

                        // Extract only vertices used by this submesh
                        var usedVerts = new HashSet<int>(triangles);
                        var oldToNew = new Dictionary<int, int>();

                        var verts = new List<Vector3>(usedVerts.Count);
                        var norms = new List<Vector3>(usedVerts.Count);
                        var uv0 = new List<Vector2>(usedVerts.Count);
                        List<Vector2> uv1 = srcUV1 != null && srcUV1.Length > 0
                            ? new List<Vector2>(usedVerts.Count) : null;
                        List<Vector4> tangents = srcTangents != null && srcTangents.Length > 0
                            ? new List<Vector4>(usedVerts.Count) : null;
                        List<Color> colors = srcColors != null && srcColors.Length > 0
                            ? new List<Color>(usedVerts.Count) : null;

                        foreach (int idx in usedVerts.OrderBy(x => x))
                        {
                            oldToNew[idx] = verts.Count;

                            // Transform position and normal into prototype-root space
                            verts.Add(localMatrix.MultiplyPoint3x4(srcVerts[idx]));
                            norms.Add(idx < srcNorms.Length
                                ? localMatrix.MultiplyVector(srcNorms[idx]).normalized
                                : Vector3.up);

                            // UVs: exact copy
                            uv0.Add(idx < srcUV0.Length ? srcUV0[idx] : Vector2.zero);

                            if (uv1 != null)
                                uv1.Add(idx < srcUV1.Length ? srcUV1[idx] : Vector2.zero);

                            if (tangents != null && idx < srcTangents.Length)
                            {
                                var t = srcTangents[idx];
                                var dir = localMatrix.MultiplyVector(
                                    new Vector3(t.x, t.y, t.z)).normalized;
                                tangents.Add(new Vector4(dir.x, dir.y, dir.z, t.w));
                            }
                            else if (tangents != null)
                            {
                                tangents.Add(new Vector4(1, 0, 0, 1));
                            }

                            colors?.Add(idx < srcColors.Length ? srcColors[idx] : Color.white);
                        }

                        var remapped = new int[triangles.Length];
                        for (int t = 0; t < triangles.Length; t++)
                            remapped[t] = oldToNew[triangles[t]];

                        subMeshes.Add(new PrototypeSubMesh
                        {
                            Material = mat,
                            Vertices = verts.ToArray(),
                            Normals = norms.ToArray(),
                            UV0 = uv0.ToArray(),
                            UV1 = uv1?.ToArray(),
                            Tangents = tangents?.ToArray(),
                            Colors = colors?.ToArray(),
                            Triangles = remapped
                        });
                    }
                }

                if (subMeshes.Count > 0)
                {
                    resolved[i] = new ResolvedPrototype
                    {
                        Name = go.name,
                        SubMeshes = subMeshes.ToArray()
                    };

                    int tv = subMeshes.Sum(s => s.Vertices.Length);
                    Debug.Log($"[GrassChunkBaker] Resolved prototype {i} '{go.name}': " +
                              $"{subMeshes.Count} submesh(es), {tv} verts");
                }
            }

            return resolved;
        }

        //  Helpers

        private static void AppendOrPad<T>(List<T> target, List<T> source,
            int count, bool channelActive, T defaultValue)
        {
            if (!channelActive) return;

            if (source.Count == count)
                target.AddRange(source);
            else
                target.AddRange(Enumerable.Repeat(defaultValue, count));
        }

        /// <summary>
        /// Creates a readable copy of a non-readable mesh.
        /// Tries ModelImporter first (for imported models), then falls back
        /// to reading GPU buffers via Mesh.GetVertices etc. which work
        /// even on non-readable meshes in the Editor.
        /// </summary>
        private static Mesh MakeReadableCopy(Mesh source)
        {
            // Try ModelImporter path first
            string assetPath = AssetDatabase.GetAssetPath(source);
            if (!string.IsNullOrEmpty(assetPath))
            {
                var importer = AssetImporter.GetAtPath(assetPath) as ModelImporter;
                if (importer != null)
                {
                    importer.isReadable = true;
                    importer.SaveAndReimport();
                    var reimported = AssetDatabase.LoadAssetAtPath<Mesh>(assetPath);
                    if (reimported != null && reimported.isReadable)
                    {
                        Debug.Log($"[GrassChunkBaker] Enabled Read/Write via ModelImporter: {assetPath}");
                        return reimported;
                    }
                }
            }

            // For script-created mesh assets (.asset), use GraphicsBuffer readback
            try
            {
                var copy = new Mesh();
                copy.name = source.name + "_readable";
                copy.indexFormat = source.indexFormat;

                // In Editor, these work even on non-readable meshes
                copy.vertices = source.vertices;
                copy.normals = source.normals;
                copy.tangents = source.tangents;
                copy.uv = source.uv;
                copy.uv2 = source.uv2;
                copy.colors = source.colors;
                copy.bindposes = source.bindposes;
                copy.subMeshCount = source.subMeshCount;

                for (int s = 0; s < source.subMeshCount; s++)
                    copy.SetTriangles(source.GetTriangles(s), s);

                copy.RecalculateBounds();
                Debug.Log($"[GrassChunkBaker] Created readable copy of '{source.name}'");
                return copy;
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[GrassChunkBaker] Failed to create readable copy of '{source.name}': {e.Message}");
                return null;
            }
        }

    }

    //  Internal Types

    internal class ResolvedPrototype
    {
        public string Name;
        public PrototypeSubMesh[] SubMeshes;
    }

    internal class PrototypeSubMesh
    {
        public Material Material;
        public Vector3[] Vertices;
        public Vector3[] Normals;
        public Vector2[] UV0;
        public Vector2[] UV1;       // nullable
        public Vector4[] Tangents;  // nullable
        public Color[] Colors;      // nullable
        public int[] Triangles;
    }

    internal class MeshAccumulator
    {
        public List<Vector3> Vertices = new List<Vector3>();
        public List<Vector3> Normals = new List<Vector3>();
        public List<Vector4> Tangents = new List<Vector4>();
        public List<Vector2> UV0 = new List<Vector2>();
        public List<Vector2> UV1 = new List<Vector2>();
        public List<Color> Colors = new List<Color>();
        public List<int> Triangles = new List<int>();
    }

    //  Public Data Types

    [Serializable]
    public class GrassChunkSettings
    {
        [Tooltip("World-space size of each chunk in meters. Max 1023 for Warband compatibility.")]
        [Range(1f, 1023f)]
        public float ChunkSize = 10f;
    }

    public class BakedGrassChunk
    {
        public Vector2Int ChunkKey;
        public Vector3 Center;
        public Mesh Mesh;
        public Material[] Materials;
        public int InstanceCount;
        public int VertexCount;
    }

    internal struct GrassInstance
    {
        public Vector3 WorldPosition;
        public float RotationY;
        public Vector3 Scale;
        public int PrototypeIndex;
    }
}
