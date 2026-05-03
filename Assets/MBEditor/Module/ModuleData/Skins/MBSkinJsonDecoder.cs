using System;
using System.Collections.Generic;
using UnityEngine;

namespace MountAndBlade.Data
{
    public static class MBSkinJsonDecoder
    {
        public static DecodedSkinData DecodeSkin(SkinJsonData skinJson)
        {
            if (skinJson == null)
            {
                throw new ArgumentNullException(nameof(skinJson));
            }

            var decoded = new DecodedSkinData
            {
                skinId = skinJson.id,
                skinFlags = ParseSkinFlags(skinJson.flags_raw),
                skeletonName = skinJson.skeleton,
                bodyMesh = skinJson.body_mesh,
                calfMeshLeft = skinJson.calf_mesh,
                handMeshLeft = skinJson.hand_mesh,
                headMesh = skinJson.head_mesh,
                faceKeys = ExtractFaceKeys(skinJson.face_keys),
                hairMeshes = skinJson.hair_meshes ?? new string[0],
                beardMeshes = skinJson.beard_meshes ?? new string[0],
                hairTextures = skinJson.hair_textures ?? new string[0],
                beardTextures = skinJson.beard_textures ?? new string[0],
                faceTextures = ExtractFaceTextures(skinJson.face_textures),
                voiceSounds = ExtractVoiceSounds(skinJson.voices),
                bloodParticles1 = skinJson.blood_particles_1,
                bloodParticles2 = skinJson.blood_particles_2
            };

            return decoded;
        }

        public static DecodedSkinData[] DecodeSkins(SkinJsonData[] skinsJson)
        {
            if (skinsJson == null || skinsJson.Length == 0)
            {
                return new DecodedSkinData[0];
            }

            var decodedSkins = new DecodedSkinData[skinsJson.Length];
            for (int i = 0; i < skinsJson.Length; i++)
            {
                decodedSkins[i] = DecodeSkin(skinsJson[i]);
            }

            return decodedSkins;
        }

        public static List<string> ValidateSkin(DecodedSkinData skinData)
        {
            var warnings = new List<string>();

            if (skinData == null)
            {
                warnings.Add("Skin data is null");
                return warnings;
            }

            if (string.IsNullOrEmpty(skinData.skinId))
            {
                warnings.Add("Skin ID is empty");
            }

            if (string.IsNullOrEmpty(skinData.skeletonName))
            {
                warnings.Add("Skeleton name is empty");
            }

            if (string.IsNullOrEmpty(skinData.bodyMesh))
            {
                warnings.Add("Body mesh is empty");
            }

            if (skinData.faceKeys == null || skinData.faceKeys.Count == 0)
            {
                warnings.Add("No face keys defined - face generation won't work");
            }

            if (skinData.faceTextures == null || skinData.faceTextures.Count == 0)
            {
                warnings.Add("No face textures defined");
            }

            return warnings;
        }

        private static int ParseSkinFlags(string flagsRaw)
        {
            if (string.IsNullOrEmpty(flagsRaw))
                return 0;

            flagsRaw = flagsRaw.Replace("skf_", "").Trim();

            var flagMap = new Dictionary<string, int>
            {
                {"use_morph_key_10", 1},
                {"use_morph_key_20", 2},
                {"use_morph_key_30", 3},
                {"use_morph_key_40", 4},
                {"use_morph_key_50", 5},
                {"use_morph_key_60", 6},
                {"use_morph_key_70", 7}
            };

            if (flagMap.ContainsKey(flagsRaw.ToLower()))
            {
                return flagMap[flagsRaw.ToLower()];
            }

            if (int.TryParse(flagsRaw, out int result))
            {
                return result;
            }

            return 0;
        }

        private static List<FaceKeyData> ExtractFaceKeys(FaceKeyJsonData[] faceKeysJson)
        {
            var faceKeys = new List<FaceKeyData>();

            if (faceKeysJson == null || faceKeysJson.Length == 0)
            {
                return faceKeys;
            }

            foreach (var keyJson in faceKeysJson)
            {
                if (keyJson == null)
                    continue;

                faceKeys.Add(new FaceKeyData
                {
                    keyId = keyJson.morph_key,
                    reserved = 0,
                    minValue = keyJson.min_value,
                    maxValue = keyJson.max_value,
                    displayName = keyJson.name ?? ""
                });
            }

            return faceKeys;
        }

        private static List<FaceTextureData> ExtractFaceTextures(FaceTextureJsonData[] faceTexJson)
        {
            var faceTextures = new List<FaceTextureData>();

            if (faceTexJson == null || faceTexJson.Length == 0)
            {
                return faceTextures;
            }

            foreach (var texJson in faceTexJson)
            {
                if (texJson == null)
                    continue;

                faceTextures.Add(new FaceTextureData
                {
                    textureName = texJson.texture_name,
                    baseSkinColor = texJson.base_color,
                    hairTextureOptions = texJson.hair_colors ?? new string[0],
                    skinColorVariations = texJson.skin_colors ?? new string[0]
                });
            }

            return faceTextures;
        }

        private static List<VoiceEntryData> ExtractVoiceSounds(VoiceJsonData[] voicesJson)
        {
            var voices = new List<VoiceEntryData>();

            if (voicesJson == null || voicesJson.Length == 0)
            {
                return voices;
            }

            foreach (var voiceJson in voicesJson)
            {
                if (voiceJson == null)
                    continue;

                voices.Add(new VoiceEntryData
                {
                    eventType = (VoiceEventType)voiceJson.type_id,
                    soundId = voiceJson.sound_id
                });
            }

            return voices;
        }

        [Serializable]
        public class SkinJsonData
        {
            public int index;
            public string id;
            public string skeleton;
            public FaceKeyJsonData[] face_keys;
            public VoiceJsonData[] voices;
            public FaceTextureJsonData[] face_textures;
            public string[] hair_meshes;
            public string[] hair_textures;
            public string[] beard_meshes;
            public string[] beard_textures;
            public string head_mesh;
            public string body_mesh;
            public string calf_mesh;
            public string hand_mesh;
            public string flags_raw;
            public string blood_particles_1;
            public string blood_particles_2;
        }

        [Serializable]
        public class FaceKeyJsonData
        {
            public float max_value;
            public float min_value;
            public float unknown_1;
            public string name;
            public int morph_key;
        }

        [Serializable]
        public class FaceTextureJsonData
        {
            public string texture_name;
            public string base_color;
            public string[] hair_colors;
            public string[] skin_colors;
        }

        [Serializable]
        public class VoiceJsonData
        {
            public string sound_id;
            public string type;
            public int type_id;
        }

        [Serializable]
        public class DecodedSkinData
        {
            public string skinId;
            public int skinFlags;
            public string skeletonName;
            public string bodyMesh;
            public string calfMeshLeft;
            public string handMeshLeft;
            public string headMesh;
            public List<FaceKeyData> faceKeys;
            public string[] hairMeshes;
            public string[] beardMeshes;
            public string[] hairTextures;
            public string[] beardTextures;
            public List<FaceTextureData> faceTextures;
            public List<VoiceEntryData> voiceSounds;
            public string bloodParticles1;
            public string bloodParticles2;

            public override string ToString()
            {
                return $"Skin: {skinId} - Skeleton: {skeletonName}, FaceKeys: {faceKeys?.Count ?? 0}, Textures: {faceTextures?.Count ?? 0}";
            }
        }
    }

    [Serializable]
    public class FaceKeyData
    {
        public int keyId;
        public int reserved;
        public float minValue;
        public float maxValue;
        public string displayName;

        public override string ToString()
        {
            return $"{displayName} (Key {keyId}): [{minValue}, {maxValue}]";
        }
    }

    [Serializable]
    public class FaceTextureData
    {
        public string textureName;
        public string baseSkinColor;
        public string[] hairTextureOptions;
        public string[] skinColorVariations;

        public override string ToString()
        {
            return $"{textureName} - Base: {baseSkinColor}, Variations: {skinColorVariations?.Length ?? 0}";
        }
    }

    [Serializable]
    public class VoiceEntryData
    {
        public VoiceEventType eventType;
        public string soundId;

        public override string ToString()
        {
            return $"{eventType}: {soundId}";
        }
    }
}