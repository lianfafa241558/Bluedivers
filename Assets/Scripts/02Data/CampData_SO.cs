using System.Collections.Generic;
using Core;
using GameContract;
using System.Linq;
using UnityEngine;


[CreateAssetMenu(fileName = "new Data", menuName = "Data/阵营配置")]
public class CampData_SO : ScriptableObject
{

  

    /// <summary>波次间隔</summary>
    //[Foldout("波次", true)]
    [InspectorName("波次间隔(秒)")]
    public int WaveCool;
    /// <summary>波次使用物体</summary>
    [InspectorName("波次使用物体")]
    public List<GameObject> WaveUseObject;

    [InspectorName("波次模板")]
    public List<CampTemplate> templates;


    //[Foldout("巡逻队",true)]

    [Space(16)]
    [InspectorName("巡逻队创建基准时间")]
    public int PatrolCreatValue;
    [InspectorName("巡逻队模板")]
    public List<PatrolCfg> patrolCfgs;

    [Foldout("基本", true)]
    /// <summary>类型</summary>
    [InspectorName("类型")]
    public EnemyVarietyType enemyVarietyType;
    /// <summary>名称</summary>
    [InspectorName("名称")]
    public string ShowName;
    /// <summary>图标</summary>
    [InspectorName("图标")]
    public Sprite Sprite;
    /// <summary>击杀图标</summary>
    [InspectorName("击杀图标")]
    public Sprite KillSprite;

    /// <summary>颜色</summary>
    [InspectorName("颜色")]
    public Color Color;

    /// <summary>语音后缀</summary>
    [InspectorName("语音后缀")]
    public string Suffix;

    /// <summary>描述</summary>
    [TextArea(3, 5)]
    public string Desc;


    [Foldout("任务", true)]

    [InspectorName("允许的主线类型")]
    public MissionEnum[] mainTypes;
    [InspectorName("允许的额外类型")]
    public MissionEnum[] extraTypes;

    [InspectorName("巢穴类型")]
    public MissionEnum[] nestTypes;

    [InspectorName("备份的主线类型")]
    public MissionEnum[] mainTypesBackup;



    /*
    [ContextMenu("转换")]
    public void trans()
    {
        Templates.ForEach((key, value) => templates.Add(new() {
            name = key,
            Template = new(value.Template),
            PatrolTemplate = new(value.PatrolTemplate)
        }));
        Patrol.ForEach((key, value) => patrolCfgs.Add(new() {
            name = key,
            units = value.Template.Select(item => new SKVP<UnitTier, int>(item.Key,item.Value)).ToList()
        }));
    }*/

    [System.Serializable]
    public class PatrolCfg
    {
        public string name;
        public List<SKVP<UnitTier, int>> units;
    }

    [System.Serializable]
    public class CampTemplate
    {
        public string name;

        public List<TierCfg> template;

        [InspectorName("巡逻队类型")]
        public List<string> PatrolTemplate;

    }

    [System.Serializable]
    public struct UnitWeightCfg
    {
        /// <summary>预制体</summary>
        public GameObject unit;
        /// <summary>概率 </summary>
        public int weight;
        /// <summary>占据补给量</summary>
        public int size;
    }

    [System.Serializable]
    public struct TierCfg
    {
        public UnitTier tier;
        //[InspectorName("权重")]
        public int weight;
        //[Header("单位类型")]
        public List<UnitWeightCfg> unitWeights;
    }


}





