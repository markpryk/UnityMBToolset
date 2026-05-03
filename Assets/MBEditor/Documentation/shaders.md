# Shader Reference

Key rendering details from `mb.fx` relevant to terrain and flora modding.

---

## Gamma Pipeline

The engine uses gamma 2.2 throughout:

- Texture sample: `pow(texel, 2.2)` via `INPUT_TEX_GAMMA`
- Final output: `pow(color, 1/2.2)` via `OUTPUT_GAMMA`

CPU-side `squareRgb()` in `getPositionColor()` is a pre-correction that works with this pipeline — the perceptual result is subtle darkening, not harsh squaring.

---

## Terrain Techniques

### `dot3`

Standard normal-mapped terrain layer. Single diffuse texture × vertex color × lighting. Used for layers without multitex blending.

### `dot3_multitex`

Earth+green blend when multitex material is active. Two texture samples:

- `MeshTextureSampler` — earth diffuse at base UV
- `Diffuse2Sampler` — green diffuse at `UV × 1.237`

Blend driven by `VertexColor.a` (green layer intensity from CPU):

```hlsl
result.rgb = earth.rgb × (1.0 - VertexColor.a) + green.rgb × VertexColor.a;
result.rgb *= VertexColor.rgb;  // position color tint
```

Fresnel edge darkening: `result.rgb *= max(0.6, fresnel² + 0.1)` — prevents washed-out appearance at glancing camera angles.

---

## Flora Technique

### `flora` / `flora_SHDW` / `flora_SHDWNVIDIA`

Vertex shader splits ambient and sun for shadow map modulation:

- Ambient: `vColor × (vAmbientColor + vSunColor × 0.06)` — tiny sun bleed simulates subsurface scattering
- Sun: `vSunColor × 0.34 × vMaterialColor × vColor` — attenuated to ~1/3

Pixel shader: alpha test at 0.05 (leaf cutouts). Output = `tex × (ambient + sun × shadowAmount)`.

### `flora_PRESHADED`

Fully CPU-shaded objects. Just `vColor × vMaterialColor`, no GPU shadow evaluation.

---

## Grass Technique

### `grass` / `grass_SHDW` / `grass_SHDWNVIDIA`

Key differences from flora:

- Ambient: `vColor × vAmbientColor` (no sun bleed)
- Sun: `vSunColor × 0.55` (stronger than flora's 0.34)
- Distance fade: `alpha = min(1.0, (1.0 - d/50.0) × 2.0)` — starts at 25 units, invisible at 50
- Alpha test: 0.1 (tighter cutout than flora's 0.05)
- `GrassTextureSampler` uses CLAMP addressing

### `grass_no_shadow`

Default grass mode. `vColor × vMaterialColor`, no shadow maps. Used unless `iFloraShadows = 2`.

---

## Billboard Flora (New Tree System)

When `USE_NEW_TREE_SYSTEM` is defined, enables distance-based LOD billboards.

`flora_detail` (default 40.0 units) controls the transition:

- Below `flora_detail_clip`: vertex culled (position = `(0,0,-1,1)`)
- Between clip and fade: `alpha = saturate(0.5 + (distance - fade) / fade_inv)` blends in
- Far: only billboard visible

### `tree_billboards_flora`

Flat-lit billboard batches for distant tree foliage. Uses flora pixel shader with shadow.

### `tree_billboards_dot3_alpha`

Bump-mapped billboard variant with per-pixel sun/sky lighting for higher visual quality.

---

## Standard Shader — Terrain Color Ambient

The `standart` technique (most scene objects) has a `terrain_color_ambient` parameter (default true). When active, ambient switches from constant to hemisphere:

```hlsl
ambientTerm = lerp(vGroundAmbientColor × sun_amount, vAmbientColor, (dot(normal, skyDir) + 1) × 0.5);
```

Ground ambient default: `(84/255, 44/255, 54/255)` — warm reddish bounce light on downward-facing surfaces.

Techniques with `_noterraincolor` suffix disable this for objects where hemisphere ambient is undesirable.

---

## Key Constants

| Constant | Value | Notes |
|----------|-------|-------|
| `input_gamma` | 2.2 | Texture linearization |
| `output_gamma_inv` | 1/2.2 | Final output curve |
| `uv_2_scale` | 1.237 | Multitex second UV offset |
| `flora_detail` | 40.0 | Billboard LOD distance |
| `FLORA_DETAIL_FADE_MUL` | Config-dependent | Fade start multiplier |
| `fShadowMapSize` | 4096 | Shadow map resolution |
| `spec_coef` | 1.0 | Specular coefficient |
| `map_normal_detail_factor` | 1.4 | Map shader normal detail |

---

## UV Coordinates

Terrain UVs are world-space projected:

```
uv = vertex_position.xy / 40.0 × ground_spec.uv_scale
```

Base period = 40 world units. Each ground spec's `uv_scale` adjusts tiling frequency. Multitex second texture samples at `uv × 1.237`.
