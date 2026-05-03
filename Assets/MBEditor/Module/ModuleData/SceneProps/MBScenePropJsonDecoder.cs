using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace MountAndBlade.Data
{
    public static class MBScenePropJsonDecoder
    {
        public static DecodedScenePropData DecodeSceneProp(ScenePropJsonData propJson)
        {
            if (propJson == null)
            {
                throw new ArgumentNullException(nameof(propJson));
            }

            var decoded = new DecodedScenePropData
            {
                propId = propJson.id,
                flags = ExtractFlags(propJson.property_flags),
                mesh = propJson.mesh_name ?? "0",
                collision = propJson.physics_name ?? "0",
                triggers = ExtractTriggers(propJson.triggers),
                typeName = ExtractTypeName(propJson.scene_prop_type),
                hitPoints = propJson.hit_points,
                useTime = propJson.use_time,
                category = DetermineCategory(propJson)
            };

            return decoded;
        }

        public static DecodedScenePropData[] DecodeSceneProps(ScenePropJsonData[] propsJson)
        {
            if (propsJson == null || propsJson.Length == 0)
            {
                return new DecodedScenePropData[0];
            }

            var decodedProps = new DecodedScenePropData[propsJson.Length];
            for (int i = 0; i < propsJson.Length; i++)
            {
                decodedProps[i] = DecodeSceneProp(propsJson[i]);
            }

            return decodedProps;
        }

        public static List<string> ValidateSceneProp(DecodedScenePropData propData)
        {
            var warnings = new List<string>();

            if (propData == null)
            {
                warnings.Add("Scene prop data is null");
                return warnings;
            }

            if (string.IsNullOrEmpty(propData.propId))
            {
                warnings.Add("Empty Prop ID");
            }

            if (string.IsNullOrEmpty(propData.mesh) || propData.mesh == "0")
            {
                warnings.Add("No mesh specified");
            }

            if (propData.useTime > 0 && (string.IsNullOrEmpty(propData.collision) || propData.collision == "0"))
            {
                warnings.Add($"Useable prop (use_time={propData.useTime}) requires collision mesh");
            }

            if (propData.hitPoints < 0)
            {
                warnings.Add($"Hit points cannot be negative: {propData.hitPoints}");
            }

            if (propData.useTime < 0)
            {
                warnings.Add($"Use time cannot be negative: {propData.useTime}");
            }

            return warnings;
        }

        public static string DetermineCategory(ScenePropJsonData propJson)
        {
            if (propJson.scene_prop_type != null)
            {
                string typeName = propJson.scene_prop_type.name ?? "";

                if (typeName.Contains("container"))
                    return "Containers";
                if (typeName.Contains("barrier"))
                    return "Barriers";
                if (typeName.Contains("ai_limiter"))
                    return "AI_Limiters";
                if (typeName.Contains("player_limiter"))
                    return "Player_Limiters";
                if (typeName.Contains("ladder"))
                    return "Ladders";
            }

            if (propJson.property_flags?.decomposed != null)
            {
                var flagNames = propJson.property_flags.decomposed
                    .Select(f => f.name ?? "")
                    .ToList();

                if (flagNames.Any(f => f.Contains("destructible")))
                    return "Destructible";
                if (flagNames.Any(f => f.Contains("moveable")))
                    return "Moveable";
                if (flagNames.Any(f => f.Contains("invisible")))
                    return "Invisible";
            }

            if (propJson.id != null && propJson.id.Contains("light"))
                return "Lights";

            if (propJson.use_time > 0)
                return "Useable";

            return "Other";
        }

        private static string ExtractFlags(PropertyFlagsJsonData flagsJson)
        {
            if (flagsJson == null)
            {
                return "0";
            }

            return flagsJson.raw_flags_value.ToString();
        }

        private static string ExtractTriggers(TriggersJsonData triggersJson)
        {
            if (triggersJson == null || !triggersJson.has_triggers)
            {
                return "";
            }

            return triggersJson.raw_code ?? "";
        }

        private static string ExtractTypeName(ScenePropTypeJsonData typeJson)
        {
            if (typeJson == null)
            {
                return "";
            }

            return typeJson.name ?? typeJson.value.ToString();
        }

        [Serializable]
        public class ScenePropJsonData
        {
            public string id;
            public ScenePropTypeJsonData scene_prop_type;
            public PropertyFlagsJsonData property_flags;
            public string mesh_name;
            public string physics_name;
            public int hit_points;
            public int use_time;
            public TriggersJsonData triggers;
        }

        [Serializable]
        public class ScenePropTypeJsonData
        {
            public int value;
            public string hex;
            public string name;
        }

        [Serializable]
        public class PropertyFlagsJsonData
        {
            public int raw_flags_value;
            public string raw_flags_hex;
            public string property_bits_only_hex;
            public DecomposedFlagJsonData[] decomposed;
        }

        [Serializable]
        public class DecomposedFlagJsonData
        {
            public string name;
            public int value;
            public string hex;
        }

        [Serializable]
        public class TriggersJsonData
        {
            public bool has_triggers;
            public int trigger_count;
            public string raw_code;
            public ParsedTriggerJsonData[] parsed_triggers;
        }

        [Serializable]
        public class ParsedTriggerJsonData
        {
            public string type;
            public string trigger_type;
            public string variable_name;
            public string raw_code;
        }

        [Serializable]
        public class DecodedScenePropData
        {
            public string propId;
            public string flags;
            public string mesh;
            public string collision;
            public string triggers;
            public string typeName;
            public int hitPoints;
            public int useTime;
            public string category;

            public override string ToString()
            {
                return $"SceneProp: {propId} - Category: {category}, Type: {typeName}, HP: {hitPoints}";
            }
        }
    }
}