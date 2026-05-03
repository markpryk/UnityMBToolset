using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;

namespace MountAndBlade.Data
{
    /// <summary>
    /// Compares decoded JSON data (scene props, items) against existing ScriptableObject assets.
    /// Simplified: only checks if SO and prefab exist - no deep field comparison.
    /// </summary>
    public static class JsonDiffEngine
    {
        // SCENE PROPS

        /// <summary>
        /// Check which scene props from decoded JSON are missing as SOs or prefabs.
        /// Only reports Added (missing) entries - no field-level diffing.
        /// </summary>
        public static ScenePropDiffResult CompareSceneProps(
            MBScenePropJsonDecoder.DecodedScenePropData[] decoded,
            List<MBScenePropData> existing,
            HashSet<string> knownMeshNames = null,
            string prefabsPath = null)
        {
            var result = new ScenePropDiffResult();

            // Build lookup of existing props by ID
            var existingIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (existing != null)
            {
                foreach (var asset in existing)
                {
                    if (asset != null && !string.IsNullOrEmpty(asset.PropID))
                        existingIds.Add(asset.PropID);
                }
            }

            // Build prefab lookup
            var existingPrefabs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (!string.IsNullOrEmpty(prefabsPath) && Directory.Exists(prefabsPath))
            {
                foreach (var file in Directory.GetFiles(prefabsPath, "*.prefab", SearchOption.TopDirectoryOnly))
                    existingPrefabs.Add(Path.GetFileNameWithoutExtension(file));
            }

            if (decoded != null)
            {
                foreach (var d in decoded)
                {
                    if (d == null || string.IsNullOrEmpty(d.propId)) continue;

                    var newSnap = ScenePropSnapshot.FromDecoded(d);
                    bool hasSO = existingIds.Contains(d.propId);
                    bool hasPrefab = existingPrefabs.Contains(d.propId);

                    var entry = new BrfDiffEntry<ScenePropSnapshot>
                    {
                        Name = d.propId,
                        Status = hasSO ? DiffStatus.Unchanged : DiffStatus.Added,
                        OldValue = null,
                        NewValue = newSnap,
                        Selected = !hasSO // auto-select only missing ones
                    };

                    // Track what's missing
                    if (!hasSO)
                        entry.ChangedFields.Add("Missing SO");
                    if (!hasPrefab)
                        entry.ChangedFields.Add("Missing Prefab");

                    // Check mesh dependency
                    CheckScenePropMeshDependency(entry, newSnap, knownMeshNames);

                    // Auto-deselect entries with missing meshes - can't sync without them
                    if (entry.HasMissingDependencies)
                        entry.Selected = false;

                    result.Entries.Add(entry);
                }
            }

            // Sort: Added (missing) first, then existing
            result.Entries.Sort((a, b) =>
            {
                int order = GetSortOrder(a.Status).CompareTo(GetSortOrder(b.Status));
                return order != 0 ? order : string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase);
            });

            return result;
        }

        private static void CheckScenePropMeshDependency(
            BrfDiffEntry<ScenePropSnapshot> entry,
            ScenePropSnapshot snap,
            HashSet<string> knownMeshNames)
        {
            if (knownMeshNames == null || string.IsNullOrEmpty(snap.Mesh)) return;

            if (snap.Mesh == "0" || snap.Mesh == "none") return;

            if (!knownMeshNames.Contains(snap.Mesh))
                entry.MissingMeshDependencies.Add(snap.Mesh);
        }

        // ITEMS

        /// <summary>
        /// Check which items from decoded JSON are missing as SOs or prefabs.
        /// Only reports Added (missing) entries - no field-level diffing.
        /// </summary>
        public static ItemDiffResult CompareItems(
            MBItemJsonDecoder.DecodedItemData[] decoded,
            List<MBItemData> existing,
            HashSet<string> knownMeshNames = null,
            string prefabsPath = null)
        {
            var result = new ItemDiffResult();

            var existingIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (existing != null)
            {
                foreach (var asset in existing)
                {
                    if (asset != null && !string.IsNullOrEmpty(asset.ItemID))
                        existingIds.Add(asset.ItemID);
                }
            }

            var existingPrefabs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (!string.IsNullOrEmpty(prefabsPath) && Directory.Exists(prefabsPath))
            {
                foreach (var file in Directory.GetFiles(prefabsPath, "*.prefab", SearchOption.TopDirectoryOnly))
                    existingPrefabs.Add(Path.GetFileNameWithoutExtension(file));
            }

            if (decoded != null)
            {
                foreach (var d in decoded)
                {
                    if (d == null || string.IsNullOrEmpty(d.itemId)) continue;

                    var newSnap = ItemSnapshot.FromDecoded(d);
                    bool hasSO = existingIds.Contains(d.itemId);
                    bool hasPrefab = existingPrefabs.Contains(d.itemId);

                    var entry = new BrfDiffEntry<ItemSnapshot>
                    {
                        Name = d.itemId,
                        Status = hasSO ? DiffStatus.Unchanged : DiffStatus.Added,
                        OldValue = null,
                        NewValue = newSnap,
                        Selected = !hasSO
                    };

                    if (!hasSO)
                        entry.ChangedFields.Add("Missing SO");
                    if (!hasPrefab)
                        entry.ChangedFields.Add("Missing Prefab");

                    CheckItemMeshDependencies(entry, newSnap, knownMeshNames);

                    // Auto-deselect entries with missing meshes - can't sync without them
                    if (entry.HasMissingDependencies)
                        entry.Selected = false;

                    result.Entries.Add(entry);
                }
            }

            result.Entries.Sort((a, b) =>
            {
                int order = GetSortOrder(a.Status).CompareTo(GetSortOrder(b.Status));
                return order != 0 ? order : string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase);
            });

            return result;
        }

        private static void CheckItemMeshDependencies(
            BrfDiffEntry<ItemSnapshot> entry,
            ItemSnapshot snap,
            HashSet<string> knownMeshNames)
        {
            if (knownMeshNames == null || snap.MeshNames == null) return;

            foreach (var meshName in snap.MeshNames)
            {
                if (string.IsNullOrEmpty(meshName) || meshName == "0") continue;

                if (!knownMeshNames.Contains(meshName))
                    entry.MissingMeshDependencies.Add(meshName);
            }
        }

        // HELPERS

        /// <summary>
        /// Build a set of all known mesh names from the BRF database.
        /// Includes both full names (e.g. "candle_b.0") and base names (e.g. "candle_b")
        /// since scene props/items reference meshes by base name.
        /// </summary>
        public static HashSet<string> BuildKnownMeshNames(ModBrfDataBase brfDatabase)
        {
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (brfDatabase == null) return names;

            foreach (var brf in brfDatabase.BrfAssets)
            {
                if (brf == null) continue;
                foreach (var mesh in brf.Meshes)
                {
                    if (!string.IsNullOrEmpty(mesh.Name))
                        names.Add(mesh.Name);

                    if (!string.IsNullOrEmpty(mesh.BaseName))
                        names.Add(mesh.BaseName);
                }
            }

            return names;
        }

        private static int GetSortOrder(DiffStatus status) => status switch
        {
            DiffStatus.Added => 0,
            DiffStatus.Modified => 1,
            DiffStatus.Removed => 2,
            DiffStatus.Unchanged => 3,
            _ => 4
        };
    }
}
