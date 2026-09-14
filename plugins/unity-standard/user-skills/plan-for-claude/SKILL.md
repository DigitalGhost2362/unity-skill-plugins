---
name: plan-for-claude
description: "Plan-only workflow: DeepSeek researches and writes a bulletproof implementation plan, then emits a single self-contained copy-paste prompt that Claude Code (with Unity MCP) executes. Use when the user wants a plan/prompt for Claude to do the coding, not for DeepSeek to implement."
---

# Plan for Claude (DeepSeek plans, Claude executes)

You are the planner; you never implement. Deliver a plan plus one copy-paste prompt for Claude Code, which runs in the same repo with Unity MCP access. Trigger: any request for "a plan", "a prompt for Claude", "Claude will do the work" - any split where you plan and another agent executes.

## Workflow

### 1. Fix the objective

One sentence, split into **must** vs **should**. If you cannot name the deliverable, ask via `ask_user_question` before spending research budget.

### 2. Load the repo's own rules FIRST

Before any other research: read `AGENTS.md` (especially its `## Local skills` section) and `CLAUDE.md`; list the available skills (project ones under `.claude/skills/`, shared ones installed from the `nbg-unity` marketplace as `<plugin>:<skill>`) and read the frontmatter `description:` of every non-MCP-tool skill; read in full every skill matching the task. Keep that list - it becomes the first line of the Claude prompt.

| Task touches | Skill to read |
|---|---|
| C# scripts | `unity-coding` + `unity-hot-reload` |
| Panel / popup / widget / button | `unity-ui-panel` |
| Building UI from a mockup image | `unity-ui-from-image` |
| Choosing an animation or effect | `brainstorm` |
| Tracked README / CHANGELOG / package.json | `unity-package-docs` |
| Proving behavior in Play mode | `unity-playmode-verify` |
| Git, commit, tag, version bump | `unity-git-release` |
| Analytics events | `firebase-event-tracking` |

Those are the portable `unity-standard` skills. A project-specific plugin (e.g. `reward-system`)
ships **deltas** on top of several of them - `reward-package` for the package contract,
`reward-system:unity-coding` and `reward-system:unity-ui-panel` for that repo's boundaries. When one
exists, read the generic skill first and the delta second; the delta wins on conflict.

Repo skills **override** your defaults. Never plan a Unity task without this step.

### 3. Research - verify, never assume

Dispatch independent `subagent` runs in the SAME message so they run concurrently, one per area: repo conventions, the code under change, tests and the verification scene, and `web_search` for current API versions. Each prompt must stand alone.

**State the premise behind every bulk operation and prove it against the repo, not against intuition.** The failure mode to avoid: "git reports a rename, so GUIDs are preserved" must be proven by diffing the actual `guid:` lines, never inferred.

Every path, class/method name, API and version must trace to a file you read or a source you found. Anything unproven is marked `UNVERIFIED`, not asserted.

### 4. Unity asset safety - non-negotiable

A plan violating any of these is wrong. Rewrite it.

- `.meta` files and GUIDs are **Unity's output**. Never copy, overwrite, hand-edit or generate them. See `unity-git-release`: "never hand-write GUIDs".
- Never bulk-copy (`robocopy`, `cp -r`, `xcopy`, `Copy-Item -Recurse`) over a folder Unity imports. Move or rename assets through the Editor (`assets-move`, `assets-copy`) so the AssetDatabase keeps references intact.
- Any step that could change a GUID must first list every asset referencing it and get the user's explicit approval. Broken references fail silently.
- Serialized assets (`.prefab`, `.unity`, `.asset`, `.meta`) are edited by Unity, not by text tools, unless the user explicitly asked for a text edit.
- Deleting or overwriting tracked files needs a stated rollback and user sign-off.

### 5. Verification that can fail

**A check that cannot fail is not a check.**

- Banned: comparing a copy against the source you just copied it from; re-reading a file you just wrote; counting lines you predicted yourself.
- Required: a baseline captured BEFORE the change, compared AFTER.
- Unity baselines worth taking: `git status --porcelain`, every `guid:` line in the asset tree, console errors (`console-clear-logs` then `console-get-logs`), a missing-script scan of the demo scene.
- Every acceptance criterion names the observation that would prove the step FAILED.

### 6. Write the plan

Per step: exact file(s), exact type/method names, the Claude-side MCP tool. Cover happy path, error paths, lifecycle (init order, subscriptions, tween/async cancellation), data and compat (save keys, migrations), verification, rollback, and a separated list of assumptions and open decisions. Do not restate what a repo skill already says - cite the skill by name.

### 7. Write the Claude handoff prompt

ONE fenced block, same repo, cold session, in this order:

1. `Read these skills first: <list from step 2>. Then AGENTS.md.`
2. Objective and hard constraints (read-only paths, what must not change).
3. The steps, with acceptance criteria per step.
4. The section 4 invariants **verbatim** - the last line of defence if the skill reads get skipped.
5. Baseline and verification commands from section 5.
6. Stop-and-report rule: on any mismatch, stop; never improvise.
7. Report format: done/blocked, files changed, verification results, remaining risks.

**Do not re-paste the plan body into this block.** Anything the repo already states, reference by path. Split into sequenced phases only when the task is genuinely too large for one.

### 8. Self-check

- [ ] Repo skills discovered, read, and listed at the top of the Claude prompt
- [ ] No step copies, edits or writes a `.meta` or a GUID
- [ ] No bulk copy over a Unity-imported folder
- [ ] Every check has a baseline and can fail
- [ ] Every path/name/API traces to a file read, else marked `UNVERIFIED`
- [ ] Destructive steps have rollback and sign-off
- [ ] The handoff block does not duplicate the plan body
- [ ] "Must" vs "should" is explicit

## Output shape

1. `# Plan` - objective, constraints, steps (files and tools), edge cases, verification, rollback, assumptions and open decisions.
2. `## Copy-paste prompt for Claude` - one fenced block.

Respond in the user's language. Keep file paths, class names, tool names and error text verbatim - never paraphrase identifiers.

## Guardrails

- Never implement: no code edits, no exit into a coding state.
- Never claim you verified anything in the Unity Editor; you cannot run Unity MCP here. Frame every Editor and Play-mode check as a Claude step.
- Synthesize the plan and the handoff prompt yourself; never delegate them wholesale.
- Concise beats exhaustive: a reasoning model loses accuracy on padded prompts. Cut every line a repo skill already covers.
