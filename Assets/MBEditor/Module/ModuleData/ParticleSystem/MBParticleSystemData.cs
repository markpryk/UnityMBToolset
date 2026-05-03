using UnityEngine;

namespace MountAndBlade.Data
{
    /// <summary>
    /// ScriptableObject representation of a Mount & Blade Warband Particle System.
    /// Corresponds to item tuples in module_particle_systems.py
    /// 
    /// Tuple fields:
    ///  1) id               - Particle system id (prefix psys_ auto-added)
    ///  2) flags            - Particle system flags (see header_particle_systems.py)
    ///  3) mesh_name        - Name of the particle mesh
    ///  4) num_particles    - Particles emitted per second
    ///  5) particle_life    - Lifetime in seconds
    ///  6) damping          - Speed lost to friction
    ///  7) gravity_strength - Gravity effect (negative = float up)
    ///  8) turbulence_size  - Random turbulence size in meters
    ///  9) turbulence_strength - Turbulence influence
    /// 10-19) keys          - Alpha/Red/Green/Blue/Scale key pairs (time, magnitude)
    /// 20) emit_box_size    - Emission box dimensions
    /// 21) emit_velocity    - Initial particle velocity
    /// 22) emit_dir_randomness - Direction randomness
    /// 23) rotation_speed   - Angular speed (degrees/sec)
    /// 24) rotation_damping - Rotation decay rate
    /// </summary>
    [CreateAssetMenu(fileName = "New MB Particle System", menuName = "Mount & Blade/Particle System Data", order = 2)]
    public class MBParticleSystemData : ScriptableObject
    {
        [Header("Identity")]
        public string ParticleSystemID;
        public string Flags;

        [Header("Mesh")]
        public string MeshName;

        [Header("Emission")]
        public int NumParticlesPerSecond;
        public float ParticleLife;
        public float Damping;
        public float GravityStrength;
        public float TurbulenceSize;
        public float TurbulenceStrength;

        [Header("Keys (Time, Magnitude)")]
        public Vector2 AlphaKey1;
        public Vector2 AlphaKey2;
        public Vector2 RedKey1;
        public Vector2 RedKey2;
        public Vector2 GreenKey1;
        public Vector2 GreenKey2;
        public Vector2 BlueKey1;
        public Vector2 BlueKey2;
        public Vector2 ScaleKey1;
        public Vector2 ScaleKey2;

        [Header("Emit Shape & Velocity")]
        public Vector3 EmitBoxSize;
        public Vector3 EmitVelocity;
        public float EmitDirRandomness;

        [Header("Rotation")]
        public float RotationSpeed;
        public float RotationDamping;
    }
}
