using FPSGame.GameContract;
using UnityEngine;

namespace FPSGame.Data
{


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
}
