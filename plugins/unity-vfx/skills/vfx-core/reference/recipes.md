# Layer recipes, timing, symptom table

Recipes are starting points written mesh-first; the domain skills add their own. Counts are per
instance on mobile. "add" / "alpha" = `_AlphaBlend` 0 / 1 on VFX/Uber, or the project's equivalents.

## Recipes

| Effect | Layers (role: mesh or particle, texture/blend, timing) |
|---|---|
| hit / impact (0.3-0.4 s) | Flash: 1 glow particle add, life 0.08-0.12, big, white -> color; Ring: ring mesh add, scale 0 -> 1 ease-out over 0.2 s, erode out; Sparks: 8-16 Stretched Billboard add, speed 6-12, gravity 0.5, life 0.2-0.4, shrink; optional Dust: 2-4 alpha puffs |
| explosion (1-1.5 s) | Flash; Fireball: 6-10 billboards, ramp dark red -> yellow, grow, erode; Shockwave: ring mesh flat on the ground; Smoke: 3-5 dark alpha, starts 0.1 s, rises, life 1-1.5; Debris/sparks with gravity; Scorch: dark ground quad, then a smaller hot core that fades first |
| slash (0.15-0.3 s, = the swing) | Arc mesh particle on the swing frame, U-pan + erosion 0 -> 1; thin bright edge arc on top; 4-8 sparks at the arc tip |
| projectile | Head: glow core, Local space, brightest point; Trail: Trails module or a stretched mesh, fades behind, direction readable in one still frame; Motes by `rateOverDistance`, World space; Muzzle: 0.1-0.2 s flash on the caster; Impact: the hit recipe at the contact point, up = surface normal |
| buff / aura (loop) | Ground ring mesh, slow U-pan; Column: cylinder mesh, noise pan up, alpha 0.2-0.4; Motes: 8-15/s glow add, noise, rise; start burst when it begins |
| heal | slow (under 60 BPM) green-white motes rising, plus/cross flares, soft ground ring, round shapes only |
| pickup / collect | short glow pop, small ring, 6-10 star sparkles spiralling (`velocityOverLifetime.orbitalY`) |
| fire (loop) | Flames: 6-12 billboards, ramp, cone up, speed 1-2, grow then erode; Core glow: 1 particle; Embers: 10-30 spark add, noise; Smoke above: 2-4 alpha |
| lightning | lightning flipbook add, few particles, life 0.05-0.1, repeated bursts via `cycles`; flash <= 3 per second |
| tornado / beam | cone or cylinder mesh, noise distortion, pan V; debris particles orbiting; ground ring |

## Timing

| Rule | Value |
|---|---|
| impact peak | by 0.05-0.1 s |
| main action | first ~25% of the effect's life, then a long dissipation |
| muzzle / flash life | 0.1-0.2 s |
| hit sparks life | 0.15-0.4 s |
| trail time of a fast projectile | 0.15-0.2 s |
| slash life | about the swing, 0.15-0.3 s |
| tail after the gameplay state ends | under 0.5 s, 0 particles after it |
| attack vs heal pace | attack faster than 60 BPM, heal slower |
| UI response | under 0.3 s for a click; celebrations up to ~1.2 s, skippable |

Curves: ease-out for size and speed (`0:0, 0.2:1, 1:0.8`), alpha fast in, hold, out to 0; sparks
shrink, fire and smoke grow; lifetimes, sizes and speeds as `[min, max]` ranges.

## Symptom -> cause -> fix

| Seen | Likely cause | Fix |
|---|---|---|
| layer never appears | rate 0 and no bursts; burst time > duration; sub-emitter parent never dies | add burst/rate, burst inside the duration, check the sub-emitter list |
| everything appears at once and vanishes | life too short, no size/alpha curves | longer life, curves |
| hard square edges | wrong texture or blend, material missing | soft shape texture, check the material |
| lemon yellow instead of orange | HDR multiplier pushed green over 1 | lower the multiplier or the green channel |
| glow flat, no halo | no Bloom in the preview or on the tier | judge in the game camera; without Bloom bake the halo into the texture |
| washed-out white blob | too many additive layers stacked | fewer/smaller, darker colors, move one layer to alpha |
| invisible on bright ground | additive on a bright background | dark alpha layer under it |
| sparks look like dots | Billboard mode | Stretched Billboard, velocity scale 0.05-0.15, spark texture |
| motion uniform, CG-looking | constant speed and size, linear curves | ranges, noise 0.2-0.5, gravity, ease curves |
| erosion does nothing | Custom Data stream missing, or noise imported sRGB | audit: streams, sRGB off |
| effect too dark / erodes too early | data texture sRGB | sRGB off |
| layers flicker while the camera moves | equal sorting orders | distinct orders, glow on top |
| straight cut where smoke meets the ground | billboards intersect geometry | offset along the normal (mobile) or soft particles (PC) |
| every shot identical | fixed seed | auto seed / reseed on spawn |
| hitch the first time it plays | shader/PSO compile | warm shaders at load |
| spike when many play at once | Instantiate per shot, too many systems | pool, cap concurrent instances, lite variant |
| fps drops when the camera is close | full-screen fill | cap Max Particle Size, camera fade, fewer big layers |
| looping effect costs CPU off-screen | procedural mode lost | `cullingMode = Pause` or CullingGroup |
| flipbook flashes between shapes | variant sheet played as animation | random fixed frame |
| ends too late / lingers | long lifetimes, loop on | shorten, `loop = false`, stop with the gameplay state |
