using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MountAndBlade.Data;
using Newtonsoft.Json;
using UnityEngine;

namespace MountAndBlade.ModdingToolkit
{
    // TEXTURE DIFF RESULT

    /// <summary>
    /// A single texture entry in the diff - represents a .dds file
    /// that may be new, modified, or unchanged relative to what's
    /// currently imported into the Unity project.
    /// </summary>
    [Serializable]
    public class TextureDiffEntry
    {
        public string TextureName;       // e.g. "armor_a_d" (without extension)
        public string SourcePath;        // full path to .dds in M&B Textures folder
        public string DestPath;          // full path in Unity BRF/Textures folder (may not exist yet)
        public string BrfName;           // which BRF references this texture
        public DiffStatus Status;
        public long SourceSize;
        public long DestSize;
        public DateTime SourceModified;
        public DateTime DestModified;
        public bool Selected = true;

        public bool HasChanges => Status != DiffStatus.Unchanged;

        /// <summary>Human-readable size diff for the UI.</summary>
        public string SizeSummary
        {
            get
            {
                if (Status == DiffStatus.Added)
                    return FormatSize(SourceSize);
                if (Status == DiffStatus.Modified)
                    return $"{FormatSize(DestSize)} → {FormatSize(SourceSize)}";
                return FormatSize(DestSize);
            }
        }

        private static string FormatSize(long bytes)
        {
            if (bytes < 1024) return $"{bytes} B";
            if (bytes < 1024 * 1024) return $"{bytes / 1024f:F1} KB";
            return $"{bytes / (1024f * 1024f):F1} MB";
        }
    }

    /// <summary>
    /// Aggregate diff result for all textures across all BRFs in a module.
    /// </summary>
    [Serializable]
    public class TextureDiffResult
    {
        public List<TextureDiffEntry> Entries = new();

        public bool HasChanges => TotalAdded + TotalModified > 0;
        public int TotalAdded => Entries.Count(e => e.Status == DiffStatus.Added);
        public int TotalModified => Entries.Count(e => e.Status == DiffStatus.Modified);
        public int TotalUnchanged => Entries.Count(e => e.Status == DiffStatus.Unchanged);
        public int SelectedCount => Entries.Count(e => e.Selected && e.HasChanges);

        public string GetSummary()
        {
            var parts = new List<string>();
            if (TotalAdded > 0) parts.Add($"+{TotalAdded} new");
            if (TotalModified > 0) parts.Add($"~{TotalModified} modified");
            parts.Add($"{TotalUnchanged} up to date");
            return string.Join(" | ", parts);
        }
    }

    // TEXTURE DIFF ENGINE

    /// <summary>
    /// Compares source DDS textures in the M&B module's Textures folder
    /// against what has been imported into each BRF's Textures/ subfolder.
    /// Uses file size + last-write-time for fast change detection.
    /// </summary>
    public static class TextureDiffEngine
    {
        /// <summary>
        /// Scan all BRFs in the module and compare their referenced textures
        /// against the source DDS files in the M&B Textures folder.
        /// </summary>
        public static TextureDiffResult Scan(MBModule module)
        {
            var result = new TextureDiffResult();

            // 1. Build lookup of all available source DDS files
            var sourceDdsLookup = BuildSourceDdsLookup(module);
            if (sourceDdsLookup.Count == 0)
            {
                Debug.LogWarning("No source DDS textures found");
                return result;
            }

            // 2. Walk each BRF folder and check its referenced textures
            string resourcePath = MBPathHelpers.ModResourcePath(module.ID);
            if (!Directory.Exists(resourcePath))
                return result;

            var brfFolders = Directory.GetDirectories(resourcePath)
                .Where(d => File.Exists(Path.Combine(d, "data.json")))
                .ToList();

            // Track which textures we've already processed (a texture may be
            // referenced by multiple BRFs - we only show it once, for the first BRF)
            var processedTextures = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var brfFolder in brfFolders)
            {
                string brfName = Path.GetFileName(brfFolder);
                var referencedTexNames = GetReferencedTextureNames(brfFolder);

                string texturesFolder = Path.Combine(brfFolder, "Textures");

                foreach (string texName in referencedTexNames)
                {
                    if (processedTextures.Contains(texName))
                        continue;

                    processedTextures.Add(texName);

                    // Find source DDS (try with and without .dds extension)
                    string lookupKey = Path.GetFileNameWithoutExtension(texName);
                    if (!sourceDdsLookup.TryGetValue(lookupKey, out string sourcePath))
                        continue; // texture not in source folder - skip (may be engine-internal)

                    // Check if already imported
                    string destPath = Path.Combine(texturesFolder, Path.GetFileName(sourcePath));
                    var sourceInfo = new FileInfo(sourcePath);

                    var entry = new TextureDiffEntry
                    {
                        TextureName = lookupKey,
                        SourcePath = sourcePath,
                        DestPath = destPath,
                        BrfName = brfName,
                        SourceSize = sourceInfo.Length,
                        SourceModified = sourceInfo.LastWriteTimeUtc
                    };

                    if (File.Exists(destPath))
                    {
                        var destInfo = new FileInfo(destPath);
                        entry.DestSize = destInfo.Length;
                        entry.DestModified = destInfo.LastWriteTimeUtc;

                        // Compare by size + last-write-time
                        if (sourceInfo.Length != destInfo.Length ||
                            sourceInfo.LastWriteTimeUtc > destInfo.LastWriteTimeUtc)
                        {
                            entry.Status = DiffStatus.Modified;
                        }
                        else
                        {
                            entry.Status = DiffStatus.Unchanged;
                        }
                    }
                    else
                    {
                        entry.Status = DiffStatus.Added;
                    }

                    result.Entries.Add(entry);
                }
            }

            // Sort: Added first, then Modified, then Unchanged
            result.Entries.Sort((a, b) =>
            {
                int statusOrder = GetStatusOrder(a.Status).CompareTo(GetStatusOrder(b.Status));
                return statusOrder != 0 ? statusOrder : string.Compare(a.TextureName, b.TextureName, StringComparison.OrdinalIgnoreCase);
            });

            return result;
        }

        /// <summary>
        /// Build a lookup of available DDS files in the M&B Textures folder.
        /// For Native: scans the game's root Textures/ folder.
        /// For mods: scans ONLY the module's own Textures/ folder -
        /// native textures are already imported and resolved from Unity.
        /// Key = filename without extension, Value = full path.
        /// </summary>
        private static Dictionary<string, string> BuildSourceDdsLookup(MBModule module)
        {
            var lookup = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            bool isNative = module.ID == "Native";

            if (isNative)
            {
                string nativeTexturesPath = Path.Combine(MBEditorManager.MbEditorSettings.MbPath, "Textures");
                AddDdsFiles(lookup, nativeTexturesPath);
            }
            else
            {
                // For mods: only the module's own textures - native ones are already imported
                string moduleTexturesPath = Path.Combine(
                    MBEditorManager.MbEditorSettings.MbPath,
                    "Modules", module.ID, "Textures");
                AddDdsFiles(lookup, moduleTexturesPath);
            }

            return lookup;
        }

        private static void AddDdsFiles(Dictionary<string, string> lookup, string folder)
        {
            if (!Directory.Exists(folder)) return;

            foreach (string ddsFile in Directory.GetFiles(folder, "*.dds"))
            {
                string key = Path.GetFileNameWithoutExtension(ddsFile);
                lookup[key] = ddsFile; // later entries override earlier (mod overrides native)
            }
        }

        /// <summary>
        /// Get all texture names referenced by a BRF's data.json (from materials).
        /// </summary>
        private static HashSet<string> GetReferencedTextureNames(string brfFolder)
        {
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            string dataJsonPath = Path.Combine(brfFolder, "data.json");

            if (!File.Exists(dataJsonPath)) return names;

            try
            {
                string json = File.ReadAllText(dataJsonPath);
                var brfData = JsonConvert.DeserializeObject<BrfData>(json);

                // Collect texture names from materials
                if (brfData?.materials != null)
                {
                    foreach (var mat in brfData.materials)
                    {
                        AddTexName(names, mat.diffuseA);
                        AddTexName(names, mat.diffuseB);
                        AddTexName(names, mat.bump);
                        AddTexName(names, mat.enviro);
                        AddTexName(names, mat.spec);
                    }
                }

                // Also include explicit texture entries
                if (brfData?.textures != null)
                {
                    foreach (var tex in brfData.textures)
                    {
                        AddTexName(names, tex.name);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"Failed to read data.json in {brfFolder}: {ex.Message}");
            }

            return names;
        }

        private static void AddTexName(HashSet<string> set, string texName)
        {
            if (string.IsNullOrEmpty(texName) || texName == "none")
                return;
            set.Add(Path.GetFileNameWithoutExtension(texName));
        }

        private static int GetStatusOrder(DiffStatus status) => status switch
        {
            DiffStatus.Added => 0,
            DiffStatus.Modified => 1,
            DiffStatus.Removed => 2,
            DiffStatus.Unchanged => 3,
            _ => 4
        };
    }
}
