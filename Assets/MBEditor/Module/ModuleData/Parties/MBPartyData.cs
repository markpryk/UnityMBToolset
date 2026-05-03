using System;
using System.Collections.Generic;
using UnityEngine;

namespace MountAndBlade.Data
{
    /// <summary>
    /// ScriptableObject representing a Mount & Blade party with all its data
    /// </summary>
    [CreateAssetMenu(fileName = "New Party", menuName = "Mount & Blade/Party Data", order = 1)]
    public class MBPartyData : ScriptableObject
    {
        public string partyId;
        public string partyName;
        public string mapIcon;
        public string flags;
        public string menu;
        public string partyTemplate;
        public string aiBehavior;
        public string aiTarget;
        public string faction;
        public string personality;
        public float direction;

        public Vector2 worldPosition;
        public List<MBTroopStack> troops = new List<MBTroopStack>();
    }
    
    /// <summary>
    /// Represents a stack of troops in a party
    /// </summary>
    [Serializable]
    public class MBTroopStack
    {
        public string TroopID ;
        public int Count ;
        public string StackFlags ;
        
    }
}