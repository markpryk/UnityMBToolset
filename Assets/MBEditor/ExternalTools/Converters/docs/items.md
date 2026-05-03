# Items - JSON Output Structure

**Script:** `convert_items.py`
**Source:** `module_items.py`
**Output:** `items_full.json`

## Overview

Converts the `items` list into a JSON array. Decomposes item type, property flags, capabilities, and modifier bits. Stats expressions and trigger code are extracted from the Python source file via regex to preserve symbolic function calls like `weight(5)|spd_rtng(100)`.

## Root Structure

```
items_full.json -> Array of Item objects
```

## Item Object

| Field | Type | Source Field | Description |
|---|---|---|---|
| `id` | `string` | `[0]` | Item string ID (e.g. `"sword_medieval_a"`) |
| `name` | `string` | `[1]` | Display name |
| `meshes` | `Mesh[]` | `[2]` | List of mesh entries |
| `item_type` | `ItemType` | `[3] & 0xFF` | Extracted item type (bits 0-7) |
| `property_flags` | `PropertyFlags` | `[3] & ~0xFF` | Bitwise property flags (bits 8+) |
| `capabilities` | `Capabilities` | `[4]` | Item capability flags (`itcf_*`) |
| `value` | `int` | `[5]` | Base item value in denars |
| `stats` | `Stats` | `[6]` | Parsed stats with source expressions |
| `modifier_bits` | `ModifierBits` | `[7]` | Allowed item modifiers (`imodbit_*`) |
| `triggers` | `string\|null` | `[8]` | Raw trigger Python code, or null |
| `factions` | `FactionRef[]` | `[9]` | Factions that can use this item |

## Nested Objects

### Mesh

```json
{
  "mesh_name": "sword_medieval_a",
  "usage_type_name": "ixmesh_inventory",
  "usage_type": "1048576",
  "modifier": "0"
}
```

| Field | Type | Description |
|---|---|---|
| `mesh_name` | `string` | Mesh resource name |
| `usage_type_name` | `string` | `ixmesh_*` constant name, or `"imodbit_plain"` |
| `usage_type` | `string` | Numeric value of usage type |
| `modifier` | `string` | Remaining modifier bits after removing ixmesh |

### ItemType

```json
{
  "value": 4,
  "hex": "0x4",
  "name": "itp_type_one_handed_wpn"
}
```

### PropertyFlags

```json
{
  "raw_flags_value": 268435460,
  "raw_flags_hex": "0x10000004",
  "property_bits_only_hex": "0x10000000",
  "decomposed": [
    {
      "name": "itp_merchandise",
      "value": 268435456,
      "hex": "0x10000000"
    }
  ]
}
```

### Capabilities

```json
{
  "raw_value": "11258999068426240",
  "hex": "0x28000000000000",
  "decomposed": [
    {
      "name": "itcf_carry_sword_left_hip",
      "value": 11258999068426240,
      "hex": "0x28000000000000"
    }
  ]
}
```

### Stats

```json
{
  "raw_expression": "83886180",
  "functions": "weight(1.5)|difficulty(0)|spd_rtng(100)|weapon_length(95)|swing_damage(29,cut)|thrust_damage(20,pierce)"
}
```

| Field | Type | Description |
|---|---|---|
| `raw_expression` | `string` | Evaluated numeric value from Python |
| `functions` | `string\|null` | Symbolic expression extracted from source, or null |

### ModifierBits

```json
{
  "raw_value": "1073741824",
  "hex": "0x40000000",
  "decomposed": [
    {
      "name": "imodbit_rusty",
      "value": 1073741824,
      "hex": "0x40000000"
    }
  ]
}
```

### FactionRef

```json
{
  "faction_id": 5,
  "faction_name": "fac_kingdom_1"
}
```
