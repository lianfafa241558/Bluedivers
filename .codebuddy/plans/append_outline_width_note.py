import io

NOTE = """
### 描边宽度与距离（查证，未改）
- 公式：`ToonLit_Shared.hlsl:231-238` `width = _OutlineWidth * clamp(dist/10, 1, 3)`，再乘
  `GetOutlineCameraFovAndDistanceFixMultiplier()`（`NiloOutlineUtil.hlsl:22-51`）。透视下该 multiplier =
  `saturate(|z_view|) * FOV * 0.00005`，而 `saturate(|z|)` 在 1m 外恒为 1 ⇒ **"屏幕等宽"的补偿实际失效**。
- 实际效果：d<10m 世界宽度恒定（屏幕近粗远细，∝1/d）；10~30m 宽度∝d（屏幕宽度基本恒定）；>30m 固定 3 倍（又变细）。
- `_OutlineWidth`：shader 默认 4、单位材质多为 2（Range 0~20）。Feature 的 `maxDistance/minScreenPixels` 只决定
  整条描边画不画（当前都是 0=关），不改变宽度。本轮修黑描边没碰宽度。
"""

p = r"d:\Pro\Bluedivers\.codebuddy\memory\2026-11-22.md"
with io.open(p, "a", encoding="utf-8", newline="") as f:
    f.write(NOTE)
print("ok")
