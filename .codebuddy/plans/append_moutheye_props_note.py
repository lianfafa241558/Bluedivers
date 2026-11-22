import io

NOTE = """
### ToonLit_MouthEye 发暗 = Properties 里没声明 `_ShadowMapColor`（2026-11-22，已修）
- 实测（Unity MCP，改前）：MouthEye 材质 `HasProperty("_ShadowMapColor")=false`、`GetColor` 返回 (0,0,0,0)，
  尽管 .mat 的 m_Colors 里明明存着 0.8 ⇒ **Properties 块没声明的属性，材质里的值不会被上传，cbuffer 拿到 0**。
- 后果：`ToonLit_LightingEquation.hlsl:71` `litOrShadowColor = lerp(_ShadowMapColor, 1, litOrShadowArea)`，
  `_ShadowMapColor=0` ⇒ 被判成暗面的像素完全不受该光源照射，只剩 ~0.1 环境光 ⇒ 眼嘴比脸暗一大截。
- 已改：`Assets/Shader/ToonLit/Main/ToonLit_MouthEye.shader` Properties 补
  `_ShadowMapColor("阴影颜色", Color) = (0.85,0.85,0.85)` 和 `_IndirectLightMinColor("环境光下限", Color) = (0.1,0.1,0.1)`；
  refresh 后实测 HasProperty=true、值读到 0.8 / 0.1（材质里的旧值立刻生效，不用重设）。
- 同一批"材质有值但 shader 没声明 → 实为 0"的还有（Face 与 MouthEye 都缺，材质里分别存 0.05 / 0.65 / 1）：
  `_CelShadeSoftness`（=0 ⇒ smoothstep 上下界相同，明暗交界硬切）、`_ReceiveShadowMappingAmount`（=0 ⇒ 完全不接收实时阴影）、
  `_FogMaxValue`（=0 ⇒ ApplyFog 的乘子为 0，等于不吃雾）。要补需逐个确认副作用。
"""

p = r"d:\Pro\Bluedivers\.codebuddy\memory\2026-11-22.md"
with io.open(p, "a", encoding="utf-8", newline="") as f:
    f.write(NOTE)
print("ok")
