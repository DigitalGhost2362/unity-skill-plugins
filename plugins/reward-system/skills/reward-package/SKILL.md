---
name: reward-package
description: Mandatory workflow for building the reusable UPM package Packages/com.nabagame.reward/ (namespace NabaGame.Reward) — the entire purpose of this repo. Covers the folder contract, asmdef boundaries, the panel-owned contract (decisions #21-#27: one prefab per feature, rows in via Initialize, grants out via Row.OnClaimed, static RewardHooks), the leniency ladder, and the definition of done. Use for ANY work on package code, package docs, the sample, or integration questions.
---

# Reward package workflow (`com.nabagame.reward`)

The package docs are **authoritative** and outrank this skill on any conflict. Read before touching the package:

- `Packages/com.nabagame.reward/Documentation~/ARCHITECTURE.md` — dependency rules, the panel-owned reward model, static hooks, `OnClaimed` grants, events, time, save, ads, and the decision record (#1–#27).
- `.../Documentation~/FEATURES/{daily-reward,online-reward,lucky-spin}.md` — per-feature specs.

## Current state

Phases 0–4.5 are done — package `0.8.0`, all three features shipped on the **panel-owned contract** (decisions #21–#27, 2026-08-20). The demo host is `Assets/_RewardDemo/`, a **symlink view** of `Samples~/RewardDemo/` — one source, no mirror step; one scene `RewardSample.unity`. Next: phase 5 (import dry-runs, polish, `1.0.0`).

## Rule 1 — The dependency boundary is the product

```
Assembly-CSharp  ->  NabaGame.Reward  ->  { com.bmh.core.runtime, com.nabagame.ui.runtime, UniTask, Sirenix, DOTween }
```

One-way, enforced by asmdef. `NabaGame.Reward.asmdef` **never** references `Assembly-CSharp`.

Banned inside the package, without exception: game enums (`RewardType`, `RewardID`, `SoundType`, …), game singletons (`GameController`, `UIManager`, `GameManager`, `AudioManager`, …), Easy Save 3, any ads SDK, `TrackingManager`, and any new third-party dependency without an ARCHITECTURE §9 decision first. If package code seems to need one of these, the answer is a hook, a callback, or an event — not a reference.

## Rule 2 — Folder contract

```
Packages/com.nabagame.reward/
  Runtime/
    Core/                      shared by 2+ features: RewardHooks (static), RewardClock, TimeScheduler,
                               AdFlow, RewardProfileStore, RewardUi, RewardAmountFormat
    Features/{DailyReward,OnlineReward,LuckySpin}/
                               row + profile + panel + widget + events per feature
    NabaGame.Reward.asmdef
  Samples~/RewardDemo/         THE demo host (symlinked at Assets/_RewardDemo): one scene,
                               Sample*-prefixed scripts, sample art, prefabs
  Documentation~/              docs (NOT imported by Unity)
```

Feature folders never reference each other; `Core/` never references a feature. Namespaces: `NabaGame.Reward` (runtime), `NabaGame.Reward.Sample` (sample, every type prefixed `Sample`). **Never `PainAndSeek`.**

## Rule 3 — One panel, one row list, grants via `OnClaimed` (decisions #21–#24)

There is **no package-side manager**. `{Feature}Panel` owns save, timers, ads, IAP, and rules. The host writes a ~15-line manager (templates: `Samples~/RewardDemo/Scripts/Sample{Feature}Manager.cs`) that fills `[TableList] public List<{Feature}Row> rows`, assigns each row's `OnClaimed`, and calls `panel.Initialize(rows)` at boot.

Everything the dev fills lives in the one row class — `Key` (opaque), `Icon`, `Amount`, `ClaimSfx`, `OnClaimed`, plus feature extras (`Weight`, `UnlockAfterSeconds`, `LabelOverride`). List position is the index — there is no `Day`/`Wedge`/`Slot` field. Rows ship constructors for code authoring and a parameterless ctor for the Inspector.

Grants: the panel mutates + saves, `Debug.Log`s the grant (mandatory audit line), then invokes `Row.OnClaimed`. Null callback → `LogError` "was NOT granted". **No grant events exist.**

Leniency ladder: incomplete row data → one aggregated `LogWarning` via `{Feature}Row.Warn(rows, context)`; structural breakage (empty list, <2 wedges, non-increasing unlocks) → throw naming index+value; unset hooks → LogError then proceed. A half-filled prefab must run and complain, never brick.

## Rule 4 — Static hooks, `Initialize` lifecycle (decisions #23, #25)

`RewardHooks` is a **static class with safe defaults** (`PlaySfx` no-ops; `ShowRewardedAd`/`PurchaseIap` LogError then reward/succeed), assigned once at boot before any `Initialize`, reset via `[RuntimeInitializeOnLoadMethod(SubsystemRegistration)]`.

Lifecycle naming: `Initialize(List<{Feature}Row>)` is the **single** init (validate → load save → arm timers → bind the authored board list / build dynamic templates → bind listeners idempotently). `OpenPanel()`/`ClosePanel()` are the dev-facing activation APIs — **never rename them**, `BaseUIInspectorProcessor` string-matches those names. `StartClass` and `SetInfo` are retired; never reintroduce them. Call `Initialize` from `Start()`, never `Awake()`; Online Reward must be initialized at boot (accrual starts there).

Panel regions, fixed vocabulary in order: `#region API` (first — init, open/close, red-dot queries, reset, placement consts; a dev reads only this), `Logic`, `UI`, `Debug`.

## Rule 5 — Null-tolerant UI (decision #26)

Every serialized UI reference may be disabled or deleted by the dev: guard every dereference, `LogError`-and-skip a missing template or empty authored board list, silently skip null authored-list entries, bounds-check cell/wedge indexing, keep click callbacks null-safe (`RewardUi.Bind` handles buttons). `AdFlow.Busy` auto-releases after ~15s when a host SDK swallows both callbacks — a flow may fail, it must never stick. `OpenPanel` before `Initialize` logs one error and refuses.

## Rule 6 — Time and persistence

**Time.** `RewardClock` in `Core/` is the only clock: `NowMs`, `UtcNow`, `TodayUtc`, `NextUtcMidnightMs`, `SecondsUntil`, `MonotonicSeconds`, `MsUntilNextTick` — no other Runtime file reads `DateTime*` or `Time.realtimeSinceStartup*`. `TimeScheduler` (`Schedule`, `Cancel`) runs deadlines on one shared 1-second realtime loop; callbacks are try/caught. Accrued time uses the monotonic-baseline pattern; the Online flush rides `Application.focusChanged` (never `OnApplicationPause` — it dies on a deactivated panel). **No `Update()` polling.** Countdown loops gate on `IsVisible()`, use `DelayType.Realtime`, and wake at the next displayed-digit boundary via `RewardClock.MsUntilNextTick(remaining, rate)` — one refresh per visible change at any speed-up rate.

**Save.** PlayerPrefs + `JsonUtility` via `RewardProfileStore`. Keys `NabaReward.Daily`/`.Online`/`.Spin`, each profile carries `int Version`. Save on every mutation — there is no pause/quit save pass. Never ES3, never a save plugin.

## Rule 7 — UI and the reference art

One standalone `BaseUI` popup per feature, prefab-authored. Everything in `unity-ui-panel` applies.

**The reference art is local only and never tracked**: Daily/Spin/Playtime previews live in `Assets/_ASMR-Tower/Art/preview/*.jpg` (outside the package; no `Documentation~/RefUI` folder exists in the repo). Never reference that folder, its name, or the source game in anything tracked.

`BaseUIInspectorProcessor` lives in `Assembly-CSharp` and does not reach package panels — package panels carry explicit `[Button, DisableInEditorMode]` debug methods instead.

## Rule 8 — The sample IS the demo host (decision #27)

`Samples~/RewardDemo/` is simultaneously the importable sample and the repo's demo host (via the `Assets/_RewardDemo` symlinks) — edit it once, there is no mirror. One scene (`RewardSample.unity`), every type `Sample`-prefixed, bundled sample art. `SampleGameController` is the composition root; `SampleRewardGranter.Grant` is the only piece that knows what a reward means.

## Rule 9 — Definition of done

- [ ] Lives under `Runtime/Features/<Feature>/`, referencing only `Core/` and allowed externals.
- [ ] `Initialize(rows)` applies the leniency ladder: warns on gaps, throws only on structure, never on hooks.
- [ ] Zero references to game types, game enums, ES3, ads SDKs, or `TrackingManager`.
- [ ] Grants leave only through `Row.OnClaimed` (with the mandatory audit log); the sample's `OnClaimed` grants end to end.
- [ ] Persistence is PlayerPrefs + `JsonUtility` with a `Version` field; save-on-mutation only.
- [ ] No `Update()`; clock reads via `RewardClock`, deadlines via `TimeScheduler`; countdown loops gate on `IsVisible()`.
- [ ] UI is prefab-authored (fixed boards wired into serialized lists, decision #28), regions are `API`/`Logic`/`UI`/`Debug`, `Initialize` cannot duplicate listeners, and the null-button pass holds (including deleting an authored card/wedge).
- [ ] The package compiles standalone with the console clean, and the sample scene drives the feature end to end.

Then bump the version per SemVer and add a CHANGELOG entry.

## Verifying

Hot Reload is not installed — every C# change needs a real recompile (`unity-hot-reload`). Checks to run on package work:

1. **Boundary check** — `grep -rnE 'PainAndSeek|RewardType|RewardID|RedDotKey|GameController|UIManager|GameManager|AudioManager|RuntimeDataManager|ES3\.|TrackingManager' Packages/com.nabagame.reward/Runtime` — any hit is a violation.
2. **Play-mode pass** in `RewardSample.unity`: claim/spin/online grants land in `SampleRewardGranter.Grant` with audit logs; badges and countdowns live; null-`OnClaimed` LogError fires.
3. **Null-button pass**: disable a serialized button/label/template → no exception, no stuck flow.
