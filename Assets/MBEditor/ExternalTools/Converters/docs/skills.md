# Skills - JSON Output Structure

**Script:** `convert_skills.py`
**Source:** `module_skills.py`
**Output:** `skills_full.json`

## Overview

Converts the `skills` list into JSON. Each skill is a 5-field tuple with an ID, display name, flags (base attribute + modifiers), max level, and description. Skills are hardcoded by index; reserved slots can be repurposed for new skills.

This runs from the Module System directory.

## Root Structure

```
skills_full.json -> Array of Skill objects
```

## Skill Object

| Field | Type | Source | Description |
|---|---|---|---|
| `id` | `string` | `[0]` | Skill ID (e.g. `"trade"`, `"riding"`) |
| `index` | `int` | positional | Zero-based hardcoded skill index |
| `name` | `string` | `[1]` | Display name (e.g. `"Trade"`, `"Riding"`) |
| `flags` | `SkillFlags` | `[2]` | Base attribute and modifiers |
| `max_level` | `int` | `[3]` | Maximum skill level (typically 10) |
| `description` | `string` | `[4]` | In-game description text |
| `is_reserved` | `bool` | derived | `true` if ID starts with `"reserved"` |
| `skill_type` | `string` | derived | `"party"`, `"personal"`, or `"inactive"` |

## SkillFlags Object

```json
{
  "raw_value": 19,
  "hex": "0x13",
  "symbolic": "sf_base_att_cha|sf_effects_party",
  "base_attribute": {
    "name": "Charisma",
    "flag": "sf_base_att_cha",
    "value": 3
  },
  "modifiers": [...],
  "is_party_skill": true,
  "is_inactive": false
}
```

### Base Attribute (bits 0-3, encoded)

| Flag | Value | Attribute |
|---|---|---|
| `sf_base_att_str` | `0x000` | Strength |
| `sf_base_att_agi` | `0x001` | Agility |
| `sf_base_att_int` | `0x002` | Intelligence |
| `sf_base_att_cha` | `0x003` | Charisma |

### Modifier Flags (bitwise)

| Flag | Value | Description |
|---|---|---|
| `sf_effects_party` | `0x010` | Party skill (best value in party applies) |
| `sf_inactive` | `0x100` | Skill is disabled / reserved |

## Skill Types

Derived from flags for convenience:

| Type | Condition | Description |
|---|---|---|
| `party` | `sf_effects_party` set, not inactive | Best level across party members used |
| `personal` | No `sf_effects_party`, not inactive | Only the individual's level matters |
| `inactive` | `sf_inactive` set | Disabled, reserved for modders |

## Notes

- **Hardcoded indices**: Skill positions are engine-hardcoded. Adding new skills requires using reserved slots (`reserved_1` through `reserved_18`).
- **Attribute dependency**: Each skill point requires 1 point per 3 levels in the associated base attribute (e.g. Riding level 6 needs 6 Agility).
- **42 total slots**: 24 active skills + 18 reserved/inactive slots.
- **Description format strings**: Some descriptions use `%%` for literal percent signs (Python 2.7 string formatting artifact).
