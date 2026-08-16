"""Unity MCP 서버(HTTP, localhost:8080/mcp)에 JSON-RPC 호출.
사용: python mcp_call.py <tool_name> '<json_args>'
     python mcp_call.py --list          (도구 목록)
"""
import json, sys, urllib.request

BASE = "http://localhost:8080/mcp"
HEADERS = {"Content-Type": "application/json", "Accept": "application/json, text/event-stream"}

def post(payload, session=None):
    h = dict(HEADERS)
    if session:
        h["mcp-session-id"] = session
    req = urllib.request.Request(BASE, json.dumps(payload).encode(), h, method="POST")
    with urllib.request.urlopen(req, timeout=120) as r:
        sid = r.headers.get("mcp-session-id", session)
        body = r.read().decode("utf-8", "replace")
    # SSE 형식이면 data: 라인만 추출
    datas = []
    for line in body.splitlines():
        if line.startswith("data:"):
            datas.append(line[5:].strip())
    if datas:
        body = datas[-1]
    try:
        return sid, json.loads(body) if body.strip() else None
    except json.JSONDecodeError:
        return sid, {"raw": body[:2000]}

def main():
    sid, init = post({
        "jsonrpc": "2.0", "id": 1, "method": "initialize",
        "params": {"protocolVersion": "2025-03-26",
                   "capabilities": {},
                   "clientInfo": {"name": "claude-cli-bridge", "version": "1.0"}}
    })
    if init is None or "error" in (init or {}):
        print(json.dumps(init, ensure_ascii=False)); sys.exit(1)
    post({"jsonrpc": "2.0", "method": "notifications/initialized"}, sid)

    if sys.argv[1] == "--list":
        _, res = post({"jsonrpc": "2.0", "id": 2, "method": "tools/list"}, sid)
        tools = res.get("result", {}).get("tools", [])
        for t in tools:
            print(t["name"], "-", t.get("description", "")[:100])
        return

    tool = sys.argv[1]
    args = json.loads(sys.argv[2]) if len(sys.argv) > 2 else {}
    _, res = post({"jsonrpc": "2.0", "id": 3, "method": "tools/call",
                   "params": {"name": tool, "arguments": args}}, sid)
    print(json.dumps(res, ensure_ascii=False, indent=1)[:6000])

if __name__ == "__main__":
    main()
