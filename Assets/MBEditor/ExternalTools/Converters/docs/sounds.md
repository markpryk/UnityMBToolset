# Sounds - JSON Output Structure

**Script:** `convert_sounds.py`
**Source:** `module_sounds.py`
**Output:** `sounds_full.json`

## Overview

Converts the `sounds` list into JSON. Each sound is a 3-field tuple with an ID, flags (encoding priority, volume, and playback mode), and a list of sample files. When multiple samples are listed, the engine randomly selects one at playback time.

This runs from the Module System directory.

## Root Structure

```
sounds_full.json -> Array of Sound objects
```

## Sound Object

| Field | Type | Source | Description |
|---|---|---|---|
| `id` | `string` | `[0]` | Sound ID (e.g. `"sword_clash_1"`) |
| `index` | `int` | positional | Zero-based sound index |
| `flags` | `SoundFlags` | `[1]` | Playback flags with priority and volume |
| `samples` | `string[]` | `[2]` | Audio file list (randomly selected) |
| `sample_count` | `int` | derived | Number of sample files |
| `has_samples` | `bool` | derived | `false` if sample list is empty |
| `formats` | `string[]` | derived | Unique file extensions (e.g. `["ogg"]`) |

## SoundFlags Object

```json
{
  "raw_value": 2128,
  "hex": "0x850",
  "symbolic": "sf_priority_5|sf_vol_8",
  "priority": 5,
  "volume": 8,
  "is_2d": false,
  "is_looping": false,
  "decomposed": [...]
}
```

### Bitwise Flags

| Flag | Value | Description |
|---|---|---|
| `sf_2d` | `0x01` | 2D sound (no spatialization) |
| `sf_looping` | `0x02` | Loop continuously |
| `sf_start_at_random_pos` | `0x04` | Start at random position in sample |
| `sf_stream_from_hd` | `0x08` | Stream from disk (for large files) |
| `sf_always_send_via_network` | `0x100000` | Force network replication |

### Priority (bits 4-7)

Encoded as a nibble value 0-15. Higher priority sounds override lower ones when the engine reaches the channel limit.

```
sf_priority_N = N << 4   (e.g. sf_priority_5 = 0x50)
```

### Volume (bits 8-11)

Encoded as a nibble value 0-15. Controls the base volume level of the sound.

```
sf_vol_N = N << 8   (e.g. sf_vol_8 = 0x800)
```

## Notes

- **Random selection**: When a sound has multiple samples, the engine randomly picks one each time. This creates variety for frequently played sounds (e.g. `sword_clash_1` has 8 variants).
- **2D vs 3D**: Sounds without `sf_2d` are 3D-spatialized (positioned in the world). 2D sounds play at constant volume regardless of listener position (UI, music stingers, ambient loops).
- **Empty samples**: `quest_taken` has an empty sample list - it's a placeholder sound with no audio.
- **Mixed formats**: Most sounds use `.ogg`, but some legacy entries use `.wav`.
