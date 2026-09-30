#!/usr/bin/env python3
"""Static .NET metadata inspector for the precompiled KCPNet plugin DLLs.

Replaces the old PowerShell reflection script: reads the DLL's CLI metadata with the
pure-Python `dnfile` package instead of loading the assembly, so it works even when
dependencies are missing or incompatible (which is exactly when you need it).

It prints:
  * target framework / assembly identity (name + version)
  * AssemblyRef table  -> the dependency matrix Unity must be able to resolve
  * TypeDef table      -> the public type surface of the library
  * MethodDef names grouped by declaring type (best effort)

For full signatures (parameter types, optional/default values) and for *runtime*
verification, use Unity MCP instead - the assemblies are loaded in the editor now:
  unity_reflect(action="get_type", class_name="KCPNet.NetConfig")
  unity_reflect(action="search", query="Lan", scope="project")
  execute_code(code="return typeof(KCPNet.NetConfig).GetFields(...)...")
Unity's reflection is the authoritative source and needs no extra tooling; this script
is the offline / no-editor fallback and the fastest way to read the dependency matrix.

One-time setup:
    python -m pip install dnfile

Usage:
    python inspect_dll.py                          # default: Assets/Plugins/KCPNet/*.dll
    python inspect_dll.py <dll-or-dir> [more...]
    python inspect_dll.py --refs-only              # dependency matrix only (fast, greppable)
    python inspect_dll.py --types-only
"""

from __future__ import annotations

import os
import sys

DEFAULT_DIR = r"d:\Pro\Bluedivers\Assets\Plugins\KCPNet"

try:
    import dnfile
except ImportError:
    print("!! missing dependency: run  python -m pip install dnfile")
    sys.exit(2)


def as_text(value) -> str:
    """dnfile exposes names as dnString wrappers; unwrap defensively across versions."""
    if value is None:
        return ""
    for attr in ("value", "string", "name"):
        inner = getattr(value, attr, None)
        if isinstance(inner, str):
            return inner
    return str(value)


def rows(table) -> list:
    if table is None:
        return []
    return list(getattr(table, "rows", []) or [])


def assembly_identity(pe) -> str:
    try:
        table = pe.net.mdtables.Assembly
        if table is not None and len(table.rows) > 0:
            a = table.rows[0]
            ver = f"{a.MajorVersion}.{a.MinorVersion}.{a.BuildNumber}.{a.RevisionNumber}"
            return f"{as_text(a.Name)} {ver}"
    except Exception as e:  # pragma: no cover - defensive
        return f"<assembly row unreadable: {e}>"
    return "<no Assembly row>"


def assembly_refs(pe) -> list[str]:
    out = []
    for r in rows(pe.net.mdtables.AssemblyRef):
        ver = f"{r.MajorVersion}.{r.MinorVersion}.{r.BuildNumber}.{r.RevisionNumber}"
        out.append(f"{as_text(r.Name)} {ver}")
    return sorted(out)


def type_defs(pe) -> list[str]:
    out = []
    for t in rows(pe.net.mdtables.TypeDef):
        ns = as_text(t.TypeNamespace)
        name = as_text(t.TypeName)
        out.append(f"{ns}.{name}" if ns else name)
    return sorted(out)


def methods_by_type(pe) -> dict[str, list[str]]:
    """Map TypeDef -> MethodDef names via the MethodList column ranges (best effort)."""
    result: dict[str, list[str]] = {}
    tdefs = rows(pe.net.mdtables.TypeDef)
    mdefs = rows(pe.net.mdtables.MethodDef)
    if not tdefs or not mdefs:
        return result

    def method_index(row) -> int | None:
        col = getattr(row, "MethodList", None)
        if col is None:
            return None
        for attr in ("row_index", "value", "row"):
            v = getattr(col, attr, None)
            if isinstance(v, int):
                return v
        if isinstance(col, int):
            return col
        return None

    starts: list[int] = []
    for t in tdefs:
        idx = method_index(t)
        if idx is None:
            return {}
        starts.append(idx)
    starts.append(len(mdefs) + 1)  # sentinel: MethodDef table is 1-based

    for i, t in enumerate(tdefs):
        ns = as_text(t.TypeNamespace)
        name = as_text(t.TypeName)
        full = f"{ns}.{name}" if ns else name
        lo, hi = starts[i], starts[i + 1]
        names = []
        for rid in range(lo, min(hi, len(mdefs) + 1)):
            row = mdefs[rid - 1]
            names.append(as_text(row.Name))
        if names:
            result[full] = names
    return result


def report(path: str, show_refs: bool, show_types: bool, show_methods: bool) -> None:
    print(f"===== {os.path.basename(path)}   ({os.path.getsize(path)} bytes)")
    try:
        pe = dnfile.dnPE(path)
    except Exception as e:
        print(f"  !! cannot parse: {e}")
        return

    if pe.net is None:
        print("  !! not a managed assembly (no CLI header)")
        return

    print(f"  identity: {assembly_identity(pe)}")

    if show_refs:
        refs = assembly_refs(pe)
        print(f"  -- AssemblyRef ({len(refs)}) : dependency matrix --")
        for r in refs:
            print(f"     {r}")

    if show_types:
        types = type_defs(pe)
        print(f"  -- TypeDef ({len(types)}) --")
        for t in types:
            if t.startswith("<"):
                continue
            print(f"     {t}")

    if show_methods:
        mbt = methods_by_type(pe)
        if mbt:
            print("  -- MethodDef (name only, grouped by type) --")
            for t in sorted(mbt):
                print(f"     {t}: {', '.join(mbt[t])}")
        else:
            print("  -- MethodDef: skipped (MethodList column not introspectable) --")

    print("")


def collect_targets(args: list[str]) -> list[str]:
    paths: list[str] = []
    for a in args:
        if os.path.isdir(a):
            paths += [
                os.path.join(a, f)
                for f in sorted(os.listdir(a))
                if f.lower().endswith(".dll")
            ]
        else:
            paths.append(a)
    return paths


def main() -> int:
    argv = sys.argv[1:]
    flags = {a for a in argv if a.startswith("--")}
    targets = [a for a in argv if not a.startswith("--")]

    refs_only = "--refs-only" in flags
    types_only = "--types-only" in flags
    show_refs = not types_only
    show_types = not refs_only
    show_methods = not refs_only and not types_only

    if not targets:
        targets = [DEFAULT_DIR]

    paths = collect_targets(targets)
    if not paths:
        print(f"!! nothing to inspect in {targets}")
        return 1

    for p in paths:
        if not os.path.exists(p):
            print(f"!! not found: {p}")
            continue
        report(p, show_refs=show_refs, show_types=show_types, show_methods=show_methods)

    print("tip: full signatures / runtime check -> Unity MCP")
    print('     unity_reflect(action="get_type", class_name="KCPNet.KCPNet`2")')
    print('     unity_reflect(action="search", query="Lan", scope="project")')
    return 0


if __name__ == "__main__":
    sys.exit(main())
