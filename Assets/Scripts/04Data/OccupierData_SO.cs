using System.Collections.Generic;
using UnityEngine;

namespace FPSGame.Data
{
    /// <summary>某个势力在某个任务点上的占领度（百分比整数 0~100；UI 直接 /100 当进度条）。</summary>
    [System.Serializable]
    public class OccupierEntry
    {
        [InspectorName("势力")]
        public string name;

        [InspectorName("占领度")]
        public int value;
    }

    /// <summary>
    /// 一个任务点的势力表。⚠ 下标 == <c>MapData_SO.mapItemInfos</c> 的下标（与 <c>TaskCfgs</c> 的列同源），
    /// <c>pointName</c> 只用于编辑期对齐（检视器的「重建」按名字保住已填数值），运行时不按名字查。
    /// </summary>
    [System.Serializable]
    public class OccupierPointData
    {
        [InspectorName("任务点")]
        public string pointName;

        [InspectorName("势力")]
        public List<OccupierEntry> occupiers = new List<OccupierEntry>();
    }

    /// <summary>一张地图的势力表。<c>mapId</c> = 地图资产名去掉 <c>MD_</c> 前缀（= <c>TaskManager.MapData</c> 的键）。</summary>
    [System.Serializable]
    public class OccupierMapData
    {
        [InspectorName("地图")]
        public string mapId;

        [InspectorName("任务点")]
        public List<OccupierPointData> points = new List<OccupierPointData>();
    }

    /// <summary>势力名册的一项：名字（占领度行按它引用）+ 图标（选图界面左卡片用）+ 是否友方。</summary>
    [System.Serializable]
    public class OccupierFaction
    {
        [InspectorName("势力")]
        public string name;

        [InspectorName("图标")]
        public Sprite icon;

        /// <summary>友方势力：玩家打赢后控制度涨给它（本局任务点里有友方势力时）。
        /// 一个友方势力都没有才涨给 <see cref="OccupierData_SO.SuperFactionName"/>。</summary>
        [InspectorName("友方")]
        [Tooltip("打赢后控制度涨给这个势力。本局任务点里没有任何友方势力时，才涨给「超级夏莱」。")]
        public bool isFriendly;
    }

    /// <summary>
    /// 地图势力占领度（原 <c>Resources/GameData/Occupier_Default.json</c> 转成的资产）。
    ///
    /// <para>▍为什么是「每张图的**每个任务点**一份」：占领度描述的是**具体据点**的归属。整图一份时，
    /// 阿拜多斯的遗迹区和主校区只能共用一组数字。任务点身份 = <c>MapData_SO.mapItemInfos</c> 下标。</para>
    ///
    /// <para>▍为什么从 json 改成 SO：json 只能手改文本、没有校验也看不到结构；改成资产后走
    /// <c>Assets/Editor/OccupierDataSOEditor</c>（按 MapData_SO 对齐任务点 / 名册下拉 / 合计校验）。
    /// 它**只是设计期配置**，运行时不写回 ⇒ 原来的落盘与联机整份下发一并取消（见 <c>OccupierState</c>）。</para>
    ///
    /// <para>▍图标也在这（2026-10-28）：原来在 <c>TaskManager.OccupierIcon</c> 那张
    /// <c>DisplayDic&lt;string, Sprite&gt;</c> 里（配在 TaskManager 预制体上），名字与图标被拆在两处、
    /// 打错字只会静默回落到默认图；现在名册一项 = 名字 + 图标，UI 走 <c>OccupierState.GetIcon</c>。</para>
    /// </summary>
    [CreateAssetMenu(fileName = "OccupierData", menuName = "Data/势力占领度")]
    public class OccupierData_SO : ScriptableObject
    {
        /// <summary>唯一一份势力表在 Resources 下的路径（<c>OccupierState</c> 按它取，别在别处再拼路径）。</summary>
        public const string ResourcePath = "GameData/OccupierData";

        /// <summary>吃"玩家战果"的那个势力名（结算时涨控制度、其余势力等分让出）。
        /// <para>⚠ 必须与 <see cref="factions"/> 里的名字逐字一致（检视器会在名册缺它时报黄字）。</para></summary>
        public const string SuperFactionName = "超级夏莱";

        [InspectorName("势力名册")]
        [Tooltip("名字 = 占领度行引用的键，也是运行时取图标的键；图标留空 ⇒ 选图界面那一行没图。")]
        public List<OccupierFaction> factions = new List<OccupierFaction>();

        [InspectorName("地图势力表")]
        public List<OccupierMapData> maps = new List<OccupierMapData>();

        /// <summary>按 mapId 找地图条目（没有 ⇒ null，**不建占位条目**）。</summary>
        public OccupierMapData FindMapData(string mapId)
        {
            if (string.IsNullOrEmpty(mapId)) return null;
            return maps.Find(m => m != null && m.mapId == mapId);
        }

        /// <summary>某地图的任务点列表（没有这张图 ⇒ null）。</summary>
        public List<OccupierPointData> GetPoints(string mapId)
        {
            var map = FindMapData(mapId);
            return map != null ? map.points : null;
        }

        /// <summary>某地图某任务点的条目（越界 / 无此图 ⇒ null）。</summary>
        public OccupierPointData GetPoint(string mapId, int pointIndex)
        {
            var points = GetPoints(mapId);
            if (points == null || pointIndex < 0 || pointIndex >= points.Count) return null;
            return points[pointIndex];
        }

        /// <summary>按名字找名册项（名册里没有 ⇒ null）。</summary>
        public OccupierFaction FindFaction(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            return factions.Find(f => f != null && f.name == name);
        }

        /// <summary>按名字取势力图标（名册里没有 / 图标没配 ⇒ null）。</summary>
        public Sprite GetIcon(string name)
        {
            var faction = FindFaction(name);
            return faction != null ? faction.icon : null;
        }

        /// <summary>该势力是否友方（名册里没有这个名字 ⇒ false）。</summary>
        public bool IsFriendly(string name)
        {
            var faction = FindFaction(name);
            return faction != null && faction.isFriendly;
        }
    }
}
