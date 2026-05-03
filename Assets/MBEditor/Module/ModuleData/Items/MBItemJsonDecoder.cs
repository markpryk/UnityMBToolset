using System;
using System.Collections.Generic;
using System.Numerics;
using UnityEngine;

namespace MountAndBlade.Data
{
    public static class MBItemJsonDecoder
    {
        public static DecodedItemData DecodeItem(ItemJsonData itemJson)
        {
            if (itemJson == null)
            {
                throw new ArgumentNullException(nameof(itemJson));
            }

            var decoded = new DecodedItemData
            {
                itemId = itemJson.id,
                itemName = itemJson.name,
                price = itemJson.value,
                flags = itemJson.property_flags?.raw_flags_value ?? "0",
                capabilities = itemJson.capabilities?.raw_value ?? "0",
                stats = itemJson.stats?.raw_expression ?? "0",
                modifierBits = itemJson.modifier_bits?.raw_value ?? "0",
                triggersCode = itemJson.triggers ?? "",
                meshes = ExtractMeshes(itemJson.meshes),
                factionIds = ExtractFactionIds(itemJson.factions),
                itemType = DetermineItemType(itemJson.property_flags)
            };

            return decoded;
        }

        public static DecodedItemData[] DecodeItems(ItemJsonData[] itemsJson)
        {
            if (itemsJson == null || itemsJson.Length == 0)
            {
                return new DecodedItemData[0];
            }

            var decodedItems = new DecodedItemData[itemsJson.Length];
            for (int i = 0; i < itemsJson.Length; i++)
            {
                decodedItems[i] = DecodeItem(itemsJson[i]);
            }

            return decodedItems;
        }

        public static List<string> ValidateItem(DecodedItemData itemData)
        {
            var warnings = new List<string>();

            if (itemData == null)
            {
                warnings.Add("Item data is null");
                return warnings;
            }

            if (string.IsNullOrEmpty(itemData.itemId))
            {
                warnings.Add("Item ID is empty");
            }

            if (string.IsNullOrEmpty(itemData.itemName))
            {
                warnings.Add("Item name is empty");
            }

            if (itemData.price < 0)
            {
                warnings.Add($"Price is negative: {itemData.price}");
            }

            if (itemData.meshes == null || itemData.meshes.Count == 0)
            {
                warnings.Add("No meshes defined");
            }
            else
            {
                foreach (var mesh in itemData.meshes)
                {
                    if (string.IsNullOrEmpty(mesh.meshName))
                    {
                        warnings.Add("Mesh has empty mesh name");
                    }
                }
            }

            if (string.IsNullOrEmpty(itemData.flags) || itemData.flags == "0")
            {
                warnings.Add("No flags defined - item type unknown");
            }

            return warnings;
        }

        public static string DetermineItemType(FlagsJsonData flagsJson)
        {
            if (flagsJson == null || string.IsNullOrEmpty(flagsJson.raw_flags_value))
            {
                return "Unknown";
            }

            if (BigInteger.TryParse(flagsJson.raw_flags_value, out var flagsValue))
            {
                return ItemFlagPropertyDecoder.GetItemType(flagsValue);
            }

            return "Unknown";
        }

        private static List<ItemMeshData> ExtractMeshes(MeshJsonData[] meshesJson)
        {
            var meshes = new List<ItemMeshData>();

            if (meshesJson == null || meshesJson.Length == 0)
            {
                return meshes;
            }

            foreach (var meshJson in meshesJson)
            {
                if (meshJson == null)
                    continue;

                ItemModifierBitsDecoder.TryExtractFirstModifier(meshJson.modifier, out var imodbit, out var rest);

                meshes.Add(new ItemMeshData
                {
                    meshName = meshJson.mesh_name,
                    itemModifier = imodbit,
                    usageType = ItemExtraMeshDecoder.GetMeshUsageType(meshJson.usage_type)
                });
            }

            return meshes;
        }

        private static List<string> ExtractFactionIds(FactionJsonData[] factionsJson)
        {
            var factionIds = new List<string>();

            if (factionsJson == null || factionsJson.Length == 0)
            {
                return factionIds;
            }

            foreach (var faction in factionsJson)
            {
                if (faction == null || string.IsNullOrEmpty(faction.faction_name))
                    continue;

                factionIds.Add(faction.faction_name);
            }

            return factionIds;
        }

        [Serializable]
        public class ItemJsonData
        {
            public string id;
            public string name;
            public MeshJsonData[] meshes;
            public FlagsJsonData property_flags;
            public CapabilitiesJsonData capabilities;
            public int value;
            public StatsJsonData stats;
            public ModifierBitsJsonData modifier_bits;
            public string triggers;
            public FactionJsonData[] factions;
        }

        [Serializable]
        public class FactionJsonData
        {
            public int faction_id;
            public string faction_name;
        }

        [Serializable]
        public class MeshJsonData
        {
            public string mesh_name;
            public string modifier;
            public string usage_type;
        }

        [Serializable]
        public class FlagsJsonData
        {
            public string raw_flags_value;
        }

        [Serializable]
        public class CapabilitiesJsonData
        {
            public string raw_value;
        }

        [Serializable]
        public class ModifierBitsJsonData
        {
            public string raw_value;
        }

        [Serializable]
        public class StatsJsonData
        {
            public string raw_expression;
            public string functions;
        }

        [Serializable]
        public class DecodedItemData
        {
            public string itemId;
            public string itemName;
            public int price;
            public string flags;
            public string capabilities;
            public string stats;
            public string modifierBits;
            public string triggersCode;
            public List<ItemMeshData> meshes;
            public List<string> factionIds;
            public string itemType;

            public override string ToString()
            {
                return $"Item: {itemId} ({itemName}) - Type: {itemType}, Price: {price}, Meshes: {meshes?.Count ?? 0}";
            }
        }
    }

    [Serializable]
    public class ItemMeshData
    {
        public string meshName;
        public string itemModifier;
        public MBItemData.ItemMesh.MeshUsageType usageType;

        public override string ToString()
        {
            return $"{meshName} (Modifier: {itemModifier}, Usage: {usageType})";
        }
    }
}