using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Core;

using UnityEditor;
using UnityEngine;


namespace Unity.FPS.Game
{

    [CreateAssetMenu(fileName = "new Data", menuName = "Data/角色")]
    public class RoleData_SO : ScriptableObject
    {
        public string ID;
        
        public DisplayDic<WeaponTypeEnum, List<WeaponPlayerController>> weapons;

        [SerializeField]
        DisplayDic<SpeechTypeEnum,SoundGroup_SO> speechGroups=new DisplayDic<SpeechTypeEnum, SoundGroup_SO>();
        public List<WeaponPlayerController> GetStartingWeapons(ArchivesData_SO.ArchRoleData arch)
        {
            List<WeaponPlayerController> re=new() { 
                weapons[WeaponTypeEnum.Primary][arch.weaponSelect[WeaponTypeEnum.Primary]] , 
                weapons[WeaponTypeEnum.Secondary][arch.weaponSelect[WeaponTypeEnum.Secondary]],
                weapons[WeaponTypeEnum.Special][arch.weaponSelect[WeaponTypeEnum.Special]],
                weapons[WeaponTypeEnum.Grenade][arch.weaponSelect[WeaponTypeEnum.Grenade]],
                weapons[WeaponTypeEnum.FlareGun][arch.weaponSelect[WeaponTypeEnum.FlareGun]],
            };
            return re;
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
    }

    public enum SpeechTypeEnum
    {
        /// <summary>呼叫补给</summary>
        [InspectorName("战斗/呼叫补给")]
        Supply,
        /// <summary>呼叫撤离</summary>
        [InspectorName("战斗/呼叫撤离")]
        Evacuate,
        /// <summary>呼叫战备</summary>
        [InspectorName("战斗/呼叫战备")]
        Airdrop,
        /// <summary>呼叫载具</summary>
        [InspectorName("战斗/呼叫载具")]
        Vehicle,
        /// <summary>呼叫炮台</summary>
        [InspectorName("战斗/呼叫炮台")]
        Turret,
        /// <summary>呼叫轨道轰炸</summary>
        [InspectorName("战斗/呼叫轨道轰炸")]
        Bombing,
        /// <summary>呼叫飞鹰</summary>
        [InspectorName("战斗/呼叫飞鹰")]
        Airstrike,
        /// <summary>呼叫凯伊</summary>
        [InspectorName("战斗/呼叫凯伊")]
        Kei,
        /// <summary>战斗/呼叫炸弹</summary>
        [InspectorName("战斗/呼叫炸弹")]
        HaloBomb,
        /// <summary>占位符</summary>
        [InspectorName("占位符")]
        Placeholder2,
        /// <summary>求救</summary>
        [InspectorName("战斗/求救")]
        Help,
        /// <summary>马上到</summary>
        [InspectorName("战斗/马上到")]
        Responded,
        /// <summary>复活感谢</summary>
        [InspectorName("战斗/复活感谢")]
        Thank,
        /// <summary>采集遗物</summary>
        [InspectorName("战斗/采集遗物")]
        CollOOParts,
        /// <summary>占位符</summary>
        [InspectorName("占位符")]
        Placeholder5,
        /// <summary>占位符</summary>
        [InspectorName("占位符")]
        Placeholder6,
        /// <summary>发现敌人</summary>
        [InspectorName("战斗/发现敌人")]
        EnemySpotted,
        /// <summary>受击</summary>
        [InspectorName("战斗/受击")]
        Damage,
        /// <summary>占位符</summary>
        [InspectorName("占位符")]
        Placeholder7,
        /// <summary>占位符</summary>
        [InspectorName("占位符")]
        Placeholder8,
        /// <summary>占位符</summary>
        [InspectorName("占位符")]
        Placeholder9,
        /// <summary>选择</summary>
        [InspectorName("大厅/选择")]
        Select,
        /// <summary>新武器</summary>
        [InspectorName("大厅/新武器")]
        NewWeapon,
        /// <summary>解锁升级</summary>
        [InspectorName("大厅/解锁升级")]
        Upgrade,
        /// <summary>占位符</summary>
        [InspectorName("占位符")]
        Placeholder10,
        /// <summary>占位符</summary>
        [InspectorName("占位符")]
        Placeholder11,
        /// <summary>占位符</summary>
        [InspectorName("占位符")]
        Placeholder12,
        /// <summary>占位符</summary>
        [InspectorName("占位符")]
        Placeholder13,
        /// <summary>占位符</summary>
        [InspectorName("占位符")]
        Placeholder14,
        /// <summary>占位符</summary>
        [InspectorName("占位符")]
        Placeholder15,
        /// <summary>占位符</summary>
        [InspectorName("占位符")]
        Placeholder16,
        /// <summary>占位符</summary>
        [InspectorName("占位符")]
        Placeholder17,
        /// <summary>胜利</summary>
        [InspectorName("结算/胜利")]
        Victory,
        /// <summary>失败</summary>
        [InspectorName("结算/失败")]
        Defeat,
    }
}
