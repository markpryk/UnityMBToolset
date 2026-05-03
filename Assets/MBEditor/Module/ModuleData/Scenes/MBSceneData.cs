using System;
using System.Collections.Generic;
using System.Numerics;
using UnityEngine;
using UnityEngine.Serialization;

namespace MountAndBlade.Data
{
    /// <summary>
    /// ScriptableObject representation of a Mount & Blade Warband Scene.
    /// Corresponds to item tuples in module_scenes.py
    /// </summary>
    [CreateAssetMenu(fileName = "New MB Scene", menuName = "Mount & Blade/Scene Data", order = 1)]
    public class MBSceneData : ScriptableObject
    {
        public string SceneID;
        public string Flags;
        public string MeshName;
        public string BodyName;
        public UnityEngine.Vector2 MinPos;
        public UnityEngine.Vector2 MaxPos;
        public float WaterLevel;
        public string TerrainCode;
        public string[] AccessibleScenes;
        public string[] Chests;
        public string OuterTerrainMesh;
    }
}