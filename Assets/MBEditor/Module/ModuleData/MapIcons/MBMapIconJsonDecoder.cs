using System;
using System.Collections.Generic;
using UnityEngine;

namespace MountAndBlade.Data
{
    public static class MBMapIconJsonDecoder
    {
        public static DecodedMapIconData DecodeMapIcon(MapIconJsonData iconJson)
        {
            if (iconJson == null)
            {
                throw new ArgumentNullException(nameof(iconJson));
            }

            var decoded = new DecodedMapIconData
            {
                iconId = iconJson.id,
                displayName = FormatDisplayName(iconJson.id),
                meshName = iconJson.mesh_name ?? "",
                scale = iconJson.scale,
                noShadow = ExtractNoShadowFlag(iconJson.flags),
                soundId = ExtractSoundId(iconJson.sound),
                flagOffsetX = iconJson.offset_x,
                flagOffsetY = iconJson.offset_y,
                flagOffsetZ = iconJson.offset_z,
                triggerCode = iconJson.triggers_raw ?? ""
            };

            return decoded;
        }

        public static DecodedMapIconData[] DecodeMapIcons(MapIconJsonData[] iconsJson)
        {
            if (iconsJson == null || iconsJson.Length == 0)
            {
                return new DecodedMapIconData[0];
            }

            var decodedIcons = new DecodedMapIconData[iconsJson.Length];
            for (int i = 0; i < iconsJson.Length; i++)
            {
                decodedIcons[i] = DecodeMapIcon(iconsJson[i]);
            }

            return decodedIcons;
        }

        public static List<string> ValidateMapIcon(DecodedMapIconData iconData)
        {
            var warnings = new List<string>();

            if (iconData == null)
            {
                warnings.Add("Map icon data is null");
                return warnings;
            }

            if (string.IsNullOrEmpty(iconData.iconId))
            {
                warnings.Add("Icon ID is empty");
            }

            if (string.IsNullOrEmpty(iconData.meshName))
            {
                warnings.Add("Mesh name is empty");
            }

            if (iconData.scale <= 0)
            {
                warnings.Add($"Scale must be greater than 0: {iconData.scale}");
            }

            if (iconData.scale > 10.0f)
            {
                warnings.Add($"Scale is unusually large: {iconData.scale}");
            }

            return warnings;
        }

        private static bool ExtractNoShadowFlag(FlagsJsonData flagsJson)
        {
            if (flagsJson == null)
            {
                return false;
            }

            return flagsJson.raw == 1;
        }

        private static string ExtractSoundId(SoundJsonData soundJson)
        {
            if (soundJson == null)
            {
                return "";
            }

            return soundJson.name ?? "";
        }

        private static string FormatDisplayName(string iconId)
        {
            if (string.IsNullOrEmpty(iconId))
            {
                return "";
            }

            string name = iconId.Replace("_", " ");
            return System.Globalization.CultureInfo.CurrentCulture.TextInfo.ToTitleCase(name);
        }

        [Serializable]
        public class MapIconJsonData
        {
            public string id;
            public string mesh_name;
            public float scale;
            public SoundJsonData sound;
            public FlagsJsonData flags;
            public float offset_x;
            public float offset_y;
            public float offset_z;
            public string triggers_raw;
        }

        [Serializable]
        public class SoundJsonData
        {
            public string name;
            public int value;
        }

        [Serializable]
        public class FlagsJsonData
        {
            public int raw;
            public string[] decomposed;
            public string hex;
        }

        [Serializable]
        public class DecodedMapIconData
        {
            public string iconId;
            public string displayName;
            public string meshName;
            public float scale;
            public bool noShadow;
            public string soundId;
            public float flagOffsetX;
            public float flagOffsetY;
            public float flagOffsetZ;
            public string triggerCode;

            public override string ToString()
            {
                return $"MapIcon: {iconId} ({displayName}) - Mesh: {meshName}, Scale: {scale}";
            }
        }
    }
}