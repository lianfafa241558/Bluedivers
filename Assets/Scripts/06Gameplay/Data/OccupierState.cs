using System.Collections.Generic;
using FPSGame.Data;
using FPSGame.GameContract;
using UnityEngine;

namespace FPSGame.GameData
{
    /// <summary>占领度显示项（浮点：基础值是 SO 里的整数百分比，玩家打出来的控制度增量带小数）。</summary>
    public struct OccupierValue
    {
        public string name;
        public float value;
    }

    /// <summary>
    /// 地图势力占领度的**唯一读取入口**。
    ///
    /// <para>▍两层数据：**基础值**在设计期资产 <see cref="OccupierData_SO"/>（全项目一份，走 Resources）；
    /// **增量**在个人存档 <see cref="ArchivesData_SO.occupierProgress"/>（玩家打赢任务点涨出来的）。
    /// 显示值 = 基础值 + 增量，在这里合成 —— 别在 UI 侧再拼一遍。</para>
    ///
    /// <para>▍增量口径（2026-10-28 用户定）：在**本局打的那个任务点**上，涨
    /// <c>0.1% + 0.03% × 难度等级</c> —— 涨给谁见 <see cref="BeneficiaryOf"/>：本点里有**友方势力**就涨给它，
    /// 一个都没有才涨给 <see cref="OccupierData_SO.SuperFactionName"/>。
    /// 其余势力**等分让出**同样多（每个 −增量/其他势力数，各自钳到 ≥0）。</para>
    /// <para>只存「谁 累计涨了多少」这一个数（按点 + 势力记）：显示值随时可按基础值重算，
    /// 不会像逐个改值那样越算越飘。</para>
    ///
    /// <para>▍⚠ 这是**个人存档**里的进度（用户口径）：联机时每人各记一份，不做同步。
    /// 若要改成"全房间共享"，得另立房主权威状态，别把增量塞回 SO（打包版的 Resources 只读）。</para>
    /// </summary>
    public static class OccupierState
    {
        /// <summary>胜利基础奖励（百分点）。</summary>
        public const float BaseGain = 1f;

        /// <summary>难度每高 1 级的额外奖励（百分点）——<c>DifficultyEnum</c> 的 int 值即等级。</summary>
        public const float GainPerDifficulty = 0.3f;

        /// <summary>「没有势力」的共享空表。⚠ 只读，调用方不得修改。</summary>
        private static readonly List<OccupierValue> Empty = new List<OccupierValue>();

        private static OccupierData_SO _data;
        private static bool _loadFailed;

        /// <summary>整份势力表。缺资产 ⇒ 报一次错并返回 null（选图界面每次悬停都会走到这里，别刷屏）。</summary>
        public static OccupierData_SO Data
        {
            get
            {
                if (_data == null && !_loadFailed)
                {
                    _data = Resources.Load<OccupierData_SO>(OccupierData_SO.ResourcePath);
                    if (_data == null)
                    {
                        _loadFailed = true;
                        Debug.LogError($"[势力] 缺少 Resources/{OccupierData_SO.ResourcePath} 资产，选图界面不会显示势力。");
                    }
                }
                return _data;
            }
        }

        /// <summary>本局应得的控制度增量：<c>1% + 0.3% × 难度等级</c>。</summary>
        public static float GainFor(DifficultyEnum difficulty) => BaseGain + GainPerDifficulty * (int)difficulty;

        /// <summary>某任务点的势力表（已叠加存档增量）。<paramref name="pointIndex"/> = <c>MapData_SO.mapItemInfos</c> 下标。</summary>
        public static List<OccupierValue> Get(string mapId, int pointIndex)
        {
            var data = Data;
            var point = data != null ? data.GetPoint(mapId, pointIndex) : null;
            if (point == null) return Empty;
            return BuildView(mapId, pointIndex, point.occupiers);
        }

        /// <summary>
        /// 整张图的势力概况 = **同一势力在各地任务点的平均值**（没有该势力的任务点按 0 计入），按占领度降序。
        /// <para>▍给"还没进图"的左卡片用；进图后按任务点显示真实值。</para>
        /// <para>⚠ 每次调用都新建一张表（悬停时调一次即可），别放进 Update。</para>
        /// </summary>
        public static List<OccupierValue> Average(string mapId)
        {
            var data = Data;
            var points = data != null ? data.GetPoints(mapId) : null;
            if (points == null || points.Count == 0) return Empty;

            var sums = new Dictionary<string, float>();
            for (int i = 0; i < points.Count; ++i)
            {
                var view = Get(mapId, i);   // 已经叠加过该点的增量
                for (int u = 0; u < view.Count; ++u)
                {
                    sums.TryGetValue(view[u].name, out float sum);
                    sums[view[u].name] = sum + view[u].value;
                }
            }
            if (sums.Count == 0) return Empty;

            var re = new List<OccupierValue>(sums.Count);
            foreach (var kv in sums)
                re.Add(new OccupierValue { name = kv.Key, value = kv.Value / points.Count });
            re.Sort((a, b) => b.value.CompareTo(a.value));
            return re;
        }

        /// <summary>势力图标（选图界面左卡片用）。名册里没这个名字 / 没配图 ⇒ null。</summary>
        public static Sprite GetIcon(string name)
        {
            var data = Data;
            return data != null ? data.GetIcon(name) : null;
        }

        /// <summary>
        /// 某任务点的**受益势力**：本点里第一个友方势力（名册上勾了「友方」的）；
        /// 一个友方势力都没有 ⇒ 兜底 <see cref="OccupierData_SO.SuperFactionName"/>。
        /// <para>▍为什么要兜底：新区域还没有友方阵营，但玩家的战果总得记在某处。</para>
        /// </summary>
        public static string BeneficiaryOf(string mapId, int pointIndex)
        {
            var data = Data;
            var point = data != null ? data.GetPoint(mapId, pointIndex) : null;
            return BeneficiaryOf(point != null ? point.occupiers : null);
        }

        /// <summary>
        /// 【结算】给某任务点的受益势力加控制度，其余势力等分让出。写进存档但**不落盘**，
        /// 由调用方统一 <see cref="ArchivesData_SO.Save"/>（结算界面本来就在结尾存一次）。
        /// <para>返回实际加了多少（已封顶到 100%，加不进去 ⇒ 0）。</para>
        /// </summary>
        public static float AddControl(string mapId, int pointIndex, float gain)
        {
            var arch = ArchivesData_SO.Current;
            if (arch == null)
            {
                Debug.LogError("[势力] 结算加控制度时存档还没就绪（ArchivesData_SO.Current 为空），本次不记录");
                return 0f;
            }

            var point = Data != null ? Data.GetPoint(mapId, pointIndex) : null;
            if (point == null || gain <= 0f)
            {
                Debug.LogError($"[势力] 结算加控制度失败：{mapId} 第 {pointIndex} 个任务点在 OccupierData 里没有条目");
                return 0f;
            }

            string faction = BeneficiaryOf(point.occupiers);
            float baseValue = BaseValueOf(point.occupiers, faction);
            float room = 100f - baseValue - arch.GetOccupierGain(mapId, pointIndex, faction);
            float applied = Mathf.Min(gain, room);
            if (applied <= 0f) return 0f;

            arch.AddOccupierGain(mapId, pointIndex, faction, applied);
            return applied;
        }

        //========================================================
        //  内部
        //========================================================

        private static string BeneficiaryOf(List<OccupierEntry> list)
        {
            var data = Data;
            if (list != null && data != null)
            {
                for (int i = 0; i < list.Count; ++i)
                {
                    var e = list[i];
                    if (e == null || string.IsNullOrEmpty(e.name)) continue;
                    if (data.IsFriendly(e.name)) return e.name;
                }
            }
            return OccupierData_SO.SuperFactionName;
        }

        private static float BaseValueOf(List<OccupierEntry> list, string faction)
        {
            if (list == null) return 0f;
            for (int i = 0; i < list.Count; ++i)
            {
                var e = list[i];
                if (e != null && e.name == faction) return e.value;
            }
            return 0f;
        }

        /// <summary>
        /// 基础值 + 增量 ⇒ 显示值。吃了增量的势力（通常就一个）拿自己的增量，
        /// 其余势力各让 <c>增量总和 / 其他势力数</c>。
        /// </summary>
        private static List<OccupierValue> BuildView(string mapId, int pointIndex, List<OccupierEntry> list)
        {
            if (list == null || list.Count == 0) return Empty;

            var arch = ArchivesData_SO.Current;
            float totalGain = arch != null ? arch.GetOccupierTotalGain(mapId, pointIndex) : 0f;

            var re = new List<OccupierValue>(list.Count + 1);
            if (totalGain <= 0f)
            {
                // 还没打过 ⇒ 就是基础值，别白算
                for (int i = 0; i < list.Count; ++i)
                {
                    var e = list[i];
                    if (e == null || string.IsNullOrEmpty(e.name)) continue;
                    re.Add(new OccupierValue { name = e.name, value = e.value });
                }
                return re;
            }

            int others = 0;
            for (int i = 0; i < list.Count; ++i)
            {
                var e = list[i];
                if (e == null || string.IsNullOrEmpty(e.name)) continue;
                if (arch.GetOccupierGain(mapId, pointIndex, e.name) <= 0f) ++others;
            }
            float each = others > 0 ? totalGain / others : 0f;

            for (int i = 0; i < list.Count; ++i)
            {
                var e = list[i];
                if (e == null || string.IsNullOrEmpty(e.name)) continue;
                float own = arch.GetOccupierGain(mapId, pointIndex, e.name);
                float v = own > 0f ? e.value + own : e.value - each;
                re.Add(new OccupierValue { name = e.name, value = Mathf.Clamp(v, 0f, 100f) });
            }

            // 受益势力不在本点列表里（这一点原本没配它）⇒ 补一行，否则玩家看不到自己的战果
            string beneficiary = BeneficiaryOf(list);
            float beneficiaryGain = arch.GetOccupierGain(mapId, pointIndex, beneficiary);
            if (beneficiaryGain > 0f && !re.Exists(v => v.name == beneficiary))
                re.Add(new OccupierValue { name = beneficiary, value = Mathf.Clamp(beneficiaryGain, 0f, 100f) });

            return re;
        }
    }
}
