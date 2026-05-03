using System;
using System.Collections.Generic;
using MountAndBlade.Data;
using UnityEngine;

/// <summary>
/// Categorized library of flora entries for terrain population.
///
/// Categories (model-based):
///   Detail - simple single-mesh, no collision → DetailPrototype (GPU instanced)
///   Tree   - multi sub-mesh or has collision  → TreePrototype (LODGroup)
///
/// Biome tags (from M&B terrain flags, for filtering/grouping):
///   Plain, Steppe, Snow, Desert, PlainForest, SteppeForest, SnowForest, DesertForest
///   An entry can belong to multiple biomes.
/// </summary>
[CreateAssetMenu(fileName = "New Flora Library", menuName = "Mount & Blade/Flora Library", order = 2)]
public class MBFloraLibrary : ScriptableObject
{
    [Header("Source")]
    public string ModuleID;

    [Header("Entries (per-variant)")]
    public List<FloraLibraryEntry> Entries = new List<FloraLibraryEntry>();

    [NonSerialized] private Dictionary<FloraCategory, List<FloraLibraryEntry>> _categoryLookup;
    [NonSerialized] private Dictionary<string, FloraLibraryEntry> _idLookup;
    [NonSerialized] private Dictionary<string, List<FloraLibraryEntry>> _floraIdToVariants;

    public List<FloraLibraryEntry> GetByCategory(FloraCategory category)
    {
        BuildLookupsIfNeeded();
        return _categoryLookup.TryGetValue(category, out var list) ? list : new List<FloraLibraryEntry>();
    }

    public FloraLibraryEntry FindByID(string entryId)
    {
        BuildLookupsIfNeeded();
        _idLookup.TryGetValue(entryId, out var entry);
        return entry;
    }

    public List<FloraLibraryEntry> GetVariants(string floraId)
    {
        BuildLookupsIfNeeded();
        return _floraIdToVariants.TryGetValue(floraId, out var list) ? list : new List<FloraLibraryEntry>();
    }

    public void RebuildLookups()
    {
        _categoryLookup = null;
        _idLookup = null;
        _floraIdToVariants = null;
        BuildLookupsIfNeeded();
    }

    private void BuildLookupsIfNeeded()
    {
        if (_categoryLookup != null) return;

        _categoryLookup = new Dictionary<FloraCategory, List<FloraLibraryEntry>>();
        _idLookup = new Dictionary<string, FloraLibraryEntry>(StringComparer.OrdinalIgnoreCase);
        _floraIdToVariants = new Dictionary<string, List<FloraLibraryEntry>>(StringComparer.OrdinalIgnoreCase);

        foreach (FloraCategory cat in Enum.GetValues(typeof(FloraCategory)))
            _categoryLookup[cat] = new List<FloraLibraryEntry>();

        foreach (var entry in Entries)
        {
            if (entry == null) continue;
            _categoryLookup[entry.Category].Add(entry);

            if (!string.IsNullOrEmpty(entry.EntryID))
                _idLookup[entry.EntryID] = entry;

            if (entry.FloraData != null && !string.IsNullOrEmpty(entry.FloraData.FloraID))
            {
                if (!_floraIdToVariants.TryGetValue(entry.FloraData.FloraID, out var variants))
                {
                    variants = new List<FloraLibraryEntry>();
                    _floraIdToVariants[entry.FloraData.FloraID] = variants;
                }
                variants.Add(entry);
            }
        }
    }

    public int DetailCount => GetByCategory(FloraCategory.Detail).Count;
    public int TreeCount => GetByCategory(FloraCategory.Tree).Count;
}

/// <summary>
/// Detail - single mesh, no collision → DetailPrototype
/// Tree   - multi sub-mesh or has collision → TreePrototype
/// </summary>
public enum FloraCategory
{
    Detail,
    Tree
}

public enum DetailMode
{
    Mesh,
    Billboard
}

/// <summary>
/// Biome flags from M&B terrain conditions.
/// Forest variants are merged into their base biome
/// (e.g. Plain Forest → Plain, Snow Forest → Snow).
/// </summary>
[Flags]
public enum FloraBiome
{
    None    = 0,
    Plain   = 1 << 0,
    Steppe  = 1 << 1,
    Snow    = 1 << 2,
    Desert  = 1 << 3,
    All     = 0xF,
}

[Serializable]
public class FloraLibraryEntry
{
    [Header("Identity")]
    public string EntryID;
    public MBFloraData FloraData;
    public int VariantIndex;
    public string MeshName;
    public FloraCategory Category;

    [Header("Biome")]
    public FloraBiome Biomes;

    [Header("Detail Mode")]
    public DetailMode DetailMode = DetailMode.Mesh;

    [Header("Prefabs")]
    public GameObject Prefab;
    public GameObject PrototypePrefab;
    public Texture2D BillboardTexture;

    [Header("Detail Settings")]
    [Range(0f, 1f)] public float DetailDensity = 1f;
    [Range(0f, 1f)] public float TargetCoverage = 1f;
    [Range(0.01f, 1f)] public float NoiseSpread = 0.1f;
    [Range(0f, 1f)] public float PositionJitter = 0.5f;

    [Header("Size")]
    public float MinWidth = 1f;
    public float MaxWidth = 2f;
    public float MinHeight = 1f;
    public float MaxHeight = 2f;

    [Header("Color")]
    public Color HealthyColor = new Color(0.263f, 0.976f, 0.165f, 1f);
    public Color DryColor = new Color(0.804f, 0.737f, 0.102f, 1f);

    [Header("Tree Settings")]
    public float BendFactor = 0.5f;

    [Header("Flags (read-only)")]
    public string TerrainConditions;
    public string BehaviorSummary;
    public bool HasPointUp;
    public bool HasAlignToGround;

    [Header("State")]
    public bool IsRegistered;
    public int PrototypeIndex = -1;

    public bool HasRequiredAssets
    {
        get
        {
            if (Category == FloraCategory.Detail && DetailMode == DetailMode.Billboard)
                return BillboardTexture != null;
            return PrototypePrefab != null;
        }
    }

    public bool MatchesBiome(FloraBiome filter)
    {
        if (filter == FloraBiome.All) return true;
        if (Biomes == FloraBiome.None) return true;
        return (Biomes & filter) != 0;
    }

    public override string ToString()
    {
        string mode = Category == FloraCategory.Detail ? $" ({DetailMode})" : "";
        return $"{EntryID} [{Category}{mode}] v{VariantIndex}";
    }
}
