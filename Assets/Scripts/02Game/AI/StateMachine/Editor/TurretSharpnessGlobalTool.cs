using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Unity.FPS.AI.Editor
{
    /// <summary>
    /// 全局批量换算 AIInputUnitController.Turret 的 aimSharpness（瞄准锐度）工具。
    /// 目的：把旧的"Slerp 衰减系数"换算成新的"匀速转速系数(°/秒)"。
    /// 规则：aimSharpness 小于 10 的值 ×90 倍，同时最大值封顶 180。
    ///       例：旧值 5 -> min(5*90, 180)=180；旧值 0.5 -> min(45,180)=45。
    /// 用法：菜单栏 Tools -> Turret -> 批量换算瞄准锐度（×90，封顶180）
    /// </summary>
    public static class TurretSharpnessGlobalTool
    {
        private const string MenuRoot = "Tools/Turret/";

        /// <summary>换算阈值：仅对小于该值的瞄准锐度进行换算</summary>
        private const float ConvertThreshold = 10f;
        /// <summary>放大倍数</summary>
        private const float Multiply = 90f;
        /// <summary>换算后的最大封顶值（°/秒）</summary>
        private const float MaxClamp = 180f;

        [MenuItem(MenuRoot + "批量换算瞄准锐度（×90，封顶180）")]
        public static void BatchConvertSharpnessInPrefabs()
        {
            if (!EditorUtility.DisplayDialog(
                    "批量换算瞄准锐度",
                    $"将对所有预制体中 < {ConvertThreshold} 的瞄准锐度执行 ×{Multiply}，并封顶 {MaxClamp}。\n" +
                    $"例：旧值 5 -> {Mathf.Min(5 * Multiply, MaxClamp)}；旧值 0.5 -> {Mathf.Min(0.5f * Multiply, MaxClamp)}。\n\n是否继续？",
                    "继续", "取消"))
            {
                return;
            }

            // 扫描所有继承自 AIInputUnitController 的组件类型
            var types = TypeCache.GetTypesDerivedFrom<AIInputUnitController>()
                .Where(t => !t.IsAbstract && t.IsSubclassOf(typeof(MonoBehaviour)))
                .ToArray();

            var prefabGuids = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets" });
            int totalTurret = 0, totalPrefab = 0, skippedPrefab = 0;

            try
            {
                AssetDatabase.StartAssetEditing();

                for (int g = 0; g < prefabGuids.Length; g++)
                {
                    string guid = prefabGuids[g];
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    if (go == null) continue;

                    bool modified = false;
                    int prefabTurretCount = 0;

                    foreach (var type in types)
                    {
                        var comps = go.GetComponentsInChildren(type, true);
                        for (int c = 0; c < comps.Length; c++)
                        {
                            var comp = comps[c];
                            var so = new SerializedObject(comp);
                            var turrets = so.FindProperty("turrets");
                            if (turrets == null || !turrets.isArray) continue;

                            bool compModified = false;
                            for (int i = 0; i < turrets.arraySize; i++)
                            {
                                var elem = turrets.GetArrayElementAtIndex(i);
                                var sharpness = elem.FindPropertyRelative("aimSharpness");
                                if (sharpness == null) continue;

                                float oldVal = sharpness.floatValue;
                                if (oldVal >= ConvertThreshold) continue;

                                float newVal = Mathf.Min(oldVal * Multiply, MaxClamp);
                                if (Mathf.Approximately(oldVal, newVal)) continue;

                                sharpness.floatValue = newVal;
                                prefabTurretCount++;
                                compModified = true;
                                modified = true;
                            }

                            if (compModified) so.ApplyModifiedPropertiesWithoutUndo();
                        }
                    }

                    if (modified)
                    {
                        EditorUtility.SetDirty(go);
                        PrefabUtility.SavePrefabAsset(go);
                        totalPrefab++;
                        totalTurret += prefabTurretCount;
                    }
                    else
                    {
                        skippedPrefab++;
                    }
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
                AssetDatabase.SaveAssets();
            }

            Debug.Log($"[Turret工具] 换算完成：修改 {totalPrefab} 个预制体、共 {totalTurret} 个炮台瞄准锐度(<{ConvertThreshold} 则 ×{Multiply} 且封顶 {MaxClamp})；跳过 {skippedPrefab} 个无变化预制体");
        }
    }
}
