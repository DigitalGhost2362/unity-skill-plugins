# unity-skill-plugins

Marketplace `dh-skills`. Shared Claude Code and Codex skills for Unity projects, written once and
picked up by every project on every machine that installs them. The skills hold the policy - how to
write, verify, document and release Unity work. They drive the Editor through an Editor MCP bridge
when one is connected; without one they still apply the conventions and say plainly what could not
be compiled or played.

## Plugins and skills

Every skill is invoked by its bare name (`/coding`). If another plugin uses the same name, the full
form `/unity-core:coding` always works.

### `unity-core` - every role on a Unity project

| Skill | Path | Use before |
|---|---|---|
| `/coding` | `plugins/unity-core/skills/coding/SKILL.md` | writing, fixing or reviewing any C# |
| `/recompile` | `plugins/unity-core/skills/recompile/SKILL.md` | any recompile or play-mode round trip |
| `/verify` | `plugins/unity-core/skills/verify/SKILL.md` | reporting a task done, reproducing a bug in play mode |
| `/docs` | `plugins/unity-core/skills/docs/SKILL.md` | tracked README, CHANGELOG, package.json, feature specs |
| `/release` | `plugins/unity-core/skills/release/SKILL.md` | commit, version bump, tag, release |
| `/tracking` | `plugins/unity-core/skills/tracking/SKILL.md` | adding or changing an analytics event |
| `/handoff` | `plugins/unity-core/skills/handoff/SKILL.md` | writing a plan and prompt for another agent to execute |
| `/sheet-data` | `plugins/unity-core/skills/sheet-data/SKILL.md` | editing or formatting a Google data sheet with `n_`/`s_`/`f_` headers |

### `unity-ui` - uGUI work

| Skill | Path | Use before |
|---|---|---|
| `/ui-panel` | `plugins/unity-ui/skills/ui-panel/SKILL.md` | any panel, popup, widget, button or HUD element |
| `/ui-from-image` | `plugins/unity-ui/skills/ui-from-image/SKILL.md` | building a panel prefab from a mockup image |
| `/brainstorm` | `plugins/unity-ui/skills/brainstorm/SKILL.md` | choosing how a UI effect or animation looks |

A project that does no UI work only needs `unity-core`. A project can ship its own skills on top of
these; the project skill wins on conflict.

## Skill index hook

`unity-core` ships one `SessionStart` hook. On startup, after `/clear` and after a context
compaction - not on resume - it checks the project for `ProjectSettings/ProjectVersion.txt`. In a
Unity project it adds `plugins/unity-core/hooks/unity-always.md` to the agent context. Elsewhere it
prints nothing.

That file is an index, not a rulebook: four universal rules, then which skill to invoke before which
kind of work and which docs each skill carries. The rules themselves live in the skills, which load
only when used. Keep the index short - it is paid for on every session, and anything over 10,000
characters is cut to a preview.

Wiring:

- `hooks/hooks.json` calls `hooks/run-hook.cmd session-start`.
- `run-hook.cmd` is a polyglot: cmd.exe runs its batch half, which finds Git Bash; a Unix shell runs
  its bash half. One script therefore serves Claude Code and Codex on Windows, macOS and Linux.
- `hooks/session-start` does the detection and prints the index as
  `hookSpecificOutput.additionalContext`.

On Windows the hook needs Git for Windows. Without bash it exits quietly and only the index is lost.

## Install - Claude Code

```bash
claude plugin marketplace add https://github.com/BingoBoiz/unity-skill-plugins.git
claude plugin install unity-core@dh-skills
claude plugin install unity-ui@dh-skills
```

Auto-update is off by design (see below), so nothing changes under you.

## Install - Codex

Clone this repository, then run the setup script from PowerShell:

```powershell
git clone https://github.com/BingoBoiz/unity-skill-plugins.git
cd unity-skill-plugins
powershell -ExecutionPolicy Bypass -File .\scripts\setup-codex.ps1
```

It installs both plugins; pass `-Plugins unity-core` for the core plugin only. The manual
equivalent:

```text
codex plugin marketplace add <absolute-path-to-this-repo>
codex plugin add unity-core@dh-skills
codex plugin add unity-ui@dh-skills
```

Codex requires one safety review for plugin hooks: open Codex, run `/hooks`, review and trust the
`unity-core` `SessionStart` hook, then start a new task from the Unity project root.

## Update - Claude Code

Updates are pulled on demand only:

```
/plugin marketplace update dh-skills
```

No plugin declares a `version`, so the marketplace commit is the version: one `git push` here, one
`marketplace update` there, and every machine is on the same rules.

To keep auto-update off, `~/.claude/settings.json` carries:

```json
{ "env": { "CLAUDE_CODE_PLUGIN_DISABLE_AUTO_UPDATE": "1" } }
```

## Update - Codex

```powershell
git pull
powershell -ExecutionPolicy Bypass -File .\scripts\setup-codex.ps1
```

When a hook definition changes, Codex invalidates its trust hash; run `/hooks` and trust the new
definition after reviewing the diff. Start a new task so the refreshed skills are discovered.

## Editing a skill

```bash
git clone https://github.com/BingoBoiz/unity-skill-plugins.git
claude --plugin-dir ./unity-skill-plugins/plugins/unity-core
```

`--plugin-dir` overrides the installed copy for that session, so you can try a change before
publishing it. `/reload-plugins` picks up further edits without a restart. Validate before pushing:

```bash
claude plugin validate .
claude plugin validate ./plugins/unity-core
claude plugin validate ./plugins/unity-ui
```

Scripts bundled with a skill are addressed through `${CLAUDE_SKILL_DIR}`. A plugin cannot reference
files outside its own directory.

## Layout

```
.claude-plugin/marketplace.json      <- Claude Code catalog; only this file lives here
.agents/plugins/marketplace.json     <- Codex catalog
plugins/<plugin>/
    .claude-plugin/plugin.json       <- Claude Code manifest
    .codex-plugin/plugin.json        <- Codex manifest
    skills/<skill>/SKILL.md          <- skills/ and hooks/ sit at the plugin root
plugins/unity-core/hooks/
    hooks.json  run-hook.cmd  session-start  unity-always.md
scripts/setup-codex.ps1
```

Each entry in `marketplace.json` spells its `source` out as an explicit relative path:

```json
{ "name": "unity-core", "source": "./plugins/unity-core" }
```

Do not use a bare plugin name plus `metadata.pluginRoot`. That shorthand parses fine but Claude Code
2.1.x fails the install with *"This plugin uses a source type your Claude Code version does not
support"*.

## Renaming or removing a plugin

Add an entry to `renames` in `.claude-plugin/marketplace.json` mapping the old name to the new one,
or to `null` if removed. Treat that map as append-only history, or machines still on the old name
break.
