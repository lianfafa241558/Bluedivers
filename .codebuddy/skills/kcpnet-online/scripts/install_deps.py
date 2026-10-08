#!/usr/bin/env python3
"""Install / verify the NuGet managed DLLs required by the precompiled KCPNet plugin.

Why this script exists
----------------------
KCPNet ships as a closed-source DLL bundle (Assets/Plugins/KCPNet/KCPNet.dll +
Kcp.dll). Removing the dependency closure makes the WHOLE project fail to compile
with CS0246 on [MessagePackObject] / [Key] (see NetTmp/Services/Msg/*.cs), and -
worse - makes Unity refuse to load the plugins at all ("will not be loaded due to
errors: Unable to resolve reference 'X'").

Default targets = the verified, complete closure (2026-09-30, all installed and
confirmed working in the editor + at runtime):

    MessagePack                            3.1.8    (lib/netstandard2.0 asset)
    MessagePack.Annotations                3.1.8
    Microsoft.Bcl.AsyncInterfaces          8.0.0    ┐
    Microsoft.NET.StringTools              17.11.4  │ AssemblyRef of MessagePack.dll
    System.Collections.Immutable           8.0.0    │ - one missing breaks the chain
    System.Runtime.CompilerServices.Unsafe 6.0.0    ┘

Do NOT add these: Unity already provides them, and a duplicate precompiled
assembly with the same name is an error:
    System.Memory (Unity ships 4.0.99.0, satisfying KCPNet.dll's 4.0.1.2 ref),
    System.Buffers, System.Numerics.Vectors, System.Reflection.Emit(+ILGeneration,
    +Lightweight), System.Threading.Tasks.Extensions, netstandard.

Usage
-----
    python install_deps.py                 # install the full default closure
    python install_deps.py --check         # read-only status: what is present/missing
    python install_deps.py --force         # re-download even if the DLL already exists
    python install_deps.py --dry-run       # only list what each nupkg contains
    python install_deps.py --package MessagePack:3.1.9   # extra/ad-hoc Id:Version
    python install_deps.py --dest <dir>    # override destination folder

Notes
-----
* Only `lib/<tfm>/*.dll` is extracted, into Assets/Plugins/KCPNet (which has NO
  asmdef, so the DLLs land in the default plugin scope = all platforms).
* TFM preference is netstandard2.0 first. The project's API compatibility level is
  NET_Standard_2_0, so the netstandard2.0 assets are required; the netstandard2.1
  assets (MessagePack ships one with fewer deps) would NOT load. net6.0/net8.0
  assets are never usable in Unity.
* Unity does not auto-import files written by an external process: call Unity MCP
  `refresh_unity(mode=force, scope=all, compile=request)` afterwards, then verify:
  read_console -> 0 error, execute_code -> assemblies loaded + MessagePack roundtrip,
  and if a plugin still fails, read the reason from
  %LOCALAPPDATA%\\Unity\\Editor\\Editor.log  ("Unable to resolve reference 'X'").
"""

from __future__ import annotations

import argparse
import io
import os
import shutil
import sys
import urllib.error
import urllib.request
import zipfile

DEFAULT_DEST = r"d:\Pro\Bluedivers\Assets\Plugins\KCPNet"
DEFAULT_PACKAGES = [
    "MessagePack:3.1.8",
    "MessagePack.Annotations:3.1.8",
    "Microsoft.Bcl.AsyncInterfaces:8.0.0",
    "Microsoft.NET.StringTools:17.11.4",
    "System.Collections.Immutable:8.0.0",
    "System.Runtime.CompilerServices.Unsafe:6.0.0",
]
# Provided by Unity itself - installing them causes duplicate-assembly errors.
UNITY_PROVIDED = [
    "System.Memory",
    "System.Buffers",
    "System.Numerics.Vectors",
    "System.Reflection.Emit",
    "System.Threading.Tasks.Extensions",
]

# Ordered by preference. Anything outside this list is only reported, never picked.
TFM_PREFERENCE = [
    "lib/netstandard2.0/",
    "lib/netstandard2.1/",
    "lib/net472/",
    "lib/net461/",
    "lib/net6.0/",
    "lib/net8.0/",
]
FLAT_CONTAINER = "https://api.nuget.org/v3-flatcontainer/{id}/{version}/{id}.{version}.nupkg"


def log(msg: str) -> None:
    print(msg, flush=True)


def download_nupkg(pkg_id: str, version: str) -> bytes:
    low_id = pkg_id.lower()
    url = FLAT_CONTAINER.format(id=low_id, version=version)
    log(f"  download: {url}")
    req = urllib.request.Request(url, headers={"User-Agent": "Bluedivers-kcpnet-installer"})
    with urllib.request.urlopen(req, timeout=120) as resp:
        return resp.read()


def pick_asset(names: list[str]) -> tuple[str | None, list[str]]:
    """Return (chosen_dll_entry, all_dll_entries)."""
    dlls = [n for n in names if n.lower().endswith(".dll")]
    for prefix in TFM_PREFERENCE:
        for n in sorted(dlls):
            if n.startswith(prefix):
                return n, dlls
    return None, dlls


def install_package(dest: str, pkg: str, force: bool, dry_run: bool) -> bool:
    pkg_id, _, version = pkg.partition(":")
    if not version:
        log(f"!! bad --package '{pkg}' (expected Id:Version)")
        return False

    log(f"[{pkg_id} {version}]")
    try:
        raw = download_nupkg(pkg_id, version)
    except urllib.error.URLError as e:
        log(f"!! download failed: {e}")
        return False

    with zipfile.ZipFile(io.BytesIO(raw)) as zf:
        names = zf.namelist()
        entry, all_dlls = pick_asset(names)
        if all_dlls:
            log("  dlls inside nupkg:")
            for n in sorted(all_dlls):
                log(f"    - {n}")
        if entry is None:
            log("  !! no usable lib/*.dll found (unexpected layout)")
            return False

        file_name = os.path.basename(entry)
        out_path = os.path.join(dest, file_name)
        if os.path.exists(out_path) and not force:
            log(f"  skip (already exists): {file_name}   [use --force to overwrite]")
            return True
        if dry_run:
            log(f"  dry-run: would extract {entry} -> {out_path}")
            return True

        os.makedirs(dest, exist_ok=True)
        with zf.open(entry) as src, open(out_path, "wb") as dst:
            shutil.copyfileobj(src, dst)
        log(f"  installed: {file_name}  ({os.path.getsize(out_path)} bytes)  from {entry}")
        return True


def expected_file(pkg: str) -> str:
    """MessagePack:3.1.8 -> MessagePack.dll (true for all six default packages)."""
    return pkg.partition(":")[0] + ".dll"


def meta_any_platform(dll_path: str) -> str:
    """Read Any.enabled out of the PluginImporter .meta so build breakage is visible."""
    meta = dll_path + ".meta"
    if not os.path.exists(meta):
        return "NO META (not imported by Unity yet)"
    try:
        text = open(meta, encoding="utf-8", errors="replace").read()
    except OSError as e:
        return f"meta unreadable: {e}"
    if "Any: " not in text:
        return "NO 'Any' platform entry -> NOT available on all platforms"
    for line in text.splitlines():
        if line.strip() == "enabled: 1":
            return "any-platform OK"
    for line in text.splitlines():
        if line.strip() == "enabled: 0":
            return "Any.enabled=0 -> DISABLED (builds will miss this assembly)"
    return "unknown"


def check(dest: str) -> int:
    """Read-only status: answers 'already installed / what is missing'."""
    log(f"destination: {dest}")
    log("")
    missing = []
    log("-- expected DLLs --")
    for pkg in DEFAULT_PACKAGES:
        name = expected_file(pkg)
        path = os.path.join(dest, name)
        if os.path.exists(path):
            log(f"  OK       {name:<44} {os.path.getsize(path):>7} B   {meta_any_platform(path)}")
        else:
            missing.append(name)
            log(f"  MISSING  {name}")
    log("")

    log("-- Unity-provided (must NOT be present in the project) --")
    for base in UNITY_PROVIDED:
        hits = [f for f in os.listdir(dest) if f.lower() == (base + ".dll").lower()]
        log(f"  {'!! DUPLICATE FOUND' if hits else 'absent (good)   '}  {base}")
    log("")

    log("-- downstream evidence --")
    # 2026-10-09 起网络代码分在 02_Net(NetTmp) 与 07_NetGame 两个程序集里（不再只落 Assembly-CSharp）
    asm_dir = r"d:\Pro\Bluedivers\Library\ScriptAssemblies"
    for _name in ("02_Net", "07_NetGame", "Assembly-CSharp"):
        asm = os.path.join(asm_dir, _name + ".dll")
        if os.path.exists(asm):
            blob = open(asm, "rb").read()
            extra = "   (正常: DTO 的 [MessagePackObject]/[Key] 特性需要它)" if _name == "07_NetGame" else ""
            log(f"  {_name}.dll references: KCPNet={b'KCPNet' in blob}  MessagePack={b'MessagePack' in blob}{extra}")
        else:
            note = "（本项目已全部走 asmdef，没有 Assembly-CSharp 属正常）" if _name == "Assembly-CSharp" else "（未编译过？）"
            log(f"  {_name}.dll not found {note}")
    log("")

    log("-- assembly load result of the LAST domain reload --")
    log_path = os.path.expandvars(r"%LOCALAPPDATA%\Unity\Editor\Editor.log")
    stale = 0
    loaderr: list[str] = []
    if os.path.exists(log_path):
        lines = open(log_path, encoding="utf-8", errors="replace").read().splitlines()
        # Unity prints load errors first, then "Loaded All Assemblies" as the pass summary,
        # so the newest pass lives between the last two summary markers.
        marks = [i for i, l in enumerate(lines) if "Loaded All Assemblies" in l]
        if len(marks) >= 2:
            seg, stale = lines[marks[-2]:marks[-1]], len(marks)
        elif marks:
            seg, stale = lines[:marks[-1]], len(marks)
        else:
            seg = lines
        unresolved = [l.strip() for l in seg if "Unable to resolve reference" in l]
        loaderr = [l.strip() for l in seg if "will not be loaded due to errors" in l]
        log(f"  domain reloads in log: {stale}    newest pass: {len(loaderr)} load-error header(s), "
            f"{len(unresolved)} unresolved reference(s)")
        for l in unresolved:
            log(f"    !! {l}")
        if not loaderr and not unresolved:
            log("  newest pass is CLEAN (older failures earlier in the log are history)")
    else:
        log("  Editor.log not found")
    log("")

    if missing:
        log(f"VERDICT: incomplete - {len(missing)} DLL(s) missing -> run without --check")
        return 1
    if loaderr:
        log("VERDICT: files present but the newest domain reload STILL failed to load assemblies:")
        log("         install the unresolved references above and refresh again.")
        return 1
    log("VERDICT: files present and assemblies loaded. Next: read_console + execute_code roundtrip.")
    return 0


def main() -> int:
    ap = argparse.ArgumentParser(description="Install / verify KCPNet NuGet dependencies in the Unity project.")
    ap.add_argument("--dest", default=DEFAULT_DEST, help=f"destination folder (default: {DEFAULT_DEST})")
    ap.add_argument("--package", action="append", default=[], help="additional Id:Version, repeatable")
    ap.add_argument("--check", action="store_true", help="read-only status report, installs nothing")
    ap.add_argument("--force", action="store_true", help="overwrite existing DLLs")
    ap.add_argument("--dry-run", action="store_true", help="list nupkg contents without writing files")
    args = ap.parse_args()

    if args.check:
        return check(args.dest)

    targets = list(DEFAULT_PACKAGES) + args.package

    log(f"destination: {args.dest}")
    log(f"packages   : {', '.join(targets)}")
    log("")

    ok = True
    for pkg in targets:
        ok = install_package(args.dest, pkg, args.force, args.dry_run) and ok
        log("")

    if ok and not args.dry_run:
        log("done. next steps:")
        log("  1) Unity MCP: refresh_unity(mode=force, scope=all, compile=request)")
        log("  2) Unity MCP: read_console(types=[\"error\"])  -> expect 0 entries")
        log("  3) Unity MCP: execute_code -> assert KCPNet/MessagePack are in AppDomain assemblies")
        log("  4) if a plugin still fails to load: python install_deps.py --check  (and read Editor.log)")
    return 0 if ok else 1


if __name__ == "__main__":
    sys.exit(main())
