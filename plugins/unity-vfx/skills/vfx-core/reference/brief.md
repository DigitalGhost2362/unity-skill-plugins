# VFX brief

Fill this before any build. Most fields come from the request, the code and the references; ask
the user only for the rest, in one question. Keep the filled brief in the chat or next to the
effect (`<EffectFolder>/BRIEF.md` when the project keeps briefs) - every later check compares
against it.

```
Effect:        VFX_<Domain>_<Name>
Domain:        combat | ui | environment | clothing | other
Purpose:       what the player must understand, in one sentence
Binding:       data it must match + where it lives in code (config key, field, animation event)
               e.g. radius = SkillConfig.aoeRadius (m), hit frame = AnimationEvent "OnHit"
Camera:        gameplay camera, distance or zoom range, how big it reads on a phone (% of screen height)
Style family:  toon/anime | stylized | realistic | <project's own>
Keywords:      3-5, e.g. "crisp, cel-shaded, hard eroded edges, bright core, no soft smoke"
Palette:       2-3 colors + near-white core, team/rarity color rules if any
References:    file or link -> what to take from it (shape only / timing only / palette only)
Timeline:      anticipation 0-x s | impact/peak at y s | dissipation to z s | loop or one-shot
Importance:    1-10 (frequent and idle dim, rare and dangerous bright); copies on screen worst case
Target:        lowest device tier, pipeline, Bloom on that tier?, compute available?
Budget:        peak FSE, p95 layers, peak alive, draw calls (from the vfx-core table or project profile)
Acceptance:    e.g. edge matches the hit radius gizmo; reads on the darkest and brightest map; in budget
```

## Style families

Decide once per project and write it down; a new effect follows the family of its neighbours.

| Family | Shapes and textures | Edges | Motion | Mobile cost |
|---|---|---|---|---|
| toon / anime | hand-drawn or generated flipbooks 4x4-8x8 at 12-24 fps, 2-3 tone ramps, cel smoke | hard, alpha-eroded | snappy, held frames on 2s/3s, big anticipation | low |
| stylized | meshes with panning masks + erosion, painted-looking ramps, few soft particles | mixed: hard silhouettes, soft glow | ease-out, overshoot, readable arcs | low-medium |
| realistic | soft smoke flipbooks, motion vectors, lit particles, distortion | soft | physical, drag, gravity | high - avoid on low tiers |

Record per project: value range (VFX above characters in value and saturation, just below the UI),
smooth gradients in the darks vs crunched gradients in the highlights, thick-thin shape mix, the
dominant hue per element or team, and the rules that may be broken on purpose (ultimates).

## Reference breakdown sheet

One row per reference. A reference without a "take" column is decoration, not direction.

| Ref | Take | Phases (frames @fps -> s) | Silhouette | Colors | Value | Edges | Notes |
|---|---|---|---|---|---|---|---|
| boss_slam.mp4 | timing | wind-up 0-14, impact 15, settle 16-40 @30 -> 0.47/0.03/0.8 s | flat ring + vertical spikes | orange, dark red, white core | dark body, white rim | hard | camera shake on 15 |

Retiming: keep the ratios (wind-up : impact : dissipation), scale to the gameplay duration.

## Naming and folders

Follow the project's existing layout first. Otherwise:

```
Assets/VFX/
  _Shared/Shaders  _Shared/Textures  _Shared/Meshes  _Shared/Materials
  Combat/<Effect>/   UI/<Effect>/   Environment/<Effect>/   Clothing/<Effect>/
```

- Prefab `VFX_<Domain>_<Name>`, material `M_VFX_<Name>_<Layer>`, texture `T_VFX_<Name>_<Type>`
  (`_Mask`, `_Noise`, `_Ramp`, `_Flip4x4`), mesh `MESH_VFX_<Name>`.
- Child objects named by role: `Core`, `Glow`, `Body`, `Sparks`, `Smoke`, `Ring`, `Decal`, `Trail`.
- Variants as Prefab Variants: `_Lite` (low tier / far LOD), `_Ally` / `_Enemy`, rarity tiers.
