# Unity project detected — the nbg-unity standard is in effect

These rules apply to every piece of work in this project, whether or not a skill was invoked.
They are the compressed form; the full text lives in the skills listed below.

## Non-negotiables

- **Least code that works.** Question whether the task needs new code at all. Engine-native features
  and existing project helpers before new code; Inspector/prefab wiring before runtime construction.
  No interface, base class or config option for a future that is not scheduled.
- **Reuse before writing.** Time, scheduling, events, persistence, async, ads, SFX, button wiring
  and config are capabilities a project owns once. Grep for the existing owner before writing a
  second one. Re-implementing one locally is a bug, not a style choice.
- **Nothing in `Update()`** for timing, polling or state watching. Use the project's scheduler,
  an event, or a gated async loop.
- **`Initialize(...)` is the single init convention.** Panels take their data through
  `Initialize(rows)`; activation is `OpenPanel()` / `ClosePanel()` and nothing else. Call
  `Initialize` from `Start()`, never `Awake()`; services and hooks are assigned before it.
  `OpenPanel` before `Initialize` logs one error and refuses.
- **Zero comments in UI code.** No `//`, no `/* */`, no `/// <summary>`, no `[Tooltip]`. Rename or
  extract instead. Never delete or translate comments that already exist. Elsewhere a comment is
  allowed only for a hidden constraint the name cannot carry: one line, lowercase, new information.
- **Buttons are wired in code.** Inspector OnClick lists stay empty. Null-guard the reference and
  remove-then-add on a stable method group, never a lambda, so repeated init cannot double-subscribe.
- **Every repeated cell/slot/row/card is its own prefab.** One inactive nested template for a dynamic
  list, N nested instances wired in order into a serialized `List<>` for a fixed board. Editor-time
  copies use `PrefabUtility.InstantiatePrefab`, never `Object.Instantiate`. Never build UI at runtime
  with `new GameObject()` + `AddComponent`.
- **Anchor to the edge the element is stuck to** — a rect left on the default center-middle anchor
  drifts on every aspect ratio the design was not drawn at. Changing an anchor must never move a
  pixel: click the preset without Alt, or preserve `offsetMin`/`offsetMax` in script.
- **Debug buttons are mandatory on every new panel**: `[Button, DisableInEditorMode]` methods that
  reach each state without playing the whole flow, plus a reset for any persistent state it writes.
- **Fail loudly, on a ladder.** Incomplete data warns and keeps running; only structure the machine
  cannot run throws; an unknown key `LogError`s naming the key and is never silently mapped to
  something reasonable. Serialized UI references are optional — guard every dereference.
- **Never hand-author `.prefab` or `.unity` YAML, and never read one whole.** Build and inspect
  through the Editor API (`script-execute` when a bridge is connected, see Editor tools below); grep
  for the few fields you need.
- **Every `.cs` edit needs a real recompile and a console read** before it is live, unless this
  project has Hot Reload installed. Play mode must be stopped for a script change to take effect.
- **Verify in play mode, or say plainly that you did not.** A code reading is never a verification
  result.

## Full skills — invoke before the matching work

| Before | Invoke |
|---|---|
| any C# | `unity-coding` |
| any UI panel, popup, widget, button | `unity-ui-panel` |
| building UI from a mockup image | `unity-ui-from-image` |
| any recompile / play-mode round trip | `unity-hot-reload` |
| proving a task is done | `unity-playmode-verify` |
| package docs, README, CHANGELOG | `unity-package-docs` |
| commit, version bump, tag | `unity-git-release` |

## Editor tools (optional)

The workflow skills name tools of an Editor MCP bridge: `script-execute`, `script-update-or-create`,
`assets-refresh`, `editor-application-set-state`, `console-get-logs`, `scene-open`,
`screenshot-game-view`. A tool missing from your tool list may only be disabled: bridges keep tools
off to save tokens, so call `unity-tool-list`, then `tool-set-enabled-state`, when the bridge offers
them. With no bridge connected, write files with the file tools, use the offline route in
`unity-hot-reload`, and say plainly that nothing was compiled or played. If a shared-Editor skill is
installed, invoke it before play mode, a compile or a scene open.

A project-local `CLAUDE.md`, `AGENTS.md`, or a plugin that declares itself a delta on these skills
overrides this text on conflict.
