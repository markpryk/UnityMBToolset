# Scene Props - JSON Output Structure

**Script:** `convert_scene_props.py`
**Source:** `module_scene_props.py`
**Output:** `scene_props_full.json`

## Overview

Converts the `scene_props` list into JSON. Extracts scene prop type, hit points, use time, and property flags from the packed flags value. Trigger code is extracted from the Python source and parsed into structured trigger entries with variable definition resolution.

## Root Structure

```
scene_props_full.json -> Array of SceneProp objects
```

## SceneProp Object

| Field | Type | Source Field | Description |
|---|---|---|---|
| `id` | `string` | `[0]` | Scene prop string ID (e.g. `"chest_a"`) |
| `scene_prop_type` | `ScenePropType` | `[1] & sokf_type_mask` | Prop type (container, barrier, etc.) |
| `hit_points` | `int` | `[1]` | Extracted hit points (only if > 0) |
| `use_time` | `int` | `[1]` | Extracted use time in seconds (only if > 0) |
| `property_flags` | `PropertyFlags` | `[1]` | Bitwise property flags (`sokf_*`) |
| `mesh_name` | `string` | `[2]` | Visual mesh resource name |
| `physics_name` | `string` | `[3]` | Physics/collision object name |
| `triggers` | `Triggers` | `[4]` | Trigger data with raw code |

## Nested Objects

### ScenePropType

```json
{
  "value": 7,
  "hex": "0x7",
  "name": "sokf_type_container"
}
```

Extracted from bits 0-7 using `sokf_type_mask`. Possible values include:
- `sokf_type_barrier`
- `sokf_type_ai_limiter`
- `sokf_type_player_limiter`
- `sokf_type_container`
- `sokf_type_ladder`

### PropertyFlags

```json
{
  "raw_flags_value": 570425351,
  "raw_flags_hex": "0x22000007",
  "property_bits_only_hex": "0x22000000",
  "decomposed": [
    {
      "name": "sokf_destructible",
      "value": 536870912,
      "hex": "0x20000000"
    },
    {
      "name": "sokf_show_hit_point_bar",
      "value": 33554432,
      "hex": "0x2000000"
    }
  ]
}
```

Hit points and use time bits are masked out before flag decomposition.

### Triggers

```json
{
  "raw_code": "[check_item_use_trigger, (ti_on_scene_prop_use, ...)]",
  "has_triggers": true,
  "trigger_count": 2,
  "parsed_triggers": [
    {
      "type": "variable_reference",
      "variable_name": "check_item_use_trigger",
      "raw_code": "check_item_use_trigger"
    },
    {
      "type": "inline_trigger",
      "trigger_type": "ti_on_scene_prop_use",
      "raw_code": "(ti_on_scene_prop_use, [...])"
    }
  ],
  "variable_definitions": {
    "check_item_use_trigger": "(ti_on_scene_prop_use, [...])"
  }
}
```

| Field | Type | Description |
|---|---|---|
| `raw_code` | `string` | Complete trigger list as Python source text |
| `has_triggers` | `bool` | Whether triggers exist |
| `trigger_count` | `int` | Number of trigger entries |
| `parsed_triggers` | `TriggerEntry[]` | Parsed trigger items |
| `variable_definitions` | `dict` | Resolved definitions for referenced trigger variables |

### TriggerEntry

Each parsed trigger is one of:

**Inline trigger** (defined directly):
```json
{
  "type": "inline_trigger",
  "trigger_type": "ti_on_scene_prop_hit",
  "raw_code": "(ti_on_scene_prop_hit, [...])"
}
```

**Variable reference** (defined elsewhere in the file):
```json
{
  "type": "variable_reference",
  "variable_name": "check_item_use_trigger",
  "raw_code": "check_item_use_trigger"
}
```
