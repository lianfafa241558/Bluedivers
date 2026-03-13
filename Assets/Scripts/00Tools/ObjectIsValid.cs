using System;
using UnityEngine;

public static class ObjectIsValid 
{

    // Unity原生Object
    public static bool IsValid(this UnityEngine.Object obj)
      => obj != null && !ReferenceEquals(obj, null) && !obj.Equals(null);

    // 普通C#对象
    public static bool IsValid(this System.Object obj)
        => obj != null && !obj.Equals(null);

    // 字符串
    public static bool IsValid(this string obj)
        => obj != null && !string.IsNullOrEmpty(obj);

    public static bool IsEnable(this MonoBehaviour obj)
        => obj != null && obj.enabled && obj.gameObject.activeInHierarchy;

    //  仅适配Unity对象实现的接口
    public static bool IsValid(this IComparable interfaceObj) // 或任意你实际的接口类型，比如I_Actor
    {
        // 第一步：判断接口实例是否为null
        if (interfaceObj == null)
        {
            return false;
        }

        // 第二步：强制转换为Unity Object（已确定是Unity对象，无需判空）
        UnityEngine.Object unityObj = interfaceObj as UnityEngine.Object;

        // 第三步：复用Unity Object的IsValid逻辑，校验是否被销毁
        return unityObj.IsValid();
    }

}
