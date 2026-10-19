# Unity project - skill index

This session runs inside a Unity project. This is an index, not the rules: before a kind of work
listed below, invoke its skill and follow it; open the listed docs only when the skill points you
there. A project's own `CLAUDE.md`, `AGENTS.md` or project skills win on conflict.

## Always

- Least code that works. Before writing a capability (time, events, saving, async, config), grep for
  the project's existing owner of it.
- Never hand-write a `.prefab` or `.unity` file, and never read one whole. Go through the Editor.
- A `.cs` edit is not live until it is recompiled and the console has been read.
- Play-test only when the user asks for it (play test, verify, reproduce a bug). Otherwise never
  enter play mode to check your work; just report that it was not play-tested.

## End of every report

The last line of a turn that did Unity work is one status line, nothing after it:

`Kế hoạch ✅ · Sửa trong Unity ✅ · Playtest ❌ (chưa yêu cầu)`

- **Kế hoạch**: the plan, task list or docs for the task.
- **Sửa trong Unity**: code and assets (prefabs, scenes, data). ✅ only when the Editor recompiled
  with a clean console and every scene/prefab change is saved. An offline compile check, a pending
  test case or an unsaved scene is ❌.
- **Playtest**: ✅ only with play-mode evidence from this turn.
- ❌ always carries a reason of a few words in parentheses. Use ➖ for a part the task did not need.

## unity-core

| Before | Skill | Docs inside the skill |
|---|---|---|
| writing, fixing or reviewing any C# | `/coding` | |
| any recompile or play-mode round trip | `/recompile` | |
| a play test the user asked for, reproducing a bug | `/verify` | |
| tracked README, CHANGELOG, package.json, specs | `/docs` | |
| commit, version bump, tag, release | `/release` | |
| adding or changing an analytics event | `/tracking` | |
| writing a plan for another agent to execute | `/handoff` | |
| an implement -> review loop graded before "done" (level gates/quick/standard/strict), or a loop matching a reference image | `/review-loop` | `reference/prompts.md`, `reference/visual.md`, `scripts/loop_gate.py` |
| working alone in the Editor while the user watches the Scene view | `/solo` | `scripts/solo-focus.cs`, `scripts/ref-shot-capture.cs`, `scripts/ref-compare.py` |
| editing a Google data sheet with `n_`/`s_`/`f_` headers | `/sheet-data` | `scripts/sheet_data.py` |

## unity-ui (if installed)

| Before | Skill | Docs inside the skill |
|---|---|---|
| any uGUI panel, popup, widget, button, HUD | `/ui-panel` | `reference/baseui-framework.md`, `reference/ui-reference-hierarchy.md` |
| building UI from a mockup image | `/ui-from-image` | `reference/project-facts-template.md` |
| choosing how a UI effect or animation looks | `/brainstorm` | `reference/fx-vocabulary.md` |

## unity-vfx (if installed)

| Before | Skill | Docs inside the skill |
|---|---|---|
| any particle, shader or effect work | `/vfx-core` | `reference/brief.md`, `reference/uber-material.md`, `reference/recipes.md`, `scripts/*.cs` |
| combat effects: skills, hits, projectiles, AoE | `/vfx-core` + `/vfx-combat` | `scripts/vfx-fit.cs` |
| effects inside a Canvas | `/vfx-core` + `/vfx-ui` | |
| ambient, weather, water, fire props, fog, sky and sun look | `/vfx-core` + `/vfx-environment` | `reference/urp-environment-traps.md` |
| garment and outfit effects | `/vfx-core` + `/vfx-clothing` | `reference/garment-effect.cs` |

## Editor tools (optional)

The workflow skills name tools of an Editor MCP bridge (`script-execute`, `assets-refresh`,
`console-get-logs`, ...). A tool missing from your list may only be disabled; ask the bridge to list
and enable it. With no bridge connected, write files with the file tools, follow the offline route in
`/recompile`, and say plainly that nothing was compiled or played. If a shared-Editor skill is
installed, invoke it before play mode, a compile or a scene open - unless `/solo` is active.
