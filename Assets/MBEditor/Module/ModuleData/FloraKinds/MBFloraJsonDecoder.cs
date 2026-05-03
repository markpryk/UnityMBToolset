using System;
using System.Collections.Generic;
using System.Numerics;
using UnityEngine;

namespace MountAndBlade.Data
{
    public static class MBFloraJsonDecoder
    {
        public static DecodedFloraData DecodeFlora(FloraJsonData floraJson)
        {
            if (floraJson == null)
            {
                throw new ArgumentNullException(nameof(floraJson));
            }

            var decoded = new DecodedFloraData
            {
                floraId = floraJson.id,
                meshCount = floraJson.mesh_count,
                flags = floraJson.flags.raw_value,
                density = floraJson.flags.density.value,
                meshes = ExtractMeshes(floraJson.meshes),
                floraType = DetermineFloraType(floraJson),
                terrainConditions = ExtractTerrainConditions(floraJson.flags.terrain_conditions),
                typeFlags = ExtractTypeFlags(floraJson.flags.type_flags)
            };

            return decoded;
        }

        public static DecodedFloraData[] DecodeFlora(FloraJsonData[] floraJson)
        {
            if (floraJson == null || floraJson.Length == 0)
            {
                return new DecodedFloraData[0];
            }

            var decodedFlora = new DecodedFloraData[floraJson.Length];
            for (int i = 0; i < floraJson.Length; i++)
            {
                decodedFlora[i] = DecodeFlora(floraJson[i]);
            }

            return decodedFlora;
        }

        public static List<string> ValidateFlora(DecodedFloraData floraData)
        {
            var warnings = new List<string>();

            if (floraData == null)
            {
                warnings.Add("Flora data is null");
                return warnings;
            }

            if (string.IsNullOrEmpty(floraData.floraId))
            {
                warnings.Add("Flora ID is empty");
            }

            if (floraData.meshes == null || floraData.meshes.Count == 0)
            {
                warnings.Add("No meshes defined");
            }
            else
            {
                foreach (var mesh in floraData.meshes)
                {
                    if (string.IsNullOrEmpty(mesh.meshName))
                    {
                        warnings.Add("Mesh has empty mesh name");
                    }
                }
            }

            if (floraData.density < 0 || floraData.density > 65535)
            {
                warnings.Add($"Density {floraData.density} is out of valid range (0-65535)");
            }

            if (floraData.terrainConditions == null || floraData.terrainConditions.Count == 0)
            {
                warnings.Add("No terrain conditions defined - flora won't spawn anywhere");
            }

            if (floraData.typeFlags == null || floraData.typeFlags.Count == 0)
            {
                warnings.Add("No type flags defined - flora type is unknown");
            }

            return warnings;
        }

        public static string DetermineFloraType(FloraJsonData floraJson)
        {
            if (floraJson.flags?.type_flags == null)
            {
                return "Other";
            }

            foreach (var typeFlag in floraJson.flags.type_flags)
            {
                switch (typeFlag.name)
                {
                    case "fkf_grass":
                        return "Grass";
                    case "fkf_tree":
                        return "Trees";
                    case "fkf_rock":
                        return "Rocks";
                }
            }

            return "Other";
        }

        private static List<FloraMeshData> ExtractMeshes(FloraMeshJsonData[] meshesJson)
        {
            var meshes = new List<FloraMeshData>();

            if (meshesJson == null || meshesJson.Length == 0)
            {
                return meshes;
            }

            foreach (var meshJson in meshesJson)
            {
                if (meshJson == null)
                    continue;

                meshes.Add(new FloraMeshData
                {
                    meshName = meshJson.mesh_name,
                    collisionObject = string.IsNullOrEmpty(meshJson.collision_object) ? "0" : meshJson.collision_object
                });
            }

            return meshes;
        }

        private static List<TerrainConditionData> ExtractTerrainConditions(TerrainConditionJsonData[] terrainJson)
        {
            var conditions = new List<TerrainConditionData>();

            if (terrainJson == null || terrainJson.Length == 0)
            {
                return conditions;
            }

            foreach (var terrain in terrainJson)
            {
                if (terrain == null)
                    continue;

                conditions.Add(new TerrainConditionData
                {
                    name = terrain.name,
                    value = terrain.value,
                    hex = terrain.hex
                });
            }

            return conditions;
        }

        private static List<TypeFlagData> ExtractTypeFlags(TypeFlagJsonData[] typeFlagsJson)
        {
            var typeFlags = new List<TypeFlagData>();

            if (typeFlagsJson == null || typeFlagsJson.Length == 0)
            {
                return typeFlags;
            }

            foreach (var typeFlag in typeFlagsJson)
            {
                if (typeFlag == null)
                    continue;

                typeFlags.Add(new TypeFlagData
                {
                    name = typeFlag.name,
                    value = typeFlag.value,
                    hex = typeFlag.hex
                });
            }

            return typeFlags;
        }

        [Serializable]
        public class FloraJsonData
        {
            public string id;
            public int mesh_count;
            public FlagsJsonData flags;
            public FloraMeshJsonData[] meshes;
        }

        [Serializable]
        public class FlagsJsonData
        {
            public string raw_value;
            public string raw_hex;
            public TypeFlagJsonData[] type_flags;
            public TerrainConditionJsonData[] terrain_conditions;
            public DensityJsonData density;
        }

        [Serializable]
        public class TypeFlagJsonData
        {
            public string hex;
            public string name;
            public int value;
        }

        [Serializable]
        public class TerrainConditionJsonData
        {
            public string hex;
            public string name;
            public int value;
        }

        [Serializable]
        public class DensityJsonData
        {
            public string description;
            public int value;
        }

        [Serializable]
        public class FloraMeshJsonData
        {
            public string mesh_name;
            public string collision_object;
        }

        [Serializable]
        public class DecodedFloraData
        {
            public string floraId;
            public int meshCount;
            public string flags;
            public int density;
            public List<FloraMeshData> meshes;
            public string floraType;
            public List<TerrainConditionData> terrainConditions;
            public List<TypeFlagData> typeFlags;

            public override string ToString()
            {
                return $"Flora: {floraId} - Type: {floraType}, Density: {density}, Meshes: {meshes?.Count ?? 0}";
            }
        }
    }

    [Serializable]
    public class FloraMeshData
    {
        public string meshName;
        public string collisionObject;

        public override string ToString()
        {
            return $"{meshName} (Collision: {collisionObject})";
        }
    }

    [Serializable]
    public class TerrainConditionData
    {
        public string name;
        public int value;
        public string hex;

        public override string ToString()
        {
            return $"{name} ({hex})";
        }
    }

    [Serializable]
    public class TypeFlagData
    {
        public string name;
        public int value;
        public string hex;

        public override string ToString()
        {
            return $"{name} ({hex})";
        }
    }
}