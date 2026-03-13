using System.Collections;
using System.Collections.Generic;
using Unity.BaseTool;
using UnityEngine;

[CreateAssetMenu(fileName = "new Data", menuName = "Data/主线任务配置")]
public class MissionMainData_SO : MissionData_SO
{
    [CustomLabel("任务类型名称")]
    public new string name;
    [CustomLabel("任务颜色")]
    public Color color;
    [CustomLabel("任务地图大小")]
    public SizeType sizeType;
    [CustomLabel("任务需求的进度数量")]
    public Vector2Int count;//仅用于创建，任务内部不使用

    [CustomLabel("撤离类型")]
    public MissionEnum evacuateType;


}
