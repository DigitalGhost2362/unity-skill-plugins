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

Skills are namespaced once installed: `unity-coding` becomes
`/unity-standard:unity-coding`. Where both plugins ship a skill of the same name, the
`reward-system` one is a delta: read the `unity-standard` version first, and the delta wins on
conflict.

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
restart. Validate before pushing:

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
```

Scripts bundled with a skill must be addressed through `${CLAUDE_PLUGIN_ROOT}`;
a plugin cannot reference files outside its own directory.

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
