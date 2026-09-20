
using UnityEngine;
using UnityEngine.Events;
using Utils;
using Core.Interface;
/// <summary>
/// 限时存活组件：由 <see cref="VFXManager"/> 的对象池按帧轮询 <see cref="IsAlive"/>，
/// 返回 false 时对象才被回收(触发 <see cref="OnHide"/> 与 <see cref="OnEnd"/>)。
/// 配置 <see cref="EndDelay"/> 后，寿命结束时先触发一次 <see cref="OnEnd"/>，
/// 再等该秒数才正式回收，用于让特效"结束"后再残留一段时间。
/// </summary>
public class LimitedLife : MonoBehaviour,IRecyclable
{
    [SerializeField]
    private bool IsDestroy;//这玩意目前还没效果
    [SerializeField]
    private float LiftTime;
    [InspectorName("提前释放系数1=不允许")]
    [SerializeField]
    [Range(0, 1)]
    private float PreRelease = 1;
    [InspectorName("允许可见时释放")]
    [SerializeField]
    private bool allowSeeRelease;
    [InspectorName("延时回收(秒)")]
    [Tooltip("寿命结束后先触发 OnEnd，再等该秒数才正式回收(0=到点立即回收)；外部强制回收(allowRelease/VFXManager.Release)不受此延时影响")]
    [SerializeField]
    private float EndDelay;

    private float CreatTime;
    /// <summary>本次存活是否已触发过 OnEnd，避免"延时回收"期间重复触发</summary>
    private bool endInvoked;

    [InspectorName("允许释放")]
    public bool allowRelease;
    public bool useDebug;

    public UnityEvent OnEnd;
    public float showScale;

    public bool IsAlive()
    {
        showScale = (Time.time - CreatTime) / LiftTime;
        //if(useDebug) Debug.LogError("超时" + (Time.time - CreatTime)+"/" + LiftTime);
        if (allowRelease) return false;//外部强制回收：立即放行，不走延时
        float endTime = CreatTime + LiftTime;
        if (Time.time < endTime) return true;
        //寿命结束：先触发一次 OnEnd，等 EndDelay 秒后才允许对象池正式回收
        InvokeEnd();
        return Time.time < endTime + EndDelay;
    }
    public bool AllowPreRelease()
    {
        //if (useDebug) Debug.LogError("不在屏幕"+(!Tool.IsScreenVisible(transform.position))+"超时"+(CreatTime + LiftTime * PreRelease)+"/"+ Time.time);
        return (allowSeeRelease||!Tool.IsScreenVisible(transform.position)) && Time.time > CreatTime + LiftTime * PreRelease + EndDelay;
    }

    /// <summary>只改寿命长度，不重置计时起点；寿命被延长回存活期内时允许再次触发 OnEnd</summary>
    public void SetLift(float lift)
    {
        LiftTime = lift;
        if (Time.time < CreatTime + LiftTime) endInvoked = false;
    }
    public void ResetLift(float lift)
    {
        CreatTime = Time.time;
        LiftTime = lift;
        endInvoked = false;
    }
    /// <summary>运行时覆盖延时回收时长(秒)</summary>
    public void SetEndDelay(float delay) => EndDelay = delay;

    public void OnShow()
    {
        CreatTime = Time.time;
        allowRelease = false;
        endInvoked = false;
    }

    public void OnHide()
    {
        //延时回收期间已提前触发过 OnEnd 时跳过，避免重复触发
        InvokeEnd();
    }

    /// <summary>触发一次 OnEnd(同一次存活内只触发一次)</summary>
    private void InvokeEnd()
    {
        if (endInvoked) return;
        endInvoked = true;
        OnEnd?.Invoke();
    }
}
