using UnityEngine;

[CreateAssetMenu(fileName = "new Data", menuName = "Data/主线任务配置")]
public class MissionMainData_SO : MissionData_SO
{
    [InspectorName("任务类型名称")]
    public new string name;
    [InspectorName("任务颜色")]
    public Color color;
    [InspectorName("任务地图大小")]
    public SizeType sizeType;
    [InspectorName("任务需求的进度数量")]
    public Vector2Int count;//仅用于创建，任务内部不使用

    [InspectorName("撤离类型")]
    public MissionEnum evacuateType;
    [InspectorName("子任务类型")]
    public MissionEnum[] subType;

}
