# Map Icons - JSON Output Structure

**Script:** `convert_map_icons.py`
**Source:** `module_map_icons.py`
**Output:** `map_icons_full.json`

## Overview

Converts the `map_icons` list into JSON. Handles both short (5-field) and full (8-field with offsets) formats. Trigger code is extracted from the Python source via regex.

## Root Structure

```
map_icons_full.json -> Array of MapIcon objects
```

## MapIcon Object

| Field | Type | Source Field | Description |
|---|---|---|---|
| `id` | `string` | `[0]` | Icon string ID (e.g. `"player"`) |
| `flags` | `Flags` | `[1]` | Icon flags (`mcn_*`) |
| `mesh_name` | `string` | `[2]` | 3D mesh resource name |
| `scale` | `float` | `[3]` | Render scale |
| `sound` | `Sound\|null` | `[4]` | Sound on interact, or null |
| `offset_x` | `float` | `[5]` | X offset (only in 8-field format) |
| `offset_y` | `float` | `[6]` | Y offset (only in 8-field format) |
| `offset_z` | `float` | `[7]` | Z offset (only in 8-field format) |
| `triggers_raw` | `string` | `[8]` or `[5]` | Raw Python trigger code (only if triggers exist) |

## Nested Objects

### Flags

```json
{
  "raw": 1,
  "hex": "0x1",
  "decomposed": ["mcn_no_shadow"]
}
```

### Sound

```json
{
  "value": 12,
  "name": "snd_footstep_grass"
}
```

Set to `null` if the sound value is 0.
