# Animations - JSON Output Structure

**Script:** `convert_animations.py`
**Source:** `module_animations.py`
**Output:** `animations_full.json`

## Overview

Converts the `animations` list into JSON. Each animation is a list with an ID, two flag fields (animation-level `acf_*` and master-level `amf_*`), followed by one or more sequences. Sequences define the actual BRF resource clips with frame ranges, timing, and per-sequence `arf_*` flags. All animations are hardcoded by index; IDs can be renamed but positions cannot change.

This runs from the Module System directory.

## Root Structure

```
animations_full.json -> Array of Animation objects
```

## Animation Object

| Field | Type | Source | Description |
|---|---|---|---|
| `id` | `string` | `[0]` | Animation ID (e.g. `"stand"`, `"run_forward"`) |
| `index` | `int` | positional | Zero-based hardcoded animation index |
| `anim_flags` | `ACF Flags` | `[1]` | Animation-level flags (`acf_*`) |
| `master_flags` | `AMF Flags` | `[2]` | Master playback flags (`amf_*`) |
| `sequences` | `Sequence[]` | `[3:]` | Animation clip sequences |
| `sequence_count` | `int` | derived | Number of sequences |

## Sequence Object

| Field | Type | Source | Description |
|---|---|---|---|
| `duration` | `float` | `[0]` | Duration in seconds |
| `resource` | `string` | `[1]` | BRF animation resource name |
| `begin_frame` | `int` | `[2]` | Start frame within the resource |
| `end_frame` | `int` | `[3]` | End frame within the resource |
| `flags` | `ARF Flags` | `[4]` | Sequence-level flags |
| `sound_triggers` | `Pack2F`/`null` | `[5]` | Decoded `pack2f(a, b)` sound trigger points |
| `displacement` | `Vector3` | `[6]` | Position displacement (x, y, z) |
| `progress_ratio` | `float` | `[7]` | Blend progress ratio (0.0 - 1.0) |

Fields 5-7 are optional and only present when the source tuple has >5 elements.

## Flag Objects

### ACF Flags (Animation-level)

Combines bitwise flags, rotation vertical mode, and animation length.

| Flag | Value | Description |
|---|---|---|
| `acf_synch_with_horse` | `0x01` | Sync with horse animation |
| `acf_align_with_ground` | `0x02` | Align body with ground slope |
| `acf_enforce_lowerbody` | `0x100` | Enforce lower body only |
| `acf_enforce_rightside` | `0x200` | Enforce right side only |
| `acf_enforce_all` | `0x400` | Enforce entire body |
| `acf_parallels_for_look_slope` | `0x1000` | Use parallels for look slope |
| `acf_lock_camera` | `0x2000` | Lock camera during animation |
| `acf_displace_position` | `0x4000` | Displace agent position |
| `acf_ignore_slope` | `0x8000` | Ignore ground slope |
| `acf_thrust` | `0x10000` | Thrust attack type |
| `acf_right_cut` | `0x20000` | Right cut attack type |
| `acf_left_cut` | `0x40000` | Left cut attack type |
| `acf_overswing` | `0x80000` | Overswing attack type |

**Encoded values:**
- `rot_vertical`: `acf_rot_vertical_bow` (0x100000) or `acf_rot_vertical_sword` (0x200000)
- `anim_length`: bits 24-31, extracted via `acf_anim_length(x)`

### AMF Flags (Master-level)

Combines encoded priority, rider rotation mode, and bitwise playback flags.

**`priority`**: bits 0-11, numeric value controlling animation override priority.

**`rider_rotation`**: encoded in bits 12-15:

| Mode | Value |
|---|---|
| `amf_rider_rot_bow` | `0x1000` |
| `amf_rider_rot_throw` | `0x2000` |
| `amf_rider_rot_crossbow` | `0x3000` |
| `amf_rider_rot_pistol` | `0x4000` |
| `amf_rider_rot_overswing` | `0x5000` |
| `amf_rider_rot_thrust` | `0x6000` |
| `amf_rider_rot_swing_right` | `0x7000` |
| `amf_rider_rot_swing_left` | `0x8000` |
| `amf_rider_rot_couched_lance` | `0x9000` |
| `amf_rider_rot_shield` | `0xA000` |
| `amf_rider_rot_defend` | `0xB000` |

**Bitwise flags** (bits 16+):

| Flag | Value | Description |
|---|---|---|
| `amf_start_instantly` | `0x10000` | Start without blend |
| `amf_use_cycle_period` | `0x100000` | Use cycle period for timing |
| `amf_use_weapon_speed` | `0x200000` | Scale by weapon speed |
| `amf_use_defend_speed` | `0x400000` | Scale by defend speed |
| `amf_accurate_body` | `0x800000` | Accurate body positioning |
| `amf_client_prediction` | `0x1000000` | Enable client-side prediction |
| `amf_play` | `0x2000000` | Play once (non-looping) |
| `amf_keep` | `0x4000000` | Hold at end frame |
| `amf_restart` | `0x8000000` | Restart even if already playing |
| `amf_hide_weapon` | `0x10000000` | Hide weapon during animation |
| `amf_client_owner_prediction` | `0x20000000` | Owner client prediction |
| `amf_use_inertia` | `0x40000000` | Apply inertia |
| `amf_continue_to_next` | `0x80000000` | Auto-transition to next animation |

### ARF Flags (Sequence-level)

**`blend_in`**: bits 0-7 as an encoded counter (0 = instant, higher = smoother blend).

| Flag | Value | Description |
|---|---|---|
| `arf_make_walk_sound` | `0x100` | Trigger walk footstep sounds |
| `arf_make_custom_sound` | `0x200` | Trigger custom sound |
| `arf_two_handed_blade` | `0x1000000` | Two-handed blade variant |
| `arf_lancer` | `0x2000000` | Lancer variant |
| `arf_stick_item_to_left_hand` | `0x4000000` | Stick item to left hand |
| `arf_cyclic` | `0x10000000` | Loop animation |
| `arf_use_walk_progress` | `0x20000000` | Sync with walk progress |
| `arf_use_stand_progress` | `0x40000000` | Sync with stand progress |
| `arf_use_inv_walk_progress` | `0x80000000` | Sync with inverse walk progress |

### Pack2F (Sound Triggers)

```json
{ "a": 0.4, "b": 0.9, "raw": 58112 }
```

Decoded from `pack2f(a, b)`, representing normalized (0-1) trigger points within the animation cycle where footstep or custom sounds fire.

## Notes

- **Hardcoded indices**: Animation positions are engine-hardcoded. Only `unused_human_anim_*` and `unused_horse_anim_*` slots can be repurposed.
- **Multi-sequence**: Most animations have 1 sequence; `stand` has 4 (weapon stance variants).
- **Frame expressions**: Some frame indices use constants like `combat + 500` which are evaluated at import time.
