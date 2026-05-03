using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace MountAndBlade.Data
{
    public static class MBSceneJsonDecoder
    {
        public static DecodedSceneData DecodeScene(SceneJsonData sceneJson)
        {
            if (sceneJson == null)
            {
                throw new ArgumentNullException(nameof(sceneJson));
            }

            var decoded = new DecodedSceneData
            {
                sceneId = sceneJson.id,
                flags = ExtractFlags(sceneJson.flags),
                meshName = sceneJson.mesh_name ?? "none",
                bodyName = sceneJson.body_name ?? "none",
                minPos = ExtractPosition(sceneJson.min_pos),
                maxPos = ExtractPosition(sceneJson.max_pos),
                waterLevel = sceneJson.water_level,
                terrainCode = sceneJson.terrain_code ?? "0",
                accessibleScenes = sceneJson.other_scenes ?? new string[0],
                chests = sceneJson.chest_troops ?? new string[0],
                outerTerrainMesh = sceneJson.outer_terrain_border ?? "",
                category = DetermineSceneCategory(sceneJson)
            };

            return decoded;
        }

        public static DecodedSceneData[] DecodeScenes(SceneJsonData[] scenesJson)
        {
            if (scenesJson == null || scenesJson.Length == 0)
            {
                return new DecodedSceneData[0];
            }

            var decodedScenes = new DecodedSceneData[scenesJson.Length];
            for (int i = 0; i < scenesJson.Length; i++)
            {
                decodedScenes[i] = DecodeScene(scenesJson[i]);
            }

            return decodedScenes;
        }

        public static List<string> ValidateScene(DecodedSceneData sceneData)
        {
            var warnings = new List<string>();

            if (sceneData == null)
            {
                warnings.Add("Scene data is null");
                return warnings;
            }

            if (string.IsNullOrEmpty(sceneData.sceneId))
            {
                warnings.Add("Scene ID is empty");
            }

            bool isIndoor = sceneData.flags.Contains("sf_indoors") || sceneData.flags == "1";

            if (isIndoor)
            {
                if (sceneData.meshName == "none" || string.IsNullOrEmpty(sceneData.meshName))
                {
                    warnings.Add("Indoor scene should have a mesh name (not 'none')");
                }

                if (sceneData.bodyName == "none" || string.IsNullOrEmpty(sceneData.bodyName))
                {
                    warnings.Add("Indoor scene should have a body name (not 'none')");
                }

                if (sceneData.terrainCode != "0" && !string.IsNullOrEmpty(sceneData.terrainCode))
                {
                    warnings.Add("Indoor scene should have terrain code '0'");
                }
            }
            else
            {
                if (sceneData.meshName != "none")
                {
                    warnings.Add("Outdoor scene should have mesh name 'none'");
                }

                if (sceneData.bodyName != "none")
                {
                    warnings.Add("Outdoor scene should have body name 'none'");
                }
            }

            if (sceneData.minPos.x >= sceneData.maxPos.x || sceneData.minPos.y >= sceneData.maxPos.y)
            {
                warnings.Add("Min position should be less than max position");
            }

            return warnings;
        }

        public static string DetermineSceneCategory(SceneJsonData sceneJson)
        {
            string id = sceneJson.id?.ToLower() ?? "";

            bool isIndoor = false;
            bool isGenerated = false;

            if (sceneJson.flags?.decomposed != null)
            {
                foreach (var flag in sceneJson.flags.decomposed)
                {
                    if (flag.name == "sf_indoors")
                        isIndoor = true;
                    if (flag.name == "sf_generate")
                        isGenerated = true;
                }
            }

            if (isIndoor)
                return "Indoor";

            if (id.Contains("random_scene") || id.Contains("random_multi"))
                return "Random";

            if (id.StartsWith("town_") || id.Contains("_town_"))
                return "Towns";

            if (id.StartsWith("castle_") || id.Contains("_castle_"))
                return "Castles";

            if (id.StartsWith("village_") || id.Contains("_village_"))
                return "Villages";

            if (id.Contains("multi_scene") || id.Contains("multiplayer"))
                return "Multiplayer";

            if (id.Contains("quick_battle"))
                return "QuickBattle";

            if (id.Contains("arena"))
                return "Arenas";

            if (id.Contains("lair_") || id.Contains("bandit"))
                return "BanditLairs";

            if (id == "conversation_scene" || id == "water" || id.Contains("meeting_scene") ||
                id == "wedding" || id == "training_ground" || id == "novice_ground")
                return "Special";

            if (isGenerated)
                return "Generated";

            return "Other";
        }

        private static string ExtractFlags(FlagsJsonData flagsJson)
        {
            if (flagsJson == null)
            {
                return "0";
            }

            return flagsJson.raw_value.ToString();
        }

        private static Vector2 ExtractPosition(PositionJsonData posJson)
        {
            if (posJson == null)
            {
                return Vector2.zero;
            }

            return new Vector2(posJson.x, posJson.y);
        }

        [Serializable]
        public class SceneJsonData
        {
            public string id;
            public FlagsJsonData flags;
            public string mesh_name;
            public string body_name;
            public PositionJsonData min_pos;
            public PositionJsonData max_pos;
            public float water_level;
            public string terrain_code;
            public string[] other_scenes;
            public string[] chest_troops;
            public string outer_terrain_border;
        }

        [Serializable]
        public class FlagsJsonData
        {
            public int raw_value;
            public string hex;
            public FlagDecomposedData[] decomposed;
        }

        [Serializable]
        public class FlagDecomposedData
        {
            public string name;
            public int value;
            public string hex;
        }

        [Serializable]
        public class PositionJsonData
        {
            public float x;
            public float y;
        }

        [Serializable]
        public class DecodedSceneData
        {
            public string sceneId;
            public string flags;
            public string meshName;
            public string bodyName;
            public Vector2 minPos;
            public Vector2 maxPos;
            public float waterLevel;
            public string terrainCode;
            public string[] accessibleScenes;
            public string[] chests;
            public string outerTerrainMesh;
            public string category;

            public override string ToString()
            {
                return $"Scene: {sceneId} - Category: {category}, Bounds: ({minPos.x},{minPos.y}) to ({maxPos.x},{maxPos.y})";
            }
        }
    }
}