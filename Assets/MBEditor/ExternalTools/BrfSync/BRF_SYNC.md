# BRF Sync Tool — Documentation

A standalone command-line tool for converting Mount & Blade: Warband `.brf` resource files to/from an editable folder structure (JSON + OBJ/SMD). Designed for integration with the Unity MBToolset synchronizer pipeline.

---

## Overview

```
┌──────────────┐     export      ┌──────────────────────────────┐
│  module.brf  │ ──────────────► │  output_folder/              │
│  (binary)    │                 │    data.json                 │
└──────────────┘                 │    Meshes/*.obj              │
       ▲                         │    Skeletons/*.smd           │
       │         import          │    Animations/*.smd          │
       └──────────────────────── │    Collisions/*.obj          │
                                 └──────────────────────────────┘
```

The tool reads M&B's proprietary BRF binary format and produces:
- **`data.json`** — Metadata manifest (mesh names, materials, textures, flags, collision primitives)
- **`Meshes/*.obj`** — 3D mesh geometry in Wavefront OBJ format
- **`Skeletons/*.smd`** — Skeleton rigs in Valve SMD format
- **`Animations/*.smd`** — Skeletal animations in Valve SMD format
- **`Collisions/*.obj`** — Collision body geometry (manifold shapes)

---

## Commands

### `export` — Full Export

```bash
brf_sync export <source.brf> <dest_folder>
```

Loads the BRF file and writes everything: `data.json` + all OBJ/SMD files.

**Use when:** You need the actual mesh/animation files (e.g., importing into Unity for the first time, or after detecting changes that require new geometry).

**Example:**
```bash
brf_sync.exe export "C:\Modules\Native\Resource\CommonRes\armor_a.brf" "C:\Temp\armor_a"
```

**Output structure:**
```
armor_a/
├── data.json
├── Meshes/
│   ├── mail_shirt.obj
│   ├── mail_mittens.obj
│   └── ...
├── Skeletons/
│   └── skel_human.smd
├── Animations/
│   ├── combat_stand.smd
│   └── ...
└── Collisions/
    └── bo_mail_shirt.obj
```

---

### `import` — Import Back to BRF

```bash
brf_sync import <source_folder> <dest.brf>
```

Reads a previously exported folder (the `data.json` + OBJ/SMD files) and writes a new BRF binary.

**Use when:** You've modified meshes externally (e.g., in Blender) and want to pack them back into a BRF.

**Example:**
```bash
brf_sync.exe import "C:\Temp\armor_a" "C:\Modules\MyMod\Resource\armor_a.brf"
```

---

### `info` — Metadata-Only Scan (Fast)

```bash
brf_sync info <source.brf> <dest_folder>
```

Loads the BRF and writes **only `data.json`** — no OBJ/SMD files are generated. This is dramatically faster because it skips all geometry export.

**Use when:** You only need to know *what's inside* the BRF (names, materials, flags) without needing the actual geometry. This is the primary command used by the Unity synchronizer for change detection scans.

**Performance comparison:**
| Command | Time (typical) | Disk output |
|---------|---------------|-------------|
| `export` | 200–2000ms | data.json + all OBJ/SMD files |
| `info` | **10–30ms** | data.json only |

**Example:**
```bash
brf_sync.exe info "C:\Modules\Native\Resource\CommonRes\armor_a.brf" "C:\Temp\armor_a_info"
```

---

## data.json Schema

The JSON manifest is consumed by Unity's `BrfDataModels.cs` deserializer. Here is the complete schema:

```jsonc
{
    "version": "1.0",

    "meshes": [
        {
            "name": "mail_shirt",           // BRF mesh name (matches M&B module system references)
            "material": "mail_a",           // Material name reference
            "flags": 0,                     // BRF mesh flags bitmask
            "lod_level": 0,                 // LOD level (0 = base, 1+ = lower detail)
            "source": "Meshes/mail_shirt.obj"  // Relative path to exported OBJ (only with 'export')
        }
    ],

    "materials": [
        {
            "name": "mail_a",
            "flags": 0,                    // MaterialFlags bitmask (render order, blend mode, etc.)
            "shader": "shdr_armor",         // Shader name reference
            "diffuseA": "mail_a_d",         // Primary diffuse texture name
            "diffuseB": "",                 // Secondary diffuse texture
            "bump": "mail_a_nm",            // Normal map texture name
            "enviro": "",                   // Environment/reflection map
            "spec": "mail_a_sp",            // Specular map texture name
            "color_rgb": [0.8, 0.8, 0.8],  // Specular color multiplier [R, G, B]
            "specular_value": 45.0          // Specular intensity coefficient
        }
    ],

    "textures": [
        {
            "name": "mail_a_d",
            "flags": 0                      // TextureFlags (animation frames, clamp, HDR, etc.)
        }
    ],

    "shaders": [
        {
            "name": "shdr_armor",
            "technique": "armor_shading",
            "fallback": "simple_shader",
            "flags": 0,
            "requirements": 0,
            "options": [
                {
                    "map": 0,
                    "colorOp": 0,
                    "alphaOp": 0,
                    "flags": 0
                }
            ]
        }
    ],

    "skeletons": [
        {
            "name": "skel_human",
            "flags": 0,
            "source": "Skeletons/skel_human.smd"
        }
    ],

    "animations": [
        {
            "name": "combat_stand",
            "source": "Animations/combat_stand.smd"
        }
    ],

    "bodies": [
        {
            "name": "bo_mail_shirt",
            "source": "Collisions/bo_mail_shirt.obj",  // Only present for manifold (mesh) colliders
            "primitives": [
                {
                    "type": "sphere",       // "sphere" | "capsule" | "manifold" | "face"
                    "flags": 0,
                    "radius": 0.15,
                    "center": { "x": 0.0, "y": 0.5, "z": 0.0 }
                },
                {
                    "type": "capsule",
                    "flags": 0,
                    "radius": 0.1,
                    "p1": { "x": 0.0, "y": 0.3, "z": 0.0 },
                    "p2": { "x": 0.0, "y": 0.8, "z": 0.0 }
                },
                {
                    "type": "manifold",
                    "flags": 0,
                    "orientation": 1,
                    "vertices": [
                        { "x": 0.0, "y": 0.0, "z": 0.0 },
                        { "x": 1.0, "y": 0.0, "z": 0.0 }
                    ],
                    "faces": [ [0, 1, 2], [2, 3, 0] ]
                }
            ]
        }
    ]
}
```

---

## Unity Integration

The tool is invoked by the Unity MBToolset via `BRFSyncHandler.cs` using `System.Diagnostics.Process`. The typical workflow is:

### Scan Workflow (Change Detection)
```
1. Unity calls:  brf_sync info <module.brf> <temp_folder>
2. Reads the produced data.json
3. Compares against the previously stored data.json (BrfDiffEngine.cs)
4. Shows Added/Modified/Removed entries in BRFSynchronizerWindow
```

### Sync Workflow (Apply Changes)
```
1. Unity calls:  brf_sync export <module.brf> <temp_folder>
2. Reads data.json + copies relevant OBJ/SMD files
3. BrfDataImporter.cs creates Unity materials from material entries
4. OBJ meshes are imported as Unity Mesh assets
5. MBPrefabsGenerator.cs creates/updates prefabs
```

### File Locations (Unity Project)
| Unity File | Role |
|---|---|
| `BRFSyncHandler.cs` | Process launcher — invokes brf_sync.exe |
| `BRFSynchronizerWindow.cs` | Editor UI — shows diff results, triggers sync |
| `BrfSyncExecutor.cs` | Applies changes — copies meshes, updates materials, generates prefabs |
| `BrfDiffEngine.cs` | Compares two data.json files to detect changes |
| `BrfDataModels.cs` | C# data classes matching the JSON schema above |
| `BrfDataImporter.cs` | Creates Unity materials/colliders from JSON data |

---

## Building

### Prerequisites
- **CMake** (3.16+) — bundled with Visual Studio 2022
- **Visual Studio 2022** (or 2019) with C++ desktop workload
- **No Qt SDK required**

### Build Steps

**Option A: One-click script**
```bash
build_sync_standalone.bat
```

**Option B: Manual CMake**
```bash
cmake -B build_standalone -G "Visual Studio 17 2022" -A x64
cmake --build build_standalone --config Release
```

The output is `build_standalone/Release/brf_sync.exe` — a single 338 KB executable with zero external DLL dependencies.

### Build Architecture

```
CMakeLists.txt
│
├── Core BRF Logic (Qt-free, unchanged from OpenBRF)
│   ├── brfMesh.cpp        — Mesh data structures + OBJ export
│   ├── brfData.cpp        — BRF container (load/save binary)
│   ├── brfMaterial.cpp    — Material properties
│   ├── brfBody.cpp        — Collision bodies
│   ├── brfSkeleton.cpp    — Skeleton rigs
│   ├── brfAnimation.cpp   — Skeletal animations
│   ├── saveLoad.cpp       — Binary BRF read/write
│   ├── ioSMD.cpp          — SMD file I/O (already Qt-free)
│   └── ioMD3.cpp          — MD3 file I/O (already Qt-free)
│
├── Sync-Specific (Qt-free replacements)
│   ├── main_sync_standalone.cpp   — CLI entry point (replaces main_sync.cpp)
│   ├── brfJson_sync.cpp/h         — JSON serialization via nlohmann/json
│   ├── ioOBJ_sync.cpp             — OBJ I/O via FILE*/STL (replaces ioOBJ.cpp)
│   ├── vcgmesh_stub.cpp           — Minimal stubs (replaces vcgmesh.cpp)
│   └── brfHitBox_stub.cpp         — Stub for XML hitbox functions
│
└── Dependencies (header-only)
    ├── dependencies/vcglib/       — VCG mesh library (from OpenBRF)
    └── dependencies/nlohmann/     — nlohmann/json single-header library
```

### Excluded from Standalone Build
| File | Reason |
|---|---|
| `ioMB.cpp` | Maya Binary format — uses `QString`, `QFile`, `QDebug` |
| `iniData.cpp` | Module.ini parser — uses `QtCore`, `QTextBrowser` |
| `ioOBJ.cpp` | Replaced by `ioOBJ_sync.cpp` |
| `brfJson.cpp` | Replaced by `brfJson_sync.cpp` |
| `main_sync.cpp` | Replaced by `main_sync_standalone.cpp` |
| `brfHitBox.cpp` | XML hitbox parsing — uses `QDomDocument` |
| `vcgmesh.cpp` | Full VCG mesh processing — uses `QOpenGLWidget` |

All `#ifdef` guards use `BRF_NO_QT` (for Qt includes) and `BRF_NO_GUI` (for OpenGL, inherited from OpenBRF). The OpenBRF GUI build is completely unaffected.

---

## Version History

| Version | Changes |
|---|---|
| **2.0** | Qt-free standalone build. Added `info` command. CMake build system. |
| **1.0** | Original Qt-dependent version (via `sync.pro` / qmake). `export` and `import` commands only. |
