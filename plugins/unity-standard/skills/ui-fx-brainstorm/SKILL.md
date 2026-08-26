---
name: ui-fx-brainstorm
description: Brainstorm UI animation/effect ideas by building ONE self-contained HTML preview showing 4 labelled options (A/B/C/D) side by side, so the user can watch them run and pick one before any Unity code is written. Use whenever the user asks for effect/animation/juice ideas for a panel, popup, button, currency counter, reward, transition or any UI moment — "làm hiệu ứng cho X", "brainstorm effect", "gợi ý animation", "cho tôi vài option hiệu ứng", "panel này mở kiểu gì cho đẹp". Also use before implementing a non-trivial UI animation whose look has not been decided yet.
---

# UI FX Brainstorm

Decide the *look* of a UI effect in a browser, where iteration is seconds, **then** build it in Unity once.

Deliverable of this skill = **one HTML file + a pick**. Not Unity code. Unity code happens after the user picks, under `unity-coding` + `unity-ui-panel`.

## Non-negotiables

1. **One file, zero network.** No CDN, no `import`, no fonts, no external images. It must run by double-clicking the file with Wi-Fi off. (GSAP/anime.js/Motion were considered and rejected: nothing in a 4-option UI preview needs them, and a dead CDN kills the whole deliverable.)
2. **Animate `transform` / `opacity` / `filter` only.** Never `width`, `height`, `top`, `left`, `margin`, `background-position`. Those stutter on the preview *and* have no cheap Unity equivalent.
3. **Every option must be buildable in Unity** with DOTween + `UIElement`. `UIParticle` is **not installed in this repo** — if an idea needs particles, label it `⚠ needs UIParticle` on the card and confirm with the user before promising it. Same for real gaussian blur, per-pixel distortion or a custom shader: drop it or label it `⚠ needs shader`.
4. **4 genuinely different options.** No two may share the same primary motion verb (see `reference/fx-vocabulary.md` → *Divergence axes*). Four flavours of "scale in with a different ease" is a failed deliverable.
5. **Mock the real element.** If the subject is a known panel, the stage must roughly resemble it (board shape, button row, currency pill, landscape framing). Grey rectangles hide the timing problems that matter.
6. **UI timing band: 0.15s–0.6s.** A click response never exceeds 0.3s. Celebration/reward sequences may reach ~1.2s total but must be skippable. State the numbers on the card.

## Workflow

### 1. Pin the moment (cheap)

You need two things: **what** element, and **which moment** (open / close / press / success / fail / gain / unlock / idle / transition). Infer from the request when possible.

Only if genuinely ambiguous, ask **one** `AskUserQuestion` with both questions in it. Never ask "what style do you want" — producing 4 styles *is* the job.

### 2. Ground it (≤2 tool calls, skip if the subject is generic)

If the effect targets an existing panel, `grep` that panel's script for what already exists — do **not** read prefabs or whole files:

```
grep -n "DOScale\|DOFade\|DOAnchorPos\|UIElement\|Sequence\|SetEase" Assets/_GameBase/Scripts/UI/Panel/<X>Panel.cs
```

Purpose: reuse the existing animation vocabulary and know which transforms are already driven, so the picked option drops in instead of fighting `UIElement`.

### 3. Choose 4 options along one axis

Pick an axis from `reference/fx-vocabulary.md` (e.g. *restraint → spectacle*, or *mechanical / organic / physical / stylised*) and place one option at each stop. Write down for each: name, one-line description, and the Unity recipe *before* writing HTML. If you cannot write the Unity recipe, the option is not real.

### 4. Build the file

Copy `assets/template.html` to the scratchpad as `fx-<subject>-<moment>.html` and replace only the `EFFECTS` array. The harness (grid, replay, speed, loop, light/dark) is already done — do not rewrite it, do not restyle it.

Each entry:

```js
{
  key: 'A',
  name: 'Pop & Settle',
  desc: 'Board scales 0→1.06→1, backdrop fades under it.',
  unity: 'board.DOScale(1f, .32f).SetEase(Ease.OutBack, 1.7f) + cg.DOFade(1, .12f)',
  cycle: 1800,                 // ms between auto-replays
  render(stage, H) { ... }     // build DOM + start animations
}
```

Rules inside `render`:
- Use `H.anim(el, frames, opts)` — it honours the speed slider. Use the `delay` option for sequencing; **never `setTimeout`** (the speed slider can't reach it).
- Use `H.EASE.*` so the preview easing and the DOTween easing are the same curve.
- Build the subject with `H.panel/H.button/H.pill/H.card` helpers where they fit.

### 5. Show it

```powershell
Invoke-Item "<full path to the html>"
```

Then in chat, list the 4 in **one short block** — letter, name, one line each (Vietnamese if the user writes Vietnamese). Do not paste code, do not explain the harness.

Finish with an `AskUserQuestion`: options A / B / C / D. (The user can always type "A nhưng chậm hơn" via Other — expect and welcome mixes.)

### 6. Iterate or implement

- **Tweak requested** → edit the same file, re-run `Invoke-Item`. Never create a second file for a tweak; the user is comparing against what they just saw.
- **Picked** → now switch to `unity-coding` + `unity-ui-panel` and implement that one option from its `unity` recipe. Delete nothing from the HTML; it stays in the scratchpad as the spec.

## Web → Unity translation

| Preview | Unity |
|---|---|
| `transform: scale()` | `rect.DOScale(v, d)` |
| `translate()` (px) | `rect.DOAnchorPos(v, d)` — px map 1:1 at the CanvasScaler reference resolution (2400×1080 in the reference project; re-check once this repo has a root Canvas) |
| `rotate()` | `rect.DOLocalRotate(new Vector3(0,0,z), d)` |
| `opacity` | `canvasGroup.DOFade(v, d)` — one CanvasGroup, not per-Image fades |
| chained `delay:` | `DOTween.Sequence()` + `Insert/Append/AppendInterval` |
| particle burst | ⚠ `UIParticle` is not installed here — DOTween-driven sprite fans/flights are the available substitute |
| `filter: brightness()` | `image.DOColor()` or a white flash overlay Image |
| `filter: blur()` | ⚠ no cheap equivalent — avoid |

Easing (DOTween ↔ CSS, `H.EASE` already carries these):

| DOTween | cubic-bezier |
|---|---|
| `OutQuad` | `0.5, 1, 0.89, 1` |
| `OutCubic` | `0.33, 1, 0.68, 1` |
| `OutQuart` | `0.25, 1, 0.5, 1` |
| `OutExpo` | `0.16, 1, 0.3, 1` |
| `InOutCubic` | `0.65, 0, 0.35, 1` |
| `OutBack` (overshoot 1.7) | `0.34, 1.56, 0.64, 1` |
| `InBack` | `0.36, 0, 0.66, -0.56` |
| `OutElastic` / `OutBounce` | not expressible — use explicit keyframes, `H.EASE` has `elasticOut`/`bounceOut` keyframe helpers |

Duration: CSS ms ÷ 1000 = DOTween seconds. Keep them identical so the preview *is* the spec.

## Traps

- **`getAnimations()` needs a frame.** The harness already waits one rAF before applying playback rate. Animations you create later than that ignore the speed slider — hence the no-`setTimeout` rule.
- **Autoplay loop hides the entrance.** `cycle` must be ≥ total duration + 700ms of stillness, or the user never sees the settled state.
- **Don't publish as an Artifact by default.** A local file opens instantly and is editable in place. Only publish if the user asks for a shareable link — the zero-dependency rule already makes the file CSP-safe.
- **Orientation.** The reference project is landscape (2400×1080). This repo has no root Canvas yet — take orientation from the preview jpgs in `Assets/_ASMR-Tower/Art/preview/` and say which one you assumed.
- **Sound is half the juice and the preview has none.** When an option's impact depends on a hit/whoosh, say so on the card (`sfx: impact on land`) instead of over-animating to compensate.
- **`UIElement` may already own the transform** on the target panel. If step 2 shows a `UIElement` Show/Hide animation, the picked effect must extend it or replace it deliberately — not stack a second DOScale on the same RectTransform.

## Files

- `assets/template.html` — the harness. Ships with 4 working demo effects; replace `EFFECTS`, keep everything else.
- `reference/fx-vocabulary.md` — divergence axes, per-element effect menu, timing/easing cheatsheet.
