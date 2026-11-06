using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.Events;
namespace FPSGame.Weapon
{
    using Info =  WeaponPlayerController.AttrInfo;
    using Weapon = WeaponPlayerController;
    /// <summary>
    /// 特殊的升级应用的场合
    /// </summary>
    public static class WeaponUniqueAttributeApplier
    {
        private static Dictionary<string, UnityAction<Weapon, Info>> data = new() {
            ["激光制导射程"] = (Weapon weapon, Info attr) => {
                weapon.Damages[1].MaxRange = attr.Value;
            },
            ["恐惧尾迹"] = (Weapon weapon, Info attr) => {
                weapon.Damages[1].SetExplosionTypeVertigo(new(attr.Value));//基础伤害是4，持续5秒
            },
            ["电磁尾迹"] = (Weapon weapon, Info attr) => {
                weapon.Damages[1].SetExplosionTypeElectric(new(attr.Value));
            },
            ["燃烧尾迹"] = (Weapon weapon, Info attr) => {
                weapon.Damages[1].SetExplosionTypeBurn(new(attr.Value));
            },
            ["尾迹范围"] = (Weapon weapon, Info attr) => {
                ApplyTrailRange(weapon, attr.Value);
            },
            ["全自动模式"] = (Weapon weapon, Info attr) => {
                weapon.ShootType = attr.Value>0? WeaponShootType.Automatic: WeaponShootType.Manual;
            },
            ["强制连射"] = (Weapon weapon, Info attr) => {
                SetTag(attr, ref weapon.WeaponFlag, WeaponFlag.ForceShoot);
            },
            ["穿透单位"] = (Weapon weapon, Info attr) => {
                SetTag(attr,ref weapon.BulletFlag, BulletFlag.PenetrateUnits);
            },
            ["锁定重定向"] = (Weapon weapon, Info attr) => {
                SetTag(attr, ref weapon.WeaponFlag, WeaponFlag.Relock);
            },
            
        };



        /// <summary>
        /// 尾迹档位(1 号)的原始爆炸半径。首次应用时抓一次，"尾迹范围"每次都按「原始 + 累计加成」重算。
        /// <para>⚠ 不能直接 +=：<see cref="Apply"/> 在每次 <c>ApplyUpgrade</c> 都会用重建的参数表重跑一遍，
        /// 累加写法会让半径随每次换装一路膨胀。</para>
        /// </summary>
        private static readonly Dictionary<DamageData, float> s_TrailBaseRange = new();

        /// <summary>
        /// 「尾迹范围」：把 1 号伤害档的爆炸半径改成「原始半径 + 加成」。
        /// <para>用 <see cref="DamageData.SetExplosionRange"/> 而不是直接写字段：它会按原内/外比例
        /// 同步内半径，否则只放大外半径会把整圈变成"边缘大幅衰减"。</para>
        /// </summary>
        private static void ApplyTrailRange(Weapon weapon, float bonus)
        {
            var damages = weapon.Damages;
            if (damages == null || damages.Count <= 1) return;   // 没有尾迹档，改了也没人读
            var data = damages[1];
            if (data == null) return;

            if (!s_TrailBaseRange.TryGetValue(data, out var baseRange))
            {
                baseRange = data.GetDamageOuterRadius(1).RawFloat;
                s_TrailBaseRange[data] = baseRange;
            }
            data.SetExplosionRange(new(Mathf.Max(0f, baseRange + bonus)));
        }

        private static void SetTag<T>(Info attr,ref T now,T flag) where T:Enum
        {
            ref int nowRef = ref Unsafe.As<T, int>(ref now);
            int flagValue = Unsafe.As<T, int>(ref flag);

            if (attr.Value > 0)
                nowRef |= flagValue;
            else
                nowRef &= ~flagValue;
        }



        public static void Apply(Weapon weapon, Dictionary<string, Info> parameter)
        {
            foreach (var item in parameter)
            {
                if(data.TryGetValue(item.Key,out var action))
                {
                    action.Invoke(weapon, item.Value);
                }
            }
        } 

    }
}