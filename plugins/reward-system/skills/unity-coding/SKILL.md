---
name: unity-coding
description: Mandatory coding standard for ALL C# script work in the reward-system Unity project — the embedded package Packages/com.nabagame.reward/ and the Samples~/RewardDemo demo host (symlinked at Assets/_RewardDemo) — panels, systems, refactors, and any non-trivial edit (UI panels ALSO follow unity-ui-panel on top of this). Enforces ponytail-unity minimalism, the fixed panel #region vocabulary, and reuse-first: never duplicate a capability (timing, events, saving, async, config) that a shared system already provides. Trigger on any request to write, add, refactor, fix, or review C# code in this repo.
---

# Unity Coding Standard (reward hosts)

Six rules, applied in this order, on every C# task.

## Rule 0 — Know which side of the boundary you are writing

This repo has two code territories with **different** rules. Decide which one the file belongs to first, then apply the right column everywhere below.

| | `Packages/com.nabagame.reward/Runtime/` (the product) | `Samples~/RewardDemo/Scripts/` (demo host, = `Assets/_RewardDemo/Scripts` via symlink) |
|---|---|---|
| Namespace | `NabaGame.Reward` | `NabaGame.Reward.Sample`, every type prefixed `Sample` |
| Assembly | `NabaGame.Reward.asmdef` | `Assembly-CSharp` (compiled through the Assets symlink) |
| May reference | `com.bmh.core.runtime`, `com.nabagame.ui.runtime`, UniTask, Odin, DOTween | anything, incl. the package |
| May NOT reference | game code, game enums, ES3, an ads SDK, `TrackingManager` | — |
| Persistence | PlayerPrefs + `JsonUtility` via `RewardProfileStore`, keys `NabaReward.*`, `int Version`, save-on-mutation | PlayerPrefs (`Sample.*` keys) |
| Gets host services via | static `RewardHooks` (`PlaySfx`, `ShowRewardedAd`, `PurchaseIap` — safe defaults, assigned at boot) | direct calls / its own adapters |

Package docs are authoritative and override this skill on conflict:
`Packages/com.nabagame.reward/Documentation~/ARCHITECTURE.md` (Vietnamese, for consumers) and the internal English rule book in `.claude/docs/reward-package/` (local only). For package work, invoke the `reward-system:reward-package` skill as well.

**Compiling: Hot Reload is NOT installed in this repo.** Every C# change needs a real recompile. Write the file, then `assets-refresh` (or use `script-update-or-create`, which validates via Roslyn and refreshes for you), then read `console-get-logs`. Play mode must be stopped for a script change to take effect. Do not assume an edit is live.

## Rule 1 — Always apply ponytail-unity first

Invoke the `ponytail-unity` skill (default level: full) before writing any code. Its core demands, which this skill inherits:

- Least code that works. Question whether the task needs new code at all.
- Engine-native features and existing project helpers before new code; Inspector/prefab wiring before runtime construction.
- No speculative abstraction: no interface, base class, or config option for a future that isn't scheduled.
- Path-scoped reads; don't dump whole files to "understand" them.

## Rule 2 — `#region` grouping

**Package panels use the fixed four-region vocabulary, in order: `API`, `Logic`, `UI`, `Debug`** (AGENTS.md). `API` comes first and is self-sufficient — a consuming dev reads only it (SetInfo, OpenPanel/ClosePanel, red-dot queries, reset, placement consts). Same names in every panel.

Other multi-concern classes: region per concern, names describe the concern (never the access level), `Debug` always last, fields and tiny computed properties above the first region. A class with a single concern (widget, event payload, row class, thin sample manager) gets NO regions.

## Rule 3 — Reuse-first: shared systems inventory

Before writing ANY capability, check this table, then grep for prior art. Re-implementing a row locally is a bug, not a style choice.

| Need | Use | Never write |
|---|---|---|
| Wall-clock / monotonic time, "run X at unix time T" (cooldowns, rollover, slot timers), countdown cadence | `RewardClock` (`Runtime/Core/`): `NowMs`, `UtcNow`, `TodayUtc`, `NextUtcMidnightMs`, `SecondsUntil`, `MonotonicSeconds`, `MsUntilNextTick(remaining, rate)` — the only file that reads `DateTime*`/`realtimeSinceStartup*`; `TimeScheduler`: `Schedule`, `Cancel` — one shared 1s realtime loop, try/caught callbacks | Per-class `DateTimeOffset.UtcNow`/`DateTime.UtcNow` + CTS + fixed `UniTask.Delay(1000)` plumbing (skips digits under speed-up). **Anything in `Update()`.** |
| Background/suspend handling | `Application.focusChanged` (static — fires on deactivated GameObjects) | `OnApplicationPause`/`OnApplicationQuit` on a panel — they die when the host deactivates it |
| Cross-system notification | package `GameEvent` subclasses (per-feature `*Events.cs`) via `NabaGame.Core.Runtime.EventManager`; fresh instance per raise | C# static events, per-frame polling, cached mutated payloads |
| Persistence | `RewardProfileStore` (PlayerPrefs + JsonUtility, `NabaReward.*`, `Version`) — save on every mutation | ES3, save plugins, pause-time save passes |
| Granting a reward | `Row.OnClaimed` callback + the mandatory audit `Debug.Log`; sample maps keys in `SampleRewardGranter.Grant` | grant events, `IRewardGranter`, package knowledge of what a Gem is |
| Rewarded ads | `AdFlow` over `RewardHooks.ShowRewardedAd`: `Busy` guard, next-frame release, ~15s timeout failsafe | Package referencing an ads SDK; flows that can latch a button dead |
| SFX / IAP | `RewardHooks.PlaySfx` / `RewardHooks.PurchaseIap` (statics with safe defaults) | Package referencing `AudioManager`/`SoundType`/an IAP SDK |
| Button wiring | `RewardUi.Bind(button, handler)` — null-guard + Remove-then-Add | bare `AddListener` (duplicates on re-init, NREs on missing buttons) |
| Async flows | `UniTask` (+ `GetCancellationTokenOnDestroy`); countdown loops gate on `IsVisible()` | Coroutines; `while (true)` UI loops |
| Tunable numbers | `[SerializeField]` knobs on the owning panel prefab | Hardcoded constants; config SOs |
| Debug/cheat buttons | Odin `[Button, DisableInEditorMode]` in the panel's `Debug` region | Debug logic inside flows |

When a needed capability has no shared owner yet: create ONE shared system in `Runtime/Core/`, then migrate only the call sites you are already touching. A new package dependency requires an ARCHITECTURE.md §9 decision-record entry first.

## Rule 4 — Fail loudly, with the leniency ladder

Per AGENTS.md and ARCHITECTURE decision #24:

- An unknown `Row.Key` reaching the host granter must `Debug.LogError` naming the key — never silently map it to a "reasonable" reward.
- Incomplete row data (missing `Key`/`Icon`, bad `Amount`/`Weight`) **warns** via `{Feature}Row.Warn` and keeps running; only structure the machine cannot run with **throws** (empty list, <2 wedges, non-increasing unlocks).
- A claimed row with null `OnClaimed` `LogError`s "was NOT granted"; unset `RewardHooks` LogError and proceed.
- A `switch`/`default` over an internal id still throws `ArgumentOutOfRangeException` naming the value.
- **Serialized UI references are optional** (decision #26): guard every dereference — a deleted button is a designed workflow, not a wiring bug. Everything else that is required still fails loud.

## Rule 5 — Comments

UI code (panels, widgets, FX, templates) stays near-zero-comment per unity-ui-panel; hidden constraints are the exception. All other C#:

- No comments unless the name alone cannot convey intent (hidden constraint, subtle invariant, known footgun).
- When a comment is needed: one line, `//` only, lowercase start, short. No XML doc / `<summary>`.
- Comments add **new** information — never restate what the name already says.

## Rule 6 — Initialization is `SetInfo()`

`SetInfo(...)` is the single init convention everywhere (`StartClass` is retired — never reintroduce it). Panels: `SetInfo(List<{Feature}Row>)`; activation is `OpenPanel()`/`ClosePanel()` (names are load-bearing for `BaseUIInspectorProcessor`). Boot order: hooks first, then `SetInfo` chains from `Start()` — never `Awake()`, and Online Reward always at boot.

## When touching a file that violates these rules

Migrate only the parts the task touches. Report every extra file changed.
