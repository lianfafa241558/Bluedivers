using System.Collections.Generic;
using System.Text;
using Unity.FPS.Game;
using UnityEditor;
using UnityEngine;

namespace Unity.FPS.EditorExt
{
    /// <summary>
    /// 单位半高度批量设置工具
    /// 遍历 Assets 下所有带 <see cref="Actor"/> 的预制体，把 Actor 的「半高度」设置为
    /// 瞄准点(AimPoint) 相对该 Actor 物体的局部 Y 坐标。
    /// 菜单：Tools/单位半高度批量设置
    /// </summary>
    public class ActorHalfHeightTool : EditorWindow
    {
        private const string HalfHeightField = "halfHeight";

        private class Item
        {
            /// <summary>预制体资源路径</summary>
            public string PrefabPath;
            /// <summary>Actor 在预制体内的相对路径</summary>
            public string ActorPath;
            /// <summary>当前(旧)值</summary>
            public float OldValue;
            /// <summary>由 AimPoint 计算出的新值</summary>
            public float NewValue;
            /// <summary>是否可应用</summary>
            public bool Valid;
            /// <summary>不可应用时的原因</summary>
            public string Reason;
        }

        private readonly List<Item> _items = new List<Item>();
        private Vector2 _scrollPos;
        private string _report = "";

        [InspectorName("覆盖已配置过的值")]
        private bool _overwriteConfigured = true;
        [InspectorName("跳过嵌套预制体内的 Actor")]
        private bool _skipNestedPrefab = true;

        [MenuItem("Tools/单位半高度批量设置")]
        private static void Open()
        {
            GetWindow<ActorHalfHeightTool>("单位半高度批量设置");
        }

        private void OnGUI()
        {
            EditorGUILayout.HelpBox(
                "遍历 Assets 下所有带 Actor 的预制体，把 Actor 的「半高度」设置为 AimPoint 相对该 Actor 物体的局部 Y 坐标。\n" +
                "该值用于地雷等需要区分空中/地面单位的竖直判定，为 0 时退化为不做高度过滤。",
                MessageType.Info);

            _overwriteConfigured = EditorGUILayout.ToggleLeft("覆盖已配置过的值(不勾选则只填当前为 0 的)", _overwriteConfigured);
            _skipNestedPrefab = EditorGUILayout.ToggleLeft("跳过嵌套预制体内的 Actor(避免产生多余 override)", _skipNestedPrefab);

            EditorGUILayout.Space();

            if (GUILayout.Button("1.扫描全部预制体", GUILayout.Height(30)))
            {
                Scan();
            }

            if (GUILayout.Button("2.应用修改", GUILayout.Height(30)))
            {
                Apply();
            }

            EditorGUILayout.Space();

            if (!string.IsNullOrEmpty(_report))
            {
                _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);
                EditorGUILayout.TextArea(_report, GUILayout.ExpandHeight(true));
                EditorGUILayout.EndScrollView();

                if (GUILayout.Button("复制结果"))
                {
                    EditorGUIUtility.systemCopyBuffer = _report;
                    Debug.Log("已复制到剪贴板");
                }
            }
        }

        /// <summary>扫描全部预制体，生成预览列表(只读，不写入)</summary>
        private void Scan()
        {
            _items.Clear();

            string[] guids = AssetDatabase.FindAssets("t:Prefab");
            var sb = new StringBuilder();
            int prefabCount = 0;

            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                if (string.IsNullOrEmpty(path) || !path.StartsWith("Assets/")) continue;

                EditorUtility.DisplayProgressBar("扫描预制体", path, i / (float)guids.Length);

                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (!prefab) continue;

                var actors = prefab.GetComponentsInChildren<Actor>(true);
                if (actors == null || actors.Length == 0) continue;

                prefabCount++;
                foreach (var actor in actors)
                {
                    var item = new Item
                    {
                        PrefabPath = path,
                        ActorPath = GetTransformPath(actor.transform, prefab.transform),
                    };

                    if (!actor.AimPoint)
                    {
                        item.Valid = false;
                        item.Reason = "未配置瞄准点(AimPoint)";
                    }
                    else if (_skipNestedPrefab && IsInNestedPrefab(actor.gameObject))
                    {
                        item.Valid = false;
                        item.Reason = "位于嵌套预制体内(已跳过)";
                    }
                    else
                    {
                        item.NewValue = GetLocalAimHeight(actor);
                        item.Valid = item.NewValue > 0f;
                        item.Reason = item.Valid ? "" : "AimPoint 局部 Y 必须 > 0";
                    }

                    item.OldValue = ReadHalfHeight(actor);
                    _items.Add(item);
                }
            }

            EditorUtility.ClearProgressBar();

            int valid = 0;
            foreach (var item in _items)
            {
                if (item.Valid) valid++;
            }

            sb.AppendLine($"扫描完成：共 {guids.Length} 个预制体，其中 {prefabCount} 个含 Actor，共 {_items.Count} 个 Actor，可应用 {valid} 个");
            sb.AppendLine();
            foreach (var item in _items)
            {
                sb.AppendLine($"{item.PrefabPath}  [{item.ActorPath}]  {item.OldValue:0.###} -> {(item.Valid ? item.NewValue.ToString("0.###") : "跳过(" + item.Reason + ")")}");
            }

            _report = sb.ToString();
            Debug.Log(_report);
            Repaint();
        }

        /// <summary>把扫描结果写入预制体</summary>
        private void Apply()
        {
            if (_items.Count == 0)
            {
                Debug.LogWarning("[半高度] 请先执行扫描");
                return;
            }

            // 需要写入的预制体去重
            var paths = new HashSet<string>();
            foreach (var item in _items)
            {
                if (item.Valid) paths.Add(item.PrefabPath);
            }

            int changed = 0;
            int skipped = 0;
            var sb = new StringBuilder();
            int index = 0;

            foreach (string path in paths)
            {
                EditorUtility.DisplayProgressBar("应用半高度", path, index++ / (float)paths.Count);

                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    bool dirty = false;
                    foreach (var actor in root.GetComponentsInChildren<Actor>(true))
                    {
                        if (!actor.AimPoint) continue;
                        if (_skipNestedPrefab && IsInNestedPrefab(actor.gameObject))
                        {
                            skipped++;
                            continue;
                        }

                        float value = GetLocalAimHeight(actor);
                        if (value <= 0f)
                        {
                            skipped++;
                            continue;
                        }

                        var so = new SerializedObject(actor);
                        var prop = so.FindProperty(HalfHeightField);
                        if (prop == null)
                        {
                            Debug.LogWarning($"[半高度] 未找到字段 {HalfHeightField}：{path}", actor);
                            skipped++;
                            continue;
                        }

                        if (!_overwriteConfigured && prop.floatValue > 0f)
                        {
                            skipped++;
                            continue;
                        }

                        if (Mathf.Approximately(prop.floatValue, value))
                        {
                            continue;
                        }

                        prop.floatValue = value;
                        so.ApplyModifiedPropertiesWithoutUndo();
                        dirty = true;
                        changed++;
                        sb.AppendLine($"{path}  [{GetTransformPath(actor.transform, root.transform)}]  {value:0.###}");
                    }

                    if (dirty)
                    {
                        PrefabUtility.SaveAsPrefabAsset(root, path);
                    }
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }

            EditorUtility.ClearProgressBar();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            var result = new StringBuilder();
            result.AppendLine($"应用完成：修改 {changed} 个 Actor，跳过 {skipped} 个，涉及预制体 {paths.Count} 个");
            result.AppendLine();
            result.Append(sb);
            _report = result.ToString();
            Debug.Log(_report);
            Repaint();
        }

        /// <summary>读取 Actor 当前配置的半高度</summary>
        private static float ReadHalfHeight(Actor actor)
        {
            var so = new SerializedObject(actor);
            var prop = so.FindProperty(HalfHeightField);
            return prop != null ? prop.floatValue : 0f;
        }

        /// <summary>AimPoint 相对 Actor 物体的局部 Y 坐标</summary>
        private static float GetLocalAimHeight(Actor actor)
        {
            Transform aim = actor.AimPoint;
            if (!aim) return 0f;
            // 用 InverseTransformPoint 而非 localPosition，兼容 AimPoint 不是 Actor 直接子节点的情况
            return actor.transform.InverseTransformPoint(aim.position).y;
        }

        /// <summary>该物体是否位于某个嵌套预制体实例内</summary>
        private static bool IsInNestedPrefab(GameObject go)
        {
            return PrefabUtility.GetNearestPrefabInstanceRoot(go) != null;
        }

        /// <summary>获取 target 相对 root 的层级路径</summary>
        private static string GetTransformPath(Transform target, Transform root)
        {
            if (target == root) return root.name;

            var sb = new StringBuilder(target.name);
            var parent = target.parent;
            while (parent != null && parent != root)
            {
                sb.Insert(0, parent.name + "/");
                parent = parent.parent;
            }
            return sb.ToString();
        }
    }
}
