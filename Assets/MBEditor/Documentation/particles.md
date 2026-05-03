# Particle Systems

The particle system in M&B is a CPU-driven billboard/mesh emitter. Each particle system definition is a template — the engine instantiates runtime `rglParticleSystem` objects from `mbParticleSystem` templates loaded from `particle_systems.txt`. Particles are emitted in bursts, simulated with gravity/damping/turbulence per frame, and rendered by stamping a source mesh per particle into a single batched mesh.

---

## Module System Setup

Particle systems are defined in `module_particle_systems.py` and exported to `particle_systems.txt`. The prefix `psys_` is automatically added to each ID.

### Entry Format

```python
(id, flags, mesh_name,
 num_particles, life, damping, gravity_strength, turbulence_size, turbulence_strength,
 alpha_key_0, alpha_key_1,
 red_key_0, red_key_1,
 green_key_0, green_key_1,
 blue_key_0, blue_key_1,
 scale_key_0, scale_key_1,
 emit_box_size,      # (x, y, z)
 emit_velocity,      # (x, y, z)
 emit_dir_randomness,
 rotation_speed,     # degrees/sec (converted to radians on load)
 rotation_damping,
)
```

### Parameter Reference

| Field | Type | Description |
|-------|------|-------------|
| `id` | string | Identifier, becomes `psys_{id}` |
| `flags` | int | Combination of `psf_` flags |
| `mesh_name` | string | BRF mesh used as the particle quad/shape |
| `num_particles` | float | Emission rate (particles per second) |
| `life` | float | Particle lifetime in seconds |
| `damping` | float | Velocity drag per second. `velocity *= (1 - damping × dt)` |
| `gravity_strength` | float | Multiplier on gravity `(0,0,-9.8)`. Negative = float up |
| `turbulence_size` | float | Perlin noise spatial scale (meters) |
| `turbulence_strength` | float | Perlin noise velocity influence |
| `alpha_key_0/1` | (time, mag) | Alpha envelope. Alpha ramps from 0→key0 at start, interpolates between keys, ramps key1→0 at end |
| `red/green/blue_key_0/1` | (time, mag) | Color channel envelope. Clamped outside key range, linearly interpolated between |
| `scale_key_0/1` | (time, mag) | Size envelope. Same interpolation as color |
| `emit_box_size` | (x,y,z) | Half-extents of the emission volume |
| `emit_velocity` | (x,y,z) | Base velocity direction. Transformed to local space unless `psf_global_emit_dir` |
| `emit_dir_randomness` | float | Random spread added to emit velocity |
| `rotation_speed` | float | Angular speed in degrees/sec (converted to radians on load) |
| `rotation_damping` | float | Angular drag. `angularSpeed *= (1 - angularDamping × dt)` |

---

## Flags

| Flag | Value | Effect |
|------|-------|--------|
| `psf_always_emit` | `0x002` | Continuous emission (burst count set to 1 billion) |
| `psf_global_emit_dir` | `0x010` | Emit velocity is world-space, not transformed by emitter orientation |
| `psf_emit_at_water_level` | `0x020` | Particle spawn Z is forced to water level |
| `psf_billboard_2d` | `0x100` | Up = rotation vector, front rotated toward camera |
| `psf_billboard_3d` | `0x200` | Front vector points to camera |
| `psf_billboard_drop` | `0x300` | Front to camera, up aligned to velocity (rain/drops) |
| `psf_turn_to_velocity` | `0x400` | Front aligned to velocity direction |
| `psf_randomize_rotation` | `0x1000` | Random initial rotation (full 360° for billboards, XYZ Euler for meshes) |
| `psf_randomize_size` | `0x2000` | Random size variation: 50% chance of ×(1.0–1.6), 50% chance of ×(0.6–1.0) |
| `psf_2d_turbulance` | `0x10000` | Turbulence zeroes the Z component (XY-only noise) |
| `psf_next_effect_is_lod` | `0x20000` | Next particle system in the list is a LOD fallback |

Billboard modes occupy bits 8–11 (`psf_billboard_mask = 0xF00`). Only one can be set. If none is set, particles keep their emitter orientation with Z rotation.

### Internal-Only Flags

| Flag | Value | Notes |
|------|-------|-------|
| `psf_forced` | `0x001` | Engine-internal. Forces rendering even when particles are globally disabled |
| `psf_use_ambient_color` | `0x040` | Defined in header but not used in any code path |

---

## Keyframe System

Each particle has 5 animated channels: **alpha**, **red**, **green**, **blue**, **scale**. Each channel has exactly 2 keys defining a `(time, magnitude)` pair where `time` is normalized particle lifetime (0.0 = birth, 1.0 = death).

### Color Channels (R/G/B)

Between the two keys, magnitude is linearly interpolated. Before key 0's time, the value is clamped to key 0's magnitude. After key 1's time, clamped to key 1's magnitude. The final channel value is multiplied by the system's base `m_color` (default white).

### Alpha Channel

Alpha has special ramp behavior. Before key 0: `alpha = (time / key0.time) × key0.magnitude` — ramps from 0 at birth. After key 1: `alpha = ((1 - time) / (1 - key1.time)) × key1.magnitude` — ramps to 0 at death. Between keys: linearly interpolated.

This means alpha always starts and ends at 0 regardless of key magnitudes, creating automatic fade-in and fade-out.

### Scale Channel

Same interpolation as color (clamp outside, lerp between). Final particle size = `particle.m_size × scale_interpolated`. The `m_size` is 1.0 by default, or randomized if `psf_randomize_size` is set.

---

## Simulation

### Emission

Particles are emitted in bursts via `emit(strength)`. Each frame, the system accumulates `numParticles × burstFactor × dt` and spawns particles when the accumulator exceeds a random threshold (`rand() + rand()`). The `burstFactor` is `clamp((burstsLeft + 10) / (burstStrength + 10), 0, 1)`, creating a natural decay curve.

A distance-based LOD scales emission rate: `clamp(degradeDistance² / distanceToCamera², 0, 1)`. Far particles emit less.

With `psf_always_emit`, burst strength and count are both set to 1 billion — effectively infinite continuous emission.

### Per-Frame Update

Each frame (at `fParticleSystemUpdateInterval` intervals):

1. **Gravity**: `velocity += (0, 0, -9.8) × gravityStrength × dt`
2. **Turbulence**: if enabled, sample `rglPerlin(turbulenceSize, position + timeOffset)` and add `perlin × turbulenceStrength × dt` to velocity. With `psf_2d_turbulance`, Z component is zeroed.
3. **Damping**: `velocity *= (1 - damping × dt)`
4. **Integration**: `position += velocity × dt`
5. **Angular**: `angularSpeed *= (1 - angularDamping × dt)`, `rotation += angularSpeed × dt`
6. **Lifetime**: `time += dt / life`. Particles with `time ≥ 1.0` are removed (swap-with-last).

### Particle Initialization

Position is randomly offset within the emit box: `emitterTransform × (randomVec × emitBoxSize)` where `randomVec` uses a triangle distribution: `rand() - 0.5 + rand() - 0.5` per axis.

Velocity starts as `emitVelocity + (randomVec × emitRandomness)`, transformed to local space unless `psf_global_emit_dir`, then scaled by `rand() + rand()` (triangle distribution, range 0–2, mean 1).

---

## Rendering

Each frame, all active particles are stamped into a single merged mesh. The source `m_particleMesh` is copied per particle with:

- Position/orientation from the billboard mode calculation
- Vertices scaled by `particle.size × scaleKey` and transformed
- Vertex color set to the interpolated RGBA from the keyframe system
- Normals transformed to match billboard orientation

The mesh is rendered with the source particle mesh's material. Max rendered particles is clamped by `iParticleSystemMaxNumRenderedParticles` and `iParticleSystemMaxNumMeshVertices / faceCorners_per_particle`.

---

## Mapped Particle Systems

The engine has 10 "mapped" particle system slots (`MB_NUM_MAPPED_PARTICLE_SYSTEMS`) that are looked up by name at load time via `mapParticleSystems()`. These are hardcoded engine references for specific gameplay effects (weather, blood, hoof dust, etc.). If a mapped system can't be found by name, the engine logs a warning.

---

## Common Patterns

| Category | Examples | Typical Config |
|----------|----------|----------------|
| Weather | `game_rain`, `game_snow` | `psf_always_emit \| psf_global_emit_dir`, large emit box, high rate |
| Fire | `torch_fire`, `cooking_fire` | `psf_always_emit \| psf_billboard_3d`, orange→red color ramp, upward velocity |
| Smoke | `torch_smoke`, `flue_smoke_*` | `psf_always_emit \| psf_billboard_3d`, dark color, negative gravity (float up), scale ramp up |
| Dust | `game_hoof_dust`, `ladder_dust_*` | `psf_billboard_3d \| psf_2d_turbulance`, earthy colors, low gravity |
| Blood | `game_blood`, `blood_hit_*` | `psf_billboard_3d`, red tint, high damping, gravity |
| Water | `game_water_splash_*` | `psf_emit_at_water_level`, gravity, short life |
| Ambient | `fire_fly_1`, `bug_fly_1`, `fall_leafs_a` | `psf_always_emit`, low rate, long life, turbulence |
| Impact | `stone_hit_1`, `dummy_straw` | Burst (no `psf_always_emit`), high rate, short life |

### Ground Spec Connection

The `gtf_dusty` flag on ground specs (`steppe`, `earth`, `desert`, `path`) controls whether foot dust particles (`game_hoof_dust` variants) are triggered when agents walk on that surface. Snow surfaces use `game_hoof_dust_snow` instead. Mud variants exist for wet conditions.
