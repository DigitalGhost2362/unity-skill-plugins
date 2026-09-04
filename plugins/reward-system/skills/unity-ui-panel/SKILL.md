---
name: unity-ui-panel
description: Reward-repo delta on the studio UI standard. Read unity-standard:unity-ui-panel first; this file adds only what is specific to the reward hosts - the package/demo-host namespace split, SampleUIManager registration and the boot chain, the concrete panel parking coordinates on the 2400x1080 canvas, BaseUIInspectorProcessor and the OnValidate shadowing trap, and the protected Vietnamese field comments. Use it every time you create, edit, or wire any UI panel, popup, widget, button, or HUD element in this repo.
---

# Unity UI panel workflow - reward delta

**Read `unity-standard:unity-ui-panel` first.** It is the standard: the panel contract
(`Initialize` / `OpenPanel` / `ClosePanel`), zero comments, code-only button wiring, one prefab per
repeated element, anchors and pivots, panel parking, layering, events, tweens, the mandatory debug
buttons, and the finishing checklist. `reward-system:unity-coding` applies underneath both.

All UI in this repo sits on `com.nabagame.ui`, a renamed fork of DoozyUI v2 - read
`unity-standard:unity-ui-panel`'s `reference/nabagame-ui.md` before touching a panel prefab. Three
of its four traps silently break panels that otherwise look correct.

## Rule 1 - Namespace and location depend on which side you are on

| | Package UI | Demo-host UI |
|---|---|---|
| Namespace | `NabaGame.Reward` | `NabaGame.Reward.Sample`, types prefixed `Sample` |
| Scripts | `Packages/com.nabagame.reward/Runtime/Features/<Feature>/` | `Packages/com.nabagame.reward/Samples~/RewardDemo/Scripts/` (= `Assets/_RewardDemo/Scripts` via symlink) |
| Prefabs | `Samples~/RewardDemo/Prefabs/` | same folder |
| Owns hooks/adapters | no - uses the static `RewardHooks` | yes - assigns them at boot |

Package UI must never reference `UIManager`, `GameController`, `AudioManager`, `GameManager`, or any
game enum. It plays SFX through `RewardHooks.PlaySfx` and grants through `Row.OnClaimed`. See the
`reward-system:reward-package` skill.

## Rule 2 - `SampleUIManager` owns every host panel

`Samples~/RewardDemo/Scripts/SampleUIManager.cs` (a `UIManagerSingleton<SampleUIManager>`) is the UI
composition root for the demo host: the `SampleUIManager.prefab` root Canvas (ScreenSpaceOverlay,
CanvasScaler ScaleWithScreenSize 2400x1080 match 0.5) with every panel as a child prefab instance.

When you add a host panel:

1. Add a public Odin-grouped field in `SampleUIManager`:

```csharp
[SerializeField, FoldoutGroup("Common")] public DailyRewardPanel dailyRewardPanel;
```

2. Auto-wire it in `OnValidate()` - pass `true` so panels authored disabled are still found:

```csharp
if (!dailyRewardPanel) dailyRewardPanel = GetComponentInChildren<DailyRewardPanel>(true);
```

3. Initialize it from the boot chain (`SampleGameController.Initialize()` calls the feature's
   `Sample*Manager.Initialize()`, which calls `panel.Initialize(rows)`).

4. If it is a blocking popup, register it in `checkHasPopup` inside `SampleUIManager.Initialize()` so
   `HasPopup()` stays accurate:

```csharp
if (dailyRewardPanel && !checkHasPopup.Contains(dailyRewardPanel)) checkHasPopup.Add(dailyRewardPanel);
```

The chain is `SampleGameController.Start() -> RewardHooks.* -> SampleUIManager.Initialize() ->
Sample{Feature}Manager.Initialize() -> panel.Initialize(rows) -> widget.Initialize(...)`. A panel
never reached by this chain is a bug - nothing initializes it.

## Rule 3 - Protected comments

The zero-comment rule holds, with one carve-out: the serialized fields of the three package panels
carry short Vietnamese `//` comments (user-mandated field guides for the consuming dev). Keep them,
keep writing them in Vietnamese under 7 words when adding a new panel field, and never translate or
delete them.

## Rule 4 - Button wiring and widgets, concretely

Use `RewardUi.Bind(button, handler)` in package code - it is the repo's null-guard + remove-then-add
helper. Reference implementations: `Runtime/Features/DailyReward/DailyRewardCard.cs`,
`OnlineReward/OnlineRewardCell.cs`.

```csharp
public void Initialize(int day, DailyRewardRow row, Sprite cardBackground, Action<int> clickedCallback)
{
    onClicked = clickedCallback;
    if (icon) icon.sprite = row.Icon;
    RewardUi.Bind(button, HandleClicked);
}
```

## Rule 5 - Two `BaseUI` traps specific to this repo

- `BaseUIInspectorProcessor` injects green `OpenPanel` / red `ClosePanel` inspector buttons onto
  every `BaseUI` subclass **by method-name string match** - never rename or duplicate those two. It
  lives in `Assembly-CSharp`, so it does **not** reach package panels; package panels carry explicit
  `PreviewOpen` / `PreviewClose` `[Button]`s instead.
- **NEVER declare `OnValidate` on a `BaseUI` panel.** `BaseUI.OnValidate()` is private and auto-wires
  `uiPanel = GetComponent<UIPanel>()`; a derived declaration silently shadows it (Unity calls only
  the most-derived magic method) and future prefabs lose the wire. `OnValidate` auto-wiring is still
  encouraged on widgets and host classes.

The intro-replay idiom, when a panel holds `[SerializeField] UIElement[] animatedElements`:

```csharp
for (int i = 0; i < animatedElements.Length; i++)
{
    animatedElements[i].HideUiElement(true);
    animatedElements[i].ShowUiElement();
}
```

## Rule 6 - The parking lot, with real coordinates

Canvas reference is 2400x1080. Canonical slots in `SampleUIManager.prefab`: home/HUD at `(0, 0)`,
`200_DailyRewardPanel (0, 1500)`, `200_OnlineRewardPanel (0, -1500)`, `200_LuckySpinPanel (2800, 0)`,
`250_ItemReceivedPanel (-2800, 0)`. New panels continue the ring: X step 2800, Y step 1500.

`50_HomePanel` and `60_CurrencyPanel` park at `(0, 0)`: `BaseUI` + `UIPanel`, `useBackground = false`,
`startHidden = false`, and **not** registered in `checkHasPopup` - an always-visible panel would make
`HasPopup()` return `true` forever and deadlock every button that guards on it.

Template prefabs ship in `Samples~/RewardDemo/Prefabs/`: `DailyRewardCard`, `LuckySpinWedge`,
`OnlineRewardCell`, `SampleItemReceivedCell`. The shipped fixed-board pattern (decision #28) is
`DailyRewardPanel.cards` and `LuckySpinPanel.wedges`.

## Extra checklist rows for this repo

On top of the generic checklist:

- [ ] Correct namespace for the side you are on: `NabaGame.Reward` in the package,
      `NabaGame.Reward.Sample` (+ `Sample` prefix) in the demo host.
- [ ] Host panel registered in `SampleUIManager`: Odin field + `OnValidate` auto-wire + boot-chain
      init + `checkHasPopup` when blocking.
- [ ] No `OnValidate` declared on a `BaseUI` subclass; `OpenPanel`/`ClosePanel` not renamed.
- [ ] Panel parked on the 2800/1500 ring, prefab has `useCustomStartAnchoredPosition = true` +
      `customStartAnchoredPosition = (0, 0, 0)`.
- [ ] Vietnamese serialized-field comments on package panels preserved (and added, under 7 words,
      for any new panel field).
- [ ] Verified in play mode per `reward-system:reward-verify`, or explicitly reported as not run.
