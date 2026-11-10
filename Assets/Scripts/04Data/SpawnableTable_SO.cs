using System;
using System.Collections.Generic;
using UnityEngine;

namespace FPSGame.Data
{
    /// <summary>
    /// 「可被网络同步创建」的预制体总表（编号 → prefab），房主只发 int，成员端靠这张表还原 prefab。
    ///
    /// <para>▍为什么需要它：<c>GameObject</c> 过不了网 ⇒ 4063 现在靠「生成者自己持表 + 槽位」绕开这件事，
    /// 代价是**成员端必须已经有那个生成者**。有这张全局表就不需要生成者，任意组件都能请求"在某处造一个单位"。</para>
    ///
    /// <para>▍⚠ 它只解决「造什么」；「这是第几份」仍必须由房主分配（<c>ActorsManager.NextHostSpawnNetId</c>）——
    /// 同一个 prefab 会被造 N 份，实例号不能静态填。</para>
    ///
    /// <para>▍写入方：编辑器（`Tools/可生成单位表` 扫描 <see cref="FPSGame.Core.NetObject"/> 重建）；
    /// 读取方：<c>ResSvc.Init</c> 注入一次 + 运行时 <see cref="TryGet"/>（只读，别在运行时改它）。</para>
    /// </summary>
    [CreateAssetMenu(fileName = "SpawnableTable", menuName = "Data/可生成单位表")]
    public class SpawnableTable_SO : ScriptableObject
    {
        /// <summary>一条「编号 → 预制体」映射。</summary>
        [Serializable]
        public struct Entry
        {
            [InspectorName("编号")]
            public int Id;
            [InspectorName("预制体")]
            public GameObject Prefab;
        }

        [SerializeField] private Entry[] _entries = Array.Empty<Entry>();

        /// <summary>运行时缓存（由 <see cref="Inject"/> 建；null = 还没注入 ⇒ <see cref="TryGet"/> 恒 false）。</summary>
        private static Dictionary<int, GameObject> _dict;

        /// <summary>编辑器用（扫描重建后要读出来做校验）。运行时不要拿它乱改。</summary>
        public Entry[] Entries => _entries;

        /// <summary>装配根注入点（<c>ResSvc.Init</c>）。重复编号保留先出现的并报错 —— 这是策划可改的表，属于不可控输入。</summary>
        public static void Inject(SpawnableTable_SO table)
        {
            _dict = new Dictionary<int, GameObject>();
            if (table == null)
            {
                // 不是致命错误：没配表时整套按 id 生成都不会工作，一条 Warn 足够定位
                Debug.LogWarning("[SpawnableTable] 没找到 Resources/GameData/SpawnableTable ⇒ 所有按编号生成都会失败");
                return;
            }

            for (int i = 0; i < table._entries.Length; ++i)
            {
                Entry e = table._entries[i];
                if (e.Id == 0 || e.Prefab == null) continue;

                if (_dict.ContainsKey(e.Id))
                {
                    Debug.LogError($"[SpawnableTable] 编号重复 {e.Id}：「{e.Prefab.name}」被忽略（保留先出现的那条）");
                    continue;
                }
                _dict.Add(e.Id, e.Prefab);
            }
        }

        /// <summary>按编号取预制体（未收录 / 未注入 ⇒ false）。</summary>
        public static bool TryGet(int id, out GameObject prefab)
        {
            prefab = null;
            return _dict != null && _dict.TryGetValue(id, out prefab);
        }
    }
}
