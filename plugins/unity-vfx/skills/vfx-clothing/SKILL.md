---
name: vfx-clothing
description: Clothing and outfit VFX in Unity - effects on garments and characters for fashion, dress-up and customization games - sequin and glitter sparkle, holographic and iridescent fabric, rim and sheen, scrolling emissive patterns, dissolve outfit swaps, reveal moments, idle shimmer, auras, bokeh and petals around the character, particles emitted from the garment surface. Holds the choice between a material effect and attached particles, how to bind emitters and overlays to a SkinnedMeshRenderer so they follow every pose and body type, the rarity effect ladder, reveal and idle timing, cloth-simulation interplay, and the extra cost of overlay passes and skinned emission. Invoke with vfx-core for any VFX on an outfit or character - "hiệu ứng trang phục", "hiệu ứng quần áo", "outfit vfx", "lấp lánh", "kim tuyến", "sequin", "hologram", "aura nhân vật", "đổi đồ", "reveal outfit".
---

# Clothing VFX

Invoke `vfx-core` first. Outfit effects have one rule above the others: **the outfit is the
subject, the effect serves it.** Idle effects stay subtle so the garment's design reads; the big
moments are short and deliberate (reveal, evolve, equip). They are also seen up close, in a
showroom camera, for long stretches - so screen coverage and fill matter more than in gameplay.

## Rule 0 - The clothing brief

Add to the `vfx-core` brief:

| Field | Why |
|---|---|
| the garment(s): which SkinnedMeshRenderer, submeshes, materials, texture | effects bind to these |
| rarity / tier and the moment: idle, equip, reveal, evolve, try-on swap | the ladder below |
| the showroom / dressing camera: framing, distance, post-processing volume (Bloom?) | close-up coverage, glow |
| body types, blend shapes, poses and animations the outfit is shown with | must hold on all of them |
| cloth simulation on the garment (Magica Cloth, Unity Cloth, bone chains) | what the effect can bind to |
| the garment's design language: material (silk, sequin, denim, armour), palette, motifs | the effect extends it, never fights it |
| how many outfit effects at once (a lobby of 20 characters?) | per-character budget and LOD |

Look at the garment first (a screenshot in the showroom camera) and name what the effect must
emphasise: the trim, the hem, the fabric surface, the silhouette.

## Rule 1 - Material effect or attached particles

| Want | Use |
|---|---|
| the fabric itself shines, sparkles, shifts color, glows in a pattern | **material effect** on the garment |
| something around or leaving the garment: aura, motes, petals, butterflies, trailing sparkles | **attached particles** |
| a transition: swap, reveal, evolve | material dissolve + a short particle burst at the edge |

Material effects, in order of preference:
1. **Feature of the garment's own shader** (`shader_feature_local` toggles: GLITTER, IRIDESCENT,
   RIM, EMISSIVE_SCROLL, driven by mask channels - e.g. R glitter area, G sheen, B emissive pattern).
   No extra draw, no re-skinning. Keep the feature count small (4 features = 16 variants per pass)
   and strip unused ones.
2. **Extra material slot**: a renderer with more materials than submeshes draws the LAST submesh
   again with the extra material - an additive shell pass without skinning again. Only covers the
   last submesh.
3. **Overlay skin**: a second SkinnedMeshRenderer with the same mesh and bones and the effect
   material (`reference/garment-effect.cs`). Works on any garment without touching its material,
   but skins the mesh again and adds a draw - reserve it for high tiers and temporary moments.

## Rule 2 - Binding to the body

- Effect prefabs sit as a child of the garment's SkinnedMeshRenderer (or a bone socket) and bind at
  `Initialize`: emitters with Shape = SkinnedMeshRenderer get the garment at runtime
  (`reference/garment-effect.cs`), never a mesh baked into the prefab.
- **Emit from a low-poly proxy**, not the full garment: a small mesh skinned to the garment's bones
  (just the trim, the hem, the shoulders), or limit emission to one submesh. Skinned emission samples
  the skinned result; cost grows with vertex count. Keep the emitting mesh Read/Write enabled - the Shape module needs CPU access to it.
- Emission 5-30 particles/s per emitter; World simulation space so sparkles trail behind motion.
- Auras and bokeh: parented to the hips (or root) bone, shape sized from the garment or body bounds
  at `Initialize`, not hard-coded offsets - so it fits every body type.
- Patterns (sweeps, emissive bands) in object space between the garment's bounds or in UV space:
  feed the bounds through a MaterialPropertyBlock at bind time so a sweep runs from hem to collar on
  every garment. Blend shapes and body types must not shift the pattern.
- **Cloth simulation**: shader effects follow simulated cloth for free. Particles bind to a
  SkinnedMeshRenderer (bone-driven cloth is fine); mesh-deforming cloth output may not feed the Shape
  module - emit from the bones or a proxy instead. Profile simulated cloth on device (it often runs
  2-3x slower in the Editor than in builds, and mesh cloth is heavy on low-core phones).

## Rule 3 - The look

- **Glitter / sequins**: per-cell random normal in a cell pattern (hex or circle cells in UV),
  reflect the light or view and keep only well-aligned cells; push those above 1 so Bloom haloes
  them. Sparkle must be stable - tied to view/light angle, never to per-frame noise - and twinkle
  slowly. Cell size from texture tiling; check it at the showroom distance and at gameplay distance.
- **Iridescent / holographic**: a hue ramp indexed by N.V (plus a thickness or noise texture for
  variation); cheap and art-directable. Physically based thin-film only when the project asks.
- **Rim / sheen**: `pow(1 - N.V, 3-5)` x color; silk and satin get a soft anisotropic highlight.
- **Emissive patterns**: UV-panned mask x emission x a slow pulse; on packed garment UVs use a second
  UV set or object space so the pattern does not jump across islands.
- **Without Bloom** on the target tier the halo must be in the texture or an additive card - an HDR
  value alone will not glow.
- Palette from the garment: the effect picks up 1-2 colors of the outfit plus a near-white highlight.

## Rule 4 - Rarity ladder and moments

| Tier (adapt names) | Idle | Moments |
|---|---|---|
| common | nothing | none |
| rare | rim or sheen | small sparkle on equip |
| epic | glitter or iridescence + a shimmer pulse every 4-6 s | equip burst |
| legendary / evolved | emissive pattern + aura particles + shimmer | signature reveal |

- **Signature reveal** (1.5-3 s, skippable): camera push-in, dissolve or bloom swell over the
  garment, a burst at the edge, then settle into the subtle idle. Main action in the first ~25%.
- **Outfit swap**: dissolve driven by world height + noise (bottom-up or top-down reads the same on
  every garment), old outfit 1 -> 0 while the new one goes 0 -> 1 over 0.6-1 s, a sparkle burst
  riding the band, HDR edge color. Alpha-clip costs on mobile (late depth test): switch back to the
  opaque, non-clip material when the transition ends.
- **Idle**: low alpha, slow, sparse; never more motion than the character's own idle animation.
  Something still moves (a shimmer passes, a mote drifts) so the outfit feels alive.

## Rule 5 - Cost

- Budget per character by tier: the showroom shows one character up close (full-body framing is
  your worst case - measure with `vfx-measure.cs` using the showroom camera); lobbies and gameplay
  show many - lower tiers of effect (idle off, `_Lite`) beyond the first few characters or past a
  distance.
- Overlay skins and skinned emission are per-character costs: count them for the lobby case.
- Glitter cells and patterns are shader math on the garment, usually cheaper than particles - but
  every overlay pass doubles the fragment cost on that garment's screen area.

## Verify

1. `vfx-audit.cs` 0 ERR (data textures sRGB off; Mesh-shape emitters Read/Write). Skinned emitters
   bound at runtime are not visible to the audit: check their mesh import by hand.
2. Showroom camera capture at idle and at the reveal peak; `vfx-measure.cs` from that camera.
3. Play mode (`verify`): every body type and the outfit's poses/animations - emitters stay on the
   garment, patterns do not swim, aura fits the body; swap in and out repeatedly (re-entry: no
   duplicate overlay skins, no leaked emitters).
4. The lobby / crowd case with the real number of characters. Report what was not checked.

## Mistakes

| Mistake | Looks like | Fix |
|---|---|---|
| emitter mesh baked into the prefab | particles float off the moving body | bind the SkinnedMeshRenderer at runtime |
| emitting from the full high-poly garment | CPU cost per character | low-poly proxy skin or one submesh |
| mesh not Read/Write | no particles, console warning | enable Read/Write on the emitting mesh |
| aura offsets hard-coded | wrong on other body types | size from bounds, parent to a bone |
| sparkle from per-frame noise | sizzling, unstable glitter | sparkle by view/light angle per cell |
| overlay skin on every tier | double skinning in the lobby | garment shader feature or extra material slot |
| loud idle effect | the effect, not the outfit, is what players see | quiet idle, loud only on moments |
| dissolve material left after the swap | alpha-clip cost forever | back to the opaque material |
| pattern in packed UV space | bands jump between UV islands | object space or second UV set |
| HDR glow on a tier without Bloom | flat, no halo | halo in the texture or an additive card |

## Files

- `reference/garment-effect.cs` - runtime glue: binds skinned emitters to the garment or a proxy
  skin, builds an overlay skin, feeds garment bounds and texture to the overlay material.
