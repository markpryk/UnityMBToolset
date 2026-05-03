# MSJsonConverters - Documentation

Python 2.7 converter scripts that transform Mount & Blade: Warband Module System data into structured JSON.

All converters can be run individually from the Module System directory, or via the standalone CLI tool.

## Standalone EXE (Recommended)

A standalone `ms_converter.exe` (~4 MB) bundles all 20 converters. No Python installation required.

```bash
# Convert all modules at once
ms_converter.exe --all --source "C:\path\to\Module_system"

# Convert a single module
ms_converter.exe --converter meshes --source "C:\path\to\Module_system"

# JSON output for programmatic use (C#, PowerShell, etc.)
ms_converter.exe --converter items --source "path" --json --quiet

# Convert using a custom file name
ms_converter.exe --converter skills --source "C:\path\to\Module_system" --filename my_custom_skills.py
```

Full CLI reference: [cli_api.md](cli_api.md)

## Converters

| Script | Module Source | Output File | Docs |
|---|---|---|---|
| `convert_troops.py` | `module_troops.py` | `troops_full.json` | [troops.md](troops.md) |
| `convert_items.py` | `module_items.py` | `items_full.json` | [items.md](items.md) |
| `convert_scene_props.py` | `module_scene_props.py` | `scene_props_full.json` | [scene_props.md](scene_props.md) |
| `convert_factions.py` | `module_factions.py` | `factions_full.json` | [factions.md](factions.md) |
| `convert_scenes.py` | `module_scenes.py` | `scenes_full.json` | [scenes.md](scenes.md) |
| `convert_parties.py` | `module_parties.py` | `parties_full.json` | [parties.md](parties.md) |
| `convert_party_templates.py` | `module_party_templates.py` | `party_templates_full.json` | [party_templates.md](party_templates.md) |
| `convert_map_icons.py` | `module_map_icons.py` | `map_icons_full.json` | [map_icons.md](map_icons.md) |
| `convert_particle_systems.py` | `module_particle_systems.py` | `particle_systems_full.json` | [particle_systems.md](particle_systems.md) |
| `convert_flora.py` | `Flora_kinds.py` | `flora_full.json` | [flora.md](flora.md) |
| `convert_skins.py` | `module_skins.py` | `skins_full.json` | [skins.md](skins.md) |
| `convert_ground_specs.py` | `Ground_specs.py` | `ground_specs_full.json` | [ground_specs.md](ground_specs.md) |
| `convert_skyboxes.py` | `Skyboxes.py` | `skyboxes_full.json` | [skyboxes.md](skyboxes.md) |
| `convert_meshes.py` | `module_meshes.py` | `meshes_full.json` | [meshes.md](meshes.md) |
| `convert_animations.py` | `module_animations.py` | `animations_full.json` | [animations.md](animations.md) |
| `convert_music.py` | `module_music.py` | `music_full.json` | [music.md](music.md) |
| `convert_postfx.py` | `module_postfx.py` | `postfx_full.json` | [postfx.md](postfx.md) |
| `convert_skills.py` | `module_skills.py` | `skills_full.json` | [skills.md](skills.md) |
| `convert_sounds.py` | `module_sounds.py` | `sounds_full.json` | [sounds.md](sounds.md) |
| `convert_strings.py` | `module_strings.py` | `strings_full.json` | [strings.md](strings.md) |

## Usage (Individual Scripts)

1. Copy the desired `convert_*.py` script into the appropriate directory:
   - **Module System converters** (troops, items, scenes, etc.) go in the `Module_system` directory.
   - **Module Data converters** (ground_specs, skyboxes, flora) go in the `Module_data` directory.
2. Run with Python 2.7:
   ```
   python convert_troops.py
   ```
3. The output JSON file is written to the current directory and copied to `converted/`.

## Architecture

All converters follow a shared pattern:

1. **Reverse Lookups** - Introspect `globals()` to build `value -> name` dictionaries for constants.
2. **Flag Decomposition** - Bitwise AND against known flag values to produce named constant lists.
3. **Field Parsing** - Walk each module tuple positionally, converting each field into a structured dict.
4. **JSON Export** - Serialize with `json.dumps(indent=2, ensure_ascii=False)` through `io.open()` for Unicode safety.

Some converters additionally extract raw Python source code (triggers, stats expressions) via regex to preserve symbolic names that are lost after evaluation.

## Common JSON Patterns

### Flags Object

Most flag fields share this shape:

```json
{
  "raw_flags_value": 1234,
  "raw_flags_hex": "0x4d2",
  "property_bits_only_hex": "0x400",
  "decomposed": [
    {
      "name": "flag_constant_name",
      "value": 1024,
      "hex": "0x400"
    }
  ]
}
```

### Lookup Resolution

Faction, troop, item, and scene references are resolved to symbolic names:

```json
{
  "value": 5,
  "name": "fac_kingdom_1"
}
```
