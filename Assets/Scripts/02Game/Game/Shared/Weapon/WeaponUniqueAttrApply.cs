using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
namespace Unity.FPS.Game
{
    using Weapon = WeaponPlayerController;
    using Info =  WeaponPlayerController.AttrInfo;

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
        };


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