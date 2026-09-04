---
name: unity-git-release
description: Git workflow for a Unity repo or UPM package. Use when asked to commit, stage, release, bump the version, update the changelog, or tag. Covers the staging allowlist, the .meta pairing check, the tilde-folder convention, the leak scan, SemVer and CHANGELOG rules, commit message style, why pushing is refused, and publishing on tag from CI.
---

# Release and commit workflow

Git is allowed only in the exact shapes below, and only when the owner asks for a commit or a
release. **Refuse everything else**: push, force, amend, rebase, reset of shared history, tag delete
or move, `git add -A`, `git add .`, `--author`, `--no-verify`. Tell the owner to do it by hand.

## 1. Tracked set (allowlist)

A Unity repo is mostly untracked machine state. Stage an explicit allowlist, never a wildcard, then
prove nothing else got in:

```sh
git add <tracked path 1> <tracked path 2> ...
git diff --cached --name-only | grep -v -E '^(<tracked path 1>|<tracked path 2>)'   # must print nothing
```

Normally never staged: `Library/`, `Temp/`, `Logs/`, `obj/`, `*.csproj`, `*.sln`, `.vsconfig`,
`.claude/`, `.mcp.json`, `CLAUDE.md`, `AGENTS.md`, audit/scratch markdown - and, when the repo's
product is a package rather than the game, also `Assets/`, `ProjectSettings/`,
`Packages/manifest.json`, `Packages/packages-lock.json` and every other folder under `Packages/`.

Write the project's real allowlist into its own delta skill or `CLAUDE.md` once, so this is a lookup
and not a judgement call each time.

## 2. `.meta` pairing (before staging)

Unity identity lives in `.meta` files, and a missing one silently re-GUIDs an asset on the next
import - breaking every reference to it in someone else's clone.

Every file and folder Unity imports has a `.meta`; no `.meta` exists without its asset. Folders
under a `Samples~` directory carry no `.meta` at all, and `Documentation~` has none - Unity never
imports a tilde folder.

```sh
find <imported root> -mindepth 1 ! -name '*.meta' | while read f; do [ -e "$f.meta" ] || echo "MISSING META: $f"; done
find . -name '*.meta' | while read m; do [ -e "${m%.meta}" ] || echo "ORPHAN META: $m"; done
```

Fix a missing `.meta` **in Unity** - never hand-write a GUID - then re-run.

## 3. Tilde folders

`Documentation~`, `Samples~`, `Tools~`: Unity does not import them, so they hold docs, sample
content and repo infrastructure (CI config, publish scripts) without appearing in the editor or
needing meta files. Keep infrastructure out of the published tarball with `.npmignore`.

## 4. Leak scan (must pass before every commit)

Run after staging. Any printed line is a fix-and-restage, not a judgement call. Fill the token list
with the things this repo must never ship: other project or studio names, internal SDK names,
absolute developer paths, internal hostnames, agent/tool mentions, unfilled placeholders, and the
non-ASCII punctuation the docs ban.

```sh
export LC_ALL=C.UTF-8
git grep --cached -n -I -i -P -e '<banned tokens>' -- . ':!*.png' ':!*.ttf' \
  && echo "LEAK SCAN FAILED" || echo "leak scan clean"
```

Add a strict ASCII pass over code and metadata (docs may legitimately be in another language):

```sh
git grep --cached -n -I -P '[^\x00-\x7F]' -- . ':!*.png' ':!*.ttf' ':!*.md'   # must print nothing
```

Carve out only the files where non-ASCII is deliberate and say why in the project's delta.

## 5. Version bump (SemVer)

- **Major**: a breaking change to a hook, a data shape, a public panel API, the save payload, or the
  removal of a public type.
- **Minor**: a new feature or an additive API.
- **Patch**: a fix only - a fix-only release bumps **only the patch digit** (0.5.0 -> 0.5.1), never
  the minor.
- A save-payload change also bumps that payload's `Version` field and ships either a migration or an
  explicit logged reset.
- `package.json` version and the CHANGELOG entry change in the same commit as the code.
- Consumers reference the tag in the git URL:
  `https://<host>/<owner>/<repo>.git?path=Packages/<package>#vX.Y.Z`.

## 6. CHANGELOG entry

`## [X.Y.Z] - YYYY-MM-DD` then only `### Added`, `### Changed`, `### Fixed`, `### Removed` as
needed. Short bullets, ASCII punctuation, present tense. No decision numbers, no dates inside
bullets, no other project or studio names, no "ported", "legacy", "consumer", "reference game", no
history of how the code came to be. Fill the real date before committing; the scan blocks the
placeholder.

## 7. Commit

- Author and committer stay the owner's existing identity. Never pass `--author`, never change
  `user.*`.
- Message: one line, imperative, sentence case, under 50 characters, no period, no body, no trailer,
  no emoji. First commit of a repo: `Initial commit`. Then e.g. `Add online reward speed-up`,
  `Fix daily reward rollover`, `Bump version to 1.1.0`, `Update integration guide`.
- `git commit -m "..."`, then verify `git log -1 --format='%an <%ae>%n%B'` shows the owner and
  exactly one line.
- Local only. Never push.

## 8. No attribution in commits

Configure it once, at project scope:
`{ "attribution": { "commit": "", "pr": "" }, "includeCoAuthoredBy": false }`. After each commit
`git log -1 --format=%B | grep -ci 'claude\|co-authored'` must print 0. If it does not, the owner
amends by hand; **do not amend**.

## 9. Tag

Annotated tag on the release commit: `git tag -a vX.Y.Z -m "vX.Y.Z"`. Never delete, move or re-point
a tag; a wrong release gets a new patch version.

## 10. When asked to push

Refuse. Print the command instead - `git push origin main --follow-tags` - and let the owner run it.
Do not run it even with `--dry-run`, do not add remotes, do not open PRs.

## 11. Publishing on tag from CI

A package can publish itself to a registry when a `vX.Y.Z` tag is pushed. The job belongs in a tilde
folder (`Tools~/`) so Unity never imports it, and `.npmignore` keeps that folder out of the
published tarball. The job must verify the tag equals the `package.json` version and fail on a
mismatch, write its auth file from a CI token, publish, and delete the auth file in `after_script`.
These files are static infrastructure - they are not rewritten per release.

Pushing is still the owner's job: after the local tag, the owner pushes and CI publishes. Never push
yourself; if the owner asks about publishing, point at this flow.
