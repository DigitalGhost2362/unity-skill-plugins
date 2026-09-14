# FX vocabulary

Idea bank for picking 4 options that actually differ. Read this instead of inventing from scratch.

## Divergence axes

Pick ONE axis, put one option at each stop. Never four points on the same stop.

| Axis | A | B | C | D |
|---|---|---|---|---|
| **Restraint → spectacle** | fade + tiny scale | pop with overshoot | pop + particles | slam + flash + shake |
| **Physicality** | mechanical (linear/expo) | organic (back/ease) | physical (gravity, squash, bounce) | stylised (rotate, skew, unfold) |
| **Direction of arrival** | from centre (scale) | from an edge (slide) | from the source object (fly-from) | from nowhere (dissolve/wipe) |
| **Who moves** | the element | the element's contents | the background/backdrop | the whole screen (shake/zoom) |

The rule that makes this work: **no two options may share the same primary motion verb.**
Verbs: scale · slide · drop · fly · unfold · rotate · flip · wipe · dissolve · shake · pulse · sweep.

## Per-element menu

**Popup / panel open**
scale 0→1 `OutBack` · drop + squash landing · unfold from a line (scaleY) · slam from oversized + flash · slide from an edge with backdrop fade · fly out of the button that opened it.
Always: backdrop fades faster than the board (0.12s vs 0.3s) or it feels laggy.

**Popup close**
Never just reverse the open — halve the duration and use `InBack`/`InQuad`. Close should feel dismissed, not un-played.

**Button press**
scale 0.92 down `OutQuad` 0.06s → back `OutBack` 0.12s · squash 1.08/0.9 · sink (translateY +4px, drop the bottom shadow) · rim flash.
Never exceed 0.2s total. Never block input during it.

**Button idle / call-to-action**
breathing pulse 1↔1.04 (1.4s loop, `InOutCubic`) · shine sweep across the face every ~2.5s · tilt wobble · a stopped-then-nudge every N seconds (cheaper on the eye than a constant loop).

**Currency gain**
coins fly from source to the pill on staggered arcs, each arrival bumps the pill scale 1.15 · rolling counter · +N label floats up and fades.
The reference project pairs a spend with a rolling counter + red `-$N`; a gain effect must not fight it. This repo has no `CurrencySection` yet.

**Currency spend**
counter rolls down · pill flashes red · shake on "not enough".

**Reward / item received**
card flip reveal · rarity-coloured burst scaled by rarity · light sweep behind the icon · icon lands with elastic and holds · stagger when there are several.

**Progress bar / XP**
fill `OutQuart` 0.5s with a leading glow · tick marks pop as they pass · overflow → fill to 100, flash, reset to 0 and continue.

**Level up / unlock**
full-screen white flash · radial rays rotating slowly behind · scale-punch on the number · confetti burst.

**Locked → unlocked**
lock icon shakes then breaks apart · grey→colour transition (`filter: saturate`) · shackle pops off and falls.

**List / grid appear**
stagger 40–70ms per cell, `OutCubic`, each from scale 0.85 + fade. Cap the total: with >12 cells, stagger by row instead of by cell or the last cell arrives a second late.

**Tab switch**
outgoing slides out the direction of travel while incoming slides in · underline/indicator slides between tabs `OutExpo` (this one alone sells the whole screen).

**Error / rejection**
horizontal shake 5 oscillations, 10px damped, 0.3s · red flash · nothing else. Longer reads as a bug.

**Timer / urgency**
pulse scale on each of the last 10 ticks, increasing amplitude · colour ramp to red · a stronger beat at 3-2-1.

## Timing cheatsheet

| Moment | Duration |
|---|---|
| Button press feedback | 0.06 + 0.12s |
| Hover/select highlight | 0.10–0.15s |
| Popup open | 0.25–0.35s |
| Popup close | 0.15–0.20s |
| Panel/tab transition | 0.20–0.30s |
| Reward reveal beat | 0.30–0.45s per beat |
| Full celebration | ≤1.2s, skippable |
| Idle loop | 1.2–2.5s per cycle |

Stagger step: 40–70ms. Anticipation (pull back before moving): 0.05–0.08s, and only on the loud options.

## Easing rules

- **Things arriving** ease *out* (`OutQuad/OutCubic/OutExpo`) — fast start, gentle stop.
- **Things leaving** ease *in* (`InQuad/InBack`) — they accelerate off screen.
- **Things doing both** `InOutCubic`.
- **Overshoot (`OutBack`) is the single highest-value change** on a flat animation — but only on the arriving element, never on a fade, and never on more than ~2 things at once.
- **Elastic/bounce read as toy-like.** Fine for rewards, wrong for settings and shop.
- Never `linear` except for shakes, sweeps and rotation loops.

## Cost notes for the Unity side

- `DOScale`/`DOFade`/`DOAnchorPos` on a handful of objects: free.
- Fading many Images individually: put ONE `CanvasGroup` on the root instead.
- Particles: `UIParticle` is **not installed** in this repo. Substitute DOTween-driven pooled sprites, or flag the option as blocked.
- Blur, per-pixel distortion, real shadows: not cheap on mobile. Fake with a pre-blurred sprite or don't offer it.
- Any looping idle animation must be killed on `Hide()` (`DOTween.Kill` on the panel), or it survives the popup.
