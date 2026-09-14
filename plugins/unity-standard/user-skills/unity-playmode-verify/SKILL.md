---
name: unity-playmode-verify
description: Runbook for proving Unity behavior in play mode through the MCP bridge - preconditions and the Editor focus trap, reaching a clean state, driving features from script-execute, calling debug buttons (public or by reflection), what evidence to collect, and the three standing passes (re-entry, null-reference, null-callback). Use whenever a task must be proven rather than argued, when running a feature checklist, when reproducing a state/timer/save bug, and before reporting ANY Unity task done. Covers behavior; layout verification lives in unity-ui-from-image.
---

# Play-mode verification

A Unity task is done when the Editor produced evidence for it, or when you state plainly that it was
not verified. Recompiling is `unity-hot-reload`'s job - do that first, then come here.

## Rule 0 - Preconditions, in order

0. **You hold the Editor lock** (`unity-shared-editor`). Other sessions may share this Editor; if the
   lock is taken or a human is playing, write the test cases instead and stop.
1. **Unity Editor open on this project and holding OS focus.** An unfocused Editor never finishes
   compiling: `isCompiling` stays true forever and no MCP call unsticks it. If a compile does not
   return, ask the user to click the Editor window. Plan for it - if the user is away, report the
   change as written but not compiled rather than waiting on a request that cannot finish.
2. MCP reachable (`ping`).
3. Code compiled with a clean console (`console-get-logs`). `editor-application-set-state` **throws
   while compile errors exist**, so this step is not optional.
4. The right scene open (`scene-open`) before entering play mode.

**Never verify by reasoning about the code.** Either the Editor produced evidence, or verification
was not run. A code reading is never a play-mode result.

## Rule 1 - Reach the state you claim to test

Persistent state survives play sessions and Editor restarts, so "fresh install" is a deliberate
wipe, not the default:

```csharp
// script-execute, edit or play mode
foreach (var k in new[]{ /* every key this feature writes */ })
    PlayerPrefs.DeleteKey(k);
PlayerPrefs.Save();
```

If the project ships a reset entry point (a `PreviewResetEverything`-style debug button on the boot
controller), prefer it - it resets the systems as well as the storage, and repaints the UI.

Nothing accrues before boot: the boot controller's `Start()` assigns hooks and calls every
`Initialize()`, and anything time-based starts counting from its own `Initialize`. Enter play mode,
then act.

## Rule 2 - Prefer public levers over reflection

Reach the feature through the project's UI/composition root and drive it with its real public API:
open/close, reset, the config setters, the queries that read state back. Buttons are invoked as the
player would: `someButton.onClick.Invoke()`. Config setters that apply live and repaint mean a
config test does not need a panel reopen.

Write down, per feature, the two lists you will use: **drive it with** and **read state with**. If
that mapping is not obvious from the code, the panel's `API` region is under-built - say so.

## Rule 3 - Debug buttons, public or by reflection

`unity-ui-panel` makes a `[Button, DisableInEditorMode]` debug surface mandatory on every panel, so
in a well-built project the state you need is one call away. Public ones are called straight from
`script-execute`. Private ones need reflection:

```csharp
var c = Object.FindObjectOfType<BootController>();
c.GetType().GetMethod("PreviewResetEverything", System.Reflection.BindingFlags.NonPublic
    | System.Reflection.BindingFlags.Instance).Invoke(c, null);
```

A debug method that returns early with a "called before `Initialize(rows)`" error means the boot
order broke - that is a real finding, not a problem with the call.

If the project has an in-build cheat menu (SRDebugger `SROptions` or similar), the same entry points
are usually mirrored there. Use it when verifying that the menu itself works; use `script-execute`
for everything else - it is faster and its result is readable.

## Rule 4 - Time and external SDKs cannot be faked

A clock that reads `DateTime.UtcNow` and `Time.realtimeSinceStartupAsDouble` with no injection point
cannot be shifted from a script, and shifting the system clock is not an option.

- Test a rollover by editing the saved payload and re-entering play mode, or by the debug button
  that clears the "last claim" stamp.
- Test a cooldown by shortening it through a config setter, or by reading the "seconds remaining"
  query instead of waiting.
- Test an unlock schedule with the unlock-all debug button, or by shortening the thresholds before
  boot.

Fake ad / IAP outcomes are chosen on the host boot controller (usually private serialized fields -
set them by reflection or in the Inspector while playing). A fake answer that resolves after a delay
must be **read on a LATER `script-execute` call**, never the same one. Always include the outcome
that never answers at all - that is the one that reveals a busy-flag deadlock.

## Rule 5 - Evidence to collect

`console-get-logs` after each step, and `screenshot-game-view` when the claim is visual.

Expect to see, by name: the audit log for every state change that moved; the fake-SDK line for each
request with the placement the spec names; the "was NOT granted" error when a callback is missing
(a **pass** in the null-callback test and a **failure** anywhere else); the "no mapping for key"
error for an unmapped key - a silent success there is the bug; the "version mismatch, resetting"
line after a save-payload version bump.

Zero unexpected errors or warnings in the console is part of the pass.

## Rule 6 - The three standing passes

Run these on any change to a panel, regardless of the feature:

1. **Re-entry** - call `Initialize(rows)` again and open/close the panel five times. Assert nothing
   fired twice (one outcome per action in the log) and no child/cell count grew.
2. **Null-reference** - null a serialized reference (button, label, template, one authored
   card/slot/cell) and repeat the flow. No exception, no stuck flow, the other elements still work.
3. **Null-callback** - clear one row's outcome callback and trigger it. Exactly one loud error, no
   state movement, every other row unaffected.

Leave the scene as you found it: do the wiring in play mode only, and confirm `scene-list-opened`
reports `IsDirty: false` afterwards.

## Rule 7 - Report honestly

State what ran and what it showed: the checklist items you executed, the log lines that prove each,
and anything you could not run (Editor closed, needs a real SDK, needs a multi-day wait) called out
as **not verified**. Never present a code reading as a play-mode result, and never let "it should
work" stand in for a screenshot or a log line.
