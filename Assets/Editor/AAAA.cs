using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using Unity.FPS.Game;
using Core;

public class AAAA : EditorWindow
{
    [MenuItem("Tools/设置武器爆炸范围")]
    public static void SetExplosionInnerRange()
    {
        // 获取 Project 选项卡中选中的所有物体
        var selectedObjects = Selection.objects;

        if (selectedObjects == null || selectedObjects.Length == 0)
        {
            Debug.LogWarning("请在 Project 选项卡中选择一个或多个物体");
            return;
        }

        int totalWeaponsFound = 0;
        int totalModified = 0;

        foreach (var obj in selectedObjects)
        {
            // 处理选中的物体可能是 GameObject 或 Prefab
            string assetPath = AssetDatabase.GetAssetPath(obj);
            if (string.IsNullOrEmpty(assetPath))
                continue;

            // 加载 Prefab 的根 GameObject
            GameObject prefabRoot = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
            if (prefabRoot == null)
                continue;

            // 获取所有 WeaponBaseController 组件（包括子物体）
            var weapons = prefabRoot.GetComponentsInChildren<WeaponBaseController>(true);

            foreach (var weapon in weapons)
            {
                if (weapon.Damages == null || weapon.Damages.Count == 0)
                    continue;

                totalWeaponsFound++;
                bool modified = false;

                foreach (var damage in weapon.Damages)
                {
                    /*
                    if ((damage.ChargeDamageScale != 0 && damage.ChargeDamageScale!=1)
                        || (damage.ChargeAOERangeScale != 0 && damage.ChargeAOERangeScale != 1)
                         || (damage.ChargeSpeedScale != 0 && damage.ChargeSpeedScale != 1)
                          || (damage.ChargeGravityScale != 0 && damage.ChargeGravityScale != 1)
                        )
                    {
                        damage.UseCharge = true;
                        modified = true;
                    }*/
                }

                if (modified)
                {
                    totalModified++;
                    // 标记 Prefab 为已修改
                    EditorUtility.SetDirty(prefabRoot);
                }
            }
        }

        // 保存修改到 Prefab
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"处理完成！共检查了 {totalWeaponsFound} 个武器组件，修改了 {totalModified} 个 Prefab。");
    }

    // 可选：添加验证，确保只在有选中物体时菜单可用
    [MenuItem("Tools/设置武器爆炸范围", true)]
    public static bool ValidateSetExplosionInnerRange()
    {
        return Selection.objects != null && Selection.objects.Length > 0;
    }
    /*
    [MenuItem("Tools/澶嶅埗 DamageGroupDirect -> DamageGroupDirect2")]
    public static void CopyDamageGroupDirectTo2()
    {
        // 鑾峰彇 "Assets" 涓嬫墍鏈?Prefab 鐨?GUID
        string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets" });

        int totalWeaponsFound = 0;
        int totalModified = 0;

        foreach (string guid in prefabGuids)
        {
            string assetPath = AssetDatabase.GUIDToAssetPath(guid);
            GameObject prefabRoot = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
            if (prefabRoot == null)
                continue;

            bool prefabModified = false;

            // 澶勭悊 WeaponBaseController
            var weapons = prefabRoot.GetComponentsInChildren<WeaponBaseController>(true);
            foreach (var weapon in weapons)
            {
                if (weapon.Damages == null || weapon.Damages.Count == 0)
                    continue;

                totalWeaponsFound++;

                foreach (var damage in weapon.Damages)
                {
                    if (damage.DamageGroupDirect2 == null || damage.DamageGroupDirect2.Count == 0)
                        continue;

                    damage.DamageGroupDirect.Clear();
                    foreach (var item in damage.DamageGroupDirect2)
                    {
                        damage.DamageGroupDirect.Add(new SKVP<DamageTypeEnum, float>(item.Key, item.Value));
                    }

                    prefabModified = true;
                }
            }

            if (prefabModified)
            {
                totalModified++;
                EditorUtility.SetDirty(prefabRoot);
            }
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"处理完成！共检查了 {totalWeaponsFound} 个武器组件，修改了 {totalModified} 个 Prefab。");
    }
    
    [MenuItem("Tools/澶嶅埗 DamageGroupDirect -> DamageGroupDirect2", true)]
    public static bool ValidateCopyDamageGroupDirectTo2()
    {
        return true;
    }*/ 
}