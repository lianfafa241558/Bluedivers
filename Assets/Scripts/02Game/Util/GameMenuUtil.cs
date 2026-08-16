#if UNITY_EDITOR
using System;
using System.Reflection;
using Core;
using Unity.FPS.AI;
using Unity.FPS.Game;
using UnityEditor;

public static class GameMenuUtil
{

    [MenuItem("GameObject/辅助功能/配置为其他单位", priority = -100)]
    static void SetActor()
    {
        var go = Selection.activeGameObject;
        var actor=go.AddComponent<Actor>();
        SetPropertyValue(actor, "aimPoint", go.transform);
        SetPropertyValue(actor, "type", UnitTypeEnum.Other);
        actor.Team = -1;
        SetPropertyValue(actor, "threat", 0);
        var damageData=go.AddComponent<Damageable>();
        var cont=go.AddComponent<OtherController>();
        cont.DeathDuration = 3;

        var health = go.GetComponent<HealthOther>();
        health.MainPart = damageData;
        go.AddComponent<BuildingFXController>();
    }

    /// <summary>
    /// 反射给对象的属性赋值
    /// </summary>
    /// <param name="obj">要设置的对象</param>
    /// <param name="propertyName">属性名</param>
    /// <param name="value">要设置的值</param>
    public static void SetPropertyValue(object obj, string propertyName, object value)
    {
        if (obj == null) return;

        Type type = obj.GetType();
        PropertyInfo prop = type.GetProperty(propertyName);

        if (prop != null && prop.CanWrite)
        {
            prop.SetValue(obj, value);
            return;
        }

        var field = type.GetField(propertyName,BindingFlags.Instance | BindingFlags.NonPublic);
        if (field != null)
        {
            field.SetValue(obj, value);
            return;
        }


    }
}
#endif