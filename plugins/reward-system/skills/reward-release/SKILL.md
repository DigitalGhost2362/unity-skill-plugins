---
name: reward-release
description: Reward-repo delta on the studio git workflow. Read unity-git-release first - it owns the refusal posture, the allowlist pattern, the meta-pairing check, SemVer, the CHANGELOG shape, commit style and the publish-on-tag flow. This file holds the reward specifics - the exact tracked set, the leak-scan token list, the owner identity, fresh-clone junction setup, and the GitLab CI job. Use when asked to commit, stage, release, bump the version, update the changelog, tag, or push.
---

# Release and commit - reward delta

**Read `unity-git-release` first.** It is the workflow: what git shapes are refused,
the staging allowlist pattern, `.meta` pairing, tilde folders, the leak scan, SemVer, the CHANGELOG
entry, commit style, no attribution, tags, refusing to push, and publish-on-tag.

## 1. Tracked set (allowlist)

Tracked: `Packages/com.nabagame.reward/**` (with `.meta`, `Samples~/RewardDemo`, `Documentation~`),
`README.md`, `.gitignore`, `.gitattributes`.

Never staged: `Assets/`, `ProjectSettings/`, `Packages/manifest.json`, `Packages/packages-lock.json`,
any other folder under `Packages/`, `.claude/` (including `.claude/docs/`, the internal English
docs), `.mcp.json`, `CLAUDE.md`, `AGENTS.md`, `REWARD-PACKAGE-AUDIT-*.md`, `.vsconfig`, `Library/`,
`Temp/`, `Logs/`, `obj/`, `*.csproj`, `*.sln`.

```sh
git add Packages/com.nabagame.reward README.md .gitignore .gitattributes
git diff --cached --name-only | grep -v -E '^(Packages/com\.nabagame\.reward/|README\.md$|\.gitignore$|\.gitattributes$)'   # must print nothing
```

## 2. Meta pairing (before staging)

Every file and folder under `Runtime/` and every file under `Samples~/RewardDemo/` has a `.meta`; no
`.meta` without its asset. Folders under `Samples~` carry no `.meta`; `Documentation~` has none. Only
`Samples~/RewardDemo/README.md` is exempt.

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

Strict pass (code and metadata only; `*.md` docs are written in Vietnamese on purpose):
`git grep --cached -n -I -P '[^\x00-\x7F]' -- . ':!*.png' ':!*.ttf' ':!*.md' ':!*Panel.cs'` must
print nothing; in the three `*Panel.cs` files non-ASCII may appear only on serialized-field lines.

## 4. Version bump

The generic SemVer rules apply. Concretely, breaking here means: hooks, row shapes, panel API, save
payload, or removal of a public type. A save payload change also bumps that profile's `Version` field
and ships a migration or an explicit logged reset. Consumers reference the tag in the git URL:
`https://github.com/<owner>/<repo>.git?path=Packages/com.nabagame.reward#vX.Y.Z`.

## 5. Commit identity

Author and committer = the owner's existing identity (`git config user.name` = `Feeder`,
`user.email` = `75729692+BingoBoiz@users.noreply.github.com`). Never pass `--author`, never change
`user.*`.

`.claude/settings.json` (project scope, untracked) holds
`{ "attribution": { "commit": "", "pr": "" }, "includeCoAuthoredBy": false }`. After each commit
`git log -1 --format=%B | grep -ci 'claude\|co-authored'` must print 0. If it does not, the owner
amends by hand; do not amend.

## 6. Fresh clone setup (local only, per clone)

1. Append to `.git/info/exclude`: `.claude/`, `.mcp.json`, `CLAUDE.md`, `AGENTS.md`,
   `REWARD-PACKAGE-AUDIT-*.md`, `.vsconfig`.
2. Copy the local Unity host from the previous working tree (`Assets/`, `ProjectSettings/`,
   `Packages/manifest.json`, `Packages/packages-lock.json`, the other `Packages/*` folders) with
   `robocopy /XJ` so junctions are not materialized. Never stage them.
3. Recreate the sample junctions (cmd.exe, repo root):

```
mklink /J Assets\_RewardDemo\Scripts Packages\com.nabagame.reward\Samples~\RewardDemo\Scripts
mklink /J Assets\_RewardDemo\Prefabs Packages\com.nabagame.reward\Samples~\RewardDemo\Prefabs
mklink /J Assets\_RewardDemo\Scenes  Packages\com.nabagame.reward\Samples~\RewardDemo\Scenes
mklink /J Assets\_RewardDemo\Fonts   Packages\com.nabagame.reward\Samples~\RewardDemo\Fonts
mklink /J Assets\_RewardDemo\Art\ItemReceived Packages\com.nabagame.reward\Samples~\RewardDemo\Art\ItemReceived
```

The 33 top-level sample sprites share GUIDs with the host's own art folder; junction the whole `Art`
folder only if that host folder is gone, otherwise Unity reports duplicate GUIDs.

## 7. Language

- Docs (`README.md`, `CHANGELOG.md`, `Documentation~/**/*.md`, `Samples~/RewardDemo/README.md`,
  `package.json` descriptions) are written in Vietnamese for the consuming team: short, plain, ASCII
  punctuation. Identifiers, file names and code snippets stay as in code.
- Code: English identifiers and comments. Vietnamese `//` comments only on the serialized fields of
  `DailyRewardPanel`, `LuckySpinPanel`, `OnlineRewardPanel`.

## 8. When asked to push

Refuse. Say: "Push is yours: `git push origin main --follow-tags`." Do not run it even with
`--dry-run`, do not add remotes, do not open PRs.

## 9. Publish via GitLab npm registry (CI)

Two tracked files make it work:

- `Packages/com.nabagame.reward/Tools~/gitlab-ci.yml` - a GitLab CI job (`publish_reward_package`)
  that runs only when a tag matching `vX.Y.Z` is pushed. It verifies the tag equals the
  `package.json` version (mismatch fails the job), writes a temporary `.npmrc-ci` auth file from
  `$CI_JOB_TOKEN`, runs `npm publish --registry "$CI_API_V4_URL/projects/$CI_PROJECT_ID/packages/npm/"`,
  and deletes the auth file in `after_script`.
- `Packages/com.nabagame.reward/.npmignore` - keeps `Tools~/` out of the published tarball.

Both live under `Packages/com.nabagame.reward/**`, so they stage with the normal allowlist and need
no `.meta` (the meta check only covers `Runtime/` and `Samples~/RewardDemo/`). They are static
infrastructure, not rewritten per release. Pushing is still the owner's job.
