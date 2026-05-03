# Meshes - JSON Output Structure

**Script:** `convert_meshes.py`
**Source:** `module_meshes.py`
**Output:** `meshes_full.json`

## Overview

Converts the `meshes` list into JSON. Each mesh is a 12-field tuple defining a UI/presentation mesh with its BRF resource binding, transform (translation, rotation, scale), and render flags. The converter adds convenience flags for aliased meshes (ID != resource name), non-identity transforms, and uniform scale detection.

This runs from the Module System directory (imports `header_meshes.py`).

## Root Structure

```
meshes_full.json -> Array of Mesh objects
```

## Mesh Object

| Field | Type | Source Field | Description |
|---|---|---|---|
| `id` | `string` | `[0]` | Mesh string ID (e.g. `"banner_a01"`) |
| `index` | `int` | positional | Zero-based mesh index |
| `flags` | `Flags` | `[1]` | Mesh flags |
| `resource_name` | `string` | `[2]` | BRF resource mesh name |
| `translation` | `Vector3` | `[3],[4],[5]` | XYZ translation |
| `has_translation` | `bool` | derived | True if any translation axis is non-zero |
| `rotation` | `Vector3` | `[6],[7],[8]` | XYZ rotation in degrees |
| `has_rotation` | `bool` | derived | True if any rotation axis is non-zero |
| `scale` | `Vector3` | `[9],[10],[11]` | XYZ scale |
| `uniform_scale` | `float` | derived | Present only when `sx == sy == sz` |
| `has_custom_scale` | `bool` | derived | True if scale is not (1, 1, 1) |
| `is_aliased` | `bool` | derived | True if `id` differs from `resource_name` |

## Nested Objects

### Flags

```json
{
  "raw_value": 1,
  "hex": "0x1",
  "decomposed": [
    { "name": "render_order_plus_1", "value": 1, "hex": "0x1" }
  ],
  "symbolic": "render_order_plus_1"
}
```

Only one flag is defined in `header_meshes.py`:

| Flag | Value | Description |
|---|---|---|
| `render_order_plus_1` | `0x01` | Increases render order priority by 1 |

### Vector3

```json
{ "x": 0.0, "y": 0.0, "z": -90.0 }
```

Used for translation (world units), rotation (degrees), and scale (multiplier).

## Notes

- **Aliased meshes**: Many banner/arms meshes map different IDs to the same BRF resource (e.g. `"arms_a15"` -> `"banner_f21"`). The `is_aliased` field flags these.
- **Common transforms**: Banners typically have `rotation.x = -90` and occasionally `rotation.z = 90`. Custom banner elements use `scale = 10`.
- **Sentinel meshes**: Entries like `"flag_projects_end"` use resource name `"0"` as markers.
