using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

//  FloraPreset - Portable preset for flora configuration
//
//  Stores decorator layer configs and/or populator registration snapshots
//  as serializable data. Entries are referenced by EntryID (string) rather
//  than direct object references, so presets are portable across scenes
//  and modules - entries get resolved against whatever MBFloraLibrary is
//  loaded at apply time.
//
//  Usage:
//    • Decorator presets save the full FloraDecoratorConfig layer stack
//    • Populator presets save which entries are registered + their tuning
//    • A preset can contain either or both

[CreateAssetMenu(fileName = "New Flora Preset", menuName = "Mount & Blade/Flora Preset", order = 4)]
public class FloraPreset : ScriptableObject
{
    [Header("Metadata")]
    public string PresetName = "Untitled";
    public string Description;
     public string SourceModule;

    public bool HasDecoratorData;
    public PopulatorPresetData populatorData = new PopulatorPresetData();
     public bool HasLibraryData;
     public LibraryPresetData libraryData = new LibraryPresetData();
}

//  Decorator Preset Data - mirrors FloraDecoratorConfig

[Serializable]
public class PopulatorPresetData
{
    public int Seed;
    public float GlobalDensityMultiplier = 1f;
    public float WaterHeight;
    public bool UseCollisionMask;
    public int CollisionLayerMask = -1;
    public int CollisionCellSize = 64;
    public int CollisionSubdivisions = 4;
    public bool AutoRespawn;

    public List<DecoratorLayerPreset> Layers = new List<DecoratorLayerPreset>();
}

[Serializable]
public class DecoratorLayerPreset
{
    public string Name = "New Layer";
    public bool Enabled = true;

    // Flora selection - by EntryID, resolved at apply time
    public List<string> EntryIDs = new List<string>();

    // Spawn rules
    public int Seed;
    public float Probability = 50f;
    public Vector2 HeightRange = new Vector2(0f, 1000f);
    public Vector2 SlopeRange = new Vector2(0f, 60f);
    public Vector2 CurvatureRange = new Vector2(0f, 1f);

    // Splatmap masks - by layer name for portability
    public List<SplatmapMaskPreset> SplatmapMasks = new List<SplatmapMaskPreset>();

    // Noise
    public float NoiseScale = 0.05f;
    public float NoiseThreshold = 0.3f;

    // Tree placement
    public float TreeDistance = 8f;
    public Vector2 TreeScaleRange = new Vector2(0.8f, 1.2f);
    public float TreeSinkAmount;

    // Options
    public FloraWaterFilter WaterFilter = FloraWaterFilter.None;
    public bool CollisionCheck;
    public float CollisionClearance = 0f;
}

[Serializable]
public class SplatmapMaskPreset
{
    public int LayerID;
    public string LayerName;
    public float Threshold;
}

//  Populator Preset Data - registration snapshot + per-entry tuning

[Serializable]
public class LibraryPresetData
{
    public List<PopulatorEntryPreset> Entries = new List<PopulatorEntryPreset>();
}

[Serializable]
public class PopulatorEntryPreset
{
    // Identity - resolved by EntryID at apply time
    public string EntryID;
    public FloraCategory Category;
    public FloraBiome Biomes;
    public DetailMode DetailMode;

    // Registration
    public bool IsRegistered;

    // Detail settings
    public float DetailDensity = 1f;
    public float TargetCoverage = 1f;
    public float NoiseSpread = 0.1f;
    public float PositionJitter = 0.5f;

    // Size
    public float MinWidth = 1f;
    public float MaxWidth = 2f;
    public float MinHeight = 1f;
    public float MaxHeight = 2f;

    // Color
    public Color HealthyColor = new Color(0.263f, 0.976f, 0.165f, 1f);
    public Color DryColor = new Color(0.804f, 0.737f, 0.102f, 1f);

    // Tree
    public float BendFactor = 0.5f;
}
