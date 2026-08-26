---
name: reward-verify
description: Runbook for verifying reward-package behavior in the Unity demo host (RewardSample.unity) - how to reach a clean state, drive Daily Reward / Lucky Spin / Online Reward from script-execute, force the ad and IAP outcomes, read the audit logs, and run the three standing passes (re-entry, null-button, null-OnClaimed). Use whenever a task must be proven in play mode, when running a FEATURES checklist, when reproducing a claim/spin/timer/save bug, and before reporting any package task done. Covers behavior, not layout (layout verification lives in ui-from-image).
---

# Play-mode verification (reward-system)

`AGENTS.md` requires play-mode verification on every task, or an explicit statement that it was not run. This skill is how to run it. Recompiling is `unity-hot-reload`'s job; do that first, then come here.

## Rule 0 -- Preconditions, in order

1. Unity Editor open on this project **and holding OS focus**. An unfocused Editor never finishes compiling: `isCompiling` stays true forever and no MCP call unsticks it. If a compile does not return, ask the user to click the Editor window.
2. Feeder MCP reachable (`ping`).
3. Code compiled with a clean console (`console-get-logs`) -- `editor-application-set-state` throws while compile errors exist.
4. Scene open: `Assets/_RewardDemo/Scenes/RewardSample.unity` (Unity only sees this path; it is the same files as `Packages/com.nabagame.reward/Samples~/RewardDemo/Scenes/`). `scene-open` it before entering play mode.

Never verify by reasoning about the code. Either the Editor produced evidence, or verification was not run.

## Rule 1 -- Reach the state you claim to test

Saves survive play sessions and Editor restarts, so "fresh install" is a deliberate wipe, not the default.

```csharp
// script-execute, edit mode or play mode: full fresh install
foreach (var k in new[]{ "NabaReward.Daily", "NabaReward.Spin", "NabaReward.Online",
                         "Sample.Cash", "Sample.Spin", "Sample.NoAds" })
    PlayerPrefs.DeleteKey(k);
PlayerPrefs.Save();
```

In play mode the same reset is one call: `Object.FindObjectOfType<SampleGameController>()` then invoke its private `PreviewResetEverything()` (see Rule 3) -- it resets the granter, all three profiles and repaints the currency panel.

Nothing accrues before boot: `SampleGameController.Start()` assigns the hooks and calls every `SetInfo()`, and Online Reward starts counting playtime from its `SetInfo`. Enter play mode, then act.

## Rule 2 -- Public levers (prefer these)

Reach panels through `SampleUIManager.Instance` (`dailyRewardPanel`, `luckySpinPanel`, `onlineRewardPanel`, `currencyPanel`, `itemReceivedPanel`, `homePanel`).

| Feature | Drive it with | Read state with |
|---|---|---|
| Daily | `OpenPanel()`, `ClosePanel()`, `ResetProfile()`, config setters (`OpenAllUseAds`, `OpenAllAdsRequired`, `OpenAllIapProductId`, ...) | `DayCount`, `StreakDay`, `ClaimableCount`, `UnopenedCount`, `GetState(day)` |
| Spin | `OpenPanel()`, `ClosePanel()`, `ResetProfile()`, `FreeSpinCooldownSeconds`, `SpinDurationSeconds`, `SpinFullTurns` | `FreeSpinReady`, `SecondsUntilFreeSpin`, `IsSpinning`, `CanSpinByAd` |
| Online | `OpenPanel()`, `ClosePanel()`, `ResetSession()`, `X2/X5DurationSeconds`, `X2/X5AdsRequired`, `OpenAll*` | `SlotCount`, `GetState(slot)`, `HasClaimable` |

Buttons are invoked as the player would: `someButton.onClick.Invoke()`. Config setters apply live and repaint, so a config test does not need a panel reopen.

## Rule 3 -- Debug buttons are public now; the private ones need reflection

Every `Preview*` method is `[Button, DisableInEditorMode]`. Since 0.3.0 the ones with no public equivalent are **public** -- call them straight from `script-execute`:

```csharp
SampleUIManager.Instance.luckySpinPanel.PreviewForceWedge(5);
```

Public (the eight with no equivalent on the panel UI): Daily `PreviewExpireToday`, `PreviewSetStreakDay(int)`; Spin `PreviewForceWedge(int)`, `PreviewSetCooldownSeconds(int)`, `PreviewRollDistribution(int)`; Online `PreviewAddSeconds(double)`, `PreviewUnlockAll`, `PreviewClearSpeedUps`. Each returns early with a `debug call arrived before SetInfo(rows)` LogError if the panel was never initialized -- seeing that line means the boot order broke, not the hack.

Still private -- either they duplicate `OpenPanel` / `ClosePanel` / `ResetProfile` / `ResetSession`, or the panel UI already does them: `PreviewOpen`, `PreviewClose`, `PreviewResetAndReopen`, `PreviewResetSession`, `PreviewClaimToday`, `PreviewSpinFree`, `PreviewSpinByAd`, `PreviewOpenAllAds`, `PreviewOpenAllIap`, `PreviewSpeedUp`, `PreviewActivateSpeedUp`. Reach those by reflection:

```csharp
var c = Object.FindObjectOfType<SampleGameController>();
c.GetType().GetMethod("PreviewResetEverything", System.Reflection.BindingFlags.NonPublic
    | System.Reflection.BindingFlags.Instance).Invoke(c, null);
```

Host privates: `SampleGameController.PreviewResetEverything`, `SampleItemReceivedPanel.PreviewSingle / PreviewMergeDuplicates / PreviewManyCards / TestRaiseGrant`, `SampleRedDot.PreviewToggle`.

**Driving it as a tester would**: `Samples~/RewardHackMenu/SROptions.HackReward.cs` (junctioned at `Assets/_RewardHack`) puts those eight plus the three resets in SRDebugger's Options tab as 14 entries across `Reward - Daily`, `Reward - Online` and `Reward - Spin`, reachable in a build. SRDebugger has no `Settings.asset` in this project, so it runs on defaults: triple-tap the top-left of the game view to open it. From `script-execute` the same menu is `SROptions.Current.<member>`. Use it when verifying that the menu itself works; use `script-execute` for everything else, it is faster and its result is readable.

## Rule 4 -- Time cannot be faked, and neither can ads

`RewardClock` reads `DateTime.UtcNow` and `Time.realtimeSinceStartupAsDouble` with no injection point. Do not try to shift the system clock from a script.

- Daily rollover: `PreviewExpireToday()` (clears `LastClaimDateUtc`, so today becomes claimable again) or `PreviewSetStreakDay(n)`. For a true rollover reading, edit the saved json (`PlayerPrefs.GetString("NabaReward.Daily")`), write it back, then re-enter play mode.
- Spin cooldown: set `FreeSpinCooldownSeconds = 1` and wait, or read `SecondsUntilFreeSpin` instead of waiting.
- Online unlocks: `PreviewUnlockAll()`, or shorten the rows' `UnlockAfterSeconds` in the manager before boot; speed-ups are cleared with `PreviewClearSpeedUps()`.

The ad and IAP outcome is chosen on `SampleGameController` (private serialized fields, set them by reflection or in the Inspector while playing): `adResult` = `Reward` / `Skip` / `Swallow` (Swallow answers never -- the correct way to test that `RewardFlow.Busy` does not deadlock the panel), `failIapPurchase`, `fakeIapPrice`. The fake ad answers after 0.5 s of realtime, so read the result on a later `script-execute` call, never in the same one.

## Rule 5 -- Evidence to collect

`console-get-logs` after each step, and `screenshot-game-view` when the claim is visual.

Expect to see, by name:

- The grant audit log for every reward that moved, plus `SampleRewardGranter` raising the currency/granted events.
- `[SampleGameController] fake ad ... -> <result>` for each ad request, with the placement the spec names.
- `was NOT granted` when a row has no `OnClaimed` -- this is a *pass* in the null-callback test and a *failure* anywhere else.
- `[SampleRewardGranter] reward key ... has no mapping` for an unmapped key; a silent grant is the bug.
- `[RewardProfileStore] ... is version ... resetting` after a `ProfileVersion` bump.

Zero unexpected errors or warnings in the console is part of the pass.

## Rule 6 -- The three standing passes

Run these on any change to a panel, regardless of the feature:

1. **Re-entry**: call `SetInfo(rows)` again and open/close the panel five times. Assert nothing fired twice (one grant per claim in the log) and no child/cell count grew.
2. **Null-button**: null a serialized reference (button, label, template, one authored card/wedge/cell) and repeat the flow. No exception, no stuck flow, the other elements still work.
3. **Null-`OnClaimed`**: clear one row's callback, claim it. Exactly one `was NOT granted` error, no currency movement, every other row unaffected.

Leave the scene as you found it: do the wiring in play mode only, and confirm `scene-list-opened` reports `IsDirty: false` afterwards.

## Rule 7 -- Report honestly

State what ran and what it showed: the checklist numbers from `Documentation~/FEATURES/<feature>.md` you executed, the log lines that prove each, and anything you could not run (Editor closed, needs a real ad SDK, needs a multi-day wait) called out as **not verified**. Never present a code reading as a play-mode result.
