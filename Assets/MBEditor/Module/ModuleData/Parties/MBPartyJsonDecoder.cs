using System;
using System.Collections.Generic;
using UnityEngine;

namespace MountAndBlade.Data
{
    public static class MBPartyJsonDecoder
    {
        public static DecodedPartyData DecodeParty(PartyJsonData partyJson)
        {
            if (partyJson == null)
            {
                throw new ArgumentNullException(nameof(partyJson));
            }

            var decoded = new DecodedPartyData
            {
                partyId = partyJson.id,
                partyName = partyJson.name,
                mapIcon = ExtractMapIcon(partyJson.icon),
                flags = ExtractFlags(partyJson.flags),
                menu = ExtractMenu(partyJson.menu),
                partyTemplate = ExtractPartyTemplate(partyJson.party_template),
                aiBehavior = ExtractAIBehavior(partyJson.ai_behavior),
                aiTarget = partyJson.ai_target.ToString(),
                faction = ExtractFaction(partyJson.faction),
                personality = ExtractPersonality(partyJson.personality),
                direction = partyJson.direction,
                worldPosition = ExtractWorldPosition(partyJson.coordinates),
                troops = ExtractTroops(partyJson.stacks),
                partyType = DeterminePartyType(partyJson.id)
            };

            return decoded;
        }

        public static DecodedPartyData[] DecodeParties(PartyJsonData[] partiesJson)
        {
            if (partiesJson == null || partiesJson.Length == 0)
            {
                return new DecodedPartyData[0];
            }

            var decodedParties = new DecodedPartyData[partiesJson.Length];
            for (int i = 0; i < partiesJson.Length; i++)
            {
                decodedParties[i] = DecodeParty(partiesJson[i]);
            }

            return decodedParties;
        }

        public static List<string> ValidateParty(DecodedPartyData partyData)
        {
            var warnings = new List<string>();

            if (partyData == null)
            {
                warnings.Add("Party data is null");
                return warnings;
            }

            if (string.IsNullOrEmpty(partyData.partyId))
            {
                warnings.Add("Party ID is empty");
            }

            if (string.IsNullOrEmpty(partyData.partyName))
            {
                warnings.Add("Party name is empty");
            }

            if (string.IsNullOrEmpty(partyData.faction))
            {
                warnings.Add("Faction is not set");
            }

            if (string.IsNullOrEmpty(partyData.aiBehavior))
            {
                warnings.Add("AI behavior is not set");
            }

            if (partyData.troops == null || partyData.troops.Count == 0)
            {
                warnings.Add("No troops defined");
            }

            return warnings;
        }

        public static string DeterminePartyType(string partyId)
        {
            if (string.IsNullOrEmpty(partyId))
            {
                return "Other";
            }

            if (partyId.StartsWith("p_town_") || partyId.Contains("_town_"))
                return "Towns";

            if (partyId.StartsWith("p_castle_") || partyId.Contains("_castle_"))
                return "Castles";

            if (partyId.StartsWith("p_village_") || partyId.Contains("_village_"))
                return "Villages";

            if (partyId.Contains("bandit") || partyId.Contains("looter") || 
                partyId.Contains("raider") || partyId.Contains("outlaw"))
                return "Bandits";

            if (partyId.Contains("caravan"))
                return "Caravans";

            if (partyId.Contains("patrol") || partyId.Contains("garrison"))
                return "Patrols";

            if (partyId.Contains("prisoner"))
                return "Prisoners";

            if (partyId.Contains("training_ground"))
                return "TrainingGrounds";

            if (partyId.StartsWith("Bridge_") || partyId.StartsWith("bridge_"))
                return "Bridges";

            if (partyId.Contains("spawn_point") || partyId.Contains("spawn"))
                return "SpawnPoints";

            if (partyId.StartsWith("main_party") || partyId.StartsWith("temp_") || 
                partyId.Contains("casualties") || partyId.Contains("collective_"))
                return "System";

            return "Other";
        }

        private static string ExtractMapIcon(IconJsonData iconJson)
        {
            if (iconJson == null)
            {
                return "";
            }

            return iconJson.name ?? "";
        }

        private static string ExtractFlags(FlagsJsonData flagsJson)
        {
            if (flagsJson == null)
            {
                return "0";
            }

            return flagsJson.raw_flags_value ?? flagsJson.raw_flags_hex ?? "0";
        }

        private static string ExtractMenu(MenuJsonData menuJson)
        {
            if (menuJson == null)
            {
                return "";
            }

            return menuJson.name ?? "";
        }

        private static string ExtractPartyTemplate(PartyTemplateJsonData templateJson)
        {
            if (templateJson == null)
            {
                return "";
            }

            return templateJson.name ?? "";
        }

        private static string ExtractAIBehavior(AIBehaviorJsonData behaviorJson)
        {
            if (behaviorJson == null)
            {
                return "";
            }

            return behaviorJson.name ?? behaviorJson.value ?? "";
        }

        private static string ExtractFaction(FactionJsonData factionJson)
        {
            if (factionJson == null)
            {
                return "";
            }

            return factionJson.name ?? "";
        }

        private static string ExtractPersonality(PersonalityJsonData personalityJson)
        {
            if (personalityJson == null)
            {
                return "0";
            }

            return personalityJson.raw_value.ToString();
        }

        private static Vector2 ExtractWorldPosition(CoordinatesJsonData coordsJson)
        {
            if (coordsJson == null)
            {
                return Vector2.zero;
            }

            return new Vector2(coordsJson.x, coordsJson.y);
        }

        private static List<TroopStackData> ExtractTroops(StackJsonData[] stacksJson)
        {
            var troops = new List<TroopStackData>();

            if (stacksJson == null || stacksJson.Length == 0)
            {
                return troops;
            }

            foreach (var stack in stacksJson)
            {
                if (stack == null)
                    continue;

                troops.Add(new TroopStackData
                {
                    troopId = stack.troop_name ?? stack.troop_id.ToString(),
                    count = stack.count,
                    stackFlags = ExtractStackFlags(stack.member_flags),
                    isPrisoner = stack.is_prisoner
                });
            }

            return troops;
        }

        private static string ExtractStackFlags(MemberFlagsJsonData flagsJson)
        {
            if (flagsJson == null)
            {
                return "0";
            }

            return flagsJson.raw_value.ToString();
        }

        [Serializable]
        public class PartyJsonData
        {
            public string id;
            public string name;
            public IconJsonData icon;
            public FlagsJsonData flags;
            public MenuJsonData menu;
            public PartyTemplateJsonData party_template;
            public FactionJsonData faction;
            public PersonalityJsonData personality;
            public AIBehaviorJsonData ai_behavior;
            public int ai_target;
            public CoordinatesJsonData coordinates;
            public StackJsonData[] stacks;
            public float direction;
        }

        [Serializable]
        public class IconJsonData
        {
            public int value;
            public string hex;
            public string name;
        }

        [Serializable]
        public class FlagsJsonData
        {
            public string raw_flags_value;
            public string raw_flags_hex;
            public string flag_bits_only_hex;
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
        public class MenuJsonData
        {
            public int value;
            public string name;
        }

        [Serializable]
        public class PartyTemplateJsonData
        {
            public int value;
            public string name;
        }

        [Serializable]
        public class FactionJsonData
        {
            public int value;
            public string name;
        }

        [Serializable]
        public class PersonalityJsonData
        {
            public int raw_value;
            public string hex;
            public CourageJsonData courage;
            public AggressivenessJsonData aggressiveness;
            public BanditnessJsonData banditness;
        }

        [Serializable]
        public class CourageJsonData
        {
            public int value;
            public string hex;
            public string name;
            public string description;
        }

        [Serializable]
        public class AggressivenessJsonData
        {
            public int value;
            public string hex;
            public string name;
            public int level;
        }

        [Serializable]
        public class BanditnessJsonData
        {
            public int value;
            public string hex;
            public string name;
        }

        [Serializable]
        public class AIBehaviorJsonData
        {
            public string value;
            public string name;
        }

        [Serializable]
        public class CoordinatesJsonData
        {
            public float x;
            public float y;
        }

        [Serializable]
        public class StackJsonData
        {
            public int troop_id;
            public string troop_name;
            public int count;
            public MemberFlagsJsonData member_flags;
            public bool is_prisoner;
        }

        [Serializable]
        public class MemberFlagsJsonData
        {
            public int raw_value;
            public string hex;
            public DecomposedFlagJsonData[] decomposed;
        }

        [Serializable]
        public class DecodedPartyData
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
            public List<TroopStackData> troops;
            public string partyType;

            public override string ToString()
            {
                return $"Party: {partyId} ({partyName}) - Type: {partyType}, Faction: {faction}, Troops: {troops?.Count ?? 0}";
            }
        }
    }

    [Serializable]
    public class TroopStackData
    {
        public string troopId;
        public int count;
        public string stackFlags;
        public bool isPrisoner;

        public override string ToString()
        {
            string prisonerStr = isPrisoner ? " [Prisoner]" : "";
            return $"{troopId} x{count}{prisonerStr}";
        }
    }
}