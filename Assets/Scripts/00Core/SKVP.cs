using System;
using System.Collections.Generic;


#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
#endif


/// <summary>
/// 结构体约束的键值对（更严格，序列化更友好）
/// </summary>
[Serializable]
public class SKVP<K, V> : KVP<K, V>
    //where K : struct
    //where V : struct
{
    public SKVP(K key, V value) : base(key, value) { }
}

/// <summary>
/// 无约束的键值对
/// </summary>
[Serializable]
public class KVP<K, V>
{
    public K Key;
    public V Value;

    public KVP(K key, V value)
    {
        Key = key;
        Value = value;
    }
}

#if UNITY_EDITOR


[CustomPropertyDrawer(typeof(SKVP<,>))]
public class SKVPDrawer : SingleLineDrawer
{
    protected override Dictionary<string, string> Fields => new()
    {
        { "Key", "K" },
        { "Value", "V" },
    };
}

#endif
