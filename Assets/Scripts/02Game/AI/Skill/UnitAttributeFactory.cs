using System.Collections;
using System.Collections.Generic;
using PEMaths;
using Unity.BaseTool;
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
            
        };
        public static AttrTag GetTag(UnitAttrType type) => attributeConfigs[type].Item2;

        public static WeaponAttribute Create(UnitAttrType type, PEInt baseValue)
        {

            if (!attributeConfigs.TryGetValue(type, out var cfg))
            {
                Debug.LogError("找不到对应的配置" + type);
                return default;
            }
            if (cfg.Item1)
            {
                return new WeaponCurrentAttribute(baseValue, cfg.Item2, cfg.Item3);
            }
            else
            {

                return new WeaponAttribute(baseValue, cfg.Item2, cfg.Item3);
            }

        }

    }

    public enum UnitAttrType
    {
        //---------通用属性-----------
        /// <summary>速度</summary>
        [CustomLabel("速度")]
        Speed = 0,
    }
}