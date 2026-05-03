# Ground Specifications

Ground specs define the visual and physical properties of each terrain surface type. There are exactly **11 ground specs** hardcoded in the engine — you cannot add new ones without engine modification.

---

## Ground Spec Table

| Index | ID | Material | UV Scale | Multitex Material | Ambient Color | Flags |
|-------|-----|----------|----------|-------------------|---------------|-------|
| 0 | `gray_stone` | `stone_a` | 4.0 | — | (0.70, 0.70, 0.70) | `gtf_has_color` |
| 1 | `brown_stone` | `patch_rock` | 2.0 | — | (0.70, 0.70, 0.70) | `gtf_has_color` |
| 2 | `turf` | `grassy_ground` | 3.3 | `ground_earth_under_grass` | (0.42, 0.59, 0.17) | `gtf_overlay\|gtf_has_color` |
| 3 | `steppe` | `ground_steppe` | 3.0 | `ground_earth_under_steppe` | (0.85, 0.73, 0.36) | `gtf_overlay\|gtf_dusty\|gtf_has_color` |
| 4 | `snow` | `snow` | 5.2 | — | (1.40, 1.40, 1.40) | `gtf_overlay\|gtf_has_color` |
| 5 | `earth` | `ground_earth` | 4.5 | — | (0.70, 0.50, 0.23) | `gtf_overlay\|gtf_dusty\|gtf_has_color` |
| 6 | `desert` | `ground_desert` | 2.5 | — | (1.40, 1.20, 0.40) | `gtf_overlay\|gtf_dusty\|gtf_has_color` |
| 7 | `forest` | `ground_forest` | 4.2 | `ground_forest_under_grass` | (0.60, 0.42, 0.28) | `gtf_overlay\|gtf_has_color` |
| 8 | `pebbles` | `pebbles` | 4.1 | — | (0.70, 0.70, 0.70) | `gtf_overlay\|gtf_has_color` |
| 9 | `village` | `ground_village` | 7.0 | — | (1.00, 0.90, 0.59) | `gtf_overlay\|gtf_has_color` |
| 10 | `path` | `ground_path` | 6.0 | — | (0.93, 0.68, 0.34) | `gtf_overlay\|gtf_dusty\|gtf_has_color` |

---

## Flags

| Flag | Value | Effect |
|------|-------|--------|
| `gtf_overlay` | `0x01` | Deprecated — no runtime effect |
| `gtf_dusty` | `0x02` | Enables foot dust particle systems on this surface |
| `gtf_has_color` | `0x04` | Uses custom ambient color instead of default `(0.61, 0.72, 0.15)` |

---

## Module System Setup

Ground specs are defined in `Ground_specs.py`. The file exports three outputs:

- `ground_specs.txt` — Read by the engine at startup
- `ground_spec_codes.h` — C++ enum header
- `header_ground_types.py` — Python constants for the module system

### Entry Format

```python
# (spec_name, flags, material_name, uv_scale, multitex_material_name, color_tuple)
("turf", gtf_overlay|gtf_has_color, "grassy_ground", 3.3, "ground_earth_under_grass", (0.42, 0.59, 0.17)),
```

- **spec_name** — Internal identifier, becomes `ground_turf` in the header
- **flags** — Combination of `gtf_overlay`, `gtf_dusty`, `gtf_has_color`
- **material_name** — BRF material reference used for terrain rendering
- **uv_scale** — Texture tiling multiplier (higher = more repetitions)
- **multitex_material_name** — Material used for smooth earth↔green blending, or `"none"`
- **color_tuple** — RGB ambient color when `gtf_has_color` is set. Values above 1.0 create overbright (snow: 1.4, desert: 1.4/1.2)

### Constraints

The order of entries matters — the engine references them by index. The generated C++ enum must match exactly:

```cpp
typedef enum {
  ground_gray_stone,   // 0
  ground_brown_stone,  // 1
  ground_turf,         // 2
  ground_steppe,       // 3
  ground_snow,         // 4
  ground_earth,        // 5
  ground_desert,       // 6
  ground_forest,       // 7
  ground_pebbles,      // 8
  ground_village,      // 9
  ground_path,         // 10
} Ground_spec_codes;
```

---

## How Ground Specs Map to Terrain Layers

The engine has 4 **core terrain layers** that are assigned ground specs based on region type during `generateLayers()`:

| Layer | Enum | Role |
|-------|------|------|
| `tlt_earth` (0) | Core | The dominant soil surface |
| `tlt_green` (1) | Core | Vegetation overlay (grass/steppe/forest) |
| `tlt_rock` (2) | Core | Exposed rock on steep slopes |
| `tlt_riverbed` (3) | Core | Always `ground_pebbles` |

Additionally, there are 11 **ground paint layers** (indices 4–14) that can hold any ground spec, applied from the SCO file to override procedural generation.

### Layer Assignment by Region Type

| Region | `tlt_earth` | `tlt_green` | `tlt_rock` | Multitex |
|--------|------------|-------------|------------|----------|
| Plain | `ground_earth` | `ground_turf` | gray/brown stone | `ground_turf` |
| Steppe | `ground_earth` | `ground_steppe` | gray/brown stone | `ground_steppe` |
| Snow | `ground_snow` | *(none)* | gray/brown stone | *(none)* |
| Desert | `ground_desert` | *(none)* | gray/brown stone | *(none)* |
| Forest | `ground_forest` | `ground_turf` | gray/brown stone | `ground_forest` |
| Steppe Forest | `ground_forest` | `ground_steppe` | gray/brown stone | *(none)* |

Snow and desert have **no green layer**. This means no `fkf_on_green_ground` flora can spawn on these biomes.

---

## Multitex Blending

Three ground specs have multitex materials: **turf**, **steppe**, and **forest**. When multitex is active, the engine does not render the green layer as a separate mesh. Instead, the earth layer mesh uses the multitex material, and the green layer intensity is encoded into vertex alpha. The `dot3_multitex` shader blends the two textures:

```hlsl
multi_tex_col.rgb = earth_tex.rgb * (1.0 - VertexColor.a) + green_tex.rgb * VertexColor.a;
```

The green texture is sampled at `uv × 1.237` to break tiling alignment with the earth texture.

---

## UV Coordinates

Terrain UVs are world-space projected:

```
uv = (vertex_position.xy / 40.0) × ground_spec.uv_scale
```

The base period is 40 world units. A UV scale of 3.3 (turf) means the texture repeats every ~12.1 units. A UV scale of 7.0 (village) means the texture repeats every ~5.7 units.

---

## Ambient Color

Each ground spec's ambient color tints the terrain surface. The engine multiplies this color into terrain vertex colors and uses it in the hemisphere ambient lighting model. Values above 1.0 (snow at 1.4, desert at 1.4/1.2) create an overbright effect that makes those surfaces appear brighter under ambient light.

The `getPositionColor()` function adds additional per-vertex Perlin noise variation on top of this base color, creating organic-looking color patches across the terrain. Rock layers (`tlt_rock`) are exempt — they always use white (no tinting).
