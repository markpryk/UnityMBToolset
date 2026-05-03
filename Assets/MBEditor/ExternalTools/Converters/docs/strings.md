# Strings - JSON Output Structure

**Script:** `convert_strings.py`
**Source:** `module_strings.py`
**Output:** `strings_full.json`

## Overview

Converts the `strings` list into JSON. Each string is a 2-field tuple with an ID and a localized text value. The converter detects non-translatable markers (`{!}`) and extracts M&B variable references (`{s0}`, `{reg1}`, etc.) for downstream tooling.

The source file uses `cp1254` (Turkish Windows) encoding. The converter handles this transparently.

This runs from the Module System directory.

## Root Structure

```
strings_full.json -> Array of String objects
```

## String Object

| Field | Type | Source | Description |
|---|---|---|---|
| `id` | `string` | `[0]` | String ID (e.g. `"msg_battle_won"`) |
| `index` | `int` | positional | Zero-based string index |
| `value` | `string` | `[1]` | Localized text value |
| `length` | `int` | derived | Character count |
| `no_translate` | `bool` | derived | `true` if prefixed with `{!}` |
| `references` | `string[]` | derived | M&B variable references found in text |

### Example

```json
{
  "id": "hero_taken_prisoner",
  "index": 159,
  "value": "{s1} of {s3} has been taken prisoner by {s2}.",
  "length": 45,
  "references": ["s1", "s3", "s2"]
}
```

## Variable References

M&B strings support runtime variable interpolation:

| Pattern | Type | Description |
|---|---|---|
| `{s0}`-`{s63}` | String register | Populated via `str_store_*` operations |
| `{reg0}`-`{reg63}` | Integer register | Populated via `assign`/`store_*` operations |
| `{!}` | Marker | Non-translatable prefix (stripped at display) |

## Notes

- **First 4 strings are hardcoded**: `no_string`, `empty_string`, `yes`, `no` must remain at indices 0-3.
- **`{!}` prefix**: Marks strings that should not be translated (UI identifiers, debug text, format-only strings). The converter sets `no_translate = true` for these.
- **cp1254 encoding**: The source file declares `# -*- coding: cp1254 -*-` for Turkish character support. The converter handles this with a cp1254 fallback in the Unicode conversion.
- **3,399 strings**: Includes UI labels, quest descriptions, dialog templates, credits, and tutorial text.
