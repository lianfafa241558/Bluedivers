using UnityEngine;

/// <summary>全队强化类型</summary>
public enum BoosterType
{
    /// <summary>无</summary>
    [InspectorName("无")]
    Empty,
    /// <summary>生命力强化：全队受到的伤害降低 10%</summary>
    [InspectorName("生命力强化")]
    Vitality,
    /// <summary>无人机侦察：增加所有玩家的有效雷达射程</summary>
    [InspectorName("无人机侦察")]
    Radar,
    /// <summary>增加增援预算：增加可用增援次数</summary>
    [InspectorName("增加增援预算")]
    ReinforcementBudget,
    /// <summary>专家飞行员：降低撤离等待时间</summary>
    [InspectorName("专家飞行员")]
    ExpertPilot,
    /// <summary>定位混淆：加长波次间隔、降低巡逻队生成速度</summary>
    [InspectorName("定位混淆")]
    PositionConfusion,
    /// <summary>护盾强化：增加所有玩家的护盾值(10)</summary>
    [InspectorName("护盾强化")]
    Shield,
    /// <summary>友情护盾：受到的友军伤害降低50%</summary>
    [InspectorName("友情护盾")]
    FriendShield,
    /// <summary>补给大师:吃完补给后所有武器立刻装弹完成，恢复的生命值从50提升到60</summary>
    [InspectorName("补给大师")]
    SuppleMaster,
    /// <summary>疾风二度，冲刺的冷却时间-20%</summary>
    [InspectorName("疾风二度")]
    Windrunner,
    /// <summary>样本拯救者:矿螺(Kei)现在会提供一个护盾,团队样本(已经存放了的)会提升这个护盾的生命值</summary>
    [InspectorName("样本拯救者")]
    SampleSaver,
    /// <summary>荆棘护甲:近战攻击你的敌人会受到24点伤害</summary>
    [InspectorName("荆棘护甲")]
    ThornArmor,

}

/// <summary>
/// 全队强化配置数据
/// 战备第5栏"全队强化"是独立系统（非战备类型），本类保存每个强化项的配置。
/// </summary>
[CreateAssetMenu(fileName = "new Data", menuName = "Data/全队强化")]
public class Booster_SO : ScriptableObject
{
    [InspectorName("ID")]
    public int ID;
    [InspectorName("图标")]
    public Sprite icon;
    [InspectorName("名称")]
    public string showName;
    [TextArea]
    [InspectorName("描述")]
    public string desc;
    [InspectorName("强化类型")]
    public BoosterType type;

    public Color color => new(0.5f, 0.916f, 1f, 1f);
}
