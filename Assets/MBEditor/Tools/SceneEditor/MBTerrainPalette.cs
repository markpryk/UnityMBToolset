using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
[CreateAssetMenu(fileName = "TerrainPalette", menuName = "MBTerrain/Terrain Palette")]
public class MBTerrainPalette : ScriptableObject
{
    public List<UnityEngine.TerrainLayer> PaletteLayers = new List<UnityEngine.TerrainLayer>();
}