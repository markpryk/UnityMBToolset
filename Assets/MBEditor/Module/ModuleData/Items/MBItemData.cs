using System;
using System.Collections.Generic;
using System.Numerics;
using UnityEngine;
using UnityEngine.Serialization;

namespace MountAndBlade.Data
{
    /// <summary>
    /// ScriptableObject representation of a Mount & Blade Warband item.
    /// Corresponds to item tuples in module_items.py
    /// </summary>
    [CreateAssetMenu(fileName = "New MB Item", menuName = "Mount & Blade/Item Data", order = 1)]
    public class MBItemData : ScriptableObject
    {
        public string ItemID = "";
        public string ItemName = "";
        public List<ItemMesh> Meshes = new List<ItemMesh>();
        public int Price = 0;
        public string Flags;
        public string Capabilities;
        public string Stats;
        public string ModifierBits;
        public string TriggersCode = "";
        public List<string> FactionIds = new List<string>();

        public string ItemType
        {
            get
            {
                var flagsValue = BigInteger.TryParse(Flags, out var flags) ? (BigInteger)flags : 0;
                return ItemFlagPropertyDecoder.GetItemType(flagsValue);
            }
        }
        // Update your ItemMesh class in MBItemData.cs
        [Serializable]
        public class ItemMesh
        {
            public string MeshName = "";
    
            [Header("Mesh Type")]
            [Tooltip("What this mesh is used for")]
            public MeshUsageType UsageType = MeshUsageType.Default;
    
            [Header("Item Modifier (Visual Variant)")]
            [Tooltip("Item modifier for visual variants (rusty, balanced, etc.)")]
            public string ItemModifier = "0"; // imodbit value
    
            public enum MeshUsageType
            {
                Default,        // Base mesh (0)
                Inventory,      // ixmesh_inventory
                Carry,          // ixmesh_carry (scabbards, quivers)
                FlyingAmmo      // ixmesh_flying_ammo
            }
        }
    }
}