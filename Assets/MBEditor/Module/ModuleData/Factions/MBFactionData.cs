using UnityEngine;
using System;
using System.Collections.Generic;

namespace MountAndBlade.Data
{
    /// <summary>
    /// ScriptableObject representing a Mount & Blade faction with all its properties.
    /// </summary>
    /// 
    [CreateAssetMenu(fileName = "New Faction", menuName = "Mount & Blade/Faction Data", order = 1)]
    public class MBFactionData : ScriptableObject
    {
        public string factionId;
        public string factionName;
        public string flags;
        public float coherence;
        public string factionColor;
        public List<FactionRelation> relations = new List<FactionRelation>();

        // Deprecated ranks system from M&B 0.7xx (usually empty)
        // public List<string> ranks = new List<string>();
    }

    /// <summary>
    /// Represents a relation between two factions
    /// </summary>
    [Serializable]
    public struct FactionRelation
    {
        public string factionId;
        public float relationValue;
    }
}