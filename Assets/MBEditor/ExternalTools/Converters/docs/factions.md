# Factions - JSON Output Structure

**Script:** `convert_factions.py`
**Source:** `module_factions.py`
**Output:** `factions_full.json`

## Overview

Converts the `factions` list into JSON. Decomposes faction flags with special handling for `max_player_rating` encoded in bits 8-15. Parses inter-faction relations with human-readable descriptions and resolves faction colors to RGB/HTML format.

## Root Structure

```
factions_full.json -> Array of Faction objects
```

## Faction Object

| Field | Type | Source Field | Description |
|---|---|---|---|
| `id` | `string` | `[0]` | Faction string ID (e.g. `"kingdom_1"`) |
| `name` | `string` | `[1]` | Display name |
| `flags` | `FactionFlags` | `[2]` | Faction flags with rating extraction |
| `coherence` | `float` | `[3]` | Internal coherence value (0.0 - 1.0) |
| `relations` | `Relation[]` | `[4]` | Inter-faction relations (only if non-empty) |
| `ranks` | `Rank[]` | `[5]` | Faction rank names (only if non-empty) |
| `color` | `Color` | `[6]` | Faction map color (only if present) |

## Nested Objects

### FactionFlags

```json
{
  "raw_value": 2816,
  "hex": "0xb00",
  "flags": [
    {
      "name": "ff_always_hide_label",
      "value": 1,
      "hex": "0x1"
    }
  ],
  "max_player_rating": {
    "encoded_value": 11,
    "original_rating": 89,
    "note": "Created via max_player_rating(89)"
  }
}
```

| Field | Type | Description |
|---|---|---|
| `raw_value` | `int` | Full flags value |
| `hex` | `string` | Hex representation |
| `flags` | `FlagEntry[]` | Decomposed `ff_*` flags (excluding rating bits) |
| `max_player_rating` | `object` | Present only if bits 8-15 encode a rating |

### Relation

```json
{
  "faction_id": "kingdom_2",
  "faction_name": "Kingdom of Vaegirs",
  "relation_value": -0.05,
  "relation_description": "Neutral (-0.1 to 0.1)"
}
```

Relation descriptions:

| Range | Description |
|---|---|
| >= 0.9 | Allied |
| 0.5 to 0.9 | Friendly |
| 0.1 to 0.5 | Positive |
| -0.1 to 0.1 | Neutral |
| -0.5 to -0.1 | Unfriendly |
| -0.9 to -0.5 | Hostile |
| < -0.9 | Enemy |

### Rank

```json
{
  "index": 0,
  "name": "Serf"
}
```

### Color

```json
{
  "hex": "0xCE3519",
  "rgb": [206, 53, 25],
  "html": "#CE3519"
}
```
