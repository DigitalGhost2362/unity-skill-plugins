---
name: reward-verify
description: Reward-repo delta on the studio play-mode runbook. Read unity-standard:unity-playmode-verify first - it owns the preconditions, the focus trap, the evidence rules and the three standing passes. This file holds the reward specifics - the demo scene and save keys, the per-feature lever table, the public and private Preview* inventory, the SRDebugger hack menu, the ad/IAP outcome fields, and the exact log lines to expect. Use whenever a task must be proven in play mode, when running a FEATURES checklist, when reproducing a claim/spin/timer/save bug, and before reporting any package task done.
---

# Play-mode verification - reward delta

**Read `unity-standard:unity-playmode-verify` first.** It is the runbook: preconditions and the
Editor focus trap, reaching a clean state, public levers over reflection, why time and SDKs cannot be
faked, what evidence to collect, the three standing passes, and reporting honestly.

`AGENTS.md` requires play-mode verification on every task, or an explicit statement that it was not
run. Recompiling is `unity-hot-reload`'s job; do that first, then come here. Layout verification
lives in `ui-from-image`.

## The scene

`Assets/_RewardDemo/Scenes/RewardSample.unity` - Unity only sees this path; it is the same files as
`Packages/com.nabagame.reward/Samples~/RewardDemo/Scenes/`. `scene-open` it before entering play mode.

## Fresh install

```csharp
// script-execute, edit mode or play mode: full fresh install
foreach (var k in new[]{ "NabaReward.Daily", "NabaReward.Spin", "NabaReward.Online",
                         "Sample.Cash", "Sample.Spin", "Sample.NoAds" })
    PlayerPrefs.DeleteKey(k);
PlayerPrefs.Save();
```

In play mode the same reset is one call: `Object.FindObjectOfType<SampleGameController>()` then
invoke its private `PreviewResetEverything()` - it resets the granter, all three profiles and
repaints the currency panel.

Nothing accrues before boot: `SampleGameController.Start()` assigns the hooks and calls every
`Initialize()`, and Online Reward starts counting playtime from its `Initialize`. Enter play mode,
then act.

## Public levers

Reach panels through `SampleUIManager.Instance` (`dailyRewardPanel`, `luckySpinPanel`,
`onlineRewardPanel`, `currencyPanel`, `itemReceivedPanel`, `homePanel`).

| Feature | Drive it with | Read state with |
|---|---|---|
| Daily | `OpenPanel()`, `ClosePanel()`, `ResetProfile()`, config setters (`OpenAllUseAds`, `OpenAllAdsRequired`, `OpenAllIapProductId`, ...) | `DayCount`, `StreakDay`, `ClaimableCount`, `UnopenedCount`, `GetState(day)` |
| Spin | `OpenPanel()`, `ClosePanel()`, `ResetProfile()`, `FreeSpinCooldownSeconds`, `SpinDurationSeconds`, `SpinFullTurns` | `FreeSpinReady`, `SecondsUntilFreeSpin`, `IsSpinning`, `CanSpinByAd` |
| Online | `OpenPanel()`, `ClosePanel()`, `ResetSession()`, `X2/X5DurationSeconds`, `X2/X5AdsRequired`, `OpenAll*` | `SlotCount`, `GetState(slot)`, `HasClaimable` |

Buttons are invoked as the player would: `someButton.onClick.Invoke()`. Config setters apply live and
repaint, so a config test does not need a panel reopen.

## Debug buttons: public now, the rest by reflection

Every `Preview*` method is `[Button, DisableInEditorMode]`. Since 0.3.0 the ones with no public
equivalent are **public** - call them straight from `script-execute`:

```csharp
SampleUIManager.Instance.luckySpinPanel.PreviewForceWedge(5);
```

Public (the eight with no equivalent on the panel UI): Daily `PreviewExpireToday`,
`PreviewSetStreakDay(int)`; Spin `PreviewForceWedge(int)`, `PreviewSetCooldownSeconds(int)`,
`PreviewRollDistribution(int)`; Online `PreviewAddSeconds(double)`, `PreviewUnlockAll`,
`PreviewClearSpeedUps`. Each returns early with a `debug call arrived before Initialize(rows)`
LogError if the panel was never initialized - seeing that line means the boot order broke, not the
hack.

Still private - either they duplicate `OpenPanel` / `ClosePanel` / `ResetProfile` / `ResetSession`,
or the panel UI already does them: `PreviewOpen`, `PreviewClose`, `PreviewResetAndReopen`,
`PreviewResetSession`, `PreviewClaimToday`, `PreviewSpinFree`, `PreviewSpinByAd`,
`PreviewOpenAllAds`, `PreviewOpenAllIap`, `PreviewSpeedUp`, `PreviewActivateSpeedUp`. Host privates:
`SampleGameController.PreviewResetEverything`, `SampleItemReceivedPanel.PreviewSingle /
PreviewMergeDuplicates / PreviewManyCards / TestRaiseGrant`, `SampleRedDot.PreviewToggle`.

**Driving it as a tester would**: `Samples~/RewardHackMenu/SROptions.HackReward.cs` (junctioned at
`Assets/_RewardHack`) puts those eight plus the three resets in SRDebugger's Options tab as 14
entries across `Reward - Daily`, `Reward - Online` and `Reward - Spin`, reachable in a build.
SRDebugger has no `Settings.asset` in this project, so it runs on defaults: triple-tap the top-left
of the game view to open it. From `script-execute` the same menu is `SROptions.Current.<member>`. Use
it when verifying that the menu itself works; use `script-execute` for everything else.

## Time, ads and IAP

`RewardClock` reads `DateTime.UtcNow` and `Time.realtimeSinceStartupAsDouble` with no injection
point. Do not try to shift the system clock from a script.

- Daily rollover: `PreviewExpireToday()` (clears `LastClaimDateUtc`, so today becomes claimable
  again) or `PreviewSetStreakDay(n)`. For a true rollover reading, edit the saved json
  (`PlayerPrefs.GetString("NabaReward.Daily")`), write it back, then re-enter play mode.
- Spin cooldown: set `FreeSpinCooldownSeconds = 1` and wait, or read `SecondsUntilFreeSpin` instead
  of waiting.
- Online unlocks: `PreviewUnlockAll()`, or shorten the rows' `UnlockAfterSeconds` in the manager
  before boot; speed-ups are cleared with `PreviewClearSpeedUps()`.

The ad and IAP outcome is chosen on `SampleGameController` (private serialized fields; set them by
reflection or in the Inspector while playing): `adResult` = `Reward` / `Skip` / `Swallow` (Swallow
answers never - the correct way to test that `RewardFlow.Busy` does not deadlock the panel),
`failIapPurchase`, `fakeIapPrice`. The fake ad answers after 0.5s of realtime, so read the result on
a **later** `script-execute` call, never in the same one.

## Log lines to expect

- The grant audit log for every reward that moved, plus `SampleRewardGranter` raising the
  currency/granted events.
- `[SampleGameController] fake ad ... -> <result>` for each ad request, with the placement the spec
  names.
- `was NOT granted` when a row has no `OnClaimed` - a *pass* in the null-callback test and a
  *failure* anywhere else.
- `[SampleRewardGranter] reward key ... has no mapping` for an unmapped key; a silent grant is the bug.
- `[RewardProfileStore] ... is version ... resetting` after a `ProfileVersion` bump.

Zero unexpected errors or warnings in the console is part of the pass.

## Reporting

Name the checklist numbers from `Documentation~/FEATURES/<feature>.md` you executed and the log lines
that prove each. Anything you could not run - Editor closed, needs a real ad SDK, needs a multi-day
wait - is called out as **not verified**.
