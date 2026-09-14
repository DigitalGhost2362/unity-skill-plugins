# <Task title>

- **Status:** pending
- **Written:** <yyyy-MM-dd HH:mm> by <owner>
- **Branch @ HEAD:** <branch> @ <short sha> (plus uncommitted changes below)
- **Compiled:** yes, console clean | no - Editor was busy
- **Touched files** (`git hash-object`):
  - `<path>` - `<hash>`

## Why

What changed, in one paragraph, and what it could break. Name the flows a regression would show up in.

## Preconditions

- Scene(s) open, and which one is active:
- Prefs / save state (with how to back up and restore it):
- Config values or debug toggles:
- Anything from the project's `TESTCASE.md` reset recipe that applies:

## Cases

### TC1 - <what is proven>

- **Steps:**
  1. `<mcp tool>` with `<args>` / `script-execute`: `<snippet>` / debug button `<Class.Method>`
- **Expect:** `<exact log line>` / `<state read and value>` / screenshot showing `<what>`
- **Result:** _

### TC2 - <edge case: re-entry, missing reference, empty data, second call>

- **Steps:**
- **Expect:**
- **Result:** _

## Standing passes

Only when a panel changed: re-entry, null-reference, null-callback (`unity-playmode-verify` Rule 6).

## Cleanup

What to restore after the run: prefs, save file, timeScale, scenes.

## Not covered

What these cases cannot prove (real SDK, device-only, multi-day timer) - so nobody reads PASS as more
than it is.

## Run log

<!-- verifier appends: time, owner, per-case PASS/FAIL/BLOCKED + evidence, fixes (file + reason) -->
