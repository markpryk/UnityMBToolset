# Mount & Blade: Warband — RGL Terrain & Flora Engine Documentation

Reverse-engineered documentation of the RGL engine's terrain generation, ground specification, flora placement, particle systems, and rendering systems.

---

## Documentation

| Document | Description |
|----------|-------------|
| [**terrain.md**](terrain.md) | Terrain code system, generation pipeline, layer system, ground paint, SCO format, Perlin noise |
| [**ground-specs.md**](ground-specs.md) | All 11 ground specifications, module system setup, multitex blending, UV mapping |
| [**flora.md**](flora.md) | Flora kinds, flags, module system setup, colony generation, placement pipeline, grass streaming |
| [**shaders.md**](shaders.md) | Gamma pipeline, terrain/flora/grass shader techniques, billboard LOD, key constants |
| [**particles.md**](particles.md) | Particle system flags, keyframe animation, emission/simulation, module system setup |

---

## Quick Reference

### Generation Order

```
Seed 0 → Layers → Grid → Hills → Terrain mesh
Seed 1 → River
         Smoothing → Normals → Layer intensities → Face layers → Rock roughening
         Ground paint → Recompute normals → Face layer intensities
Seed 2 → Flora colonies → Flora placement (trees, rocks, grass)
         Entity creation → Shading → Grass streaming (runtime)
```

### Coordinate System

Z-up, XY horizontal. Cell size = `regionDetail + 2.0` meters (2–5m).

---

## Special Thanks

- **cmpxchg8b** — Reverse-engineering the Warband engine source code
- **K700** — Engine source analysis and Reverse-engineering
- **Swyter** — Engine internals knowledge, magic tools, answering a lot of questions
