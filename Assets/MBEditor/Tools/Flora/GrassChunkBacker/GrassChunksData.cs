using System;
using System.Collections.Generic;
using MountAndBlade.Data;
using UnityEngine;

namespace MountAndBlade.Flora
{
    /// <summary>
    /// Central registry for baked grass chunks in a scene.
    /// 
    /// Links to a generated MBFloraData (registered as a flora kind in Warband)
    /// and stores per-chunk entries with world positions and mesh references.
    /// Each chunk entry corresponds to one flora variant (mesh) in the flora kind.
    ///
    /// Workflow:
    ///   1. GrassChunkBaker bakes terrain details into chunk meshes
    ///   2. GrassChunkBakerUtility saves meshes/prefabs and populates this SO
    ///   3. Exporter reads this SO to emit flora entries into .sco
    ///
    /// In Warband:
    ///   flora_kinds.txt gets one entry with FloraData.FloraID
    ///   Each chunk mesh is a variant (FloraMesh) under that flora kind
    ///   .sco gets one "plant" entry per chunk at its world position
    /// </summary>
    [CreateAssetMenu(fileName = "New Grass Chunks", menuName = "Mount & Blade/Grass Chunks Data", order = 2)]
    public class GrassChunksData : ScriptableObject
    {
        [Tooltip("Generated flora data for this grass chunk set. " +
                 "Meshes list = one entry per chunk variant.")]
        public MBFloraData FloraData;

        [Tooltip("Per-chunk entries with position and mesh reference.")]
        public List<GrassChunkEntry> Chunks = new List<GrassChunkEntry>();

        [Header("Bake Info")]
        [Tooltip("Chunk size used during baking.")]
        public float ChunkSize;

        [Tooltip("Total grass instances across all chunks.")]
        public int TotalInstances;

        [Tooltip("Total vertices across all chunks.")]
        public int TotalVertices;

        /// <summary>
        /// Clears all chunk entries and resets the flora data meshes list.
        /// </summary>
        public void Clear()
        {
            Chunks.Clear();
            TotalInstances = 0;
            TotalVertices = 0;

            if (FloraData != null)
                FloraData.Meshes.Clear();
        }

        /// <summary>
        /// Adds a chunk entry and registers its mesh as a variant in FloraData.
        /// </summary>
        public void AddChunk(GrassChunkEntry entry)
        {
            int variantIndex = Chunks.Count;
            entry.VariantIndex = variantIndex;
            Chunks.Add(entry);

            // Register mesh as flora variant
            if (FloraData != null && entry.Mesh != null)
            {
                FloraData.Meshes.Add(new FloraMesh
                {
                    Mesh = entry.Mesh.name
                });
            }

            TotalInstances += entry.InstanceCount;
            TotalVertices += entry.VertexCount;
        }
    }

    [Serializable]
    public class GrassChunkEntry
    {
        [Tooltip("Grid coordinate of this chunk.")]
        public Vector2Int ChunkKey;

        [Tooltip("World-space center position of this chunk.")]
        public Vector3 WorldPosition;

        [Tooltip("Baked chunk mesh asset.")]
        public Mesh Mesh;

        [Tooltip("Materials used by submeshes.")]
        public Material[] Materials;

        [Tooltip("Variant index in the parent FloraData.Meshes list.")]
        public int VariantIndex;

        [Tooltip("Number of detail instances baked into this chunk.")]
        public int InstanceCount;

        [Tooltip("Total vertex count of this chunk mesh.")]
        public int VertexCount;
    }
}
