# Ground Specs - JSON Output Structure

**Script:** `convert_ground_specs.py`
**Source:** `Ground_specs.py` or `module_ground_specs.py`
**Output:** `ground_specs_full.json`

## Overview

Converts the `ground_specs` list into JSON. Ground types are hardcoded in the engine - you cannot add new types, only modify existing entries. The converter decomposes `gtf_*` flags and parses the optional ambient color override.

The source file is standalone (not part of the module system import chain). The converter tries importing from `Ground_specs.py` first, then `module_ground_specs.py`, falling back to `execfile()` if direct import fails.

## Root Structure

```
ground_specs_full.json -> Array of GroundSpec objects
```

## GroundSpec Object

| Field | Type | Source Field | Description |
|---|---|---|---|
| `id` | `string` | `[0]` | Ground spec name (e.g. `"turf"`) |
| `index` | `int` | positional | Zero-based index matching `header_ground_types.py` |
| `ground_constant` | `string` | derived | Python constant name (e.g. `"ground_turf"`) |
| `flags` | `Flags` | `[1]` | Ground spec flags (`gtf_*`) |
| `material` | `string` | `[2]` | Material/texture name (e.g. `"grassy_ground"`) |
| `uv_scale` | `float` | `[3]` | UV tiling scale |
| `multitex_material` | `string\|null` | `[4]` | Multitexture blend material, or null if `"none"` |
| `color` | `Color` | `[5]` | Ambient color override (only when `gtf_has_color` is set) |

## Nested Objects

### Flags

```json
{
  "raw_value": 7,
  "hex": "0x7",
  "decomposed": [
    { "name": "gtf_overlay", "value": 1, "hex": "0x1" },
    { "name": "gtf_dusty", "value": 2, "hex": "0x2" },
    { "name": "gtf_has_color", "value": 4, "hex": "0x4" }
  ],
  "symbolic": "gtf_overlay|gtf_dusty|gtf_has_color"
}
```

Available flags:

| Flag | Value | Description |
|---|---|---|
| `gtf_overlay` | `0x01` | Deprecated overlay flag |
| `gtf_dusty` | `0x02` | Enables foot dust particle systems on this ground |
| `gtf_has_color` | `0x04` | Enables ambient color override (field 5) |

### Color

```json
{
  "r": 0.42,
  "g": 0.59,
  "b": 0.17,
  "html_clamped": "#6B962B"
}
```

| Field | Type | Description |
|---|---|---|
| `r`, `g`, `b` | `float` | Linear float RGB. Can exceed 1.0 (used as ambient multipliers) |
| `html_clamped` | `string` | Clamped 0-1 preview hex color |

Values exceeding 1.0 (e.g. snow at `1.4, 1.4, 1.4`) are engine ambient multipliers, not true RGB.
