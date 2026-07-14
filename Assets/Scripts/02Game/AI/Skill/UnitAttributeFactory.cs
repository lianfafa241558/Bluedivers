using System.Collections;
using System.Collections.Generic;
using PEMaths;

using UnityEngine;


namespace Unity.FPS.Game
{
    /// <summary>
    /// 创建属性的工厂，可以将属性的配置预设好，创建只要输入基础值，其他配置由工厂注入
    /// </summary>
    public static class UnitAttributeFactory
    {
        public static readonly Dictionary<UnitAttrType, (bool, AttrTag, ModifierType)> attributeConfigs = new()
        {
            // 通用属性
            { UnitAttrType.Speed, (true, AttrTag.LimitCurr, ModifierType.All) },
            { UnitAttrType.AngularSpeed, (true, AttrTag.LimitCurr, ModifierType.All) },
        };
        public static AttrTag GetTag(UnitAttrType type) => attributeConfigs[type].Item2;

        public static GameAttribute Create(UnitAttrType type, PEInt baseValue)
        {

            if (!attributeConfigs.TryGetValue(type, out var cfg))
            {
                Debug.LogError("找不到对应的配置" + type);
                return default;
            }
            if (cfg.Item1)
            {
                return new GameCurrentAttribute(baseValue, cfg.Item2, cfg.Item3);
            }
            else
            {

                return new GameAttribute(baseValue, cfg.Item2, cfg.Item3);
            }
          
        }
        public static Dictionary<UnitAttrType, GameAttribute> CreateBaseUnit(Dictionary<UnitAttrType, PEInt> baseValues)
        {
            var result = new Dictionary<UnitAttrType, GameAttribute>();
            foreach (var kv in baseValues)
            {
                result[kv.Key] = Create(kv.Key, kv.Value);
            }
            return result;
        }

    }

    public enum UnitAttrType
    {
        //---------通用属性----------
        /// <summary>速度</summary>
        [InspectorName("速度")]
        Speed = 0,
        /// <summary>转向速度</summary>
        [InspectorName("转向速度")]
        AngularSpeed = 1,
    }
}