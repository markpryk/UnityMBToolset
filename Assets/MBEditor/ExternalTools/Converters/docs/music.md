# Music - JSON Output Structure

**Script:** `convert_music.py`
**Source:** `module_music.py`
**Output:** `music_full.json`

## Overview

Converts the `tracks` list into JSON. Each track is a 4-field tuple defining a music track with its filename, trigger flags (cultures + situations + playback mode), and continue flags (conditions under which the track can keep playing when the situation changes).

This runs from the Module System directory.

## Root Structure

```
music_full.json -> Array of Track objects
```

## Track Object

| Field | Type | Source | Description |
|---|---|---|---|
| `id` | `string` | `[0]` | Track ID (e.g. `"ambushed_by_khergit"`) |
| `index` | `int` | positional | Zero-based track index |
| `filename` | `string` | `[1]` | Audio filename (`.ogg`) |
| `flags` | `MusicFlags` | `[2]` | When this track plays |
| `continue_flags` | `MusicFlags` | `[3]` | When the track can continue playing |

## MusicFlags Object

Both `flags` and `continue_flags` share the same structure:

```json
{
  "raw_value": 266244,
  "hex": "0x41004",
  "symbolic": "mtf_culture_3|mtf_sit_ambushed|mtf_sit_siege",
  "cultures": [...],
  "culture_names": ["Khergit"],
  "situations": [...],
  "playback": [...],
  "is_module_track": false,
  "all_cultures": false
}
```

### Culture Flags (bits 0-5)

| Flag | Value | Faction |
|---|---|---|
| `mtf_culture_1` | `0x01` | Swadian |
| `mtf_culture_2` | `0x02` | Vaegir |
| `mtf_culture_3` | `0x04` | Khergit |
| `mtf_culture_4` | `0x08` | Nord |
| `mtf_culture_5` | `0x10` | Rhodok |
| `mtf_culture_6` | `0x20` | Sarranid |
| `mtf_culture_all` | `0x3F` | All cultures (convenience) |

### Playback Flags

| Flag | Value | Description |
|---|---|---|
| `mtf_looping` | `0x40` | Loop the track |
| `mtf_start_immediately` | `0x80` | Start without fade-in |
| `mtf_persist_until_finished` | `0x100` | Don't interrupt until done |

### Situation Flags

| Flag | Value | Description |
|---|---|---|
| `mtf_sit_tavern` | `0x200` | Tavern scenes |
| `mtf_sit_fight` | `0x400` | Combat |
| `mtf_sit_multiplayer_fight` | `0x800` | Multiplayer combat |
| `mtf_sit_ambushed` | `0x1000` | Ambush encounter |
| `mtf_sit_town` | `0x2000` | Town scenes |
| `mtf_sit_town_infiltrate` | `0x4000` | Sneaking into town |
| `mtf_sit_killed` | `0x8000` | Player defeated |
| `mtf_sit_travel` | `0x10000` | World map travel |
| `mtf_sit_arena` | `0x20000` | Arena fights |
| `mtf_sit_siege` | `0x40000` | Siege battles |
| `mtf_sit_night` | `0x80000` | Nighttime |
| `mtf_sit_day` | `0x100000` | Daytime |
| `mtf_sit_encounter_hostile` | `0x200000` | Hostile encounter |
| `mtf_sit_main_title` | `0x400000` | Main menu |
| `mtf_sit_victorious` | `0x800000` | Victory screen |
| `mtf_sit_feast` | `0x1000000` | Feast events |

### Other Flags

| Flag | Value | Description |
|---|---|---|
| `mtf_module_track` | `0x10000000` | Track is in the module folder (not base game) |

## Notes

- **Continue flags**: When the game situation changes, a playing track checks the `continue_flags` to decide whether to keep playing or stop. For example, a siege track with `continue_flags = mtf_sit_fight` will keep playing when the siege transitions into melee combat.
- **Culture filtering**: Tracks with culture flags only play for that faction. The `culture_names` array provides human-readable faction names.
- **Vanilla has no module tracks**: All vanilla tracks use `mtf_module_track = 0`. Mods set this flag for tracks placed in the module directory.
