# -*- coding: utf-8 -*-
"""轮询 Unity 编译产物：等待 ProjectEditors.dll 更新并包含 DDbg 标记"""
import os
import time

DLL = r"E:\Bluedivers\Library\ScriptAssemblies\ProjectEditors.dll"


def dll_state():
    mtime = os.path.getmtime(DLL)
    with open(DLL, "rb") as fp:
        data = fp.read()
    has_dbg = b"D\x00D\x00b\x00d\x00g\x00" in data or b"DDbg" in data
    return mtime, has_dbg, len(data)


last = 0
for i in range(20):
    mtime, has_dbg, size = dll_state()
    print(f"poll{i}: mtime={time.strftime('%H:%M:%S', time.localtime(mtime))} "
          f"size={size} hasDDbg={has_dbg}", flush=True)
    if has_dbg:
        print("COMPILED")
        break
    time.sleep(4)
else:
    print("TIMEOUT: dll 仍未更新")
