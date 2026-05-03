# Parties - JSON Output Structure

**Script:** `convert_parties.py`
**Source:** `module_parties.py`
**Output:** `parties_full.json`

## Overview

Converts the `parties` list into JSON. Extracts map icon from bits 0-7 of the flags field, decomposes party flags, resolves faction/troop/template references, and parses personality into courage, aggressiveness, and banditness components.

## Root Structure

```
parties_full.json -> Array of Party objects
```

## Party Object

| Field | Type | Source Field | Description |
|---|---|---|---|
| `id` | `string` | `[0]` | Party string ID (e.g. `"town_1"`) |
| `name` | `string` | `[1]` | Display name |
| `icon` | `Icon` | `[2] & 0xFF` | Map icon (bits 0-7) |
| `flags` | `Flags` | `[2] & ~0xFF` | Party flags (bits 8+) |
| `menu` | `Menu` | `[3]` | Associated game menu |
| `party_template` | `TemplateRef` | `[4]` | Party template reference |
| `faction` | `FactionRef` | `[5]` | Faction reference |
| `personality` | `Personality` | `[6]` | Courage + aggressiveness + banditness |
| `ai_behavior` | `AIBehavior` | `[7]` | Initial AI behavior |
| `ai_target` | `int` | `[8]` | AI target party index |
| `coordinates` | `Coordinates` | `[9]` | Map position (x, y) |
| `stacks` | `Stack[]` | `[10]` | Troop stacks |
| `direction` | `float` | `[11]` | Initial facing direction (optional) |

## Nested Objects

### Icon

```json
{
  "value": 2,
  "hex": "0x2",
  "name": "icon_town"
}
```

### Flags

```json
{
  "raw_flags_value": "83886082",
  "raw_flags_hex": "0x5000002",
  "flag_bits_only_hex": "0x5000000",
  "decomposed": [
    {
      "name": "pf_town",
      "value": 67108864,
      "hex": "0x4000000"
    },
    {
      "name": "pf_label_medium",
      "value": 16777216,
      "hex": "0x1000000"
    }
  ]
}
```

### Personality

```json
{
  "raw_value": 152,
  "hex": "0x98",
  "courage": {
    "value": 8,
    "hex": "0x8",
    "name": "courage_8",
    "description": "neutral"
  },
  "aggressiveness": {
    "value": 144,
    "hex": "0x90",
    "name": "aggressiveness_9",
    "level": 9
  },
  "banditness": {
    "value": 256,
    "hex": "0x100",
    "name": "banditness"
  }
}
```

Personality encoding:
- Bits 0-3: Courage (0-15, 8 = neutral)
- Bits 4-7: Aggressiveness (0-15)
- Bit 8: Banditness flag

### Stack

```json
{
  "troop_id": 42,
  "troop_name": "trp_swadian_infantry",
  "count": 10,
  "member_flags": {
    "raw_value": 0,
    "hex": "0x0"
  },
  "is_prisoner": false
}
```

| Field | Type | Description |
|---|---|---|
| `troop_id` | `int` | Numeric troop index |
| `troop_name` | `string` | Resolved `trp_*` constant name |
| `count` | `int` | Number of troops in stack |
| `member_flags` | `object` | Party member flags (`pmf_*`) |
| `is_prisoner` | `bool` | Quick check for `pmf_is_prisoner` |

### Coordinates

```json
{
  "x": -18.7,
  "y": 31.4
}
```

### Menu / TemplateRef / FactionRef / AIBehavior

All follow the standard `{value, name}` lookup pattern:

```json
{
  "value": 5,
  "name": "fac_kingdom_1"
}
```
