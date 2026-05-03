# Skyboxes - JSON Output Structure

**Script:** `convert_skyboxes.py`
**Source:** `Skyboxes.py` or `module_skyboxes.py`
**Output:** `skyboxes_full.json`

## Overview

Converts the `skyboxes` list into JSON. Decomposes flags into time-of-day (encoded), cloud density (encoded), and render flags (bitwise). Parses sun, hemisphere, and ambient colors as linear float RGB, and fog as distance + ARGB color.

The source file is standalone and writes `skyboxes.txt` on execution. The converter uses a monkeypatched `open()` to suppress file writes during import.

HDR skybox entries typically duplicate their LDR counterpart with the `sf_HDR` flag added.

## Root Structure

```
skyboxes_full.json -> Array of Skybox objects
```

## Skybox Object

| Field | Type | Source Field | Description |
|---|---|---|---|
| `mesh_name` | `string` | `[0]` | Skybox mesh name (e.g. `"skybox_cloud_1"`) |
| `index` | `int` | positional | Zero-based entry index |
| `flags` | `SkyboxFlags` | `[1]` | Time-of-day, clouds, render flags |
| `sun_heading` | `float` | `[2]` | Sun heading in degrees |
| `sun_altitude` | `float` | `[3]` | Sun altitude in degrees |
| `flare_strength` | `float` | `[4]` | Sun flare intensity (0.0 - 1.0) |
| `postfx` | `string` | `[5]` | Post-effects preset (e.g. `"pfx_sunny"`) |
| `sun_color` | `Color` | `[6]` | Sun light color (linear RGB, can exceed 1.0) |
| `hemi_color` | `Color` | `[7]` | Hemisphere light color |
| `ambient_color` | `Color` | `[8]` | Ambient light color |
| `fog` | `Fog` | `[9]` | Fog parameters |

## Nested Objects

### SkyboxFlags

```json
{
  "raw_value": 805306417,
  "hex": "0x30000011",
  "time_of_day": {
    "value": 1,
    "name": "sf_dawn"
  },
  "cloud_density": {
    "value": 16,
    "name": "sf_clouds_1",
    "level": 1
  },
  "render_flags": [
    { "name": "sf_no_shadows", "value": 268435456, "hex": "0x10000000" },
    { "name": "sf_HDR", "value": 536870912, "hex": "0x20000000" }
  ],
  "symbolic": "sf_dawn|sf_clouds_1|sf_no_shadows|sf_HDR"
}
```

**Time of day** (bits 0-3, mutually exclusive encoded values):

| Constant | Value | Description |
|---|---|---|
| `sf_day` | `0x00` | Daytime |
| `sf_dawn` | `0x01` | Dawn/sunset |
| `sf_night` | `0x02` | Nighttime |

**Cloud density** (bits 4-7, mutually exclusive encoded values):

| Constant | Value | Level | Description |
|---|---|---|---|
| `sf_clouds_0` | `0x00` | 0 | Clear sky |
| `sf_clouds_1` | `0x10` | 1 | Light clouds |
| `sf_clouds_2` | `0x20` | 2 | Moderate clouds |
| `sf_clouds_3` | `0x30` | 3 | Heavy clouds / overcast |

**Render flags** (bitwise):

| Constant | Value | Description |
|---|---|---|
| `sf_no_shadows` | `0x10000000` | Disable shadow rendering |
| `sf_HDR` | `0x20000000` | HDR skybox variant (requires RGBE textures) |

### Color

```json
{
  "r": 1.8348,
  "g": 1.6819,
  "b": 1.5012,
  "html_clamped": "#FFFFFF"
}
```

Linear float RGB. Sun color frequently exceeds 1.0 for HDR lighting. The `html_clamped` field clamps to 0-1 range for preview purposes.

### Fog

```json
{
  "start_distance": 300.0,
  "color": {
    "raw_value": 4287406765,
    "hex": "0xFF8CA2AD",
    "argb": [255, 140, 162, 173],
    "html": "#8CA2AD"
  }
}
```

| Field | Type | Description |
|---|---|---|
| `start_distance` | `float` | Distance in meters where fog begins |
| `color.raw_value` | `int` | Raw 32-bit ARGB value |
| `color.hex` | `string` | Hex representation |
| `color.argb` | `int[4]` | Decomposed [A, R, G, B] bytes |
| `color.html` | `string` | RGB-only hex for preview |
