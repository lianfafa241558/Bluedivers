import io

NOTE = """
## 描边恒黑诊断（OutlineRendererFeature，2026-11-22）
- 现象：运行时怎么改 `_OutlineColor` 描边都是纯黑。根因链：
  1) 工程材质几乎全 `_FixOutlineColor: 0` ⇒ `ConvertSurfaceColorToOutlineColor()`（ToonLit_Shared.hlsl:667）
     返回 `表面受光色 * _OutlineColor` ⇒ 表面色为 0 时改乘数完全无效；
  2) 表面色为 0 因为主光 `distanceAttenuation = unity_LightData.z`（URP RealtimeLights.hlsl:108），
     而 `unity_LightData` 在 `CBUFFER_START(UnityPerDraw)`（URP UnityInput.hlsl:104/117）= per-object，不是全局；
  3) feature 里 `cmd.SetGlobalVector("unity_LightData",(0,0,1,0))`（OutlineRendererFeature.cs:409）
     本来就算在 `DrawRenderers`（:175）**之后**才 `ExecuteCommandBuffer`（:182），只靠全局残留到下一帧侥幸生效；
  4) 11/16 把 ToonLit 家族 Stencil 改常量 `Ref 0` 后，实测 6 个变体 `ShaderUtil.GetSRPBatcherCompatibilityCode=0`
     （SRP Batcher 兼容）⇒ UnityPerDraw 改为按物体绑定，残留全局彻底不再被读 ⇒ 主光衰减 0，
     只剩 `_IndirectLightMinColor`(0.1) × occlusion × `_OutlineColor` ≈ 0 ⇒ 恒黑。
- 临时救急：材质勾 `_FixOutlineColor=1`（描边取固定色，与光照解耦）。
- 正解：Outline pass 里 `GetMainLight()` 后强制 `distanceAttentenuation = 1`（`#ifdef ToonShaderIsOutline`）。
- 顺带：`SnowRendererFeature.cs:590/593` 同款「主光常量写在 DrawRenderers 之后」bug；
  运行时改描边色只能改材质/材质实例/MPB，`Shader.SetGlobalColor` 对 UnityPerMaterial cbuffer 无效。
"""

p = r"d:\Pro\Bluedivers\.codebuddy\memory\2026-11-22.md"
with io.open(p, "a", encoding="utf-8", newline="") as f:
    f.write(NOTE)
print("ok")
