# Flora System

The flora system manages all procedurally placed vegetation, rocks, and grass on M&B terrains. Flora is divided into three independent sets with different behavior, and placement is driven by Perlin noise "colonies" evaluated against terrain properties.

---

## Flora Sets

| Set | Enum | Persistent | Physics | Rendering |
|-----|------|-----------|---------|-----------|
| Trees | `fst_trees` | Yes | Havok rigid bodies | Individual entities + billboard LOD batches |
| Rocks | `fst_rocks` | Yes | Havok rigid bodies | Batched collection meshes per terrain block |
| Grass | `fst_grass` | No (streamed) | None | Lazy-built per 20×20 block when camera approaches |

Generation order: rocks → grass → trees. This order affects the RNG sequence and is critical for deterministic replication.

---

## Module System Setup

Flora kinds are defined in `Flora_kinds.py` and exported to `flora_kinds.txt`.

### Entry Format

```python
# (name, flags | density(n), [mesh_instances])

# Grass — billboard quads, high density, green ground only
("grass", fkf_grass | fkf_on_green_ground | fkf_guarantee | fkf_align_with_ground 
    | fkf_point_up | fkf_plain | fkf_plain_forest | density(1500),
    [["grass_a","0"], ["grass_b","0"], ["grass_c","0"]])

# Tree — LOD meshes + body, needs SpeedTree placeholders
("pine_1", fkf_plain | fkf_plain_forest | fkf_steppe | fkf_steppe_forest 
    | fkf_tree | density(4),
    [("pine_1_a", "bo_pine_1_a", ("0","0")),
     ("pine_1_b", "bo_pine_1_b", ("0","0"))])

# Rock — physics body, ground-aligned
("rock", fkf_plain | fkf_align_with_ground | fkf_plain_forest | fkf_steppe 
    | fkf_steppe_forest | fkf_rock | density(50),
    [["rock1","bo_rock1"], ["rock2","bo_rock2"]])

# Hand-place only — has terrain flags but NO set flag
("thorn_a", fkf_align_with_ground | fkf_plain | fkf_plain_forest | density(150),
    [["thorn_a","0"], ["thorn_b","0"]])
```

### Mesh Instance Format

**Grass/Rock** — 2 fields per instance: `[mesh_name, body_name]`. Use `"0"` for no physics body.

**Tree** — 3 fields: `(mesh_name, body_name, (speedtree_alt_1, speedtree_alt_2))`. SpeedTree fields are non-functional in Warband but must be present as `("0","0")`.

---

## Flags

### Terrain Condition Flags (bits 0–15)

Control *where* a flora kind can spawn by matching against the scene's region type:

| Flag | Value | Matches |
|------|-------|---------|
| `fkf_plain` | `0x00000004` | `rt_plain`, `rt_bridge` |
| `fkf_steppe` | `0x00000008` | `rt_steppe` |
| `fkf_snow` | `0x00000010` | `rt_snow` |
| `fkf_desert` | `0x00000020` | `rt_desert` |
| `fkf_plain_forest` | `0x00000400` | `rt_forest` |
| `fkf_steppe_forest` | `0x00000800` | `rt_steppe_forest` |
| `fkf_snow_forest` | `0x00001000` | `rt_snow_forest` |
| `fkf_desert_forest` | `0x00002000` | `rt_desert_forest` |

> `rt_forest` checks against `fkf_plain_forest`, not a separate `fkf_forest` flag.

### Behavior Flags (bits 16+)

| Flag | Value | Effect |
|------|-------|--------|
| `fkf_point_up` | `0x00020000` | Billboard quad geometry (`addMeshPointUp`) |
| `fkf_align_with_ground` | `0x00040000` | Aligns rotation to terrain surface normal |
| `fkf_grass` | `0x00080000` | Routes to grass set (streamed, no physics) |
| `fkf_on_green_ground` | `0x00100000` | Only spawns where `tlt_green` intensity > ~0.2 |
| `fkf_rock` | `0x00200000` | Routes to rock set |
| `fkf_tree` | `0x00400000` | Routes to tree set (requires LOD + body meshes) |
| `fkf_snowy` | `0x00800000` | Uses snowy variant material |
| `fkf_guarantee` | `0x01000000` | Always gets at least one colony pass |
| `fkf_has_colony_props` | `0x04000000` | Overrides colony_radius and colony_threshold |
| `fkf_realtime_ligting` | `0x00010000` | Deprecated — no effect |
| `fkf_speedtree` | `0x02000000` | Non-functional (SpeedTree removed) |

### Density

Packed into upper 32 bits, clamped to 65535:

```python
def density(g):
    if (g > fkf_density_mask):  # 0xFFFF
        g = fkf_density_mask
    return ((dword | g) << density_bits)  # dword=0x8000000000000000, bits=32
```

| Range | Use | Examples |
|-------|-----|---------|
| 1–10 | Sparse large objects | Trees (3–5), large rocks (5) |
| 30–70 | Medium scatter | Bushes (30–70), flowers (50) |
| 150–500 | Dense ground cover | Thorns (150), grass bushes (400) |
| 1000–1500 | Maximum carpet | Grass blades (1500), ferns (1000) |

---

## Set Routing

| Flag | Routed To | Notes |
|------|-----------|-------|
| `fkf_tree` | Tree set | Requires body mesh + SpeedTree placeholders |
| `fkf_grass` | Grass set | Streamed, no physics |
| `fkf_rock` | Rock set | Default fallback |
| **None of the above** | **Not spawned** | Can only be hand-placed in scene editor |

The rock set check is `!(fkf_tree) && !(fkf_grass)` — it does NOT require `fkf_rock`. Kinds without any set flag pass this check but never receive colony passes during generation. Many entries like `thorn_a`, `basak`, `common_plant`, `wheat` are hand-place only.

---

## Generation Pipeline

### Phase 1: Colony Creation (`generateFloraSets`)

The engine determines how many colony "passes" each set gets based on the region type:

| Region | Tree Passes | Rock Passes | Grass Passes |
|--------|-------------|-------------|--------------|
| Forest | 2–5 | 2–3 | 4 |
| Snow Forest | 2 | 2 | 4 |
| Plain / Steppe | 0–2 | 2–5 | 4 |
| Desert | 0 | 2–5 | 3 |
| Steppe Forest | 2–5 | 2–4 | 3 |

Each pass selects a flora kind and creates a colony definition (`mbFaunaRec`) with:

- **Randomness** — 3 values in range 0–10000, unique Perlin noise origin
- **Radius** — 50–100 units (noise frequency). Grass gets ×0.45 = tighter clusters
- **Threshold** — 0–0.05 (minimum noise for presence). Forest trees: −0.07. Grass: −0.11
- **Density** — `(threshold + 0.4) × rand(1,2) × (kind_density/1000) × 15 × densityFactor`

The `densityFactor` is `3.0 / (numPasses + 2.0)`, reducing per-colony density when more passes exist.

The first pass picks from guaranteed kinds (`fkf_guarantee`). Subsequent passes use weighted random by suitability.

### Phase 2: Instance Placement (`populateFloraSet`)

For every terrain face, both triangles are evaluated against each colony. The engine samples 3-octave Perlin noise at the colony's radius frequency, offset by the colony's randomness vector:

```
threshold = (min(noise.x + noise.y + noise.z, 0.5) - colony.threshold) × colony.density × vegetationDensity
```

`vegetationDensity = pow(cellSize, 1.7) × 0.3` compensates for coarser grids having fewer faces.

The placement loop runs while `threshold > rand()`. Each placement subtracts 1.0 from threshold. So threshold=2.7 means 2 guaranteed + 70% chance of a third.

### Placement Conditions

Faces must be: above water (`z > -0.3`), on `tlt_earth` or `tlt_green`, and for grass: `earth + green intensity > 0.9`.

### Green Ground Check

`fkf_on_green_ground`: `green_intensity - 0.2 ≤ rand()` → at green=1.0: 80% pass. At green=0.2: ~0% pass.

All grass (`fkf_grass`): `green_intensity + 0.3 ≤ rand()` → at green=0.0: 30% pass. At green=0.7: 100%.

Both checks `break` on failure — all remaining attempts for this colony at this face are skipped.

### Instance Transform

Position starts at triangle centroid, offset randomly toward each vertex. Three dummy `rglRand()` calls precede position calculation (critical for determinism). Ground-aligned kinds (`fkf_align_with_ground`) get the terrain face normal as up vector. Rocks additionally project Z onto the triangle plane. Random Y rotation 0–360°. Scale: base factor 0.65–1.35, plus per-axis jitter (Z: 0.85–1.15, XY: 0.9–1.1).

---

## Grass Streaming

Grass geometry is built lazily as the camera moves. The terrain is divided into a grid of 20×20 unit blocks (up to 48×48 blocks). Each block has an entity and a populated flag.

Every frame, `updateCamera()` checks each block. If within range (bounding box radius + 40 units) and not yet populated, `populateGrassEntity()` batches all grass instances in that block into a single mesh. Blocks are never depopulated — only the `m_visible` flag toggles rendering.

During population, each grass instance receives a position color tint from `getPositionColor()`. Grass kinds with `fkf_rock` flag (caught by the default routing) get additional random per-axis scale (Z: 0.5–1.5, XY: 0.5–2.0).

In the shader, grass fades with distance: alpha starts dropping at 25 units and reaches 0 at 50 units.

---

## Flora Shading

Trees and rocks are shaded at load time via `shadeFloraSet()`. Grass is shaded per-block during streaming.

For materials with `mf_uniform_shading`, the engine computes per-instance lighting:

- **Grass sun**: dot product of ground face normal vs light direction
- **Shadow quality 0**: ray cast from flora center toward sun. If occluded, intensity ×0.1
- **Shadow quality 1**: samples shadow map per vertex
- **Shadow quality 2**: skips CPU shading, uses GPU shadow maps

Final vertex color: `ambientColor × 1.5 + sunColor × sunIntensity`

### Position Color

`getPositionColor()` generates procedural color variation from 3-octave Perlin noise at frequency 15.0. Snow biomes return white. Other biomes get subtle RGB variation near 1.0 with constraints: green can't drop far below red, blue never exceeds red or green. Desert suppresses green channel. Final `squareRgb()` applies gamma-like darkening. This color tints terrain vertices and grass instances. Rock terrain vertices are exempt (always white).

---

## Entity Creation

**Trees** with LOD meshes get individual entities for close-up rendering plus batched billboard meshes (shader: `tree_billboards_flora`/`tree_billboards_bark`) for distant LOD. Billboard collections share a single entity with 10000 unit visibility radius.

**Rocks and non-LOD flora** are batched into collection meshes per terrain block per material. Each batch can hold up to 32000 face corners. `fkf_point_up` kinds use `addMeshPointUp()` (billboard). Others use `addMesh()` (full 3D).

**Grass** entities are one per 20×20 block. Each block has a single mesh that gets all instances merged into it during the streaming pass.
