import io

NOTE = """
### 上条修正：不是"恒黑"，是"运行时（夜）黑"
- 用户补充：**非运行（编辑模式）调 `_OutlineColor`=红确实会变成"光照*红"**，一进 Play 就黑。实测当时运行时状态：
  `ambientMode=Trilight`、`ambientSky=(0.096,0.121,0.215)`、主光=`Moon`（int 1，色 0.278/0.306/0.400）、`Sun` int≈0.39、`_IndirectLightMinColor=0.1`、材质 `_FixOutlineColor=0`。
- 结论：描边 = **受光色 × `_OutlineColor`**（ToonLit_Shared.hlsl:680），昼夜系统（EnvironmentLightingModule 每帧改 RenderSettings.ambient*、
  CelestialVisualsModule 改日月光强）把运行时受光色压到 0.0x（Outline 分支走 `unity_AmbientSky` + min 0.1，不走 `SampleSH`，
  所以物体本体亮、描边黑），乘任何颜色都 ≈ 黑；编辑模式用的是场景里 authored 的白天光照 ⇒ 看得见。
- 叠加因素（上一轮查的）：ToonLit 现在是 SRP Batcher 兼容（实测 `GetSRPBatcherCompatibilityCode=0`），
  `unity_LightData`（UnityPerDraw，per-object）的全局写入=死代码，Outline pass 的主光项恒为 0，只剩 0.1 环境光。
- 修法取向：描边不该吃昼夜光照 ⇒ `_FixOutlineColor=1`（纯 `_OutlineColor`），或 Outline 分支用 albedo 当色调而非受光色。
"""

p = r"d:\Pro\Bluedivers\.codebuddy\memory\2026-11-22.md"
with io.open(p, "a", encoding="utf-8", newline="") as f:
    f.write(NOTE)
print("ok")
