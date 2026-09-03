using UnityEngine;


/// <summary>
/// 逐步长按/持续提交家具接口。
///
/// 实现此接口的家具将由 <see cref="PlayerOperationController"/> 在长按交互时采用
/// "接管按压"模式驱动（而非其默认的固定 MeetTime 到点触发）：
/// - 按下瞬间调用 <see cref="BeginPress"/>，由家具锁定本次需求与总时长；
/// - 按住期间每帧调用 <see cref="StepPress"/>，家具可按时间逐步推进（如逐颗扣减），
///   返回 true 表示本次已转完，控制器随即调用 <see cref="IFurniture.Handle"/> 收尾；
/// - 松开调用 <see cref="CancelPress"/>，家具可保留已推进的进度。
/// </summary>
public interface IStepPress
{
    /// <summary>该家具是否允许以接管按压模式交互（需在成为目标前保持为 true）</summary>
    bool CanOperateStepped(GameObject unit);

    /// <summary>按下交互键瞬间调用：锁定本次需求并复位进度，返回 true 表示开始长按</summary>
    bool BeginPress(GameObject unit);

    /// <summary>按住期间逐帧推进，返回 true 表示本次已全部转完，可结束</summary>
    bool StepPress(float deltaTime);

    /// <summary>松开交互键：取消/暂存本次按压状态</summary>
    void CancelPress();
}
