---
name: vfx-ui
description: UI VFX in Unity uGUI - particles and shader effects inside a Canvas - reward bursts, coin/gem fly to a counter, rarity glows and shine sweeps, button and unlock feedback, level-up and gacha reveals, popup sparkle, dissolve and transition effects. Holds how to get particles to render in a Canvas (UI particle package, camera-canvas sorting sandwich, or tween-only), UI shaders and masking, effects that keep running while the game is paused, aspect-ratio and scaler independence, rarity tier ladder, and UI overdraw. Invoke with vfx-core for any VFX that lives in the UI - "hiệu ứng UI", "particle trên UI", "coin bay", "glow item hiếm", "shine", "reward effect", "hiệu ứng mở rương". Choosing the look of a UI animation first is the brainstorm skill (unity-ui); panel wiring is ui-panel.
---

# UI VFX

Invoke `vfx-core` first. UI effects keep its rules (layers, motion, one material per look, budgets)
but render through a Canvas, which changes how particles draw, sort, clip and scale. If the look is
not decided yet, the `brainstorm` skill (unity-ui plugin, if installed) picks it in an HTML preview
first; `ui-panel` owns the panel code the effect plugs into.

## Rule 0 - The UI brief

Add to the `vfx-core` brief:

| Field | Why |
|---|---|
| canvas render mode of the target panel (Overlay / Camera / World) | decides the render route below |
| CanvasScaler reference resolution and match, orientation | effects are authored in canvas units |
| the moment (open, press, gain, unlock, reveal, idle, rarity) and its length | UI timing band: response under 0.3 s, celebrations up to ~1.2 s and skippable |
| does it run while the game is paused (`Time.timeScale = 0`)? | unscaled time everywhere |
| masking: inside a scroll view, under a Mask? | clipping support needed |
| rarity / tier, if any | the ladder below |
| how many at once (a grid of 30 item cards?) | per-item effects must batch or share |

## Rule 1 - Pick the render route

Check `Packages/manifest.json` before promising particles:

| Route | When | How |
|---|---|---|
| **UI particle package** (`com.coffee.ui-particle`) | installed, or the user agrees to add it | ParticleSystem as a CHILD of a `UIParticle` component; renders through CanvasRenderer, sorts with the hierarchy, clips under Mask / RectMask2D with a UI shader. Auto Scaling follows the Canvas scale. Mesh Sharing lets many identical icons share one simulation. `UIParticleAttractor` pulls particles to a target with an `OnAttracted` callback per particle - the native coin-fly |
| **Camera-canvas sandwich** | no package; the canvas is Screen Space - Camera | particles render in world space in front of the UI camera; sort between canvases with separate Canvases (`overrideSorting = true`, distinct `sortingOrder`) and the renderer's sorting layer/order. Hierarchy order only sorts inside one canvas; each nested Canvas is a new batch root, so use few |
| **Tween-only** | Overlay canvas, no package, simple bursts | pooled Images driven by the project's tween library (scale, fade, path). Most "particle" UI moments (coin fly, sparkle pop, rays) are fine this way |

Never move a panel's canvas to a different render mode just to fit an effect without asking - it
changes every panel's layout and sorting.

## Rule 2 - UI materials and masking

- Canvas graphics need UI shaders: stencil support (Mask), clip-rect support (RectMask2D,
  `UNITY_UI_CLIP_RECT`), vertex color. The world uber shader (`VFX/Uber`) is not a UI shader.
- Alpha: `UI/Default`. Additive glow: an additive UI shader (the UI particle package ships samples;
  a project-made one must keep the stencil and clip-rect blocks of `UI/Default`).
- Sweeps, dissolve, outline, gradient, blur, transitions on Images and text: a UI effect package
  (`com.coffee.ui-effect`: Shiny sweep, Dissolve, Transition modes, a tweener with no code) when
  installed; otherwise a project UI shader. Ask before adding packages.
- **RectMask2D** (no stencil, no extra draw calls, rectangles only, has softness) for scroll views;
  **Mask** (stencil, any shape, breaks batching when nested) only for non-rectangular shapes.
- `sharedMaterial`, never `material` from code: `.material` clones it and breaks batching. One
  material per look; per-instance values through the shader's vertex color or the UI effect component.

## Rule 3 - Time, scale and layout

- **Paused game**: `main.useUnscaledTime = true` on every system, unscaled update on tweens
  (`SetUpdate(true)` in DOTween), `AnimatorUpdateMode.UnscaledTime` on animators, unscaled mode on
  attractors. Shader `_Time` keeps running on its own - fine for idle shimmer.
- **Author in canvas units** at the CanvasScaler reference resolution; let Auto Scaling (UI
  particle) or the canvas scale carry the effect to other resolutions. Never size UI particles in
  world metres.
- **Anchor the effect root to the element it decorates** (same parent, centred, stretch where it
  should cover), never to screen corners. Check the narrowest and widest aspect the game supports.
- Particles inside a scaled-down popup animation follow the scale only if the effect is a child of
  the animated RectTransform (and Auto Scaling or `scalingMode Hierarchy` is on).

## Rule 4 - The moments

| Moment | Build | Numbers |
|---|---|---|
| button press | scale 0.92-0.95 down, overshoot back; optional small ring or 4-6 sparks | total under 0.2 s, never blocks input |
| currency gain (coin fly) | 8-15 pooled icons, stagger 0.02-0.05 s, curved paths to the counter, each arrival punches the counter (1.0 -> 1.15 -> 1.0 in ~0.15 s) and ticks the number | 0.6-1.2 s total |
| item / reward reveal | flash, card flip or scale-in with overshoot, rarity-coloured burst behind, rays rotating slowly, icon settles and holds | 0.6-1.2 s, skippable |
| rarity idle | the ladder below | quiet; never pulls focus from the active UI |
| unlock | lock shakes then breaks, grey -> colour, burst | 0.5-0.8 s |
| level up / big win | full-panel flash (alpha <= 0.5, 1-2 frames), rays, number punch, confetti | <= 3 flashes/s, skippable |
| progress fill | leading glow on the fill edge, ticks pop as they pass | fill 0.3-0.5 s ease-out |

**Rarity ladder** (adapt the tier names to the project): common - nothing; uncommon - static tinted
frame; rare - shine sweep every ~3 s; epic - sweep + soft pulse; legendary - sweep + particles +
slow rotating rays. One material per tier so a grid of cards batches; stagger sweeps per card so a
grid does not flash in unison.

## Rule 5 - UI cost

- UI overdraw is real: full-screen images, dimmers and big glows stack. A full-screen dimmer +
  panel + big glow + particles is already 3-4 FSE in that area. Keep glows sized to the element,
  trim transparent margins from sprites, prefer one composed sprite over stacked transparent layers.
- Particles in a canvas mark the canvas dirty every frame: keep animated UI effects under their own
  child Canvas so the static panel is not rebuilt.
- A grid of animated cards: idle effects off for cards not on screen (scroll view culling), shared
  materials, staggered timing.
- `vfx-measure.cs` does not see Overlay canvases; measure a Camera canvas with the UI camera, or
  judge UI overdraw with the URP Rendering Debugger (overdraw mode) / Scene view overdraw.

## Verify

Play mode (`verify`): trigger the moment through the panel's real API; check it at
`Time.timeScale = 0` if it can happen paused; screenshots at the narrowest and widest supported
aspect; inside its scroll view, scrolled half out (clipping); with the real number of items on
screen. Report what was not checked.

## Mistakes

| Mistake | Looks like | Fix |
|---|---|---|
| ParticleSystem dropped into an Overlay canvas | invisible, or always behind/above the UI | UI particle package, or a camera-canvas sandwich |
| world particle shader inside a Canvas | ignores Mask / RectMask2D, wrong sorting | UI shader with stencil and clip rect |
| effect frozen on the pause screen | reward plays only after unpause | unscaled time on systems, tweens, animators |
| sizes in metres / authored at one resolution | tiny or huge on other devices | canvas units + Auto Scaling / canvas scale |
| `image.material` set from code | every item its own draw call | `sharedMaterial`, per-item values via component |
| all rarity sweeps in sync | the grid strobes | stagger the start per card |
| big additive glow over a full-screen dimmer | fill spikes, washed-out panel | size glows to the element |
| animated effect on the main panel canvas | whole panel rebuilds every frame | child Canvas for the effect |
