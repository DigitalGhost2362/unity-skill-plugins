# VFX vocabulary

Idea bank for 4 options that really differ, the spec the harness simulates, and what the user
checks before picking. Read this instead of inventing from scratch.

## Divergence axes

Every kept option differs from every other on at least 2 of these. The gameplay invariants
(radius, hit time, duration, camera) never vary.

| Axis | Stops |
|---|---|
| **Primary driver** - what carries the read | shape (a ring, a spike, a symbol) / motion (a fall, a sweep, a swirl) / color (a flash, a tint wave) |
| **Shape language** | round = friendly, safe, heal / sharp, triangular = danger, damage / square = sturdy, tech, shield |
| **Tempo** | impulse (peak by 0.05 s, long tail) / crescendo (anticipation, then the hit) / rhythmic (pulses, multi-hit) / lingering (slow rise, slow fade) |
| **Construction** | mesh + erosion (stylized, cheap) / flipbook or cel shapes (toon, held frames) / soft billboards and smoke (volume, expensive) / graphic symbols (stars, glyphs, runes) |
| **Intensity** | restraint (frequent, dim) / standard / spectacle (rare, bright, short) |
| **Element read** | fire, ice, lightning, arcane, nature, holy, shadow, tech, water, wind - and its color convention (heal green, frost blue, fire orange-red) |
| **Space** | on the ground / in the air / around the character / from the source to the target / screen-level |

**Primary verbs** - no two options share one: expand, burst, billow, erupt, rise, fall, sweep,
swirl, converge, shatter, flicker, drift, bloom, pulse, wipe, orbit.

A good set of 4 usually spans construction (one mesh-first, one soft) and intensity (one restrained,
one loud) and lets the other axes follow from the thesis.

## Per-domain menus

**Hit / impact** (0.3-0.4 s): ring snap + sparks (mesh) / star flash + streaks (graphic) / dust puff
+ debris (physical) / slash-mark decal + shake (stylised). Peak by 0.05-0.1 s.

**AoE / telegraph** (lasts the cast time): filling disc from the centre / edge ring that tightens /
runes drawn around the edge / ground cracks spreading. The primary edge = the damage edge; fill
alpha 0.15-0.3 so the ground stays readable. Enemy and ally colors must differ.

**Projectile**: glowing head + trail / spinning shape + motes / beam-like streak / lobbed object +
shadow. The head is the brightest point; the direction must read in one paused frame.

**Slash**: arc mesh with erosion / two-tone crescent + sparks at the tip / afterimage strips / ink
or cel smear. Life = the swing (0.15-0.3 s).

**Buff / aura** (loop): ground ring + rising motes / cylinder of panning noise / orbiting symbols /
pulsing rim. Start with a burst; idle at a low intensity; frequent effects dim.

**Heal**: slow rising motes (under 60 BPM) / soft plus-shapes / green-white ground bloom / petals.
Round shapes only.

**Reward / pickup (world or UI)**: glow pop + star sparkles / coin fountain / spiral converge to the
target / shine sweep + ring. Under 0.3 s for a pickup, celebrations up to ~1.2 s.

**Rarity ladder**: common nothing or a faint rim / rare a slow shine / epic motes + pulse / legendary
aura + periodic burst. Each tier adds one layer; do not jump to spectacle at tier 2.

**Outfit reveal / swap**: dissolve with a burning edge / spiral of petals or sparkles / light column
+ silhouette flash / ribbon wrap. Route B when the look is in the fabric shader.

**Ambient**: drifting motes / fireflies blinking / falling leaves or petals / god-ray cards. Loop,
low alpha, cheap - they exist many times.

**Weather / water / fire prop**: rain streaks vs drops + splashes; snow flakes vs drifting clumps;
splash = crown + droplets vs ring ripple; torch = flipbook flame vs mesh flame vs billboard core +
embers.

## Spec subset the harness simulates

Encodings are `vfx-apply`'s (`vfx-core/reference/vfx-tools.md`): number, `[min,max]`, curve string
`"0:0,0.2:1,1:0"` (auto-smooth, flat ends), `{c, m}`, `{min, max, m}`; colors `#RRGGBB[AA][*k]`;
gradients `"#FFF@0,#F80@1|1@0,0@1"`. Defaults match a new `vfx-apply` emitter: duration 1, lifetime
1, speed 2, size 0.5, rate 10/s, sphere r 0.1, `glow/add`, one-shot. Set `rateOverTime: 0` on
burst-only layers. `spec.defaults` merges per module into every emitter; `spec.root.scale` scales
the whole effect.

| Key | Simulated |
|---|---|
| emitter | `name, pos, rot` (Unity Euler order Z, X, Y), `material "<tex>/<add or alpha>"`, `trailMaterial` |
| `main` | `duration, loop, startDelay, startLifetime, startSpeed, startSize` (or `startSize3D` + `startSizeX/Y/Z`), `startRotation` (or `startRotation3D` + `startRotationY`), `startColor` (color, two colors, gradient over duration, `{rand: gradient}`), `gravityModifier, simulationSpace, maxParticles` |
| `emission` | `rateOverTime`, `bursts [[t, count or [min,max], cycles, interval, probability]]` |
| `shape` | `Sphere, Hemisphere, Cone, Circle, Donut, Edge, Box, Rectangle`; `radius, radiusThickness, angle, arc, donutRadius, scale, position, rotation, randomDirectionAmount, sphericalDirectionAmount`; `false` = point along +Z. Cone/Hemisphere point along local +Z: `rot:[-90,0,0]` points them up |
| `velocityOverLifetime` | `x, y, z, radial, orbitalY` (rad/s, approximate) |
| `limitVelocityOverLifetime` | `limit, dampen, drag` |
| `forceOverLifetime` | `x, y, z` |
| `noise` | `strength, frequency, scrollSpeed` - approximate, flagged |
| `sizeOverLifetime` | `size`, or `separateAxes` + `x, y, z` |
| `colorOverLifetime` | `color` gradient (multiplies startColor, alpha included) |
| `rotationOverLifetime` | `z` (deg/s), `y` for flat meshes |
| `trails` | `lifetime` (fraction of particle life), `widthOverTrail, colorOverTrail, minVertexDistance`; drawn as a tapered stroke, texture ignored |
| `subEmitters` | `list [{emitter, type:'Death', inherit:'InheritColor'}]`; the target needs `rateOverTime: 0` and bursts at 0 |
| `renderer` | `renderMode Billboard / Stretch / HorizontalBillboard / Mesh`, `velocityScale, lengthScale, sortingOrder, mesh` |
| preview-only | `uber {erode, erosion, edge, edgeWidth, ramp, intensity}`, `path {to, time, ease:'in'/'out', clear}`, `face:'camera'` |

Textures: `glow circle dot ring spark star flare smoke shard petal trail`. Mesh tokens: `ring:<inner>`,
`arc:<degrees>:<inner>` (centred on +Z, like `vfx-mesh.cs`), `disc`, `quad`, `cone:<topRadius>`,
`dome`. Ground meshes lie in XZ; `cone` and `dome` stand along +Y.

Not simulated (flagged as a chip): collision, trigger, lights, textureSheetAnimation, externalForces,
inheritVelocity, colorBySpeed, sizeBySpeed, rotationBySpeed, customData (use `uber`), Birth
and Collision sub-emitters, VerticalBillboard, emitter `parent` (positions are root-relative).

## Pick checklist (for the user, one line in chat)

1. Pause on the hit frame: does the still alone say what happened and where?
2. Grey on: is it still readable? Readability comes from value contrast, not hue.
3. Bloom off (if the weakest device has none) and bright ground: still there?
4. x5 copies: do they merge into one blob?
5. FSE color: green ships, amber needs a reason, red is hero tier or a rebuild.

## Timing and color quick rules

- Main action in the first ~25% of the life; climax carries the highest contrast and saturation;
  dissipation short and low-contrast; tail under 0.5 s after the gameplay state ends.
- Attack faster than a heartbeat, heal slower than 60 BPM.
- One dominant hue + one secondary, near-white core. Avoid 0 and 100% value and saturation in large
  areas. VFX sit above characters in value and below the UI.
- No linear curves except loops and shakes.
