using System;
using System.Collections.Generic;
using UnityEngine;

namespace MountAndBlade.Data
{
    [CreateAssetMenu(fileName = "New MB Flora", menuName = "Mount & Blade/Flora Data", order = 1)]
    public class MBFloraData : ScriptableObject
    {
        public string FloraID;  
        // raw value flags that wll decoded using decoder at using editor time
        public string Flags;  
        public int Density;
        public float ColonyRadius;
        public float ColonyThreshold;
        
        public List<FloraMesh> Meshes = new List<FloraMesh>();
    }
    
    [Serializable]
    public class FloraMesh
    {
        public string Mesh;
        public string MeshCollision;
        public string AlternativeMesh;
        public string AlternativeMeshCollision;
    }
}