using UnityEngine;
using System.Collections.Generic;
using MountAndBlade.Data;

namespace WarbandParticles
{
    // FLAGS - mirrors rglParticleSystemFlags from rglParticleSystem.h

    [System.Flags]
    public enum ParticleSystemFlags : uint
    {
        None              = 0x000,
        Forced            = 0x001,
        AlwaysEmit        = 0x002,
        GlobalEmitDir     = 0x010,
        EmitAtWaterLevel  = 0x020,
        Billboard2D       = 0x100,
        Billboard3D       = 0x200,
        BillboardDrop     = 0x300,
        TurnToVelocity    = 0x400,
        BillboardMask     = 0xF00,
        RandomizeRotation = 0x1000,
        RandomizeSize     = 0x2000,
        TurbulenceIs2D    = 0x10000,   // Named to match old code
        Turbulence2D      = 0x10000,   // Alias
        NextEffectIsLod   = 0x20000,
    }

    // PARTICLE KEY - same type the old system and renderer used

    [System.Serializable]
    public class ParticleKey
    {
        public float Time;
        public float Magnitude;
    }

    // PARTICLE - same as old system

    public class Particle
    {
        public Vector3 Position;
        public Quaternion Rotation = Quaternion.identity;
        public Vector3 Velocity;
        public float Time;
        public float RotationZ;
        public float AngularSpeed;
        public float Size = 1f;
    }

    // WARBAND PARTICLE SYSTEM
    //
    // Plain class (not MonoBehaviour) that can be driven by:
    //   - WarbandParticleEmitter (new MonoBehaviour wrapper)
    //   - Direct code (tests, tools)
    //
    // Keeps the SAME public API as the old MonoBehaviour version so
    // WarbandParticleRenderer and WarbandParticleFallbackRenderer
    // continue to work without changes.
    //
    // Simulation ported from rglParticleSystem.cpp
    // Data loaded from MBParticleSystemData ScriptableObject

    public class WarbandParticleSystem
    {
        // The renderers access these directly.

        public string Id;
        public ParticleSystemFlags Flags;
        public Mesh ParticleMesh;
        public Material Material;
        public float NumParticles = 50f;
        public float Life = 1f;
        public float Damping = 0.5f;
        public float GravityStrength = 1f;
        public float TurbulenceSize = 1f;
        public float TurbulenceStrength = 0f;

        public ParticleKey[] AlphaKeys = new ParticleKey[2]
        {
            new ParticleKey { Time = 0f, Magnitude = 1f },
            new ParticleKey { Time = 1f, Magnitude = 0f },
        };
        public ParticleKey[] RedKeys = new ParticleKey[2]
        {
            new ParticleKey { Time = 0f, Magnitude = 1f },
            new ParticleKey { Time = 1f, Magnitude = 1f },
        };
        public ParticleKey[] GreenKeys = new ParticleKey[2]
        {
            new ParticleKey { Time = 0f, Magnitude = 1f },
            new ParticleKey { Time = 1f, Magnitude = 1f },
        };
        public ParticleKey[] BlueKeys = new ParticleKey[2]
        {
            new ParticleKey { Time = 0f, Magnitude = 1f },
            new ParticleKey { Time = 1f, Magnitude = 1f },
        };
        public ParticleKey[] ScaleKeys = new ParticleKey[2]
        {
            new ParticleKey { Time = 0f, Magnitude = 1f },
            new ParticleKey { Time = 1f, Magnitude = 1f },
        };

        public Vector3 EmitBoxSize = new Vector3(0.1f, 0.1f, 0.1f);
        public Vector3 EmitVelocity = new Vector3(0, 0, 1f);
        public float EmitRandomness = 0.5f;
        public float AngularSpeed = 0f;
        public float AngularDamping = 0.5f;
        public bool useWarbandCoordinates = true;


        private List<Particle> _particles = new List<Particle>();
        private float _accumulatedTime = 0f;
        private float _numBurstParticles = 0f;
        private float _burstStrength = 0f;
        private int _numBurstsLeft = 0;
        private float _waterLevel = 0f;
        private float _radius = 0f;

        // Transform provided by the emitter each frame
        private Transform _emitterTransform;


        public List<Particle> Particles => _particles;
        public bool IsAlive => _particles.Count > 0 || _numBurstsLeft > 0;
        public float Radius => _radius;
        public int ActiveParticleCount => _particles.Count;
        public int EstimatedMaxParticles => Mathf.CeilToInt(NumParticles * Life) + 1;

        // INITIALIZATION FROM DATA

        /// <summary>
        /// Load all parameters from an MBParticleSystemData ScriptableObject.
        /// </summary>
        public void InitializeFromData(MBParticleSystemData data)
        {
            if (data == null)
            {
                Debug.LogError("WarbandParticleSystem: MBParticleSystemData is null");
                return;
            }

            Id = data.ParticleSystemID;

            // Parse flags
            if (int.TryParse(data.Flags, out int flagsInt))
                Flags = (ParticleSystemFlags)flagsInt;
            else if (uint.TryParse(data.Flags, out uint flagsUint))
                Flags = (ParticleSystemFlags)flagsUint;
            else
                Flags = ParticleSystemFlags.None;

            // Emission
            NumParticles = data.NumParticlesPerSecond;
            Life = data.ParticleLife;
            Damping = data.Damping;
            GravityStrength = data.GravityStrength;
            TurbulenceSize = data.TurbulenceSize;
            TurbulenceStrength = data.TurbulenceStrength;

            // Keys - load into ParticleKey[] arrays (same type renderers expect)
            AlphaKeys[0].Time = data.AlphaKey1.x; AlphaKeys[0].Magnitude = data.AlphaKey1.y;
            AlphaKeys[1].Time = data.AlphaKey2.x; AlphaKeys[1].Magnitude = data.AlphaKey2.y;
            RedKeys[0].Time = data.RedKey1.x;     RedKeys[0].Magnitude = data.RedKey1.y;
            RedKeys[1].Time = data.RedKey2.x;     RedKeys[1].Magnitude = data.RedKey2.y;
            GreenKeys[0].Time = data.GreenKey1.x; GreenKeys[0].Magnitude = data.GreenKey1.y;
            GreenKeys[1].Time = data.GreenKey2.x; GreenKeys[1].Magnitude = data.GreenKey2.y;
            BlueKeys[0].Time = data.BlueKey1.x;   BlueKeys[0].Magnitude = data.BlueKey1.y;
            BlueKeys[1].Time = data.BlueKey2.x;   BlueKeys[1].Magnitude = data.BlueKey2.y;
            ScaleKeys[0].Time = data.ScaleKey1.x;  ScaleKeys[0].Magnitude = data.ScaleKey1.y;
            ScaleKeys[1].Time = data.ScaleKey2.x;  ScaleKeys[1].Magnitude = data.ScaleKey2.y;

            // Emit shape - stored as-is, coordinate conversion happens at emit time
            EmitBoxSize = data.EmitBoxSize;
            EmitVelocity = data.EmitVelocity;
            EmitRandomness = data.EmitDirRandomness;

            // Rotation - stored in degrees, converted to radians at use time (like old code)
            AngularSpeed = data.RotationSpeed;
            AngularDamping = data.RotationDamping;
        }

        // EMITTER BINDING

        /// <summary>
        /// Bind to an emitter transform. Position/rotation used for emit box.
        /// </summary>
        public void SetEmitterTransform(Transform t)
        {
            _emitterTransform = t;
        }

        public void SetWaterLevel(float level)
        {
            _waterLevel = level;
        }

        public void Emit(int strength)
        {
            _numBurstsLeft = strength;
            _burstStrength = strength;
            _numBurstParticles = 0f;
        }

        public void ResetState()
        {
            _particles.Clear();
            _numBurstsLeft = 0;
            _numBurstParticles = 0f;
            _burstStrength = 0f;
            _accumulatedTime = 0f;
            _radius = 0f;
        }

        // SIMULATION
        // Same logic as old MonoBehaviour Update + EmitParticles,
        // but callable externally by the emitter.

        /// <summary>
        /// Advance simulation by deltaTime. Call once per frame.
        /// </summary>
        public void FrameMove(float deltaTime)
        {
            _accumulatedTime += deltaTime;
            UpdateParticles(deltaTime);
            EmitParticles(deltaTime);
        }

        private void UpdateParticles(float deltaTime)
        {
            // Gravity vector - same as old working code
            Vector3 gravityVector = useWarbandCoordinates
                ? new Vector3(0, 0, -9.8f) * GravityStrength
                : Physics.gravity * GravityStrength;

            if (useWarbandCoordinates)
                gravityVector = CoordinateConverter.WarbandToUnityDirection(gravityVector);

            Vector3 gravityFactor = gravityVector * deltaTime;
            float dampingFactor = 1f - Mathf.Min(Damping * deltaTime, 1f);
            float angularDampingFactor = 1f - Mathf.Min(AngularDamping * deltaTime, 1f);
            float timeFactor = (Life > 0f) ? deltaTime / Life : 0f;

            float maxArea = 0f;
            Vector3 emitterPos = _emitterTransform != null ? _emitterTransform.position : Vector3.zero;

            for (int i = _particles.Count - 1; i >= 0; i--)
            {
                Particle p = _particles[i];

                // Gravity
                p.Velocity += gravityFactor;

                // Turbulence
                if (TurbulenceStrength > 0.01f)
                {
                    Vector3 perlinInput = p.Position;
                    perlinInput.z += _accumulatedTime * TurbulenceSize * 0.2f;

                    Vector3 turbulence = new Vector3(
                        Mathf.PerlinNoise(perlinInput.x * TurbulenceSize, perlinInput.y * TurbulenceSize) - 0.5f,
                        Mathf.PerlinNoise(perlinInput.y * TurbulenceSize, perlinInput.z * TurbulenceSize) - 0.5f,
                        Mathf.PerlinNoise(perlinInput.z * TurbulenceSize, perlinInput.x * TurbulenceSize) - 0.5f
                    );

                    if ((Flags & ParticleSystemFlags.TurbulenceIs2D) != 0)
                    {
                        if (useWarbandCoordinates)
                        {
                            turbulence = CoordinateConverter.WarbandToUnityDirection(
                                new Vector3(turbulence.x, turbulence.y, 0));
                        }
                        else
                        {
                            turbulence.y = 0;
                        }
                    }

                    p.Velocity += turbulence * TurbulenceStrength * deltaTime;
                }

                // Damping
                p.Velocity *= dampingFactor;

                // Integrate
                p.Position += p.Velocity * deltaTime;

                // Angular
                p.AngularSpeed *= angularDampingFactor;
                p.RotationZ += p.AngularSpeed * deltaTime;

                // Lifetime
                p.Time += timeFactor;

                // Remove dead
                if (p.Time >= 1f)
                {
                    // Swap with last for perf (like engine does)
                    int last = _particles.Count - 1;
                    if (i < last)
                        _particles[i] = _particles[last];
                    _particles.RemoveAt(last);
                }
                else
                {
                    float distSq = (p.Position - emitterPos).sqrMagnitude;
                    if (distSq > maxArea) maxArea = distSq;
                }
            }

            _radius = Mathf.Sqrt(maxArea);
        }

        private void EmitParticles(float deltaTime)
        {
            if ((Flags & ParticleSystemFlags.AlwaysEmit) == 0 && _numBurstsLeft <= 0)
                return;

            // Burst factor - same as old working code
            float burstFactor = 1f;
            if (_numBurstsLeft > 0)
            {
                burstFactor = Mathf.Clamp(
                    (_numBurstsLeft + 10f) / (_burstStrength + 10f), 0f, 1f);
            }

            // Distance degradation
            float distanceFactor = 1f;
            Camera cam = null;

#if UNITY_EDITOR
            if (!Application.isPlaying && UnityEditor.SceneView.lastActiveSceneView != null)
                cam = UnityEditor.SceneView.lastActiveSceneView.camera;
#endif
            if (cam == null) cam = Camera.main;

            if (cam != null && _emitterTransform != null)
            {
                float degradeDistance = 50f;
                float distSq = (_emitterTransform.position - cam.transform.position).sqrMagnitude;
                distanceFactor = Mathf.Clamp(degradeDistance * degradeDistance / Mathf.Max(distSq, 0.001f), 0f, 1f);
            }

            // Accumulate
            _numBurstParticles += NumParticles * burstFactor * deltaTime * distanceFactor;

            // Emit using Warband's two-random approach
            while (_numBurstParticles > 0)
            {
                float randomThreshold = Random.value + Random.value;

                if (randomThreshold < _numBurstParticles &&
                    (_numBurstsLeft > 0 || (Flags & ParticleSystemFlags.AlwaysEmit) != 0))
                {
                    if (_numBurstsLeft > 0)
                        _numBurstsLeft--;

                    _numBurstParticles -= randomThreshold;

                    Particle newParticle = new Particle();
                    InitializeParticle(newParticle);
                    _particles.Add(newParticle);
                }
                else
                {
                    break;
                }
            }
        }

        // PARTICLE INIT - same as old working code
        // Uses _emitterTransform for position/direction

        private void InitializeParticle(Particle particle)
        {
            Vector3 pos = _emitterTransform != null ? _emitterTransform.position : Vector3.zero;

            // Random offset in emit box - same coord handling as old code
            Vector3 randomOffset = new Vector3(
                Random.Range(-0.5f, 0.5f) * EmitBoxSize.x,
                Random.Range(-0.5f, 0.5f) * (useWarbandCoordinates ? EmitBoxSize.z : EmitBoxSize.y),
                Random.Range(-0.5f, 0.5f) * (useWarbandCoordinates ? EmitBoxSize.y : EmitBoxSize.z)
            );

            if (useWarbandCoordinates)
                randomOffset = CoordinateConverter.WarbandToUnity(randomOffset);

            if (_emitterTransform != null)
                particle.Position = pos + _emitterTransform.TransformDirection(randomOffset);
            else
                particle.Position = pos + randomOffset;

            // Emit at water level
            if ((Flags & ParticleSystemFlags.EmitAtWaterLevel) != 0)
                particle.Position.y = _waterLevel;

            // Velocity - same as old code
            Vector3 randomDir = new Vector3(
                Random.Range(-1f, 1f),
                Random.Range(-1f, 1f),
                Random.Range(-1f, 1f)
            ).normalized * EmitRandomness;

            Vector3 emitDir = EmitVelocity.normalized;

            if (useWarbandCoordinates)
                emitDir = CoordinateConverter.WarbandToUnityDirection(emitDir);

            Vector3 velocity = emitDir + randomDir;

            if ((Flags & ParticleSystemFlags.GlobalEmitDir) != 0)
            {
                particle.Velocity = velocity * EmitVelocity.magnitude;
            }
            else
            {
                if (_emitterTransform != null)
                    particle.Velocity = _emitterTransform.TransformDirection(velocity * EmitVelocity.magnitude);
                else
                    particle.Velocity = velocity * EmitVelocity.magnitude;
            }

            // Triangular distribution [0,2] - same as engine
            particle.Velocity *= Random.value + Random.value;

            // Init
            particle.Time = 0f;
            particle.RotationZ = 0f;
            particle.Size = 1f;

            if ((Flags & ParticleSystemFlags.RandomizeRotation) != 0)
                particle.RotationZ = Random.Range(0f, Mathf.PI * 2f);

            if ((Flags & ParticleSystemFlags.RandomizeSize) != 0)
            {
                if (Random.value <= 0.5f)
                    particle.Size *= 1f + Random.Range(0f, 0.6f);
                else
                    particle.Size *= 1f - Random.Range(0f, 0.4f);
            }

            particle.AngularSpeed = (AngularSpeed * Mathf.Deg2Rad) * Random.Range(-1f, 1f);
        }

        // INTERPOLATION - SAME SIGNATURE AS OLD CODE
        // The renderer calls: InterpolateKey(system.AlphaKeys, time)
        // Uses the same interpolation for ALL channels (matching old behavior)
        //
        // From rglParticleSystem.cpp renderParticle():
        //   Alpha: before key0 → fade in from 0; after key1 → fade out to 0
        //   RGB:   before key0 → constant; after key1 → constant
        //
        // The old code used the alpha-style for everything and it worked,
        // so we keep that. The difference only matters for RGB channels
        // where key0.time > 0 (rare in practice).

        /// <summary>
        /// Interpolate alpha keys. Matches RGL alpha interpolation exactly:
        /// Before key0: ramp from 0 to key0.magnitude
        /// Between:     linear interpolation
        /// After key1:  ramp from key1.magnitude to 0
        /// </summary>
        public float InterpolateKey(ParticleKey[] keys, float time)
        {
            if (keys[0].Time > time)
            {
                if (keys[0].Time < 0.0001f) return keys[0].Magnitude;
                return time / keys[0].Time * keys[0].Magnitude;
            }
            else if (keys[1].Time < time)
            {
                float denom = 1f - keys[1].Time;
                if (denom < 0.0001f) return keys[1].Magnitude;
                return (1f - time) / denom * keys[1].Magnitude;
            }
            else
            {
                float denom = keys[1].Time - keys[0].Time;
                if (denom < 0.0001f) return keys[0].Magnitude;
                float t = (time - keys[0].Time) / denom;
                return t * (keys[1].Magnitude - keys[0].Magnitude) + keys[0].Magnitude;
            }
        }

        /// <summary>
        /// Interpolate RGB keys. Matches RGL RGB interpolation exactly:
        /// Before key0: constant key0.magnitude (no fade)
        /// Between:     linear interpolation
        /// After key1:  constant key1.magnitude (no fade)
        /// This differs from alpha which fades to 0 outside the key range.
        /// </summary>
        public float InterpolateRGBKey(ParticleKey[] keys, float time)
        {
            if (keys[0].Time > time)
            {
                return keys[0].Magnitude;
            }
            else if (keys[1].Time < time)
            {
                return keys[1].Magnitude;
            }
            else
            {
                float denom = keys[1].Time - keys[0].Time;
                if (denom < 0.0001f) return keys[0].Magnitude;
                float t = (time - keys[0].Time) / denom;
                return t * (keys[1].Magnitude - keys[0].Magnitude) + keys[0].Magnitude;
            }
        }

        public Color GetColorAtTime(float time)
        {
            return new Color(
                InterpolateRGBKey(RedKeys, time),
                InterpolateRGBKey(GreenKeys, time),
                InterpolateRGBKey(BlueKeys, time),
                InterpolateKey(AlphaKeys, time)
            );
        }

        public float GetScaleAtTime(float time)
        {
            return InterpolateRGBKey(ScaleKeys, time);
        }

        /// <summary>
        /// Clears all particles and stops emission.
        /// </summary>
        public void ClearParticles()
        {
            _particles.Clear();
            _numBurstsLeft = 0;
            _numBurstParticles = 0f;
        }
    }
}
