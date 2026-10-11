using System.Collections.Generic;
using System.IO;
using System.Text;
using FPSGame.MapUtils;
using FPSGame.Data;
using UnityEditor;
using UnityEngine;

namespace ProjectEditors
{
    /// <summary>
    /// 【方案A】给每张地图烘一份**专属 <see cref="TerrainData"/>**，运行时由
    /// <c>BattleManager.InitTerrain</c> 赋给 BattleScene 里那个 Terrain。
    ///
    /// <para>▍为什么需要它（2026-10-10 实测的根因）：
    /// <c>TerrainData.treePrototypes</c> / <c>detailPrototypes</c> 的 setter 是 <b>Editor-only</b>
    /// —— 打包运行时调用只会往 Player.log 刷 "only implemented in editor …/DetailDatabase.cpp"，
    /// 而且**什么都不会改**。而战斗地形原本只有**一份** <c>Assets/Art/TerrainData/MainMap.asset</c>
    /// ⇒ 打包端无法"按图换原型"，各图都只能用 MainMap 里烘着的那一套。
    /// 既然运行时改不了，就把"每图的原型"**烘进各自的地形资产**，运行时只换资产（
    /// <c>Terrain.terrainData</c> 的 setter 是运行时 API ✓）。</para>
    ///
    /// <para>▍与运行时的一致性：原型数组一律通过
    /// <see cref="GenerateNoiseTerrain.BuildTreePrototypes"/> / <see cref="GenerateNoiseTerrain.BuildDetailPrototypes"/>
    /// 构造（**单一事实来源**）⇒ 烘出来的与运行时在编辑器里重建的必然相同。</para>
    ///
    /// <para>▍现场生成不受影响：高度图 / 树实例 / 草层数据仍然每局现算（
    /// <c>ApplyFractalNoiseToTerrain</c> / <c>SetTreeInstances</c> / <c>SetDetailLayer</c> 都是运行时 API），
    /// 资产里烘的只是"用哪几个原型"+ 贴图混合图等静态部分。</para>
    ///
    /// <para>⚠ 副作用（已知且有意）：每份资产 ≈ 13MB（高度图 4.2MB + 贴图混合图 6MB 是大头，清不掉）
    /// ⇒ 只给"确实配了原型"的图建资产；未配原型的图 <c>terrainAsset</c> 留空、沿用 MainMap（区间为空，不会生成植被）。</para>
    ///
    /// <para>⚠ 用法：改完某图的 <c>stonePrototypes/treePrototypes/detailPrototypes</c> 后，**打包前**跑一次本菜单
    /// （或跑一次该图的战斗——编辑器里的运行时重建也会写进它自己的资产）。</para>
    /// </summary>
    public static class TerrainDataBaker
    {
        /// <summary>模板：既是 BattleScene 里那个 Terrain 的原始资产，也是"参数（分辨率/层/尺寸）"的来源。</summary>
        private const string TemplatePath = "Assets/Art/TerrainData/MainMap.asset";

        private const string OutputDir = "Assets/Art/TerrainData";
        private const string Prefix = "Map_";

        /// <summary>
        /// 烘焙**单张**地图（`MapData_SO` 检视器上的按钮与整批菜单共用这一份逻辑）。
        /// 返回写入后的文件大小（字节）；<c>-1</c> = 跳过（未配原型）或失败（原因已写进 <paramref name="sb"/>）。
        /// </summary>
        public static long BakeInto(MapData_SO map, StringBuilder sb)
        {
            if (map == null) { sb?.AppendLine("[失败] map 为空"); return -1; }

            int stone = map.stonePrototypes != null ? map.stonePrototypes.Length : 0;
            int tree = map.treePrototypes != null ? map.treePrototypes.Length : 0;
            int detail = map.detailPrototypes != null ? map.detailPrototypes.Length : 0;
            string key = map.name.StartsWith("MD_") ? map.name.Substring(3) : map.name;

            var template = AssetDatabase.LoadAssetAtPath<TerrainData>(TemplatePath);
            if (template == null) { sb?.AppendLine($"[失败] 找不到模板地形资产 {TemplatePath}"); return -1; }

            // 一张原型都没配的图：不需要专属资产 —— 区间为空（(0,-1)），运行时本来就不会种任何东西，
            // 沿用 MainMap 即可（也省下一份资产）。
            if (stone + tree + detail == 0)
            {
                if (map.terrainAsset != null)
                {
                    map.terrainAsset = null;
                    EditorUtility.SetDirty(map);
                }
                sb?.AppendLine($"[跳过] {map.name,-22} 未配任何原型 ⇒ 沿用 MainMap（区间为空，不会生成植被）");
                return -1;
            }

            string target = $"{OutputDir}/{Prefix}{key}.asset";
            var td = AssetDatabase.LoadAssetAtPath<TerrainData>(target);
            bool created = false;
            if (td == null)
            {
                // ⚠ 必须**复制模板**（不能 `new TerrainData()`）：`detailResolution` / `detailResolutionPerPatch`
                //   是**只读属性**（脚本改不了），而 `SpawnDetails` 直接读 `terrainData.detailResolution`
                //   ⇒ 草的最终分辨率**只能靠复制继承**，new 出来是默认值 ⇒ 草立刻变糙。
                if (!AssetDatabase.CopyAsset(TemplatePath, target))
                {
                    sb?.AppendLine($"[失败] {map.name} ⇒ 复制模板到 {target} 失败");
                    return -1;
                }
                td = AssetDatabase.LoadAssetAtPath<TerrainData>(target);
                if (td == null)
                {
                    sb?.AppendLine($"[失败] {map.name} ⇒ 复制后加载不到 {target}");
                    return -1;
                }
                created = true;
            }

            // 复制来的大份（13MB）里绝大部分是**运行时会被重设**的数据 ⇒ 就地缩掉（见 ShrinkRuntimeOwnedData）
            ShrinkRuntimeOwnedData(td);

            // ⚠ 顺序与运行时严格一致：**先**把"要变短的那几层"草清零（数组一换短就再也寻址不到它们），**再**换原型数组。
            int oldDetailCount = td.detailPrototypes != null ? td.detailPrototypes.Length : 0;
            var materialsWithoutInstancing = new List<string>();
            if (map.detailPrototypes != null)
            {
                var detailProtos = GenerateNoiseTerrain.BuildDetailPrototypes(
                    map.detailPrototypes, td.detailPrototypes, materialsWithoutInstancing);
                for (int i = detailProtos.Length; i < oldDetailCount; ++i)
                    td.SetDetailLayer(0, 0, i, new int[td.detailHeight, td.detailWidth]);
                td.detailPrototypes = detailProtos;
            }
            else
            {
                sb?.AppendLine($"       · 细节原型为 null（= 未提供）⇒ 保留模板里的 {oldDetailCount} 层（与运行时同语义）");
            }

            // 再把**所有**细节层清零（用户 2026-10-10 要求）：资产里不带任何一局的草，进战斗时 SpawnDetails
            // 会逐层整层重写 ⇒ 清空零风险，且文件从 1.17MB 掉到 ~49KB（零数据不占序列化空间）。
            ClearAllDetailLayers(td);

            // ⚠⚠ **先清树实例、再换原型数组**（顺序反了会让 Unity 把越界实例逐条报 `Tree removed: invalid prototype N`）。
            if (td.treeInstances.Length > 0)
                td.SetTreeInstances(System.Array.Empty<TreeInstance>(), false);

            td.treePrototypes = GenerateNoiseTerrain.BuildTreePrototypes(
                map.stonePrototypes, map.treePrototypes, td.treePrototypes);
            td.RefreshPrototypes();
            EditorUtility.SetDirty(td);

            if (map.terrainAsset != td)
            {
                map.terrainAsset = td;          // ← 写进 MD_*.asset（配置资产，两端一致、不走网络）
                EditorUtility.SetDirty(map);
            }

            long size = new FileInfo(target).Length;
            sb?.AppendLine($"[烘焙] {map.name,-22} ⇒ {Prefix}{key}.asset{(created ? "（新建）" : "（更新）")}  " +
                          $"石块 {stone} + 树 {tree} ⇒ 树原型 {td.treePrototypes.Length}、" +
                          $"细节层 {td.detailPrototypes.Length}、{size / 1024.0:N0} KB");
            if (materialsWithoutInstancing.Count > 0)
                sb?.AppendLine($"       ⚠ 下列细节材质未勾 Enable GPU Instancing（会静默退回非实例化）：" +
                              string.Join("、", materialsWithoutInstancing));
            return size;
        }

        /// <summary>「烘焙到本图地形资产」——供 `MapData_SO` 检视器按钮调用，返回可读的报告。</summary>
        public static string BakeOne(MapData_SO map)
        {
            var sb = new StringBuilder();
            long size = BakeInto(map, sb);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            if (size >= 0) sb.Append($"⇒ 完成（{size / 1024.0:N0} KB）");
            return sb.ToString();
        }

        /// <summary>
        /// 本图地形资产与地图数据是否一致（供检视器显示状态；**只读、不写盘**）。
        /// </summary>
        public static string StatusOf(MapData_SO map, out bool ok)
        {
            ok = false;
            if (map == null) return "—";

            int stone = map.stonePrototypes != null ? map.stonePrototypes.Length : 0;
            int tree = map.treePrototypes != null ? map.treePrototypes.Length : 0;
            int detail = map.detailPrototypes != null ? map.detailPrototypes.Length : 0;

            if (stone + tree + detail == 0)
            {
                ok = map.terrainAsset == null;
                return ok ? "未配原型 ⇒ 沿用 MainMap（区间为空，不生成植被）"
                          : "⚠ 未配任何原型，却挂着地形资产 ⇒ 建议烘焙一次（会清掉该引用）";
            }

            if (map.terrainAsset == null)
                return "⚠ 未烘焙 ⇒ 打包端只能用 MainMap 的原型（本图植被种类会错）";

            var td = map.terrainAsset;
            bool orderOk = td.treePrototypes.Length == stone + tree;
            for (int i = 0; i < td.treePrototypes.Length && orderOk; ++i)
            {
                var expect = i < stone ? map.stonePrototypes[i] : map.treePrototypes[i - stone];
                orderOk = td.treePrototypes[i].prefab == expect;
            }
            bool detailOk = map.detailPrototypes == null || td.detailPrototypes.Length == detail;

            ok = orderOk && detailOk;
            return ok
                ? $"✓ 已同步（{td.name}：树原型 {td.treePrototypes.Length}、草层 {td.detailPrototypes.Length}）"
                : $"⚠ 与地图数据不一致（{td.name}：树原型 {td.treePrototypes.Length}/{stone + tree}" +
                  $"，顺序{(orderOk ? "对" : "错")}；草层 {td.detailPrototypes.Length}/{detail}）⇒ 再点一次烘焙";
        }

        [MenuItem("Tools/地形/生成每图 TerrainData（方案A）")]
        public static void BakeAll()
        {
            var maps = FindAllMapData();
            var sb = new StringBuilder();
            long totalBytes = 0;
            int baked = 0, shared = 0;

            foreach (var map in maps)
            {
                long one = BakeInto(map, sb);
                if (one < 0) ++shared;
                else { totalBytes += one; ++baked; }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            sb.Insert(0, $"[烘焙] 完成：新建/更新 {baked} 份专属资产（合计 {totalBytes / 1048576.0:N1} MB），" +
                         $"沿用 MainMap 的 {shared} 张\n");
            Debug.Log(sb.ToString());
        }

        [MenuItem("Tools/地形/体检：每图资产 vs 地图数据")]
        public static void Verify()
        {
            var sb = new StringBuilder();
            int bad = 0;
            foreach (var map in FindAllMapData())
            {
                int stone = map.stonePrototypes != null ? map.stonePrototypes.Length : 0;
                int tree = map.treePrototypes != null ? map.treePrototypes.Length : 0;
                int detail = map.detailPrototypes != null ? map.detailPrototypes.Length : 0;

                if (map.terrainAsset == null)
                {
                    // 未配原型 + 空引用 = 正常；配了原型却没烘 = 打包端会用 MainMap 那一套（错配）
                    if (stone + tree + detail > 0)
                    {
                        ++bad;
                        sb.AppendLine($"[✗] {map.name,-22} 未烘焙（terrainAsset 为空）⇒ 打包端会用 MainMap 的原型！" +
                                      "请跑一次「生成每图 TerrainData」");
                    }
                    continue;
                }

                var td = map.terrainAsset;
                int expectTree = stone + tree;
                bool treeOk = td.treePrototypes.Length == expectTree;
                if (treeOk)
                {
                    // 顺带核对"石块在前、树紧随"的顺序（区间语义的根基）
                    for (int i = 0; i < expectTree && treeOk; ++i)
                    {
                        var expect = i < stone ? map.stonePrototypes[i] : map.treePrototypes[i - stone];
                        treeOk = td.treePrototypes[i].prefab == expect;
                    }
                }
                bool detailOk = map.detailPrototypes == null || td.detailPrototypes.Length == detail;

                if (!treeOk || !detailOk)
                {
                    ++bad;
                    sb.AppendLine($"[✗] {map.name,-22} 资产与地图数据不一致：树原型 {td.treePrototypes.Length}/{expectTree}（顺序" +
                                  (treeOk ? "对" : "错") + $"）、细节层 {td.detailPrototypes.Length}/{detail} ⇒ 重跑烘焙");
                }
                else
                {
                    sb.AppendLine($"[✓] {map.name,-22} {Prefix}{(map.name.StartsWith("MD_") ? map.name.Substring(3) : map.name)}.asset " +
                                  $"树原型 {expectTree}、细节层 {td.detailPrototypes.Length}");
                }
            }
            sb.Insert(0, bad == 0 ? "[体检] 全部一致 ✓\n" : $"[体检] 有 {bad} 张图不一致 ✗\n");
            Debug.Log(sb.ToString());
        }

        /// <summary>
        /// 把**运行时一定会重设**的那两项缩到最小，让每份专属资产从 13MB 掉到"只剩细节层"（1~7MB）。
        ///
        /// <para>▍依据（都实测过）：</para>
        /// <list type="bullet">
        ///   <item><c>BattleManager.InitTerrain</c> 每次进战斗都设 <c>heightmapResolution = mapRes + 1</c>、
        ///         <c>alphamapResolution = mapRes</c>、<c>size = (MapSize, MapHeight, MapSize)</c>
        ///         （且注释写明"不能调换顺序，会出问题"）⇒ 资产里的高度图（4.2MB）与新尺寸无关；</item>
        ///   <item>随后 <c>ApplyAlphamapsInChunks</c> 用 <c>SetAlphamaps</c> 把贴图混合图**整张重写**
        ///         ⇒ 资产里的混合图（6MB）也是废数据。</item>
        /// </list>
        ///
        /// <para>⚠ 反过来 <c>detailResolution</c> / <c>detailResolutionPerPatch</c> **运行时不会重设**
        /// （而且它们是**只读属性**，脚本无从赋值）⇒ 只能靠"复制模板"继承，绝不能 new 一个新资产。</para>
        /// </summary>
        private static void ShrinkRuntimeOwnedData(TerrainData td)
        {
            td.heightmapResolution = 33;   // 2^n+1 里最小的合法值；运行时会被改成 513 / 1025
            td.alphamapResolution = 16;    // 运行时会被改成 512 / 1024，并且混合图随后整张重写
        }

        /// <summary>
        /// 把资产里**所有**细节（草）层清零 —— 草是"现场生成"的，资产里不该留任何一局的数据。
        /// <para>▍零风险依据：运行时 <c>SpawnDetails</c> 会遍历 <c>detailPrototypes.Length</c> 逐层
        /// <c>SetDetailLayer</c> **整层重写**（不撒草的分支提交全零）⇒ 清空只是"提前把废数据扔掉"。
        /// 各层复用同一块零数组，避免逐层分配整张细节图（与运行时 <c>ClearDetailLayers</c> 同做法）。</para>
        /// </summary>
        private static void ClearAllDetailLayers(TerrainData td)
        {
            int count = td.detailPrototypes != null ? td.detailPrototypes.Length : 0;
            if (count == 0 || td.detailWidth <= 0 || td.detailHeight <= 0) return;

            int[,] empty = new int[td.detailHeight, td.detailWidth];
            for (int i = 0; i < count; ++i)
                td.SetDetailLayer(0, 0, i, empty);
        }

        private static List<MapData_SO> FindAllMapData()
        {
            var maps = new List<MapData_SO>();
            foreach (var guid in AssetDatabase.FindAssets("t:MapData_SO"))
            {
                var so = AssetDatabase.LoadAssetAtPath<MapData_SO>(AssetDatabase.GUIDToAssetPath(guid));
                if (so != null) maps.Add(so);
            }
            // 顺序只为日志好看（别依赖 AssetDatabase 的枚举顺序——见 OrderedMaps 的教训）
            maps.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
            return maps;
        }
    }
}
