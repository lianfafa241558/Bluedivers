# 主题 · 联机（KCPNet，试验中）

> 由 `MEMORY.md` 路由表按触发词加载。预算 ≤ 8000 字符。

- 组成：`Assets/Plugins/KCPNet/{KCPNet,Kcp}.dll` + 自研适配层 `Assets/Scripts/NetTmp`
- 形态：房主权威 + 局域网 P2P（UDP 广播 → KCP 回连）
- 端口：**17666 / 29800 / 29801**
- 依赖：MessagePack 3.1.8 及传递依赖已装齐（`scripts/install_deps.py`，`--check` 可自检）
- ⚠ `Standalone = IL2CPP` ⇒ MessagePack 的 AOT 风险
- ⚠ 插件加载失败看 `Editor.log` 里的 `Unable to resolve reference` / `will not be loaded due to errors`
- 扩展方式与排查清单见 skill `kcpnet-online`
