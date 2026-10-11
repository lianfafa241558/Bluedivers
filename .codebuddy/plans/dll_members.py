# -*- coding: utf-8 -*-
"""只读：用 dnfile 静态解析 **Unity 编译产物**（Library/ScriptAssemblies/*.dll）里的类型成员。

用途：MCP 掉线 / 不想触发域重载时，替代 `execute_code` 的"反射核验"——
      Console 0 error 有"假空转/假缺失 meta"两种假象，成员到底有没有编进 dll，看这里最实。

跑法：
    python -X utf8 .codebuddy/plans/dll_members.py 05_UnitCore:FPSGame.Game.ActorsManager
    python -X utf8 .codebuddy/plans/dll_members.py 09_Managers:FPSGame.Managers.WaveManager FPSGame.Managers.BattleManager
    （第二段起可省略程序集 —— 省略时在全部 dll 里找）

只打印「字段名 / 方法名(重载计数)」，不求签名 —— 重载数量够判断"新重载有没有落地"，
参数类型由离线编译（offline_compile.py）把关。
"""
import os
import sys
from collections import Counter

import dnfile

sys.stdout.reconfigure(encoding="utf-8", errors="replace")

ROOT = os.environ.get("BLUEDIVERS_ROOT") or os.path.abspath(
    os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", ".."))
SA = os.path.join(ROOT, "Library", "ScriptAssemblies")


def dump(path, want, only_member=None):
    pe = dnfile.dnPE(path)
    md = pe.net.mdtables
    for td in md.TypeDef:
        ns = str(td.TypeNamespace or "")
        nm = str(td.TypeName or "")
        full = (ns + "." + nm) if ns else nm
        if full != want:
            continue

        fields = []
        if td.FieldList:
            for row in td.FieldList:
                if row.row is None:
                    continue
                fields.append(str(row.row.Name))
        methods = Counter()
        if td.MethodList:
            for row in td.MethodList:
                if row.row is None:
                    continue
                methods[str(row.row.Name)] += 1

        print("== %s  @ %s" % (full, os.path.basename(path)))
        if only_member:
            hit = [m for m in methods if m == only_member]
            print("   方法 %s : %s (重载数=%d)" % (
                only_member, "存在" if hit else "**不存在**", methods.get(only_member, 0)))
            print("   字段 %s : %s" % (
                only_member, "存在" if only_member in fields else "不存在"))
            return True

        print("   [字段] " + (", ".join(fields) if fields else "<无>"))
        print("   [方法] " + (", ".join("%s%s" % (k, ("x%d" % v) if v > 1 else "")
                                        for k, v in methods.items()) if methods else "<无>"))
        return True
    return False


def main():
    args = sys.argv[1:]
    if not args:
        print(__doc__)
        return 1

    dlls = {}
    for f in sorted(os.listdir(SA)):
        if f.endswith(".dll"):
            dlls[f[:-4]] = os.path.join(SA, f)

    ok = True
    for a in args:
        if ":" in a:
            asm, want = a.split(":", 1)
        else:
            want = a
            asm = None

        found = False
        # asm 允许写成路径（如 Temp/offline_compile/07_NetGame）⇒ 在 Unity 还没重编译时用离线产物核验
        if asm and ("/" in asm or "\\" in asm):
            p = asm if asm.endswith(".dll") else asm + ".dll"
            p = p if os.path.isabs(p) else os.path.join(ROOT, p)
            if os.path.exists(p) and dump(p, want):
                found = True
        else:
            names = [asm] if asm else list(dlls)
            for n in names:
                p = dlls.get(n)
                if p and dump(p, want):
                    found = True
                    break
        if not found:
            print("== %s  **未找到**（程序集=%s）" % (want, asm or "全部"))
            ok = False
    return 0 if ok else 1


if __name__ == "__main__":
    sys.exit(main())
