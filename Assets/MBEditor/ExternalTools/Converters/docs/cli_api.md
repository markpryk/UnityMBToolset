# ms_converter CLI API Reference

**Executable:** `ms_converter.exe` (standalone, no Python required)
**Size:** ~4 MB
**Location:** `dist/ms_converter.exe`

## Overview

A standalone command-line tool that converts Mount & Blade: Warband Module System `.py` files to structured JSON. Bundles all 20 converters into a single executable with an embedded Python interpreter.

The tool copies converter scripts temporarily into your Module System directory, runs them, and cleans up after itself. Original module files are never modified.

---

## Quick Start

```bash
# List all available converters
ms_converter.exe --list

# Convert a single module
ms_converter.exe --converter meshes --source "C:\path\to\Module_system"

# Convert everything
ms_converter.exe --all --source "C:\path\to\Module_system"

# Get JSON output for programmatic use
ms_converter.exe --converter skills --source "C:\path\to\Module_system" --json --quiet
```

---

## Commands

### `--list` / `-l`

List all available converters.

```bash
ms_converter.exe --list
ms_converter.exe --list --json    # JSON array of converter names
```

### `--converter <name>` / `-c <name>`

Run a single converter.

```bash
ms_converter.exe --converter meshes --source "path/to/Module_system"
```

### `--all` / `-a`

Run all 20 converters.

```bash
ms_converter.exe --all --source "path/to/Module_system"
```

---

## Options

| Option | Short | Description |
|---|---|---|
| `--source <dir>` | `-s` | **(Required)** Path to Module System directory containing `.py` source files |
| `--output <dir>` | `-o` | Output directory for JSON files. Default: `converted/` next to the exe |
| `--filename <name>`| `-f` | Optional: specify a custom `.py` filename to use instead of the hardcoded default (e.g. `my_skills.py`) |
| `--json` | | Output results as machine-readable JSON to stdout |
| `--quiet` | `-q` | Suppress progress output, only show final results |

---

## Available Converters

| Name | Source File | Output JSON |
|---|---|---|
| `animations` | `module_animations.py` | `animations_full.json` |
| `factions` | `module_factions.py` | `factions_full.json` |
| `flora` | `module_flora_kinds.py` | `flora_full.json` |
| `ground_specs` | `Ground_specs.py` | `ground_specs_full.json` |
| `items` | `module_items.py` | `items_full.json` |
| `map_icons` | `module_map_icons.py` | `map_icons_full.json` |
| `meshes` | `module_meshes.py` | `meshes_full.json` |
| `music` | `module_music.py` | `music_full.json` |
| `particle_systems` | `module_particle_systems.py` | `particle_systems_full.json` |
| `parties` | `module_parties.py` | `parties_full.json` |
| `party_templates` | `module_party_templates.py` | `party_templates_full.json` |
| `postfx` | `module_postfx.py` | `postfx_full.json` |
| `scene_props` | `module_scene_props.py` | `scene_props_full.json` |
| `scenes` | `module_scenes.py` | `scenes_full.json` |
| `skills` | `module_skills.py` | `skills_full.json` |
| `skins` | `module_skins.py` | `skins_full.json` |
| `skyboxes` | `Skyboxes.py` | `skyboxes_full.json` |
| `sounds` | `module_sounds.py` | `sounds_full.json` |
| `strings` | `module_strings.py` | `strings_full.json` |
| `troops` | `module_troops.py` | `troops_full.json` |

---

## JSON API Output

When using `--json`, the tool outputs a structured result object to stdout:

```json
{
  "source_dir": "C:\\path\\to\\Module_system",
  "output_dir": "C:\\path\\to\\converted",
  "total": 1,
  "success": 1,
  "skipped": 0,
  "errors": 0,
  "results": [
    {
      "converter": "skills",
      "status": "success",
      "source_module": "module_skills.py",
      "output_file": "skills_full.json",
      "output_path": "C:\\path\\to\\converted\\skills_full.json",
      "count": 42,
      "elapsed_seconds": 0.03
    }
  ]
}
```

### Result Status Values

| Status | Description |
|---|---|
| `success` | Converter ran successfully, JSON file created |
| `skipped` | Source `.py` file not found in the source directory |
| `error` | Converter failed (see `error` and `traceback` fields) |

### Error Result Fields

On error, additional fields are present:

```json
{
  "converter": "skins",
  "status": "error",
  "error": "description of the error",
  "traceback": "full Python traceback"
}
```

---

## Exit Codes

| Code | Meaning |
|---|---|
| `0` | All requested converters succeeded (skipped is OK) |
| `1` | One or more converters failed |

---

## Integration Examples

### Batch Script

```batch
@echo off
set SOURCE=C:\Games\MountBlade\Modules\Native\Module_system
set OUTPUT=C:\output\json

ms_converter.exe --all --source "%SOURCE%" --output "%OUTPUT%"
if %ERRORLEVEL% NEQ 0 echo Some converters failed!
```

### PowerShell

```powershell
$result = & .\ms_converter.exe --all --source $sourcePath --json --quiet | ConvertFrom-Json
$result.results | Where-Object { $_.status -eq 'success' } | ForEach-Object {
    Write-Host "$($_.converter): $($_.count) entries"
}
```

### C# / Unity Integration

```csharp
var process = new System.Diagnostics.Process();
process.StartInfo.FileName = "ms_converter.exe";
process.StartInfo.Arguments = "--converter items --source \"" + moduleDir + "\" --json --quiet";
process.StartInfo.RedirectStandardOutput = true;
process.StartInfo.UseShellExecute = false;
process.Start();

string json = process.StandardOutput.ReadToEnd();
process.WaitForExit();

var result = JsonUtility.FromJson<ConverterResult>(json);
```

---

## Building from Source

Requires Python 2.7 with pip and PyInstaller 3.6:

```bash
# Install dependencies (one-time)
pip install pyinstaller==3.6

# Build
python build_exe.py
```

Output: `dist/ms_converter.exe`

## Notes

- The exe embeds a Python 2.7 interpreter and all converter scripts
- Source module files are never modified; the tool operates in read-only mode
- If your `.py` files have non-standard names (e.g. `my_skills.py` instead of `module_skills.py`), use the `--filename` flag to explicitly point the converter to your file.
- The `--output` directory is created automatically if it doesn't exist
