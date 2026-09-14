---
name: unity-hot-reload
description: Decide how a C# change reaches the running Unity Editor in a Unity project driven through the MCP bridge. When Hot Reload is not installed, every C# edit needs a real recompile — this skill holds the recompile/play-mode workflow, the tool order, and the console verification step. Read BEFORE writing any .cs file and BEFORE calling script-update-or-create, assets-refresh, or editor-application-set-state. Trigger on every C# edit and every play-mode verification.
---

# Recompile workflow

## The premise: check for Hot Reload before assuming

Grep `Packages/manifest.json`, `Packages/packages-lock.json`, `Packages/`, `Assets/Plugins/` and any vendored-package folder for `com.singularitygroup.hotreload`, and branch on the answer. **Check per project, every time** — sibling projects differ, and carrying the habit across is how a session ends up running stale code for an hour.

Everything below assumes it is **not** installed, which is the common case. Consequences, all of them load-bearing:

- **Every** C# change requires a domain reload. There is no "patch it into the running game" path.
- A script edit made while play mode is running does **not** take effect in that session.
- Writing a `.cs` file with Write/Edit alone changes nothing in the Editor until the AssetDatabase refreshes.

## The decision table

| Change | What to do |
|---|---|
| Any `.cs` edit — method body, new field, new type, signature, enum, asmdef | Stop play mode → write the file → refresh → check console → start play mode |
| Serialized field added/removed/renamed | Same, plus re-check the prefab/scene inspector: Unity may drop the old value |
| Prefab / scene / ScriptableObject edit only (no `.cs`) | No recompile. Use the `gameobject-*` / `assets-modify` tools directly |
| Inspector value tweak during play mode | No recompile, but the value is lost when play mode stops unless you copy the component |
| `[Button]` debug method already compiled | No recompile — just click it via the Odin inspector in play mode |

## Tool order

Every step below that compiles or enters play mode needs the Editor lock (`unity-shared-editor`). If
Hot Reload *is* installed, a plain file save already reaches whoever is playing - another session's
test included.

Preferred, because it validates before it writes:

1. `script-update-or-create` — Roslyn-validates the C#, rejects invalid code without touching the file, writes it, refreshes the AssetDatabase, and delivers the post-compilation result via `requestId`.
2. `console-get-logs` — confirm zero compile errors before doing anything else.
3. `editor-application-set-state` — enter play mode. It **throws if the project has compilation errors**, so step 2 is not optional.

When you edited files on the filesystem instead (Write/Edit, or a bulk `sed`):

1. `assets-refresh` — forces the import + script compilation.
2. `console-get-logs`.
3. `editor-application-set-state`.

Never call `editor-application-set-state` to start play mode as the first action after an edit — you will either run stale code or hit the compile-error throw.

## Verifying a change actually landed

Compilation succeeding is not the same as the change being live. Confirm with one of:

- `console-get-logs` after entering play mode, looking for a log the new code emits.
- `gameobject-component-get` on the component, checking a new serialized field exists.
- `screenshot-game-view` for a visual change.
- A `[Button, DisableInEditorMode]` debug method driven through the Odin inspector — the fastest way to reach one panel state without replaying a whole flow. This is why `unity-ui-panel` makes those buttons mandatory.

## The focus trap

Unity does not compile while its window lacks OS focus. If the Editor is in the background, `assets-refresh` / `script-update-or-create` return, `isCompiling` stays `true` forever, and every later call queues behind it — no MCP call unsticks it. Symptom: a `requestId` that never resolves and `editor-application-get-state` reporting `isCompiling: true` long after the write.

Fix: ask the user to click the Unity window once. Then re-check `editor-application-get-state`. Plan for it — if the user is away, say the change is written but not compiled rather than waiting on a request that cannot finish.

## Compiling without opening Unity

When the Editor is not running and you only need to know whether the code compiles, use the offline route: `csc` with the Bee-generated `.rsp` from `Library/Bee/`, plus the referenced DLLs resolved from a `Library/ScriptAssemblies` folder that already has them — this project's, or a sibling project on the same package set. This is the only compile signal available with Unity closed; it catches syntax and reference errors, not serialization or runtime wiring.

## If Hot Reload is installed later

Delete this skill's premise section and regenerate the decision table from scratch against the installed version — do not resurrect a table measured on a different project. Measurements taken under another Unity or Hot Reload version do not transfer.
