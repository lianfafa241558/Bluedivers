"""只读：用 dnfile 静态解析 KCPNet.dll 的指定类型，打印字段 / 方法 / 是否 sealed。

用途：确认 `LanRoomInfo` 是否可被项目侧继承/扩展、有哪些公开字段，
决定「加难度字段」该改库还是走项目侧包装。

跑法：python -X utf8 .codebuddy/plans/dll_type_dump.py KCPNet.LanRoomInfo
"""
import sys

import dnfile

sys.stdout.reconfigure(encoding="utf-8", errors="replace")

DLL = r"d:/Pro/Bluedivers/Assets/Plugins/KCPNet/KCPNet.dll"


def flags_of(td):
    f = getattr(td, "Flags", None) or []
    try:
        return " ".join(sorted(str(x) for x in f))
    except Exception:
        return str(f)


def main():
    want = sys.argv[1] if len(sys.argv) > 1 else "KCPNet.LanRoomInfo"
    pe = dnfile.dnPE(DLL)
    md = pe.net.mdtables
    for td in md.TypeDef:
        name = str(td.TypeNamespace) + "." + str(td.TypeName)
        if name != want:
            continue
        print("类型:", name)
        print("Flags:", flags_of(td))
        field_count = getattr(td, "FieldList", None)
        nf = 0
        print("\n[字段]")
        if td.FieldList:
            for row in td.FieldList:
                if row.row is None:
                    continue
                attrs = " ".join(sorted(str(a) for a in (row.row.Flags or [])))
                print(f"  {row.row.Name}   ({attrs})")
                nf += 1
        print(f"  (共 {nf})")
        print("\n[方法]")
        if td.MethodList:
            for row in td.MethodList:
                if row.row is None:
                    continue
                print(f"  {row.row.Name}")
        return
    print("未找到类型:", want)


if __name__ == "__main__":
    main()
