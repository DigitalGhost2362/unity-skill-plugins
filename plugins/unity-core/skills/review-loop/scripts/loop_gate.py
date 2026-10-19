#!/usr/bin/env python3
"""Deterministic gate for the review-loop skill. Python 3 stdlib and git only.

Commands
  init     snapshot the working tree (tracked, modified and untracked) as the baseline,
           hash the contract, count the tests
  check    snapshot again, diff against the baseline, scan for forbidden paths, scope
           creep, a dropped test count and cheat patterns; write diff.patch and gate-N.json
  verdict  combine the latest gate with the reviewer JSON into PASS / FAIL / UNVERIFIED /
           STOP and print it; the agent relays that block verbatim
  level    record a strictness change the user asked for
  pair     visual tasks: put this round's shots and the best round's shots side by side under
           blind X/Y labels for the judge; the label map stays in .git, out of the judge's reach

State lives in <repo>/.review-loop/ (added to .git/info/exclude, never committed).

Exit codes: 0 clean, 1 fixable issues, 2 stop and ask the user, 3 the gate itself failed.
Any unexpected error exits 3: a gate that crashes must never read as a pass.
"""
import argparse
import hashlib
import json
import os
import random
import re
import shutil
import subprocess
import sys
import tempfile
import xml.etree.ElementTree as ET

LEVELS = ["gates", "quick", "standard", "strict"]
FIX_ROUNDS = {"gates": 2, "quick": 1, "standard": 2, "strict": 3}
# Matching a reference image moves in smaller steps than fixing a bug, so it gets more rounds;
# the pivot rule, not the cap, is what stops a loop that only retunes numbers.
VISUAL_FIX_ROUNDS = {"gates": 2, "quick": 2, "standard": 4, "strict": 6}
CHECKS = ("compile", "tests", "review", "play", "visual", "metric")
VISUAL = ("visual", "metric")
NOT_RUN = {
    "gates": ["reviewer", "refuter", "play mode"],
    "quick": ["two-phase blind review", "refuter", "play mode"],
    "standard": ["refuter per finding", "play mode", "user read of test diffs"],
    "strict": [],
}

TEST_GLOBS = [
    "**/Tests/**", "**/Test/**", "**/Editor/Tests/**",
    "**/*Tests.cs", "**/*Test.cs", "**/*Tests.asmdef", "**/*Test.asmdef",
]
DEFAULT_FORBIDDEN = TEST_GLOBS + [
    ".review-loop/**", ".claude/**", ".codex/**", ".agents/**", ".github/**",
    ".gitlab-ci.yml", "CLAUDE.md", "AGENTS.md",
    "ProjectSettings/**", "Packages/manifest.json", "Packages/packages-lock.json",
]

# ERE for git grep: an NUnit test attribute opening a [...] list or following a comma in one.
TEST_ATTR_ERE = r"[[,][[:space:]]*(Test|UnityTest|TestCase|TestCaseSource)[[:space:]]*[](,]"

# (id, regex, where, severity). where: any | test | src | asmdef | manifest
PATTERNS = [
    ("ignore-attribute", r"[\[,]\s*(Ignore|Explicit)\s*[\](,]", "any", "fail"),
    ("assert-skip", r"\bAssert\.(Pass|Ignore|Inconclusive)\s*\(", "any", "fail"),
    ("assert-always-true", r"\bAssert\.(IsTrue|True|That)\s*\(\s*true\s*[,)]", "any", "fail"),
    ("logassert-ignore", r"LogAssert\.ignoreFailingMessages\s*=\s*true", "any", "fail"),
    ("logging-disabled", r"Debug\.unityLogger\.logEnabled\s*=\s*false", "any", "fail"),
    ("process-exit", r"\b(Environment\.Exit|EditorApplication\.Exit)\s*\(", "any", "fail"),
    ("assert-weakened", r"\bAssert\.(IsNotNull|NotNull|IsTrue|True)\s*\(", "test", "warn"),
    ("application-quit", r"\bApplication\.Quit\s*\(", "src", "warn"),
    ("empty-catch", r"catch\s*(\([^)]*\))?\s*\{\s*\}", "any", "warn"),
    ("warning-disabled", r"#\s*pragma\s+warning\s+disable", "any", "warn"),
    ("dead-branch", r"#\s*if\s+(false|0)\b", "any", "warn"),
    ("asmdef-constraint", r"\"(defineConstraints|includePlatforms|excludePlatforms|overrideReferences)\"",
     "asmdef", "warn"),
    ("manifest-testables", r"\"testables\"", "manifest", "warn"),
]
STRING_TOKEN = re.compile(r'"((?:[^"\\\r\n]|\\.)*)"')
NUMBER_LIT = re.compile(r"(?<![\w.])(\d+\.\d{3,}|\d{5,})(?![\w.])")


def distinctive_literals(text):
    """String literals of 6-80 chars and long or precise numbers: the values a
    special-cased implementation copies out of a test."""
    strings = [s for s in STRING_TOKEN.findall(text) if 6 <= len(s) <= 80]
    return set(strings) | set(NUMBER_LIT.findall(STRING_TOKEN.sub('""', text)))


class GateError(Exception):
    pass


ROOT = None
STATE = None


def git(*args, env=None, ok_codes=(0,)):
    r = subprocess.run(["git", *args], cwd=ROOT, env=env, capture_output=True,
                       text=True, encoding="utf-8", errors="replace")
    if r.returncode not in ok_codes:
        raise GateError(f"git {' '.join(args[:3])} failed: {r.stderr.strip()[:400]}")
    return r.stdout


def find_root():
    r = subprocess.run(["git", "rev-parse", "--show-toplevel"], capture_output=True, text=True)
    if r.returncode != 0:
        raise GateError("not inside a git repository; the gate needs git to snapshot the tree")
    return r.stdout.strip()


def git_path(name):
    p = git("rev-parse", "--git-path", name).strip()
    return p if os.path.isabs(p) else os.path.join(ROOT, p)


def ensure_excluded():
    path = git_path("info/exclude")
    os.makedirs(os.path.dirname(path), exist_ok=True)
    text = open(path, encoding="utf-8").read() if os.path.exists(path) else ""
    if "/.review-loop/" not in text.splitlines():
        with open(path, "a", encoding="utf-8") as f:
            f.write(("" if text.endswith("\n") or not text else "\n") + "/.review-loop/\n")


def snapshot():
    """Tree SHA of the whole working tree, untracked files included, without touching
    the real index. Starts from a copy of the index so unchanged files are not re-hashed."""
    index = git_path("index")
    fd, tmp = tempfile.mkstemp(prefix="review-loop-index-")
    os.close(fd)
    try:
        if os.path.exists(index):
            shutil.copyfile(index, tmp)
        else:
            os.remove(tmp)
        env = dict(os.environ, GIT_INDEX_FILE=tmp)
        git("add", "-A", env=env)
        return git("write-tree", env=env).strip()
    finally:
        if os.path.exists(tmp):
            os.remove(tmp)


def glob_re(pattern):
    out, i = "", 0
    while i < len(pattern):
        if pattern.startswith("**/", i):
            out, i = out + "(?:.*/)?", i + 3
        elif pattern.startswith("**", i):
            out, i = out + ".*", i + 2
        elif pattern[i] == "*":
            out, i = out + "[^/]*", i + 1
        elif pattern[i] == "?":
            out, i = out + "[^/]", i + 1
        else:
            out, i = out + re.escape(pattern[i]), i + 1
    return re.compile(out + r"\Z", re.IGNORECASE)


def matches(path, globs):
    # Foo.cs.meta goes with Foo.cs, and a folder's Foo.meta with the folder Foo/.
    bare = path[:-5] if path.lower().endswith(".meta") else path
    return any(glob_re(g).match(p) for g in globs for p in (path, bare, bare + "/"))


def sha_file(path):
    with open(path, "rb") as f:
        return hashlib.sha256(f.read()).hexdigest()[:12]


def load(name, required=True):
    path = os.path.join(STATE, name)
    if not os.path.exists(path):
        if required:
            raise GateError(f"{name} missing in .review-loop/ - run the earlier step first")
        return None
    with open(path, encoding="utf-8") as f:
        return json.load(f)


def save(name, data):
    with open(os.path.join(STATE, name), "w", encoding="utf-8") as f:
        json.dump(data, f, indent=2, ensure_ascii=False)


def test_counts(tree):
    out = git("grep", "-o", "-E", TEST_ATTR_ERE, tree, "--", "*.cs", ok_codes=(0, 1))
    counts = {}
    for line in out.splitlines():
        parts = line.split(":", 2)
        if len(parts) == 3:
            counts[parts[1]] = counts.get(parts[1], 0) + 1
    return counts


def is_test_file(path, counts):
    return matches(path, TEST_GLOBS) or path in counts


def load_contract():
    contract = load("contract.json")
    criteria = contract.get("criteria")
    if not isinstance(criteria, list) or not criteria:
        raise GateError("contract.json needs a non-empty criteria list")
    for c in criteria:
        if not c.get("id") or c.get("check") not in CHECKS:
            raise GateError(f"criterion {c!r} needs an id and check: {'|'.join(CHECKS)}")
        if c["check"] == "metric" and not (c.get("metric") and {"min", "max", "ref_tol"} & set(c)):
            raise GateError(f"metric criterion {c['id']} needs metric and one of min, max, ref_tol")
    if any(c["check"] == "visual" for c in criteria):
        refs = contract.get("reference")
        if not isinstance(refs, dict) or not refs or not contract.get("shots"):
            raise GateError("visual criteria need reference {shot: image} and shots (the shots.json path)")
        for c in criteria:
            if c["check"] == "visual" and c.get("shot") not in refs:
                raise GateError(f"visual criterion {c['id']} needs a shot named in reference")
    if any(c["check"] in VISUAL for c in criteria):
        triage = contract.get("triage")
        if not isinstance(triage, list) or not triage:
            raise GateError("a visual contract needs triage: every element of the reference, its kind, in_scope")
        for t in triage:
            if not t.get("element") or not t.get("kind") or not isinstance(t.get("in_scope"), bool):
                raise GateError(f"triage item {t!r} needs element, kind and in_scope true|false")
            if not t["in_scope"] and not str(t.get("excluded_by", "")).strip():
                raise GateError(f"triage item {t['element']!r} is out of scope without excluded_by "
                                "(the user's words) - only the user takes part of the reference out")
    contract.setdefault("allowed", [])
    contract.setdefault("tests_may_change", False)
    if "forbidden" not in contract:
        forbidden = list(DEFAULT_FORBIDDEN)
        if contract["tests_may_change"]:
            forbidden = [g for g in forbidden if g not in TEST_GLOBS]
        contract["forbidden"] = forbidden
    return contract


def is_visual(contract):
    return any(c["check"] in VISUAL for c in contract["criteria"])


def frozen_files(contract):
    """Files a visual loop must not move: the reference images, the camera poses and the spec.
    Hashed directly because they often live in gitignored folders the tree snapshot skips."""
    paths = list((contract.get("reference") or {}).values())
    paths += [p for p in (contract.get("shots"), contract.get("spec")) if p]
    out = {}
    for p in paths:
        full = p if os.path.isabs(p) else os.path.join(ROOT, p)
        out[p] = sha_file(full) if os.path.exists(full) else "missing"
    return out


def shot_path(rnd, shot):
    return os.path.join(STATE, "shots", str(rnd), f"{shot}.png")


# ---------------------------------------------------------------- init / level

def cmd_init(args):
    os.makedirs(STATE, exist_ok=True)
    ensure_excluded()
    contract = load_contract()
    tree = snapshot()
    counts = test_counts(tree)
    base = {
        "tree": tree,
        "head": git("rev-parse", "--verify", "-q", "HEAD", ok_codes=(0, 1)).strip() or None,
        "level": args.level,
        "level_history": [{"level": args.level, "round": 0, "reason": "init"}],
        "contract_sha": sha_file(os.path.join(STATE, "contract.json")),
        "tests_static": sum(counts.values()),
        "frozen": frozen_files(contract),
    }
    missing = [p for p, h in base["frozen"].items() if h == "missing"]
    if missing:
        raise GateError(f"reference, shots or spec file not found: {', '.join(missing)}")
    save("baseline.json", base)
    print(f"baseline {tree[:12]}  level {args.level}  tests counted {base['tests_static']}  "
          f"contract sha {base['contract_sha']}")
    print(f"forbidden: {', '.join(contract['forbidden'])}")
    if contract["allowed"]:
        print(f"allowed: {', '.join(contract['allowed'])}")
    if base["frozen"]:
        print(f"frozen: {', '.join(base['frozen'])}")
        print("capture round 0 (before any change) into .review-loop/shots/0/<shot>.png")
    return 0


def cmd_level(args):
    base = load("baseline.json")
    base["level_history"].append({"level": args.level, "round": args.round, "reason": args.reason})
    base["level"] = args.level
    save("baseline.json", base)
    print(f"level is now {args.level} (reason: {args.reason})")
    return 0


# ---------------------------------------------------------------- check

def added_lines(base, cur):
    diff = git("diff", "-U0", "--no-color", "--no-ext-diff", base, cur, "--",
               "*.cs", "*.asmdef", "*.asmref", "Packages/manifest.json")
    path, line_no, out = None, 0, []
    for raw in diff.splitlines():
        if raw.startswith("+++ "):
            path = raw[6:] if raw.startswith("+++ b/") else None
        elif raw.startswith("@@"):
            m = re.search(r"\+(\d+)", raw)
            line_no = int(m.group(1)) if m else 0
        elif raw.startswith("+") and path:
            out.append((path, line_no, raw[1:]))
            line_no += 1
    return out


def test_literals(tree, files):
    found = set()
    files = sorted(files)
    for i in range(0, len(files), 80):
        out = git("grep", "-h", "-E", r'"|[0-9]{5,}|[0-9]\.[0-9]{3,}',
                  tree, "--", *files[i:i + 80], ok_codes=(0, 1))
        for line in out.splitlines():
            found |= distinctive_literals(line)
    return found


def run_check(args, write):
    contract = load_contract()
    base = load("baseline.json")
    stop, fail, warn = [], [], []

    if sha_file(os.path.join(STATE, "contract.json")) != base["contract_sha"]:
        stop.append({"id": "contract-changed",
                     "detail": "contract.json changed after init; only the user may change it, then re-init"})
    now = frozen_files(contract)
    for p, h in base.get("frozen", {}).items():
        if now.get(p) != h:
            stop.append({"id": "frozen-changed",
                         "detail": f"{p} changed after init - a moved camera or a new reference makes rounds "
                                   "incomparable; only the user may change it, then re-init"})

    values = {}
    for item in args.value or []:
        name, _, num = item.partition("=")
        try:
            values[name.strip()] = float(num)
        except ValueError:
            raise GateError(f"--value takes name=number, got {item!r}")

    cur = snapshot()
    changed = []
    for line in git("diff", "--name-status", "--no-renames", base["tree"], cur).splitlines():
        status, _, path = line.partition("\t")
        if path:
            changed.append({"status": status, "path": path})

    for c in changed:
        if matches(c["path"], contract["forbidden"]):
            stop.append({"id": "forbidden-path", "detail": f"{c['status']} {c['path']}"})
        elif contract["allowed"] and not matches(c["path"], contract["allowed"]):
            fail.append({"id": "out-of-scope", "detail": f"{c['status']} {c['path']} is outside allowed"})
    if not changed:
        warn.append({"id": "no-changes", "detail": "working tree equals the baseline"})

    counts = test_counts(cur)
    now_tests = sum(counts.values())
    if now_tests < base["tests_static"]:
        entry = {"id": "test-count-dropped", "detail": f"{base['tests_static']} -> {now_tests} test attributes"}
        (warn if contract["tests_may_change"] else fail).append(entry)

    lines = added_lines(base["tree"], cur)
    for path, no, text in lines:
        kind = kind_of(path, counts)
        for pid, rx, where, sev in PATTERNS:
            if where in ("any", kind) and re.search(rx, text):
                (fail if sev == "fail" else warn).append(
                    {"id": pid, "detail": f"{path}:{no}: {text.strip()[:160]}"})

    tests_files = set(counts) | {c["path"] for c in changed if c["path"].endswith(".cs")
                                 and c["status"] != "D" and matches(c["path"], TEST_GLOBS)}
    literals = test_literals(cur, tests_files) if tests_files else set()
    if literals:
        for path, no, text in lines:
            if kind_of(path, counts) != "src":
                continue
            for lit in sorted(distinctive_literals(text) & literals):
                warn.append({"id": "test-literal-in-source",
                             "detail": f"{path}:{no}: literal {lit!r} also appears in a test file"})

    tests = None
    if args.tests_xml:
        root = ET.parse(args.tests_xml).getroot()
        run = root if root.tag == "test-run" else root.find(".//test-run")
        if run is None:
            raise GateError(f"{args.tests_xml} is not an NUnit 3 result file")
        tests = {k: int(run.get(k, 0)) for k in ("total", "passed", "failed", "skipped", "inconclusive")}
        tests["source"] = args.tests_xml
    elif args.tests:
        m = re.fullmatch(r"(\d+)/(\d+)/(\d+)", args.tests)
        if not m:
            raise GateError("--tests takes passed/failed/total, e.g. 12/0/12")
        tests = {"passed": int(m[1]), "failed": int(m[2]), "total": int(m[3]), "source": "orchestrator"}
    if tests:
        if tests["failed"]:
            fail.append({"id": "tests-failed", "detail": f"{tests['failed']} of {tests['total']} failed"})
        if tests.get("skipped") or tests.get("inconclusive"):
            warn.append({"id": "tests-skipped",
                         "detail": f"{tests.get('skipped', 0)} skipped, {tests.get('inconclusive', 0)} inconclusive"})
        if tests["total"] < now_tests:
            warn.append({"id": "tests-partial-run",
                         "detail": f"runner reported {tests['total']} tests, {now_tests} test attributes exist"})
    if args.compile == "errors":
        fail.append({"id": "compile-errors", "detail": "console reports compile errors"})

    patch = git("diff", "--no-color", "--no-ext-diff", "--binary", base["tree"], cur)
    result = {
        "round": args.round, "level": base["level"], "base": base["tree"], "current": cur,
        "changed": changed, "stop": stop, "fail": fail, "warn": warn,
        "tests_static": {"base": base["tests_static"], "now": now_tests},
        "compile": args.compile, "tests": tests, "values": values,
        "diff_sha": hashlib.sha256(patch.encode("utf-8")).hexdigest()[:12],
    }
    if write:
        with open(os.path.join(STATE, "diff.patch"), "w", encoding="utf-8", newline="\n") as f:
            f.write(patch)
        save(f"gate-{args.round}.json", result)
    return result


def kind_of(path, counts):
    if path.endswith((".asmdef", ".asmref")):
        return "asmdef"
    if path.endswith("manifest.json"):
        return "manifest"
    return "test" if is_test_file(path, counts) else "src"


def cmd_check(args):
    r = run_check(args, write=not args.verify)
    print(f"round {r['round']}  level {r['level']}  {len(r['changed'])} files changed  "
          f"tests counted {r['tests_static']['base']} -> {r['tests_static']['now']}  diff sha {r['diff_sha']}")
    for sev in ("stop", "fail", "warn"):
        for item in r[sev]:
            print(f"  {sev.upper():4} {item['id']}: {item['detail']}")
    if not args.verify:
        print(f"wrote .review-loop/gate-{r['round']}.json and .review-loop/diff.patch")
    return 2 if r["stop"] else 1 if r["fail"] else 0


# ---------------------------------------------------------------- pair

def best_round(rnd):
    prev = load(f"verdict-{rnd - 1}.json", required=False) if rnd > 1 else None
    return prev.get("best_round", 0) if prev else 0


def cmd_pair(args):
    contract = load_contract()
    shots = sorted({c["shot"] for c in contract["criteria"] if c["check"] == "visual"})
    if not shots:
        raise GateError("the contract has no visual criteria to pair")
    best = best_round(args.round)
    out = os.path.join(STATE, f"pair-{args.round}")
    shutil.rmtree(out, ignore_errors=True)
    os.makedirs(out)
    rng, mapping = random.SystemRandom(), {}
    for shot in shots:
        sources = {"current": shot_path(args.round, shot), "best": shot_path(best, shot)}
        for who, path in sources.items():
            if not os.path.exists(path):
                raise GateError(f"{who} shot missing: {path}")
        labels = ["current", "best"]
        rng.shuffle(labels)
        mapping[shot] = {"X": labels[0], "Y": labels[1]}
        for label, who in mapping[shot].items():
            shutil.copyfile(sources[who], os.path.join(out, f"{shot}-{label}.png"))
    with open(git_path(f"review-loop-pair-{args.round}.json"), "w", encoding="utf-8") as f:
        json.dump({"round": args.round, "best_round": best, "map": mapping}, f)
    print(f"round {args.round} paired against best round {best} under blind labels:")
    for shot in shots:
        ref = contract["reference"][shot]
        print(f"  {shot}: ref {ref}  X .review-loop/pair-{args.round}/{shot}-X.png  "
              f"Y .review-loop/pair-{args.round}/{shot}-Y.png")
    return 0


# ---------------------------------------------------------------- verdict

def metric_value(name, values, metrics):
    """name is a --value name, or shot.key / shot.region.stat in ref-compare's JSON.
    Returns (current, reference or None, source) or None when it was not measured."""
    if name in values:
        return values[name], None, "orchestrator --value"
    parts = name.split(".")
    try:
        node = metrics[parts[0]]
        if len(parts) == 2:
            return float(node[parts[1]]), None, "ref-compare"
        region = node["regions"][parts[1]]
        if parts[2] == "score":
            return float(region["score"]), None, "ref-compare"
        return float(region["cur"][parts[2]]), float(region["ref"][parts[2]]), "ref-compare"
    except (KeyError, IndexError, TypeError, ValueError):
        return None


def judge_metric(c, measured):
    if measured is None:
        return "unknown", f"{c['metric']} not measured this round"
    cur, ref, src = measured
    ok, parts = True, [f"{c['metric']}={cur:.3g}"]
    if "min" in c:
        ok &= cur >= c["min"]
        parts.append(f"min {c['min']}")
    if "max" in c:
        ok &= cur <= c["max"]
        parts.append(f"max {c['max']}")
    if "ref_tol" in c:
        if ref is None:
            return "unknown", f"{c['metric']} has no reference value for ref_tol"
        ok &= abs(cur - ref) <= c["ref_tol"]
        parts.append(f"ref {ref:.3g} tol {c['ref_tol']}")
    return ("pass" if ok else "fail"), f"{', '.join(parts)} ({src})"

def cmd_verdict(args):
    contract = load_contract()
    base = load("baseline.json")
    level = base["level"]
    gate = load(f"gate-{args.round}.json")
    checks = {c["id"]: c["check"] for c in contract["criteria"]}
    needs_review = any(ch in ("review", "play") for ch in checks.values()) or not is_visual(contract)
    review = load(f"review-{args.round}.json", required=needs_review) if level != "gates" else None
    visual = is_visual(contract)
    judge = load(f"judge-{args.round}.json", required=False) if visual and level != "gates" else None
    judged = {j.get("id"): j for j in (judge or {}).get("criteria", [])}
    pair_file = git_path(f"review-loop-pair-{args.round}.json")
    pair = None
    if os.path.exists(pair_file):
        with open(pair_file, encoding="utf-8") as f:
            pair = json.load(f)
    metrics = load(f"metrics-{args.round}.json", required=False) or {}
    regressions, wins = [], 0

    stop = list(gate["stop"])
    if snapshot() != gate["current"]:
        stop.append({"id": "tree-changed-after-gate",
                     "detail": "files changed between check and verdict (during review?) - re-run check"})
    if sha_file(os.path.join(STATE, "contract.json")) != base["contract_sha"]:
        stop.append({"id": "contract-changed", "detail": "contract.json changed after init"})

    by_id = {c.get("id"): c for c in (review or {}).get("criteria", [])}
    rows, fails, unknown = [], [], []
    for c in contract["criteria"]:
        cid, check = c["id"], c["check"]
        status, evidence = "unknown", ""
        if check == "compile":
            reviewer = (review or {}).get("compile")
            if gate["compile"] == "errors" or reviewer == "errors":
                status, evidence = "fail", "compile errors in console"
            elif gate["compile"] == "ok" and reviewer != "errors":
                status, evidence = "pass", "console clean" + (" (reviewer confirmed)" if reviewer == "ok" else "")
            else:
                evidence = "compile not run"
        elif check == "tests":
            t = gate.get("tests")
            if not t:
                evidence = "tests not run"
            elif t["failed"]:
                status, evidence = "fail", f"{t['failed']}/{t['total']} failed ({t['source']})"
            elif t["total"] > 0:
                status, evidence = "pass", f"{t['passed']}/{t['total']} passed ({t['source']})"
        elif check == "metric":
            status, evidence = judge_metric(c, metric_value(c["metric"], gate.get("values", {}), metrics))
        elif check == "visual":
            j = judged.get(cid)
            if level == "gates":
                evidence = "no judge at level gates"
            elif pair is None:
                evidence = "shots not paired this round (run pair)"
            elif not j:
                evidence = "judge gave no status"
            else:
                labels = pair["map"][c["shot"]]
                mine = "X" if labels["X"] == "current" else "Y"
                status, evidence = j.get(mine, "unknown"), (j.get("evidence") or "").strip()
                if status not in ("pass", "fail", "unknown"):
                    status = "unknown"
                if status == "pass" and not evidence:
                    status, evidence = "unknown", "pass without evidence counts as unknown"
                closer = j.get("closer")
                if closer in ("X", "Y"):
                    if labels[closer] == "best":
                        regressions.append(cid)
                    else:
                        wins += 1
        elif check == "play" and level != "strict":
            evidence = f"not play-tested at level {level}"
        elif level == "gates":
            evidence = "no reviewer at level gates"
        else:
            r = by_id.get(cid)
            if not r:
                evidence = "reviewer gave no status"
            else:
                status, evidence = r.get("status", "unknown"), (r.get("evidence") or "").strip()
                if status == "pass" and not evidence:
                    status, evidence = "unknown", "pass without evidence counts as unknown"
                if status not in ("pass", "fail", "unknown"):
                    status = "unknown"
        rows.append((cid, status, check, evidence))
        (fails if status == "fail" else unknown if status == "unknown" else []).append(cid)

    findings = (review or {}).get("findings", [])
    if level in ("standard", "strict") and any(f.get("refuted") is None for f in findings):
        raise GateError("level needs a refuter: set refuted true/false on every finding in "
                        f"review-{args.round}.json from the refuter's output")
    standing = [f for f in findings if f.get("refuted") is not True]

    if stop:
        status = "STOP"
    elif gate["fail"] or fails or standing or regressions:
        status = "FAIL"
    elif unknown:
        status = "UNVERIFIED"
    else:
        status = "PASS"

    failing_set = sorted(fails + [f"gate:{i['id']}" for i in gate["fail"]] +
                         [f"finding:{f.get('file')}:{f.get('criterion')}" for f in standing])
    prev = load(f"verdict-{args.round - 1}.json", required=False) if args.round > 1 else None
    prev_streaks = (prev or {}).get("streaks", {})
    streaks = {cid: prev_streaks.get(cid, 0) + 1 for cid in fails}
    pivot = sorted(cid for cid, n in streaks.items() if n == 2 and checks[cid] in VISUAL)
    stuck = sorted(cid for cid, n in streaks.items() if n >= 3 and checks[cid] in VISUAL)
    prev_best = pair["best_round"] if pair else best_round(args.round)
    best = args.round if status != "STOP" and not regressions and (wins or pair is None) else prev_best
    if visual:
        no_progress = status == "FAIL" and bool(stuck)
    else:
        no_progress = status == "FAIL" and prev is not None and prev.get("failing_set") == failing_set
    cap = (VISUAL_FIX_ROUNDS if visual else FIX_ROUNDS)[level] + 1

    print(f"review-loop verdict - round {args.round} of {cap}, level {level}")
    print(f"STATUS: {status}")
    for cid, st, check, ev in rows:
        print(f"  {cid:<4} {st:<8} {check:<8} {ev[:200]}")
    if standing:
        print("Blocking findings:")
        for f in standing:
            print(f"  {f.get('file')}:{f.get('line')} [{f.get('criterion')}] {f.get('summary')}")
            print(f"      scenario: {f.get('failure_scenario')}")
    for item in stop:
        print(f"  STOP {item['id']}: {item['detail']}")
    for item in gate["fail"]:
        print(f"  GATE {item['id']}: {item['detail']}")
    for item in gate["warn"]:
        print(f"  warn {item['id']}: {item['detail']}")
    for note in (review or {}).get("integrity", []):
        print(f"  integrity: {note}")
    for note in (review or {}).get("notes", [])[:3]:
        print(f"  note (not blocking): {note}")
    for cid in regressions:
        print(f"  REGRESSION {cid}: round {prev_best} is closer to the reference than this round")
    if regressions:
        old = base["tree"] if prev_best == 0 else load(f"gate-{prev_best}.json")["current"]
        paths = git("diff", "--name-only", old, gate["current"]).split()
        print(f"  restore round {prev_best} (tree {old[:12]}) for these paths before the next change, "
              f"then refresh the Editor: {' '.join(paths[:15])}{' ...' if len(paths) > 15 else ''}")
    for cid in pivot:
        print(f"  PIVOT {cid}: failed two rounds running - retuning values is over for it; the next round "
              "changes the technique or ends BLOCKED")
    if visual:
        print(f"Best round so far: {best}")
        for t in contract.get("triage", []):
            if not t["in_scope"]:
                print(f"  out of scope: {t['element']} ({t['kind']}) - {t['excluded_by']}")
    print(f"Files changed: {len(gate['changed'])}  tests counted "
          f"{gate['tests_static']['base']} -> {gate['tests_static']['now']}  contract sha {base['contract_sha']}")
    if NOT_RUN[level]:
        print(f"Not run at level {level}: {', '.join(NOT_RUN[level])}")
    if len(base["level_history"]) > 1:
        print("Level changes: " + "; ".join(f"round {h['round']} -> {h['level']} ({h['reason']})"
                                           for h in base["level_history"][1:]))
    if status == "FAIL":
        if no_progress and visual:
            print(f"NEXT: no progress - {', '.join(stuck)} failed again after the pivot round. "
                  "Stop and write the stuck report.")
        elif no_progress:
            print("NEXT: no progress - same failures as last round. Stop and write the stuck report.")
        elif args.round >= cap:
            print("NEXT: round cap reached. Stop and write the stuck report.")
        else:
            print(f"NEXT: round {args.round + 1} - send the failing criteria and blocking findings to the implementer.")

    save(f"verdict-{args.round}.json", {
        "round": args.round, "level": level, "status": status, "criteria": rows,
        "standing_findings": standing, "failing_set": failing_set, "no_progress": no_progress,
        "streaks": streaks, "pivot": pivot, "regressions": regressions, "best_round": best,
    })
    return {"PASS": 0, "UNVERIFIED": 0, "FAIL": 1, "STOP": 2}[status]


def main():
    global ROOT, STATE
    for stream in (sys.stdout, sys.stderr):
        stream.reconfigure(encoding="utf-8", errors="replace")
    p = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    sub = p.add_subparsers(dest="cmd", required=True)
    s = sub.add_parser("init")
    s.add_argument("--level", choices=LEVELS, default="standard")
    s = sub.add_parser("level")
    s.add_argument("level", choices=LEVELS)
    s.add_argument("--round", type=int, default=0)
    s.add_argument("--reason", required=True, help="the user's words asking for the change")
    s = sub.add_parser("check")
    s.add_argument("--round", type=int, required=True)
    s.add_argument("--compile", choices=["ok", "errors", "not-run"], default="not-run")
    s.add_argument("--tests", help="passed/failed/total read from the test tool output")
    s.add_argument("--tests-xml", help="NUnit 3 result XML written by the Unity test runner")
    s.add_argument("--verify", action="store_true", help="recompute and print, write nothing")
    s.add_argument("--value", action="append", help="name=number measured in the Editor (particles alive, "
                   "overdraw, draw calls) for metric criteria; repeat for each")
    s = sub.add_parser("pair")
    s.add_argument("--round", type=int, required=True)
    s = sub.add_parser("verdict")
    s.add_argument("--round", type=int, required=True)
    args = p.parse_args()
    try:
        ROOT = find_root()
        STATE = os.path.join(ROOT, ".review-loop")
        handler = {"init": cmd_init, "level": cmd_level, "check": cmd_check, "pair": cmd_pair,
                   "verdict": cmd_verdict}[args.cmd]
        return handler(args)
    except GateError as e:
        print(f"GATE ERROR: {e}", file=sys.stderr)
        return 3
    except Exception as e:  # fail closed
        print(f"GATE ERROR (unexpected): {type(e).__name__}: {e}", file=sys.stderr)
        return 3


if __name__ == "__main__":
    sys.exit(main())
