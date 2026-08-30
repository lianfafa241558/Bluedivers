using System.Collections;
using System.Collections.Generic;
using Core.Interface;
using FPSGame.Attribute;
using FPSGame.Furn;

using UnityEngine;

public class Furniture_EquipActor : Furniture_Equip, I_Entity
{
    [Foldout("信息", true)]
    [InspectorName("名称")]
    public string showName;

    public string id;
    [InspectorName("头像")]
    public Sprite Portrait;
    [InspectorName("额外图标")]
    public Sprite ExtraPortrait;


    public virtual float HalfRange => 1;

    /// <summary>
    /// 单位半高度：单位竖直占位区间 = [CenterPos.y - HalfHeight, CenterPos.y + HalfHeight]
    /// 0 表示未配置，需要做竖直判定的逻辑应退化为"不做高度过滤"
    /// </summary>
    public virtual float HalfHeight => 0;

    public override string ShowName { get => showName; }
    public override string Id { get => id; }

    protected override Sprite Icon { get => Portrait; }

    string I_Entity.ShowName { get => showName; set => showName = value; }
    string I_Entity.Id { get => id; set => id = value; }

    Sprite I_Entity.Portrait { get => Portrait; set => Portrait = value; }
    Sprite I_Entity.ExtraPortrait { get => ExtraPortrait; set => ExtraPortrait = value; }
    Color I_Entity.Color { get => Color.white; set { } }

}
