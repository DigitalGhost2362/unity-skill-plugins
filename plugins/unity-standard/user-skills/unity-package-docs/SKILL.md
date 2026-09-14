---
name: unity-package-docs
description: Mandatory workflow for writing or updating any TRACKED doc that ships with a Unity package or project - README.md, CHANGELOG.md, package.json descriptions, Documentation~/ARCHITECTURE.md, INTEGRATION-GUIDE.md, per-feature specs, sample READMEs. Holds the tracked-vs-internal split, the reader test, the doc-sync matrix (which files must move together when behavior changes), the voice rules, the ASCII punctuation rule, and the self-scan to run BEFORE handing work back instead of discovering leaks at commit time. Trigger on any request to write, translate, restructure, review or sync package documentation, changelog entries, or a feature spec.
---

# Package docs workflow (tracked docs only)

Tracked docs are a **product surface**: they ship to the consuming team inside the package. They are
not a work log. `unity-git-release` owns the commit-time rules; this skill owns the writing.

## Rule 0 - Tracked or internal?

| Ships with the package (tracked) | Stays local (`.claude/docs/<package>/`, untracked) |
|---|---|
| What the package does today, how to use it | Why it is built that way, the decision record, the roadmap |
| Data shapes, API, save keys, hooks, placements | Conventions, research, changelog archaeology |
| Per-feature behavior + the dev's verification checklist | Anything naming another project, an SDK, an agent or a tool |

If a sentence explains *history*, *alternatives considered*, or *how the code came to be*, it belongs
in the internal copy. Never move internal material back into the shipped docs.

## Rule 0b - The reader test (every sentence, no exceptions)

Before a sentence goes into a tracked doc, answer: **after reading this, does the dev do anything
differently?** If no, cut it. A tracked doc is the list of things the reader must do or decide, not
a record of what the package does inside.

Cut on sight:

- Mechanics the reader cannot act on ("package X is a shim pointing at Y", "Unity does not support
  git URLs in a package's `dependencies`"). The actionable half is "install these first" - ship only
  that half.
- Reassurance about work they never had to do ("Package Manager installs it, you do not have to do
  anything").
- Answers to a question the reader is not asking yet: update mechanics inside an install section,
  cache-clearing tips for a dev who has not installed once.
- Justification of a design, or an alternative that was rejected - that lives in the internal copy.
- Facts that only matter to whoever maintains the package: assembly names, script GUIDs, internal
  loop shapes, "the profile must stay flat".

Length is a budget, not a free resource: every paragraph the reader skips makes the paragraph that
mattered less likely to be read. When in doubt, cut. A dev who needs the detail will ask; a dev
drowned in it reads nothing.

## Rule 1 - The doc-sync matrix

A behavior change is not done until every row that applies has been updated **in the same change**.
Fill this table in per project with the real file paths, then treat it as a checklist:

| What changed | Files that must change with it |
|---|---|
| Data/row field added, removed or renamed | the feature spec's data section, the integration guide's data section, `CHANGELOG.md`, `package.json` |
| Public API - init signature, queries, config properties | the feature spec's API section, the integration guide's usage section, `CHANGELOG.md`, `package.json` |
| Hook signature, or a new hook | the architecture doc's hook section, the integration guide's hook section, every feature spec that uses it, `CHANGELOG.md`, `package.json` |
| Save payload or its version | the feature spec's save section, the architecture doc's persistence section, `CHANGELOG.md`, `package.json` (bump + migration or logged reset) |
| Event added or renamed | the architecture doc's event section, the feature spec, `CHANGELOG.md` |
| Analytics parameter or placement name | the feature spec, the integration guide's analytics sections, `CHANGELOG.md` |
| New dependency | the architecture doc's dependency section, root `README.md`, `CHANGELOG.md`, `package.json` |
| Sample scene/prefab/script shape | the sample's `README.md`, and the feature spec if the dev copies from it |
| Folder layout of `Runtime/` | root `README.md` |

`package.json` version and the `CHANGELOG.md` entry always move together; SemVer rules live in
`unity-git-release`.

## Rule 2 - Voice

- Written for a dev who has to ship a feature today. Short sentences, imperative, no marketing.
- Second person is the consuming dev; the subject is "the package". The dev never reads about "we"
  or about the repo owner.
- State behavior in the present tense as fact. No "will do", no "coming soon", no TODO, no open
  questions - **if it is not shipped, it is not documented**.
- Identifiers, file names, type names, code snippets and log strings stay exactly as in code.
- **Language is a per-project setting.** A package written for a specific team may ship its docs in
  that team's language (this is the norm on NBG packages, where tracked docs are Vietnamese and code
  identifiers stay English). Follow whatever the existing docs do; never mix, and never translate a
  code identifier or an existing in-code comment.

## Rule 3 - A feature spec has a fixed section order

Every per-feature doc keeps the same skeleton, so a dev who has read one can navigate all of them.
Localize the headings if the project's docs are not in English, but keep the order and the meaning:

```text
# <Feature>
one line: which panel owns it, which manager the dev writes
## What it is            what the player sees happen
## Interface             the visual contract, per state
## Data (you supply)     row shape, leniency, what the prefab holds
## Saving                table: field -> meaning, plus when it is written
## API: <Panel>          only the public members, grouped as in code
## Events / hooks        outcomes, notifications, hooks used, placement, analytics
## Verification checklist  numbered, runnable by the dev, one behavior per line
## Settled rules         the rules that look wrong until explained
## Layout notes          optional, art substitutions and sizing
```

The checklist is the contract with QA - it must include a fresh-install case, a restart case, a
repeated open/close case, a null-reference case and a null-callback case. When behavior changes, fix
the checklist line too, not just the prose.

## Rule 4 - Banned in tracked docs

Never write, in any tracked file: another project or studio name, an internal SDK or save-plugin
name, an absolute developer path, "decision #", "decision record", "ported from", "legacy",
"reference game", "consumer team", any mention of an agent, an AI tool, or a skill, any co-authored
trailer, and no unfilled placeholder (`YYYY-MM-DD`, `<owner>`) at commit time.

Punctuation is ASCII only: `-`, `:`, `;`, `->`, `x`, `<=`. No em-dash, no arrow glyph, no
multiplication sign, no warning sign, no curly quotes, no ellipsis character. Diacritics in a
non-English doc language are fine; they are the only non-ASCII allowed.

## Rule 5 - Self-scan before handing back

Run over the worktree (not `--cached`, so it works mid-task). Fill the token list with this
project's banned words:

```sh
export LC_ALL=C.UTF-8
grep -rn -I -i -P -e '\x{2014}|\x{2192}|\x{21D2}|\x{00D7}|\x{26A0}|\x{2026}|\x{2018}|\x{2019}|\x{201C}|\x{201D}|<other-project-names>|<sdk-names>|[a-z]:[\/]\w+[\/]|consumer team|ported from|legacy rule|reference game|decisions? #|decision record|\bagents?\b|claude|\bskills?\b|co-authored-by|generated with|] - yyyy-mm-dd|<owner>' \
  <tracked-doc-paths> --include=*.md --include=*.json \
  && echo "DOC SCAN FAILED" || echo "doc scan clean"
```

Any hit is a fix, not a judgement call. Then re-read the changed section against the code you just
wrote: every API name, save key, placement string and default value in the doc must exist in the
runtime with that exact spelling.

## Checklist

- [ ] Reader test (Rule 0b) run over every new sentence: nothing left that the dev cannot act on.
- [ ] Every row of the doc-sync matrix that applies was updated in this change.
- [ ] `package.json` + `CHANGELOG.md` moved together, with a real date and a SemVer-correct number.
- [ ] Feature spec keeps the fixed section order; its checklist covers the new behavior.
- [ ] No history, no decisions, no roadmap, no other project, no SDK name in a tracked file.
- [ ] ASCII punctuation only; identifiers match the code character for character.
- [ ] Doc scan clean.
