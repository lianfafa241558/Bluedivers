using System.Collections;
using System.Collections.Generic;
using System.Linq;
using FPSGame.Core;

#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;
using FPSGame.Data;
using FPSGame.Game;
using FPSGame.Gameplay;
using FPSGame.Weapon;


namespace FPSGame.GameData
{

    [CreateAssetMenu(fileName = "new Data", menuName = "Data/角色")]
    public class RoleData_SO : ScriptableObject
    {
        public string ID;

        public DisplayDic<WeaponTypeEnum, List<WeaponPlayerController>> weapons;

        [SerializeField]
        DisplayDic<SpeechTypeEnum,SoundGroup_SO> speechGroups=new DisplayDic<SpeechTypeEnum, SoundGroup_SO>();

        /// <summary>角色默认携带的战备ID列表</summary>
        public int[] DefaultAirdropIDs;
        public List<WeaponPlayerController> GetStartingWeapons(ArchivesData_SO.ArchRoleData arch)
        {
            List<WeaponPlayerController> re = new() {
                GetWeapon(WeaponTypeEnum.Primary,arch.weaponSelect[WeaponTypeEnum.Primary]),
                GetWeapon(WeaponTypeEnum.Secondary,arch.weaponSelect[WeaponTypeEnum.Secondary]),
                GetWeapon(WeaponTypeEnum.Special,arch.weaponSelect[WeaponTypeEnum.Special]),
                GetWeapon(WeaponTypeEnum.Grenade,arch.weaponSelect[WeaponTypeEnum.Grenade]),
                GetWeapon(WeaponTypeEnum.FlareGun,arch.weaponSelect[WeaponTypeEnum.FlareGun]),
            };
            return re;
        }
        public WeaponPlayerController GetWeapon(WeaponTypeEnum type, int index)
        {
            return weapons[type][index % weapons[type].Count];
        }
        public SoundGroup_SO SpeechGroup(SpeechTypeEnum type)
        {
            if (speechGroups.TryGet(type, out var list))
            {
                return list;
            }
            Debug.LogError(ID + "没有配置" + type + "的语音");
            return null;
        }

    }
    public enum WeaponTypeEnum
    {
        /// <summary>主武器</summary>
        [InspectorName("主武器")]
        Primary,
        /// <summary>副武器</summary>
        [InspectorName("副武器")]
        Secondary,
        /// <summary>特殊武器</summary>
        [InspectorName("特殊武器")]
        Special,
        /// <summary>投掷物</summary>
        [InspectorName("投掷物")]
        Grenade,
        /// <summary>信号枪</summary>
        [InspectorName("信号枪")]
        FlareGun,
        /// <summary>护甲</summary>
        [InspectorName("护甲")]
        Armor,
        /// <summary>支援武器</summary>
        [InspectorName("支援武器")]
        Support,
        /// <summary>空手</summary>
        [InspectorName("空手")]
        Empty,

    }

}
