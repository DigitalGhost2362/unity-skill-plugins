---
name: unity-ui-panel
description: Mandatory workflow for ALL UI work in the reward-system Unity project — both the reward package UI (Packages/com.nabagame.reward/, namespace NabaGame.Reward) and the Samples~/RewardDemo demo host (symlinked at Assets/_RewardDemo, namespace NabaGame.Reward.Sample). Use this skill every time you create, edit, or wire any UI panel, popup, widget, button, or HUD element — even small tweaks like adding a single button or label. Covers the NabaGame.UI (BaseUI/UIPanel/UIElement) framework and its traps, SampleUIManager registration, SetInfo initialization, null-guarded button wiring, template prefabs, and the no-comments code style.
---

# Unity UI Panel Workflow (reward hosts)

All UI in this repo sits on `com.nabagame.ui` — a renamed fork of DoozyUI v2. Read Rule 0 before anything else; three of its facts silently break panels that otherwise look correct.

For the framework's full shape and the reference implementation it was modelled on (paint-and-seek's 37-panel hierarchy, layering bands, template patterns), see `reference/ui-framework.md` in this skill folder.

## Rule 0 — How `BaseUI` / `UIPanel` / `UIElement` actually behave

```
UIElement            (RequireComponent: RectTransform + Canvas + CanvasGroup + GraphicRaycaster)
  └─ UIPanel         (adds pooled background dimmer, startHidden, animateAtStart)
BaseUI               (RequireComponent: UIPanel — the class every panel inherits)
UIManagerSingleton<T> : IUiManager   (provides ONLY the background dimmer pool)
```

Four traps, all verified in the package source:

1. **`Show()`/`Hide()` do not `SetActive`.** They toggle `Canvas.enabled` + `GraphicRaycaster.enabled`. Only `disableWhenHidden = true` deactivates the GameObject.
2. **`BaseUI.Show()` cannot revive a deactivated GameObject** — it calls `StartCoroutine` on an inactive object. A panel authored with `disableWhenHidden` must be reopened through `UIElement.ShowUiElement()`.
3. **`Show()` with no in/out animation channel enabled logs a warning and returns, doing nothing.** Every panel prefab needs at least one `move`/`rotate`/`scale`/`fade` channel enabled on `inAnimations` *and* `outAnimations`. There are no preset assets in this project — 100% of animation is authored per-prefab in the Inspector.
4. **`UIElement.Awake()` runs `GetComponentInParent<IUiManager>()`.** A panel that is not a descendant of the `UIManager` GameObject cannot get a background dimmer and logs `Cannot find UiManager`.

Also inert here, do not reach for them: `UIButton`, `UINavigation`, `NamesDatabase`, `DUISettings`, `UIBackgroundShow/HideEvent`. The project uses plain `UnityEngine.UI.Button` plus its own DOTween feedback.

## Rule 1 — Namespace and location depend on which side you are on

| | Package UI | Demo-host UI |
|---|---|---|
| Namespace | `NabaGame.Reward` | `NabaGame.Reward.Sample`, types prefixed `Sample` |
| Scripts | `Packages/com.nabagame.reward/Runtime/Features/<Feature>/` | `Packages/com.nabagame.reward/Samples~/RewardDemo/Scripts/` (= `Assets/_RewardDemo/Scripts` via symlink) |
| Prefabs | `Samples~/RewardDemo/Prefabs/` | same folder |
| Owns hooks/adapters | no — uses the static `RewardHooks` | yes — assigns them at boot |

Package UI must never reference `UIManager`, `GameController`, `AudioManager`, `GameManager`, or any game enum. It plays SFX through `RewardHooks.PlaySfx` and grants through `Row.OnClaimed`. See the `reward-system:reward-package` skill.

## Rule 2 — `SampleUIManager` owns every host panel

`Samples~/RewardDemo/Scripts/SampleUIManager.cs` (a `UIManagerSingleton<SampleUIManager>`) is the UI composition root for the demo host: the `SampleUIManager.prefab` root Canvas (ScreenSpaceOverlay, CanvasScaler ScaleWithScreenSize 2400x1080 match 0.5) with every panel as a child prefab instance.

When you add a host panel:

1. Add a public Odin-grouped field in `SampleUIManager`:

```csharp
[SerializeField, FoldoutGroup("Common")] public DailyRewardPanel dailyRewardPanel;
```

2. Auto-wire it in `OnValidate()` — pass `true` so panels authored disabled are still found:

```csharp
if (!dailyRewardPanel) dailyRewardPanel = GetComponentInChildren<DailyRewardPanel>(true);
```

3. Initialize it from the boot chain (`SampleGameController.SetInfo()` calls the feature's `Sample*Manager.SetInfo()`, which calls `panel.SetInfo(rows)`).

4. If it is a blocking popup, register it in `checkHasPopup` inside `SampleUIManager.SetInfo()` so `HasPopup()` stays accurate:

```csharp
if (dailyRewardPanel && !checkHasPopup.Contains(dailyRewardPanel)) checkHasPopup.Add(dailyRewardPanel);
```

The chain is `SampleGameController.Start() -> RewardHooks.* -> SampleUIManager.SetInfo() -> Sample{Feature}Manager.SetInfo() -> panel.SetInfo(rows) -> widget.SetInfo(...)`. A panel never reached by this chain is a bug — nothing initializes it.

## Rule 3 — Zero comments in new UI code

No `//`, no `/* */`, no `/// <summary>`, no `[Tooltip(...)]`. Code must be self-explanatory through naming; rename or extract instead of commenting. Do not delete or rewrite comments that already exist unless the user asks. **Protected exception:** the serialized fields of the three package panels carry short Vietnamese `//` comments (user-mandated field guides for the consuming dev) — keep them, keep writing them in Vietnamese under 7 words when adding a new panel field, and never translate or delete them.

## Rule 4 — Buttons are wired in code, never in the Inspector

Never use the Button component's OnClick list in the editor. Declare a `[SerializeField] Button` for every button and add listeners in code with named methods. Two sanctioned patterns:

**BaseUI panels** — pair add/remove in `OnEnable`/`OnDisable`:

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

Note this only fires if the panel actually deactivates — with `disableWhenHidden = false` (the default) the GameObject stays active and `OnDisable` never runs. When in doubt use the second pattern.

**Widgets/templates initialized via `SetInfo(...)`** — `RewardUi.Bind` (package) or remove-then-add on a stable method group (never a lambda), so repeated `SetInfo()` can never double-subscribe, and null-guarded so a deleted button never throws (decision #26):

```csharp
public void SetInfo(int day, DailyRewardRow row, Sprite cardBackground, Action<int> clickedCallback)
{
    onClicked = clickedCallback;
    if (icon) icon.sprite = row.Icon;
    RewardUi.Bind(button, HandleClicked);
}
```

Reference implementations: `Runtime/Features/DailyReward/DailyRewardCard.cs`, `OnlineReward/OnlineRewardCell.cs`.

A button handler may play feedback and call one public API; it never implements the result itself.

## Rule 5 — Panels inherit BaseUI with SetInfo / OpenPanel / ClosePanel

- `SetInfo(List<{Feature}Row> rows)` — the single init: validate rows (leniency ladder), load save, arm timers, build dynamic lists, `SetInfo()` child widgets, bind listeners, render initial state. Guard the one-time build with `bool built`. Never use Unity `Start()` for feature init; `StartClass` is retired.
- Package panel members live in the fixed regions `API` / `Logic` / `UI` / `Debug` — `API` first and self-sufficient (init, open/close, red-dot queries, reset, placement consts).
- `OpenPanel()` / `ClosePanel()` — the only public way to show/hide (never rename them — `BaseUIInspectorProcessor` string-matches the names). They wrap `Show()`/`Hide()` and do the presentation work: refreshing, DOTween sequences (field-held, `?.Kill()` before replay, killed in `ClosePanel`/`OnDestroy`), starting the `IsVisible()`-gated countdown loop. `OpenPanel` before `SetInfo` logs one error and refuses. Other code calls `OpenPanel()`/`ClosePanel()`, never `Show()`/`Hide()` directly.
- The intro-replay idiom, when a panel holds `[SerializeField] UIElement[] animatedElements`:

```csharp
for (int i = 0; i < animatedElements.Length; i++)
{
    animatedElements[i].HideUiElement(true);
    animatedElements[i].ShowUiElement();
}
```

- **Debug buttons are MANDATORY on every new panel** (user requirement): ship private `[Button, DisableInEditorMode]` play-mode methods covering every feature the panel implements, so each state can be driven from the Inspector without playing the full flow. Cover: (a) open-with-variant — one button per significant state; (b) simulate the state change behind every visual (e.g. `PreviewAddPlaytime(60)` → `rewardManager.AddSeconds(60)`); (c) randomized/parameterized entry points; (d) reset any persistent state the panel writes so tests repeat cleanly. Names are `Preview*`/`Test*`, no comments. Since Hot Reload is absent (see `unity-hot-reload`), these buttons are the cheapest way to verify without recompiling.
- `BaseUIInspectorProcessor` injects green `OpenPanel` / red `ClosePanel` inspector buttons onto every `BaseUI` subclass **by method-name string match** — never duplicate or rename those two. It lives in `Assembly-CSharp`, so it does **not** reach package panels; package panels carry explicit `PreviewOpen`/`PreviewClose` `[Button]`s instead.
- `OnValidate()` auto-wiring of stable child references is encouraged on widgets and host classes — but NEVER declare `OnValidate` on a `BaseUI` panel: `BaseUI.OnValidate()` is private and auto-wires `uiPanel = GetComponent<UIPanel>()`; a derived declaration silently shadows it (Unity calls only the most-derived magic method) and future prefabs lose the wire.

## Rule 6 — Every repeated element is its own prefab

Any repeated cell/slot/row/card must be a standalone prefab named after its widget script, root ACTIVE. Templates ship in `Samples~/RewardDemo/Prefabs/` (`DailyRewardCard`, `LuckySpinWedge`, `OnlineRewardCell`, `SampleItemReceivedCell`).

- **Dynamic list**: nest ONE instance under the scroll content, deactivate that instance (the prefab asset root stays active), point the panel's serialized template ref at it, and instantiate in `SetInfo()`:

```csharp
for (int i = 0; i < rows.Count; i++)
{
    OnlineRewardCell cell = Instantiate(cellTemplate, parent);
    cell.gameObject.SetActive(true);
    cell.SetInfo(i, rows[i], OnCellClicked);
    cells.Add(cell);
}
```

A missing template ref must `LogError`-and-skip (empty panel, no crash), and cell/wedge indexing must be bounds-checked (decision #26).

- **Fixed board** (a spin wheel's 8 wedges, a 7-day strip): nest N instances of the same template prefab and wire them **in order** into a serialized `List<>` on the panel (`DailyRewardPanel.cards`, `LuckySpinPanel.wedges` — the shipped pattern, decision #28). Per-slot cosmetics (background sprites) are authored on each instance, never pushed from code. `SetInfo` *binds* rows onto the list: count mismatch warns and binds the min (`SetActive(i < rows.Count)` hides surplus), an empty list LogErrors-and-skips, null entries skip silently. Layout is pure prefab work — rearranging the board never touches C#. In editor tooling create the copies with `PrefabUtility.InstantiatePrefab` — `Object.Instantiate` produces plain clones that stop following the prefab.

Never build UI at runtime with `new GameObject()` + `AddComponent`.

## Layering

Panel z-order comes from a per-panel nested `Canvas` with `overrideSorting: 1` and an explicit `sortingOrder`, not from sibling order. The reference project encodes the order in the instance name (`200_RewardPanel`) and uses bands: HUD 50–60, popups 200–250, toast 210, reward-flight layer 290, screen transition / no-internet / ad-break 300, tutorial 500. Adopt the same scheme when building the demo scene. The shared dimmer (`UIBackGround`) is pooled by `UIManagerSingleton` and always placed at `openingPanel.sortingOrder - 1`; opt in per panel via `UIPanel.useBackground`.

## Panel parking — scene authoring is a parking lot (MANDATORY, always follow)

Editor position and runtime position are decoupled; both halves of the rule are required on every panel, every time:

- Every panel GameObject carries a `UIPanel` component (guaranteed when its script inherits `BaseUI` — `RequireComponent`) with `useCustomStartAnchoredPosition = true` and `customStartAnchoredPosition = (0, 0, 0)`, set in the panel's own prefab. `Show()` then always lands the panel centered, no matter where it was parked in the editor.
- Panel RectTransforms must never overlap each other or the home screen — each panel parks in its own slot around the center with a clear gap. Canonical slots in `SampleUIManager.prefab` (canvas ref 2400×1080): home/HUD at `(0, 0)`, `200_DailyRewardPanel (0, 1500)`, `200_OnlineRewardPanel (0, -1500)`, `200_LuckySpinPanel (2800, 0)`, `250_ItemReceivedPanel (-2800, 0)`. New panels continue the ring: X step 2800, Y step 1500.
- Always-visible HUD panels park at `(0, 0)` — they are never hidden, so there is no reveal to park away from. `50_HomePanel` and `60_CurrencyPanel` are panels like any other: `BaseUI` + `UIPanel`, `useBackground = false`, `startHidden = false`, and they are NOT registered in `checkHasPopup` (a panel that is always visible would make `HasPopup()` return `true` forever and deadlock every button that guards on it).

## Anchors and pivots — anchor to what the element is stuck to (MANDATORY, always follow)

There is no safe-area code and no layout script anywhere in this project: the `CanvasScaler`
(2400×1080, match 0.5) is the **only** thing that reacts to screen shape. A rect left on the default
center-middle anchor keeps its distance from the *canvas center*, so on any aspect ratio other than
20:9 every piece of screen-edge chrome drifts — a close button walks inward on a tall phone and off
the corner on a wide one. Unity's default anchor is a placeholder, never an answer.

**The rule: the anchor names the parent edge or corner the element is visually attached to.**

| The element sits ... | Anchor |
|---|---|
| a top corner — close button, no-ads badge | top / right or top / left |
| a header group — icon + title | top / left (the whole group, same anchor) |
| the top edge, horizontally centered — warning line, ornament | top / center |
| the bottom edge — OPEN ALL, X2/X5 SPEED, tap-to-continue | bottom / center, or bottom / right when it hugs that corner |
| a side rail — the stacked home buttons | middle / left or middle / right |
| genuinely centered — wheel, card strip, board, popup content | middle / center; already right, leave it |
| full-width or full-height — dim, tap-catcher, decorative bar | stretch on that axis |

**Scope: only where the parent can change size.** The rule binds any child of a rect that resizes
with the screen — panel roots (`stretch 0,0-1,1`) and any stretched group. Children of a
**fixed-size** container never drift, so their center anchors are correct and must be left alone:
the icon inside a 340×127 button, the label on a 274×450 card, the badge on a 180×196 cell. Do not
"fix" those; it is churn with no behavior change.

**Attached to a window, not to the screen.** If an element hangs off a centered board or window, it
belongs **under that board**, anchored to the board's own corner. Parenting it to the panel root and
eyeballing a center offset only works for as long as the board never changes size — and it hides the
real relationship from the next person. Online Reward is the worked example: `HeaderIcon`,
`CloseButton`, `WarningLabel`, `OpenAllRoot`, `X2Button`, `X5Button` are children of `Board`, not of
the panel root.

**Changing an anchor must never move anything.** Click the preset in the Anchor Presets popup
*without* Alt — Unity recomputes `anchoredPosition` so the rect stays exactly where it was. The
number changing (`Pos X 1053` → `-147`) **is** the fix working, not a regression. Typing a new anchor
while leaving the old `anchoredPosition` teleports the element. When scripting the change, capture
`offsetMin`/`offsetMax` before and restore them after:

```csharp
Vector2 min = rt.offsetMin, max = rt.offsetMax;   // relative to the OLD anchors
Vector2 pSize = ((RectTransform)rt.parent).rect.size;
Vector2 oldMin = rt.anchorMin * pSize + min;      // corners in parent space
Vector2 oldMax = rt.anchorMax * pSize + max;
rt.anchorMin = newMin; rt.anchorMax = newMax; rt.pivot = newPivot;
rt.offsetMin = oldMin - newMin * pSize;           // rect restored exactly
rt.offsetMax = oldMax - newMax * pSize;
```

`PrefabUtility.LoadPrefabContents` gives no Canvas, so a panel root's `rect.size` reads `0×0` there
and this maths silently collapses. Resolve sizes yourself from the reference resolution (2400×1080)
down, or do the edit in a prefab stage.

**The demo host stores per-instance layout, so a prefab anchor edit can teleport things.** Every panel
(`200_DailyRewardPanel`, `200_LuckySpinPanel`, ...) is a **nested prefab instance inside
`SampleUIManager.prefab`**, and each instance carries its own `m_AnchoredPosition` overrides -- the
alignment work done against the mockup, which lives in the *outer* prefab and in the scene, not in the
panel prefab. Overrides are per-property and even per-axis (`m_AnchoredPosition.x` and `.y` are
separate). So editing an anchor in `DailyRewardPanel.prefab` propagates the anchor down while the
instance keeps its old, center-relative position -- and the element jumps off-screen. After any anchor
edit on a panel prefab, re-derive the instance overrides too: per axis, if the rect's center now falls
outside the parent's half-extent, subtract `refSize * (anchorCenter - 0.5)` on that axis. Verify in
Play mode, never by eye in the Scene view: panels sit in their parking slots there
(`(0, 1500)`, `(2800, 0)`, ...), so any "distance from the canvas edge" you measure at rest is wrong.

**Pivot follows the anchor only when the rect grows.** Leave `pivot` at `(0.5, 0.5)` for every
fixed-size rect — moving it changes nothing except which number you have to read. Move it to the
matching edge when the size is content-driven or animated from that edge: an auto-sized label that
must grow away from its corner, a bar that fills upward, a scale-in that should pop from a corner.
`Shift`+preset sets anchor and pivot together; `Alt`+preset also *moves* the rect — never use Alt.

## Events

Cross-system updates use `NabaGame.Core.Runtime.EventManager.EventManager`. Package payloads live beside their feature (`*Events.cs`); sample payloads in `SampleEvents.cs`. **Every raise allocates a fresh payload instance** — never cache and mutate one. Grants are NOT events — they travel through `Row.OnClaimed`. Panels that subscribe do so in `SetInfo()` (remove-then-add) with unsubscribe in `OnDestroy` (`EventManager.Instance` is never null — no guard). The authoritative owner raises after changing state and refreshes itself directly, gated on `if (IsVisible()) RefreshAll();`. Never poll per frame.

## Tweens

Field-held `Tween`/`Sequence`; `?.Kill()` before replay; `.SetUpdate(true)` (unscaled, so UI animates while paused); `.SetLink(gameObject)`; cleanup in `OnDisable`/`OnDestroy`. Count-ups via `DOVirtual.Int`, fills via `DOFillAmount`, feedback via `DOPunchScale`. Staggered intros are per-widget `PlayIntro(delay)` calls from the panel.

## Checklist before finishing any UI task

- [ ] Correct namespace for the side you are on: `NabaGame.Reward` in the package, `NabaGame.Reward.Sample` (+ `Sample` prefix) in the demo host.
- [ ] Panel inherits `BaseUI` with `SetInfo(rows)`, `OpenPanel()`, `ClosePanel()` and the `API`/`Logic`/`UI`/`Debug` regions; widgets are MonoBehaviours with `SetInfo(...)`.
- [ ] Prefab has at least one in and one out animation channel enabled, or `Show()` will silently do nothing.
- [ ] Panel is a descendant of the `UIManager` GameObject if it uses a background dimmer.
- [ ] Panel parked in a free non-overlapping slot (X step 2800 / Y step 1500) and its prefab has `useCustomStartAnchoredPosition = true` + `customStartAnchoredPosition = (0, 0, 0)`; always-visible HUD panels sit at `(0, 0)`.
- [ ] Every edge-pinned rect under a screen-sized parent carries the matching corner/edge anchor — nothing that touches an edge is left on center-middle; children of fixed-size containers were left alone.
- [ ] Each anchor change preserved the rendered rect exactly (position and size identical before/after); `anchoredPosition` numbers moved, pixels did not.
- [ ] Anything hanging off a centered board is a child of that board, anchored to the board's corner — not a sibling with an eyeballed center offset.
- [ ] Host panel registered in `SampleUIManager`: Odin field + `OnValidate` auto-wire + boot-chain init + `checkHasPopup` when blocking.
- [ ] Every button has a serialized reference and a null-guarded, remove-then-add listener (`RewardUi.Bind` in the package); editor OnClick lists are empty; deleting any button must not throw or stick.
- [ ] Event subscriptions cannot duplicate across repeated `SetInfo()` / open-close cycles.
- [ ] No comments, summaries, or tooltips added.
- [ ] New panel ships `[Button, DisableInEditorMode]` debug methods covering every implemented feature.
- [ ] Every repeated cell/slot is an instance of a template prefab (dynamic-list instances authored inactive; fixed-board instances authored active and wired in order into the panel's serialized `List<>`), never a plain child.
- [ ] Recompiled and console-checked per `unity-hot-reload` (no Hot Reload in this repo), then verified in Play mode — or explicitly reported as not run.
