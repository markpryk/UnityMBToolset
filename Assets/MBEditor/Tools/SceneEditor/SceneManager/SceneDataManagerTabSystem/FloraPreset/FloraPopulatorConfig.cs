
//  Data Model - Per-scene decorator configuration (ScriptableObject)

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Random = UnityEngine.Random;

/// <summary>
/// Per-scene flora decorator config. Stores global settings and a
/// stack of <see cref="FloraDecoratorLayer"/>s that define procedural
/// placement rules for terrain details and trees.
///
/// Create via the Decorator subtab or Assets → Create → Mount &amp; Blade.
/// </summary>
public enum FloraWaterFilter
{
    None,
    RejectUnderwater,
    OnlyUnderwater
}

[Serializable]
public class FloraPopulatorConfig : MonoBehaviour
{
    [Header("Global")]
    public int Seed;
    public string WarbandTerrainCode = "0x0000000030000500800d2348000024bd0000637a00004b92"; // Default from native

    [Range(0f, 2f)]
    public float GlobalDensityMultiplier = 1f;
    public float WaterHeight = -0.3f;
    public bool UseCollisionMask;
    public LayerMask CollisionLayer = -1;

    [Tooltip("Cell size in world units for the collision cache grid")]
    public int CollisionCellSize = 64;
    [Range(1, 8)]
    public int CollisionSubdivisions = 4;

    [Tooltip("Automatically respawn when layer parameters change")]
    public bool AutoRespawn;

    [Header("Layers")]
    public List<FloraDecoratorLayer> Layers = new List<FloraDecoratorLayer>();
}


/// <summary>
/// One layer in the decorator stack. Selects flora library entries and
/// defines spawn rules (height, slope, curvature, splatmap masks, noise).
/// </summary>
[Serializable]
public class FloraDecoratorLayer
{
    public string Name = "New Layer";
    public bool Enabled = true;

    [Tooltip("EntryIDs from MBFloraLibrary")]
    public List<string> EntryIDs = new List<string>();

    public int Seed;

    [Range(0f, 100f)]
    public float Probability = 50f;

    public Vector2 HeightRange = new Vector2(-500f, 2000f);

    [Tooltip("0-90 degrees")]
    public Vector2 SlopeRange = new Vector2(0f, 60f);

    [Tooltip("0=concave, 0.5=flat, 1=convex")]
    public Vector2 CurvatureRange = new Vector2(0f, 1f);

    public List<DecoratorSplatmapMask> SplatmapMasks = new List<DecoratorSplatmapMask>();

    [Range(0.001f, 0.5f)]
    public float NoiseScale = 0.05f;
    [Range(0f, 1f)]
    public float NoiseThreshold = 0.3f;

    [Range(0.5f, 50f)]
    public float TreeDistance = 8f;
    public Vector2 TreeScaleRange = new Vector2(0.8f, 1.2f);
    [Range(0f, 1f)]
    public float TreeSinkAmount;

    public FloraWaterFilter WaterFilter = FloraWaterFilter.None;
    public bool CollisionCheck;
    [Range(0f, 10f)]
    public float CollisionClearance = 0f;

    public FloraLayerType LayerType = FloraLayerType.Standard;

    [Tooltip("Index of the Terrain Layer (PGM) to mask out based on this RGL layer's placement.")]
    public int RGL_SplatmapTargetLayer = -1;

    /// <summary>Editor-only foldout state (not serialized)</summary>
    [NonSerialized] public bool Foldout = true;

    /// <summary>Instance count from last spawn, for stats display</summary>
    [NonSerialized] public int LastInstanceCount;

    /// <summary>Time taken by last spawn, for stats display</summary>
    [NonSerialized] public float LastSpawnTimeMs;

    public float VegetationMaskStrength { get; set; }

    /// <summary>Deep copy for duplication</summary>
    public FloraDecoratorLayer Duplicate()
    {
        var copy = new FloraDecoratorLayer
        {
            Name = Name + " (Copy)",
            Enabled = Enabled,
            EntryIDs = new List<string>(EntryIDs),
            Seed = Random.Range(0, 9999),
            LayerType = LayerType,
            Probability = Probability,
            HeightRange = HeightRange,
            SlopeRange = SlopeRange,
            CurvatureRange = CurvatureRange,
            NoiseScale = NoiseScale,
            NoiseThreshold = NoiseThreshold,
            TreeDistance = TreeDistance,
            TreeScaleRange = TreeScaleRange,
            TreeSinkAmount = TreeSinkAmount,
            WaterFilter = WaterFilter,
            CollisionCheck = CollisionCheck,
            RGL_SplatmapTargetLayer = RGL_SplatmapTargetLayer,
            SplatmapMasks = SplatmapMasks.Select(m => new DecoratorSplatmapMask
            {
                LayerID = m.LayerID,
                LayerName = m.LayerName,
                Threshold = m.Threshold
            }).ToList()
        };
        return copy;
    }
}


[Serializable]
public class DecoratorSplatmapMask
{
    public int LayerID;
    public string LayerName;
    [Range(0f, 1f)]
    public float Threshold;
}

public enum FloraWarbandMask
{
    None,
    Earth,
    Green,
    RiverBed
}

public enum FloraLayerType
{
    Standard,
    RGL_Automatic
}

