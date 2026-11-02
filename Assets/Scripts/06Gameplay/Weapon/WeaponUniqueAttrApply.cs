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
                weapon.Damages[1].SetExplosionTypeElectric(new(attr.Value));
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