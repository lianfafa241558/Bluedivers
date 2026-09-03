
using Core;
using FPSGame.Attribute;
using UnityEngine;
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
    

    [InspectorName("操作")]
    public DirectionEnum[] opter;
    [InspectorName("类型")]
    public AirdropType type;
    [InspectorName("投送方式")]
    public AirdropDeliveryEnum deliveryType;

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
    public int subAirdrop = 0;

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
    private static readonly Color[] colors = new Color[] { new Color(1f,0.5f, 0.5f), new Color(0.5f, 0.8f, 1f), new Color(0.73f, 1f, 0.6f), new Color(1, 0.7f, 0.5f), new Color(0.86f, 0.81f, 0.61f), Color.white };

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
