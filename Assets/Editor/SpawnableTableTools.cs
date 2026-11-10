using System.Collections.Generic;
using FPSGame.Core;
using FPSGame.Data;
using UnityEditor;
using UnityEngine;

namespace FPSGame.EditorExt
{
    /// <summary>
    /// 可生成单位表的编辑器工具：找表 / 建表 / 扫描全仓 <see cref="NetObject"/> 重建 / 统计重复编号。
    /// <para>▍检视器与独立窗口共用这一份（扫描要给 1100+ 个预制体刷进度条，别复制两份）。</para>
    /// </summary>
    public static class SpawnableTableTools
    {
        public const string Dir = "Assets/Resources/GameData";
        public const string AssetPath = Dir + "/SpawnableTable.asset";

        /// <summary>找唯一那张表；createIfMissing 时在 Resources/GameData 下建一张。</summary>
        public static SpawnableTable_SO FindTable(bool createIfMissing)
        {
            string[] guids = AssetDatabase.FindAssets("t:SpawnableTable_SO");
            if (guids.Length > 0)
                return AssetDatabase.LoadAssetAtPath<SpawnableTable_SO>(AssetDatabase.GUIDToAssetPath(guids[0]));

            if (!createIfMissing) return null;

            if (!AssetDatabase.IsValidFolder(Dir))
            {
                if (!AssetDatabase.IsValidFolder("Assets/Resources"))
                    AssetDatabase.CreateFolder("Assets", "Resources");
                AssetDatabase.CreateFolder("Assets/Resources", "GameData");
            }

            var asset = ScriptableObject.CreateInstance<SpawnableTable_SO>();
            AssetDatabase.CreateAsset(asset, AssetPath);
            AssetDatabase.SaveAssets();
            Debug.Log($"[SpawnableTable] 已新建 {AssetPath}");
            return asset;
        }

        /// <summary>扫描全工程带 <see cref="NetObject"/> 的预制体（**只认根组件**）并覆盖写入表。
        /// <returns>true = 已写入；false = 用户取消。</returns></summary>
        public static bool Rebuild(SpawnableTable_SO table)
        {
            if (table == null) return false;

            string[] guids = AssetDatabase.FindAssets("t:Prefab");
            var ids = new List<int>(guids.Length);
            var pfs = new List<GameObject>(guids.Length);

            bool canceled = false;
            try
            {
                for (int i = 0; i < guids.Length; ++i)
                {
                    // 进度条本身很贵 ⇒ 每 64 个刷一次
                    if ((i & 63) == 0
                        && EditorUtility.DisplayCancelableProgressBar("扫描可生成预制体",
                            AssetDatabase.GUIDToAssetPath(guids[i]), i / (float)guids.Length))
                    {
                        canceled = true;
                        break;
                    }

                    var go = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guids[i]));
                    if (go == null) continue;

                    var net = go.GetComponent<NetObject>();   // ⚠ 只认根：子物体上的编号不参与
                    if (net == null) continue;
                    ids.Add(net.Id);
                    pfs.Add(go);
                }
            }
            finally { EditorUtility.ClearProgressBar(); }

            if (canceled) return false;

            // 按编号升序写回（0 排最后），便于肉眼比对
            var order = new List<int>(ids.Count);
            for (int i = 0; i < ids.Count; ++i) order.Add(i);
            order.Sort((a, b) => SortKey(ids[a]).CompareTo(SortKey(ids[b])));

            var so = new SerializedObject(table);
            var arr = so.FindProperty("_entries");
            arr.arraySize = order.Count;
            for (int i = 0; i < order.Count; ++i)
            {
                var elem = arr.GetArrayElementAtIndex(i);
                elem.FindPropertyRelative("Id").intValue = ids[order[i]];
                elem.FindPropertyRelative("Prefab").objectReferenceValue = pfs[order[i]];
            }
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(table);
            AssetDatabase.SaveAssets();

            Debug.Log($"[SpawnableTable] 扫描完成：{guids.Length} 个预制体里收录 {order.Count} 个带网络编号的。");
            return true;
        }

        /// <summary>重复编号统计（编号 → 出现次数，只返回次数 &gt; 1 的）。没有序列化上下文时用这个做摘要。</summary>
        public static Dictionary<int, int> CountDuplicates(SpawnableTable_SO table)
        {
            var result = new Dictionary<int, int>();
            var entries = table != null ? table.Entries : null;
            if (entries == null) return result;

            for (int i = 0; i < entries.Length; ++i)
            {
                int id = entries[i].Id;
                if (id == 0 || entries[i].Prefab == null) continue;
                result.TryGetValue(id, out int n);
                result[id] = n + 1;
            }

            // 就地剔除只出现一次的（倒序遍历删除才安全）
            var once = new List<int>();
            foreach (var kv in result) if (kv.Value < 2) once.Add(kv.Key);
            for (int i = 0; i < once.Count; ++i) result.Remove(once[i]);
            return result;
        }

        private static int SortKey(int id) => id == 0 ? int.MaxValue : id;
    }
}
