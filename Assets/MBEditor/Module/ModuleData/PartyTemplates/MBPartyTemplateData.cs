using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace MountAndBlade.Data
{
    /// <summary>
    /// ScriptableObject representing a Mount & Blade party template.
    /// Party templates are blueprints used to spawn parties on the map.
    /// They define troop composition ranges but don't have AI behavior or positions.
    /// </summary>
    [CreateAssetMenu(fileName = "New Party Template", menuName = "Mount & Blade/Party Template Data", order = 2)]
    public class MBPartyTemplateData : ScriptableObject
    {
        public string ID;
        [FormerlySerializedAs("TempalteName")] public string TemplateName;
        public string Flags;
        public string Menu;
        public string Faction;
        public string Personality;
        public List<MBTemplateStack> TemplateStacks = new List<MBTemplateStack>();
        
        public static int MAX_TROOP_STACKS = 6;
    }
    
    /// <summary>
    /// Represents a template troop stack with min/max ranges.
    /// When parties spawn from this template, they get random troop counts within these ranges.
    /// </summary>
    [Serializable]
    public class MBTemplateStack
    {
        public string TroopID;
        public int MinCount;
        public int MaxCount;
        public string StackFlags;
    }
}