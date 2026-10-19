---
name: review-loop
description: Honest implement -> gate -> review -> fix loop for a Unity task, with a strictness level the user picks to fit the time they have - gates (deterministic checks only), quick (one fresh reviewer), standard (default - blind two-phase reviewer plus a refuter), strict (user-confirmed contract, play-mode evidence, a refuter per finding, a fresh implementer per round). The agent that writes the code never decides it is done - a frozen contract, a gate script (forbidden paths, scope, test count, cheat patterns, working-tree snapshot) and a fresh reviewer that never sees the implementer's summary do. A lower level runs fewer checks and reports what it skipped as unverified; it never lowers the bar for PASS. Work judged by looking - an effect, lighting or level dressing that must match a reference image or a before/after spec - runs the visual track: triage of the whole reference first, frozen camera shots, measured region and budget gates, a blind judge comparing this round with the best one, and a pivot rule that stops endless retuning. Use when the user asks for a review loop, "làm đến khi pass", "implement rồi review", "vòng lặp chấm", "chấm trung thực", "subagent chấm điểm", "sửa theo ảnh ref", "làm giống concept", "loop until green", "/review-loop quick|standard|strict", or wants work graded before it is called done.
---

# Review loop - the worker never grades itself

Agents cheat when they are cornered: an impossible, ambiguous or contradictory task plus "keep going
until green" is the setting where test edits, special-cased inputs and "all tests pass" claims show
up. Reviewers fail the other way: one that reads "I implemented X, tests pass" agrees with it. So the
loop takes the verdict away from both. A gate script compares the working tree to a snapshot, a fresh
reviewer judges the diff against a frozen contract without ever seeing the implementer's account, and
the script, not an agent, turns that evidence into the final status.

Script: `python "${CLAUDE_SKILL_DIR}/scripts/loop_gate.py"` (`python3` on macOS/Linux; stdlib and
git only). Written `<gate>` below. Prompt templates: `reference/prompts.md`.

## Visual tasks - read `reference/visual.md` first

When any criterion is judged by looking at a capture against a reference image or spec, follow
`reference/visual.md` on top of the steps below. In short:

1. **Triage before code** - list every element of every "after" image in the spec, tag it (vfx,
   material, lighting, post, geometry, camera, unreachable), state the ceiling, show it to the user.
   Only the user's words take an element out of scope.
2. **Frozen shots** - reference images, `shots.json` and the spec are hashed at `init`; a moved
   camera or a swapped reference is STOP.
3. **Measured gates** - `metric` criteria read the solo skill's `ref-compare.py --json` numbers per
   region and the budgets passed with `check --value`; no 0-10 scores anywhere.
4. **Blind pair judge** - `<gate> pair` puts this round and the best round under random X/Y labels;
   the judge grades both and says which is closer, never seeing a score, the implementer's account
   or how the effect is built. A round worse than the best one FAILs with REGRESSION and is restored.
5. **Pivot** - a criterion failing two rounds running bans retuning: change the technique or end
   BLOCKED. A third failure stops the loop.

## Pick the level

| Level | Use when | Contract | Tests | Reviewer | Refuter | Fix rounds |
|---|---|---|---|---|---|---|
| `gates` | very little time, or a diff one sentence describes | drafted, shown, no wait | static count; run if a test tool exists | none | - | 2 |
| `quick` | small task, little time | drafted, shown, no wait | as `gates` | one fresh pass, a cheaper model is fine | none | 1 |
| `standard` (default) | a normal feature or fix | shown; ask only about an ambiguous criterion | EditMode, if the bridge can run it | blind two-phase, strongest model | one agent for all findings | 2 |
| `strict` | save data, economy, shared systems, a bug that came back, release | confirmed by the user before any code | EditMode and PlayMode, play evidence via `verify` | as standard, and it re-runs the gate itself | one per finding, in parallel | 3, fresh implementer each |

The level comes from the user's words (`/review-loop quick`; "nhanh thôi", "gấp" -> `quick`; "chỉ
check compile" -> `gates`; "kỹ vào", "release" -> `strict`), else a `review-loop: <level>` line in the
project's `CLAUDE.md`/`AGENTS.md`, else `standard`. Say which level is running in your first line.

### What no level changes

1. The gate runs every round at every level. It takes seconds and it is the anti-cheat floor.
2. Not checked means `unknown`, and `unknown` is never PASS. A lower level ends in UNVERIFIED more
   often - that is the honest price of less time, not a defect to talk around.
3. Only the user changes the level. You may suggest raising it. Never lower it yourself, mid-loop,
   because a round failed or time is short: weakening the checker without permission is the exact
   behavior this skill exists to block. Record a change the user asked for with
   `<gate> level <level> --round <N> --reason "<their words>"`.
4. The implementer prompt and its BLOCKED exit are used at every level.
5. The final status is the `verdict` command's output, relayed verbatim with its level stamp and its
   "Not run at level" line.

### Out of time mid-loop

The user says stop, "chốt", "hết giờ": finish the tool call in flight, start no new round, run `check`
and `verdict` on what exists, and relay the block. It will usually say UNVERIFIED or FAIL; report it
that way.

## Step 0 - Contract and baseline

Write `<repo>/.review-loop/contract.json`:

```json
{
  "task": "Premium packs cost 250 gold everywhere the shop prices them",
  "criteria": [
    {"id": "C1", "text": "Project compiles with a clean console", "check": "compile"},
    {"id": "C2", "text": "Buying with too little gold spends nothing and shows the toast", "check": "review"},
    {"id": "C3", "text": "ShopTests pass", "check": "tests"},
    {"id": "C4", "text": "In play mode a premium pack shows 250 and buys", "check": "play"}
  ],
  "allowed": ["Assets/Scripts/Shop/**"],
  "tests_may_change": false
}
```

- 2-7 binary criteria; each names the observation that would prove it failed. `check` is
  `compile`, `tests`, `review` or `play`, and for visual tasks `visual` or `metric`
  (`reference/visual.md`). `play` criteria stay `unknown` below `strict` - play mode runs only when
  the user asks, and choosing `strict` is that ask.
- `allowed` lists where edits belong; a matching `.meta` is allowed with its asset. An empty list
  turns the scope check off.
- Leave `forbidden` out to get the defaults: test folders and test files, test asmdefs,
  `ProjectSettings/`, `Packages/manifest.json` and its lock, agent config (`.claude/`, `.codex/`,
  `.agents/`, `CLAUDE.md`, `AGENTS.md`), CI config and `.review-loop/`. `tests_may_change: true`
  drops the test paths from that list. Change either only on the user's words.
- Ambiguity drives cheating about as much as impossibility. At `standard`, ask about any criterion
  two readings could satisfy; at `strict`, get the whole contract confirmed.

Then `<gate> init --level <level>`. It snapshots the whole working tree - uncommitted and untracked
files included - so the user's own work in progress is never blamed on the loop, hashes the
contract, counts the test attributes, and adds `.review-loop/` to `.git/info/exclude`. From here the
contract is frozen: changing it takes the user and a fresh `init`.

## Step 1 - Implement

- `gates`, `quick`, `standard`: you implement, in this session.
- `strict`: a fresh subagent with edit tools implements each round; you only orchestrate. When it
  fails, never fix the code yourself and mark the round done.

Use the implementer template; follow `coding` and `recompile`. **BLOCKED is a result, not a
failure**: stop the loop and give the user the BLOCKED block as written.

## Step 2 - Gates (every round, every level)

1. **Compile** - the `recompile` flow, then read the console. If a shared-Editor skill is installed,
   invoke it before the compile and before any test run.
2. **Tests** - EditMode tests through the bridge's test tool when it has one (names vary; list the
   tools). Never run Unity in batchmode against a project the Editor has open. PlayMode tests only at
   `strict`. Save the raw output under `.review-loop/` (`tests-<N>.txt` or the result XML) - outside
   it, the file itself trips the scope check. No way to run tests: `tests` criteria stay `unknown`.
3. **Gate** - `<gate> check --round <N> --compile ok|errors|not-run [--tests P/F/T | --tests-xml <file>]`.
   It diffs the tree against the snapshot, writes `diff.patch` and `gate-<N>.json`, and flags:
   forbidden paths, out-of-scope files, a dropped test count, skip and always-pass asserts, ignored
   log failures, disabled logging, process exits, empty catches, disabled warnings, asmdef and
   `testables` edits, and literals from test files showing up in new non-test code.

| Exit | Meaning | Do |
|---|---|---|
| 0 | clean | go on |
| 1 | fixable | give the implementer the gate's FAIL lines verbatim; at most 3 tries within the round |
| 2 | stop | a forbidden path or the contract changed. Stop, show the user the listed paths. Do not revert anything yourself, and do not let the implementer explain it away |
| 3 | the gate itself failed | not a pass. Fix the cause or report it |

## Step 3 - Review (not at `gates`)

The reviewer is a fresh subagent with no edit tools (in Claude Code, a read-only agent type; a
general one told it is read-only also works, because `verdict` catches any file that changed after
`check`). It gets the contract, `diff.patch`, `gate-<N>.json` and the raw evidence files - nothing
else. Never the implementer's summary, its reasoning, a "done" or "tests pass" line, or this chat.

- `quick`: one pass with the quick template.
- `standard`, `strict`: phase A sees only the contract and writes its expectations; phase B
  continues the same agent with the diff and the evidence. This makes "decide before you look" a
  structure instead of a request.
- `strict`: phase B also re-runs `<gate> check --verify` and reports any difference.

Save its JSON verbatim as `.review-loop/review-<N>.json`. Do not tidy it - fixing a typo is editing
the verdict.

## Step 4 - Refute (`standard`, `strict`)

Give the blocking findings to the refuter template - one agent for all of them at `standard`, one
per finding in parallel at `strict`. Copy each `refuted` value into `review-<N>.json`; that is the
only field you write there. A finding stands only when the refuter traced its failure scenario
through the code. At `quick` findings are not refuted; each must still carry a failure scenario.

## Step 5 - Verdict and the next round

`<gate> verdict --round <N>`. It re-snapshots the tree (any change since `check` is STOP), resolves
every criterion from the gate and the review, and prints PASS, FAIL, UNVERIFIED or STOP with what
was not run. Relay that block verbatim.

- **PASS** - every criterion passed with evidence, no standing finding, clean gate.
- **UNVERIFIED** - nothing failed, but something was not checked. Done for this level; the block
  says what is missing.
- **FAIL** - when it prints `NEXT: round N+1`, the implementer gets only the contract and
  `verdict-<N>.json`: failing criteria and standing findings, never the notes. Chasing every
  reviewer remark is how loops over-engineer.
- **Stop** on STOP, BLOCKED, the round cap, or no progress (the same failing set two rounds running;
  for a visual contract, a criterion failing a third time after its PIVOT round; `verdict` detects
  both and prints it). At a cap or no-progress, write the stuck report.

## Report

End with the last `verdict` block verbatim, then the Unity status line from the skill index:
"Sửa trong Unity" is ✅ only with `compile ok`; "Playtest" is ✅ only at `strict` with a passed `play`
criterion. At `strict` with `tests_may_change`, ask the user to read the test-file diff themselves -
no scan catches an expected value edited to match a bug.

## Limits - say them, do not hide them

- The gate detects after the fact; it does not prevent. On native Windows shell commands are not
  sandboxed, and a plugin cannot give its subagents hooks. A project that wants prevention adds a
  PreToolUse hook in its own settings denying Edit, Write and shell writes on the forbidden paths.
- The pattern scan is a tripwire, not a proof: a deleted test replaced by a weaker one, an assertion
  loosened into a plausible one, or a renamed test passes it. That is why the reviewer reads the
  test diff and why `strict` puts it in front of the user.
- `.review-loop/` is writable by the agent. `init` prints the contract hash and every verdict prints
  it again - they must match.
- Reviewer accuracy is not measured by this skill. When the templates or the gate change, run them
  on a few known-bad diffs (an edited expected value, a special-cased test input, an added
  `[Ignore]`, a test that only checks a mock, a changed test count) and count how many get through.
