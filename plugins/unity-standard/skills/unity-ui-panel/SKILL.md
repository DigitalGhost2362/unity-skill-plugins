---
name: unity-ui-panel
description: Mandatory workflow for ALL uGUI work in any Unity project - panels, popups, widgets, buttons, HUD elements - even small tweaks like adding a single button or label. Covers the panel lifecycle (Initialize / OpenPanel / ClosePanel), code-only button wiring, one prefab per repeated element, the anchor and pivot rules that stop layouts drifting across aspect ratios, panel parking, layering bands, tweens, the mandatory debug-button surface, and the zero-comment style. Read this on top of unity-coding for anything that draws.
---

# Unity UI panel workflow

Applies to every panel, popup, widget, HUD element and button. `unity-coding` still applies in full
underneath it. A project-specific skill may add rules on top; it overrides this file on conflict.

If the project sits on the `com.nabagame.ui` framework (`BaseUI` / `UIPanel` / `UIElement`), read
`reference/nabagame-ui.md` first - four of its behaviours silently break panels that otherwise look
correct. `reference/ui-reference-hierarchy.md` is a worked example of a full 37-panel hierarchy with
its layering bands and panel anatomy, useful when starting a UI layer from nothing.

## Rule 1 - The panel contract

Every panel exposes exactly three things and nothing else:

- **`Initialize(List<TRow> rows)`** - the single init. Validate the rows against the leniency ladder,
  load save state, arm timers, build dynamic lists, `Initialize(...)` the child widgets, bind
  listeners, render the initial state. Guard the one-time build with a `bool built`. Never use Unity
  `Start()` for feature init; `Start()` only kicks the chain. `StartClass` and `SetInfo` are retired
  names - see `unity-coding` Rule 6 for why `SetInfo` in particular is a trap.
- **`OpenPanel()` / `ClosePanel()`** - the only public way to show or hide. They wrap the framework's
  show/hide and own the presentation work: refresh, the intro tween, starting a visibility-gated
  countdown loop. `OpenPanel` before `Initialize` logs one error and refuses. Other code calls these,
  never the framework's `Show()`/`Hide()` directly.
- Members grouped in the fixed regions `API` / `Logic` / `UI` / `Debug`, `API` first and
  self-sufficient. Widgets are plain MonoBehaviours with `Initialize(...)` and no regions.

## Rule 2 - Zero comments in new UI code

No `//`, no `/* */`, no `/// <summary>`, no `[Tooltip(...)]`. Code is self-explanatory through
naming; rename or extract instead of commenting. Do not delete, rewrite or translate comments that
already exist unless the user asks - a project may deliberately keep short field guides in a
particular language on its serialized fields, and those are protected.

## Rule 3 - Buttons are wired in code, never in the Inspector

Never use the Button component's OnClick list in the editor; it must stay empty. Declare a
`[SerializeField] Button` for every button and add listeners in code with **named methods**. Two
sanctioned patterns:

**Panels that actually deactivate** - pair add/remove in `OnEnable`/`OnDisable`:

```csharp
private void OnEnable()
{
    claimButton.onClick.AddListener(OnClaimClicked);
    closeButton.onClick.AddListener(ClosePanel);
}

private void OnDisable()
{
    claimButton.onClick.RemoveListener(OnClaimClicked);
    closeButton.onClick.RemoveListener(ClosePanel);
}
```

This only fires if the GameObject really deactivates. Many UI frameworks hide a panel by toggling
its `Canvas`, leaving the GameObject active - then `OnDisable` never runs. When in doubt use the
second pattern.

**Widgets and anything initialized through `Initialize(...)`** - null-guarded remove-then-add on a
stable method group, never a lambda, so a repeated `Initialize()` can never double-subscribe and a
deleted button never throws:

```csharp
public void Initialize(int index, TRow row, Action<int> clickedCallback)
{
    onClicked = clickedCallback;
    if (icon) icon.sprite = row.Icon;
    if (button)
    {
        button.onClick.RemoveListener(HandleClicked);
        button.onClick.AddListener(HandleClicked);
    }
}
```

Give the project one bind helper that does exactly that and call it everywhere (`unity-coding`
Rule 2). A button handler may play feedback and call one public API; it never implements the result
itself.

## Rule 4 - Every repeated element is its own prefab

Any repeated cell, slot, row or card is a standalone prefab named after its widget script, with the
prefab asset's root **active**. Never a plain child duplicated by hand.

- **Dynamic list**: nest ONE instance under the scroll content, deactivate *that instance* (the
  prefab asset root stays active), point the panel's serialized template ref at it, and instantiate
  in `Initialize()`:

```csharp
for (int i = 0; i < rows.Count; i++)
{
    TCell cell = Instantiate(cellTemplate, parent);
    cell.gameObject.SetActive(true);
    cell.Initialize(i, rows[i], OnCellClicked);
    cells.Add(cell);
}
```

  A missing template ref must `LogError`-and-skip (empty panel, no crash), and cell indexing must be
  bounds-checked.

- **Fixed board** (a spin wheel's 8 wedges, a 7-day strip): nest N instances of the same template
  prefab and wire them **in order** into a serialized `List<>` on the panel. Per-slot cosmetics -
  background sprites, per-slot art - are authored on each instance, never pushed from code.
  `Initialize` *binds* rows onto that list: a count mismatch warns and binds the minimum
  (`SetActive(i < rows.Count)` hides the surplus), an empty list `LogError`s and skips, null entries
  skip silently. Layout is then pure prefab work - rearranging the board never touches C#.

In editor tooling, create the copies with `PrefabUtility.InstantiatePrefab`. `Object.Instantiate`
produces plain clones that stop following the prefab, and the drift is invisible until someone edits
the template. `IsPartOfPrefabInstance` must be true on every copy.

Never build UI at runtime with `new GameObject()` + `AddComponent`.

## Rule 5 - Debug buttons are mandatory on every new panel

Ship private `[Button, DisableInEditorMode]` play-mode methods covering every feature the panel
implements, so each state can be driven from the Inspector without playing the full flow:

1. **Open-with-variant** - one button per significant state.
2. **Drive the state change behind every visual** - e.g. `PreviewAddPlaytime(60)` calling the
   manager's real public command, not a fake repaint.
3. **Randomized / parameterized entry points** for anything with a distribution or an index.
4. **A reset** for any persistent state the panel writes, so tests repeat cleanly.

Names are `Preview*` / `Test*`, no comments. Where Hot Reload is absent these buttons are the
cheapest way to verify a state without a recompile, and they are what a human uses to check
agent-written work. A panel without them is incomplete.

## Anchors and pivots - anchor to what the element is stuck to (MANDATORY)

Unless the project ships safe-area code or layout scripts, the `CanvasScaler` is the **only** thing
that reacts to screen shape. A rect left on the default center-middle anchor keeps its distance from
the *canvas center*, so on any aspect ratio other than the one it was drawn at, every piece of
screen-edge chrome drifts - a close button walks inward on a tall phone and off the corner on a wide
one. Unity's default anchor is a placeholder, never an answer.

**The rule: the anchor names the parent edge or corner the element is visually attached to.**

| The element sits ... | Anchor |
|---|---|
| a top corner - close button, badge | top / right or top / left |
| a header group - icon + title | top / left (the whole group, same anchor) |
| the top edge, horizontally centered - warning line, ornament | top / center |
| the bottom edge - primary action, speed toggles, tap-to-continue | bottom / center, or bottom / right when it hugs that corner |
| a side rail - a stack of buttons | middle / left or middle / right |
| genuinely centered - wheel, card strip, board, popup content | middle / center; already right, leave it |
| full-width or full-height - dim, tap-catcher, decorative bar | stretch on that axis |

**Scope: only where the parent can change size.** The rule binds any child of a rect that resizes
with the screen - panel roots (`stretch 0,0-1,1`) and any stretched group. Children of a
**fixed-size** container never drift, so their center anchors are correct and must be left alone:
the icon inside a button, the label on a card, the badge on a cell. Do not "fix" those; it is churn
with no behavior change.

**Attached to a window, not to the screen.** If an element hangs off a centered board or window, it
belongs **under that board**, anchored to the board's own corner. Parenting it to the panel root and
eyeballing a center offset only works for as long as the board never changes size - and it hides the
real relationship from the next person.

**Changing an anchor must never move anything.** Click the preset in the Anchor Presets popup
*without* Alt - Unity recomputes `anchoredPosition` so the rect stays exactly where it was. The
number changing (`Pos X 1053` to `-147`) **is** the fix working, not a regression. Typing a new
anchor while leaving the old `anchoredPosition` teleports the element. When scripting the change,
capture `offsetMin`/`offsetMax` before and restore them after:

```csharp
Vector2 min = rt.offsetMin, max = rt.offsetMax;   // relative to the OLD anchors
Vector2 pSize = ((RectTransform)rt.parent).rect.size;
Vector2 oldMin = rt.anchorMin * pSize + min;      // corners in parent space
Vector2 oldMax = rt.anchorMax * pSize + max;
rt.anchorMin = newMin; rt.anchorMax = newMax; rt.pivot = newPivot;
rt.offsetMin = oldMin - newMin * pSize;           // rect restored exactly
rt.offsetMax = oldMax - newMax * pSize;
```

`PrefabUtility.LoadPrefabContents` gives no Canvas, so a panel root's `rect.size` reads `0x0` there
and this maths silently collapses. Resolve sizes yourself from the reference resolution down, or do
the edit in a prefab stage.

**A nested prefab instance stores its own layout, so a prefab anchor edit can teleport things.**
When panels are nested prefab instances inside a UI-manager prefab or a scene, each instance carries
its own `m_AnchoredPosition` overrides - and overrides are per-property and even per-axis
(`m_AnchoredPosition.x` and `.y` are separate). Editing an anchor in the panel prefab propagates the
anchor down while the instance keeps its old, center-relative position, and the element jumps
off-screen. After any anchor edit on a panel prefab, re-derive the instance overrides too: per axis,
if the rect's center now falls outside the parent's half-extent, subtract
`refSize * (anchorCenter - 0.5)` on that axis. Verify in Play mode, never by eye in the Scene view -
panels sit in their parking slots there, so any "distance from the canvas edge" measured at rest is
wrong.

**Pivot follows the anchor only when the rect grows.** Leave `pivot` at `(0.5, 0.5)` for every
fixed-size rect; moving it changes nothing except which number you have to read. Move it to the
matching edge when the size is content-driven or animated from that edge: an auto-sized label that
must grow away from its corner, a bar that fills upward, a scale-in that should pop from a corner.
`Shift`+preset sets anchor and pivot together; `Alt`+preset also *moves* the rect - never use Alt.

## Panel parking - scene authoring is a parking lot (MANDATORY)

Editor position and runtime position are decoupled; both halves are required on every panel:

- Every panel prefab pins its runtime position to the canvas center (in `com.nabagame.ui`:
  `useCustomStartAnchoredPosition = true` and `customStartAnchoredPosition = (0, 0, 0)` on its
  `UIPanel`). Showing the panel then always lands it centered, no matter where it was parked.
- Panel RectTransforms must never overlap each other or the home screen in the editor - each panel
  parks in its own slot around the center with a clear gap, on a fixed X/Y step the project picks
  once (a step wider than the canvas, e.g. X 2800 / Y 1500 on a 2400x1080 design). New panels
  continue the ring.
- Always-visible HUD panels park at `(0, 0)` - they are never hidden, so there is no reveal to park
  away from. They are panels like any other, but they are **not** registered in the project's
  "is a popup open" list: an always-visible panel would make that query return true forever and
  deadlock every button guarding on it.

## Layering

Panel z-order comes from a per-panel nested `Canvas` with `overrideSorting: 1` and an explicit
`sortingOrder`, not from sibling order. Encode the order in the instance name (`200_RewardPanel`)
and keep bands, e.g. HUD 50-60, popups 200-250, toast 210, reward-flight layer 290, screen
transition / no-internet / ad-break 300, tutorial 500. A shared dimmer sits at
`openingPanel.sortingOrder - 1` and is opted into per panel.

## Events

Cross-system updates go through the project's event bus. **Every raise allocates a fresh payload
instance** - never cache and mutate one. Grants and one-shot outcomes are not events; they travel
through the row's callback. Panels subscribe in `Initialize()` (remove-then-add) and unsubscribe in
`OnDestroy`. The authoritative owner raises after changing state and refreshes itself directly,
gated on `if (IsVisible()) RefreshAll();`. Never poll per frame.

## Tweens

Field-held `Tween`/`Sequence`; `?.Kill()` before replay; `.SetUpdate(true)` so UI animates while the
game is paused; `.SetLink(gameObject)`; cleanup in `OnDisable`/`OnDestroy`. Count-ups via
`DOVirtual.Int`, fills via `DOFillAmount`, feedback via `DOPunchScale`. Staggered intros are
per-widget `PlayIntro(delay)` calls made from the panel.

## Checklist before finishing any UI task

- [ ] Panel exposes `Initialize(rows)`, `OpenPanel()`, `ClosePanel()` and the `API`/`Logic`/`UI`/`Debug` regions; widgets are MonoBehaviours with `Initialize(...)`.
- [ ] Panel prefab satisfies whatever the UI framework needs to actually appear (animation channels enabled, correct parent, dimmer opt-in) - see `reference/nabagame-ui.md` when that is the framework.
- [ ] Panel parked in a free non-overlapping slot and its prefab pins the runtime position to center; always-visible HUD panels sit at `(0, 0)` and are not registered as popups.
- [ ] Every edge-pinned rect under a screen-sized parent carries the matching corner/edge anchor - nothing that touches an edge is left on center-middle; children of fixed-size containers were left alone.
- [ ] Each anchor change preserved the rendered rect exactly (position and size identical before/after); `anchoredPosition` numbers moved, pixels did not. Nested-instance overrides re-derived.
- [ ] Anything hanging off a centered board is a child of that board, anchored to the board's corner - not a sibling with an eyeballed center offset.
- [ ] Panel registered wherever the project's UI composition root expects it (serialized field, auto-wire, boot-chain init, popup list when blocking).
- [ ] Every button has a serialized reference and a null-guarded, remove-then-add listener; editor OnClick lists are empty; deleting any button must not throw or stick.
- [ ] Event subscriptions cannot duplicate across repeated `Initialize()` / open-close cycles.
- [ ] No comments, summaries, or tooltips added.
- [ ] New panel ships `[Button, DisableInEditorMode]` debug methods covering every implemented feature.
- [ ] Every repeated cell/slot is an instance of a template prefab (dynamic-list instance authored inactive; fixed-board instances authored active and wired in order into the panel's serialized `List<>`), never a plain child.
- [ ] Recompiled and console-checked per `unity-hot-reload`, then verified in play mode per `unity-playmode-verify` - or explicitly reported as not run.
