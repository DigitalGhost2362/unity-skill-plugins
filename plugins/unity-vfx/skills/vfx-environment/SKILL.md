---
name: vfx-environment
description: Environment VFX in Unity - ambient life (dust motes, fireflies, falling leaves, petals, butterflies), weather (rain, snow, wind, lightning), water (rivers, waterfalls, ripples, splashes, foam), fire and smoke props (torches, campfires, chimneys), fog cards, god rays, magic props and portals. Holds the rules for effects that loop forever and exist many times in a level - seamless loops, off-screen culling and distance LOD, camera-attached weather in world space, one global wind and time-of-day, vertex-animated meshes instead of particles, cheap fades for fog cards, whole-scene overdraw budget. Invoke with vfx-core for any VFX placed in a level or attached to the camera - "hiệu ứng môi trường", "mưa", "tuyết", "lá rơi", "đom đóm", "thác nước", "sương mù", "god ray", "lửa đuốc", "ambient".
---

# Environment VFX

Invoke `vfx-core` first. An environment effect is judged differently from a combat one: nobody
waits for it, it plays for the whole session, it exists many times in a level, and most of the time
it is off-screen or far away. Its cost is paid continuously, so it is the first place a level loses
frame time.

## Rule 0 - The environment brief

Add to the `vfx-core` brief:

| Field | Why |
|---|---|
| where it lives: placed in the level, attached to the camera, or global | culling and simulation space |
| how many instances in the level, how many visible at once | the real budget is per scene, not per effect |
| camera range: closest and farthest the player sees it | LOD bands, fade distances, texture size |
| loop or event (lightning strike, splash) | loop rules below |
| wind, time of day, weather state that drive it | global inputs, not per-effect settings |
| gameplay meaning, if any (hazard fire, interactable glow) | then it follows `vfx-combat` readability |

## Rule 1 - Loops that never show a seam

- `loop = true`, emission by `rateOverTime`, not bursts. Start lifetime no longer than the
  duration, so the cycle boundary is invisible.
- `prewarm = true` so it is already full the first time it is seen.
- Neighbouring instances must not pulse together: random start delay or seed per instance.
- An effect enabled late (streamed in, revealed): `Simulate(t, true, true)` then `Play()` so it
  appears mid-cycle.
- Mesh loops (water, fog, portals) are pure UV panning - seamless by construction when the textures
  tile; check the seam on the U wrap of cylinders.
- Soak check: watch (or capture) at t and t + one duration; nothing pops, nothing accumulates.

## Rule 2 - Off-screen and far away cost nothing

- **Procedural mode** lets Unity skip off-screen systems and fast-forward them when seen again.
  It is lost by: World simulation space, noise, collision, trigger, sub-emitters, trails, external
  forces, limit velocity, rotation by speed, rate over distance, curves with more than 8 keys, and
  any script change at runtime. The Inspector shows a speech-bubble icon with the reason;
  `ps.proceduralSimulationSupported` reads it.
- When procedural mode is lost: `main.cullingMode = Pause` for ambient loops (Pause and Catch-up can
  spike; Always Simulate only when the state must be exact).
- **Distance LOD** for many instances: one `CullingGroup` with bounding spheres and distance bands -
  band 0 full, band 1 the `_Lite` variant (fewer particles, no secondary layers), band 2 off. Not
  per-effect `Update` distance checks.
- Very far: a static card or nothing. A torch 60 m away is a small additive glow quad, not a
  particle system.

## Rule 3 - Prefer geometry and shaders to particles

- Grass, banners, cloth, vines, butterflies on a path, floating petals near the camera: meshes with
  vertex animation (sine sway weighted by vertex color or UV.y) - zero CPU simulation, batched.
- Water: scrolling-UV meshes with the flow stretched along U or V, two textures panning at different
  speeds and scales; foam particles only where water hits something. Ripples: a pooled flat ring
  quad expanding while it fades (0.6-1 s), not a render-texture simulation on low tiers.
- Waterfalls: a curved strip mesh, UVs stretched along the fall, the seam at the back; mist cards at
  the base; splash particles few and large.
- Fire props: 6-12 flame billboards with a ramp, 1 core glow, 10-30 embers, 2-4 smoke; the light it
  casts is baked or a cheap flicker on an existing light, never the `lights` module.
- GPU instancing for mesh particles pays off only at tens of thousands; give trails their own
  material when it is on.

## Rule 4 - Weather

- **Rain and snow follow the camera but simulate in World space**: a box emitter above and ahead of
  the camera, moved with it; particles stay where they fell. A local-space curtain parented to the
  camera looks glued to the lens when the camera turns or looks up.
- Rain: Stretched Billboard, velocity scale for streak length, few large drops near the camera plus a
  faint scrolling full-screen streak layer for density at almost no fill. Splashes from a ground
  emitter (or collision with messages off). Snow: about twice the lifetime, noise, slower.
- Density scales with the device tier and the weather intensity value, not with copies of emitters.
- **Wind**: one global source (WindZone and/or a global shader vector `_WindDir` / `_WindStrength`)
  read by foliage shaders and by particles through External Forces (its multiplier - particles
  usually need more push than branches). Never a per-effect hard-coded wind.
- **Lightning**: 2-3 flicker pulses of the main and ambient light over 0.2-0.4 s, a bolt mesh or
  flipbook for 0.05-0.1 s, thunder delayed by distance. At most 3 flashes per second.

## Rule 5 - Fog cards and god rays

- Unlit quads, additive or alpha, with three fades: **camera distance** (vanish before the lens),
  **view angle** (fade when seen edge-on, N.V), and soft intersection. Soft particles need the camera
  depth texture, which costs on tile GPUs - on mobile prefer the camera and angle fades, and keep
  cards away from geometry.
- Fit the card mesh to the visible shape (polygon instead of a quad) when cards are large: fewer
  transparent fragments.
- God rays: a few long additive cards or a cone mesh with a vertical gradient and a noise pan, faded
  by view angle; never a stack of overlapping soft particles.

## Rule 6 - Time of day and global state

- Tint, emission rate and visibility of ambient effects come from global values (`_TimeOfDay` 0-1,
  weather intensity) set once per frame or on change through `Shader.SetGlobal*` and one manager -
  fireflies appear at dusk because the global says night, not because each emitter checks a clock.
- Per-instance variation through the seed and start delay, not material copies.

## Budget

The scene, not the effect: all environment transparents together at the busiest view stay within
the scene budget in `vfx-core` (2-3 FSE total on mobile, the effects' share of it agreed with the
project). Measure an effect with `vfx-measure.cs` from the gameplay camera, then profile the
busiest spot of the level in play mode with every instance active.

## Verify

1. `vfx-audit.cs` 0 ERR; every looping emitter is procedural or has `cullingMode` set.
2. Soak: capture at t and t + duration (`vfx-capture.cs` with explicit `Times`), no seam, no pop.
3. Play mode: walk the camera away and back - effects pause or switch LOD, resume mid-cycle without
   a visible restart; turn the camera - weather stays in world space.
4. Profile the busiest view of the level with the real instance count. Report what was not run.

## Mistakes

| Mistake | Looks like | Fix |
|---|---|---|
| bursts in a looping ambient effect | visible pulse every cycle | rate over time, lifetime <= duration |
| no prewarm | effect fills up in front of the player | prewarm on |
| identical instances side by side | torches flicker in unison | random start delay / seed |
| noise + world space on 50 torches | all simulate off-screen, CPU cost | cullingMode Pause, CullingGroup LOD |
| rain parented to the camera in local space | rain sticks to the lens | camera-following emitter, World space |
| per-effect wind values | leaves and grass blow different ways | one global wind |
| soft particles on every fog card on mobile | depth texture cost, fps drop | camera-distance + view-angle fades |
| particle grass, particle butterflies near the path | CPU simulation for decoration | vertex-animated meshes |
| `lights` module on fire props | one realtime light per particle | baked light or one flicker light |
| full-screen stack of fog cards | fill cost everywhere the camera looks | fewer, fitted cards; fade near the camera |
