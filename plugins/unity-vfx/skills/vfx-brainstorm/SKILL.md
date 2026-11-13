---
name: vfx-brainstorm
description: Previews 4 directions for a Unity particle or shader effect in one HTML file, at the gameplay camera with the cost of each, so the user picks a look before anything is built. Use when an effect's look is undecided, e.g. "brainstorm vfx", "gợi ý hiệu ứng", "cho vài option vfx", "hiệu ứng này nên trông thế nào". UI tween motion uses ui-brainstorm.
---

# VFX brainstorm

Decide how an effect looks in a browser, where a direction costs seconds, then build it in Unity
once. The deliverable is **one HTML file and a pick**: no Unity asset is created by this skill.

Why this shape: pros settle purpose first, then primary shape and timing, then color, using the
cheapest artefact that answers those questions - a moving grey block-in per option and a still at the
climax, pinned up side by side so the team sees how different the pictures in their heads were
(Keyser, Riot; Nordeus' "three influences"; Gnomon's anchor frames then timing proxies). The harness
is that block-in, made honest: same camera, same scale, same gameplay shape and the cost of every
option on screen, so the user never picks something that cannot ship.

## When

| Situation | Do |
|---|---|
| look undecided, no or loose references | this skill, then `vfx-core` + the domain skill builds the pick |
| a clear reference image or video the user wants matched | skip; `vfx-core` Rule 0 reference breakdown, then build |
| the user names an exact recipe ("ring + sparks, orange") | skip; build |
| a UI moment made of tweens (panel pop, button press, counter) | `ui-brainstorm` (unity-ui) |
| a UI moment made of particles or shader glow (reward burst, rarity aura) | this skill with `view:'side'`, `char:null`, `overlay:null`, 1 preview metre = 100 canvas units |
| the look lives in a surface shader on a mesh (hologram fabric, dissolve on a garment, water surface) | route B below |

## Non-negotiables

1. **Same invariants, different looks.** Camera, scale, gameplay shape, hit time and duration are
   shared by all 4 options (they live in `SETUP` and `beats`). Only the look varies. An option that
   changes the radius or the hit time is a design change: ask, do not sneak it in.
2. **4 options that differ on at least 2 axes each** (`reference/vfx-vocabulary.md` -> Divergence).
   Draft 6-8 one-line theses first and keep the 4 most different; the first ideas are the safe ones.
   **Grey test**: picture the 4 climax frames in grayscale - if two read alike, replace one.
3. **The spec is the build.** Each option's `spec` is a `vfx-apply` spec (`vfx-core/reference/vfx-tools.md`)
   plus three preview-only keys (`uber`, `path`, `face`). Write the layer plan - role, mesh or
   particle, texture, blend, timing - before the spec. If a layer has no Unity construction you can
   name, it is not an option.
4. **Honest numbers stay visible.** Never trim an option to hide its FSE or alive count; label it
   (`flags:['hero tier']`) or replace it. Bloom starts in the state of the TARGET tier
   (`SETUP.bloom`): if the weakest device has no Bloom, every option must read with it off.
5. **Look at it before the user does.** Screenshot the sheet (step 5) and read it. A blank cell, a
   red chip or a primary edge that misses the gameplay shape is fixed before the file is shown.
6. **One file, zero network.** Copy `assets/template.html`, edit only `SUBJECT`, `SETUP`, `OPTIONS`.
   Never rewrite the harness, never add a CDN.

## Workflow

### 1. Brief, the short version (no tool call if the request answers it)

From `vfx-core` Rule 0 take only what the preview needs: **purpose** (one sentence the player must
understand), **binding** (radius / cone / box, hit time, duration - from code), **camera** (view,
pitch, metres visible on the phone), **style family and palette** (from neighbouring effects),
**importance and tier** (budget role, Bloom on the weakest device). Ask what none of the request, the
code and the project answer, in ONE `AskUserQuestion`. Never ask "what style do you want" -
producing 4 styles is the job.

### 2. Ground it (3 tool calls at most, skip what is already known)

- Gameplay values: `vfx-combat` Rule 0 grep (`OverlapSphere|radius|angle|AnimationEvent|OnHit`) -> `SETUP.overlay`, `beats`.
- Camera: the gameplay camera's rotation and orthographic size or FOV and distance -> `SETUP.view`,
  `pitch`, `frame` (metres across the screen). Unknown: say which one you assumed on the card subject.
- Style: `vfx-read` (or a `Glob` of the VFX folder) on 1-2 neighbouring effects for palette, blend
  and construction, so the options sit in the project's family. Reference images: one breakdown row
  each (`vfx-core/reference/brief.md`), with its "take".

### 3. Diverge

For each kept option write, in this order: **thesis** (3 words), **primary verb** (expand, billow,
erupt, fall, sweep, swirl, converge, shatter, flicker, rise, drift, bloom), **reads as** (what the
player understands in one glance), **layer plan**, **cost guess** (mesh-first is cheap, stacked soft
billboards are not). Put one option on the cheap end on purpose: it is often the right answer on
mobile, and the user only learns that by seeing it next to the loud one.

### 4. Build the file

Copy `${CLAUDE_SKILL_DIR}/assets/template.html` to the scratchpad as `vfx-<subject>.html` and
replace the top block. The shipped demo (fire AoE, 4 directions) shows every feature; delete it.

- `SETUP`: the shared camera, frame, aspect, origin, target Bloom, budget role, the game's darkest /
  typical / brightest ground colors, the character for scale, the gameplay shape.
- Each option: `key, name, desc, read, cycle` (seconds, >= active length + 0.5 s of stillness),
  `beats` (`antic:[s,e]`, `impact`, `end` - drawn on the timeline), `spec`, `unity` (one line: how it
  is built), optional `flags` (chips), `sheetTimes`, `draw(ctx,t,V)` (route A escape hatch, below).
- Spec subset the harness simulates and its encodings: `reference/vfx-vocabulary.md` -> Spec subset.
  Anything outside it shows as a chip ("collision not previewed") instead of failing.

### 5. Self-check (before the user sees it)

```bash
E="/c/Program Files (x86)/Microsoft/Edge/Application/msedge.exe"   # or Chrome
F="file:///<absolute path>/vfx-<subject>.html"
"$E" --headless=new --disable-gpu --hide-scrollbars --window-size=1400,1150 --virtual-time-budget=3000 --screenshot="<scratch>/sheet.png" "$F?sheet"
"$E" --headless=new --disable-gpu --hide-scrollbars --window-size=1400,1150 --virtual-time-budget=3000 --screenshot="<scratch>/sheet-hard.png" "$F?sheet&heat&bg=bright&nobloom"
```

Read both PNGs. Check: no red chip or crash text; every option has particles in its cells; the
primary edge sits on the red gameplay outline at the impact cell (0.95-1.1 of it); the 4 options
look different in the sheet; the second shot still reads on the bright ground without Bloom, and the
heatmap shows where each one pays. Fix, re-shoot, then show. URL switches: `sheet`, `heat`, `grey`,
`nobloom`, `close`, `bg=dark|mid|bright`, `copies=5`.

### 6. Show and pick

Open it for the user (`Invoke-Item "<path>"` on Windows, `open` / `xdg-open` elsewhere). In chat, one
short block: letter, name, thesis, FSE vs budget - in the user's language. Then the pick checklist,
one line: *pause on the hit frame - does the still alone say what and where? press grey, bloom off,
x5 on bright ground - does it still read?* Finish with `AskUserQuestion` A / B / C / D; mixes ("A's
ring with C's shards, slower") come through Other and are welcome.

### 7. Iterate, then lock

- A tweak or a mix edits the SAME file (merge emitters by name for a mix) and re-runs step 5. A new
  file breaks the comparison the user is making.
- Three rounds without convergence: stop tuning, ask which single quality is missing (shape,
  timing, color, weight) and rebuild only that axis as 4 new options.
- **Lock**: write the contract - next to the effect when the project keeps briefs
  (`<EffectFolder>/BRIEF.md`), else the scratchpad - with the brief, the picked option's spec, beats,
  the sheet times and the FSE estimate, and keep the HTML beside it. Then hand over to `vfx-core` +
  the domain skill: translate the spec (table below), build with `vfx-apply` or
  `vfx-build-template.cs`, capture with `vfx-capture.cs` using the sheet's times (`Times = {...}`),
  measure with `vfx-measure.cs`. The preview sheet is the reference image for `review-loop`'s visual
  track: compare per cell, timing and silhouette first, brightness last.

## Preview -> Unity translation

| Preview | Unity |
|---|---|
| `material:'glow/add'` on particles | same in `vfx-apply`; for the uber look a `.mat` on VFX/Uber with `T_VFX_<Tex>`, `_AlphaBlend` 0 add / 1 alpha |
| textures `glow circle ring spark star trail` | `vfx-texture.cs` jobs of the same name |
| textures `dot smoke shard petal flare` | `dot` = small `circle`; the rest: the project's own textures, or add a job to `vfx-texture.cs` |
| `renderer.mesh:'ring:0.8'`, `'arc:180:0.55'`, `'cone:0.1'`, `'dome'`, `'quad'`, `'disc'` | `vfx-mesh.cs` job with that shape and Inner / Arc / TopRadius (`disc` = ring Inner 0); `renderer.mesh` = the asset path; ground meshes `alignment: World` |
| `face:'camera'` on a mesh | `renderer.alignment: View` |
| Mesh `startSize` | the same number: unit meshes, size = radius (or `startSize3D` for pillars) |
| `uber.erode` curve over life | Custom Data Custom1.x curve + streams per `vfx-core/reference/uber-material.md`, `_NoiseOn` |
| `uber.erosion` | material `_Erosion` |
| `uber.edge`, `uber.edgeWidth` | `_EdgeColor`, `_EdgeWidth` |
| `uber.ramp` gradient | ramp texture (`vfx-texture.cs` ramp job) + `_RampOn` |
| `uber.intensity` curve | Custom1.y |
| `path:{to,time,ease}` | not in the prefab: gameplay moves the effect (projectile visual = child of the gameplay projectile, `vfx-combat` Rule 2) |
| `path.clear` | gameplay stops it on arrival: `Stop(true, StopEmittingAndClear)` and back to the pool |
| HDR `#RRGGBB*k` | the same encoding |

Not previewed, judge in the game view: lighting, soft particles (URP Depth Texture), distortion
(Opaque Texture), UV panning `_MainPan`, flipbooks, collision, Max Particle Size clamping, sorting
against scene geometry. Canvas blends in gamma space, so stacked additive reads hotter than Unity's
linear blending: expect to raise intensity in Unity, never to cut layers to match the preview.

## Route B - when the browser cannot show the look

Material looks on a real mesh (garment shimmer, hologram, dissolve swap, water surface) and effects
whose look is mostly the project's own painted textures do not survive a 2D simulation. Two options:

- **Sketch layer**: an option may carry `draw(ctx, t, V)` - plain canvas drawing on top of the
  particles (`V.P(x,y,z)` -> screen, `V.ppm`, `V.fill(px)` adds its fill to the FSE). Good for a
  shine sweep, rim pulse or dissolve wipe over a silhouette. The card gets a "custom draw" chip:
  that layer has no spec and is built by hand.
- **In-engine block-ins** (Editor bridge connected, user agrees): build the 4 options as rough
  prefabs with `vfx-apply` in a scratch folder the user names, capture each with `vfx-capture.cs`
  from the gameplay camera at the same times, and show the 4 PNGs in one page. Slower and it creates
  assets; ask before, and ask before deleting them after the pick.

## Traps

- **Cycle shorter than the effect** cuts the dissipation and hides the tail; the card warns. A tail
  longer than 0.5 s after `beats.end` warns too.
- **Close zoom flatters.** Pick at gameplay zoom; close zoom is for checking construction.
- **The meteor problem**: a flight path is gameplay timing. Put it in `beats.antic` and flag it.
- **Copies are where soft billboards die.** Five fireballs that look rich alone become a white blob;
  that is the moment to show the user `x5`.
- **Flat meshes spin with `startRotation3D` + `startRotationY`**, not `startRotation` (that tilts them
  in Unity); the card warns.
- **Do not publish the file as an Artifact** unless the user asks for a link. A local file opens
  instantly and edits in place.

## Files

- `assets/template.html` - the harness: Shuriken subset simulator, iso/side/top camera, gameplay
  outline, character scale, timeline beats, FSE estimate, overdraw heatmap, bloom from HDR excess,
  grayscale, x5 copies, sheet mode with the same cell layout as `vfx-capture.cs`, copy-spec button.
  Ships with a 4-option demo; replace the top block only.
- `reference/vfx-vocabulary.md` - divergence axes, per-domain option menus, the spec subset and
  encodings, and the pick checklist.
