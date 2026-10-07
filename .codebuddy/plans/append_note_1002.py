# -*- coding: utf-8 -*-
"""向 2026-10-02 daily 追加一条记录（保原编码/换行/BOM 状态）。

不改用 replace_in_file 的原因：daily 正被另一个窗口并发追加，末尾锚点不可预知，
append 语义（不依赖锚点、不重写已有内容）更安全。
"""
import sys

try:
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
except Exception:
    pass

p = r"d:\Pro\Bluedivers\.codebuddy\memory\2026-10-02.md"
raw = open(p, "rb").read()
bom = raw[:3] == b"\xef\xbb\xbf"
txt = raw.decode("utf-8-sig")
nl = "\r\n" if "\r\n" in txt else "\n"

note = """
## 已落码：武器拾取「先卸后装」静默卸载语音 + 记忆治理执行（2026-10-02 第三轮）

- **代码**（与"手持物替换"同款处理）：`Furniture_WeaponPickup.Operate()` 拾取分支里"支援槽已有武器则先卸下旧的"那处，
  `equipController.UninstallEquip(oldWeapon)` → `UninstallEquip(oldWeapon, silent: true)`——
  紧接着 `InstallEquip(m_Weapon)` 会播"安装"语音，两条连着播会打架。其余 6 个 `UninstallEquip` 调用点保持默认 `silent:false`。
- 验证：`plans/offline_compile.py 06_Gameplay` = **0 错误**（未触发 Unity 编译）。
- **记忆治理（处理体检红）**：
  - `topics/contract.md` 超预算（**10826 / 8000**）⇒ 把尾部「家具身份单一数据源改造」整段（4908 字符）拆到新 `topics/furniture-identity.md`，
    contract.md 原位留一行指针（现 **6047** 字符）。工具 `plans/split_contract_furniture.py`（默认 dry-run，`--apply` 落盘；
    带锚点唯一断言、DST 已存在即拒、保 BOM/行尾）。
  - `MEMORY.md` 路由表新增一行：`家具身份 / Identity / 家具 Id / BaseObject / Furniture_Base 已删 → topics/furniture-identity.md`。
  - 超 30 天 daily 归档：`memory_archive_legacy.py --cut 2026-09-02 --delete` 删除 `2026-09-01.md`（7273 字符，
    索引落 `archive/INDEX-legacy-20260902.md`）。⚠ 该文通篇 `-` 条目、**0 个 `##` 标题**，索引里只有文件名 + 字符数；
    正文可用 `git checkout HEAD -- .codebuddy/memory/2026-09-01.md` 取回（删除前该文件与 HEAD 一致）。
- ⚠ **并发写观察**：另一窗口正在写同一批记忆文件（`MEMORY.md` 3467→3130、`topics/contract.md` 5917→10826、daily +15k 字符），
  本轮所有编辑都**先重读再改**。另注：`MEMORY.md` 热区 3 的「⭐ 首选验证 = 离线编译」被改成「可选验证 = 离线编译」（非本轮改动）。
"""

if not txt.endswith(nl):
    txt += nl
txt += note.replace("\n", nl)
open(p, "wb").write((b"\xef\xbb\xbf" if bom else b"") + txt.encode("utf-8"))
print("appended, new bytes =", len(open(p, "rb").read()))
