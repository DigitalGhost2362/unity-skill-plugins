import json, os, pathlib, sys, urllib.request


def _url():
    """Read the Feeder MCP endpoint from the project's .mcp.json.

    The port differs per Unity host, so it must never be hard-coded here:
    this script is shared by every project through the nbg-unity marketplace.
    """
    env = os.environ.get("FEEDER_MCP_URL")
    if env:
        return env
    root = pathlib.Path(os.environ.get("CLAUDE_PROJECT_DIR", ".")).resolve()
    for d in [root, *root.parents]:
        f = d / ".mcp.json"
        if f.is_file():
            servers = json.loads(f.read_text(encoding="utf-8")).get("mcpServers", {})
            for name, cfg in servers.items():
                if "url" in cfg and "feeder" in name.lower():
                    return cfg["url"]
            for cfg in servers.values():
                if "url" in cfg:
                    return cfg["url"]
    raise SystemExit(
        "No .mcp.json with an http MCP server found from "
        f"{root}. Run from the Unity project root, or set FEEDER_MCP_URL."
    )


URL = _url()
HDRS = {"Content-Type": "application/json", "Accept": "application/json, text/event-stream"}


def _post(payload, sid=None):
    h = dict(HDRS)
    if sid:
        h["Mcp-Session-Id"] = sid
    req = urllib.request.Request(URL, data=json.dumps(payload).encode(), headers=h)
    resp = urllib.request.urlopen(req, timeout=300)
    body = resp.read().decode()
    return resp.headers, body


def _parse(body):
    for line in body.splitlines():
        line = line.strip()
        if line.startswith("data: "):
            line = line[6:]
        if not line or line.startswith("event:"):
            continue
        try:
            return json.loads(line)
        except json.JSONDecodeError:
            continue
    return None


def session():
    h, b = _post({"jsonrpc": "2.0", "id": 1, "method": "initialize",
                  "params": {"protocolVersion": "2024-11-05", "capabilities": {},
                             "clientInfo": {"name": "cc", "version": "1.0"}}})
    sid = h.get("Mcp-Session-Id")
    _post({"jsonrpc": "2.0", "method": "notifications/initialized"}, sid)
    return sid


def call(tool, args, sid=None):
    sid = sid or session()
    h, b = _post({"jsonrpc": "2.0", "id": 2, "method": "tools/call",
                  "params": {"name": tool, "arguments": args}}, sid)
    d = _parse(b)
    if d is None:
        return b
    if "result" in d and "content" in d["result"]:
        return "\n".join(c.get("text", "") for c in d["result"]["content"])
    return json.dumps(d, indent=1)


def run_csharp(body, cls="AvatarBuild", method="Main"):
    return call("script-execute", {
        "csharpCode": body,
        "className": cls,
        "methodName": method,
        "isMethodBody": True,
    })


if __name__ == "__main__":
    tool = sys.argv[1]
    args = json.load(open(sys.argv[2])) if len(sys.argv) > 2 else {}
    print(call(tool, args))
