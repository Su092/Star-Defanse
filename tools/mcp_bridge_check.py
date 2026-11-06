"""用 HTTP 传输验证 Unity MCP 真实链路。

Unity 侧桥接在 http://127.0.0.1:8765/mcp（Streamable HTTP），
而不是 stdio+TCP——stdio 模式发现不到 Unity 实例。
"""
import asyncio
import sys

from mcp import ClientSession
from mcp.client.streamable_http import streamable_http_client

URL = "http://127.0.0.1:8765/mcp"


def dump(label, res):
    text = "".join(getattr(c, "text", "") for c in (res.content or []))
    print(f"--- {label} ---\n{text[:800]}\n")
    return text


async def main():
    async with streamable_http_client(URL) as (read, write):
        async with ClientSession(read, write) as session:
            await asyncio.wait_for(session.initialize(), timeout=60)
            print("initialize OK")

            tools = await session.list_tools()
            names = [t.name for t in tools.tools]
            print(f"tools = {len(names)}")

            ok = 0

            try:
                res = await asyncio.wait_for(
                    session.call_tool("read_console", {"action": "get", "count": 3}),
                    timeout=60)
                text = dump("read_console", res)
                if '"success":true' in text or "success: True" in text or "Log" in text:
                    ok += 1
            except Exception as e:
                print("read_console 失败:", e)

            try:
                res = await asyncio.wait_for(
                    session.call_tool("manage_scene", {"action": "get_active"}),
                    timeout=60)
                text = dump("manage_scene", res)
                if "Main" in text or '"success":true' in text:
                    ok += 1
            except Exception as e:
                print("manage_scene 失败:", e)

            try:
                res = await asyncio.wait_for(
                    session.call_tool("find_gameobjects", {"search_term": "Main"}),
                    timeout=60)
                text = dump("find_gameobjects Main", res)
                if "Main" in text:
                    ok += 1
            except Exception as e:
                print("find_gameobjects 失败:", e)

            print(f"=== 通过 {ok}/3 ===")
            return 0 if ok >= 2 else 1


if __name__ == "__main__":
    sys.exit(asyncio.run(main()))


