# Flora - JSON Output Structure

**Script:** `convert_flora.py`
**Source:** `Flora_kinds.py` (mod-specific, e.g. TLD)
**Output:** `flora_full.json`

## Overview

Converts the `fauna_kinds` list into JSON. This converter is mod-specific - it requires a `Flora_kinds.py` file that defines flora data in Python (not present in vanilla Native). Uses a `__builtin__.open` monkeypatch to safely import Flora_kinds without triggering its file-writing side effects.

Parses terrain conditions, type flags, density from high bits, and mesh entries with tree-specific alternative representations.

## Root Structure

```
flora_full.json -> Array of Flora objects
```

## Flora Object

| Field | Type | Source Field | Description |
|---|---|---|---|
| `id` | `string` | `[0]` | Flora kind string ID |
| `flags` | `FloraFlags` | `[1]` | Terrain, type, and density |
| `meshes` | `MeshEntry[]` | `[2]` | Visual meshes with collision |
| `mesh_count` | `int` | computed | Number of mesh variants |
| `colony_properties` | `Colony` | `[3],[4]` | Only if `fkf_has_colony_props` is set |

## Nested Objects

### FloraFlags

```json
{
  "raw_value": "21474836484",
  "raw_hex": "0x500000004",
  "density": {
    "value": 5,
    "description": "Population density (higher = more flora instances)"
  },
  "terrain_conditions": [
    { "name": "fkf_plain", "value": 4, "hex": "0x4" }
  ],
  "type_flags": [
    { "name": "fkf_tree", "value": 4194304, "hex": "0x400000" }
  ]
}
```

Density is stored in bits 32+ (shifted right by `density_bits = 32`), masked to 16 bits.

Terrain conditions (bits 0-15):
- `fkf_plain`, `fkf_steppe`, `fkf_snow`, `fkf_desert`
- `fkf_plain_forest`, `fkf_steppe_forest`, `fkf_snow_forest`, `fkf_desert_forest`

Type flags (bits 16-31):
- `fkf_grass`, `fkf_tree`, `fkf_rock`
- `fkf_point_up`, `fkf_align_with_ground`, `fkf_snowy`
- `fkf_guarantee`, `fkf_has_colony_props`
- `fkf_on_green_ground`

### MeshEntry

```json
{
  "mesh_name": "tree_oak_a",
  "collision_object": "bo_tree_oak_a",
  "tree_alternative": {
    "collision": null,
    "mesh": "tree_oak_a_lod",
    "note": "Alternative representation for distant trees (NOT FUNCTIONAL in Warband)"
  }
}
```

The `tree_alternative` field only appears for flora with `fkf_tree`.

### Colony

```json
{
  "radius": 5.0,
  "threshold": 3.0,
  "description": "Controls how flora clusters together"
}
```

Only present when `fkf_has_colony_props` flag is active.
