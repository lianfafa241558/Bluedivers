using UnityEngine.Events;

/// <summary>
/// PlayerController 分部：为手持装备（HandEquip）/承载点位（PlayerMountPoint）提供玩家生命周期事件。
/// - OnEnterVehicle：玩家进载具（或被外部禁用 PlayerController）时触发，用于进载具丢下不可携带装备。
/// - OnBodySet：玩家模型动态加载完成后（SetBody 末尾）触发，用于在模型就绪后解析动态模型内的 IK/背部点位。
/// </summary>
public partial class PlayerController
{
    /// <summary>玩家进入载具（或被外部禁用 PlayerController）时触发</summary>
    public event UnityAction OnEnterVehicle;

    /// <summary>玩家模型加载完成后触发（SetBody 末尾广播）</summary>
    public event UnityAction OnBodySet;
}
