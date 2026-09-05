using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using FPSGame.AI;
using UnityEditor;
using UnityEngine;

/// <summary>
/// EnemyFxData_SO 一次性迁移工具：扫描带 EnemyControllerFX 派生组件的 prefab，
/// 把旧内联 rendererSet + fxDic 数据导出为共享 SO 资产，并给 prefab 写入 fxData 引用。
/// 用法：Tools/敌人特效/迁移-干跑报告（只报告）与 迁移-正式执行。
/// 变体处理：仅迁移"本资产自有组件"或"带 FX 覆写的变体"，避免烘焙变体、破坏嵌套引用。
/// </summary>
public static class EnemyFxDataMigrationTool
{
    private const string AssetRoot = "Assets/Resources/GameData/EnemyFx";

    private static readonly string[] SearchFolders =
    {
        "Assets/Resources/Prefabs",
        "Assets/Art/Modle/Enemy",
    };

    private sealed class Item
    {
        public string PrefabPath;
        public string SoPath;
        public EnemyControllerFX Comp;
        public bool IsVariant;
    }

    [MenuItem("Tools/敌人特效/迁移-干跑报告")]
    private static void DryRun()
    {
        Debug.Log("[敌人特效迁移] 干跑报告开始…");
        Run(execute: false);
    }

    [MenuItem("Tools/敌人特效/迁移-正式执行")]
    private static void Execute()
    {
        Debug.Log("[敌人特效迁移] 正式执行开始…");
        if (!EditorUtility.DisplayDialog("敌人特效配置迁移",
                "把 prefab 内联 rendererSet/fxDic 导出为 EnemyFxData_SO 资产并写入 fxData 引用。\n" +
                "建议先跑\"干跑报告\"并做好版本控制快照。\n\n继续？",
                "执行", "取消"))
        {
            return;
        }
        Run(execute: true);
    }

    private static void Run(bool execute)
    {
        try
        {
            RunInternal(execute);
        }
        catch (Exception e)
        {
            EditorUtility.ClearProgressBar();
            Debug.LogError("[敌人特效迁移] 执行异常：" + e);
        }
    }

    private static void RunInternal(bool execute)
    {
        var items = new List<Item>();
        var failures = new List<string>();
        int skipNoData = 0;
        int skipAlready = 0;
        int skipNotOwned = 0;

        string[] guids = AssetDatabase.FindAssets("t:Prefab", SearchFolders);
        try
        {
            for (int g = 0; g < guids.Length; ++g)
            {
                if (EditorUtility.DisplayCancelableProgressBar("扫描敌人特效配置",
                        $"{(g + 1)}/{guids.Length}",
                        (float)g / Mathf.Max(1, guids.Length)))
                {
                    break;
                }

                string prefabPath = AssetDatabase.GUIDToAssetPath(guids[g]);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                if (prefab == null) continue;

                var comps = prefab.GetComponentsInChildren<EnemyControllerFX>(true);
                if (comps.Length == 0) continue;

                foreach (var comp in comps)
                {
                    if (comp.fxData.IsValid())
                    {
                        ++skipAlready;
                        continue; // 已迁移
                    }

                    if (!comp.HasLegacyFxData)
                    {
                        ++skipNoData;
                        continue; // 本组件没有内联配置
                    }

                    if (!OwnsDataHere(comp, prefabPath))
                    {
                        ++skipNotOwned;
                        continue; // 数据属于其源资产（嵌套实例且无覆写），交给源 prefab 迁移
                    }

                    items.Add(new Item
                    {
                        PrefabPath = prefabPath,
                        SoPath = BuildSoPath(prefabPath),
                        Comp = comp,
                        IsVariant = PrefabUtility.GetCorrespondingObjectFromSource(prefab) != null,
                    });
                }
            }
        }
        catch (Exception e)
        {
            failures.Add("扫描异常：" + e);
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }

        var sb = new StringBuilder();
        sb.AppendLine($"[敌人特效迁移] 扫描 prefab {guids.Length} 个；待迁移 {items.Count} 个，" +
                      $"跳过(已迁移 {skipAlready}/无数据 {skipNoData}/非本资产数据 {skipNotOwned})。");

        if (execute)
        {
            int ok = 0;
            for (int i = 0; i < items.Count; ++i)
            {
                if (EditorUtility.DisplayCancelableProgressBar("迁移敌人特效配置",
                        $"{items[i].PrefabPath}\n-> {items[i].SoPath}",
                        (float)i / Mathf.Max(1, items.Count)))
                {
                    failures.Add("用户在 " + i + "/" + items.Count + " 处取消");
                    break;
                }
                if (MigrateOne(items[i], failures)) ++ok;
            }
            EditorUtility.ClearProgressBar();
            AssetDatabase.SaveAssets();
            sb.AppendLine($"执行完成：成功 {ok}，失败 {failures.Count}。");
        }
        else
        {
            sb.AppendLine("干跑报告（前 60 项）：");
            for (int i = 0; i < items.Count && i < 60; ++i)
            {
                sb.AppendLine((items[i].IsVariant ? "[变体] " : "[基础] ") + items[i].PrefabPath);
                sb.AppendLine("    -> " + items[i].SoPath);
            }
            if (items.Count > 60) sb.AppendLine($"... 其余 {items.Count - 60} 项执行迁移时会处理");
            sb.AppendLine("确认无误后运行 Tools/敌人特效/迁移-正式执行。");
        }

        if (failures.Count > 0)
        {
            sb.AppendLine("失败/注意清单：");
            for (int i = 0; i < failures.Count; ++i) sb.AppendLine(" - " + failures[i]);
        }
        sb.AppendLine("注意：场景中直接放置且自带配置的非 prefab 实例不在迁移范围，需要手动挂 SO。");

        Debug.Log(sb.ToString());

        if (execute && items.Count > 0 && failures.Count == 0)
        {
            EditorUtility.DisplayDialog("敌人特效迁移完成",
                $"成功迁移 {items.Count} 个 prefab。\n可重跑\"迁移-干跑报告\"确认无剩余。", "好的");
        }
    }

    /// <summary>
    /// 该组件的数据是否应由当前 prefab 资产拥有：
    /// - 非嵌套实例组件（本资产自有）→ true；
    /// - 是某源资产组件的实例：本资产无 FX 覆写 → false（源资产负责）；有覆写 → true（变体自存数据）。
    /// </summary>
    private static bool OwnsDataHere(EnemyControllerFX comp, string prefabPath)
    {
        try
        {
            var srcComp = PrefabUtility.GetCorrespondingObjectFromSource(comp) as EnemyControllerFX;
            if (srcComp == null) return true;
            return HasFxModifications(prefabPath, comp);
        }
        catch
        {
            // 探测失败时保守处理：允许迁移，避免遗漏
            return true;
        }
    }

    private static bool HasFxModifications(string prefabPath, EnemyControllerFX comp)
    {
        try
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            var mods = PrefabUtility.GetPropertyModifications(prefab);
            if (mods == null) return false;
            for (int i = 0; i < mods.Length; ++i)
            {
                var m = mods[i];
                if (m == null || m.propertyPath == null) continue;
                if (!ReferenceEquals(m.target, comp)) continue;
                if (m.propertyPath.IndexOf("fxData", StringComparison.Ordinal) >= 0 ||
                    m.propertyPath.IndexOf("rendererSet", StringComparison.Ordinal) >= 0 ||
                    m.propertyPath.IndexOf("fxDic", StringComparison.Ordinal) >= 0)
                {
                    return true;
                }
            }
        }
        catch { }
        return false;
    }

    private static bool MigrateOne(Item item, List<string> failures)
    {
        try
        {
            EnsureFolderFor(item.SoPath);
            var so = AssetDatabase.LoadAssetAtPath<EnemyFxData_SO>(item.SoPath);
            bool isNew = so == null;
            if (so == null)
            {
                so = ScriptableObject.CreateInstance<EnemyFxData_SO>();
                AssetDatabase.CreateAsset(so, item.SoPath);
            }

            // 逐字段拷贝旧内联数据到共享 SO
            item.Comp.ExportLegacyTo(so);
            EditorUtility.SetDirty(so);
            AssetDatabase.SaveAssets();

            // 资产级写入 fxData 引用（变体由 Unity 记为覆写，不烘焙内容）
            var ser = new SerializedObject(item.Comp);
            var fxDataProp = ser.FindProperty("fxData");
            if (fxDataProp == null) throw new InvalidOperationException("找不到 EnemyControllerFX.fxData 属性");
            fxDataProp.objectReferenceValue = so;
            ser.ApplyModifiedPropertiesWithoutUndo();

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(item.PrefabPath);
            PrefabUtility.SavePrefabAsset(prefab);
            Debug.Log($"{(isNew ? "新建" : "更新")} {item.SoPath}\n    <- {item.PrefabPath}");
            return true;
        }
        catch (Exception e)
        {
            failures.Add(item.PrefabPath + " 迁移失败：" + e.Message);
            return false;
        }
    }

    /// <summary>镜像 prefab 相对 Assets 的目录，避免同名资产冲突：GameData/EnemyFx/&lt;相对目录&gt;/EFX_&lt;名称&gt;.asset</summary>
    private static string BuildSoPath(string prefabPath)
    {
        string dir = Path.GetDirectoryName(prefabPath).Replace('\\', '/');
        string mirror = dir.StartsWith("Assets/", StringComparison.Ordinal)
            ? dir.Substring("Assets/".Length)
            : dir;
        string assetName = "EFX_" + SanitizeAssetName(Path.GetFileNameWithoutExtension(prefabPath));
        return AssetRoot + "/" + mirror + "/" + assetName + ".asset";
    }

    /// <summary>资产文件名规整：空格及非法字符转下划线，避免资源名带空格</summary>
    private static string SanitizeAssetName(string name)
    {
        char[] chars = name.ToCharArray();
        for (int i = 0; i < chars.Length; ++i)
        {
            if (chars[i] == ' ' || chars[i] == ':' || chars[i] == '?' || chars[i] == '!' ||
                chars[i] == '*' || chars[i] == '"' || chars[i] == '<' || chars[i] == '>' ||
                chars[i] == '|')
            {
                chars[i] = '_';
            }
        }
        return new string(chars);
    }

    private static void EnsureFolderFor(string assetPath)
    {
        string dir = Path.GetDirectoryName(assetPath).Replace('\\', '/');
        string[] parts = dir.Split('/');
        string current = parts[0];
        for (int i = 1; i < parts.Length; ++i)
        {
            string parent = current;
            current = parent + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(current))
            {
                AssetDatabase.CreateFolder(parent, parts[i]);
            }
        }
    }
}
