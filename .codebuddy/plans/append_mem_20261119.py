import io
import os

p = r"e:/Bluedivers/.codebuddy/memory/2026-11-19.md"
new_file = not os.path.exists(p)
head = "# 2026-11-19\n" if new_file else ""

note = r"""

## 新工具：动画节点路径前缀批量去除（EditorTools，已编译+端到端验证）
- 文件：`Assets/Editor/Tool/AnimationPathPrefixProcessor.cs`（核心：CollectAnimFiles / DetectCandidatePrefixes / Scan / RemovePrefix / BackupFiles）+ `AnimationPathPrefixTool.cs`（EditorWindow）。
- 入口：`Tools/动画/批量去除节点路径前缀` 与 `Assets/工具/批量去除动画路径前缀`（两个 MenuItem 贴同一方法）；支持拖文件夹、用当前选择、自动检测候选前缀、扫描预览、执行前备份。
- 关键口径：只改 `path:` 行（cli p 名不动——2026-11-17 踩过整文件 replace 误改 `m_Name`）；正则 `^([ \t]*path:[ \t]*)` + 前缀，Multiline，替换 `$1`；写回按原文件有无 BOM 选 `new UTF8Encoding(bom)`，换行原样不动。
- 写前强校验：行数不变 + 长度差 == 命中数×前缀长度，不符则该文件跳过并 LogError（宁可不改）。执行后逐文件 `ImportAsset(ForceUpdate)` + `Refresh`，避免 Animation 窗口内存旧值回写。
- 备份：`Temp/AnimPathPrefixBackup/<时间戳>/` 保留目录结构，窗口有「打开上次备份目录」。
- 实测（2026-11-19）：Devastator 目录只读 Scan/Detect = `bone_root/ 4139/4625`，与 Python 同口径一致；副本 `Assets/__AnimPrefixTest` 端到端 RemovePrefix 820 处→0、failures=0、行数 76962 不变、BOM/LF 不变，测试目录已删。
- ⚠ 统计口径坑：`bytes.splitlines()` 与 `split(b'\n')` 结果可能差很多（Unity 重序列化后残留 `\r` 等），对数时必须统一 `split(b'\n')`，否则会误判"文件被改了"。
"""

with io.open(p, "a", encoding="utf-8", newline="") as f:
    f.write(head + note)
print("ok", "created" if new_file else "appended")
