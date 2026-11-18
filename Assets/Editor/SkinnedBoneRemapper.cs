using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace FPSGame.EditorExt
{
    /// <summary>
    /// 把 SkinnedMeshRenderer 的 bones 按名称重新映射到另一套骨架。
    /// 用法：先把 rootBone 换成新骨架里的骨骼（工具以它定位搜索域），再在组件标题右键执行。
    /// 陷阱：只改 bones，不动 rootBone/bindposes；两套骨架 rest 姿态不一致时蒙皮会拉扯。
    /// </summary>
    public static class SkinnedBoneRemapper
    {
        private const string MenuPath = "CONTEXT/SkinnedMeshRenderer/重新映射骨骼(按名称)";

        [MenuItem(MenuPath)]
        private static void Remap(MenuCommand command)
        {
            var smr = command.context as SkinnedMeshRenderer;
            if (smr == null)
            {
                return;
            }

            Transform anchor = smr.rootBone;
            if (anchor == null)
            {
                Debug.LogError("[骨骼重映射] rootBone 为空：请先把它换成新骨架里的骨骼，工具用它定位搜索范围。", smr);
                return;
            }

            Transform[] oldBones = smr.bones;
            if (oldBones == null || oldBones.Length == 0)
            {
                Debug.LogWarning("[骨骼重映射] 该渲染器未绑定任何骨骼，无需处理。", smr);
                return;
            }

            var names = new HashSet<string>();
            for (int i = 0; i < oldBones.Length; i++)
            {
                if (oldBones[i] != null)
                {
                    names.Add(oldBones[i].name);
                }
            }

            Transform scope = FindScope(anchor, names);
            var duplicates = new List<string>();
            Dictionary<string, Transform> map = BuildNameMap(scope, duplicates);

            var newBones = new Transform[oldBones.Length];
            var missing = new StringBuilder();
            int changed = 0;
            for (int i = 0; i < oldBones.Length; i++)
            {
                Transform old = oldBones[i];
                if (old == null)
                {
                    continue;
                }

                if (map.TryGetValue(old.name, out Transform found))
                {
                    newBones[i] = found;
                    if (found != old)
                    {
                        changed++;
                    }
                }
                else
                {
                    newBones[i] = old;
                    if (missing.Length > 0)
                    {
                        missing.Append(", ");
                    }
                    missing.Append(old.name);
                }
            }

            Undo.RecordObject(smr, "重新映射骨骼");
            smr.bones = newBones;
            EditorUtility.SetDirty(smr);
            if (PrefabUtility.IsPartOfPrefabInstance(smr))
            {
                PrefabUtility.RecordPrefabInstancePropertyModifications(smr);
            }

            GameObject host = smr.gameObject;
            if (!EditorUtility.IsPersistent(host))
            {
                EditorSceneManager.MarkSceneDirty(host.scene);
            }

            Debug.Log($"[骨骼重映射] {smr.name}：搜索域 {GetPath(scope)}，{changed}/{oldBones.Length} 根骨骼已改指向。", smr);
            if (duplicates.Count > 0)
            {
                Debug.LogWarning($"[骨骼重映射] 搜索域内存在同名骨骼，已取首个：{string.Join(", ", duplicates)}", smr);
            }

            if (missing.Length > 0)
            {
                Debug.LogError($"[骨骼重映射] 以下骨骼在搜索域内找不到同名项，保持原引用：{missing}", smr);
            }
            else if (changed == 0)
            {
                Debug.LogWarning("[骨骼重映射] 骨骼原本就指向该搜索域，未产生变化：确认 rootBone 是否已换成新骨架。", smr);
            }
        }

        /// <summary>
        /// 从 anchor 起向上扩展到「刚好覆盖全部骨骼名」的最小层，避免爬到场景根误匹配别的角色。
        /// 覆盖数不再增长即停：anchor 已是骨架根时原地不动。
        /// </summary>
        private static Transform FindScope(Transform anchor, HashSet<string> names)
        {
            Transform scope = anchor;
            int best = CountCovered(scope, names);
            while (best < names.Count && scope.parent != null)
            {
                int parentCount = CountCovered(scope.parent, names);
                if (parentCount <= best)
                {
                    break;
                }

                scope = scope.parent;
                best = parentCount;
            }

            return scope;
        }

        private static int CountCovered(Transform scope, HashSet<string> names)
        {
            int count = 0;
            var seen = new HashSet<string>();
            Transform[] all = scope.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < all.Length; i++)
            {
                string n = all[i].name;
                if (names.Contains(n) && seen.Add(n))
                {
                    count++;
                }
            }

            return count;
        }

        private static Dictionary<string, Transform> BuildNameMap(Transform scope, List<string> duplicates)
        {
            var map = new Dictionary<string, Transform>();
            Transform[] all = scope.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < all.Length; i++)
            {
                Transform t = all[i];
                if (map.ContainsKey(t.name))
                {
                    if (!duplicates.Contains(t.name))
                    {
                        duplicates.Add(t.name);
                    }

                    continue;
                }

                map.Add(t.name, t);
            }

            return map;
        }

        private static string GetPath(Transform t)
        {
            string path = t.name;
            while (t.parent != null)
            {
                t = t.parent;
                path = t.name + "/" + path;
            }

            return path;
        }
    }
}
