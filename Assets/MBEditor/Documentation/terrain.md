# Terrain Generation

The terrain system generates a heightfield mesh with layered surface types, driven by a deterministic terrain code. The pipeline produces geometry, per-vertex layer intensities, and per-face blending weights that are then rendered with the painter's algorithm.

---

## Terrain Code

The terrain code encodes all procedural parameters as **6 unsigned 32-bit keys** (192 bits), serialized as a `0x`-prefixed 48-hex-digit string. Keys are printed in reverse order (key 5 first, key 0 last).

### Key Bitfield Layout

| Key | Bitmask | Shift | Parameter | Range |
|-----|---------|-------|-----------|-------|
| 0 | `0x7FFFFFFF` | 0 | Seed 0 (terrain) | 0–2B |
| 1 | `0x7FFFFFFF` | 0 | Seed 1 (river) | 0–2B |
| 1 | `0x80000000` | 31 | Deep Water | 0–1 |
| 2 | `0x7FFFFFFF` | 0 | Seed 2 (flora) | 0–2B |
| 3 | `0x3FF` | 0 | Size X | 0–1023 |
| 3 | `0xFFC00` | 10 | Size Y | 0–1023 |
| 3 | `0x80000000` | 31 | Place River | 0–1 |
| 4 | `0x7F` | 0 | Valley | 0–127 (÷100) |
| 4 | `0x3F80` | 7 | Hill Height | 0–127 |
| 4 | `0x1FC000` | 14 | Ruggedness | 0–127 |
| 4 | `0xFE00000` | 21 | Vegetation | 0–127 (÷100) |
| 4 | `0xF0000000` | 28 | Region Type | 0–15 |
| 5 | `0x3` | 0 | Region Detail | 0–3 |
| 5 | `0xC` | 2 | Disable Grass | 0–2 |

Cell size = `regionDetail + 2.0` meters (2.0, 3.0, 4.0, or 5.0).

### Seed Phases

Each seed resets the RNG at a specific generation phase:

| Seed | Phase | Drives |
|------|-------|--------|
| Seed 0 | `rglSrand(seed0)` | Layers, grid, hills, terrain mesh |
| Seed 1 | `rglSrand(seed1)` | River generation |
| Seed 2 | `rglSrand(seed2)` | Position color noise, flora placement |

---

## Region Types

| Enum | Value | Generable | Notes |
|------|-------|-----------|-------|
| `rt_ocean` | 0 | No | |
| `rt_mountain` | 1 | No | |
| `rt_steppe` | 2 | Yes | |
| `rt_plain` | 3 | Yes | |
| `rt_snow` | 4 | Yes | No green layer |
| `rt_desert` | 5 | Yes | No green layer, barrenness=0.26 |
| `rt_bridge` | 7 | No | |
| `rt_river` | 8 | No | |
| `rt_mountain_forest` | 9 | No | |
| `rt_steppe_forest` | 10 | Yes | |
| `rt_forest` | 11 | Yes | |
| `rt_snow_forest` | 12 | Yes | No green layer |
| `rt_desert_forest` | 13 | Yes | No green layer |
| `rt_deep_water` | 15 | No | |

Generable types exposed in the editor: `{ 2, 3, 4, 5, 11, 12, 10, 13 }`.

---

## Generation Pipeline

| Step | Function | Seed | Purpose |
|------|----------|------|---------|
| 1 | `generateLayers()` | 0 | Assign ground specs to 4 core layers |
| 2 | Grid allocation | 0 | `(numFaces+1)²` vertex grid |
| 3 | `generateTerrain()` | 0 | Perlin displacement, valleys, borders |
| 4 | `generateRiver()` | 1 | Pathfind, expand, set depth |
| 5 | `smoothHeight()` | — | Neighbor-averaged smoothing |
| 6 | `computeNormals()` | — | Face → vertex normals |
| 7 | `computeVertexLayerIntensities()` | — | Slope-based rock/earth/green |
| 8 | `selectFaceOrientation()` | — | Triangle diagonal per quad |
| 9 | `selectFaceLayers()` | — | Primary layer per triangle |
| 10 | `roughenRockVertices()` | — | Random Z on rock vertices |
| 11 | Ground paint | — | Override from SCO data |
| 12 | `computeNormals()` | — | Recompute after paint elevation |
| 13 | `computeFaceLayerIntensities()` | — | Final painter's algorithm weights |
| 14 | `generateOuterTerrain()` | — | Surrounding landscape mesh |
| 15 | Flora generation | 2 | Colony creation + placement |
| 16 | Entity creation | — | Mesh batching, physics, shading |

---

## Grid Construction

The terrain is a regular grid of quads, each split into 2 triangles:

```
numFaces = clamp(terrainSize / cellSize, 40, 250)
```

Vertices are initialized on a flat plane at `(x × cellSize, y × cellSize, 0.0)`. All vertices start with `rock = 1.0`, all other layers at 0. Rock is the implicit base.

---

## Terrain Layer System

### Painter's Algorithm

The engine renders terrain layers from bottom to top. Higher-index layers **occlude** lower ones. A vertex is "visible" for a given layer if any layer intensity remains after accounting for all layers above it:

```cpp
bool isVisible(int layerNo) {
    float transparency = 1.0f;
    for (int i = MB_NUM_TERRAIN_LAYERS - 1; i > layerNo; --i)
        transparency -= m_layerIntensities[i] * transparency;
    return (m_layerIntensities[layerNo] * transparency > 0.01f);
}
```

### Core Layers

| Index | Enum | Role |
|-------|------|------|
| 0 | `tlt_earth` | Dominant soil |
| 1 | `tlt_green` | Vegetation overlay |
| 2 | `tlt_rock` | Steep slope rock |
| 3 | `tlt_riverbed` | Always pebbles |
| 4–14 | Ground paint | Any ground spec, from SCO |

### Per-Face Layer Intensities

`computeFaceLayerIntensities()` averages the 4 corner vertices of each face to produce per-face blending weights. This is what flora placement reads to determine surface type.

---

## Vertex Layer Intensity

### Slope Formula

Earth layer intensity is driven by terrain slope (vertex normal Z component):

```
earth = 1.0 - clamp(((1.0 - normal.z) - barrenness) × 12.0, 0, 1)
```

- `barrenness = 0.19` (default) or `0.26` (desert)
- Flat surfaces (`normal.z ≈ 1.0`): full earth
- Steep slopes: earth drops to 0, exposing rock beneath

### Green Layer

Green intensity = earth intensity × Perlin noise pattern:

```
noise = rglPerlinOctave(15.0, 0.6, 3, false, vertex_grid_position)
intensity = clamp((noise.x + noise.y + noise.z) × 4.5 + 0.7, 0, 1)
green = earth × intensity
```

If noise intensity > 0.99, earth is suppressed to 0 (full green coverage).

Snow and desert biomes have no green layer (`greenGroundSpec = -1`).

### Rock Roughening

After layer assignment, vertices where `green + earth < 0.5` (predominantly rock) get random Z displacement scaled by `cellSize × 0.7`, creating jagged rock appearance.

---

## River System

River generation is a weighted random-walk pathfinding algorithm:

1. Find start edge vertex with good Perlin noise value
2. Find endpoint ≥70% of terrain diagonal away
3. Walk step-by-step, choosing directions weighted by: height gradient, direction alignment, edge avoidance
4. Direction likelihoods are cubed (strongly favor downhill + aligned moves)
5. If minimum depth ≥ 3.5, reject and retry (up to 4 attempts)
6. Expand with neighbor propagation, set depth

Expansion passes depend on cell size — larger cells get fewer passes.

---

## Ground Paint System

Ground paint is stored in the SCO file and **overrides** procedural layer intensities at painted vertices.

### Layer Structure

| Layer | Type | Data Format | Purpose |
|-------|------|-------------|---------|
| 0–10 | Ground spec | Byte → float (÷255) | Layer intensity override |
| 11 | Elevation | uint32 (raw float bits) | Vertex height modification |
| 12 | Color | uint32 (ARGB, default 0xFFFFFFFF) | Vertex color tinting |

Elevation layer magic: `-7793`. Color layer magic: `-12565`.

### Storage

RLE-compressed, column-major (`index = x × size.y + y`). Alternating empty/full runs. Ground spec layers map to terrain layers with offset: `paintLayerIndex + MB_NUM_CORE_TERRAIN_LAYERS` (offset by 4).

---

## Terrain Mesh Creation

The terrain is split into blocks of 40 faces each. For each block, the engine creates separate meshes per active layer.

When multitex is active (earth layer has a multitex material), the green layer is **not rendered as a separate mesh**. Instead, the earth mesh uses the multitex material and green intensity is encoded in vertex alpha. The shader blends between earth and green textures based on this alpha.

Each terrain mesh layer gets render order `layerNo + 11` (higher layers draw on top).

### Vertex Color

Non-rock layers: `getPositionColor(position) × vertex.m_color`. Rock layers: white (no tinting). Alpha channel = layer intensity × transparency × 255.

---

## SCO File Format

```
SCO File:
├── int32: numMissionObjects (or MB_SCO_MAGIC if versioned)
├── [if magic] int32: version
├── [if magic] int32: numMissionObjects
├── MissionObject[numMissionObjects]
├── [if version >= 4] Stream: AI mesh data
└── [optional] int32: MB_SCO_GROUND_PAINT_MAGIC
    └── Stream: ground paint data
```

Ground paint stream:

```
int32: numLayers
IntVector2: size
Per layer:
    int32: groundSpecNo (or magic for elevation/color)
    String: groundSpecId (name-based lookup overrides index)
    int32: hasCells
    [if hasCells] RLE data
```

---

## Perlin Noise

The engine uses a custom Perlin noise with fixed permutation and gradient tables (not standard Perlin). `rglPerlinOctave` has a critical quirk: `gain` is overridden to `0.61` after the first octave.

| Use | Frequency | Octaves | Purpose |
|-----|-----------|---------|---------|
| `generateTerrain()` | `noiseFrequency` | 1 | Valley slope |
| `generateTerrain()` | 40.0 | 4 | Border variation |
| `computeVertexLayerIntensities()` | 15.0 | 3 | Green layer pattern |
| `populateFloraSet()` | `colony.radius` | 3 | Colony noise |
| `getPositionColor()` | 15.0 | 3 | Color variation |

---

## Constants

| Constant | Value |
|----------|-------|
| Min/Max faces per axis | 40 / 250 |
| Max terrain blocks per axis | 5 |
| Max grass blocks per axis | 48 |
| Terrain layers | 15 (4 core + 11 paint) |
| Ground specs | 11 |
| Terrain block size | 40 faces |
| Grass block size | 20 world units |
| Grass visibility | block radius + 40 units |
| Coordinate system | Z-up, XY horizontal |
