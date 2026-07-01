
using UnityEngine;
using UnityEngine.Events;
using Utils;
using Core.Interface;
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

    private float CreatTime;
    [InspectorName("允许释放")]
    public bool allowRelease;
    public bool useDebug;
    public UnityAction OnEnd;
    public float showScale;

    public bool IsAlive()
    {
        showScale = (Time.time - CreatTime) / LiftTime;
        //if(useDebug) Debug.LogError("超时" + (Time.time - CreatTime)+"/" + LiftTime);
        return !allowRelease && Time.time < CreatTime + LiftTime;
    }
    public bool AllowPreRelease()
    {
        //if (useDebug) Debug.LogError("不在屏幕"+(!Tool.IsScreenVisible(transform.position))+"超时"+(CreatTime + LiftTime * PreRelease)+"/"+ Time.time);
        return (allowSeeRelease||!Tool.IsScreenVisible(transform.position)) && Time.time > CreatTime + LiftTime * PreRelease;
    }

    public void SetLift(float lift) => LiftTime = lift;
    public void ResetLift(float lift)
    {
        CreatTime = Time.time;
        LiftTime = lift;
    }

    public void OnShow()
    {
        CreatTime = Time.time;
        allowRelease = false;
    }

    public void OnHide()
    {
        OnEnd?.Invoke();
    }
}
