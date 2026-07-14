using System;
using FPSGame.Attribute;

/// <summary>
/// 结构体约束的键值对（更严格，序列化更友好）
/// </summary>
[Serializable]
[Singleline]
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
