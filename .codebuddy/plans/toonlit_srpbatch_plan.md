# ToonLit 恢复 SRP Batcher —— 改造草案

> 2026-11-16。目标：把 `StdRender.ApplyShader`（真机 **0.91~1.01ms / 212~231 calls**）压下来。
> ⚠ 前置更正：ToonLit **不缺** CBUFFER（`ToonLit_Shared.hlsl:108-189` 已有 55 条），
> 真凶是 **Stencil 状态里引用了材质属性**。

## 一、真因（证据）

`ToonLit.shader` 的 ForwardLit 与 Outline 两个 Pass：

```shaderlab
Pass {
    Name "ForwardLit"
    Stencil
    {
        Ref [_RenderRef]      // ← 这一行
        Comp Always
        Pass Replace
    }
```

Unity 规则：**任何一个 Pass 的渲染状态里引用材质属性，该 shader 即「SRP Batcher 不兼容」**
（官方文档 *SRP Batcher compatibility* → "Render state uses material property"）。

同一份毛病在本工程一共 38 个 shader（`plans/shader_srp_break.py` 全量扫出）：
`ToonLit` 家族 6 个、`GlowTexture2`、`WarningWall`、`LX/Texture2`、`LX/NewHalo`、
`LX/FakeAreaLight`、`Environment/SkyboxLayer`、各类 Decal / UI…

`SRPBatcher` 总开关本身是开的，已确认：
```
GraphicsSettings.useScriptableRenderPipelineBatching = True
URP Asset  m_UseSRPBatcher = 1
```

## 二、`_RenderRef` 的实际取值（决定怎么改）

`ToonLit/ToonLit` 下 35 个唯一材质：

| `_RenderRef` | 材质数 | 槽位 | 代表材质 |
|---|---|---|---|
| **0** | **25** | 175 | Ung_Stalker / container_* / School_01 / Stone1 / Desert_01 … |
| **1** | 9 | 129 | Ung_Front / Ung_Hydralisk / Nagisa_Original_Body1 / Kei_Body / AirdropGeneric_ncl … |
| **3** | 1 | 10 | Pyroxene |

其余变体全部单一取值：
| shader | 唯一材质 | `_RenderRef` |
|---|---|---|
| `ToonLit_Scene` | 2 | 全 0 |
| `ToonLit_Stone` | 1 | 全 0 |
| `ToonLit_Face` | 2 | 全 1 |
| `ToonLit_MouthEye` | 2 | 全 1 |
| `ToonLit_Hair` | 1 | 全 1 |

## 三、方案（分三步，可独立停）

### 步骤 A：四个单一取值的变体直接写死（零行为变化）

| 文件 | 行 | 改动 |
|---|---|---|
| `Assets/Shader/ToonLit/Main/ToonLit_Scene.shader` | 146、222 | `Ref [_RenderRef]` → `Ref 0` |
| `Assets/Shader/ToonLit/Main/ToonLit_Stone.shader` | 96、167 | `Ref [_RenderRef]` → `Ref 0` |
| `Assets/Shader/ToonLit/Main/ToonLit_Face.shader` | 42、91 | `Ref [_RenderRef]` → `Ref 1` |
| `Assets/Shader/ToonLit/Main/ToonLit_Hair.shader` | 111、179 | `Ref [_RenderRef]` → `Ref 1` |
| `Assets/Shader/ToonLit/Main/ToonLit_MouthEye.shader` | 43 | `Ref [_RenderRef]` → `Ref 1` |

覆盖 8 个材质 / 405 槽。**风险 0**（取值本来就唯一）。

### 步骤 B：`ToonLit` 本体写死 `Ref 0` + 给非 0 的材质另开一份壳

1. `ToonLit.shader` L198、L266：`Ref [_RenderRef]` → `Ref 0`
   ⇒ 25 个材质（175 槽）行为不变并立刻合规。
2. 新增 `Assets/Shader/ToonLit/Main/ToonLit_Stencil1.shader`（**只需改 2 行**）：
   - `Shader "ToonLit/ToonLit_Stencil1"`
   - 两处 `Ref 0` → `Ref 1`
   - 其余 100% 复制（7 个 Pass 全部 `#include "ToonLit_Shared.hlsl"`，**实现在共享文件里，壳的维护成本≈0**）
3. `Pyroxene.mat`（`_RenderRef=3`）单独一份 `ToonLit_Stencil3.shader`，或先确认它是否真的需要 3（只有 10 槽）。
4. 把 9 个 `_RenderRef=1` 的材质资产的 Shader 字段指向 `ToonLit_Stencil1`：
   `Ung_Front` / `Ung_Hydralisk` / `Ung_Back` / `AirdropGeneric_ncl`(×2) /
   `Nagisa_Original_Weapon1` / `Nagisa_Original_Body1` / `destroyedstreet_p01` / `Kei_Body`

覆盖 35 个材质 / 314 槽。**这是收益的主体。**

### 步骤 C（可选）：其它透明/特效 shader

`GlowTexture2`（1 材质）、`WarningWall`（1）、`LX/Texture2`（4）、`LX/FakeAreaLight`（1）、
`LX/NewHalo`（2）、`Environment/SkyboxLayer`（2）—— 同样是把
`Blend[_SrcBlend][_DstBlend]` / `ZWrite [_ZWriteMode]` / `Cull[_Cull]` 换成常量。
但这些是透明混合体，SRP Batcher 收益本来就有限，**等 B 验证完再决定**。

## 四、先做的一件事：10 分钟验证 B 的假设

不要一次改完。**只改 `ToonLit.shader` 那 2 行**（`Ref [_RenderRef]` → `Ref 0`），然后：

1. 重新构建 / 进 Play；
2. 看 `StdRender.ApplyShader` 的 **calls** 与 **self ms**（基线：212~231 calls / 0.90~1.01ms）。

预期：calls 掉到 **80~110**、self 掉到 **0.55~0.65ms**。

- 掉到位 ⇒ 假设成立，回来做步骤 A + B 的完整版；
- 完全没动 ⇒ SRP Batcher 还有别的兼容性问题，**立刻停**，别做 A/B。

⚠ 验证期间 XRay 的 stencil 会错（那 9 个 Ref=1 的材质变成写 0），**属预期现象，测完立刻回滚这两行**。

## 五、避坑

1. **别删 `_RenderRef` 属性本身**：`ToonLitMainShaderGUI` 可能用 `FindProperty("_RenderRef")`，
   删掉会 NRE。只改「状态里的引用」，属性留在 Properties 里没影响。
2. **CBUFFER 布局必须每个 Pass 一致**：因为都 `#include "ToonLit_Shared.hlsl"`，天然一致 —— 别在某个 Pass 里
   自己再声明 per-material 变量。
3. 改完看三个东西：ToonLit 视觉（描边/透明/受击闪白）、XRay 透视、`_CameraDepthTexture` 相关效果
   （DepthOnly / DepthNormalsOnly 两个 Pass 也带 `ToonLitShaderIsOutline`）。
4. 材质换 shader 用脚本批量 + 逐个确认 `_RenderRef` 值，别按名字猜。

## 六、预期总收益

| 项 | 现状 | 预估 |
|---|---|---|
| `StdRender.ApplyShader` calls | 212~231 | ~40~60 |
| `StdRender.ApplyShader` self | 0.90~1.01 ms | 0.25~0.35 ms |
| 帧时（7.2ms 基线） | — | **−0.55~0.65 ms（≈8%）** |

另外 `WaitForJobGroupID`（0.78~0.86ms）与 `Submit` 里有一部分是"命令缓冲区构建"，setpass 少了也会跟着降一点。
