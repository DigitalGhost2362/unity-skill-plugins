# unity-skill-plugins

Marketplace `nbg-unity`. One repo holding every shared Claude Code skill for the
NBG Unity projects, so a rule is written once and every project on every machine
picks it up from here.

## Plugins

| Plugin | Skills | Scope |
|---|---|---|
| `unity-mcp` | 83 | Feeder MCP tool skills: scene, gameobject, assets, prefab, script, screenshot, profiler, ui-inspect. Any Unity project wired to the MCP bridge. |
| `unity-standard` | 4 | Portable workflows: recompile/play-mode loop, Firebase event shape, UI FX brainstorming, plan-first prompting. |
| `reward-system` | 7 | Reward hosts only: package contract, C# and UI standards, UI-from-image, play-mode verification, Vietnamese docs, git release. |

Skills are namespaced once installed: `unity-coding` becomes
`/reward-system:unity-coding`.

## Install on a new machine

```bash
claude plugin marketplace add https://github.com/BingoBoiz/unity-skill-plugins.git
```

Then, in an interactive session, `/plugin install` and pick the plugins you want.
A Unity project that is not a reward host only needs `unity-mcp` and
`unity-standard`.

Auto-update is off by design (see below), so nothing changes under you.

## Update

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
.claude-plugin/marketplace.json      <- the catalog; only this file lives here
plugins/<plugin>/
    .claude-plugin/plugin.json       <- only plugin.json lives here
    skills/<skill>/SKILL.md          <- skills/, hooks/, agents/ sit at plugin root
```

Scripts bundled with a skill must be addressed through `${CLAUDE_PLUGIN_ROOT}`;
a plugin cannot reference files outside its own directory.

## Renaming or removing a plugin

Add an entry to `renames` in `marketplace.json` mapping the old name to the new
one, or to `null` if removed. Treat that map as append-only history: add a new
entry rather than editing an old one, or machines still on the old name break.
