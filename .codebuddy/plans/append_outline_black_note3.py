import io

NOTE = """
### 真正根因（已修，2026-11-22）：Outline pass 没声明 _FORWARD_PLUS
- 工程 URP Renderer `m_RenderingMode: 2` = **Forward+**（`Assets/Setting/New Universal Render Pipeline Asset_Renderer.asset`）。
- 但 ToonLit 家族的 **Outline pass 的 pragma 里没有 `_FORWARD_PLUS`**（只有 ForwardLit 有）⇒ 该 pass 编译时 `USE_FORWARD_PLUS` 未定义，
  `GetMainLight()` 走 `light.distanceAttenuation = unity_LightData.z` 这条老分支；Forward+ 下 `GetPerObjectLightFlags()`
  只在 `!isForwardPlus` 时附 `PerObjectData.LightData`（UniversalRenderPipeline.cs:1604-1606），这个值根本不会被填，
  再叠加我们自己的 `DrawRenderers` 也没设 perObjectData（默认 None）⇒ 主光项恒 0。
- 结果：描边受光色只剩间接光 `unity_AmbientSky`（被昼夜系统写成 ~0.096）+ `_IndirectLightMinColor`(0.1) 兜底 ≈ 0.1，
  乘 `_OutlineColor` 后恒黑 ⇒ "白天也黑、改颜色没用"（本体亮是因为它走 Forward+ 分支：distanceAttenuation=1 + 全局 `_MainLightColor` + SampleSH）。
- 已改 3 处：
  1. `Assets/Shader/ToonLit/Main/ToonLit_Shared.hlsl`（`ShadeAllLights` 里 `GetMainLight()` 后）：`#ifdef ToonShaderIsOutline` → `mainLight.distanceAttenuation = 1.0;`
  2. `OutlineRendererFeature.Execute`：`drawingSettings.perObjectData = renderingData.perObjectData;`
  3. 同文件：`context.ExecuteCommandBuffer(cmd)` 移到 `DrawRenderers` **之前**（原来 SetRenderTarget/全局 uniform 当帧全不生效）。
- 未改：`SnowRendererFeature.cs` 同样的顺序 bug（雪现在靠残留全局侥幸生效），要改问用户。
- 若要描边间接光与本体完全一致，可把 outline 分支的 `unity_AmbientSky` 换成 `SampleSH(0)`（现在 perObjectData 已给，能拿到探针）。
"""

p = r"d:\Pro\Bluedivers\.codebuddy\memory\2026-11-22.md"
with io.open(p, "a", encoding="utf-8", newline="") as f:
    f.write(NOTE)
print("ok")
