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