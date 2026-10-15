---
name: vfx-combat
description: Combat VFX in Unity - skill and attack effects, hits and impacts, projectiles, slashes, AoE telegraphs and ground indicators, explosions, buffs and debuffs, heals, beams. Holds the rules that make a combat effect honest - the drawn shape equals the damage shape from the same config value, the impact lands on the hit frame, brightness follows importance, enemy and ally read apart - plus telegraph construction, timing and hitstop, readability with many copies, pooling and LOD, and a script that renders the effect top-down and checks its reach against the gameplay radius, cone or box. Invoke with vfx-core for any combat effect - "vfx chiêu", "hiệu ứng skill", "hit effect", "đạn", "chém", "vùng sát thương", "AoE", "telegraph", "nổ", "buff".
---

# Combat VFX

Invoke `vfx-core` first: brief, system choice, mesh-first, budgets, uber material, layering. This
skill adds what combat needs on top. The order of priorities competitive games publish for their
VFX holds for every combat effect:
**hitbox accuracy, then readability, then noise, then power level, theme last.** A beautiful
effect that lies about its damage area is a bug.

## Rule 0 - The combat brief

Add to the `vfx-core` brief, reading the gameplay code instead of asking where you can:

| Field | Where it comes from |
|---|---|
| damage shape and size | the collider / `OverlapSphere` / `OverlapBox` / cone check the damage code runs, and the config field that sizes it (radius, angle, width x length) |
| hit frame | the AnimationEvent (or timeline marker) that resolves damage |
| cast / wind-up time, active time | skill config: telegraph length, channel duration, buff duration |
| who sees it | caster only, allies, enemies, everyone; team colors |
| dodgeability | can the player step out? then the telegraph is the most important layer |
| importance 1-10, per viewer | frequent basic attack low; ultimate, hard crowd control, big damage high |
| copies on screen | fire rate x lifetime, units in a fight |

Grep before asking: `OverlapSphere|OverlapBox|OverlapCapsule|SphereCast|radius|range|angle` in the
skill/ability code, `AnimationEvent|OnHit|Hit\(` in the animation glue.

## Rule 1 - Drawn shape = damage shape

- The **primary shape** (the ring edge, the telegraph outline, the slash arc, the explosion body at
  peak) matches the damage area: reach ratio 0.95-1.1 of the gameplay size, never under 0.85
  anywhere. Glow, sparks and smoke may overshoot and read as secondary; the primary edge may not.
  Never draw more area than the damage covers (overstated dodge room), never less.
- **Author at unit size, scale from the same value the damage code reads.** Unit circle = radius 1,
  cone = radius 1 with its real angle, line = 1 m long. At spawn: `scale = config.radius`, every
  system `scalingMode = Hierarchy`. A hand-scaled prefab drifts the first time a designer tunes the
  radius.
- A cone's angle is not a scale: generate the arc mesh at the gameplay angle (`vfx-mesh.cs`,
  `arc`, `Arc = angle`, `Inner = 0`), or rebuild it when the angle is data-driven.
- Particle counts that fill an area (ground fire, poison cloud) scale with radius squared, so a
  bigger area is not a sparser one.
- **Prove it**: `scripts/vfx-fit.cs` renders the effect top-down at its widest moment, measures the
  visible reach along 72 rays, prints PASS / UNDER / OVER and writes a PNG with the gameplay outline
  in red. Run it at two gameplay sizes (smallest and largest the config allows) and open the PNG.
  In play mode, a debug gizmo drawn from the SAME config (`OnDrawGizmos` wire disc / cone / box)
  next to the effect is the second proof.

## Rule 2 - Timing follows the gameplay events

- **One event drives one moment.** The hit-frame AnimationEvent resolves damage AND spawns the
  impact effect AND triggers hitstop and shake. Never time an effect with `Invoke`, coroutine delays
  or a separate timer that copies the animation timing - it drifts on the first animation edit.
- Anticipation (charge glow, gathering particles) starts on the wind-up event and ends exactly at
  release. The impact peaks within 0.05-0.1 s of the hit frame.
- **The effect ends with the gameplay state.** A buff loops and is stopped by the buff's end, a
  telegraph lasts exactly the cast time, a projectile's visual is a child of the gameplay projectile
  (it does not simulate its own flight). After the state ends: under 0.5 s of tail, 0 particles.
- Hitstop: freeze the attacker's and target's animator (`speed = 0`) for 2-3 frames on light hits,
  4-6 medium, 8-10 heavy; run the timer on unscaled time. Do not drop `Time.timeScale` - UI, audio
  and VFX freeze with it. Impact effects keep playing through hitstop (`useUnscaledTime` if needed).
- Retime from references by ratio (`vfx-core` reference breakdown), then fit the absolute numbers to
  the animation, not the reverse.

## Rule 3 - Readability

- **Brightness is a budget spent by importance.** Frequent effects dim and small; rare,
  game-changing ones bright. Check the set together: no basic attack outshines an ultimate.
- **Enemy vs ally**: a palette keyed by team (Self / Ally / Enemy / Neutral) read from one place,
  never hard-coded per effect - and never hue alone: pair color with shape (enemy telegraph: sharp
  edge, inward-pointing ticks; ally: soft fill). Leave room for a colorblind remap.
- **Telegraphs of dangerous enemy abilities are the most readable layer on screen.** Cosmetic
  effects (skins, outfit auras, ally buffs) never hide a hitbox and are at least as clear as the base.
- **Projectiles show direction in a still frame**: bright, contrasted head, tail that fades and
  blurs behind. Heals read soft and round, slower than 60 BPM; attacks sharp and faster.
- **Many copies**: review with the worst-case number on screen at gameplay spacing, not one
  instance. 20 explosions must read as 20 shapes, not one white blob - darker bodies, smaller
  additive cores, fewer layers when crowded.
- **Flash and shake**: no more than 3 flashes per second; a full-screen flash is 1-2 frames at alpha
  0.5 or less. Shake scaled by importance (most hits get none), through the project's camera-shake
  owner (Cinemachine Impulse, ...), with a user-facing intensity setting.

## Rule 4 - Telegraphs and ground indicators

- Mobile: a flat unlit transparent mesh 2-5 cm above the ground, ZWrite off - quad for circles and
  boxes (polar UV or a radial texture), arc mesh for cones. Not the legacy Projector (re-renders
  every mesh inside it), and URP Decals only for uneven ground, Screen Space technique (DBuffer needs
  a depth-normal prepass that tile GPUs pay heavily for).
- **Never turn on soft particles for a mesh lying on the ground** - the fade makes it invisible
  (observed). Lift it instead.
- Two layers: the **edge** (never erodes, full size from the first frame, the exact damage edge)
  and the **fill** (shows progress). Fill as one mesh particle whose lifetime is set to the cast time
  at spawn: radial-gradient texture, VFX/Uber erosion driven by Custom1.x from 1 to 0 over life, so
  it grows from the centre to the edge exactly when the damage lands.
- Release: the fill flashes and the impact effect takes over on the same frame; the telegraph is
  gone within 0.1 s.

## Rule 5 - Building blocks

Recipes in `vfx-core/reference/recipes.md`; combat-specific notes:

| Element | Build |
|---|---|
| slash / swipe | arc mesh as a Mesh particle spawned on the swing frame; arc = the swing angle, radius = weapon reach; U-pan + erosion over 0.15-0.3 s; thin bright edge on top; sparks at the tip. Not a TrailRenderer (its UVs re-stretch and kink) |
| weapon trail that follows a moving target | Trails module or TrailRenderer, material with `_CustomData` 0, short time (0.15-0.2 s) |
| projectile | head glow Local space + trail + `rateOverDistance` motes in World space; muzzle 0.1-0.2 s on the caster; impact oriented by the hit normal |
| impact on a surface | up = surface normal; dark scorch quad then a smaller hot core that fades first; billows offset along the normal so they do not cut into the wall |
| shockwave | ring mesh scaling out ease-out, 1 layer of fill; never a ring of smoke puffs |
| buff / debuff (loop) | ground ring + column + motes, start burst; color by buff type; stops with the buff |
| beam | cylinder or stretched quad from muzzle to hit point, length set every frame from the raycast; noise pan; hit effect at the end |
| explosion | flash, fireball, shockwave ring, smoke, debris, scorch - body radius = damage radius at peak |

## Rule 6 - Performance at fight scale

- Pool every combat effect, pre-warm the pools for the skills in the loadout at scene load.
- Cap concurrent instances per effect type (e.g. 8); beyond it steal the oldest or play the `_Lite`
  variant. Ally and far-away effects get the lite variant first; the local player's and enemies'
  telegraphs never degrade.
- Measure one instance with `vfx-measure.cs` at the gameplay camera with `EffectScale` = the real
  radius, then the worst crowd (N copies) in play mode with the profiler.
- Draw calls: about 8-10 for a big spell, 5-6 small, 2-3 for an effect that lands on many targets.

## Verify

1. `vfx-audit.cs` 0 ERR. 2. `vfx-fit.cs` PASS at min and max gameplay size (open the PNG).
3. `vfx-measure.cs` within the combat budget at gameplay framing and close framing.
4. Play mode (`verify`): trigger the real skill; the impact spawns on the hit-frame event, the
   telegraph ends at release, the effect stops with the gameplay state, the gizmo from config matches
   the edge in a screenshot. 5. Crowd: the worst-case copies on screen, profiler delta noted.
Report what was not run (device profile, team-color variants, smallest/largest radius).

## Mistakes

| Mistake | Looks like | Fix |
|---|---|---|
| prefab hand-scaled to "look right" | damage lands outside the drawn ring | unit authoring + scale from config |
| visual ring bigger than the damage | players dodge "out" and still get hit... or think they are safe | reach 0.95-1.1, `vfx-fit.cs` |
| impact timed by `Invoke` | drifts from the swing after an animation edit | spawn from the hit-frame event |
| buff effect with its own duration | outlives or dies before the buff | loop, stopped by the gameplay owner |
| slash as TrailRenderer | stretched, kinked UVs | arc mesh particle |
| telegraph with soft particles on flat ground | invisible | lift 2-5 cm, soft off |
| team told apart by hue only | unreadable for colorblind players, at a glance | hue + shape, palette from one place |
| every hit flashes and shakes | noise, fatigue, photosensitivity risk | by importance; <= 3 flashes/s |
| hitstop via `Time.timeScale` | UI and VFX freeze too | freeze the two animators |
| 20 copies of a bright additive explosion | one white blob | darker bodies, smaller cores, lite variant |

## Files

- `scripts/vfx-fit.cs` - top-down reach check against a Circle / Cone / Box gameplay shape
  (`className VfxFit`, `methodName Main`), PNG with the outline in red.
