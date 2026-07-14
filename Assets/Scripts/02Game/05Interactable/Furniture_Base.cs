using System;
using System.Collections;
using System.Collections.Generic;
using Core;
using Core.Interface;
using FPSGame.Attribute;
using FPSGame.Furn;

using Unity.FPS.Game;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.AI;



public class Furniture_Base : Furniture_Attached, IFurniture, I_Entity
{
    #region 基础
    [Foldout("信息", true)]
    [InspectorName("名称")]
    public new string ShowName;

    public new string Id;
    [InspectorName("头像")]
    public Sprite Portrait;
    [InspectorName("额外图标")]
    public Sprite ExtraPortrait;

    [InspectorName("颜色")]
    public Color Color = Color.white;

    public virtual float HalfRange => 1;

    protected override Sprite Icon { get => Portrait; }
    string I_Entity.ShowName { get => ShowName; set => ShowName = value; }
    string I_Entity.Id { get => Id; set => Id = value; }
    Sprite I_Entity.Portrait { get => Portrait; set => Portrait = value; }
    Sprite I_Entity.ExtraPortrait { get => ExtraPortrait; set => ExtraPortrait = value; }
    Color I_Entity.Color { get => Color; set => Color = value; }
    #endregion

    #region 相关
    [Foldout("关联", true)]
    [SerializeField]
    public Transform relatedTrans;
    [SerializeField]
    protected Transform relatedTrans2;
    [InspectorName("外部浮点数参数")]
    public float ExtFloatParameter;
    [InspectorName("外部布尔参数")]
    public bool ExtBoolParameter;

    [DisplayField(true, false)]
    [SerializeField]
    protected ParticleSystem particle;
    [DisplayField(true, false)]
    [SerializeField]
    protected NavMeshObstacle obs;

    protected override void Awake()
    {
        base.Awake();
        particle = GetComponentInChildren<ParticleSystem>(true);
        obs = GetComponent<NavMeshObstacle>();
    }

    #endregion
}
