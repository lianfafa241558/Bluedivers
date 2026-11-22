import io

NOTE = """
### 回归修正：描边"穿透物体"（2026-11-22，是我上一步引入的）
- 诱因：把 `context.ExecuteCommandBuffer(cmd)` 从 `DrawRenderers` 之后挪到之前时，cmd 里的
  `cmd.SetRenderTarget(_renderer.cameraColorTargetHandle)` 也跟着生效了 —— 只绑 color 会把已绑好的**深度**摘掉，
  Outline pass 的 ZTest 失效 ⇒ 膨胀壳（Cull Front）盖在所有物体上 = "描边穿透"。
  （挪之前它排在 draw 之后，只影响后续 pass，URP 每个 pass 自己重绑，所以没暴露。）
- 修法：删掉该 `SetRenderTarget`，AfterRenderingOpaques 时相机颜色+深度本来就绑好，直接继承；
  cmd 只保留主光/额外光全局 uniform，仍在 DrawRenderers 之前执行。
- 经验：**自定义 RenderPass 里别只绑 color target**；要绑就连 depth（`cameraDepthTargetHandle`）或干脆继承。
"""

p = r"d:\Pro\Bluedivers\.codebuddy\memory\2026-11-22.md"
with io.open(p, "a", encoding="utf-8", newline="") as f:
    f.write(NOTE)
print("ok")
