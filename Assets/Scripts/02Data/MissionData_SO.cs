using System.Collections;
using System.Collections.Generic;
using FpsGame.Mission;
using Unity.BaseTool;
using UnityEngine;

[CreateAssetMenu(fileName = "new Data", menuName = "Data/支线任务配置")]
public class MissionData_SO : ScriptableObject
{

    [CustomLabel("类型")]
    public MissionEnum type;
    public Sprite sprite;
    public string desc;
    [CustomLabel("主控制器")]
    public MissionBase controller;//仅用于创建，任务内部不使用

    public Vector2Int reward;//仅用于创建，任务内部不使用
    [Header("任务所需战备")]
    public List<AirdropData_SO> RequiredAD;
    
}
