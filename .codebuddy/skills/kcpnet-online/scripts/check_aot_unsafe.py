#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
AOT（IL2CPP）地雷自检 —— 扫 MessagePack 动态生成 / 动态代码生成的**真调用**。

为什么要它
    MessagePack 的 `MessagePackSerializer` 默认走 `StandardResolver → DynamicObjectResolver`，
    后者用 `System.Reflection.Emit` **现造 formatter**；IL2CPP **不支持动态代码生成** ⇒
    打包版一发消息就抛 `PlatformNotSupportedException: Operation is not supported on this platform`
    （`AssemblyBuilder.DefineDynamicAssembly`）。**编辑器走 Mono，完全看不出来**。
    本项目已改用 `02_Net` 的 `NetMsgCodec`（只用 `MessagePackWriter/Reader` + 自写反射读写），所以：

      · `MessagePackSerializer` / `DynamicObjectResolver` / `MessagePack.Resolvers` 的**真调用必须为 0**；
      · 它们出现在**注释**里是**正常**的（代码注释就写着"为什么不用它"）；
      · `[MessagePackObject]` / `[Key(n)]` 特性**必须保留**（`NetMsgCodec` 反射读 `[Key]` 决定 array 顺序）
        ⇒ 不算违规，**别去"清理"** `07_NetGame` 对 MessagePack 的依赖；
      · `MessagePackWriter` / `MessagePackReader` 只允许出现在传输层（`02_Net` / 目录名 `NetTmp`）——
        编解码是传输层的职责，业务层不该直接碰字节。

用法
    python check_aot_unsafe.py                 # 扫 Assets/Scripts（默认）
    python check_aot_unsafe.py --root <目录>    # 换扫描根（可多次）
    python check_aot_unsafe.py -v              # 连"只在注释里"的命中一起列出
    python check_aot_unsafe.py --quiet         # 只打印结论
    退出码：0 = 干净；1 = 有真代码命中或分层违规（可直接接进 CI / pre-commit）
"""
import argparse
import os
import sys

# 致命：真代码命中即打包版崩
FORBIDDEN_FATAL = (
    "MessagePackSerializer",
    "DynamicObjectResolver",
    "MessagePack.Resolvers",
    "GeneratedMessagePackResolver",
)
# 仅供参考：动态代码生成（运行时项目里通常也没必要，但可能有合法用途 ⇒ 不判失败）
FORBIDDEN_SOFT = ("AssemblyBuilder", "ILGenerator", "Reflection.Emit")
# 只允许出现在传输层的类型
TRANSPORT_ONLY = ("MessagePackWriter", "MessagePackReader")
TRANSPORT_DIR_HINT = "NetTmp"      # 02_Net 的目录名（见 references/nettmp-layer.md §0）
SKIP_DIRS = {"obj", "bin", "Library", "Temp", ".git", "node_modules"}


def strip_comment(line, in_block):
    """返回 (去掉注释后的代码, 行尾时是否仍在块注释里)。字符串字面量里的 // 不当注释。"""
    out, i, n = [], 0, len(line)
    while i < n:
        c = line[i]
        if in_block:
            if c == "*" and i + 1 < n and line[i + 1] == "/":
                in_block = False
                i += 2
                continue
            i += 1
            continue
        if c in ('"', "'"):
            quote = c
            out.append(c)
            i += 1
            while i < n:
                if line[i] == "\\" and i + 1 < n:
                    out.append(line[i]); out.append(line[i + 1]); i += 2
                    continue
                out.append(line[i])
                if line[i] == quote:
                    i += 1
                    break
                i += 1
            continue
        if c == "/" and i + 1 < n and line[i + 1] == "/":
            break
        if c == "/" and i + 1 < n and line[i + 1] == "*":
            in_block = True
            i += 2
            continue
        out.append(c)
        i += 1
    return "".join(out), in_block


def iter_cs_files(root):
    for dirpath, dirnames, filenames in os.walk(root):
        dirnames[:] = [d for d in dirnames if d not in SKIP_DIRS]
        for fn in filenames:
            if fn.endswith(".cs"):
                yield os.path.join(dirpath, fn)


def scan_file(path, patterns):
    """逐行扫；返回 [(行号, 该行去注释后的代码, 是否真代码)]，只含至少匹配一个 pattern 的行。"""
    hits = []
    in_block = False
    try:
        with open(path, "r", encoding="utf-8", errors="replace") as f:
            for lineno, raw in enumerate(f, 1):
                code, in_block = strip_comment(raw.rstrip("\n"), in_block)
                code = code.strip()
                raw_stripped = raw.strip()
                if not any(p in raw for p in patterns):
                    continue
                real = bool(code) and any(p in code for p in patterns)
                hits.append((lineno, raw_stripped if not real else code, real))
    except OSError as e:
        print(f"  !! 读不了 {path}: {e}")
    return hits


def main():
    try:
        sys.stdout.reconfigure(encoding="utf-8")
    except Exception:
        pass

    ap = argparse.ArgumentParser(description="AOT 地雷自检（MessagePack 动态生成 / Reflection.Emit 真调用）")
    ap.add_argument("--root", action="append", default=None,
                    help="扫描根（可多次；默认 Assets/Scripts，相对项目根）")
    ap.add_argument("-v", "--verbose", action="store_true", help="连'只在注释里'的命中一起列出")
    ap.add_argument("--quiet", action="store_true", help="只打印结论")
    args = ap.parse_args()

    proj = os.getcwd()
    roots = args.root or ["Assets/Scripts"]
    print("== AOT 地雷自检 ==")
    print(f"项目根: {proj}")
    print(f"扫描根: {', '.join(roots)}")

    fatal, soft, transport, comment_only = [], [], [], []
    scanned = 0
    for root in roots:
        if not os.path.isdir(root):
            print(f"  !! 目录不存在: {root}（要从项目根运行）")
            continue
        for path in iter_cs_files(root):
            scanned += 1
            rel = os.path.relpath(path, proj)
            for lineno, text, real in scan_file(path, FORBIDDEN_FATAL + FORBIDDEN_SOFT + TRANSPORT_ONLY):
                item = (rel, lineno, text)
                is_fatal = any(p in text for p in FORBIDDEN_FATAL)
                is_soft = any(p in text for p in FORBIDDEN_SOFT)
                is_transport = any(p in text for p in TRANSPORT_ONLY)
                if not real:
                    comment_only.append(item)
                    continue
                if is_fatal:
                    fatal.append(item)
                elif is_transport and TRANSPORT_DIR_HINT not in rel.replace("/", os.sep):
                    transport.append(item)
                elif is_transport or is_soft:
                    soft.append(item)

    if not args.quiet:
        if args.verbose and comment_only:
            print(f"\n-- 只在注释里出现（正常，列出仅供参考）: {len(comment_only)} --")
            for rel, lineno, text in comment_only:
                print(f"   {rel}:{lineno}  {text[:100]}")
        if soft:
            print(f"\n-- 参考（动态代码生成 / 传输层内的合法用法）: {len(soft)} --")
            for rel, lineno, text in soft:
                print(f"   {rel}:{lineno}  {text[:100]}")

    print()
    print(f"扫描 {scanned} 个 .cs")
    print(f"致命命中（{ '、'.join(FORBIDDEN_FATAL) }）= {len(fatal)}    ← 必须为 0")
    for rel, lineno, text in fatal:
        print(f"   !! {rel}:{lineno}  {text[:120]}")
    print(f"分层违规（{ '、'.join(TRANSPORT_ONLY) } 出现在传输层之外）= {len(transport)}    ← 必须为 0")
    for rel, lineno, text in transport:
        print(f"   !! {rel}:{lineno}  {text[:120]}")
    print(f"只在注释里出现 = {len(comment_only)}（正常）")

    if fatal or transport:
        print("\nVERDICT: 不通过 —— 真代码里出现了 AOT 不安全 / 越层的用法（打包版可能崩，先改掉再打包）。")
        return 1
    print("\nVERDICT: 通过 —— 没有真调用；MessagePack 只作为 DTO 特性 + NetMsgCodec 的 Writer/Reader 存在。")
    return 0


if __name__ == "__main__":
    sys.exit(main())
