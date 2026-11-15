
using System;
using System.Collections.Generic;
using FPSGame.Core;
using FPSGame.Attributes;
using UnityEngine;
using FPSGame.GameContract;
using FPSGame.Utils;

namespace FPSGame.Data
{
    public enum DirectionEnum
    {
        [InspectorName("左")]
        Left,
        [InspectorName("上")]
        Up,
        [InspectorName("右")]
        Right,
        [InspectorName("下")]
        Down
    }
    public enum AirdropDeliveryEnum
    {
        [InspectorName("空投舱")]
        Pod,
        [InspectorName("轰炸")]
        Bomb,
        [InspectorName("飞鹰")]
        Jet,
        [InspectorName("运输机")]
        Medivac,
    }

    [Flags]
    public enum AirdropLabelEnum
    {
        [InspectorName("背包")]
        Bag=1,
        [InspectorName("无人机")]
        Drone=2,
        [InspectorName("地雷")]
        Mine=4,
        [InspectorName("飞鹰")]
        Jet=8,
        [InspectorName("运输机")]
        Medivac=16,
        [InspectorName("支援武器")]
        SupportWeapon = 32,
    }

    /// <summary>纯展示用标签，不参与 AirdropLabelEnum 的分组与预览模型判定；位值只追加不重排。</summary>
    [Flags]
    public enum AirdropDisplayLabelEnum
    {
        [InspectorName("炸弹")]
        Bomb = 1 << 0,
        [InspectorName("轻甲穿透")]
        LightArmorPenetration = 1 << 1,
        [InspectorName("中甲穿透")]
        MediumArmorPenetration = 1 << 2,
        [InspectorName("重甲穿透")]
        HeavyArmorPenetration = 1 << 3,
        [InspectorName("反坦克")]
        AntiTank = 1 << 4,
        [InspectorName("凤鹰轰炸")]
        EagleBombardment = 1 << 5,
        [InspectorName("轨道武器")]
        OrbitalWeapon = 1 << 6,
        [InspectorName("背包")]
        Backpack = 1 << 7,
        [InspectorName("载具")]
        Vehicle = 1 << 8,
        [InspectorName("无人机")]
        Drone = 1 << 9,
        [InspectorName("燃烧")]
        Incendiary = 1 << 10,
        [InspectorName("毒气")]
        Gas = 1 << 11,
        [InspectorName("电弧")]
        Arc = 1 << 12,
        [InspectorName("冰冻")]
        Freeze = 1 << 13,
        [InspectorName("激光")]
        Laser = 1 << 14,

        [InspectorName("占位符2")]
        placeholder2 = 1 << 15,
        [InspectorName("占位符3")]
        placeholder3 = 1 << 16,
        [InspectorName("占位符4")]
        placeholder4 = 1 << 17,

        [InspectorName("支援武器")]
        SupportWeapon = 1 << 18,
        [InspectorName("占位符5")]
        placeholder6 = 1 << 19,
        
        [InspectorName("逐发装填")]
        RoundsReload = 1 << 20,
        [InspectorName("近战")]
        Melee = 1 << 21,
        [InspectorName("充能")]
        Charge = 1 << 22,
        [InspectorName("固定装弹")]
        FixedAmmo = 1 << 23,
        [InspectorName("消耗品")]
        Consumable = 1 << 24,

        [InspectorName("哨戒炮")]
        Sentry = 1 << 25,
    }


    [CreateAssetMenu(fileName = "new Data", menuName = "Data/战备")]
    public class AirdropData_SO : ScriptableObject
    {


        [InspectorName("在战备配置界面隐藏")]
        public bool isHide;
        public int ID;
        public string showName;
        [TextArea]
        public string desc;
        [SpritePreview(4,4)]
        [InspectorName("图标")]
        public Sprite icon;
    

        [Header("费用")]
        [InspectorName("购买价格")]
        public List<SKVP<OOPartEnum, int>> cost = new() { new(OOPartEnum.Pyroxene, 1000) };

        [InspectorName("操作")]
        public DirectionEnum[] opter;
        [InspectorName("类型")]
        public AirdropType type;
        [InspectorName("投送方式")]
        public AirdropDeliveryEnum deliveryType;
        [InspectorName("标签")]
        public AirdropLabelEnum labels;
        /// <summary>只供界面展示标签文字；分组与预览模型仍看 labels。</summary>
        [InspectorName("显示标签")]
        public AirdropDisplayLabelEnum displayLabels;

        [Space]
        [InspectorName("冷却")]
        public int cool;

        [InspectorName("部署时间")]
        [Range(0, 20)]
        public int arriveTime=8;
        [InspectorName("部署高度")]
        [Compare("deliveryType", (int)AirdropDeliveryEnum.Pod, CompareOperate.Equal)]
        public int arriveHeight;

        [InspectorName("部署次数")]
        [Range(0, 20)]
        public int arriveCount = 0;

        [InspectorName("附属战备")]
        public int[] subAirdrop;

        [InspectorName("冷却组")]
        public string coolGroup;

        [Space]
        [InspectorName("影响范围的显示")]
        public Vector2 showRange;

        [InspectorName("持续时间")]
        public int sustainTime;
        [InspectorName("创建的物体")]
        public GameObject creatObect;

        [Space]
        [InspectorName("使用标准空投舱")]
        [Compare("deliveryType", (int)AirdropDeliveryEnum.Pod, CompareOperate.Equal)]
        public bool useNormalPod;
        [InspectorName("持续时间时隐藏信息")]
        public bool sustainHideBeacon;
        [InspectorName("危险警告")]
        public bool useWarning;

        [InspectorName("空投舱永久存在")]
        [Compare("useNormalPod",1, CompareOperate.Equal)]
        public bool permanentPod;

        [InspectorName("需要授权")]
        public bool authorize;

        [InspectorName("未授权时可见")]
        public bool unAuthorizeVisible;

        [InspectorName("直接释放")]
        public bool isDirect;

        [InspectorName("死亡时可用")]
        public bool deathEnable;

        [InspectorName("忽略干扰")]
        public bool ignoreJammer;

        /// <summary>团队冷却：任一玩家释放本战备 ⇒ **每人本机那份同名战备**都进冷却（跨机器，靠 4033/4034 呼叫同步触发）。
        /// ⚠ 直接释放(isDirect)的战备不走信标、也不走呼叫同步 ⇒ 目前对它们不生效（如增援 HealBag）。</summary>
        [InspectorName("团队冷却")]
        public bool teamCool;
        public Color Color { 
            get
            {
                return colors[(int)type];
            }
        }
        public Color IconColor
        {
            get
            {
                return iconColors[(int)type];
            }
        }
        private static readonly Color[] colors = new Color[] { new Color(1f,0.5f, 0.5f), new Color(0.5f, 0.8f, 1f), new Color(0.7f, 1f, 0.55f), new Color(1, 0.7f, 0.5f), new Color(0.86f, 0.81f, 0.61f), Color.white };

        private static readonly Color[] iconColors = new Color[] { new Color(0.9f, 0.36f, 0.36f), new Color(0.32f, 0.72f, 0.9f), new Color(0.44f, 0.6f, 0.36f), new Color(1, 0.57f, 0.3f), new Color(0.86f, 0.78f, 0.43f), Color.white };

        private static readonly string[] typeName=new string[] { "进攻型战略配备", "支援型战略配备", "防御型战略配备", "载具型战略配备", "特殊型战略配备" };
        private const string attrName = "部署时间\n使用次数\n冷却时间";

        public string TypeName
        {
           get=> typeName[(int)type];
        }
    
        public string AttrName
        {
            get => attrName;
        }
        public string AttrValue
        {
            get => arriveTime+"秒\n"+(arriveCount>0? arriveCount+"":"无限")+"\n"+(cool>900?"无法冷却": cool+"秒");
        }

        public string GetDisplayLabelText(string prefix = "")
        {
            var sb = new System.Text.StringBuilder();
            ulong bits = Convert.ToUInt64(displayLabels);

            foreach (AirdropDisplayLabelEnum value in Enum.GetValues(typeof(AirdropDisplayLabelEnum)))
            {
                ulong bit = Convert.ToUInt64(value);
                if (bit == 0) continue;              // 跳过 None
                if ((bits & bit) == 0) continue;     // 未勾选

                if (sb.Length > 0) sb.Append('\n');
                sb.Append(prefix).Append(Tool.GetEnumString(value));
            }
            return sb.ToString();
        }


        public enum AirdropType{
            /// <summary>轰炸</summary>
            [InspectorName("轰炸")]
            Red,
            /// <summary>装备</summary>
            [InspectorName("装备")]
            Blue,
            /// <summary>炮台</summary>
            [InspectorName("炮台")]
            Greed,
            /// <summary>载具</summary>
            [InspectorName("载具")]
            Orange,
            /// <summary>补给</summary>
            [InspectorName("补给")]
            Yellow,
        }
    }
}
