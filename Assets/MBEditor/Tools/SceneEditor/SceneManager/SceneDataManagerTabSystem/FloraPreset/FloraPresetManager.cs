using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

//  FloraPresetManager - Save / Load / Apply flora presets
//
//  Static utility class that converts between live config objects
//  (FloraDecoratorConfig, MBFloraLibrary) and the portable FloraPreset
//  ScriptableObject format.
//
//  Entries are matched by EntryID (case-insensitive) so presets work
//  across scenes that share the same module's flora library.

public static class FloraPresetManager
{
    private const string PRESETS_FOLDER = "Assets/MBEditor/FloraPresets";

    //  Capture - Live State → Preset

    /// <summary>
    /// Capture the current decorator config into a preset.
    /// </summary>
    public static void CaptureDecorator(FloraPreset preset, FloraPopulatorConfig config)
    {
        if (config == null) return;

        preset.HasDecoratorData = true;
        var data = preset.populatorData;

        // Global settings
        data.Seed = config.Seed;
        data.GlobalDensityMultiplier = config.GlobalDensityMultiplier;
        data.WaterHeight = config.WaterHeight;
        data.UseCollisionMask = config.UseCollisionMask;
        data.CollisionLayerMask = config.CollisionLayer;
        data.CollisionCellSize = config.CollisionCellSize;
        data.CollisionSubdivisions = config.CollisionSubdivisions;
        data.AutoRespawn = config.AutoRespawn;

        // Layers
        data.Layers.Clear();
        foreach (var layer in config.Layers)
        {
            var lp = new DecoratorLayerPreset
            {
                Name = layer.Name,
                Enabled = layer.Enabled,
                EntryIDs = new List<string>(layer.EntryIDs),
                Seed = layer.Seed,
                Probability = layer.Probability,
                HeightRange = layer.HeightRange,
                SlopeRange = layer.SlopeRange,
                CurvatureRange = layer.CurvatureRange,
                NoiseScale = layer.NoiseScale,
                NoiseThreshold = layer.NoiseThreshold,
                TreeDistance = layer.TreeDistance,
                TreeScaleRange = layer.TreeScaleRange,
                TreeSinkAmount = layer.TreeSinkAmount,
                WaterFilter = layer.WaterFilter,
                CollisionCheck = layer.CollisionCheck,
                CollisionClearance = layer.CollisionClearance,
            };

            foreach (var mask in layer.SplatmapMasks)
            {
                lp.SplatmapMasks.Add(new SplatmapMaskPreset
                {
                    LayerID = mask.LayerID,
                    LayerName = mask.LayerName,
                    Threshold = mask.Threshold,
                });
            }

            data.Layers.Add(lp);
        }
    }

    /// <summary>
    /// Capture the current populator registration state into a preset.
    /// Only saves entries that are registered (to keep presets lean).
    /// Set <paramref name="allEntries"/> to true to capture everything.
    /// </summary>
    public static void CapturePopulator(FloraPreset preset, MBFloraLibrary library, bool allEntries = false)
    {
        if (library == null) return;

        preset.HasLibraryData = true;
        var data = preset.libraryData;
        data.Entries.Clear();

        foreach (var entry in library.Entries)
        {
            if (entry == null || string.IsNullOrEmpty(entry.EntryID)) continue;
            if (!allEntries && !entry.IsRegistered) continue;

            data.Entries.Add(new PopulatorEntryPreset
            {
                EntryID = entry.EntryID,
                Category = entry.Category,
                Biomes = entry.Biomes,
                DetailMode = entry.DetailMode,
                IsRegistered = entry.IsRegistered,
                DetailDensity = entry.DetailDensity,
                TargetCoverage = entry.TargetCoverage,
                NoiseSpread = entry.NoiseSpread,
                PositionJitter = entry.PositionJitter,
                MinWidth = entry.MinWidth,
                MaxWidth = entry.MaxWidth,
                MinHeight = entry.MinHeight,
                MaxHeight = entry.MaxHeight,
                HealthyColor = entry.HealthyColor,
                DryColor = entry.DryColor,
                BendFactor = entry.BendFactor,
            });
        }
    }

    /// <summary>
    /// Capture both decorator and populator state into a single preset.
    /// </summary>
    public static void CaptureAll(FloraPreset preset, FloraPopulatorConfig config, MBFloraLibrary library,
        string moduleId = null, string sceneName = null)
    {
        CaptureDecorator(preset, config);
        CapturePopulator(preset, library);
        preset.SourceModule = moduleId ?? "";
    }


    //  Apply - Preset → Live State

    /// <summary>
    /// Apply decorator preset data onto an existing config.
    /// Returns a report of what was applied and what was missing.
    /// </summary>
    public static PresetApplyReport ApplyDecorator(FloraPreset preset, FloraPopulatorConfig config,
        MBFloraLibrary library = null)
    {
        var report = new PresetApplyReport();
        if (!preset.HasDecoratorData || config == null) return report;

        Undo.RecordObject(config, "Apply Flora Decorator Preset");
        var data = preset.populatorData;

        // Global settings
        config.Seed = data.Seed;
        config.GlobalDensityMultiplier = data.GlobalDensityMultiplier;
        config.WaterHeight = data.WaterHeight;
        config.UseCollisionMask = data.UseCollisionMask;
        config.CollisionLayer = data.CollisionLayerMask;
        config.CollisionCellSize = data.CollisionCellSize;
        config.CollisionSubdivisions = data.CollisionSubdivisions;
        config.AutoRespawn = data.AutoRespawn;

        // Layers
        config.Layers.Clear();
        foreach (var lp in data.Layers)
        {
            var layer = new FloraDecoratorLayer
            {
                Name = lp.Name,
                Enabled = lp.Enabled,
                EntryIDs = new List<string>(lp.EntryIDs),
                Seed = lp.Seed,
                Probability = lp.Probability,
                HeightRange = lp.HeightRange,
                SlopeRange = lp.SlopeRange,
                CurvatureRange = lp.CurvatureRange,
                NoiseScale = lp.NoiseScale,
                NoiseThreshold = lp.NoiseThreshold,
                TreeDistance = lp.TreeDistance,
                TreeScaleRange = lp.TreeScaleRange,
                TreeSinkAmount = lp.TreeSinkAmount,
                WaterFilter = lp.WaterFilter,
                CollisionCheck = lp.CollisionCheck,
                CollisionClearance = lp.CollisionClearance,
            };

            foreach (var mp in lp.SplatmapMasks)
            {
                layer.SplatmapMasks.Add(new DecoratorSplatmapMask
                {
                    LayerID = mp.LayerID,
                    LayerName = mp.LayerName,
                    Threshold = mp.Threshold,
                });
            }

            config.Layers.Add(layer);

            // Validate entry references against current library
            if (library != null)
            {
                foreach (var id in lp.EntryIDs)
                {
                    var entry = library.FindByID(id);
                    if (entry != null)
                        report.MatchedEntries++;
                    else
                        report.MissingEntries.Add(id);
                }
            }

            report.LayersApplied++;
        }

        EditorUtility.SetDirty(config);
        return report;
    }

    /// <summary>
    /// Apply populator preset data onto a library.
    /// Matches entries by EntryID and applies registration + tuning values.
    /// </summary>
    public static PresetApplyReport ApplyPopulator(FloraPreset preset, MBFloraLibrary library)
    {
        var report = new PresetApplyReport();
        if (!preset.HasLibraryData || library == null) return report;

        Undo.RecordObject(library, "Apply Flora Populator Preset");

        // Build lookup from preset entries
        var presetLookup = new Dictionary<string, PopulatorEntryPreset>(StringComparer.OrdinalIgnoreCase);
        foreach (var pe in preset.libraryData.Entries)
        {
            if (!string.IsNullOrEmpty(pe.EntryID))
                presetLookup[pe.EntryID] = pe;
        }

        // Apply to matching library entries
        foreach (var entry in library.Entries)
        {
            if (entry == null || string.IsNullOrEmpty(entry.EntryID)) continue;

            if (presetLookup.TryGetValue(entry.EntryID, out var pe))
            {
                entry.IsRegistered = pe.IsRegistered;
                entry.Category = pe.Category;
                entry.Biomes = pe.Biomes;
                entry.DetailMode = pe.DetailMode;
                entry.DetailDensity = pe.DetailDensity;
                entry.TargetCoverage = pe.TargetCoverage;
                entry.NoiseSpread = pe.NoiseSpread;
                entry.PositionJitter = pe.PositionJitter;
                entry.MinWidth = pe.MinWidth;
                entry.MaxWidth = pe.MaxWidth;
                entry.MinHeight = pe.MinHeight;
                entry.MaxHeight = pe.MaxHeight;
                entry.HealthyColor = pe.HealthyColor;
                entry.DryColor = pe.DryColor;
                entry.BendFactor = pe.BendFactor;
                report.MatchedEntries++;
            }
            else
            {
                entry.IsRegistered = false;
            }
        }

        // Find missing entries (in preset but not in library)
        foreach (var pe in preset.libraryData.Entries)
        {
            if (library.FindByID(pe.EntryID) == null)
                report.MissingEntries.Add(pe.EntryID);
        }

        library.RebuildLookups();
        EditorUtility.SetDirty(library);
        return report;
    }

    /// <summary>
    /// Apply both decorator and populator data from a preset.
    /// </summary>
    public static PresetApplyReport ApplyAll(FloraPreset preset, FloraPopulatorConfig config, MBFloraLibrary library)
    {
        var report = new PresetApplyReport();

        if (preset.HasDecoratorData && config != null)
        {
            var dr = ApplyDecorator(preset, config, library);
            report.Merge(dr);
        }

        if (preset.HasLibraryData && library != null)
        {
            var pr = ApplyPopulator(preset, library);
            report.Merge(pr);
        }

        return report;
    }


    //  Save / Load Helpers

    /// <summary>
    /// Create a new FloraPreset asset via save panel.
    /// Returns the created asset, or null if cancelled.
    /// </summary>
    public static FloraPreset SavePresetDialog(string defaultName = "FloraPreset")
    {
        EnsurePresetsFolder();

        string path = EditorUtility.SaveFilePanelInProject(
            "Save Flora Preset",
            defaultName,
            "asset",
            "Choose where to save the flora preset",
            PRESETS_FOLDER);

        if (string.IsNullOrEmpty(path)) return null;

        var preset = ScriptableObject.CreateInstance<FloraPreset>();
        preset.PresetName = System.IO.Path.GetFileNameWithoutExtension(path);

        AssetDatabase.CreateAsset(preset, path);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"[FloraPreset] Created preset: {path}");
        return preset;
    }

    /// <summary>
    /// Browse for an existing FloraPreset asset.
    /// Returns the selected asset, or null if cancelled.
    /// </summary>
    public static FloraPreset LoadPresetDialog()
    {
        string path = EditorUtility.OpenFilePanel(
            "Load Flora Preset",
            "Assets",
            "asset");

        if (string.IsNullOrEmpty(path)) return null;

        // Convert absolute path to project-relative
        if (path.StartsWith(Application.dataPath))
            path = "Assets" + path.Substring(Application.dataPath.Length);

        var preset = AssetDatabase.LoadAssetAtPath<FloraPreset>(path);
        if (preset == null)
        {
            Debug.LogWarning($"[FloraPreset] Selected file is not a FloraPreset: {path}");
            return null;
        }

        return preset;
    }

    /// <summary>
    /// Find all FloraPreset assets in the project.
    /// </summary>
    public static List<FloraPreset> FindAllPresets()
    {
        var guids = AssetDatabase.FindAssets("t:FloraPreset");
        var presets = new List<FloraPreset>(guids.Length);

        foreach (var guid in guids)
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var preset = AssetDatabase.LoadAssetAtPath<FloraPreset>(path);
            if (preset != null) presets.Add(preset);
        }

        return presets;
    }

    /// <summary>
    /// Overwrite an existing preset asset with new data.
    /// </summary>
    public static void OverwritePreset(FloraPreset preset)
    {
        if (preset == null) return;
        EditorUtility.SetDirty(preset);
        AssetDatabase.SaveAssets();
        Debug.Log($"[FloraPreset] Updated: {AssetDatabase.GetAssetPath(preset)}");
    }

    private static void EnsurePresetsFolder()
    {
        if (!AssetDatabase.IsValidFolder(PRESETS_FOLDER))
        {
            // Create folder chain
            string[] parts = PRESETS_FOLDER.Split('/');
            string current = parts[0]; // "Assets"
            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }
    }


    //  Validation / Preview

    /// <summary>
    /// Preview what would happen if this preset were applied to the given library.
    /// Does not modify anything - just counts matches and misses.
    /// </summary>
    public static PresetApplyReport PreviewApply(FloraPreset preset, MBFloraLibrary library)
    {
        var report = new PresetApplyReport();

        if (preset.HasDecoratorData)
        {
            report.LayersApplied = preset.populatorData.Layers.Count;
            foreach (var lp in preset.populatorData.Layers)
            {
                foreach (var id in lp.EntryIDs)
                {
                    if (library != null && library.FindByID(id) != null)
                        report.MatchedEntries++;
                    else
                        report.MissingEntries.Add(id);
                }
            }
        }

        if (preset.HasLibraryData)
        {
            foreach (var pe in preset.libraryData.Entries)
            {
                if (library != null && library.FindByID(pe.EntryID) != null)
                    report.MatchedEntries++;
                else
                    report.MissingEntries.Add(pe.EntryID);
            }
        }

        return report;
    }
}


//  Apply Report

public class PresetApplyReport
{
    public int LayersApplied;
    public int MatchedEntries;
    public List<string> MissingEntries = new List<string>();

    public bool HasMissing => MissingEntries.Count > 0;

    public void Merge(PresetApplyReport other)
    {
        LayersApplied += other.LayersApplied;
        MatchedEntries += other.MatchedEntries;
        MissingEntries.AddRange(other.MissingEntries);
    }

    public string Summary()
    {
        string msg = $"{MatchedEntries} entries matched";
        if (LayersApplied > 0)
            msg = $"{LayersApplied} layers, " + msg;
        if (HasMissing)
            msg += $", {MissingEntries.Count} missing";
        return msg;
    }
}
