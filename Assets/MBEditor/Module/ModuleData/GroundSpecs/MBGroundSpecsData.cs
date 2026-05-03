using System;
using System.Collections.Generic;
using UnityEngine;

namespace MountAndBlade.Data
{
    [CreateAssetMenu(fileName = "MBGroundSpecsData", menuName = "Mount & Blade/Ground Specs Data", order = 1)]
    public class MBGroundSpecsData : ScriptableObject
    {
        public List<MBGroundSpec> GroundSpecs = new List<MBGroundSpec>();
    }

    [Serializable]
    public class MBGroundSpec
    {
        public string ID;
        public int Index;
        public string GroundConstant;
        public int Flags;
        public string Material;
        public float UVScale;
        public string MultitexMaterial;
        public bool HasColor;
        public Color Color;
    }
}
