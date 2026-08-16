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
