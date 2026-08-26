---
name: reward-docs
description: Mandatory workflow for writing or updating any TRACKED doc of the reward package (README.md, CHANGELOG.md, package.json descriptions, Documentation~/ARCHITECTURE.md, Documentation~/INTEGRATION-GUIDE.md, Documentation~/FEATURES/*.md, Samples~/RewardDemo/README.md). Holds the doc-sync matrix (which files must move together when behavior changes), the Vietnamese consumer voice, the fixed feature-spec section order, the banned vocabulary and the ASCII rule, plus the self-scan to run BEFORE handing work back instead of discovering leaks at commit time. Trigger on any request to write, translate, restructure, review or sync package documentation, changelog entries, or a feature spec.
---

# Package docs workflow (tracked docs only)

Tracked docs are a **product surface**: they ship to the consuming team inside the package. They are not a work log. `release` (section 5 and 10) owns the commit-time rules; this skill owns the writing.

## Rule 0 -- Tracked or internal?

| Goes in the package (tracked, Vietnamese) | Goes in `.claude/docs/reward-package/` (local, English) |
|---|---|
| What the package does today, how to use it | Why it is built that way, decision record, roadmap |
| Row shapes, API, save keys, hooks, placements | Conventions, consumer-style research, changelog history |
| Per-feature behavior + the dev's verification checklist | Anything naming another project, an SDK, an agent or a tool |

If a sentence explains *history*, *alternatives considered*, or *how the code came to be*, it belongs in the internal copy. Never move internal material back into `Documentation~/`.

## Rule 0b -- The reader test (every sentence, no exceptions)

Before a sentence goes into a tracked doc, answer: **after reading this, does the dev do anything differently?** If no, cut it. A tracked doc is the list of things the reader must do or decide, not a record of what the package does inside.

Cut on sight:

- Mechanics the reader cannot act on ("`com.unity.textmeshpro` is a shim pointing at ugui 2.0", "Unity does not support git URLs in a package's `dependencies`"). The actionable half is "install these first" -- ship only that half.
- Reassurance about work they never had to do ("Package Manager installs it, you do not have to do anything").
- Answers to a question the reader is not asking yet: update/upgrade mechanics inside an install section, cache-clearing tips for a dev who has not installed once.
- Justification of a design, or an alternative that was rejected -- that lives in `.claude/docs/reward-package/`.
- Facts that only matter to whoever maintains the package: assembly names, script GUIDs, internal loop shapes, "the profile must stay flat".

Length is a budget, not a free resource: every paragraph the reader skips makes the paragraph that mattered less likely to be read. When in doubt, cut. A dev who needs the detail will ask; a dev drowned in it reads nothing.

## Rule 1 -- The doc-sync matrix

A behavior change is not done until every row that applies has been updated **in the same change**:

| What changed | Files that must change with it |
|---|---|
| Row field added/removed/renamed | `FEATURES/<feature>.md` (Du lieu), `INTEGRATION-GUIDE.md` section 4, `CHANGELOG.md`, `package.json` |
| Public panel API (`SetInfo`, queries, config properties) | `FEATURES/<feature>.md` (API), `INTEGRATION-GUIDE.md` section 7/7.1, `CHANGELOG.md`, `package.json` |
| Hook signature or a new hook | `ARCHITECTURE.md` section 3, `INTEGRATION-GUIDE.md` section 6, every feature spec that uses it, `CHANGELOG.md`, `package.json` |
| Save payload / `ProfileVersion` | `FEATURES/<feature>.md` (Luu), `ARCHITECTURE.md` section 6, `CHANGELOG.md`, `package.json` (bump + migration or logged reset) |
| Event added/renamed | `ARCHITECTURE.md` section 4, feature spec (Event / hook / placement), `CHANGELOG.md` |
| Analytics param or placement name | feature spec, `INTEGRATION-GUIDE.md` sections 8-9, `CHANGELOG.md` |
| New dependency | `ARCHITECTURE.md` section 1, root `README.md`, `CHANGELOG.md`, `package.json` |
| Sample scene/prefab/script shape | `Samples~/RewardDemo/README.md`, feature spec if the dev copies from it |
| Folder layout of `Runtime/` | root `README.md` (Cau truc package) |

`package.json` version and the `CHANGELOG.md` entry always move together; SemVer rules live in `release` section 4.

## Rule 2 -- Voice

- Vietnamese, for a dev who has to ship a feature today. Short sentences, imperative, no marketing.
- Second person = the consuming dev (`ban`), the package = `package`. The dev never reads about "we" or about the repo owner.
- State behavior in the present tense as fact. No "se lam", no "sap toi", no TODO, no open questions -- if it is not shipped, it is not documented.
- Identifiers, file names, type names, code snippets and log strings stay exactly as in code (English).
- Do not translate the Vietnamese `//` field comments in the three panels, and do not copy them into docs as if they were API.

## Rule 3 -- Feature spec = fixed section order

Every file in `Documentation~/FEATURES/` keeps this skeleton (Online Reward uses `Trang thai va luu` for the save section):

```text
# <Feature>
one-line: which panel owns it, which manager the dev writes
## La gi                     what the player sees happen
## Giao dien                 the visual contract, per state
## Du lieu (ban dung)        row shape, leniency, what the prefab holds
## Luu (key PlayerPrefs ...) table: field -> meaning, plus when it is written
## API: <Panel>, `#region API`   only the public members, grouped as in code
## Event / hook / placement  grants via Row.OnClaimed, notifications, hooks used, placement, analytics
## Checklist kiem tra        numbered, runnable by the dev, one behavior per line
## Quy tac da chot           the rules that look wrong until explained (rollover, streak, progress carry)
## Ghi chu layout            optional, art substitutions and sizing notes
```

The checklist is the contract with QA -- it must include a fresh-install case, a restart case, a repeated open/close case, a null-button case and a null-`OnClaimed` case. When behavior changes, fix the checklist line too, not just the prose.

## Rule 4 -- Banned in tracked docs

Never write, in any tracked file: another project or studio name (`paint-and-seek`, `speed-clicker`, `asmr`, `_GameBase`, `dress-to-impress`), a save-plugin SDK name, a `D:\Fork\...` path, "decision #", "decision record", "ported from", "legacy", "reference game", "consumer team", any mention of an agent, an AI tool, a skill, or a co-authored trailer, and no unfilled placeholder (`YYYY-MM-DD`, `<owner>`) at commit time.

Punctuation is ASCII only: `-`, `:`, `;`, `->`, `x`, `<=`. No em-dash, no arrows glyph, no multiplication sign, no warning sign, no curly quotes, no ellipsis character. Vietnamese diacritics are fine; they are the only non-ASCII allowed.

## Rule 5 -- Self-scan before handing back

Run over the worktree (not `--cached`, so it works mid-task):

```sh
export LC_ALL=C.UTF-8; grep -rn -I -i -P -e '\x{2014}|\x{2192}|\x{21D2}|\x{00D7}|\x{26A0}|\x{2026}|\x{2018}|\x{2019}|\x{201C}|\x{201D}|paint-and-seek|paint and seek|painandseek|dress-to-impress|speed-clicker|speed_clicker|asmr|_gamebase|d:[\/]fork|consumer team|ported from|legacy rule|reference game|decisions? #|decision record|\bagents?\b|claude|\bskills?\b|co-authored-by|generated with|] - yyyy-mm-dd|<owner>' Packages/com.nabagame.reward README.md --include=*.md --include=*.json && echo "DOC SCAN FAILED" || echo "doc scan clean"
```

Any hit is a fix, not a judgement call. Then re-read the changed section against the code you just wrote: every API name, save key, placement string and default value in the doc must exist in `Runtime/` with that exact spelling.

## Checklist

- [ ] Reader test (Rule 0b) run over every new sentence: nothing left that the dev cannot act on.
- [ ] Every row of the doc-sync matrix that applies was updated in this change.
- [ ] `package.json` + `CHANGELOG.md` moved together, with a real date and SemVer-correct number.
- [ ] Feature spec keeps the fixed section order; its checklist covers the new behavior.
- [ ] No history, no decisions, no roadmap, no other project, no SDK name in a tracked file.
- [ ] ASCII punctuation only; identifiers match the code character for character.
- [ ] Doc scan clean.
