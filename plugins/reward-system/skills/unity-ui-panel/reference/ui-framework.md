# UI framework & reference hierarchy

Two parts: (A) how `com.nabagame.ui` works, (B) the paint-and-seek UI hierarchy this project is modelled on.
Part B is a **reference**, not this repo's state — `D:\Fork\paint-and-seek` is read-only and most of its panels do not exist here.
Surveyed 2026-08-19.

---

# A. `com.nabagame.ui` (namespace `NabaGame.UI`)

Installed from `https://gitlab.com/nbg-team1/nbg-core/ui-main.git`, resolved to `Library/PackageCache/com.nabagame.ui@…`.
Asmdefs: `com.bigbear.ui.runtime` / `com.bigbear.ui.editor`. It is a stripped, renamed fork of **DoozyUI v2** — hence `DUI`, `AnimData`, `NamesDatabase`, `QuickEngine`.

## Class graph

```
MonoBehaviour
 ├─ UIElement          animation engine host — [RequireComponent] RectTransform + Canvas + CanvasGroup + GraphicRaycaster
 │   └─ UIPanel        + pooled background dimmer, startHidden, animateAtStart
 ├─ BaseUI             [RequireComponent(typeof(UIPanel))] — the class game panels inherit
 ├─ UIBackground       the pooled dimmer itself: UIElement + Canvas + Image
 └─ UIButton           IPointerClickHandler + punch/state anims — UNUSED in both projects
Singleton<T>  (com.nabagame.core)
 └─ UIManagerSingleton<T> : IUiManager     background dimmer pool ONLY — no panel registry, no nav stack
     └─ UIManager
```

## `UIElement` — the engine

`Awake()` caches components and resolves the manager:

```csharp
childCanvas   = GetComponentsInChildren<Canvas>();
layoutGroups  = GetComponentsInChildren<LayoutGroup>();
UIManager     = GetComponentInParent<IUiManager>();   // panels MUST be children of the UIManager GO
LoadRuntimeInAnimationsPreset();
LoadRuntimeOutAnimationsPreset();
```

Behaviour that surprises people:

| Fact | Consequence |
|---|---|
| `Show()`/`Hide()` toggle `Canvas.enabled` + `GraphicRaycaster.enabled`, **not** `SetActive` | a "hidden" panel is still an active GameObject; `OnDisable` does not fire |
| only `iHide` calls `SetActive(false)`, and only when `disableWhenHidden` | opt-in per prefab |
| the only path that reactivates a disabled GO is `ShowUiElement()` → `ExecuteShow` | `BaseUI.Show()` on a disabled GO tries `StartCoroutine` on an inactive object and fails |
| `Show()` returns early with a warning when no IN channel is enabled | a prefab with all animation channels off never appears |
| in/out events fire on **timers** built from `inAnimations.StartDelay`/`TotalDuration` in `SetupElement()` | `OnOutAnimationsFinish` uses the *in* duration — a package quirk |
| animation runs on `Time.realtimeSinceStartup` / `WaitForSecondsRealtime`, and `UIAnimator.isTimeScaleIndependent = true` | UI animates while the game is paused |
| `RegisterToUIManager()` / `UnregisterFromUIManager()` are empty stubs | the fork removed the element registry |

Serialized flags on every `UIElement`: `disableWhenHidden`, `dontDisableCanvasWhenHidden`, `disableGraphicRaycaster`, `useCustomStartAnchoredPosition` + `customStartAnchoredPosition`, `executeLayoutFix`, `isVisible`, `inAnimations` / `outAnimations` (+ `loopAnimations` on prefabs), and four UnityEvents `OnInAnimationsStart/Finish`, `OnOutAnimationsStart/Finish`.

## `UIPanel` — dimmer on top

```csharp
public override void Show(bool instantAction)
{
    if (isVisible == false) { CheckShowBackground(instantAction); }
    base.Show(instantAction);
}
```

`CheckShowBackground` pulls the pooled `UIBackground` from `IUiManager` and sets it **one sorting order below** the panel:

```csharp
bg = UIManager.GetBackgroundPanel();
if (newColorBackground) bg.SetInfo(this.Canvas.sortingOrder - 1, backgroundColor);
else                    bg.SetInfo(this.Canvas.sortingOrder - 1);
bg.uiElement.Show(instantAction);
```

Extra serialized fields: `useBackground`, `newColorBackground`, `backgroundColor`, `startHidden`, `animateAtStart`.

## `BaseUI` — the game-facing base

```csharp
[RequireComponent(typeof(UIPanel))]
public class BaseUI : MonoBehaviour
{
    [SerializeField] protected UIPanel uiPanel;
    public UIPanel UiElement => uiPanel;
    private void OnValidate() { uiPanel = GetComponent<UIPanel>(); }
    public bool IsVisible() => uiPanel.isVisible;
    public virtual void OnInAnimationStart() { }   // + Finish / OnOutAnimationStart / Finish
    public virtual void SetInfo() { }              // obsolete hook — never override
    public void CloseUIElement() => uiPanel.Hide(false);
    [Button("Show")] public void Show() { uiPanel.Show(false); }
    [Button("Hide")] public void Hide() { uiPanel.Hide(false); }
}
```

## `UIAnimator` — static DOTween façade

`Move`/`Rotate`/`Scale`/`Fade`, `StopAnimations(rect, AnimationType)`, `LoopMove/LoopRotate/LoopScale/LoopFade`, `SetupLoops`/`PlayLoops`/`StopLoops`, `PunchMove/PunchRotate/PunchScale`, `ResetTarget`, `GetTweenId`. Constants `DEFAULT_DURATION = 0.3f`, `DEFAULT_EASE = Ease.Linear`, `DEFAULT_LOOPS = -1`.

Data structs in the same file: `Anim` (`Move move; Rotate rotate; Scale scale; Fade fade`, `AnimationType {In, Out, State}`, `Enabled`, `TotalDuration`, `StartDelay`, `Copy()`, `Reverse()`), plus `Loop`/`Punch` variants. Each channel has `enabled`, `easeType` (`Ease` or `AnimationCurve`), `startDelay`, `duration`.

**Named presets exist as a code path but not as assets.** `UIAnimatorUtil.GetInAnim(category, name)` loads from `Resources/DUI/Animations/In|Out|State|Loop|Punch/<Category>/<Name>` — neither project ships any such asset, so **100% of animation is inspector-authored per prefab**.

## Dead weight — do not reach for these

`UIButton` (zero references in game code — both projects use plain `UnityEngine.UI.Button`), `UINavigation` (a FIFO `Queue`, so not even a back-stack; zero references), `NamesDatabase` (zero references), `DUISettings` (only meaningful for `UIButton`), `UIBackgroundShowEvent`/`UIBackgroundHideEvent` (never raised or listened to).

---

# B. Reference hierarchy — `D:\Fork\paint-and-seek`

## B1. Script/prefab folder layout

```
Assets/_GameBase/Scripts/UI/
  Panel/          40 files — every BaseUI screen/popup + tab sub-controllers
  Widget/         30 files — reusable non-BaseUI MonoBehaviours (cells, currency, FX, 3D stages)
  Template/       GameplayTemplateUI.cs
  Button/         CommonButtonSetting.cs
  Data/           non-MonoBehaviour view-models & flows (ItemCardView, MapAdUnlockFlow, ShopAdUnlockFlow)
  HighLight/      tutorial spotlight (HighlightPanel, RaycastHole, BackGroundsMask) — dead, zero prefab refs
  Preview/        PreviewModel
  JoyStick&Touch/ joystick pack + editors
Assets/_GameBase/Scripts/Editor/
  BaseUIInspectorProcessor.cs, UIElementPreviewEditor.cs

Assets/_GameBase/Prefabs/UI/
  Panel/    35 panel prefabs      Template/ 12 cell prefabs
  Widget/   9 widget prefabs      Button/   2
```

## B2. `UIMainManager.prefab` — the composition root

Root components: `Canvas`, `CanvasScaler`, `GraphicRaycaster`, `UIManager`.

```
UIMainManager   [Canvas, CanvasScaler, GraphicRaycaster, UIManager]
  ----------Common----------            (empty GO, no components)
    UIBackGround               -> UI/Panel/UIBackGround.prefab
    LoadingPanel   (INACTIVE)  [Canvas, CanvasGroup, GraphicRaycaster, UIPanel]   authored inline
    JoyStickMove / TouchCamera / JumpPanel / SettingPanel(INACTIVE)
    300_NoInternet / 205_WatchAdsPanel / 200_RatingPanel(INACTIVE) / 300_AdBreakPanel
    200_ShopPanel / 300_ScreenTransitionPanel / CountText(=ItemReceivedPanel, stale name)
  ----------Lobby----------
    400_SelectGameplayPanel / 50_InLobbyPanel / TutorialPanel / 200_SelectGameModePanel
    201_MatchSetupPanel / 200_LevelPassPanel / 200_AvatarPanel / 220_InfoPanel
    200_RewardPanel / 200_InventoryPanel / 200_QuestPanel / 200_SellPanel
    200_CratePanel / 230_CrateContentsPanel / 230_GachaPanel
  ----------MainGame----------
    50_CommonInGamePanel / 50_HiderInGamePanel / 50_SeekerInGamePanel
    60_ReverseHiderRollPanel / 250_ReverseScorePanel / 250_ResultPanel / 250_DiedPanel
  ToastWidget          [Canvas(order 210), CanvasGroup, ToastWidget]
  CurrencyFlightLayer  [Canvas(order 290), RewardCeremonyFx, RewardFlightLayer]
```

Every panel is a **prefab instance**; only `LoadingPanel`, `ToastWidget`, `CurrencyFlightLayer` and the three separators are authored inline. `ToastWidget` and `CurrencyFlightLayer` sit outside the separators, as the last two children.

**Naming convention: the `NNN_` prefix is that panel's Canvas `sortingOrder`.** Where the source prefab's order differs, the instance carries an `m_SortingOrder` override — and a few names have gone stale that way (`300_AdBreakPanel` is actually 301, `CountText` is 250).

## B3. Runtime placement

`UIMainManager` is a **scene root object**, sibling to `GameController` — not a child of it:

```
Scn_Main roots: TickableBehavior, FPS, GameController(+ all feature managers as children),
                FastPoolManager, TrackingManager, LoadMapManager, UIMainManager, GameManager, AdManager, Chest
```

Root Canvas setup, worth copying verbatim when building this repo's demo scene:

- `Canvas`: RenderMode **ScreenSpaceOverlay**, no camera, `overrideSorting 0`, `sortingOrder 0`.
- `CanvasScaler`: **ScaleWithScreenSize**, reference resolution **2400 × 1080**, MatchWidthOrHeight **0.5**, refPPU 100.
- `GraphicRaycaster` on the root, and **another on every panel** (required by `UIPanel`).
- **No EventSystem** in `Scn_Main` — a bootstrap creates one at runtime:

```csharp
if (!Object.FindAnyObjectByType<EventSystem>())
    new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
```

- **No safe-area / notch handling anywhere.** Panels rely purely on the CanvasScaler.

## B4. Layering bands

Sibling order is *not* the mechanism — nearly every panel root has its own `Canvas` with `overrideSorting: 1` and an explicit `sortingOrder`.

| order | occupants |
|---|---|
| 0 | root canvas, UIBackGround (dynamic), TouchCamera |
| 1 | JumpPanel |
| 50–60 | in-game HUDs, InLobbyPanel, ReverseHiderRollPanel |
| 100 | LoadingPanel |
| 200–230 | lobby popups (Shop, Inventory, Quest, Reward, Avatar, LevelPass, Crate, Sell, Setting, SelectGameMode; MatchSetup 201, Info 220, CrateContents/Gacha 230) |
| 205 | ClaimItemPanel |
| 210 | ToastWidget |
| 250 | ResultPanel, DiedPanel, ReverseScorePanel, ItemReceivedPanel |
| 290 | CurrencyFlightLayer |
| 300 / 301 | ScreenTransitionPanel, NoInternetPanel / AdBreakPanel |
| 400 | SelectGameplayPanel |
| 500 | TutorialPanel |

Inside a panel, animated sub-groups get a nested Canvas at `parentOrder + 1` — some with `overrideSorting: 1` (a real re-sort), some with `0` (the Canvas exists only because `UIElement` requires it). Nested canvases serialize `m_RenderMode: 2`, which is inert; they inherit Overlay from the root.

## B5. Panel anatomy

**Full-screen panel** — root `[Canvas(overrideSorting=1, order=200), CanvasGroup, GraphicRaycaster, UIPanel, <PanelScript>]`, then `Board > CloseButton`, `Title`, a `ScrollRect > Viewport[RectMask2D] > Content > cards`.

**Popup** — same root signature; mutually-exclusive states are sibling roots with one deactivated (`QuestRoot` / `RefreshRoot`), FX live in a `FrontFx` group of `UIParticle` emitters, and a full-screen `TapButton` closes it.

**Template cell** — root `[CanvasRenderer, Image, Button, CanvasGroup, <WidgetScript>]`. **No Canvas, no GraphicRaycaster, no UIPanel.** Cells are plain graphics; only panels and animated sub-groups get the Canvas trio.

**`RewardPanel.prefab`** is the closest reference for this repo's package UI — 13 nested Canvases in one prefab, one per animated group:

```
RewardPanel  [Canvas(ov=1, 200), CanvasGroup, GraphicRaycaster, UIPanel, RewardPanel]
  Window > BG
    Header    [Image, Canvas(201), CanvasGroup, GraphicRaycaster, UIElement]  > GiftIcon, Title, CloseButton
    Body
      Sidebar [.., UIElement] > DailyTab/PlaytimeTab/FreeTab  (each: Icon, Text, RedDot -> Widget/RedDot.prefab)
      Content
        DailyRoot (INACTIVE) [RewardDailyTab, Canvas(ov=1,201), .., UIElement]
          DailyGrid [GridLayoutGroup] > DailyItemTemplate (INACTIVE) [Image, Button, RewardDailyItem]
                                          > Label, Icon, ClaimedCheck(INACTIVE), RedDot, Amount, SelectFrame(INACTIVE)
          Day7Root [.., UIElement] > Day7Title, Light[SpinEffect], Chest, PremiumText, ClaimRewardButton
          ClaimAllButton (INACTIVE) [.., UIElement]
        PlaytimeRoot [RewardPlaytimeTab, .., UIElement]
          PlaytimeGrid > PlaytimeSlotTemplate (INACTIVE) [Image, Button, RewardPlaytimeSlot]
          OpenAllButton / X2Button / X5Button(INACTIVE)   [.., UIElement]
        FreeRoot (INACTIVE) [RewardFreeTab, .., UIElement]
```

Note there is **no serialized `UIElement[]` array on the prefab** — animated children are individual `UIElement` GameObjects, each with its own in/out block, collected by the panel script into `animatedElements`.

## B6. Code patterns worth carrying over

**Panel** — `bool started` guard, remove-then-add listeners, `IsVisible()`-gated refresh:

```csharp
public void StartClass()
{
    if (started) return;
    started = true;
    closeButton.onClick.RemoveListener(ClosePanel);
    closeButton.onClick.AddListener(ClosePanel);
    EventManager.Instance.RemoveListener<RewardChangedEvent>(OnRewardChanged);
    EventManager.Instance.AddListener<RewardChangedEvent>(OnRewardChanged);
    dailyTab.StartClass(); playtimeTab.StartClass(); freeTab.StartClass();
}
private void OnDestroy()
{
    if (EventManager.Instance == null) return;
    EventManager.Instance.RemoveListener<RewardChangedEvent>(OnRewardChanged);
}
private void OnRewardChanged(RewardChangedEvent e) { if (IsVisible()) RefreshAll(); }
```

**First paint must not tween** — a flag around the first `RefreshAll()`:

```csharp
Show();
animateRefresh = false; RefreshAll(); animateRefresh = true;
PlayShowAnimation();
```

**Widget** — identity args + callback, remove-then-add on a method group:

```csharp
public void StartClass(PoseType pose, string labelText, Sprite iconSprite, Action<PoseType> clickedCallback)
{
    Pose = pose; label.text = labelText;
    onClicked = clickedCallback;
    button.onClick.RemoveListener(HandleClicked);
    button.onClick.AddListener(HandleClicked);
}
```

**Array of cards** — capture the index in a local, never the loop variable:

```csharp
for (int i = 0; i < packCards.Length; i++) { int index = i; packCards[i].StartClass(() => OnPackClicked(index)); }
```

**DOTween sequence lifetime**:

```csharp
Sequence showSequence;
void PlayShowAnimation()
{
    KillShowSequence();
    showSequence = DOTween.Sequence().SetUpdate(true).SetLink(gameObject);
    showSequence.Append(title.DOScale(1f, 0.45f).SetEase(Ease.OutBack));
    showSequence.Join(DOVirtual.Int(0, expAmount, countUpDuration, v => expAmountText.text = v.ToString()));
    showSequence.OnComplete(() => SetClaimButtonsInteractable(true));
}
void KillShowSequence() { showSequence?.Kill(); showSequence = null; }
void OnDestroy() { KillShowSequence(); }
```

**Ad-timeout release** — a dropped ad request never calls back, which would disable the claim buttons forever:

```csharp
private async UniTaskVoid ReleaseWhenNoAdShown()
{
    await UniTask.NextFrame();
    if (!this || adResolved) return;
    OnAdSkipped();
}
```

**Editor tooling** — `BaseUIInspectorProcessor` is an `OdinAttributeProcessor<BaseUI>` that strips the package's `[Button]` off `Show`/`Hide` and injects green/red buttons onto any method literally named `OpenPanel` / `ClosePanel`. That is why panels declare no `[Button]` for those two. It lives in `Assembly-CSharp`, so it does **not** reach package panels.

## B7. Event layer

`NabaGame.Core.Runtime.EventManager` — a plain C# lazy singleton (not a MonoBehaviour) with `Dictionary<Type, EventDelegate>` + `delegateLookup`. API: `AddListener<T>`, `RemoveListener<T>`, `RemoveAllListener()`, `Raise(GameEvent)` (synchronous, no-op with no listeners). `AddListener` is already duplicate-safe via `delegateLookup.ContainsKey`, but the code never relies on that.

Payloads: `public class X : GameEvent` with public fields + a convenience constructor, all in one global-namespace file. Two coexisting subscription conventions — `OnEnable`/`OnDisable` for panels that truly deactivate, `StartClass`/`OnDestroy` for those that don't. Given trap #1 above (panels usually stay active), **prefer `StartClass`/`OnDestroy`** unless the prefab sets `disableWhenHidden`.
