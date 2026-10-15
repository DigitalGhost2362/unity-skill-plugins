# VFX/Uber material

`assets/VFX_Uber.shader` - URP, unlit, transparent, premultiplied output. Works on particles
(billboard, stretched, mesh), MeshRenderers, Trail and Line renderers. Not a uGUI shader: inside a
Canvas use the UI shaders from `vfx-ui`.

## Properties

| Property | Use |
|---|---|
| `_MainTex` | RGB color, A shape. Generated shapes are white RGB with the shape in alpha |
| `_MainPan` | UV scroll, units per second (xy). The backbone of every mesh effect |
| `_Color`, `_Intensity` | HDR tint and multiplier. Glow without Bloom = brighter core in the texture, not a higher multiplier |
| `_AlphaBlend` | 0 additive, 1 alpha blended, between = both. One shader for both looks, so they batch |
| `_AlphaPower` | contrast of the shape alpha (>1 tighter, <1 fatter) |
| `_CustomData` | 1 = read the particle streams below; 0 on MeshRenderer, Trail/Line and Trails-module materials |
| `_RampOn` + `_RampTex`, `_RampOffset` | color from the ramp: the shape alpha picks the color (edge = left of ramp, core = right). Texture RGB ignored |
| `_NoiseOn` + `_NoiseTex`, `_NoisePan`, `_Distortion` | noise (R channel, sRGB off) drives erosion and UV distortion; offset per particle by StableRandom |
| `_Erosion`, `_ErosionSoftness` | dissolve threshold (material base, Custom1.x adds to it), edge softness |
| `_EdgeWidth`, `_EdgeColor` | burn edge band just ahead of the erosion |
| `_MaskOn` + `_MaskTex` | static mask on the raw UV (not panned): keeps the ends of a trail or slash soft while the main texture scrolls |
| `_PolarOn` | UV becomes (angle, radius): a square texture wraps into a ring or vortex on a quad |
| `_SoftOn`, `_SoftDistance` | soft intersection with the scene. Needs the URP Depth Texture - on tile GPUs that costs; mobile prefers an offset along the normal |
| `_CameraFadeOn`, `_CameraFadeNear`, `_CameraFadeRange` | fades particles that reach the camera. Hides the look, not the cost: still cap size |
| `_Cull`, `_ZTest` | per material |

## Vertex stream contract

Particle System Renderer > Custom Vertex Streams, exactly in this order:

| Stream | Lands in | Used as |
|---|---|---|
| Position | POSITION.xyz | |
| Color | COLOR | tint and alpha over life |
| UV | TEXCOORD0.xy | base UV (sub-UV when Texture Sheet Animation is on) |
| Custom1.xyzw | TEXCOORD0.zw, TEXCOORD1.xy | x erosion add, y intensity add, zw main UV offset |
| StableRandom.x | TEXCOORD1.z | noise offset per particle |

A MeshRenderer, Trail/Line renderer or the Trails module has no custom streams, and missing vertex
inputs are not 0 (`.w` reads 1, a missing TEXCOORD1 reads whatever is bound - observed as a
shifted texture). Their materials set `_CustomData` to 0; particle materials keep 1. That makes a
mesh material and a particle material separate assets even when they look alike. Every system using
the shader carries the SAME stream list, or they stop batching. `vfx-audit.cs` reports a mismatch; `FixUberStreams = true` rewrites it.

## Custom Data setup (script-execute)

```csharp
var cd = ps.customData;
cd.enabled = true;
cd.SetMode(ParticleSystemCustomData.Custom1, ParticleSystemCustomDataMode.Vector);
cd.SetVectorComponentCount(ParticleSystemCustomData.Custom1, 4);
// x: erosion 0 -> 1 over life, slow start then fast (eats the particle away at the end)
cd.SetVector(ParticleSystemCustomData.Custom1, 0, new ParticleSystem.MinMaxCurve(1f,
    new AnimationCurve(new Keyframe(0, 0, 0, 0), new Keyframe(0.6f, 0.15f), new Keyframe(1, 1, 2.5f, 0))));
// y: intensity burst at birth, gone by 15% of life
cd.SetVector(ParticleSystemCustomData.Custom1, 1, new ParticleSystem.MinMaxCurve(2f,
    new AnimationCurve(new Keyframe(0, 1, 0, -6), new Keyframe(0.15f, 0, 0, 0))));
// zw: random texture offset per particle (random between two constants)
cd.SetVector(ParticleSystemCustomData.Custom1, 2, new ParticleSystem.MinMaxCurve(0f, 1f));
cd.SetVector(ParticleSystemCustomData.Custom1, 3, new ParticleSystem.MinMaxCurve(0f, 1f));

var r = ps.GetComponent<ParticleSystemRenderer>();
r.enableGPUInstancing = false;
r.SetActiveVertexStreams(new List<ParticleSystemVertexStream> {
    ParticleSystemVertexStream.Position, ParticleSystemVertexStream.Color, ParticleSystemVertexStream.UV,
    ParticleSystemVertexStream.Custom1XYZW, ParticleSystemVertexStream.StableRandomX });
```

Variant sheets (several shapes in one texture): use Texture Sheet Animation with Start Frame random
between two constants and Frame over Time constant 0 - one fixed random cell per particle. The UV
offset in Custom1.zw is for continuous offsets (noise, tiling streaks), not for picking cells.

## Material recipes

Set keywords on the material asset (`m.SetFloat("_NoiseOn", 1); m.EnableKeyword("_NOISE_ON");`),
never at runtime. One material per look, shared by every effect that has that look.

| Look | Mesh / render mode | Settings |
|---|---|---|
| shockwave | ring mesh, scale up ease-out | ring or trail texture, pan V outward 0.5-1.5, `_AlphaBlend 0`, ramp hot->dark, erosion via Custom1.x or material |
| slash / swipe | arc mesh as Mesh particle | trail texture, pan U along the arc fast (2-4), mask on for soft ends, erosion 0->1 over 0.15-0.3 s |
| tornado / beam / pillar | cone or cylinder | noise on, pan V up, distortion 0.05-0.15, mask fades top and bottom |
| shield / dome | dome | polar off, fresnel-like rim baked in the texture or a ring texture, pan U slowly, alpha 0.3-0.6 |
| fire body | billboards, 6-12 | smoke/noise shape, ramp dark red -> orange -> pale yellow, `_AlphaBlend 0.3`, erosion over life, size grows |
| sparks | Stretched Billboard | spark texture, additive, no noise, size shrinks |
| dark smoke | billboards, few | `_AlphaBlend 1`, dark tint, erosion over life, slow growth |
| ground telegraph | quad (polar for rings) | see `vfx-combat` - fill by `_Erosion` or a dedicated progress value |
| magic circle | quad, `_PolarOn` | a strip texture wrapped by polar UV, pan U to rotate |
