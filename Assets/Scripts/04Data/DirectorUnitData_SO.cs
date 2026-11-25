using System;
using System.Collections.Generic;
using UnityEngine;

namespace FPSGame.Data
{
    /// <summary>PV 导演面板里单位列表的分组。</summary>
    public enum DirectorUnitCategory
    {
        [InspectorName("敌人")] Enemy = 0,
        [InspectorName("友军")] Friend = 1,
        [InspectorName("载具")] Vehicle = 2,
        [InspectorName("道具")] Prop = 3,
    }

    [Serializable]
    public class DirectorUnitEntry
    {
        [InspectorName("显示名")]
        public string showName;

        [InspectorName("预制体")]
        public GameObject prefab;

        [InspectorName("分类")]
        public DirectorUnitCategory category;

        /// <summary>0 = 精确落在标记点；&gt;0 时在圆内随机抖开，用于一次铺开一小队。</summary>
        [InspectorName("落点抖动半径")]
        public float scatterRange;
    }

    /// <summary>
    /// PV 导演模式的可生成单位白名单。
    /// <para>▍为什么要独立一张表：`WaveManager.CreatUnit(UnitTier)` 依赖 <c>TaskManager.nowTask.campData</c>，
    /// 非任务场景（纯布景拍摄）会空引用；`SpawnableTable_SO` 是联机号段表，塞拍摄用 prefab 会污染同步。</para>
    /// </summary>
    [CreateAssetMenu(fileName = "DirectorUnitData", menuName = "Data/PV导演单位表")]
    public class DirectorUnitData_SO : ScriptableObject
    {
        [InspectorName("可用单位")]
        public List<DirectorUnitEntry> units = new();
    }
}
