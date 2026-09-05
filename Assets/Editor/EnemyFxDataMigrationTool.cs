using System;
using System.Collections.Generic;
using FPSGame.AI;
using UnityEditor;
using UnityEngine;

/// <summary>
/// EnemyFxData_SO 改造的编辑器辅助（收尾清理）：对仍挂 EnemyControllerFX 派生组件的 prefab 做一次
/// SavePrefabAsset，剥离删除旧字段后残留的旧内联 rendererSet/fxDic 序列化数据。
/// 注意：请在手动补填完剩余变体的 fxData 后再运行本工具。
/// </summary>
public static class EnemyFxDataMigrationTool
{
    private static readonly string[] SearchFolders =
    {
        "Assets/Resources/Prefabs",
        "Assets/Art/Modle/Enemy",
    };

    [MenuItem("Tools/敌人特效/清理-二次保存剥离残留内联数据")]
    private static void CleanRemnantData()
    {
        if (!EditorUtility.DisplayDialog("敌人特效残留数据清理",
                "对含 EnemyControllerFX 的 prefab 做一次 SavePrefabAsset，\n" +
                "剥离删除旧字段后残留的内联 rendererSet/fxDic 数据。\n" +
                "请先确认所有待手填变体已挂好 fxData，再做版本控制快照。\n\n继续？",
                "执行", "取消"))
        {
            return;
        }

        string[] guids = AssetDatabase.FindAssets("t:Prefab", SearchFolders);
        var failures = new List<string>();
        int hit = 0;
        int saved = 0;
        try
        {
            for (int g = 0; g < guids.Length; ++g)
            {
                if (EditorUtility.DisplayCancelableProgressBar("清理敌人特效残留数据",
                        $"{(g + 1)}/{guids.Length}",
                        (float)g / Mathf.Max(1, guids.Length)))
                {
                    failures.Add("用户取消于 " + g + "/" + guids.Length);
                    break;
                }

                string path = AssetDatabase.GUIDToAssetPath(guids[g]);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null) continue;
                if (prefab.GetComponentsInChildren<EnemyControllerFX>(true).Length == 0) continue;

                ++hit;
                try
                {
                    PrefabUtility.SavePrefabAsset(prefab);
                    ++saved;
                }
                catch (Exception e)
                {
                    failures.Add(path + " 二次保存失败：" + e.Message);
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

        AssetDatabase.SaveAssets();
        string msg = $"[敌人特效清理] 命中含 EnemyControllerFX 的 prefab {hit}，二次保存 {saved}，失败 {failures.Count}。";
        if (failures.Count > 0)
        {
            msg += "\n" + string.Join("\n", failures);
        }
        Debug.Log(msg);
    }
}
