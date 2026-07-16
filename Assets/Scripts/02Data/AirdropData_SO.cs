using Unity.BaseTool;
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
}


[CreateAssetMenu(fileName = "new Data", menuName = "Data/战备")]
public class AirdropData_SO : ScriptableObject
{
    [InspectorName("隐藏战备")]
    public bool isHide;
    public int ID;
    public string showName;
    [TextArea]
    public string desc;
    [InspectorName("图标")]
    public Sprite icon;
    [InspectorName("操作")]
    public DirectionEnum[] opter;
    [InspectorName("类型")]
    public AirdropType type;
    [InspectorName("投送方式")]
    public AirdropDeliveryEnum deliveryType;
    [InspectorName("冷却")]
    public int cool;

    [InspectorName("部署时间")]
    [Range(0, 20)]
    public int arriveTime=8;

    [CustomLabel("部署高度", "deliveryType", (int)AirdropDeliveryEnum.Pod, CompareOperate.Equal)]
    public int arriveHeight;

    [InspectorName("部署次数")]
    [Range(0, 20)]
    public int arriveCount = 0;

    [InspectorName("影响范围的显示")]
    public Vector2 showRange;

    [InspectorName("持续时间")]
    public int sustainTime;
    [InspectorName("创建的物体")]
    public GameObject creatObect;
    [CustomLabel("使用标准空投舱", "deliveryType", (int)AirdropDeliveryEnum.Pod, CompareOperate.Equal)]
    public bool useNormalPod;
    [InspectorName("持续时间时隐藏信标")]
    public bool sustainHideBeacon;
    [InspectorName("危险警告")]
    public bool useWarning;
    [CustomLabel("空投舱永久存在", "useNormalPod")]
    public bool permanentPod;

    [InspectorName("需要允许部署")]
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
        [InspectorName("轰炸")]
        Red,
        [InspectorName("装备")]
        Blue,
        [InspectorName("炮台")]
        Greed,
        [InspectorName("载具")]
        Orange,
        [InspectorName("补给")]
        Yellow,
    }
}
