using UnityEngine;

namespace MountAndBlade.Data
{
    /// <summary>
    /// ScriptableObject representation of a Mount & Blade Warband Scene Prop.
    /// Corresponds to item tuples in module_scene_props.py
    /// </summary>
    [CreateAssetMenu(fileName = "New MB Scene Prop", menuName = "Mount & Blade/Scene Prop Data", order = 1)]
    public class MBScenePropData : ScriptableObject
    {
        public string PropID;
        public string Flags;
        public string Mesh;
        public string Collision;
        public string Triggers;
        public string TypeName;
        public int HitPoints;
        public int UseTime;
    }
}