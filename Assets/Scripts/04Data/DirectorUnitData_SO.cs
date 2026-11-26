using System;
using System.Collections.Generic;
using UnityEngine;

namespace FPSGame.Data
{
    /// <summary>导演面板里的一组可生成单位（面板上「组名」是一行小标题，下面是可拖拽的 prefab 行）。</summary>
    [Serializable]
    public struct DirectorUnitGroup
    {
        [InspectorName("组名")]
        public string name;

        /// <summary>列表显示名与生成后的物体名都取 prefab 名。</summary>
        [InspectorName("单位预制体")]
        public List<GameObject> units;
    }

    /// <summary>
    /// PV 导演模式的可生成单位白名单。
    /// <para>▍为什么要独立一张表：`WaveManager.CreatUnit(UnitTier)` 依赖 <c>TaskManager.nowTask.campData</c>，
    /// 非任务场景（纯布景拍摄）会空引用；`SpawnableTable_SO` 是联机号段表，塞拍摄用 prefab 会污染同步。</para>
    /// </summary>
    [CreateAssetMenu(fileName = "DirectorUnitData", menuName = "Data/PV导演单位表")]
    public class DirectorUnitData_SO : ScriptableObject
    {
        [InspectorName("单位分组")]
        public List<DirectorUnitGroup> groups = new();
    }
}
