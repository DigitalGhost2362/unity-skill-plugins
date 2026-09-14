---
name: unity-coding
description: Reward-repo delta on the studio C# standard. Read unity-coding first; this file adds only what is specific to the reward hosts - the boundary between the embedded package Packages/com.nabagame.reward/ and the Samples~/RewardDemo demo host (symlinked at Assets/_RewardDemo), the concrete owner of every shared capability (RewardClock, TimeScheduler, RewardProfileStore, AdFlow, RewardHooks, RewardUi.Bind), the reward leniency ladder, and the authoritative package docs. Trigger on any request to write, add, refactor, fix, or review C# in this repo.
---

# Unity coding standard - reward delta

**Read `unity-coding` first.** It is the standard: minimalism, reuse-first, the
`API`/`Logic`/`UI`/`Debug` regions, the leniency ladder, the comment policy, and `Initialize()` as
the single init convention. This file adds only what is specific to this repo, and overrides the
generic skill on conflict.

For UI work, `reward-system:unity-ui-panel` applies on top of both.

## Rule 0 - Know which side of the boundary you are writing

This repo has two code territories with **different** rules. Decide which one the file belongs to
first, then apply the right column everywhere below.

| | `Packages/com.nabagame.reward/Runtime/` (the product) | `Samples~/RewardDemo/Scripts/` (demo host, = `Assets/_RewardDemo/Scripts` via symlink) |
|---|---|---|
| Namespace | `NabaGame.Reward` | `NabaGame.Reward.Sample`, every type prefixed `Sample` |
| Assembly | `NabaGame.Reward.asmdef` | `Assembly-CSharp` (compiled through the Assets symlink) |
| May reference | `com.bmh.core.runtime`, `com.nabagame.ui.runtime`, UniTask, Odin, DOTween | anything, incl. the package |
| May NOT reference | game code, game enums, ES3, an ads SDK, `TrackingManager` | - |
| Persistence | PlayerPrefs + `JsonUtility` via `RewardProfileStore`, keys `NabaReward.*`, `int Version`, save-on-mutation | PlayerPrefs (`Sample.*` keys) |
| Gets host services via | static `RewardHooks` (`PlaySfx`, `ShowRewardedAd`, `PurchaseIap` - safe defaults, assigned at boot) | direct calls / its own adapters |

Package docs are authoritative and override this skill on conflict:
`Packages/com.nabagame.reward/Documentation~/ARCHITECTURE.md` (Vietnamese, for consumers) and the
internal English rule book in `.claude/docs/reward-package/` (local only). For package work, invoke
the `reward-system:reward-package` skill as well.

**Hot Reload is NOT installed in this repo.** Every C# change needs a real recompile. Write the
file, then `assets-refresh` (or use `script-update-or-create`, which validates via Roslyn and
refreshes for you), then read `console-get-logs`. Play mode must be stopped for a script change to
take effect. Do not assume an edit is live.

## Rule 1 - Who owns each shared capability here

The generic reuse-first table names capabilities; these are this repo's concrete owners. Writing a
second one is a bug, not a style choice.

| Need | Use | Never write |
|---|---|---|
| Wall-clock / monotonic time, "run X at unix time T" (cooldowns, rollover, slot timers), countdown cadence | `RewardClock` (`Runtime/Core/`): `NowMs`, `UtcNow`, `TodayUtc`, `NextUtcMidnightMs`, `SecondsUntil`, `MonotonicSeconds`, `MsUntilNextTick(remaining, rate)` - the only file that reads `DateTime*`/`realtimeSinceStartup*`; `TimeScheduler`: `Schedule`, `Cancel` - one shared 1s realtime loop, try/caught callbacks | Per-class `DateTimeOffset.UtcNow`/`DateTime.UtcNow` + CTS + fixed `UniTask.Delay(1000)` plumbing (skips digits under speed-up). **Anything in `Update()`.** |
| Cross-system notification | package `GameEvent` subclasses (per-feature `*Events.cs`) via `NabaGame.Core.Runtime.EventManager`; fresh instance per raise | C# static events, per-frame polling, cached mutated payloads |
| Persistence | `RewardProfileStore` (PlayerPrefs + JsonUtility, `NabaReward.*`, `Version`) - save on every mutation | ES3, save plugins, pause-time save passes |
| Granting a reward | `Row.OnClaimed` callback + the mandatory audit `Debug.Log`; the sample maps keys in `SampleRewardGranter.Grant` | grant events, `IRewardGranter`, package knowledge of what a Gem is |
| Rewarded ads | `AdFlow` over `RewardHooks.ShowRewardedAd`: `Busy` guard, next-frame release, ~15s timeout failsafe | Package referencing an ads SDK; flows that can latch a button dead |
| SFX / IAP | `RewardHooks.PlaySfx` / `RewardHooks.PurchaseIap` (statics with safe defaults) | Package referencing `AudioManager`/`SoundType`/an IAP SDK |
| Button wiring | `RewardUi.Bind(button, handler)` - null-guard + Remove-then-Add | bare `AddListener` (duplicates on re-init, NREs on missing buttons) |

When a needed capability has no shared owner yet: create ONE shared system in `Runtime/Core/`, then
migrate only the call sites you are already touching. A new package dependency requires an
`ARCHITECTURE.md` section 9 decision-record entry first.

## Rule 2 - The leniency ladder, in reward terms

Per `AGENTS.md` and ARCHITECTURE decision #24:

- An unknown `Row.Key` reaching the host granter must `Debug.LogError` naming the key - never
  silently map it to a "reasonable" reward.
- Incomplete row data (missing `Key`/`Icon`, bad `Amount`/`Weight`) **warns** via `{Feature}Row.Warn`
  and keeps running; only structure the machine cannot run **throws** (empty list, fewer than 2
  wedges, non-increasing unlocks).
- A claimed row with null `OnClaimed` `LogError`s "was NOT granted"; unset `RewardHooks` LogError and
  proceed.
- **Serialized UI references are optional** (decision #26): guard every dereference - a deleted
  button is a designed workflow, not a wiring bug.

## Rule 3 - Initialization, concretely

Panels take `Initialize(List<{Feature}Row>)`; activation is `OpenPanel()` / `ClosePanel()` (those two
names are load-bearing for `BaseUIInspectorProcessor`, which string-matches them). Boot order: hooks
first, then `Initialize` chains from `Start()` - never `Awake()` - and Online Reward always
initializes at boot.

`StartClass` and `SetInfo` are both retired. Panels in this repo that still declare `SetInfo` are
renamed to `Initialize` only when the task already has the file open for another reason.
