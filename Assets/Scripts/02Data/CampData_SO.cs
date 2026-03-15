using System.Collections;
using System.Collections.Generic;
using Core;
using Unity.BaseTool;
using UnityEngine;

[CreateAssetMenu(fileName = "new Data", menuName = "Data/阵营配置")]
public class CampData_SO : ScriptableObject
{

    /// <summary>类型</summary>
    [CustomLabel("类型")]
    public EnemyVarietyType enemyVarietyType;
    /// <summary>名称</summary>
    [CustomLabel("名称")]
    public string ShowName;
    /// <summary>图标</summary>
    [CustomLabel("图标")]
    public Sprite Sprite;
    /// <summary>击杀图标</summary>
    [CustomLabel("击杀图标")]
    public Sprite KillSprite;

    /// <summary>颜色</summary>
    [CustomLabel("颜色")]
    public Color Color;

    /// <summary>语音后缀</summary>
    [CustomLabel("语音后缀")]
    public string Suffix;

    /// <summary>描述</summary>
    [TextArea(3,5)]
    public string Desc;

    [Space(16)]
    /// <summary>波次间隔</summary>
    [Header("波次")]
    [CustomLabel("波次间隔")]
    public int WaveCool;
    /// <summary>波次使用物体</summary>
    [CustomLabel("波次使用物体")]
    public List<GameObject> WaveUseObject;

    /// <summary>模板</summary>
    [CustomLabel("模板")]
    public DisplayDic<string, CampTemplate> Templates;

    [Header("巡逻队")]
    [CustomLabel("巡逻队")]
    public DisplayDic<string, List<GameObject>> Patrol;


    [Space(16)]
    [Header("允许的主线类型")]
    [CustomLabel("允许的主线类型")]
    public MissionEnum[] mainTypes;
    [Header("允许的额外类型")]
    [CustomLabel("允许的额外类型")]
    public MissionEnum[] extraTypes;

    [Header("巢穴类型")]
    [CustomLabel("巢穴类型")]
    public MissionEnum[] nestTypes;



    [System.Serializable]
    public class CampTemplate
    {
        public List<KVP<GameObject, Vector2Int>> Template;
        public List<string> PatrolName;
    }



}

