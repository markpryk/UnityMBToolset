using System;
using System.Collections.Generic;
using System.Linq;

namespace MountAndBlade.Data
{
    /// <summary>
    /// Compares two BrfData instances (old vs new from data.json) and produces
    /// a structured BrfDiffResult showing what was added, removed, or modified.
    /// All comparisons are by name (case-insensitive).
    /// </summary>
    public static class BrfDiffEngine
    {
        /// <summary>
        /// Compare two BrfData snapshots and produce a categorized diff.
        /// </summary>
        /// <param name="oldData">Existing data.json content (currently in Unity project). Can be null for first import.</param>
        /// <param name="newData">Fresh data.json from re-exported BRF. Must not be null.</param>
        /// <param name="brfName">Name of the BRF file being compared.</param>
        /// <returns>A BrfDiffResult with categorized entries.</returns>
        public static BrfDiffResult Compare(BrfData oldData, BrfData newData, string brfName)
        {
            if (newData == null)
                throw new ArgumentNullException(nameof(newData));

            var result = new BrfDiffResult
            {
                BrfName = brfName,
                Meshes = CompareMeshes(
                    oldData?.meshes ?? new List<BrfMesh>(),
                    newData.meshes ?? new List<BrfMesh>()),
                Materials = CompareMaterials(
                    oldData?.materials ?? new List<BrfMaterial>(),
                    newData.materials ?? new List<BrfMaterial>()),
                Textures = CompareTextures(
                    oldData?.textures ?? new List<BrfTexture>(),
                    newData.textures ?? new List<BrfTexture>()),
                Bodies = CompareBodies(
                    oldData?.bodies ?? new List<BrfBody>(),
                    newData.bodies ?? new List<BrfBody>())
            };

            return result;
        }

        // MESH COMPARISON

        private static List<BrfDiffEntry<BrfMesh>> CompareMeshes(
            List<BrfMesh> oldList, List<BrfMesh> newList)
        {
            return CompareNamedList(
                oldList, newList,
                m => m.Name,
                CompareMeshFields);
        }

        private static List<string> CompareMeshFields(BrfMesh old, BrfMesh @new)
        {
            var changes = new List<string>();

            if (!string.Equals(old.Material, @new.Material, StringComparison.OrdinalIgnoreCase))
                changes.Add($"Material: {old.Material} → {@new.Material}");

            if (old.Flags != @new.Flags)
                changes.Add($"Flags: {old.Flags} → {@new.Flags}");

            if (old.LODLevel != @new.LODLevel)
                changes.Add($"LODLevel: {old.LODLevel} → {@new.LODLevel}");

            return changes;
        }

        // MATERIAL COMPARISON

        private static List<BrfDiffEntry<BrfMaterial>> CompareMaterials(
            List<BrfMaterial> oldList, List<BrfMaterial> newList)
        {
            return CompareNamedList(
                oldList, newList,
                m => m.name,
                CompareMaterialFields);
        }

        private static List<string> CompareMaterialFields(BrfMaterial old, BrfMaterial @new)
        {
            var changes = new List<string>();

            if (!string.Equals(old.shader, @new.shader, StringComparison.OrdinalIgnoreCase))
                changes.Add($"Shader: {old.shader} → {@new.shader}");

            if (!string.Equals(old.diffuseA, @new.diffuseA, StringComparison.OrdinalIgnoreCase))
                changes.Add($"DiffuseA: {old.diffuseA} → {@new.diffuseA}");

            if (!string.Equals(old.diffuseB, @new.diffuseB, StringComparison.OrdinalIgnoreCase))
                changes.Add($"DiffuseB: {old.diffuseB} → {@new.diffuseB}");

            if (!string.Equals(old.bump, @new.bump, StringComparison.OrdinalIgnoreCase))
                changes.Add($"Bump: {old.bump} → {@new.bump}");

            if (!string.Equals(old.enviro, @new.enviro, StringComparison.OrdinalIgnoreCase))
                changes.Add($"Enviro: {old.enviro} → {@new.enviro}");

            if (!string.Equals(old.spec, @new.spec, StringComparison.OrdinalIgnoreCase))
                changes.Add($"Spec: {old.spec} → {@new.spec}");

            if (old.flags != @new.flags)
                changes.Add($"Flags: 0x{old.flags:X} → 0x{@new.flags:X}");

            if (Math.Abs(old.specular_value - @new.specular_value) > 0.001f)
                changes.Add($"SpecularValue: {old.specular_value} → {@new.specular_value}");

            if (!ColorEqual(old.color_rgb, @new.color_rgb))
                changes.Add($"Color: [{FormatColor(old.color_rgb)}] → [{FormatColor(@new.color_rgb)}]");

            return changes;
        }

        // TEXTURE COMPARISON

        private static List<BrfDiffEntry<BrfTexture>> CompareTextures(
            List<BrfTexture> oldList, List<BrfTexture> newList)
        {
            return CompareNamedList(
                oldList, newList,
                t => t.name,
                CompareTextureFields);
        }

        private static List<string> CompareTextureFields(BrfTexture old, BrfTexture @new)
        {
            var changes = new List<string>();

            if (old.flags != @new.flags)
                changes.Add($"Flags: 0x{old.flags:X} → 0x{@new.flags:X}");

            return changes;
        }

        // BODY COMPARISON

        private static List<BrfDiffEntry<BrfBody>> CompareBodies(
            List<BrfBody> oldList, List<BrfBody> newList)
        {
            return CompareNamedList(
                oldList, newList,
                b => b.name,
                CompareBodyFields);
        }

        private static List<string> CompareBodyFields(BrfBody old, BrfBody @new)
        {
            var changes = new List<string>();

            int oldCount = old.primitives?.Count ?? 0;
            int newCount = @new.primitives?.Count ?? 0;

            if (oldCount != newCount)
                changes.Add($"Primitives: {oldCount} → {newCount}");

            if (old.flags != @new.flags)
                changes.Add($"Flags: 0x{old.flags:X} → 0x{@new.flags:X}");

            // Compare primitive types if counts match
            if (oldCount == newCount && oldCount > 0)
            {
                for (int i = 0; i < oldCount; i++)
                {
                    if (old.primitives[i].type != @new.primitives[i].type)
                    {
                        changes.Add($"Primitive[{i}] type: {old.primitives[i].type} → {@new.primitives[i].type}");
                    }
                }
            }

            return changes;
        }

        // GENERIC COMPARISON CORE

        /// <summary>
        /// Generic comparison between two lists of named BRF entries.
        /// Matches entries by name (case-insensitive) and produces Added/Removed/Modified/Unchanged entries.
        /// </summary>
        private static List<BrfDiffEntry<T>> CompareNamedList<T>(
            List<T> oldList,
            List<T> newList,
            Func<T, string> getName,
            Func<T, T, List<string>> compareFields) where T : class
        {
            var result = new List<BrfDiffEntry<T>>();

            var oldLookup = new Dictionary<string, T>(StringComparer.OrdinalIgnoreCase);
            foreach (var item in oldList)
            {
                var name = getName(item);
                if (!string.IsNullOrEmpty(name))
                    oldLookup[name] = item;
            }

            var newLookup = new Dictionary<string, T>(StringComparer.OrdinalIgnoreCase);
            foreach (var item in newList)
            {
                var name = getName(item);
                if (!string.IsNullOrEmpty(name))
                    newLookup[name] = item;
            }

            // Check for added and modified entries
            foreach (var kvp in newLookup)
            {
                if (oldLookup.TryGetValue(kvp.Key, out var oldItem))
                {
                    // Exists in both - check for modifications
                    var changedFields = compareFields(oldItem, kvp.Value);

                    result.Add(new BrfDiffEntry<T>
                    {
                        Name = kvp.Key,
                        Status = changedFields.Count > 0 ? DiffStatus.Modified : DiffStatus.Unchanged,
                        OldValue = oldItem,
                        NewValue = kvp.Value,
                        ChangedFields = changedFields,
                        Selected = changedFields.Count > 0 // auto-select only changes
                    });
                }
                else
                {
                    // New entry - Added
                    result.Add(new BrfDiffEntry<T>
                    {
                        Name = kvp.Key,
                        Status = DiffStatus.Added,
                        OldValue = null,
                        NewValue = kvp.Value,
                        Selected = true
                    });
                }
            }

            // Check for removed entries
            foreach (var kvp in oldLookup)
            {
                if (!newLookup.ContainsKey(kvp.Key))
                {
                    result.Add(new BrfDiffEntry<T>
                    {
                        Name = kvp.Key,
                        Status = DiffStatus.Removed,
                        OldValue = kvp.Value,
                        NewValue = null,
                        Selected = false // removals not auto-selected for safety
                    });
                }
            }

            // Sort: Added first, then Modified, then Removed, then Unchanged
            result.Sort((a, b) =>
            {
                int statusOrder = GetStatusSortOrder(a.Status).CompareTo(GetStatusSortOrder(b.Status));
                return statusOrder != 0 ? statusOrder : string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase);
            });

            return result;
        }

        // HELPERS

        private static int GetStatusSortOrder(DiffStatus status) => status switch
        {
            DiffStatus.Added => 0,
            DiffStatus.Modified => 1,
            DiffStatus.Removed => 2,
            DiffStatus.Unchanged => 3,
            _ => 4
        };

        private static bool ColorEqual(float[] a, float[] b)
        {
            if (a == null && b == null) return true;
            if (a == null || b == null) return false;
            if (a.Length != b.Length) return false;

            for (int i = 0; i < a.Length; i++)
            {
                if (Math.Abs(a[i] - b[i]) > 0.001f)
                    return false;
            }

            return true;
        }

        private static string FormatColor(float[] c)
        {
            if (c == null || c.Length < 3) return "null";
            return $"{c[0]:F2}, {c[1]:F2}, {c[2]:F2}";
        }
    }
}
