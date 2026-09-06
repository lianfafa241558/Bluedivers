using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 单维度变种配置：条件键类型 K 由子类封闭（string=按地图，EnemyVarietyType=按敌人类型），
/// 未命中回退默认值。仅适用于单维度变种需求。
/// 泛型 SO 无法直接生成资产，需通过具体子类封闭 K/V 使用，
/// 例如：<c>[CreateAssetMenu(menuName = "Data/敌人变种贴图")] public class DecorTextureVariant_SO : ActorVariantData_SO&lt;EnemyVarietyType, Texture&gt; {}</c>
/// </summary>
/// <typeparam name="K">条件键类型（string=地图、EnemyVarietyType=敌人变种等）</typeparam>
/// <typeparam name="V">配置值类型，需为 Unity 可序列化类型（Texture/GameObject/SO 引用等）</typeparam>
public abstract class VariantBase_SO<K, V> : ScriptableObject
{
    [InspectorName("默认值")]
    public V defaultValue;

    [InspectorName("变种列表")]
    public SKVP<K, V>[] variants;

    /// <summary>按键取值，未配置对应键时返回默认值</summary>
    public V Get(K key)
    {
        if (variants != null)
        {
            for (int i = 0; i < variants.Length; i++)
            {
                // 泛型 K 不能用 ==，用 EqualityComparer 处理枚举/字符串比较
                if (EqualityComparer<K>.Default.Equals(variants[i].Key, key))
                {
                    return variants[i].Value;
                }
            }
        }
        return defaultValue;
    }
}
