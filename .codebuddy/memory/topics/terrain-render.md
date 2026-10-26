# 主题 · 地形 / 渲染（TerrainData / 天气 / ToonLit）

> 2026-10-27 从 `gameplay-world.md` 拆出（超 8000 预算）。同伴：`gameplay-world.md`（家具/任务/逻辑帧/存档）、`08Map` 程序集。

## 地形 / 渲染
- NavMesh 在 `MapRoot`；跨层只传 `GameObject[]`/基础类型
- ToonLit 5 pass 共用 `ToonLit_Shared.hlsl`；`GetFinalBaseColor` = albedo 唯一入口；未进 CBUFFER = 死属性；溶解 `clip(step(dissolve,v)-dissolve+_EdgeWidth)`
- 天气 `WeatherSystem.RollWeather` 的随机源**必须按权威种子派生独立流**（`SeedStream.Weather=14`）：原来抽全局 `BattleRandom`，它在开局被任务点/兴趣点生成推进到不同位置 ⇒ 联机"一边晴一边雨"；单机（`TaskState.Seed==0`）仍沿用全局流
- ⭐ **主地形 = 一图一份 `TerrainData`**：`MapData_SO.terrainAsset` 持有，`BattleManager.InitTerrain` 在**读 terrainData / 设 size / SetTextures 之前**换上（留空 = 沿用 BattleScene 里那份 `MainMap`）。⚠ 打包端 `treePrototypes`/`detailPrototypes` 的 setter 是 **Editor-only**（只刷"only implemented in editor"、什么都不改）⇒ 原型**只能烘在资产里**；生成入口 `Tools/地形/生成每图 TerrainData（方案A）`（`Assets/Editor/TerrainDataBaker.cs`，**改了原型数组后打包前必跑**）+ `Tools/地形/体检`
- ⚠⚠ **`TerrainCollider` 自带独立一份 `terrainData` 引用、不跟随 `Terrain` 组件** ⇒ 换地形时必须同时 `terrain.GetComponent<TerrainCollider>().terrainData = ...`，否则"看着是新地形、脚下踩空/悬空"（2026-10-11 修）

- ⚠ 地形资产硬约束：`detailResolution`/`detailResolutionPerPatch` 是**只读属性**（脚本改不了，运行时也没人赋值）⇒ 专属资产**必须由"复制 MainMap"得来**，`new TerrainData()` 会让草分辨率变默认值而变糙；能缩的只有运行时**每次都会重设**的 `heightmapResolution`(→33)/`alphamapResolution`(→16)（`BattleManager` + `ApplyAlphamapsInChunks` 整张重写混合图），再加上**把细节（草）层全部清零**（`ClearAllDetailLayers`，草每局现场生成）⇒ 单份 13MB → 1.17MB → **约 49KB**（7 份合计 ~344KB）。⚠ 烘焙顺序必须**先清树实例、再换 `treePrototypes`**（反了会逐条报 `Tree removed: invalid prototype N`）。⚠ `terrainLayers` 不能省：`SetTextures` 只改已有层的 diffuse/tile、**从不建层**

## 地形树 / 覆盖石 / 清物（2026-09 归档前抽出的判据）

- ⭐ **只有 `CapsuleCollider` 会被实例化成"地形树碰撞体"**（Box/Sphere 不参与）⇒ 原型上那些 r=2~5m、离地 7~24m 的树冠球/胶囊**会变成半空隐形墙**（"跳不起来 / 打到看不见的墙"就是它，属 `TerrainCollider` 而非某个 GameObject，清不掉只能删原型上的 Collider）。
- ⚠ 判断"树碰撞体到底存不存在"**只服向下射线**（命中 `TerrainCollider` 且命中点离地 >0.5m ⇒ 必是树/石碰撞体）；**别信** `SerializedObject.m_EnableTreeColliders`（曾读到 0 而实际是 1，据此下的结论整段作废）。日志里 `closestHit.collider.name` 对树/石/地面**永远打印 "Terrain"**，区分不了。
- ⚠ `SetTreeInstances` **不会**更新树碰撞体，只能切一次 `TerrainCollider.enabled` 强制重建（几千棵 ≈ 10.75ms 全是重建钱；`SetTreeInstances` 本身 3650 实例只要 0.08ms ⇒ "真删实例让数组变小"没有收益）。
- **清物门面 `08Map/TerrainClearer.cs`**：请求入队 → 逐帧派到三条腿（`TreeDestructor` / `TerrainDetailEraser` / `RockCoverDestructor`）→ 每 `RefreshInterval=0.25s` 统一 `Flush()` + `RebuildTreeColliders` + `TerrainUtils.AsyncRefresh(true)`（= 重烘 NavMesh）。`ClearInRadius`=清除（忽略白名单）、`DestroyInRadius`=摧毁（走白名单）、`MarkTerrainChanged()`=标脏高度。
- ⭐ `RockCoverDestructor` 是 `TerrainUtils.AreaCircles` 的**唯一写入者**（Register/Remove 后 `SyncAreaCircles` 重建）⇒ 纯数据永远与存活实例一致。⚠ **清悬崖不会自动重烘 NavMesh**（与挖坑同款），若清的是 AI 必经位置要补 `AsyncRefresh(true)`。
- 生成顺序（`GenerateNoiseTerrain.ApplyFractalNoiseToTerrain`）：高度图 → alphamap → `SpawnVegetation`(石/树) → `SetTreeInstances` → Rebuild → `SpawnDetails`(草) → NavMesh。**"树/草之后、NavMesh 之前"是插覆盖物的天然位置**（生成时排除 = 零提交、无过渡帧，不用事后擦）。
- 巨型悬崖**不能"每格概率"**（那是 1~4m 小石的做法）⇒ 改"目标数量/尝试次数 + 最小间距(半径倍数) + 圆互斥 + 坡度/高度过滤"；石头必须放 **Ground 层**才能被同一次 NavMesh 烘焙带走。
- NavMesh 实测参数：agentRadius 0.6 / agentHeight 2 / **agentSlope 60** / **agentClimb 1.5** / voxel 0.2 / tile 128 ⇒ 悬崖坡面 >60° 自动成墙、顶面 ≥2㎡ 才是可行走面。

> 2026-09-27 之前的 daily 正文已归档（标题索引见 `archive/INDEX-legacy-20260927.md`，全文 `git checkout HEAD -- .codebuddy/memory/<日期>.md`）。
