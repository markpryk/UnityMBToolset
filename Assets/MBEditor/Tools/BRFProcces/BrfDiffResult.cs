using System;
using System.Collections.Generic;
using System.Linq;

namespace MountAndBlade.Data
{
    /// <summary>
    /// Status of a single entry in a BRF diff comparison.
    /// </summary>
    public enum DiffStatus
    {
        Unchanged,
        Added,
        Removed,
        Modified
    }

    /// <summary>
    /// A single diff entry representing a named BRF resource (mesh, material, texture, or body)
    /// that has been added, removed, modified, or remains unchanged between two data.json snapshots.
    /// </summary>
    [Serializable]
    public class BrfDiffEntry<T> where T : class
    {
        public string Name;
        public DiffStatus Status;
        public T OldValue;               // null if Added
        public T NewValue;               // null if Removed
        public List<string> ChangedFields = new(); // populated for Modified entries
        public bool Selected = true;     // UI toggle - defaults to selected

        /// <summary>
        /// Mesh names referenced by this entry that are missing from the BRF database.
        /// Populated by dependency checking.
        /// </summary>
        public List<string> MissingMeshDependencies = new();

        public bool HasChanges => Status != DiffStatus.Unchanged;
        public bool HasMissingDependencies => MissingMeshDependencies.Count > 0;
    }

    /// <summary>
    /// Complete diff result for a single BRF file, containing categorized lists
    /// of mesh, material, texture, and body changes.
    /// </summary>
    [Serializable]
    public class BrfDiffResult
    {
        public string BrfName;
        public string BrfFolderPath;
        public string SourceBrfPath;

        public List<BrfDiffEntry<BrfMesh>> Meshes = new();
        public List<BrfDiffEntry<BrfMaterial>> Materials = new();
        public List<BrfDiffEntry<BrfTexture>> Textures = new();
        public List<BrfDiffEntry<BrfBody>> Bodies = new();

        // --- Aggregate properties ---

        public bool HasChanges => TotalAdded + TotalRemoved + TotalModified > 0;

        public int TotalAdded =>
            Meshes.Count(e => e.Status == DiffStatus.Added) +
            Materials.Count(e => e.Status == DiffStatus.Added) +
            Textures.Count(e => e.Status == DiffStatus.Added) +
            Bodies.Count(e => e.Status == DiffStatus.Added);

        public int TotalRemoved =>
            Meshes.Count(e => e.Status == DiffStatus.Removed) +
            Materials.Count(e => e.Status == DiffStatus.Removed) +
            Textures.Count(e => e.Status == DiffStatus.Removed) +
            Bodies.Count(e => e.Status == DiffStatus.Removed);

        public int TotalModified =>
            Meshes.Count(e => e.Status == DiffStatus.Modified) +
            Materials.Count(e => e.Status == DiffStatus.Modified) +
            Textures.Count(e => e.Status == DiffStatus.Modified) +
            Bodies.Count(e => e.Status == DiffStatus.Modified);

        public int TotalUnchanged =>
            Meshes.Count(e => e.Status == DiffStatus.Unchanged) +
            Materials.Count(e => e.Status == DiffStatus.Unchanged) +
            Textures.Count(e => e.Status == DiffStatus.Unchanged) +
            Bodies.Count(e => e.Status == DiffStatus.Unchanged);

        /// <summary>
        /// Returns only entries (across all categories) that the user has selected in the UI.
        /// </summary>
        public int SelectedCount =>
            Meshes.Count(e => e.Selected && e.HasChanges) +
            Materials.Count(e => e.Selected && e.HasChanges) +
            Textures.Count(e => e.Selected && e.HasChanges) +
            Bodies.Count(e => e.Selected && e.HasChanges);

        /// <summary>
        /// Returns a compact summary string for display in the BRF list panel.
        /// </summary>
        public string GetSummary()
        {
            if (!HasChanges) return "Up to date";

            var parts = new List<string>();
            if (TotalAdded > 0) parts.Add($"+{TotalAdded}");
            if (TotalRemoved > 0) parts.Add($"-{TotalRemoved}");
            if (TotalModified > 0) parts.Add($"~{TotalModified}");
            return string.Join(" ", parts);
        }
    }

    // SCENE PROPS DIFF

    /// <summary>
    /// Diff result for scene props - checks if SOs and prefabs exist.
    /// </summary>
    [Serializable]
    public class ScenePropDiffResult
    {
        public List<BrfDiffEntry<ScenePropSnapshot>> Entries = new();

        public bool HasChanges => TotalMissingSO > 0;
        public int TotalMissingSO => Entries.Count(e => e.Status == DiffStatus.Added);
        public int TotalMissingPrefab => Entries.Count(e => e.ChangedFields.Contains("Missing Prefab"));
        public int TotalExisting => Entries.Count(e => e.Status == DiffStatus.Unchanged);
        public int TotalWithMissingMeshes => Entries.Count(e => e.HasMissingDependencies);

        public string GetSummary()
        {
            var parts = new List<string>();
            if (TotalMissingSO > 0) parts.Add($"+{TotalMissingSO} missing SO");
            if (TotalMissingPrefab > 0) parts.Add($"🏗 {TotalMissingPrefab} missing prefab");
            if (TotalWithMissingMeshes > 0) parts.Add($"⚠ {TotalWithMissingMeshes} missing meshes");
            parts.Add($"{TotalExisting} existing");
            return string.Join(" | ", parts);
        }
    }

    /// <summary>
    /// Flat snapshot of a scene prop for diffing - avoids coupling to ScriptableObject lifecycle.
    /// </summary>
    [Serializable]
    public class ScenePropSnapshot
    {
        public string PropID;
        public string Flags;
        public string Mesh;
        public string Collision;
        public string Triggers;
        public string TypeName;
        public int HitPoints;
        public int UseTime;

        public static ScenePropSnapshot FromDecoded(MBScenePropJsonDecoder.DecodedScenePropData d) => new()
        {
            PropID = d.propId, Flags = d.flags, Mesh = d.mesh,
            Collision = d.collision, Triggers = d.triggers,
            TypeName = d.typeName, HitPoints = d.hitPoints, UseTime = d.useTime
        };

        public static ScenePropSnapshot FromAsset(MBScenePropData a) => new()
        {
            PropID = a.PropID, Flags = a.Flags, Mesh = a.Mesh,
            Collision = a.Collision, Triggers = a.Triggers,
            TypeName = a.TypeName, HitPoints = a.HitPoints, UseTime = a.UseTime
        };
    }

    // ITEMS DIFF

    /// <summary>
    /// Diff result for items - checks if SOs and prefabs exist.
    /// </summary>
    [Serializable]
    public class ItemDiffResult
    {
        public List<BrfDiffEntry<ItemSnapshot>> Entries = new();

        public bool HasChanges => TotalMissingSO > 0;
        public int TotalMissingSO => Entries.Count(e => e.Status == DiffStatus.Added);
        public int TotalMissingPrefab => Entries.Count(e => e.ChangedFields.Contains("Missing Prefab"));
        public int TotalExisting => Entries.Count(e => e.Status == DiffStatus.Unchanged);
        public int TotalWithMissingMeshes => Entries.Count(e => e.HasMissingDependencies);

        public string GetSummary()
        {
            var parts = new List<string>();
            if (TotalMissingSO > 0) parts.Add($"+{TotalMissingSO} missing SO");
            if (TotalMissingPrefab > 0) parts.Add($"🏗 {TotalMissingPrefab} missing prefab");
            if (TotalWithMissingMeshes > 0) parts.Add($"⚠ {TotalWithMissingMeshes} missing meshes");
            parts.Add($"{TotalExisting} existing");
            return string.Join(" | ", parts);
        }
    }

    /// <summary>
    /// Flat snapshot of an item for diffing.
    /// </summary>
    [Serializable]
    public class ItemSnapshot
    {
        public string ItemID;
        public string ItemName;
        public int Price;
        public string Flags;
        public string Capabilities;
        public string Stats;
        public string ModifierBits;
        public string TriggersCode;
        public List<string> MeshNames = new();
        public List<string> FactionIds = new();

        public static ItemSnapshot FromDecoded(MBItemJsonDecoder.DecodedItemData d) => new()
        {
            ItemID = d.itemId, ItemName = d.itemName, Price = d.price,
            Flags = d.flags, Capabilities = d.capabilities, Stats = d.stats,
            ModifierBits = d.modifierBits, TriggersCode = d.triggersCode,
            MeshNames = d.meshes?.Select(m => m.meshName).ToList() ?? new(),
            FactionIds = d.factionIds ?? new()
        };

        public static ItemSnapshot FromAsset(MBItemData a) => new()
        {
            ItemID = a.ItemID, ItemName = a.ItemName, Price = a.Price,
            Flags = a.Flags, Capabilities = a.Capabilities, Stats = a.Stats,
            ModifierBits = a.ModifierBits, TriggersCode = a.TriggersCode,
            MeshNames = a.Meshes?.Select(m => m.MeshName).ToList() ?? new(),
            FactionIds = a.FactionIds ?? new()
        };
    }

    // SCENES DIFF

    /// <summary>
    /// Diff result for scenes - checks for new, modified, and removed scene entries
    /// with field-level comparison.
    /// </summary>
    [Serializable]
    public class SceneDiffResult
    {
        public List<BrfDiffEntry<SceneSnapshot>> Entries = new();

        public bool HasChanges => Entries.Any(e => e.HasChanges);
        public int TotalAdded => Entries.Count(e => e.Status == DiffStatus.Added);
        public int TotalModified => Entries.Count(e => e.Status == DiffStatus.Modified);
        public int TotalRemoved => Entries.Count(e => e.Status == DiffStatus.Removed);
        public int TotalUnchanged => Entries.Count(e => e.Status == DiffStatus.Unchanged);

        public string GetSummary()
        {
            var parts = new List<string>();
            if (TotalAdded > 0) parts.Add($"+{TotalAdded} new");
            if (TotalModified > 0) parts.Add($"~{TotalModified} modified");
            if (TotalRemoved > 0) parts.Add($"-{TotalRemoved} removed");
            parts.Add($"{TotalUnchanged} unchanged");
            return string.Join(" | ", parts);
        }
    }

    /// <summary>
    /// Flat snapshot of a scene for diffing - avoids coupling to ScriptableObject lifecycle.
    /// </summary>
    [Serializable]
    public class SceneSnapshot
    {
        public string SceneID;
        public string Flags;
        public string MeshName;
        public string BodyName;
        public UnityEngine.Vector2 MinPos;
        public UnityEngine.Vector2 MaxPos;
        public float WaterLevel;
        public string TerrainCode;
        public string[] Chests;
        public string OuterTerrainMesh;
        public string Category;

        public static SceneSnapshot FromDecoded(MBSceneJsonDecoder.DecodedSceneData d) => new()
        {
            SceneID = d.sceneId,
            Flags = d.flags,
            MeshName = d.meshName,
            BodyName = d.bodyName,
            MinPos = d.minPos,
            MaxPos = d.maxPos,
            WaterLevel = d.waterLevel,
            TerrainCode = d.terrainCode,
            Chests = d.chests ?? new string[0],
            OuterTerrainMesh = d.outerTerrainMesh,
            Category = d.category
        };

        public static SceneSnapshot FromAsset(MBSceneData a) => new()
        {
            SceneID = a.SceneID,
            Flags = a.Flags,
            MeshName = a.MeshName,
            BodyName = a.BodyName,
            MinPos = a.MinPos,
            MaxPos = a.MaxPos,
            WaterLevel = a.WaterLevel,
            TerrainCode = a.TerrainCode,
            Chests = a.Chests ?? new string[0],
            OuterTerrainMesh = a.OuterTerrainMesh
        };

        /// <summary>
        /// Compare two snapshots and return a list of changed field names.
        /// </summary>
        public static List<string> GetChangedFields(SceneSnapshot oldSnap, SceneSnapshot newSnap)
        {
            var changes = new List<string>();

            if (!string.Equals(oldSnap.Flags, newSnap.Flags, StringComparison.Ordinal))
                changes.Add("Flags");
            if (!string.Equals(oldSnap.MeshName, newSnap.MeshName, StringComparison.OrdinalIgnoreCase))
                changes.Add("MeshName");
            if (!string.Equals(oldSnap.BodyName, newSnap.BodyName, StringComparison.OrdinalIgnoreCase))
                changes.Add("BodyName");
            if (oldSnap.MinPos != newSnap.MinPos)
                changes.Add("MinPos");
            if (oldSnap.MaxPos != newSnap.MaxPos)
                changes.Add("MaxPos");
            if (Math.Abs(oldSnap.WaterLevel - newSnap.WaterLevel) > 0.001f)
                changes.Add("WaterLevel");
            if (!string.Equals(oldSnap.TerrainCode, newSnap.TerrainCode, StringComparison.Ordinal))
                changes.Add("TerrainCode");
            if (!string.Equals(oldSnap.OuterTerrainMesh, newSnap.OuterTerrainMesh, StringComparison.OrdinalIgnoreCase))
                changes.Add("OuterTerrainMesh");
            if (!ArraysEqual(oldSnap.Chests, newSnap.Chests))
                changes.Add("Chests");

            return changes;
        }

        private static bool ArraysEqual(string[] a, string[] b)
        {
            if (a == null && b == null) return true;
            if (a == null || b == null) return false;
            if (a.Length != b.Length) return false;
            for (int i = 0; i < a.Length; i++)
            {
                if (!string.Equals(a[i], b[i], StringComparison.OrdinalIgnoreCase))
                    return false;
            }
            return true;
        }
    }
}

