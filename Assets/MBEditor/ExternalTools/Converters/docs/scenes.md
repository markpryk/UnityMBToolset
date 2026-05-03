# Scenes - JSON Output Structure

**Script:** `convert_scenes.py`
**Source:** `module_scenes.py`
**Output:** `scenes_full.json`

## Overview

Converts the `scenes` list into JSON. Decomposes scene flags (`sf_*`), parses terrain bounds as position pairs, and captures water level, terrain code, chest troops, and optional outer terrain border.

## Root Structure

```
scenes_full.json -> Array of Scene objects
```

## Scene Object

| Field | Type | Source Field | Description |
|---|---|---|---|
| `id` | `string` | `[0]` | Scene string ID (e.g. `"town_1_center"`) |
| `flags` | `Flags` | `[1]` | Scene flags (`sf_*`) |
| `mesh_name` | `string` | `[2]` | Terrain mesh name |
| `body_name` | `string` | `[3]` | Collision body name |
| `min_pos` | `Position` | `[4]` | Terrain minimum bounds |
| `max_pos` | `Position` | `[5]` | Terrain maximum bounds |
| `water_level` | `float` | `[6]` | Water plane height |
| `terrain_code` | `string` | `[7]` | Terrain generation code string |
| `other_scenes` | `int[]` | `[8]` | Linked scene IDs (deprecated, only if non-empty) |
| `chest_troops` | `int[]` | `[9]` | Troop IDs for scene chests (only if non-empty) |
| `outer_terrain_border` | `string` | `[10]` | Outer terrain mesh (optional) |

## Nested Objects

### Flags

```json
{
  "raw_value": 256,
  "hex": "0x100",
  "decomposed": [
    {
      "name": "sf_generate",
      "value": 256,
      "hex": "0x100"
    }
  ]
}
```

Common scene flags:
- `sf_generate` - Scene terrain is auto-generated
- `sf_indoors` - Indoor scene
- `sf_force_skybox` - Force skybox rendering
- `sf_no_rain` - Disable rain particles

### Position

```json
{
  "x": -40.0,
  "y": -40.0
}
```

Terrain bounds define the playable area rectangle.
