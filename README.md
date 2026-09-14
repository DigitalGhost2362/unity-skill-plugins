# unity-skill-plugins

Marketplace `nbg-unity`. One repo holding every shared Claude Code and Codex skill for the
NBG Unity projects, so a rule is written once and every project on every configured machine
picks it up from here.

## Plugins

| Plugin | Skills | Scope |
|---|---|---|
| `unity-mcp` | 83 | Feeder MCP tool skills: scene, gameobject, assets, prefab, script, screenshot, profiler, ui-inspect. Any Unity project wired to the MCP bridge. |
| `unity-standard` | 10 | **The standard.** C# and UI conventions, mockup-to-prefab pipeline, recompile loop, play-mode verification, package docs, git release, Firebase event shape, UI FX brainstorming, plan-first prompting. Applied automatically in any Unity project (see below). |
| `reward-system` | 7 | Reward hosts only: the package contract plus **deltas** on the `unity-standard` skills - the package/demo-host boundary, repo UI facts, play-mode levers, Vietnamese docs, git release. |

Plugin skills are namespaced once installed: `ui-from-image` becomes
`/reward-system:ui-from-image`. **`unity-standard` is the exception** - its skills keep their bare
name (`/unity-coding`, `/brainstorm`), see *Short skill names* below. Where both plugins ship a skill
of the same name, the `reward-system` one is a delta: read the `unity-standard` version first, and
the delta wins on conflict.

## Short skill names

Poracode's slash menu matches only the *start* of a skill's full name, so a namespaced
`unity-standard:brainstorm` never shows up for `/brai`. Claude Code has no setting to drop a plugin
namespace; only user skills (`<config home>/skills/<name>`) keep a bare name.

So `unity-standard` keeps its skills in `user-skills/`, not `skills/`. Claude Code does not load that
folder as plugin skills. Instead the `SessionStart` hook copies each one into
`${CLAUDE_CONFIG_DIR:-~/.claude}/skills/<name>` and drops a `.nbg-unity` marker holding the plugin
root it came from. On later sessions it re-copies a skill only when the plugin root changed or a
source file is newer than the marker. The mtime check matters for a directory-source marketplace,
whose root stays the same across updates. It deletes marked copies whose skill no longer exists, and
never touches a folder without the marker; if a name is already taken it prints a warning to stderr.

Consequences:

- A fresh install shows the short names from the **second** session on. The first session is the
  one that copies them.
- A skill that ships scripts addresses them as `${CLAUDE_SKILL_DIR}`, never `${CLAUDE_PLUGIN_ROOT}`.
  The copy runs as a user skill, and `CLAUDE_PLUGIN_ROOT` is not set there.
- Each config home gets its own copy. A machine running several `CLAUDE_CONFIG_DIR`s installs the
  plugin once per home, as it already has to.

## Always applied

`unity-standard` ships a `SessionStart` hook. On every session start - and again after `/clear` and
after a context compaction - it checks the project directory for
`ProjectSettings/ProjectVersion.txt` and, if it is a Unity project, prints
`hooks/unity-always.md` into the agent context. That text is the compressed standard plus an index of
which skill to invoke before which kind of work.

The consequence: install the plugin once and every Unity project on that machine gets the rules,
including a project created tomorrow. Outside a Unity project the hook prints nothing and costs
nothing.

Edit `plugins/unity-standard/hooks/unity-always.md` to change what gets injected. Keep it short - it
is paid for on every session. The detection lives in `hooks/unity-context.sh` and the wiring in
`hooks/hooks.json`.

## Install on a new machine - Claude Code

```bash
claude plugin marketplace add https://github.com/BingoBoiz/unity-skill-plugins.git
```

Then, in an interactive session, `/plugin install` and pick the plugins you want.
A Unity project that is not a reward host only needs `unity-mcp` and
`unity-standard`.

Auto-update is off by design (see below), so nothing changes under you.

## Install on a new machine - Codex

Clone this repository, then run the setup script from PowerShell:

```powershell
git clone https://github.com/BingoBoiz/unity-skill-plugins.git
cd unity-skill-plugins
powershell -ExecutionPolicy Bypass -File .\scripts\setup-codex.ps1
```

Add `-WithRewardSystem` only on a machine that works on reward hosts:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\setup-codex.ps1 -WithRewardSystem
```

The equivalent manual setup is:

```text
codex plugin marketplace add <absolute-path-to-this-repo>
codex plugin add unity-standard@nbg-unity
codex plugin add unity-mcp@nbg-unity
```

Codex requires one safety review for plugin hooks. Open Codex, run `/hooks`, review and trust the
`unity-standard` `SessionStart` hook, then start a new task from the Unity project root. From that
point on, every Unity project on that machine - including projects created later - receives the
standard automatically. A machine that only cloned the repo but did not register and install the
plugins does not receive the rules.

`unity-mcp` supplies tool instructions; the Unity project still needs the Feeder MCP bridge and the
machine still needs its `ai-game-developer` MCP connection configured before those tools can run.

## Update - Claude Code

Updates are pulled on demand only. When you want the latest rules:

```
/plugin marketplace update nbg-unity
```

No plugin declares a `version`, so the marketplace commit is the version: one
`git push` here, one `marketplace update` there, and every machine is on the same
rules. Nothing to bump by hand.

The short-name copies of `unity-standard` refresh at the next session start after the update.

To keep auto-update off, `~/.claude/settings.json` carries:

```json
{ "env": { "CLAUDE_CODE_PLUGIN_DISABLE_AUTO_UPDATE": "1" } }
```

## Update - Codex

Pull the repository and rerun the setup script to reinstall the local plugin snapshots:

```powershell
git pull
powershell -ExecutionPolicy Bypass -File .\scripts\setup-codex.ps1
```

Use `-WithRewardSystem` again if that optional plugin is installed. When a hook definition changes,
Codex invalidates its previous trust hash; run `/hooks` and trust the new definition after reviewing
the diff. Start a new task after reinstalling so the refreshed skills are discovered.

## Editing a skill

```bash
git clone https://github.com/BingoBoiz/unity-skill-plugins.git
claude --plugin-dir ./unity-skill-plugins/plugins/unity-standard
```

`--plugin-dir` overrides the installed copy for that session, so you can try a
change before publishing it. `/reload-plugins` picks up further edits without a
restart. `unity-standard`'s short-name copies are the exception: they refresh at the next session
start. Validate before pushing:

```bash
claude plugin validate ./plugins/unity-standard
```

## Layout

```
.agents/plugins/marketplace.json      <- Codex marketplace catalog
.claude-plugin/marketplace.json      <- the catalog; only this file lives here
plugins/<plugin>/
    .codex-plugin/plugin.json        <- Codex manifest
    .claude-plugin/plugin.json       <- only plugin.json lives here
    skills/<skill>/SKILL.md          <- skills/, hooks/, agents/ sit at plugin root
plugins/unity-standard/
    user-skills/<skill>/SKILL.md     <- copied to user skills by the hook (short names)
```

Scripts bundled with a skill must be addressed through `${CLAUDE_SKILL_DIR}`, which works for plugin
and user skills alike. A plugin cannot reference files outside its own directory.

Each entry in `marketplace.json` must spell its `source` out as an explicit
relative path:

```json
{ "name": "unity-standard", "source": "./plugins/unity-standard" }
```

Do not use a bare plugin name plus `metadata.pluginRoot`. That shorthand parses
fine but Claude Code 2.1.x fails the install with *"This plugin uses a source
type your Claude Code version does not support"*. The explicit `./plugins/<name>`
form works on every version.

## Renaming or removing a plugin

Add an entry to `renames` in `marketplace.json` mapping the old name to the new
one, or to `null` if removed. Treat that map as append-only history: add a new
entry rather than editing an old one, or machines still on the old name break.
