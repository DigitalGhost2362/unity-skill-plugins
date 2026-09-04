# `com.nabagame.ui` framework reference

Read this only if the project depends on `com.nabagame.ui` (`BaseUI` / `UIPanel` / `UIElement`).
Nothing here applies to a project on a different UI framework.

The `SetInfo` members quoted below are the framework's own API, read from its source. They are NOT
the project init convention - that is `Initialize(...)` (`unity-coding` Rule 6). `BaseUI.SetInfo()`
existing as a no-arg virtual it tells you never to override is exactly why the convention moved off
that name.

---

## The package

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

