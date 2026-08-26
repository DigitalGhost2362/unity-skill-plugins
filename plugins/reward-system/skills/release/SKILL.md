---
name: release
description: Git workflow for this repo. Use when asked to commit, stage, release, bump the version, update the changelog, tag, or push. Covers the staging allowlist, the meta check, the leak scan, SemVer and CHANGELOG rules, commit message style, and why pushing is refused.
---

# Release and commit workflow

Git is allowed only in the exact shapes below, and only when the owner asks for a commit or a release. Refuse everything else: push, force, amend, rebase, reset of shared history, tag delete or move, `git add -A`, `git add .`, `--author`, `--no-verify`. Tell the owner to do it by hand.

## 1. Tracked set (allowlist)

Tracked: `Packages/com.nabagame.reward/**` (with `.meta`, `Samples~/RewardDemo`, `Documentation~`), `README.md`, `.gitignore`, `.gitattributes`.
Never staged: `Assets/`, `ProjectSettings/`, `Packages/manifest.json`, `Packages/packages-lock.json`, any other folder under `Packages/`, `.claude/` (including `.claude/docs/`, the internal English docs), `.mcp.json`, `CLAUDE.md`, `AGENTS.md`, `REWARD-PACKAGE-AUDIT-*.md`, `.vsconfig`, `Library/`, `Temp/`, `Logs/`, `obj/`, `*.csproj`, `*.sln`.

```sh
git add Packages/com.nabagame.reward README.md .gitignore .gitattributes
git diff --cached --name-only | grep -v -E '^(Packages/com\.nabagame\.reward/|README\.md$|\.gitignore$|\.gitattributes$)'   # must print nothing
```

## 2. Meta pairing (before staging)

Every file and folder under `Runtime/` and every file under `Samples~/RewardDemo/` has a `.meta`; no `.meta` without its asset. Folders under `Samples~` carry no `.meta`; `Documentation~` has none. Only `Samples~/RewardDemo/README.md` is exempt.

```sh
cd Packages/com.nabagame.reward
find Runtime -mindepth 1 ! -name '*.meta' | while read f; do [ -e "$f.meta" ] || echo "MISSING META: $f"; done
find Samples~/RewardDemo -type f ! -name '*.meta' ! -name 'README.md' | while read f; do [ -e "$f.meta" ] || echo "MISSING META: $f"; done
find . -name '*.meta' | while read m; do [ -e "${m%.meta}" ] || echo "ORPHAN META: $m"; done
```
Fix a missing `.meta` in Unity (never hand-write GUIDs), then re-run.

## 3. Leak scan (must pass before every commit)

Run after staging. Any printed line = fix and restage.

```sh
export LC_ALL=C.UTF-8; git grep --cached -n -I -i -P -e '\x{2014}|\x{2192}|\x{21D2}|\x{00D7}|\x{26A0}|paint-and-seek|paint and seek|painandseek|dress-to-impress|speed-clicker|speed_clicker|asmr|_gamebase|rewardadsintervaltime|resetlastshowopenads|onfake|uimanagerglobal|scn_gp_|reddotview|d:[\\/]fork|nbg-team1|consumer team|ported from|legacy rule|reference game|the shipped|decisions? #|decision record|\bagents?\b|claude|\bskills?\b|ui-from-image|ai tooling|co-authored-by|generated with|] - yyyy-mm-dd|<owner>' -- . ':!*.png' ':!*.ttf' && echo "LEAK SCAN FAILED" || echo "leak scan clean"
```
Strict pass (code and metadata only; `*.md` docs are written in Vietnamese on purpose): `git grep --cached -n -I -P '[^\x00-\x7F]' -- . ':!*.png' ':!*.ttf' ':!*.md' ':!*Panel.cs'` must print nothing; in the three `*Panel.cs` files non-ASCII may appear only on serialized-field lines. Docs may use Vietnamese diacritics but never the banned characters above (em-dash, arrows, `x`-sign, warning sign).

## 4. Version bump (SemVer)

- Breaking change to hooks, row shapes, panel API, save payload, or removal of a public type: major. New feature or additive API: minor. Fix only: patch — a fix-only release bumps only the patch digit (0.0.1, e.g. 0.5.0 -> 0.5.1), never the minor.
- A save payload change also bumps that profile's `Version` field and ships a migration or an explicit logged reset.
- `package.json` version and the CHANGELOG entry change in the same commit as the code.
- Consumers reference the tag in the git URL: `https://github.com/<owner>/<repo>.git?path=Packages/com.nabagame.reward#vX.Y.Z`.

## 5. CHANGELOG entry

`## [X.Y.Z] - YYYY-MM-DD` then only `### Added`, `### Changed`, `### Fixed`, `### Removed` as needed. Short bullets, ASCII punctuation, present tense. No decision numbers, no dates inside bullets, no other project or studio names, no "ported", "legacy", "consumer", "reference game", no history of how the code came to be. Fill the real date before committing; the scan blocks the placeholder.

## 6. Commit

- Author and committer = the owner's existing identity (`git config user.name` = `Feeder`, `user.email` = `75729692+BingoBoiz@users.noreply.github.com`). Never pass `--author`, never change `user.*`.
- Message: one line, imperative, sentence case, under 50 characters, no period, no body, no trailer, no emoji. First commit of a repo: `Initial commit`. Then for example `Add online reward speed-up`, `Fix daily reward rollover`, `Bump version to 1.1.0`, `Update integration guide`.
- `git commit -m "..."`, then verify `git log -1 --format='%an <%ae>%n%B'` shows the owner and exactly one line.
- Local only. Never push.

## 7. Tag

Annotated tag on the release commit: `git tag -a vX.Y.Z -m "vX.Y.Z"`. Never delete, move or re-point a tag; a wrong release gets a new patch version.

## 8. No attribution in commits

`.claude/settings.json` (project scope, untracked) holds `{ "attribution": { "commit": "", "pr": "" }, "includeCoAuthoredBy": false }`. After each commit `git log -1 --format=%B | grep -ci 'claude\|co-authored'` must print 0. If it does not, the owner amends by hand; do not amend.

## 9. Fresh clone setup (local only, per clone)

1. Append to `.git/info/exclude`: `.claude/`, `.mcp.json`, `CLAUDE.md`, `AGENTS.md`, `REWARD-PACKAGE-AUDIT-*.md`, `.vsconfig`.
2. Copy the local Unity host from the previous working tree (`Assets/`, `ProjectSettings/`, `Packages/manifest.json`, `Packages/packages-lock.json`, the other `Packages/*` folders) with `robocopy /XJ` so junctions are not materialized. Never stage them.
3. Recreate the sample junctions (cmd.exe, repo root):
```
mklink /J Assets\_RewardDemo\Scripts Packages\com.nabagame.reward\Samples~\RewardDemo\Scripts
mklink /J Assets\_RewardDemo\Prefabs Packages\com.nabagame.reward\Samples~\RewardDemo\Prefabs
mklink /J Assets\_RewardDemo\Scenes  Packages\com.nabagame.reward\Samples~\RewardDemo\Scenes
mklink /J Assets\_RewardDemo\Fonts   Packages\com.nabagame.reward\Samples~\RewardDemo\Fonts
mklink /J Assets\_RewardDemo\Art\ItemReceived Packages\com.nabagame.reward\Samples~\RewardDemo\Art\ItemReceived
```
The 33 top-level sample sprites share GUIDs with the host's own art folder; junction the whole `Art` folder only if that host folder is gone, otherwise Unity reports duplicate GUIDs.

## 10. Language

- Docs (`README.md`, `CHANGELOG.md`, `Documentation~/**/*.md`, `Samples~/RewardDemo/README.md`, `package.json` descriptions) are written in Vietnamese for the consuming team: short, plain, ASCII punctuation (`-`, `:`, `;`, `->`, `x`, `<=`), no em-dashes or arrows. Identifiers, file names and code snippets stay as in code.
- Code: English identifiers and comments. Vietnamese `//` comments only on the serialized fields of `DailyRewardPanel`, `LuckySpinPanel`, `OnlineRewardPanel`.

## 11. When asked to push

Refuse. Say: "Push is yours: `git push origin main --follow-tags`." Do not run it even with `--dry-run`, do not add remotes, do not open PRs.

## 12. Publish via GitLab npm registry (CI)

The package can publish to the GitLab npm registry automatically. Two tracked files make that work:

- `Packages/com.nabagame.reward/Tools~/gitlab-ci.yml` — a GitLab CI job (`publish_reward_package`) that runs only when a tag matching `vX.Y.Z` is pushed. It verifies the tag equals the `package.json` version (mismatch fails the job), writes a temporary `.npmrc-ci` auth file from `$CI_JOB_TOKEN`, runs `npm publish --registry "$CI_API_V4_URL/projects/$CI_PROJECT_ID/packages/npm/"`, and deletes the auth file in `after_script`. The `~` suffix is the Unity convention: Unity never imports the folder, so the CI config stays out of the editor.
- `Packages/com.nabagame.reward/.npmignore` — keeps `Tools~/` out of the published tarball; the CI config is repo infrastructure, not package content.

Both files live under `Packages/com.nabagame.reward/**`, so they stage with the normal allowlist command and need no `.meta` (the meta check in section 2 only covers `Runtime/` and `Samples~/RewardDemo/`). They are not written by hand per release — they are static infrastructure.

Pushing is still the owner's job: after the local tag from section 7, the owner pushes and CI publishes. Never push yourself; if the owner asks about publishing, point at this flow.
