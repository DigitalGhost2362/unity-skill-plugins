---
name: unity-coding
description: Mandatory coding standard for ALL C# script work in any Unity project - systems, panels, widgets, editor tooling, refactors, and any non-trivial edit (UI work ALSO follows unity-ui-panel on top of this). Enforces minimalism, reuse-first (never duplicate a capability - timing, events, saving, async, config - that a shared system already provides), the fixed region vocabulary, the leniency ladder, the near-zero comment policy, and Initialize() as the single init convention. Trigger on any request to write, add, refactor, fix, or review C# in a Unity project.
---

# Unity coding standard

Six rules, applied in this order, on every C# task. A project-local `CLAUDE.md`/`AGENTS.md` or a
project-specific skill overrides this file on conflict; everything it does not mention still applies.

## Rule 1 - Minimalism first

Before writing anything:

- **Least code that works.** Question whether the task needs new code at all. A prefab field, an
  existing helper, or a deleted branch beats a new class.
- **Engine-native features and existing project helpers before new code.** Inspector/prefab wiring
  before runtime construction.
- **No speculative abstraction.** No interface, base class, event, or config option for a future
  that is not scheduled. One caller means one concrete method.
- **Path-scoped reads.** Do not dump whole files, prefabs or scenes to "understand" them; read the
  members you need. `.prefab` and `.unity` files are never read whole - grep the few fields, or
  inspect the live objects through the Editor.
- **Migrate only what you touch.** When a file you are editing violates these rules, fix the parts
  the task already reaches, and report every extra file you changed.

## Rule 2 - Reuse-first: find the capability's owner before writing one

A project owns each capability below exactly once. Before writing any of them, grep for the existing
owner (search the capability's verbs and the types in the "never write" column). Re-implementing a
row locally is a bug, not a style choice.

| Capability | What the shared owner provides | Never write |
|---|---|---|
| Wall-clock and monotonic time; run X at unix time T; cooldowns, daily rollover, slot timers, countdown cadence | ONE file that reads `DateTime*` / `realtimeSinceStartup*`, plus a scheduler with `Schedule`/`Cancel` running a single shared realtime loop with try/caught callbacks | Per-class `DateTime.UtcNow` + a CTS + a fixed `UniTask.Delay(1000)` (it skips digits under time speed-up). **Anything in `Update()`.** |
| Background / suspend handling | `Application.focusChanged` - static, so it fires even for deactivated GameObjects | `OnApplicationPause` / `OnApplicationQuit` on a panel: they die the moment the host deactivates it |
| Cross-system notification | The project's event bus, with a **fresh payload instance per raise** | C# static events, per-frame polling, a cached payload that gets mutated |
| Persistence | ONE store wrapping the save backend, with a `Version` field on the payload, saving on every mutation | A second save plugin; a save pass on pause-time only |
| Granting a reward / reporting an outcome | A callback on the data row (`OnClaimed`-style) plus one audit `Debug.Log` per grant | An event bus for grants; a granter interface; the producer knowing what the consumer's currency means |
| Rewarded ads | ONE flow wrapper over the host's ad hook: busy guard, next-frame release, a timeout failsafe | A feature package referencing an ads SDK; a flow that can latch a button dead when the ad never answers |
| SFX / IAP / analytics from a reusable package | Static hook fields with safe no-op defaults, assigned once at boot | The package referencing the game's `AudioManager`, IAP SDK or tracker directly |
| Button wiring | ONE bind helper: null-guard + remove-then-add | Bare `AddListener` - it duplicates on re-init and NREs on a deleted button |
| Async flows | `UniTask` with `GetCancellationTokenOnDestroy`; UI loops gate on visibility | Coroutines for flows; `while (true)` UI loops |
| Tunable numbers | `[SerializeField]` knobs on the owning prefab | Hardcoded constants; a config ScriptableObject for three numbers |
| Debug / cheat entry points | `[Button, DisableInEditorMode]` methods in the class's `Debug` region | Debug branches inside production flows |

When a needed capability has no owner yet: create **one** shared system in the project's core folder,
then migrate only the call sites you are already touching. Adding a new package dependency is an
architecture decision - raise it before taking it.

## Rule 3 - `#region` grouping

**Panels and other multi-role classes use the fixed vocabulary, in order: `API`, `Logic`, `UI`,
`Debug`.** `API` comes first and is self-sufficient: a consuming dev reads only that region and can
use the class - init, open/close, queries, reset, placement constants. Same names in every panel.

Other multi-concern classes: one region per concern, named after the concern and never after the
access level; `Debug` always last; fields and tiny computed properties above the first region.

A class with a single concern - a widget, an event payload, a row class, a thin manager - gets **no
regions at all**.

## Rule 4 - Fail loudly, on the leniency ladder

The ladder, from most to least forgiving:

- **Incomplete data warns and keeps running.** A missing icon, a bad amount or weight, a null entry
  in a list: log one warning naming the field and the index, then carry on with what is usable.
- **Serialized UI references are optional.** Guard every dereference. A designer deleting a button
  from a prefab is a supported workflow, not a wiring bug - nothing may throw or stick because of it.
- **Only structure the machine cannot run throws.** An empty required list, a wheel with fewer than
  two wedges, non-increasing unlock thresholds.
- **An unknown key `LogError`s naming the key** and is never silently mapped to something reasonable.
  A silent "sensible default" is the bug; the loud error is the feature.
- **A `switch` default over an internal id throws** `ArgumentOutOfRangeException` naming the value.
- **A missing outcome callback `LogError`s** that the thing was NOT granted - the caller must be
  able to see the drop in the console.

Everything required that is not a serialized UI reference still fails loud.

## Rule 5 - Comments

UI code (panels, widgets, FX, templates) stays at zero comments per `unity-ui-panel`. All other C#:

- No comments unless the name alone cannot convey the intent: a hidden constraint, a subtle
  invariant, a known footgun.
- When one is needed: a single `//` line, lowercase start, short. No block comments, no
  `/// <summary>`.
- A comment adds **new** information. Never restate what the name already says.
- Never delete or rewrite comments that already exist unless the user asks, and never translate them.

## Rule 6 - Initialization is `Initialize()`

`Initialize(...)` is the single init convention across the project.

- Panels: `Initialize(List<TRow> rows)` - validate the rows against the ladder, load save state, arm
  timers, build dynamic lists, `Initialize(...)` the child widgets, bind listeners idempotently,
  render the initial state. Guard the one-time build with a `bool built`.
- Activation is `OpenPanel()` / `ClosePanel()`, and nothing else. `OpenPanel` before `Initialize`
  logs one error and refuses.
- Boot order: hooks and services assigned first, then `Initialize` chains **from `Start()`**, never
  from `Awake()`. A class the chain never reaches is a bug - nothing initializes it.
- Repeated `Initialize` must be safe: listeners cannot double-subscribe, child lists cannot grow.

Two retired names, never reintroduced: `StartClass` and `SetInfo`. `SetInfo` in particular is a trap
when the project sits on `com.nabagame.ui`, whose `BaseUI` already declares a no-arg
`public virtual void SetInfo()` it tells you never to override - the old convention was an overload
piled on top of it. Existing members still named `SetInfo` or `StartClass` are renamed only when you
already have the file open for another reason (Rule 1, migrate only what you touch).

## Compiling

Assume nothing about whether an edit is live. Check whether Hot Reload is installed in **this**
project, then follow `unity-hot-reload`: write the file, refresh the AssetDatabase (or use
`script-update-or-create`, which Roslyn-validates and refreshes for you), read `console-get-logs`,
and only then act. Play mode must be stopped for a script change to take effect.
