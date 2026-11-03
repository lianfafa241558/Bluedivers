# VFX / 粒子特效（材质坑 + 预制体规范 + 编辑器预览法）

> 2026-10-26 做 `VFX_ThermiteBurst`（铝热烟花式橙色爆炸）时逐张渲染实测出来的结论。

## 1 特效预制体规范（被 `VFXManager` 池化）

- **根物体必须挂一个 ParticleSystem 当"生命时钟"**：`VFXManager.ItemUpdate` 用 `go.TryGetComponent<ParticleSystem>()` 后 `ps.IsAlive(false)` 判定特效是否结束，返回 false 即回池（`VFXManager.cs:158-170`）。
  - 做法同 `vfx_Explosion_Yellow.prefab`：根 PS 渲**染器关掉**（`ParticleSystemRenderer.enabled=false`）、`duration` 很小、`startLifetime` = 整段时长、burst 1 个不可见粒子。
  - 子层都比"根 lifetime"短，否则池会提前回收（根 1.75s 时子层最长 1.5s）。
- 子层命名按表现作用：`Flash / Boom / Rays / RaysThin / Embers / Glow / ShockRing / Smoke`，方便后续单层调。
- 所有子层 `playOnAwake=true`：池靠 `SetActive(true)` 重播，不调 `Play()`。
- 弹道类问题（回池后子物体残留激活态）见 `2026-10-26.md`，与纯特效预制体无关。

## 2 材质实测结论（URP 14）

| 材质 | 真实形态 | 用法 |
|---|---|---|
| `General_Explosion_Orange_Glow` | 干净圆形火球（HDR tint 47/17/2） | 火球/余辉首选 |
| `General_Explosion_Orange_GlowTrail` | 横向长条辉光（Effect_Glow_Shot） | `renderMode=Stretch` ⇒ 烟花长射线 |
| `General_Explosion_Orange_Shockwave` | 真环形 | 冲击环（Billboard 即可） |
| `General_Explosion_Orange_Boom` / `_Fire2` / `General_Explosion_Boom2` | **8×8 图集** | 直接 Billboard = 点阵方阵；必须开 Texture Sheet Animation |
| `DiskExplosion`（Art/VFX/FPS） | ExplosionRing 贴图 | Billboard 会渲成竖条纹；要 `renderMode=Mesh`（`VFX_MortarHit` 就是这么用的） |
| `Spark`（Art/VFX/FPS/Materials） | **无贴图** | 粒子是硬边白方块；**拖尾也用它 = 一串白条** |
| `Point` / `Magic Point` / `Point_White` | 圆点/软点 | 火星、细射线（Stretch 后是软铅笔线） |
| `Flash`（魔法包） | 4 角星 | 放大后像"蝴蝶"，白热核心不如 `Point_White` |
| `WFX_M_Smoke` / `WFX_M_Sparks Metal` | 可用 | 烟 / 短划火星 |
| Magic 包的 `Particles/Standard Unlit`（Point/Smoke26/Flash） | —— | 在本工程 URP 下**能正常渲染**（实测非粉红），可用 |

## 3 叠加（additive）淡出必须靠颜色

- `alpha` 淡出在**大尺寸 HDR 辉光**上会渲出"肥皂泡色环"（边缘亮环 + 内部发绿）。
- 正确做法：`colorOverLifetime` 的 **alpha 恒为 1**，颜色收到接近黑（`0.2,0.04,0`）⇒ 叠加自然消失。
- 例外：alpha 混合材质（`LX/Texture2` 这类 blend 3/7）才用 alpha 淡出。

## 4 不进 Play 的编辑器预览法（可复用）

```csharp
var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
var cam = new GameObject("cam").AddComponent<Camera>();   // MoveGameObjectToScene 到新场景
cam.targetTexture = rt;                                  // 640×640
ps.Simulate(0.10f, false, true);   // ⚠ edit 模式**可用**（实测），restart=true 从 0 推
cam.Render(); RenderTexture.active = rt; tex.ReadPixels(...); File.WriteAllBytes("Assets/../x.png", png);
EditorSceneManager.CloseScene(scene, true);
```
- 好处：不脏 `GameRoot`、不进 Play、可做"逐层分解图 / 多时间点对照图"。
- 坑1：`NewScene(..., Additive)` 在**存在未保存无名场景**时直接报 `Cannot create a new scene additively with an untitled scene unsaved` ⇒ 先遍历 `SceneManager` 关掉 `path` 为空的残留场景。
- 坑2：`DestroyImmediate(RenderTexture.active)` 会警告 `Releasing render texture that is set to be RenderTexture.active` ⇒ 先 `RenderTexture.active = null`。
- 坑3：预览 RT 是线性 + 无 Bloom/调色 ⇒ 颜色判读不可全信（HDR 叠加会渲成橄榄绿/白），**只用来判形状与层次**，最终观感以进 Play 为准。

## 5 持续喷射型（点火 + 4 秒滋火花）的参数要点

- **持续发射不必开 `loop`**：子层 `main.duration = 4.05` + `loop=false` + `emission.rateOverTime` 常量 ⇒ 一样匀速吐 4 秒；开 loop 会有接缝，且根计时不好算。
- **喷口朝向**：`shape.shapeType = Cone` + `shape.rotation = (-90,0,0)` —— Cone 的轴默认是局部 **+Z**，这一下把它扳到 **+Y**（配合 `simulationSpace=World`，随根物体朝向但不被根位移拖着走）。
- **根计时必须盖住尾巴**：`startLifetime = 4.05(发射时长) + ~1.3(最长子层 life)` ≈ **5.4s**，否则 `VFXManager` 会提前判死、把烟和还在飞的尾火一起回收。
- **火花形态**：`renderMode=Stretch` + `velocityScale 0.035~0.05` + `lengthScale 1.6~2.4`；`gravityModifier 0.85` 让它们自然抛落成弧线。
- **密度参考（4s 持续型）**：Sparks 95/s（life 0.45~1.0 ⇒ 场上约 60 颗）、Streaks 18/s、Pops 32/s、Core 22/s、Smoke 4/s；再往上加要先看 maxParticles 与 DrawCall。
- 点火那一下单独给两个**一次性**子层（`Ignite` 圆点闪光 + `IgniteRays` 短星芒），否则持续层永远没有"点燃瞬间"。

## 6 产物

- `Assets/Resources/VFX/Hit/Grenade/VFX_ThermiteBurst.prefab`：**点火 + 持续 4 秒上喷火花**，9 个物体（根计时 + Ignite / IgniteRays / Sparks / Streaks / Pops / Core / ShockRing / Smoke），根计时 5.4s，向上（局部 +Y）喷射。未接任何武器（用的时候挂 `ImpactVfx` 或 `VfxPool.Creat`）。
- 文件名沿用 `ThermiteBurst`（改名属手动操作）；要更贴切可手动改成 `VFX_ThermiteSpray` 之类。

## 7 2026-10-27：AI 爆炸/烟雾/毒雾/地面火 一批 13 个（MCP 程序化生成）

产物：`Assets/Resources/VFX/Hit/AI/` —— `VFX_Explosion_S/M/L/XL/Nuke`、`VFX_Smoke_Puff/Column/Ground`、`VFX_Poison_Mist/Cloud/Spurt`、`VFX_GroundFire(_L)`；材质在 `Assets/Art/VFX/EffectMat/`（Poison_Glow / Poison_Dot / Toxiс_Flames / Nuke_Core）。

### 7.1 材质必须"复制现成材质"，不能 `new Material(Shader.Find(..))`
- `LX/SimpleFogAdditive`、`LX/Texture2`、`WFX/*` 这类工程自研 shader 的 **blend 靠材质属性**（`_SrcBlend/_DstBlend/_UseFog`）；`new Material` 只拿 shader 默认值 ⇒ 渲成**不透明黑方块**（additive 层尤其明显：贴图黑底整块遮住场景）。
- 正解：`m.shader = src.shader; m.CopyPropertiesFromMaterial(src);` 再只改 `_BaseColor`/`_TintColor`。GUID 不变 ⇒ prefab 引用不丢。
- `WFX/Multiply Soft Tint` 在**暗背景**下必然是黑块（乘法混合），别拿它做烟的主层。

### 7.2 图集贴图不开 TSA = 一排小火团
- `Flame02_AB`（Flame02 2048）是**多帧图集**，直接 Billboard 会渲成"横向一排火"。
- 地面火用 `WFX_FlameBig Texttres`（flame4.dds，**2×2** TSA + WholeSheet，`cycleCount=2`）已验证；`Vfx_GroundFlame4` 就是这么用的。

### 7.3 烟材质 `WFX_M_ExplosiveSmoke` 的贴图很薄
- 单粒子视觉密度低：要成"烟柱/烟团"需 **rate ≥ 18 / size ≥ 2 / alpha 峰值 ≥ 0.5**；只有 3~4 个粒子 + alpha 0.3 时几乎看不见（深色背景下比背景还暗）。
- 毒雾**不要 tint 材质**：改用原版 `WFX_M_ExplosiveSmoke` + **粒子 `startColor` 染色**（vertex color 乘上去），混合行为完全可控。发光层用绿 `_BaseColor` 的 `LX/SimpleFogAdditive` 副本。

### 7.4 根计时必须"自动化地对齐尾巴"
- 手写 `rootLife` 极易短于 `子层 duration + startLifetime`，被 `VFXManager` 提前判死 ⇒ 尾巴（烟/焦痕）被截断。
- 收尾脚本：遍历子 PS 取 `max(duration + startLifetime.constantMax) + 0.15`，回写根 PS 的 `startLifetime`（本次 11/13 个 prefab 都需要抬，最大 +1.55s）。

### 7.5 `velocityOverLifetime` 的 x/y/z（以及 orbitalX/Y/Z）必须同 mode
- 否则每次 Simulate/更新都刷 `Particle Velocity curves must all be in the same mode` / `Particle Orbital Velocity curves…`（在 job 线程 `VelocityModule::Update` 里刷，日志栈看不出是哪个特效）。
- `minMaxState`: 0=Constant / 1=Curve / 2=TwoConstants / 3=TwoCurves，**三轴必须完全相同**；`radial` 不参与判定（现代 UI 改不到），算进去会得到 250+ 假阳性。
- 修法：以视觉主轴的 mode 为准，把另两轴改成同 mode（曲线/scalar 保持原值，通常本来全 0 ⇒ 零视觉变化）。
- 排查工具：`.codebuddy/plans/scan_ps_curve_mode.py`（只读扫全 Assets 的 prefab/scene）。⚠ **一个 prefab 有 N 个 PS、N 个 VelocityModule**，按"第一个 VelocityModule"定位会改到 disabled 的那个 ⇒ 按块内特征（`enabled: 1`、`orbitalY` 模式）挑。

### 7.6 MCP `execute_code`（CodeDom / C#6）的三个硬约束
- 不能链式改 struct 返回值：`p.main.startSize = ..` 报 "Cannot modify a value type return value" ⇒ 先 `var m = p.main;`。
- lambda 参数名不能与外层局部变量同名（含 `root`/`bg`/`m`），否则 "would give a different meaning"。
- 变量不能在相邻块重复 `var` 同名；不能 `out var`；`Object` 与 `UnityEngine.Object` 有歧义 ⇒ 写全名。

## 8 2026-11-02：黑客光束 `Pro_P_MicrowaveHackerBeam`（10 层）+ 两个必踩坑

预制体根下：`Glitch`（小方块闪断）/`Pixel`（损坏像素块）/`Stream`（Stretch 前冲数据条）/`Hex`+`HexShell`（全息六边形，大层缓慢）/`Code`（数字图集）/`Digit`（hacker_0 派生括号）/`ScanRing`/`Spark`/`Muzzle`+`MuzzleRing`；根 LineRenderer 用 `MW_Beam_Hacker.mat`（绿 HDR、`_MoveSpeed=-1500`、宽度 0.16→0.09 才不至于远端隐没）。

### 8.1 【坑】`LX/Texture2` 加法吃的是 **RGB**，白 RGB 的透明区照样被加亮
- 表现：明明贴图 alpha 只有细线，粒子却渲成**实心大块**。
- 两个独立原因：① 用 `new Color32(255,255,255,0)` 画透明区（RGB 白）；② 导入时勾了 **`alphaIsTransparency`** —— 它会给透明像素做**颜色膨胀**（把附近白色铺满全图），代码画的图瞬间变全白。
- 正解：透明区写 `(0,0,0,0)`，且 `alphaIsTransparency=false` + `alphaSource=None` + Uncompressed + Point。工程现成贴图（`tronhex1_blue`/`techRing`）本来就是黑底，才一直没暴露。
- 反之，**要"实心块"效果就用白底/实心贴图**（`tronsquare1_blue` 就是实心方块，做 glitch 块正合适）。

### 8.2 【坑】Billboard 下 `main.startSize3D` 不生效
- 只设 `startSizeX/Y/Z` 而不设 `startSize` ⇒ 粒子回落成**默认 1m 大方块**（`GetParticles` 读回的 `startSize` 是 1）。想用非等比矩形只能走 `renderMode=Mesh` + 自定义 quad，别指望 3D size。

### 8.3 诊断法：逐层拆图
- 关 **`ParticleSystemRenderer.enabled`**（不是 `gameObject.SetActive`，那样粒子直接停）逐层渲一张拼图，一次就能看出哪层是元凶。
- ⚠ **更正（同日）**：手写 `ARGBHalf → c/(1+c) → γ2.2` 这条管线**会溢出**（实测读到 `maxG=65504`、整屏 avg 21），把正常的层误判成"大白块"。**`RenderTextureFormat.ARGB32 + ReadWrite.sRGB` 直出**才可靠，只丢 Bloom 而已；要判"是否过亮"直接看 HDR 数值，不要再套自定义色调映射。
- `ParticleSystem.textureSheetAnimation` 在 `LX/Texture2` 这类自研 shader 上**不生效**（整张 4×4 图集一起显示，成了"数字面板"）—— 要用就用，别指望单帧。

## 9 2026-11-02：挂在单位身上的持续型异常状态特效 `Vfx_AboHacker`

产物：`Assets/Resources/VFX/AboState/Vfx_AboHacker.prefab` —— 根下 7 层：`Hacker_Shell`（躯干外围环绕六边形）/`Hacker_Code`（脚下圆环升起的代码字型）/`Hacker_Pulse`（向外扩的环形脉冲）/`Hacker_Glitch`（身上闪断像素）/`Hacker_Mark` + `Hacker_MarkRing`（头顶"被控制"标记：六边形 + 反向旋转圆环）/`Hacker_Spark`。

### 9.1 【必须】根上必须留一个"活着的 loop PS"，否则会被池秒回收
- `VFXManager.ItemUpdate` 只 `go.TryGetComponent<ParticleSystem>()` 看**根**：根没有 PS ⇒ 存活判定恒 false ⇒ 一进池就被回收；`Release` 也是靠根 `ps.Stop(true)` 来触发回收。
- 做法：根 PS `loop=true` + 极小不可见粒子（rate 2、size 0.01）+ `ParticleSystemRenderer.enabled=false`，视觉全交给子层。

### 9.2 挂载口径：父级是**单位根**，几何按"局部 = 世界 / 2"设计
- `Health_AboState.EnsureAboVfx`：`VfxPool.Creat(vfx, transform.position, transform.rotation, transform)` 然后 `localScale = VisualScale * 2`。
- 单位预制体原点在**脚底**（PMC_AR 网格包围盒 center.y≈0.96、size.y≈2.04），`VisualScale` 默认 1 ⇒ 局部 1 单位 = 世界 2 米。人形光环按"躯干 y≈0.42、头顶 y≈1.20、半径 0.28"这种局部值设计。
- 全部子层 `simulationSpace=Local`（跟着单位走，也随单位旋转）。

### 9.3 【坑】Tron 系列贴图底色不纯黑，加法下整块发亮
- `tronhex1_blue` 六边形内部是 `-`（0.05~0.2 亮度）、`tronsquare1_blue` 内部同理、**`techRing` 整张全白**（做加法粒子 = 白色实心方块）。之前光速/光束预览里的"白色纸片"就是它们。
- 正解：**程序化生成干净遮罩**（黑底 + SDF 形状 + 软边）——`VFX_Hacker_HexMask/SquareMask/RingMask.png`，六边形 SDF / max(|x|,|y|) / 环 SDF，256×256、Uncompressed、`alphaIsTransparency=false`。材质只换 `_MainTex`，blend 不变。
- 通用判据：**先看贴图底色**。黑底 = 随时能用；不纯黑 = 加法会发白，必须重新做遮罩。
