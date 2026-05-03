using UnityEngine;
using System;
using System.Collections.Generic;

namespace MountAndBlade.Data
{
    /// <summary>
    /// ScriptableObject for storing Mount & Blade map icon data.
    /// Corresponds to entries in module_map_icons.py
    /// </summary>
    [CreateAssetMenu(fileName = "New MapIcon", menuName = "Mount & Blade/Map Icon Data")]
    public class MBMapIconData : ScriptableObject
    {
        public string iconId = "";
        public string displayName = "";
        public string meshName = "";
        public float scale = 0.15f;
        public bool noShadow = false;
        public string soundId = "";
        public float flagOffsetX = 0.15f;
        public float flagOffsetY = 0.173f;
        public float flagOffsetZ = 0.0f;
        public string triggerCode = "";
    }
}