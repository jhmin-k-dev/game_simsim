"""mcp_call과 동일하지만 인자를 JSON 파일에서 읽는다 (긴 C# 코드용).
사용: python mcp_call_file.py <tool_name> <args.json>
"""
import json, sys
from mcp_call import post

def main():
    sid, init = post({
        "jsonrpc": "2.0", "id": 1, "method": "initialize",
        "params": {"protocolVersion": "2025-03-26", "capabilities": {},
                   "clientInfo": {"name": "claude-cli-bridge", "version": "1.0"}}
    })
    post({"jsonrpc": "2.0", "method": "notifications/initialized"}, sid)
    tool = sys.argv[1]
    with open(sys.argv[2], encoding="utf-8") as f:
        args = json.load(f)
    _, res = post({"jsonrpc": "2.0", "id": 3, "method": "tools/call",
                   "params": {"name": tool, "arguments": args}}, sid)
    content = res.get("result", {}).get("content", [])
    for c in content:
        print(c.get("text", ""))

if __name__ == "__main__":
    main()
