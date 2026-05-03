# Party Templates - JSON Output Structure

**Script:** `convert_party_templates.py`
**Source:** `module_party_templates.py`
**Output:** `party_templates_full.json`

## Overview

Converts the `party_templates` list into JSON. Similar to parties but with min/max troop ranges instead of fixed counts. Resolves personality presets and calculates total troop range summaries.

## Root Structure

```
party_templates_full.json -> Array of PartyTemplate objects
```

## PartyTemplate Object

| Field | Type | Source Field | Description |
|---|---|---|---|
| `id` | `string` | `[0]` | Template string ID |
| `name` | `string` | `[1]` | Display name |
| `icon` | `Icon` | `[2] & 0xFF` | Map icon (bits 0-7) |
| `flags` | `Flags` | `[2] & ~0xFF` | Party flags (bits 8+) |
| `menu` | `Menu` | `[3]` | Associated game menu |
| `faction` | `FactionRef` | `[4]` | Faction reference |
| `personality` | `Personality` | `[5]` | With optional `preset` field |
| `stacks` | `TemplateStack[]` | `[6]` | Troop stacks with min/max ranges |
| `num_stacks` | `int` | computed | Number of troop stacks |
| `total_troops` | `TotalTroops` | computed | Aggregated min/max totals |

## Key Nested Objects

### TemplateStack

```json
{
  "troop_id": 42,
  "troop_name": "trp_swadian_infantry",
  "min_count": 5,
  "max_count": 20,
  "range": "5-20",
  "member_flags": { "raw_value": 0, "hex": "0x0" },
  "is_prisoner": false
}
```

### TotalTroops

```json
{ "min": 15, "max": 60, "range": "15-60" }
```

### Personality (with preset)

If the value matches a known preset (`soldier_personality`, `merchant_personality`, `bandit_personality`, `escorted_merchant_personality`), a `preset` field is added alongside the standard courage/aggressiveness decomposition.

Other nested objects (Icon, Flags, Menu, FactionRef) follow the same structure as [parties.md](parties.md).
