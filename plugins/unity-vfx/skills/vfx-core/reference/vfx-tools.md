# vfx-* Editor bridge tools

Some Editor MCP bridges ship five VFX tools. Use them when the tool list has them (they may ship
disabled: enable once with `tool-set-enabled-state` for `vfx-apply, vfx-read, vfx-preview,
vfx-texture, vfx-history`). Without them, build with `script-execute` (`build-snippets.md`).

| Tool | Use | Cost |
|---|---|---|
| `vfx-texture` | list / create procedural textures | text |
| `vfx-apply` | create or patch a prefab from a JSON spec; returns version + stats | text (image with `preview=sheet`) |
| `vfx-read` | current prefab as a compact spec (non-defaults only) | text |
| `vfx-preview` | stats + one labelled contact sheet (default 8 cells, 192 px) | ~1k tokens image; `statsOnly` = text |
| `vfx-history` | list / score / restore versions | text |

The prefab is the source of truth; humans keep editing it in the Inspector. `vfx-read` before
patching. `vfx-preview` "fill" is a world-space estimate (sum of particle size squared over the
frame area), not pixel overdraw, and ignores meshes and blend - budgets come from
`scripts/vfx-measure.cs`.

## Loop with the tools

1. `vfx-texture action=list` once; create only what is missing.
2. One `vfx-apply` with the full spec and `replace=true`. Read the stats only.
3. Fix numbers from stats, no image: emitters that never emit, wrong length, peak alive over budget.
4. One `vfx-preview` (note the returned `frame=` and pass it to later previews so scale stays
   comparable; `bg=#9A9A9A` once if the effect has alpha layers). Effects on a model:
   `context=<model prefab>`. Final look: place an instance in a scene with the game's post and use
   `camera=<name>`.
5. Critique per cell against the brief, at most 3 problems, patch only those fields.
6. `vfx-history action=score` (0-1 + note) each viewed version; `restore` the best if the last regressed.

## Spec

```json
{
  "defaults": { "main": { "duration": 0.5 } },
  "root": { "main": { "loop": false } },
  "emitters": [
    { "name": "Core", "material": "vfx_glow/add", "pos": [0,0,0],
      "main": { "startLifetime": [0.15,0.25], "startSpeed": 0, "startSize": [1.2,1.6], "startColor": "#FFD27A" },
      "emission": { "rateOverTime": 0, "bursts": [[0,1]] },
      "shape": false,
      "sizeOverLifetime": { "size": "0:0.3,0.15:1,1:0" },
      "colorOverLifetime": { "color": "#FFFFFF@0,#FF8A00@0.6|1@0,0@1" } },
    { "name": "OldLayer", "remove": true }
  ]
}
```

- Emitters match by `name`; unknown names are created; `remove:true` deletes; `replace=true` rebuilds.
- Emitter keys: `name, parent, pos, rot, scale, material, trailMaterial, remove`, any module, `renderer`.
- Module and property names are the Unity scripting API names (`main, emission, shape,
  velocityOverLifetime, limitVelocityOverLifetime, inheritVelocity, forceOverLifetime,
  colorOverLifetime, colorBySpeed, sizeOverLifetime, sizeBySpeed, rotationOverLifetime,
  rotationBySpeed, externalForces, noise, collision, trigger, subEmitters, textureSheetAnimation,
  lights, trails, customData`). `{...}` enables a module, `false` disables it. Unknown keys come back
  as errors listing the valid names. `...Multiplier` folds into its curve; rotations are degrees.
- `renderer`: `renderMode` (Billboard|Stretch|HorizontalBillboard|VerticalBillboard|Mesh|None),
  `sortMode, sortingFudge, sortingOrder, minParticleSize, maxParticleSize, alignment, pivot, flip,
  lengthScale, velocityScale, cameraVelocityScale, normalDirection, allowRoll, mesh, maskInteraction,
  shadowCastingMode, receiveShadows, enableGPUInstancing`.
- `material`: `"<texture>/<blend>"` (blend `add|alpha|premul|mul`, one shared material per pair, on
  the URP Particles Unlit shader), a `.mat` path (use this for VFX/Uber materials), or `"none"`.
  A flipbook texture enables Texture Sheet Animation with its tiles.
- `emission.bursts`: `[[time,count]]` or `[[time,count,cycles,interval,probability]]`; count may be `[min,max]`.
- `subEmitters`: `{"list":[{"emitter":"Sparks","type":"Death","inherit":"InheritColor|InheritSize","p":1}]}`;
  give the target `emission: {rateOverTime:0}`.
- `root` is the controller ParticleSystem on the prefab root; `root.scale` resizes the whole effect.
- Effects on a model: `shape: {shapeType: Mesh|SkinnedMeshRenderer, mesh: "<fbx path>",
  meshShapeType: Triangle}` with `simulationSpace: Local` to follow it.

| Encoding | Forms |
|---|---|
| number curve | `2` or `[1,3]` random or `"0:0,0.2:1,1:0"` curve or `{"c":"curve","m":5}` or `{"min":"curve","max":"curve","m":2}` |
| curve keys | `t:v` auto-smooth, `t:v:inTangent:outTangent` explicit |
| color | `#RRGGBB`, `#RRGGBBAA`, HDR `#RRGGBB*3` |
| gradient | `#FFFFFF@0,#FF8000@0.5\|1@0,0@1` (color keys `\|` alpha keys), suffix `~fixed` |
| min-max gradient | `#hex` or `["#a","#b"]` or `"gradient"` or `["grad","grad"]` or `{"rand":"gradient"}` |
| vector / enum / flags | `[x,y,z]` or `"Sphere"` or `"InheritColor\|InheritSize"` |

New-emitter baseline (omitted by `vfx-read`): one-shot, duration 1, lifetime 1, speed 2, size 0.5,
rate 10/s, sphere r=0.1, scalingMode Hierarchy, `vfx_glow/add`.

Token rules: never dump effects with `gameobject-component-get` or `particle-system-get`; never
re-send unchanged emitters; stats before images; one contact sheet per iteration.
