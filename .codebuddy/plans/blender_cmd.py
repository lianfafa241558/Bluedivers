"""直接通过 BlenderMCP addon 的 9876 socket 驱动 Blender，不需要任何 MCP 客户端。

addon 协议（见 .codebuddy/mcp/blender/addon.py:197-342）：
  请求 = 单个 JSON {"type": ..., "params": {...}}，连接持久，一条连接可连发多条
  响应 = 单个 JSON {"status": "success"|"error", "result"|"message": ...}，无分隔符

用法：
  python blender_cmd.py scene                  # 读场景概要
  python blender_cmd.py obj Cube               # 读某个物体
  python blender_cmd.py shot out.png [max]     # 视口截图
  python blender_cmd.py py path/to/code.py     # 在 Blender 里跑一段 Python
  python blender_cmd.py raw '{"type":"get_polyhaven_status","params":{}}'
"""
import json
import socket
import sys
import os

HOST = os.environ.get("BLENDER_HOST", "127.0.0.1")
PORT = int(os.environ.get("BLENDER_PORT", "9876"))


def send(cmd, timeout=180):
    """发一条命令，返回已解析的响应。响应可能被拆包，故循环拼接收直到 JSON 完整。"""
    sock = socket.create_connection((HOST, PORT), timeout=10)
    sock.settimeout(timeout)
    buf = b""
    try:
        sock.sendall(json.dumps(cmd).encode("utf-8"))
        while True:
            chunk = sock.recv(65536)
            if not chunk:
                break
            buf += chunk
            try:
                return json.loads(buf.decode("utf-8"))
            except json.JSONDecodeError:
                continue  # 还没收完
    finally:
        sock.close()
    raise RuntimeError("addon 关闭了连接但没有返回可解析的 JSON：%r" % buf[:200])


def dump(obj, limit=6000):
    text = json.dumps(obj, ensure_ascii=False, indent=2)
    print(text if len(text) <= limit else text[:limit] + "\n... (截断)")


def main(argv):
    if len(argv) < 2:
        print(__doc__)
        return 1

    action = argv[1]
    if action == "scene":
        dump(send({"type": "get_scene_info", "params": {}}))
    elif action == "obj":
        dump(send({"type": "get_object_info", "params": {"name": argv[2]}}))
    elif action == "shot":
        params = {"filepath": argv[2]}
        if len(argv) > 3:
            params["max_size"] = int(argv[3])
        dump(send({"type": "get_viewport_screenshot", "params": params}))
    elif action == "py":
        with open(argv[2], "r", encoding="utf-8") as f:
            code = f.read()
        dump(send({"type": "execute_code", "params": {"code": code}}, timeout=600))
    elif action == "raw":
        dump(send(json.loads(argv[2])), limit=20000)
    else:
        print(__doc__)
        return 1
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv))
