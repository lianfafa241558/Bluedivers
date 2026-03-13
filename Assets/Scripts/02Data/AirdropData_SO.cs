using Unity.BaseTool;
using UnityEngine;
public enum DirectionEnum
{
    [CustomLabel("左")]
    Left,
    [CustomLabel("上")]
    Up,
    [CustomLabel("右")]
    Right,
    [CustomLabel("下")]
    Down
}
public enum AirdropDeliveryEnum
{
    [CustomLabel("空投舱")]
    Pod,
    [CustomLabel("轰炸")]
    Bomb,
    [CustomLabel("飞鹰")]
    Jet,
}


[CreateAssetMenu(fileName = "new Data", menuName = "Data/战备")]
public class AirdropData_SO : ScriptableObject
{
    [CustomLabel("隐藏战备")]
    public bool isHide;
    public int ID;
    public string showName;
    [TextArea]
    public string desc;
    [CustomLabel("图标")]
    public Sprite icon;
    [CustomLabel("操作")]
    public DirectionEnum[] opter;
    [CustomLabel("类型")]
    public AirdropType type;
    [CustomLabel("投送方式")]
    public AirdropDeliveryEnum deliveryType;
    [CustomLabel("冷却")]
    public int cool;

    [CustomLabel("部署时间")]
    [Range(0, 20)]
    public int arriveTime=8;

    [CustomLabel("部署高度", "deliveryType", (int)AirdropDeliveryEnum.Pod, CompareOperate.Equal)]
    public int arriveHeight;

    [CustomLabel("部署次数")]
    [Range(0, 20)]
    public int arriveCount = 0;

    [CustomLabel("影响范围的显示")]
    public Vector2 showRange;

    [CustomLabel("持续时间")]
    public int sustainTime;
    [CustomLabel("创建的物体")]
    public GameObject creatObect;
    [CustomLabel("使用标准空投舱", "deliveryType", (int)AirdropDeliveryEnum.Pod, CompareOperate.Equal)]
    public bool useNormalPod;
    [CustomLabel("持续时间时隐藏信标")]
    public bool sustainHideBeacon;
    [CustomLabel("危险警告")]
    public bool useWarning;
    [CustomLabel("空投舱永久存在", "useNormalPod")]
    public bool permanentPod;

    [CustomLabel("需要允许部署")]
    public bool authorize;

    public Color Color { 
        get
        {
            return colors[(int)type];
        }
    }
    private static readonly Color[] colors = new Color[] { new Color(1f, 0.5f, 0.5f), new Color(0.5f, 0.8f, 1f), new Color(0.5f, 0.8f, 0.5f), new Color(1, 0.7f, 0.5f), new Color(1f, 1f, 0.5f), Color.white };

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
        get => arriveTime+"秒\n"+(arriveCount>0? arriveCount+"次":"无限")+"\n"+(cool>900?"无法冷却": cool+"秒");
    }


    public enum AirdropType{
        [CustomLabel("轰炸")]
        Red,
        [CustomLabel("装备")]
        Blue,
        [CustomLabel("炮台")]
        Greed,
        [CustomLabel("载具")]
        Orange,
        [CustomLabel("补给")]
        Yellow,
    }
}
