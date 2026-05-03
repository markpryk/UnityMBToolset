# Skins - JSON Output Structure

**Script:** `convert_skins.py`
**Source:** `module_skins.py`
**Output:** `skins_full.json`

## Overview

Converts the `skins` list into JSON using a class-based `SkinConverter`. Parses face keys, face textures with color extraction, voice sound entries, skeleton references, blood particle systems, and face key constraints.

This is the only converter that uses a class-based architecture.

## Root Structure

```
skins_full.json -> Array of Skin objects
```

## Skin Object

| Field | Type | Source Field | Description |
|---|---|---|---|
| `index` | `int` | positional | Zero-based skin index |
| `id` | `string` | `[0]` | Skin string ID (e.g. `"man_body_1"`) |
| `flags` | `string` | `[1]` | Resolved flag name (e.g. `"skf_use_morph_key_10"`) |
| `flags_raw` | `int` | `[1]` | Raw numeric flag value |
| `body_mesh` | `string` | `[2]` | Body mesh resource name |
| `calf_mesh` | `string` | `[3]` | Calf mesh resource name |
| `hand_mesh` | `string` | `[4]` | Hand mesh resource name |
| `head_mesh` | `string` | `[5]` | Head mesh resource name |
| `face_keys` | `FaceKey[]` | `[6]` | Morph key definitions |
| `hair_meshes` | `string[]` | `[7]` | Available hair mesh names |
| `beard_meshes` | `string[]` | `[8]` | Available beard mesh names |
| `hair_textures` | `string[]` | `[9]` | Hair texture names |
| `beard_textures` | `string[]` | `[10]` | Beard texture names |
| `face_textures` | `FaceTexture[]` | `[11]` | Face textures with color data |
| `voices` | `VoiceEntry[]` | `[12]` | Voice sound definitions |
| `skeleton` | `string` | `[13]` | Skeleton resource (e.g. `"skel_human"`) |
| `scale` | `float` | `[14]` | Model scale multiplier |
| `blood_particles_1` | `string` | `[15]` | Blood particle system name |
| `blood_particles_2` | `string` | `[16]` | Secondary blood particles |
| `face_key_constraints` | `Constraint[]` | `[17]` | Morph key constraints |

## Nested Objects

### FaceKey

```json
{
  "morph_key": 0,
  "unknown_1": 0,
  "min_value": 0.0,
  "max_value": 1.0,
  "name": "chin_size"
}
```

### FaceTexture

```json
{
  "texture_name": "man_face_young_1",
  "base_color": "0x00ff8040",
  "hair_colors": [0, 1, 2, 3],
  "skin_colors": ["0x00dfc8b0", "0x00c8b090"]
}
```

### VoiceEntry

```json
{
  "type": "voice_die",
  "type_id": 0,
  "sound_id": 42
}
```

Voice types: `voice_die`, `voice_hit`, `voice_grunt`, `voice_grunt_long`, `voice_yell`, `voice_warcry`, `voice_victory`, `voice_stun`.

### Constraint

```json
{
  "threshold": 0.5,
  "comparison": "greater_than",
  "comparison_value": 1,
  "terms": [
    {
      "coefficient": 1.0,
      "face_key_index": 3
    }
  ]
}
```

Face key constraints enforce relationships between morph keys (e.g. jaw width affects chin position).
