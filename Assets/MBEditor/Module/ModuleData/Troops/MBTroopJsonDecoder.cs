using System;
using System.Collections.Generic;
using UnityEngine;

namespace MountAndBlade.Data
{
    public static class MBTroopJsonDecoder
    {
        public static DecodedTroopData DecodeTroop(TroopJsonData troopJson)
        {
            if (troopJson == null)
            {
                throw new ArgumentNullException(nameof(troopJson));
            }

            var decoded = new DecodedTroopData
            {
                troopId = troopJson.id,
                troopName = troopJson.name,
                troopNamePlural = troopJson.name_plural,
                troopFlags = ExtractFlags(troopJson.flags),
                scene = troopJson.scene,
                entryPoint = troopJson.entry_point,
                factionId = ExtractFaction(troopJson.faction),
                troopAttributes = ExtractAttributes(troopJson.attributes),
                weaponProficiencies = ExtractProficiencies(troopJson.proficiencies),
                troopSkills = ExtractSkills(troopJson.skills),
                faceCode1 = troopJson.face_code_1 ?? "",
                faceCode2 = troopJson.face_code_2 ?? "",
                troopImageMesh = troopJson.troop_image ?? "",
                inventory = troopJson.inventory ?? new string[0],
                upgradePaths = troopJson.upgrades ?? new string[0]
            };

            return decoded;
        }

        public static DecodedTroopData[] DecodeTroops(TroopJsonData[] troopsJson)
        {
            if (troopsJson == null || troopsJson.Length == 0)
            {
                return new DecodedTroopData[0];
            }

            var decodedTroops = new DecodedTroopData[troopsJson.Length];
            for (int i = 0; i < troopsJson.Length; i++)
            {
                decodedTroops[i] = DecodeTroop(troopsJson[i]);
            }

            return decodedTroops;
        }

        public static List<string> ValidateTroop(DecodedTroopData troopData)
        {
            var warnings = new List<string>();

            if (troopData == null)
            {
                warnings.Add("Troop data is null");
                return warnings;
            }

            if (string.IsNullOrEmpty(troopData.troopId))
            {
                warnings.Add("Troop ID is empty");
            }

            if (string.IsNullOrEmpty(troopData.troopName))
            {
                warnings.Add("Missing troop name");
            }

            if (string.IsNullOrEmpty(troopData.factionId))
            {
                warnings.Add("Missing faction ID");
            }

            if (troopData.inventory == null || troopData.inventory.Length == 0)
            {
                warnings.Add("Empty inventory");
            }

            return warnings;
        }

        private static string ExtractFlags(FlagsJsonData flagsJson)
        {
            if (flagsJson == null)
            {
                return "0";
            }

            return flagsJson.raw_value ?? "0";
        }

        private static string ExtractFaction(FactionJsonData factionJson)
        {
            if (factionJson == null)
            {
                return "";
            }

            return factionJson.symbolic ?? factionJson.raw_value.ToString();
        }

        private static string ExtractAttributes(AttributesJsonData attributesJson)
        {
            if (attributesJson?.parsed == null)
            {
                return "0";
            }

            return attributesJson.parsed.raw_value ?? "0";
        }

        private static string ExtractProficiencies(ProficienciesJsonData proficienciesJson)
        {
            if (proficienciesJson?.parsed == null)
            {
                return "0";
            }

            return proficienciesJson.parsed.raw_value ?? "0";
        }

        private static string ExtractSkills(SkillsJsonData skillsJson)
        {
            if (skillsJson == null)
            {
                return "0";
            }

            return skillsJson.raw_value ?? "0";
        }

        [Serializable]
        public class TroopJsonData
        {
            public string id;
            public string name;
            public string name_plural;
            public string face_code_1;
            public string face_code_2;
            public string troop_image;
            public int reserved;
            public FactionJsonData faction;
            public string scene;
            public int entry_point;
            public FlagsJsonData flags;
            public AttributesJsonData attributes;
            public ProficienciesJsonData proficiencies;
            public SkillsJsonData skills;
            public string[] inventory;
            public string[] upgrades;
        }

        [Serializable]
        public class FactionJsonData
        {
            public int raw_value;
            public string symbolic;
        }

        [Serializable]
        public class FlagsJsonData
        {
            public string raw_value;
            public string[] decomposed;
            public string symbolic;
        }

        [Serializable]
        public class AttributesJsonData
        {
            public string symbolic;
            public AttributesParsedData parsed;
        }

        [Serializable]
        public class AttributesParsedData
        {
            public int cha;
            public int agi;
            public string raw_value;
            public int level;
            public int str;
        }

        [Serializable]
        public class ProficienciesJsonData
        {
            public string symbolic;
            public ProficienciesParsedData parsed;
        }

        [Serializable]
        public class ProficienciesParsedData
        {
            public string raw_value;
            public int archery;
            public int one_handed;
            public int two_handed;
            public int throwing;
            public int polearm;
            public int crossbow;
            public int firearm;
        }

        [Serializable]
        public class SkillsJsonData
        {
            public string raw_value;
            public string symbolic;
        }

        [Serializable]
        public class DecodedTroopData
        {
            public string troopId;
            public string troopName;
            public string troopNamePlural;
            public string troopFlags;
            public string scene;
            public int entryPoint;
            public string factionId;
            public string troopAttributes;
            public string weaponProficiencies;
            public string troopSkills;
            public string faceCode1;
            public string faceCode2;
            public string troopImageMesh;
            public string[] inventory;
            public string[] upgradePaths;

            public override string ToString()
            {
                return $"Troop: {troopId} ({troopName}) - Faction: {factionId}, Inventory: {inventory?.Length ?? 0} items";
            }
        }
    }
}