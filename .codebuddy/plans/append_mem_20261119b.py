import io

note = r"""

## 按用户要求去掉「备份到 Temp/AnimPathPrefixBackup」（同日，已编译验证）
- 删除 `AnimationPathPrefixProcessor.BackupFiles` 与窗口里的 `_makeBackup` / `_lastBackupDir` / 备份开关 / 「打开上次备份目录」按钮；执行流程现在只有「扫描 → 执行」，不可撤销，只能靠 git 兜底。
- 该备份根目录的历史绝对路径：`E:\Bluedivers\Temp\AnimPathPrefixBackup\<yyyyMMdd_HHmmss>\`，内部保留 `Assets/...` 相对结构；用户 10:33 跑过一次，留了 1 个时间戳目录 / 15 个文件（PL/PowerLoader 等），未替用户删除。
- 副作用提醒：`.anim` 的 `m_Name` 必须与文件名一致，否则导入报 `Main Object Name ... does not match filename`（端到端测试复制文件时踩到，属测试残留日志，非工具缺陷）。
"""

with io.open(r"e:/Bluedivers/.codebuddy/memory/2026-11-19.md", "a", encoding="utf-8", newline="") as f:
    f.write(note)
print("ok")
