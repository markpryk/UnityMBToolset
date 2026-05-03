using System;
using System.Collections.Generic;
using UnityEngine;

namespace MountAndBlade.Data
{
    /// <summary>
    /// API for decoding Mount & Blade faction JSON data.
    /// Provides reusable methods for parsing, validating, and categorizing faction data.
    /// Can be used by importers, editors, or any external code that needs to process faction JSON.
    /// </summary>
    public static class MBFactionJsonDecoder
    {
        // PUBLIC API

        /// <summary>
        /// Decode a single faction from JSON data
        /// </summary>
        /// <param name="factionJson">The faction JSON data to decode</param>
        /// <returns>Decoded faction data ready for ScriptableObject creation</returns>
        public static DecodedFactionData DecodeFaction(FactionJsonData factionJson)
        {
            if (factionJson == null)
            {
                throw new ArgumentNullException(nameof(factionJson));
            }

            var decoded = new DecodedFactionData
            {
                factionId = factionJson.id,
                factionName = factionJson.name,
                flags = ExtractFlags(factionJson.flags),
                coherence = factionJson.coherence,
                factionColor = ExtractColor(factionJson.color),
                relations = ExtractRelations(factionJson.relations),
                category = DetermineFactionCategory(factionJson.id)
            };

            return decoded;
        }

        /// <summary>
        /// Decode multiple factions from JSON array
        /// </summary>
        /// <param name="factionsJson">Array of faction JSON data</param>
        /// <returns>Array of decoded faction data</returns>
        public static DecodedFactionData[] DecodeFactions(FactionJsonData[] factionsJson)
        {
            if (factionsJson == null || factionsJson.Length == 0)
            {
                return new DecodedFactionData[0];
            }

            var decodedFactions = new DecodedFactionData[factionsJson.Length];
            for (int i = 0; i < factionsJson.Length; i++)
            {
                decodedFactions[i] = DecodeFaction(factionsJson[i]);
            }

            return decodedFactions;
        }

        /// <summary>
        /// Validate a decoded faction and return any validation warnings
        /// </summary>
        /// <param name="factionData">The decoded faction data to validate</param>
        /// <returns>List of validation warnings (empty if valid)</returns>
        public static List<string> ValidateFaction(DecodedFactionData factionData)
        {
            var warnings = new List<string>();

            if (factionData == null)
            {
                warnings.Add("Faction data is null");
                return warnings;
            }

            if (string.IsNullOrEmpty(factionData.factionId))
            {
                warnings.Add("Missing faction ID");
            }

            if (string.IsNullOrEmpty(factionData.factionName))
            {
                warnings.Add("Missing faction name");
            }

            if (factionData.coherence < 0.0f || factionData.coherence > 1.0f)
            {
                warnings.Add($"Coherence value out of range (0.0-1.0): {factionData.coherence}");
            }

            if (factionData.relations != null)
            {
                foreach (var rel in factionData.relations)
                {
                    if (rel.relationValue < -1.0f || rel.relationValue > 1.0f)
                    {
                        warnings.Add($"Relation value out of range for {rel.factionId}: {rel.relationValue}");
                    }

                    if (string.IsNullOrEmpty(rel.factionId))
                    {
                        warnings.Add("Relation has empty faction ID");
                    }
                }
            }

            return warnings;
        }

        /// <summary>
        /// Categorize a faction based on its ID
        /// </summary>
        /// <param name="factionId">The faction ID</param>
        /// <returns>Category name (Kingdoms, Cultures, Bandits, etc.)</returns>
        public static string DetermineFactionCategory(string factionId)
        {
            if (string.IsNullOrEmpty(factionId))
            {
                return "Unknown";
            }

            if (factionId.StartsWith("kingdom_"))
                return "Kingdoms";
            else if (factionId.StartsWith("culture_"))
                return "Cultures";
            else if (factionId.Contains("bandit") || factionId.Contains("outlaw") ||
                     factionId.Contains("deserter") || factionId.Contains("robber"))
                return "Bandits";
            else if (factionId == "no_faction" || factionId == "commoners" ||
                     factionId == "neutral" || factionId == "innocents")
                return "System";
            else if (factionId == "player_faction" || factionId == "player_supporters_faction" ||
                     factionId == "merchants" || factionId == "manhunters")
                return "Special";
            else
                return "Other";
        }

        // EXTRACTION HELPERS

        private static string ExtractFlags(FactionFlagsJsonData flagsJson)
        {
            if (flagsJson == null)
            {
                return "0";
            }

            // Prefer symbolic representation, fall back to hex, then raw value
            if (!string.IsNullOrEmpty(flagsJson.symbolic))
            {
                return flagsJson.symbolic;
            }
            else if (!string.IsNullOrEmpty(flagsJson.hex))
            {
                return flagsJson.hex;
            }
            else
            {
                return flagsJson.raw_value.ToString();
            }
        }

        private static string ExtractColor(FactionColorJsonData colorJson)
        {
            if (colorJson == null)
            {
                return "";
            }

            // Prefer HTML format for Unity Color compatibility
            return colorJson.html ?? colorJson.hex ?? "";
        }

        private static List<FactionRelation> ExtractRelations(FactionRelationJsonData[] relationsJson)
        {
            var relations = new List<FactionRelation>();

            if (relationsJson == null || relationsJson.Length == 0)
            {
                return relations;
            }

            foreach (var relJson in relationsJson)
            {
                if (relJson == null)
                    continue;

                relations.Add(new FactionRelation
                {
                    factionId = relJson.faction_id,
                    relationValue = relJson.relation_value
                });
            }

            return relations;
        }

        // JSON DATA STRUCTURES

        /// <summary>
        /// JSON structure for faction data from Mount & Blade module system
        /// </summary>
        [Serializable]
        public class FactionJsonData
        {
            public string id;
            public string name;
            public FactionFlagsJsonData flags;
            public float coherence;
            public FactionRelationJsonData[] relations;
            public FactionColorJsonData color;
        }

        [Serializable]
        public class FactionFlagsJsonData
        {
            public int raw_value;
            public string hex;
            public string symbolic;
            public FlagData[] flags;
            public MaxPlayerRatingData max_player_rating;
        }

        [Serializable]
        public class FlagData
        {
            public string name;
            public int value;
            public string hex;
        }

        [Serializable]
        public class MaxPlayerRatingData
        {
            public int encoded_value;
            public int original_rating;
            public string note;
        }

        [Serializable]
        public class FactionRelationJsonData
        {
            public string faction_id;
            public string faction_name;
            public float relation_value;
            public string relation_description;
        }

        [Serializable]
        public class FactionColorJsonData
        {
            public string hex;
            public int[] rgb;
            public string html;
        }

        // OUTPUT DATA STRUCTURES

        /// <summary>
        /// Decoded faction data ready for ScriptableObject creation or further processing
        /// </summary>
        [Serializable]
        public class DecodedFactionData
        {
            public string factionId;
            public string factionName;
            public string flags;
            public float coherence;
            public string factionColor;
            public List<FactionRelation> relations;
            public string category;

            /// <summary>
            /// Get a summary string for debugging
            /// </summary>
            public override string ToString()
            {
                return $"Faction: {factionId} ({factionName}) - Category: {category}, Relations: {relations?.Count ?? 0}";
            }
        }
    }

    // HELPER CLASSES

    /// <summary>
    /// Helper for deserializing JSON arrays with Unity's JsonUtility
    /// </summary>
    public static class JsonFactionDeserializeHelper
    {
        public static T[] FromJson<T>(string json)
        {
            string wrappedJson = "{\"items\":" + json + "}";
            Wrapper<T> wrapper = JsonUtility.FromJson<Wrapper<T>>(wrappedJson);
            return wrapper.items;
        }

        [Serializable]
        private class Wrapper<T>
        {
            public T[] items;
        }
    }
}
