using System;
using System.Collections.Generic;
using UnityEngine;

namespace MountAndBlade.Data
{
    public static class MBPartyTemplateJsonDecoder
    {
        public static DecodedPartyTemplateData DecodePartyTemplate(TemplateJsonData templateJson)
        {
            if (templateJson == null)
            {
                throw new ArgumentNullException(nameof(templateJson));
            }

            var decoded = new DecodedPartyTemplateData
            {
                templateId = templateJson.id,
                templateName = templateJson.name,
                flags = ExtractFlags(templateJson.flags),
                menu = ExtractMenu(templateJson.menu),
                faction = ExtractFaction(templateJson.faction),
                personality = ExtractPersonality(templateJson.personality),
                templateStacks = ExtractTemplateStacks(templateJson.stacks),
                category = DetermineCategory(templateJson)
            };

            return decoded;
        }

        public static DecodedPartyTemplateData[] DecodePartyTemplates(TemplateJsonData[] templatesJson)
        {
            if (templatesJson == null || templatesJson.Length == 0)
            {
                return new DecodedPartyTemplateData[0];
            }

            var decodedTemplates = new DecodedPartyTemplateData[templatesJson.Length];
            for (int i = 0; i < templatesJson.Length; i++)
            {
                decodedTemplates[i] = DecodePartyTemplate(templatesJson[i]);
            }

            return decodedTemplates;
        }

        public static List<string> ValidatePartyTemplate(DecodedPartyTemplateData templateData)
        {
            var warnings = new List<string>();

            if (templateData == null)
            {
                warnings.Add("Party template data is null");
                return warnings;
            }

            if (string.IsNullOrEmpty(templateData.templateId))
            {
                warnings.Add("Template ID is empty");
            }

            if (string.IsNullOrEmpty(templateData.templateName))
            {
                warnings.Add("Template name is empty");
            }

            if (templateData.templateStacks == null || templateData.templateStacks.Count == 0)
            {
                warnings.Add("No troop stacks defined");
            }
            else if (templateData.templateStacks.Count > 6)
            {
                warnings.Add($"Template has {templateData.templateStacks.Count} stacks. Mount & Blade supports maximum 6 stacks.");
            }

            foreach (var stack in templateData.templateStacks)
            {
                if (stack.minCount < 0)
                {
                    warnings.Add($"Troop {stack.troopId} has negative min count: {stack.minCount}");
                }

                if (stack.maxCount < stack.minCount)
                {
                    warnings.Add($"Troop {stack.troopId} has max count ({stack.maxCount}) less than min count ({stack.minCount})");
                }

                if (string.IsNullOrEmpty(stack.troopId))
                {
                    warnings.Add("Stack has empty troop ID");
                }
            }

            return warnings;
        }

        public static string DetermineCategory(TemplateJsonData templateJson)
        {
            string templateId = templateJson.id?.ToLower() ?? "";
            string templateName = templateJson.name?.ToLower() ?? "";

            bool hasBanditness = templateJson.personality?.banditness != null;

            bool isCivilian = false;
            bool isQuest = false;

            if (templateJson.flags?.decomposed != null)
            {
                foreach (var flag in templateJson.flags.decomposed)
                {
                    if (flag.name == "pf_civilian") isCivilian = true;
                    if (flag.name == "pf_quest_party") isQuest = true;
                }
            }

            if (hasBanditness || templateId.Contains("bandit") || templateName.Contains("bandit"))
                return "Bandits";

            if (isQuest || templateId.Contains("quest"))
                return "Quest";

            if (templateId.Contains("merchant") || templateId.Contains("caravan"))
                return "Merchants";

            if (templateId.Contains("reinforcement") || templateId.Contains("reinforce"))
                return "Reinforcements";

            if (templateId.Contains("prisoner") || templateId.Contains("prison"))
                return "Prisoners";

            if (templateId.Contains("deserter"))
                return "Deserters";

            if (isCivilian || templateId.Contains("village") || templateId.Contains("farmer"))
                return "Civilians";

            if (templateId.Contains("patrol") || templateId.Contains("kingdom") || templateId.Contains("garrison"))
                return "Military";

            return "Other";
        }

        private static string ExtractFlags(FlagsJsonData flagsJson)
        {
            if (flagsJson == null)
            {
                return "0";
            }

            return flagsJson.raw_value ?? flagsJson.raw_flags_hex ?? "0";
        }

        private static string ExtractMenu(MenuJsonData menuJson)
        {
            if (menuJson == null)
            {
                return "";
            }

            return menuJson.name ?? menuJson.value.ToString();
        }

        private static string ExtractFaction(FactionJsonData factionJson)
        {
            if (factionJson == null)
            {
                return "";
            }

            return factionJson.name ?? factionJson.value.ToString();
        }

        private static string ExtractPersonality(PersonalityJsonData personalityJson)
        {
            if (personalityJson == null)
            {
                return "0";
            }

            return personalityJson.raw_value ?? personalityJson.hex ?? "0";
        }

        private static List<TemplateStackData> ExtractTemplateStacks(StackJsonData[] stacksJson)
        {
            var stacks = new List<TemplateStackData>();

            if (stacksJson == null || stacksJson.Length == 0)
            {
                return stacks;
            }

            foreach (var stack in stacksJson)
            {
                if (stack == null)
                    continue;

                stacks.Add(new TemplateStackData
                {
                    troopId = stack.troop_name ?? stack.troop_id.ToString(),
                    minCount = stack.min_count,
                    maxCount = stack.max_count,
                    stackFlags = ExtractStackFlags(stack.member_flags),
                    isPrisoner = stack.is_prisoner
                });
            }

            return stacks;
        }

        private static string ExtractStackFlags(MemberFlagsJsonData flagsJson)
        {
            if (flagsJson == null)
            {
                return "0";
            }

            return flagsJson.raw_value ?? flagsJson.hex ?? "0";
        }

        [Serializable]
        public class TemplateJsonData
        {
            public string id;
            public string name;
            public IconJsonData icon;
            public FlagsJsonData flags;
            public MenuJsonData menu;
            public FactionJsonData faction;
            public PersonalityJsonData personality;
            public StackJsonData[] stacks;
            public int num_stacks;
        }

        [Serializable]
        public class IconJsonData
        {
            public int id;
            public string name;
            public string hex;
        }

        [Serializable]
        public class FlagsJsonData
        {
            public string raw_value;
            public string raw_flags_hex;
            public string flag_bits_only_hex;
            public FlagDecomposedData[] decomposed;
        }

        [Serializable]
        public class FlagDecomposedData
        {
            public string name;
            public string value;
            public string hex;
        }

        [Serializable]
        public class MenuJsonData
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
            public string raw_value;
            public string hex;
            public string preset;
            public PersonalityComponentData courage;
            public PersonalityComponentData aggressiveness;
            public PersonalityComponentData banditness;
        }

        [Serializable]
        public class PersonalityComponentData
        {
            public int value;
            public string hex;
            public string name;
            public string description;
            public int level;
        }

        [Serializable]
        public class StackJsonData
        {
            public int troop_id;
            public string troop_name;
            public int min_count;
            public int max_count;
            public string range;
            public MemberFlagsJsonData member_flags;
            public bool is_prisoner;
        }

        [Serializable]
        public class MemberFlagsJsonData
        {
            public string raw_value;
            public string hex;
            public FlagDecomposedData[] decomposed;
        }

        [Serializable]
        public class DecodedPartyTemplateData
        {
            public string templateId;
            public string templateName;
            public string flags;
            public string menu;
            public string faction;
            public string personality;
            public List<TemplateStackData> templateStacks;
            public string category;

            public override string ToString()
            {
                return $"Template: {templateId} ({templateName}) - Category: {category}, Stacks: {templateStacks?.Count ?? 0}";
            }
        }
    }

    [Serializable]
    public class TemplateStackData
    {
        public string troopId;
        public int minCount;
        public int maxCount;
        public string stackFlags;
        public bool isPrisoner;

        public override string ToString()
        {
            string prisonerStr = isPrisoner ? " [Prisoner]" : "";
            return $"{troopId} ({minCount}-{maxCount}){prisonerStr}";
        }
    }
}