---
name: vfx-core
description: Shared foundation for every Unity VFX task - the brief (what the effect is for, gameplay data it must match, style, reference images or video), the build loop, the mesh-first rule for mobile, FSE overdraw budgets and how to measure them, Shuriken vs VFX Graph, one uber material with a fixed vertex-stream contract, layering and motion rules, texture rules, pooling/seed/culling, and the mistakes table. Ships the VFX/Uber shader and scripts that audit prefabs, measure real overdraw, and generate textures and meshes. Invoke BEFORE vfx-combat, vfx-ui, vfx-environment or vfx-clothing, and alone for any particle or effect request that fits none of them - "làm hiệu ứng", "làm vfx", "particle", "effect bị lag", "overdraw", "tối ưu vfx", "fix effect".
---

# VFX core

An effect is done when it tells the player what it is for, matches the gameplay data behind it,
fits the project's style, and stays inside a measured budget on the weakest target device. This skill
holds what every effect shares. The domain skill on top adds the rules of its domain:

| Domain | Skill | Its question |
|---|---|---|
| skills, hits, projectiles, AoE, buffs | `vfx-combat` | does the visual match the damage area and the hit frame? |
| uGUI: rewards, buttons, rarity, popups | `vfx-ui` | does it render in the Canvas, at any aspect, while paused? |
| ambient, weather, water, fire, fog | `vfx-environment` | does it loop forever, cull off-screen, and stay cheap at scale? |
| garments, outfit reveal, aura, sparkle | `vfx-clothing` | does it follow the skinned body and stay subordinate to the outfit? |

A project's own `CLAUDE.md`, `AGENTS.md` or VFX style guide wins on conflict.

## Rule 0 - Understand the effect before building it

Never open a particle system before the brief exists. Fill `reference/brief.md` in your head or in
the chat, from the request, the code and the references; ask only for what none of those answer,
in ONE `AskUserQuestion`. The brief has, at minimum:

1. **Purpose** - what the player must understand from it (danger here, you were hit, item is rare,
   it is raining). An effect with no purpose gets cut, not polished.
2. **Gameplay binding** - the data it must match: radius, length, duration, hit frame, rarity tier,
   team. Find the config key or field in code; never eyeball a radius.
3. **Camera and screen size** - gameplay camera, distance, how large it reads on a phone screen.
4. **Style** - the project's family (toon/anime, stylized, realistic), 3-5 style keywords, palette,
   edge treatment. Read existing effects in the project first; a new effect must sit next to them.
5. **References** - images or video from the user. Break each one down (see below). No references
   and no existing effects = propose 2-3 style directions in words and let the user pick.
6. **Timeline** - anticipation, impact/peak, dissipation, total length; loop or one-shot.
7. **Importance and copies** - 1-10 rating (idle and frequent effects dim, rare game-changers bright)
   and the worst-case number on screen at once.
8. **Target tier** - lowest device, render pipeline, post-processing on that tier (no Bloom means
   glow must live in the texture), whether compute shaders are available.

### Reading a reference

For an image: silhouette, the 2-3 colors plus the near-white core, value contrast (where the
darkest dark sits against the brightest bright), edge treatment (hard cel, soft, eroded), shape
language (round = friendly, sharp = danger). For video: step it frame by frame, log the frame where
each phase starts, convert to seconds, and keep the RATIOS when retiming to gameplay length. Anime
references held on 2s or 3s (12 or 8 fps) need stepped curves or a flipbook fps, not smooth motion.
Write what you take from each reference: "shape only", "timing only", "palette only".

## Rule 1 - Pick the system by the target

| Target | System |
|---|---|
| phones, WebGL, anything that must run everywhere | Particle System (Shuriken) + meshes |
| per-particle C# logic, Unity physics collisions | Particle System |
| thousands to millions of GPU particles, depth collision, PC/console | VFX Graph |

VFX Graph needs compute shaders and SSBOs: no OpenGL ES, still preview on mobile and URP, and on the
web only through WebGPU. A device that falls back to GLES draws nothing at all. Ship VFX Graph only
on tiers that have it, with a Shuriken fallback.

VFX Graph is a black box: copy a template graph, expose a few properties and events, and drive it
from C# through cached IDs (`Shader.PropertyToID` or `ExposedProperty`), reusing one
`VFXEventAttribute`. `aliveParticleCount` is an asynchronous readback that can be up to about a
second old and reads nothing useful right after spawn - never drive gameplay from it.

## Rule 2 - Mobile is mesh-first; overdraw is the cost

- **Backbone = a simple mesh with a texture moving over it**: ring, arc, cone, cylinder, dome, quad.
  Shockwaves, slashes, beams, tornados, auras, telegraphs and water are meshes. Particles are the
  garnish: sparks, embers, motes, debris. Mobile studios report most shipped effects built this way
  (one cites about 90%); treat it as the default, not a law.
- **Particles: about 100 per emitter maximum on mobile**, usually far fewer.
- **Fill rate is the limit, not particle count.** Every transparent fragment is shaded, alpha 0
  included. Measured example: one shockwave as a ring mesh = 1 layer, 0.037 FSE at peak; the same
  wave as 36 smoke puffs = 5-7 layers where they overlap, 0.35 FSE - about ten times the cost.
- **FSE (full-screen equivalent)**: total transparent fragments / screen pixels. 1 FSE = the whole
  screen shaded once. Budget the worst frame, at gameplay framing AND at the closest framing the
  game allows.

| Effect role (mobile, one instance, gameplay framing) | Peak FSE | p95 layers |
|---|---|---|
| frequent: basic hit, footstep, pickup, UI feedback | <= 0.3 | <= 3 |
| ability, impact, buff, outfit idle | <= 0.5 | <= 4 |
| rare hero moment: ultimate, reveal, level up (short) | <= 1.5 | <= 8 |
| whole scene, all transparents together | <= 2-3 | - |

These are starting budgets; the project's device profile overrides them. CPU and memory still
matter: each Particle System costs runtime memory, and draw calls per spell stay around 8-10 for a
big spell, 5-6 for a small one, 2-3 for an effect that appears on many targets.

## Rule 3 - One uber material

One shader for every effect: grayscale shape colored by a LUT ramp, intensity, erosion with a burn
edge, noise distortion, static mask, polar UV, soft and camera fades, additive-to-alpha blend in one
slider. Per-particle variation (erosion speed, random texture offset, birth flash) arrives through
the Custom Data module and custom vertex streams, because a shader cannot invent a stable random
number per particle. One material then serves every variant, and systems that share material AND
stream list batch together.

- Project already has an uber particle shader (grep `*.shader`, `*.shadergraph` for Uber, Dissolve,
  Erosion, Custom1): use it and learn its stream contract. Do not ship a second one.
- None, URP project: copy `${CLAUDE_SKILL_DIR}/assets/VFX_Uber.shader` into the project's shared VFX
  shader folder. Its contract, properties and setup snippet: `reference/uber-material.md`.
- The stream order is a contract. A wrong order shifts every TEXCOORD; Custom Data without its
  stream reads 0 and the erosion silently disappears. Renderers without streams (MeshRenderer,
  Trail, Line) do NOT read 0 - their materials turn `_CustomData` off. `scripts/vfx-audit.cs`
  catches all three.
- Toggles are material-asset keywords. Never enable a `shader_feature` keyword from gameplay code -
  the variant was not built into the player. Swap materials.

## Rule 4 - Layers with one job each, contrast, motion

- **One job per layer.** Fireball: dark alpha body below, hot additive core in the middle, glow on
  top. Typical effect: 3-5 layers; more than 7 rarely pays for its overdraw.
- **Each stacked transparent layer gets its own sorting order**, brightest on top. Equal orders
  flicker as the camera moves.
- **Additive only brightens.** On bright backgrounds it washes out - put a dark alpha layer under it.
- **No linear curves** except loops. Fast-then-slow (ease-out) for size and speed; alpha fades in
  fast, holds, fades out. Linear reads mechanical.
- **Mix fast and slow detail.** Something is always moving; a looping effect starts with a burst.
- **Front-load the impact**: peak by 0.05-0.1 s, main action in the first ~25% of the life, then a
  long dissipation that loses intensity before it vanishes.
- **Sparks shrink, fire grows. Every layer fades in and out** - nothing pops on or off at full alpha.
- **Variant sheets**: each particle picks one random frame and keeps it (Frame over Time = random
  between two constants). Never play a variant sheet as animation.
- **Randomize** size, rotation, lifetime, speed with ranges, and the seed per spawn.
- **Hot core near white, color in the mid and outer layers.** HDR colors keep every channel that
  must stay below white under 1 after the multiplier (`#FF7A00 x1.6` stays orange, `#FFA000 x1.8`
  clips to lemon yellow).

## Rule 5 - Textures: borrow or generate, at the size the screen shows

- Simple shapes (glow, ring, spark, star, trail, noise, ramp) are generated by code, never painted:
  `scripts/vfx-texture.cs`. Simple meshes too: `scripts/vfx-mesh.cs`.
- Mobile: 256 typical, 512 maximum (4x4 flipbooks). Size from screen coverage, not from habit.
- **Data textures (noise, masks, flow, erosion, channel-packed) import with sRGB OFF.** In a Linear
  color-space project an sRGB 0.5 grey reads as 0.21: erosion and distortion go dark and wrong.
- **Ramps/LUTs**: uncompressed, Clamp, no mipmaps. sRGB on when they hold painted colors.
- Mipmaps on for world-space effects, off for UI and screen-space ones. Bilinear, anisotropic <= 2.
- Shape textures keep their values off the border; soft edges, not a hard white blob - erosion needs
  a range of values to eat into.

## Workflow

1. **Brief** (Rule 0). Then invoke the domain skill and add its questions.
2. **Plan layers in text** before any tool call: per layer - role, mesh or particle, texture,
   blend, sorting order, timing, count. Check the plan against Rules 2-4.
3. **Assets**: list what exists (`Glob` the project's VFX folders, textures and meshes); generate
   only what is missing with `vfx-texture.cs` / `vfx-mesh.cs`.
4. **Build through the Editor**, never by hand-writing prefab YAML:
   - The Editor bridge lists `vfx-apply`, `vfx-read`, `vfx-preview`, `vfx-texture`, `vfx-history`
     (they may need `tool-set-enabled-state` first): use them, spec format in `reference/vfx-tools.md`.
   - Otherwise adapt `scripts/vfx-build-template.cs` (root controller, one child per layer, uber
     material and streams pre-wired) and run it in one `script-execute` call
     (`reference/build-snippets.md`).
   - Never dump a ParticleSystem component (`particle-system-get`, `gameobject-component-get`):
     it is huge. Read back with `vfx-read` or a script that prints only the fields you need.
5. **Audit**: `scripts/vfx-audit.cs` on the prefab. 0 ERR; every WARN fixed or explained.
6. **Measure**: `scripts/vfx-measure.cs` from the gameplay camera, then at the closest framing.
   (`vfx-preview` stats give a world-space fill estimate, not pixels - use the script for budgets.)
7. **Look**: one contact sheet per iteration (`vfx-preview`, or `scripts/vfx-capture.cs` then open
   the PNG), from the gameplay camera; judge glow in the game view with the game's post-processing. Critique against the brief per phase: silhouette, timing, density,
   value contrast, readability. Name at most 3 problems, fix only those
   (`reference/recipes.md` has the symptom-to-fix table). Stop after 4 visual iterations or when it
   meets the brief; keep the best version, not the last.
8. **Integrate** (below), then prove it in play mode (`verify`) with N copies.
9. **Report**: prefab path, peak FSE / p95 layers / peak alive at both framings, audit result, what
   was not verified (device profile, other aspect ratios), and the 2-3 knobs a human will tweak.

## Integration

- **Pool** effects and pre-warm the pool at load; replay with `Clear` + `Play`. Instantiate/Destroy
  per shot causes allocation spikes. Return on `main.stopAction = Callback` /
  `OnParticleSystemStopped`; stop early with `Stop(true, StopEmittingAndClear)`.
- **First-play hitches** usually come from shader/PSO compilation, not instantiation: warm shaders
  at load (Unity 6: `GraphicsStateCollection`; older: a `ShaderVariantCollection`).
- **Seeds**: `useAutoRandomSeed` on, or reseed per spawn. Fixed seeds are for captures only.
- **Scale from data**: author at unit size (1 m radius), `scalingMode = Hierarchy` on every system,
  set the root scale from the gameplay value at spawn.
- **Culling**: looping effects must stop simulating off-screen. Procedural mode (automatic culling)
  is lost with world space, noise, collision, sub-emitters, trails, external forces, limit velocity
  and curves with more than 8 keys; then set `main.cullingMode = Pause` or toggle via `CullingGroup`.
- **Time**: effects that must run while paused use `main.useUnscaledTime`.
- **Ownership**: an effect prefab never owns gameplay. Gameplay calls play/stop; the effect does not
  deal damage or decide timing.

## Mistakes

| Mistake | Looks like | Fix |
|---|---|---|
| particle swarm where a mesh would do | ring wave costs 5-7 layers | 1 ring mesh + scrolling texture |
| Custom Data without its vertex stream | shader reads 0, no erosion, no birth flash | declare the stream; audit |
| particle material reused on a MeshRenderer | texture shifted, wrong brightness | mesh material with `_CustomData` 0 |
| stream order differs from the shader contract | wrong values, broken batching | the contract order on every system |
| data texture left sRGB | effect too dark, erosion wrong | sRGB off |
| ramp/LUT compressed or wrapping | banding, ends bleed into each other | uncompressed, Clamp, no mips |
| linear curves | stiff, mechanical | ease-out, fast-then-slow |
| fixed seed | every shot identical | auto seed or reseed per spawn |
| Instantiate/Destroy per shot | allocation spikes | pool, pre-warm at load |
| explosion right at the camera | full-screen fill, fps drop | renderer Max Particle Size (fraction of viewport, default 0.5) tuned per effect, measured at close framing |
| stacked transparents share a sorting order | flicker | own order per layer, brightest on top |
| float vertex colors on a Mesh particle's mesh | dark, tinted mesh | Color32 colors |
| GPU instancing on a Custom Data mesh particle | custom streams ignored | instancing off |
| uber keyword toggled at runtime | works in Editor, nothing in build | separate material per variant |
| VFX Graph on a GLES device | effect invisible | Shuriken fallback |
| bursts of large soft quads for a constant glow | white hotspot, no visible gain | 1-2 long-lived glow particles |
| additive layer on a bright background | washed out, invisible | dark alpha layer under it |

## Files

- `reference/brief.md` - brief template, style families, reference breakdown sheet.
- `reference/uber-material.md` - VFX/Uber properties, stream contract, Custom Data setup, material recipes.
- `reference/recipes.md` - layer recipes per effect type, timing table, symptom -> cause -> fix.
- `reference/vfx-tools.md` - spec format of the `vfx-*` Editor bridge tools, when the bridge has them.
- `reference/build-snippets.md` - `script-execute` C# for building, reading back and pooling.
- `assets/VFX_Uber.shader` - the URP uber particle/mesh shader.
- `scripts/vfx-build-template.cs` - builds an effect prefab: controller + layers, uber material,
  Custom Data and streams wired (example: a mobile hit).
- `scripts/vfx-audit.cs` - static audit of prefabs (`className VfxAudit`, `methodName Main`).
- `scripts/vfx-measure.cs` - real overdraw: FSE, deepest and p95 layers, peak alive, cost per layer.
- `scripts/vfx-capture.cs` - contact sheet PNG of the effect over time, auto-framed or from a camera.
- `scripts/vfx-texture.cs` - procedural textures with the right import settings.
- `scripts/vfx-mesh.cs` - ring, arc, cone, cylinder, dome, quad meshes with panning UVs.

All scripts run through `script-execute` with `isMethodBody: false`; fill the `FILL-IN` block first.
With no Editor bridge, write the effect plan and the scripts, and say plainly that nothing was built,
measured or seen.
