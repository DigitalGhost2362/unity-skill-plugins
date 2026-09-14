---
name: reward-docs
description: Reward-repo delta on the studio package-docs standard. Read unity-package-docs first - it owns the tracked-vs-internal split, the reader test, the doc-sync matrix pattern, the voice rules and the ASCII rule. This file holds the reward specifics - the real file paths in the sync matrix, the Vietnamese consumer voice and fixed section headings, the banned project-name list, and the repo-scoped self-scan. Trigger on any request to write, translate, restructure, review or sync package documentation, changelog entries, or a feature spec.
---

# Package docs - reward delta

**Read `unity-package-docs` first.** It is the standard: tracked vs internal, the
reader test (Rule 0b - apply it to every sentence), the doc-sync matrix as a pattern, voice, the
fixed feature-spec section order, the banned vocabulary and ASCII punctuation, and the self-scan.
`reward-release` owns the commit-time rules; this skill owns the writing.

Tracked files here: `README.md`, `CHANGELOG.md`, `package.json` descriptions,
`Documentation~/ARCHITECTURE.md`, `Documentation~/INTEGRATION-GUIDE.md`,
`Documentation~/FEATURES/*.md`, `Samples~/RewardDemo/README.md`. Internal, never shipped:
`.claude/docs/reward-package/` (English).

## The doc-sync matrix, with real paths

A behavior change is not done until every row that applies has been updated **in the same change**:

| What changed | Files that must change with it |
|---|---|
| Row field added/removed/renamed | `FEATURES/<feature>.md` (Du lieu), `INTEGRATION-GUIDE.md` section 4, `CHANGELOG.md`, `package.json` |
| Public panel API (`Initialize`, queries, config properties) | `FEATURES/<feature>.md` (API), `INTEGRATION-GUIDE.md` section 7/7.1, `CHANGELOG.md`, `package.json` |
| Hook signature or a new hook | `ARCHITECTURE.md` section 3, `INTEGRATION-GUIDE.md` section 6, every feature spec that uses it, `CHANGELOG.md`, `package.json` |
| Save payload / `ProfileVersion` | `FEATURES/<feature>.md` (Luu), `ARCHITECTURE.md` section 6, `CHANGELOG.md`, `package.json` (bump + migration or logged reset) |
| Event added/renamed | `ARCHITECTURE.md` section 4, feature spec (Event / hook / placement), `CHANGELOG.md` |
| Analytics param or placement name | feature spec, `INTEGRATION-GUIDE.md` sections 8-9, `CHANGELOG.md` |
| New dependency | `ARCHITECTURE.md` section 1, root `README.md`, `CHANGELOG.md`, `package.json` |
| Sample scene/prefab/script shape | `Samples~/RewardDemo/README.md`, feature spec if the dev copies from it |
| Folder layout of `Runtime/` | root `README.md` (Cau truc package) |

`package.json` version and the `CHANGELOG.md` entry always move together; SemVer rules live in
`reward-release` section 4.

## Voice: Vietnamese, for the consuming dev

- Vietnamese, for a dev who has to ship a feature today. Short sentences, imperative, no marketing.
- Second person is the consuming dev (`ban`), the package is `package`. The dev never reads about
  "we" or about the repo owner.
- No "se lam", no "sap toi", no TODO, no open questions - if it is not shipped, it is not documented.
- Identifiers, file names, type names, code snippets and log strings stay exactly as in code
  (English).
- Do not translate the Vietnamese `//` field comments in the three panels, and do not copy them into
  docs as if they were API.

## Feature spec section order (Vietnamese headings)

Every file in `Documentation~/FEATURES/` keeps this skeleton (Online Reward uses `Trang thai va luu`
for the save section):

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

The checklist is the contract with QA - it must include a fresh-install case, a restart case, a
repeated open/close case, a null-button case and a null-`OnClaimed` case.

## Banned here, on top of the generic list

Never write, in any tracked file: `paint-and-seek`, `speed-clicker`, `asmr`, `_GameBase`,
`dress-to-impress`, a save-plugin SDK name, a `D:\Fork\...` path, "decision #", "decision record",
"ported from", "legacy", "reference game", "consumer team", any mention of an agent, an AI tool, a
skill, or a co-authored trailer, and no unfilled placeholder (`YYYY-MM-DD`, `<owner>`) at commit time.

Vietnamese diacritics are the only non-ASCII allowed; punctuation stays ASCII (`-`, `:`, `;`, `->`,
`x`, `<=`).

## Self-scan before handing back

Run over the worktree (not `--cached`, so it works mid-task):

```sh
export LC_ALL=C.UTF-8; grep -rn -I -i -P -e '\x{2014}|\x{2192}|\x{21D2}|\x{00D7}|\x{26A0}|\x{2026}|\x{2018}|\x{2019}|\x{201C}|\x{201D}|paint-and-seek|paint and seek|painandseek|dress-to-impress|speed-clicker|speed_clicker|asmr|_gamebase|d:[\/]fork|consumer team|ported from|legacy rule|reference game|decisions? #|decision record|\bagents?\b|claude|\bskills?\b|co-authored-by|generated with|] - yyyy-mm-dd|<owner>' Packages/com.nabagame.reward README.md --include=*.md --include=*.json && echo "DOC SCAN FAILED" || echo "doc scan clean"
```

Any hit is a fix, not a judgement call. Then re-read the changed section against the code you just
wrote: every API name, save key, placement string and default value in the doc must exist in
`Runtime/` with that exact spelling.
