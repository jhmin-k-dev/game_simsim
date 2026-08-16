"""C# 파일을 Unity 에디터에서 실행 (MCP execute_code).
사용: python unity_exec.py <script.cs>
"""
import json, sys, io
from mcp_call import post

def main():
    sid, _ = post({
        "jsonrpc": "2.0", "id": 1, "method": "initialize",
        "params": {"protocolVersion": "2025-03-26", "capabilities": {},
                   "clientInfo": {"name": "claude-cli-bridge", "version": "1.0"}}
    })
    post({"jsonrpc": "2.0", "method": "notifications/initialized"}, sid)
    with io.open(sys.argv[1], encoding="utf-8") as f:
        code = f.read()
    _, res = post({"jsonrpc": "2.0", "id": 3, "method": "tools/call",
                   "params": {"name": "execute_code",
                              "arguments": {"action": "execute", "code": code}}}, sid)
    for c in res.get("result", {}).get("content", []):
        text = c.get("text", "")
        try:
            obj = json.loads(text)
            if isinstance(obj, dict):
                if obj.get("data") and isinstance(obj["data"], dict) and "result" in obj["data"]:
                    print(obj["data"]["result"]); continue
                print(json.dumps(obj, ensure_ascii=False, indent=1))
                continue
        except json.JSONDecodeError:
            pass
        print(text)

if __name__ == "__main__":
    main()
