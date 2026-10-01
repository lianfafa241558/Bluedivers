using System.Collections;
using System.Collections.Generic;
using FPSGame.Mission;
using FPSGame.Attributes;
using UnityEngine;
using FPSGame.Data;
using FPSGame.GameContract;

namespace FPSGame.GameData
{

[CreateAssetMenu(fileName = "new Data", menuName = "Data/支线任务配置")]
public class MissionData_SO : ScriptableObject
{
    /// <summary>
    /// 任务配置总表（**数据自持**，2026-09-30 为 P5 加入）：由 <c>TaskManager</c> 加载任务配置时写入。
    ///
    /// <para>▍为什么：玩法层的 <c>TaskCfg</c>（已从 TaskManager 解嵌套、搬到本层）原先读
    /// <c>TaskManager.Instance.Missions</c> ⇒ 那是向上依赖。改成读本表即可留在玩法层。</para>
    /// </summary>
    public static Dictionary<MissionEnum, MissionData_SO> Catalog { get; set; }


    [InspectorName("类型")]
    public MissionEnum type;
    [SpritePreview(4,4)]
    public Sprite sprite;
    public string desc;
    [InspectorName("主控制器")]
    public MissionBase controller;//仅用于创建，任务内部不使用

    public Vector2Int reward;//仅用于创建，任务内部不使用
    [Header("任务所需战备")]
    public List<AirdropData_SO> RequiredAD;
    
}
}
