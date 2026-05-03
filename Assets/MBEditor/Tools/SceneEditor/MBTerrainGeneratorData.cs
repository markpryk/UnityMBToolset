using UnityEngine;

public class MBTerrainGeneratorData
{
    public bool PlaceRiver { get; set; }
    public bool ShadeOcclude { get; set; }
    public bool DeepWater { get; set; }
    public bool DisableGrass { get; set; }

    public int PolygonSize { get; set; } = 3;
    public int TerrainType { get; set; } = 3;
    public int Vegetation { get; set; } = 18;
    public int Ruggedness { get; set; } = 42;
    public int Valley { get; set; } = 58;
    public int HillHeight { get; set; } = 57;
    public int SizeX { get; set; } = 330;
    public int SizeY { get; set; } = 326;

    public uint FloraSeed { get; set; } = 16879;
    public uint RiverSeed { get; set; } = 23272;
    public uint TerrainSeed { get; set; } = 15445;

    public string FullHash { get; set; } = "0x0000000000000000000000000000000000000000";
}
