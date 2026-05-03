# PostFX - JSON Output Structure

**Script:** `convert_postfx.py`
**Source:** `module_postfx.py`
**Output:** `postfx_full.json`

## Overview

Converts the `postfx_params` list into JSON. Each preset is a 6-field tuple defining a post-processing configuration with HDR, bloom, blur, and lighting coefficient parameters. These presets are referenced by skybox entries (e.g. `pfx_sunny`, `pfx_night`) and applied per-atmosphere.

This runs from the Module System directory.

## Root Structure

```
postfx_full.json -> Array of PostFX objects
```

## PostFX Object

| Field | Type | Source | Description |
|---|---|---|---|
| `id` | `string` | `[0]` | Preset ID (e.g. `"sunny"`, `"night"`) |
| `index` | `int` | positional | Zero-based index |
| `flags` | `Flags` | `[1]` | PostFX flags |
| `tonemap_operator` | `Tonemap` | `[2]` | Tonemapping operator type |
| `params1` | `Params1` | `[3]` | HDR / luminance parameters |
| `params2` | `Params2` | `[4]` | Bloom / blur parameters |
| `params3` | `Params3` | `[5]` | Lighting coefficient parameters |

## Nested Objects

### Flags

| Flag | Value | Description |
|---|---|---|
| `fxf_highhdr` | `0x01` | Enable high HDR mode |

### Tonemap Operator

```json
{ "value": 3, "name": "aces" }
```

| Value | Name | Description |
|---|---|---|
| 0 | `linear` | No tonemapping (linear passthrough) |
| 1 | `reinhard` | Reinhard tonemapping |
| 2 | `filmic` | Filmic curve |
| 3 | `aces` | ACES filmic (Academy Color Encoding) |

### Params1 (PFX1) - HDR / Luminance

Maps to shader `postfx_editor_vector[1]`.

```json
{
  "hdr_range": 128.0,
  "hdr_exposure_scaler": 1.04,
  "luminance_average_scaler": 1.2941,
  "luminance_max_scaler": 10.0,
  "raw": [128.0, 1.04, 1.2941, 10.0]
}
```

| Component | Shader | Description |
|---|---|---|
| `hdr_range` | `.x` | HDR dynamic range |
| `hdr_exposure_scaler` | `.y` | Exposure multiplier |
| `luminance_average_scaler` | `.z` | Average luminance scale |
| `luminance_max_scaler` | `.w` | Maximum luminance clamp |

### Params2 (PFX2) - Bloom / Blur

Maps to shader `postfx_editor_vector[2]`.

```json
{
  "brightpass_threshold": 2.3725,
  "brightpass_post_power": 2.1569,
  "blur_strength": 1.8431,
  "blur_amount": 0.4863,
  "raw": [2.3725, 2.1569, 1.8431, 0.4863]
}
```

| Component | Shader | Description |
|---|---|---|
| `brightpass_threshold` | `.x` | Luminance threshold for bloom |
| `brightpass_post_power` | `.y` | Power curve after brightpass |
| `blur_strength` | `.z` | Blur kernel intensity |
| `blur_amount` | `.w` | Blur blend factor |

### Params3 (PFX3) - Lighting Coefficients

Maps to shader `postfx_editor_vector[3]`.

```json
{
  "ambient_color_coef": 1.0,
  "sun_color_coef": 1.0,
  "specular_coef": 1.05,
  "reserved": 1.0,
  "raw": [1.0, 1.0, 1.05, 1.0]
}
```

| Component | Shader | Description |
|---|---|---|
| `ambient_color_coef` | `.x` | Ambient light multiplier |
| `sun_color_coef` | `.y` | Sun/directional light multiplier |
| `specular_coef` | `.z` | Specular highlight multiplier |
| `reserved` | `.w` | Reserved (typically 1.0) |

## Notes

- **Skybox reference**: Skyboxes reference these presets by name (e.g. `"pfx_sunny"` maps to the `"sunny"` preset with the `pfx_` prefix stripped).
- **Shader binding**: Parameters map directly to `postfx_editor_vector[1..3]` in the engine's HLSL shaders, accessible as `float4` vectors.
- **Night preset**: Uses `blur_amount = 0.0` and lower bloom to simulate reduced visual acuity.
