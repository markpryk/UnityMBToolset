# Particle Systems - JSON Output Structure

**Script:** `convert_particle_systems.py`
**Source:** `module_particle_systems.py`
**Output:** `particle_systems_full.json`

## Overview

Converts the `particle_systems` list into JSON. Extracts billboard mode from bits 8-11 as a separate field, decomposes remaining flags, and parses all animation keys and emission parameters.

## Root Structure

```
particle_systems_full.json -> Array of ParticleSystem objects
```

## ParticleSystem Object

| Field | Type | Source Field | Description |
|---|---|---|---|
| `id` | `string` | `[0]` | Particle system string ID |
| `billboard_mode` | `BillboardMode` | `[1] & 0xF00` | Orientation mode (bits 8-11) |
| `flags` | `Flags` | `[1] & ~0xF00` | Other flags (`psf_*`) |
| `mesh_name` | `string` | `[2]` | Particle mesh/texture |
| `num_particles_per_second` | `int` | `[3]` | Emission rate |
| `particle_life` | `float` | `[4]` | Lifetime in seconds |
| `damping` | `float` | `[5]` | Velocity damping |
| `gravity_strength` | `float` | `[6]` | Gravity multiplier |
| `turbulence_size` | `float` | `[7]` | Turbulence scale |
| `turbulence_strength` | `float` | `[8]` | Turbulence intensity |
| `keys` | `AnimKeys` | `[9]-[18]` | Color, alpha, and scale animation |
| `emit_box_size` | `Vector3` | `[19]` | Emission volume dimensions |
| `emit_velocity` | `Vector3` | `[20]` | Initial particle velocity |
| `emit_dir_randomness` | `float` | `[21]` | Direction randomness factor |
| `rotation_speed` | `float` | `[22]` | Particle spin (degrees/sec) |
| `rotation_damping` | `float` | `[23]` | Rotation damping |

## Nested Objects

### BillboardMode

```json
{
  "value": 256,
  "hex": "0x100",
  "name": "psf_billboard_2d"
}
```

Possible modes: `psf_billboard_2d`, `psf_billboard_3d`, `psf_billboard_drop`, `psf_turn_to_velocity`.

### AnimKeys

```json
{
  "alpha": {
    "key_1": { "time": 0.0, "magnitude": 1.0 },
    "key_2": { "time": 1.0, "magnitude": 0.0 }
  },
  "red":   { "key_1": { ... }, "key_2": { ... } },
  "green": { "key_1": { ... }, "key_2": { ... } },
  "blue":  { "key_1": { ... }, "key_2": { ... } },
  "scale": { "key_1": { ... }, "key_2": { ... } }
}
```

Each key is a `(time, magnitude)` pair defining animation over the particle's lifetime.

### Vector3

```json
{ "x": 0.5, "y": 0.5, "z": 1.0 }
```
