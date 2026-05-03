using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace MountAndBlade.Data
{
    public static class MBParticleSystemJsonDecoder
    {
        public static DecodedParticleSystemData DecodeParticleSystem(ParticleSystemJsonData psJson)
        {
            if (psJson == null)
            {
                throw new ArgumentNullException(nameof(psJson));
            }

            var decoded = new DecodedParticleSystemData
            {
                particleSystemId = psJson.id,
                flags = ExtractFlags(psJson.flags),
                billboardMode = ExtractBillboardMode(psJson.billboard_mode),
                meshName = psJson.mesh_name ?? "0",
                numParticlesPerSecond = psJson.num_particles_per_second,
                particleLife = psJson.particle_life,
                damping = psJson.damping,
                gravityStrength = psJson.gravity_strength,
                turbulenceSize = psJson.turbulence_size,
                turbulenceStrength = psJson.turbulence_strength,
                alphaKey1 = ExtractKey(psJson.keys?.alpha?.key_1),
                alphaKey2 = ExtractKey(psJson.keys?.alpha?.key_2),
                redKey1 = ExtractKey(psJson.keys?.red?.key_1),
                redKey2 = ExtractKey(psJson.keys?.red?.key_2),
                greenKey1 = ExtractKey(psJson.keys?.green?.key_1),
                greenKey2 = ExtractKey(psJson.keys?.green?.key_2),
                blueKey1 = ExtractKey(psJson.keys?.blue?.key_1),
                blueKey2 = ExtractKey(psJson.keys?.blue?.key_2),
                scaleKey1 = ExtractKey(psJson.keys?.scale?.key_1),
                scaleKey2 = ExtractKey(psJson.keys?.scale?.key_2),
                emitBoxSize = ExtractVector3(psJson.emit_box_size),
                emitVelocity = ExtractVector3(psJson.emit_velocity),
                emitDirRandomness = psJson.emit_dir_randomness,
                rotationSpeed = psJson.rotation_speed,
                rotationDamping = psJson.rotation_damping,
                category = DetermineCategory(psJson)
            };

            return decoded;
        }

        public static DecodedParticleSystemData[] DecodeParticleSystems(ParticleSystemJsonData[] psJsonArray)
        {
            if (psJsonArray == null || psJsonArray.Length == 0)
            {
                return new DecodedParticleSystemData[0];
            }

            var decoded = new DecodedParticleSystemData[psJsonArray.Length];
            for (int i = 0; i < psJsonArray.Length; i++)
            {
                decoded[i] = DecodeParticleSystem(psJsonArray[i]);
            }

            return decoded;
        }

        public static List<string> ValidateParticleSystem(DecodedParticleSystemData psData)
        {
            var warnings = new List<string>();

            if (psData == null)
            {
                warnings.Add("Particle system data is null");
                return warnings;
            }

            if (string.IsNullOrEmpty(psData.particleSystemId))
            {
                warnings.Add("Empty particle system ID");
            }

            if (string.IsNullOrEmpty(psData.meshName) || psData.meshName == "0")
            {
                warnings.Add("No mesh specified");
            }

            if (psData.numParticlesPerSecond <= 0)
            {
                warnings.Add($"Invalid particle count: {psData.numParticlesPerSecond}");
            }

            if (psData.particleLife <= 0)
            {
                warnings.Add($"Invalid particle life: {psData.particleLife}");
            }

            if (psData.damping < 0 || psData.damping > 1)
            {
                warnings.Add($"Damping outside typical range [0,1]: {psData.damping}");
            }

            // Validate keys are in [0,1] time range
            ValidateKey(warnings, "Alpha Key 1", psData.alphaKey1);
            ValidateKey(warnings, "Alpha Key 2", psData.alphaKey2);
            ValidateKey(warnings, "Red Key 1", psData.redKey1);
            ValidateKey(warnings, "Red Key 2", psData.redKey2);
            ValidateKey(warnings, "Green Key 1", psData.greenKey1);
            ValidateKey(warnings, "Green Key 2", psData.greenKey2);
            ValidateKey(warnings, "Blue Key 1", psData.blueKey1);
            ValidateKey(warnings, "Blue Key 2", psData.blueKey2);
            ValidateKey(warnings, "Scale Key 1", psData.scaleKey1);
            ValidateKey(warnings, "Scale Key 2", psData.scaleKey2);

            // Emit box with zero dimensions
            if (psData.emitBoxSize == Vector3.zero)
            {
                warnings.Add("Emit box size is zero - particles emit from a single point");
            }

            // Turbulence strength without size (or vice versa)
            if (psData.turbulenceStrength > 0 && psData.turbulenceSize <= 0)
            {
                warnings.Add("Turbulence strength set but turbulence size is zero");
            }

            if (string.IsNullOrEmpty(psData.billboardMode))
            {
                warnings.Add("No billboard mode set - particles may not render correctly");
            }

            return warnings;
        }

        public static string DetermineCategory(ParticleSystemJsonData psJson)
        {
            // Check billboard mode for rendering category
            if (psJson.billboard_mode != null)
            {
                string modeName = psJson.billboard_mode.name ?? "";
                if (modeName.Contains("turn_to_velocity"))
                    return "Directional";
            }

            // Check flags for behavior category
            if (psJson.flags?.decomposed != null)
            {
                var flagNames = psJson.flags.decomposed
                    .Select(f => f.name ?? "")
                    .ToList();

                if (flagNames.Any(f => f.Contains("emit_at_water_level")))
                    return "Water";
                if (flagNames.Any(f => f.Contains("next_effect_is_lod")))
                    return "LOD";
            }

            // Check ID for common particle types
            if (psJson.id != null)
            {
                string id = psJson.id.ToLowerInvariant();
                if (id.Contains("rain"))    return "Weather";
                if (id.Contains("snow"))    return "Weather";
                if (id.Contains("fire"))    return "Fire";
                if (id.Contains("smoke"))   return "Smoke";
                if (id.Contains("blood"))   return "Combat";
                if (id.Contains("dust"))    return "Dust";
                if (id.Contains("spark"))   return "Sparks";
                if (id.Contains("torch"))   return "Fire";
                if (id.Contains("water"))   return "Water";
                if (id.Contains("fog"))     return "Atmosphere";
                if (id.Contains("arrow"))   return "Combat";
                if (id.Contains("trail"))   return "Trail";
            }

            // Negative gravity = floating particles (smoke-like)
            if (psJson.gravity_strength < 0)
                return "Rising";

            return "Other";
        }

        // PRIVATE EXTRACTION HELPERS

        private static string ExtractFlags(ParticleSystemFlagsJsonData flagsJson)
        {
            if (flagsJson == null)
            {
                return "0";
            }

            return flagsJson.raw_flags_value.ToString();
        }

        private static string ExtractBillboardMode(BillboardModeJsonData modeJson)
        {
            if (modeJson == null)
            {
                return "";
            }

            return modeJson.name ?? "";
        }

        private static Vector2 ExtractKey(KeyJsonData keyJson)
        {
            if (keyJson == null)
            {
                return new Vector2(0f, 0f);
            }

            return new Vector2(keyJson.time, keyJson.magnitude);
        }

        private static Vector3 ExtractVector3(Vector3JsonData vecJson)
        {
            if (vecJson == null)
            {
                return Vector3.zero;
            }

            return new Vector3(vecJson.x, vecJson.y, vecJson.z);
        }

        private static void ValidateKey(List<string> warnings, string keyName, Vector2 key)
        {
            if (key.x < 0 || key.x > 1)
            {
                warnings.Add($"{keyName} time outside [0,1]: {key.x}");
            }
        }

        // JSON DATA STRUCTURES

        [Serializable]
        public class ParticleSystemJsonData
        {
            public string id;
            public ParticleSystemFlagsJsonData flags;
            public BillboardModeJsonData billboard_mode;
            public string mesh_name;
            public int num_particles_per_second;
            public float particle_life;
            public float damping;
            public float gravity_strength;
            public float turbulence_size;
            public float turbulence_strength;
            public ParticleKeysJsonData keys;
            public Vector3JsonData emit_box_size;
            public Vector3JsonData emit_velocity;
            public float emit_dir_randomness;
            public float rotation_speed;
            public float rotation_damping;
        }

        [Serializable]
        public class ParticleSystemFlagsJsonData
        {
            public int raw_flags_value;
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
        public class BillboardModeJsonData
        {
            public int value;
            public string hex;
            public string name;
        }

        [Serializable]
        public class ParticleKeysJsonData
        {
            public KeyPairJsonData alpha;
            public KeyPairJsonData red;
            public KeyPairJsonData green;
            public KeyPairJsonData blue;
            public KeyPairJsonData scale;
        }

        [Serializable]
        public class KeyPairJsonData
        {
            public KeyJsonData key_1;
            public KeyJsonData key_2;
        }

        [Serializable]
        public class KeyJsonData
        {
            public float time;
            public float magnitude;
        }

        [Serializable]
        public class Vector3JsonData
        {
            public float x;
            public float y;
            public float z;
        }

        // DECODED OUTPUT

        [Serializable]
        public class DecodedParticleSystemData
        {
            public string particleSystemId;
            public string flags;
            public string billboardMode;
            public string meshName;
            public int numParticlesPerSecond;
            public float particleLife;
            public float damping;
            public float gravityStrength;
            public float turbulenceSize;
            public float turbulenceStrength;

            public Vector2 alphaKey1;
            public Vector2 alphaKey2;
            public Vector2 redKey1;
            public Vector2 redKey2;
            public Vector2 greenKey1;
            public Vector2 greenKey2;
            public Vector2 blueKey1;
            public Vector2 blueKey2;
            public Vector2 scaleKey1;
            public Vector2 scaleKey2;

            public Vector3 emitBoxSize;
            public Vector3 emitVelocity;
            public float emitDirRandomness;

            public float rotationSpeed;
            public float rotationDamping;

            public string category;

            public override string ToString()
            {
                return $"ParticleSystem: {particleSystemId} - Category: {category}, Billboard: {billboardMode}, Particles/s: {numParticlesPerSecond}";
            }
        }
    }
}
