# Troops - JSON Output Structure

**Script:** `convert_troops.py`
**Source:** `module_troops.py`
**Output:** `troops_full.json`

## Overview

Converts the `troops` list from the Module System into a flat JSON array. Each troop entry is a dict with parsed attributes, weapon proficiencies, inventory, upgrade paths, and face codes.

Upgrade paths are extracted separately by regex-parsing `upgrade()` and `upgrade2()` calls from the source file.

## Root Structure

```
troops_full.json -> Array of Troop objects
```

## Troop Object

| Field | Type | Source Field | Description |
|---|---|---|---|
| `id` | `string` | `[0]` | Troop string ID (e.g. `"knight_1_1"`) |
| `name` | `string` | `[1]` | Display name |
| `name_plural` | `string` | `[2]` | Plural display name |
| `flags` | `Flags` | `[3]` | Troop flags (`tf_*` constants) |
| `scene` | `string` | `[4]` | Resolved scene name (e.g. `"scn_town_1_castle"`) |
| `entry_point` | `int` | `[4]` | Entry point index, or `-1` if none |
| `scene_raw_value` | `int` | `[4]` | Raw scene+entry encoded value |
| `reserved` | `int` | `[5]` | Reserved field |
| `faction` | `Faction` | `[6]` | Faction reference |
| `inventory` | `string[]` | `[7]` | Resolved item names (e.g. `"itm_sword_medieval_a"`) |
| `attributes` | `Attributes` | `[8]` | Parsed STR/AGI/INT/CHA/Level |
| `proficiencies` | `Proficiencies` | `[9]` | Parsed weapon proficiency levels |
| `skills` | `Skills` | `[10]` | Skill values |
| `face_code_1` | `string` | `[11]` | Hex face code (48-char, e.g. `"0x0000..."`) |
| `face_code_2` | `string` | `[12]` | Hex face code (upper range) |
| `troop_image` | `string` | `[13]` | Optional troop image mesh name |
| `upgrades` | `string[]` | parsed | Upgrade target troop IDs (e.g. `["knight_1_2", "knight_1_3"]`) |

## Nested Objects

### Flags

```json
{
  "raw_value": "144115188075856000",
  "decomposed": ["tf_hero", "tf_is_merchant"],
  "symbolic": "tf_hero|tf_is_merchant"
}
```

| Field | Type | Description |
|---|---|---|
| `raw_value` | `string` | Raw numeric value as string (can be very large) |
| `decomposed` | `string[]` | List of active `tf_*` flag names |
| `symbolic` | `string` | Pipe-joined flag names, or `"0"` |

### Faction

```json
{
  "raw_value": 5,
  "symbolic": "fac_kingdom_1"
}
```

### Attributes

```json
{
  "parsed": {
    "str": 6,
    "agi": 6,
    "int": 4,
    "cha": 5,
    "level": 14,
    "raw_value": "60129542150"
  },
  "symbolic": "str_6|agi_6|int_4|cha_5|level(14)"
}
```

Attributes are bit-packed:
- Bits 0-7: STR
- Bits 8-15: AGI
- Bits 16-23: INT
- Bits 24-31: CHA
- Bits 32-39: Level (if value > 0xFFFFFFFF)

### Proficiencies

```json
{
  "parsed": {
    "one_handed": 55,
    "two_handed": 90,
    "polearm": 80,
    "archery": 0,
    "crossbow": 0,
    "throwing": 0,
    "firearm": 0,
    "raw_value": 83907639
  },
  "symbolic": "wp_one_handed(55)|wp_two_handed(90)|wp_polearm(80)"
}
```

Each proficiency occupies 10 bits (0-1023 range):
- Bits 0-9: One-handed
- Bits 10-19: Two-handed
- Bits 20-29: Polearm
- Bits 30-39: Archery
- Bits 40-49: Crossbow
- Bits 50-59: Throwing
- Bits 60-69: Firearm

### Skills

```json
{
  "raw_value": "1125899906842624",
  "symbolic": "0x4000000000000"
}
```

Skills are stored as a raw hex value. Full decomposition is not implemented.
