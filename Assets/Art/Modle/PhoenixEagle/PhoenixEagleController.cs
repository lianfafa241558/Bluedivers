using Core.Interface;

using UnityEngine;
using UnityEngine.Events;
using Utils;

public class PhoenixEagleController : MonoBehaviour, IRecyclable
{
    [Header("飞行参数")]

    [SerializeField]
    [InspectorName("初始高度偏移速度系数")]
    private float startHeightOffestSpeedScale = 1.3f;
    [SerializeField]
    [InspectorName("初始水平偏移速度系数")]
    private float startHorizontalOffestSpeedScale = 2.6f;
    [SerializeField]
    [InspectorName("拉升高度偏移速度系数")]
    private float climbHeightOffestSpeedScale = 1.5f;
    [SerializeField]
    [InspectorName("拉升水平偏移速度系数")]
    private float climbHorizontalOffestSpeedScale = 2f;



    [SerializeField]
    [InspectorName("俯冲速度")]
    private float diveSpeed = 200f;

    [SerializeField]
    [InspectorName("拉升速度")]
    private float climbSpeed = 150f;


    [SerializeField]
    [InspectorName("停留高度")]
    private float waitHeight = 70f;

    [SerializeField]
    [InspectorName("停留时间")]
    private float waitTime = 0;

    [Header("轨迹控制")]
    [SerializeField]
    [InspectorName("俯冲曲线高度")]
    private float diveCurveHeight = 30f;

    [SerializeField]
    [InspectorName("拉升曲线高度")]
    private float climbCurveHeight = 40f;

    [SerializeField]
    [InspectorName("平滑时间")]
    private float smoothTime = 0.3f;


    [Header("其他")]
    [InspectorName("如果是在场景中直接测试要勾上")]
    [SerializeField]
    private bool useAwake = false;
    [Header("事件")]

    [SerializeField]
    private UnityEvent onDiving;
    [SerializeField]
    public UnityEvent onWait;
    [SerializeField]
    private UnityEvent onClimbing;


    // 目标点
    [SerializeField]
    private Vector3 targetA;                 // 轰炸点正上方
    [SerializeField]
    private Vector3 targetB;                 // 轰炸点后上方
    [SerializeField]
    private Vector3 startPoint;              // 起始点（前上方）

    // 贝塞尔曲线控制点
    private Vector3 diveControlPoint;        // 俯冲曲线控制点
    private Vector3 climbControlPoint;       // 拉升曲线控制点

    // 移动状态
    private Vector3 lastPos;
    private Vector3 _vel = Vector3.zero;

    [SerializeField]
    private float progress = 0f;      // 当前阶段进度 0-1
    [SerializeField]
    private FlightPhase currentPhase = FlightPhase.Diving;
    private float initialClimbSpeed = 0f;    // 记录拉升阶段的初始速度

    private enum FlightPhase
    {
        Diving,      // 俯冲阶段
        Wait,        // 等待阶段
        Climbing     // 拉升阶段
    }
    float time;
    public void OnShow()
    {
        //Debug.Log("创建点" + transform.position);

        InitializePoints();
        //Debug.Log("初始点" + startPoint);
        //Debug.Log("轰炸点" + targetA);
        //Debug.Log("拉升目标" + targetB);

        currentPhase = FlightPhase.Diving;
        initialClimbSpeed = 0f;
        progress = 0;
        //Debug.Log("传送到初始点" + startPoint);
        transform.position = startPoint;
        _vel = Vector3.zero;
        lastPos = transform.position - transform.forward;
        time = Time.time;
        diveStartTime = Time.time;
        onDiving?.Invoke();
    }

    public void OnHide()
    {
        
    }

    private void Awake()
    {
        if (useAwake) OnShow();
    }

    /// <summary>
    /// 初始化路径点和贝塞尔曲线控制点
    /// </summary>
    private void InitializePoints()
    {

        Vector3 loginDir = transform.forward.ToVector2().ToVector3().normalized;

        startPoint = transform.position - loginDir * startHorizontalOffestSpeedScale * diveSpeed + Vector3.up * startHeightOffestSpeedScale * diveSpeed;
        targetA = transform.position + waitHeight * Vector3.up;
        targetB = transform.position + climbHeightOffestSpeedScale * climbSpeed * Vector3.up + loginDir * climbHorizontalOffestSpeedScale * climbSpeed;


        // 俯冲控制点：起始点到A点的曲线控制点
        Vector3 midPointDive = (startPoint + targetA) / 2f;
        midPointDive.y += diveCurveHeight;
        diveControlPoint = midPointDive;


        // 拉升控制点：A点到B点的曲线控制点
        Vector3 midPointClimb = (targetA + targetB) / 2f;
        midPointClimb.y += climbCurveHeight;
        climbControlPoint = midPointClimb;
    }




    private void Update()
    {
        UpdateMovement();
        //transform.forward =Vector3.Lerp(transform.forward, (transform.position - lastPos).normalized,Time.deltaTime*5);
        if((transform.position - lastPos)!=default) transform.forward = (transform.position - lastPos).normalized;
        lastPos = transform.position;
    }

    private float waitStartTime;
    private float diveStartTime;
    private void UpdateMovement()
    {
        float diveDuration = CalculateDuration(diveSpeed, startPoint, targetA);

        switch (currentPhase)
        {
            case FlightPhase.Diving:
                UpdateDivingMovement();
                break;
            case FlightPhase.Wait:
                progress = Mathf.Clamp01((Time.time- waitStartTime)/waitTime);
                if (progress >= 1f)
                {
                    SwitchToClimbingPhase();
                    initialClimbSpeed = climbSpeed/5f;
                }
                break;
            case FlightPhase.Climbing:
                // 拉升阶段使用基于速度的进度更新
                UpdateClimbingMovement();
                break;
        }
    }

    private float GetDivingSpeed(float t)
    {
        if(waitTime>0) return Mathf.Lerp(diveSpeed, diveSpeed/5f, t*10-9f);
        return diveSpeed;
    }

    /// <summary>
    /// 切换到等待阶段
    /// </summary>
    private void SwitchToWaitPhase()
    {
        waitStartTime = Time.time;
        lastPos.y = transform.position.y;//保证水平
        currentPhase = FlightPhase.Wait;
        progress = 0f;
        //Debug.Log("到点用时" + (Time.time - time));
        onWait?.Invoke();
    }

    /// <summary>
    /// 切换到拉升阶段
    /// </summary>
    private void SwitchToClimbingPhase()
    {
        //transform.position = targetA;
        lastPos.y = transform.position.y;//保证水平
        currentPhase = FlightPhase.Climbing;
        progress = 0f;
        onClimbing?.Invoke();
        // 获取当前速度作为拉升的初始速度
        //float currentSpeed = Vector3.Distance(lastPos, transform.position) / Time.deltaTime;

        initialClimbSpeed = climbSpeed;
        //Debug.LogError("到点用时"+(Time.time-time));
        //3.73左右
        //现在是4.8
    }
    private void UpdateDivingMovement()
    {
        float currentSpeed = GetDivingSpeed(progress);
        float step = currentSpeed * Time.deltaTime;
        float totalDistance = Vector3.Distance(startPoint, targetA);
        float progressDelta = step / totalDistance;

        progress = Mathf.Clamp01(progress + progressDelta);

        // 到达终点后切换到拉升阶段
        if (progress >= 1f)
        {
            if (waitTime > 0) SwitchToWaitPhase();
            else SwitchToClimbingPhase();
            return;
        }
        // 计算目标位置
        Vector3 targetPos = CalculateBezierPosition(FlightPhase.Diving, progress);
        transform.position = Vector3.SmoothDamp(transform.position, targetPos, ref _vel, smoothTime);
    }
    private void UpdateClimbingMovement()
    {
        float currentSpeed = GetClimbSpeed(progress*10);
        float step = currentSpeed * Time.deltaTime;
        float totalDistance = Vector3.Distance(targetA, targetB);
        float progressDelta = step / totalDistance;

        progress = Mathf.Clamp01(progress + progressDelta);

        // 到达终点后结束或循环
        if (progress >= 1f)
        {
            progress = 1f;
            OnFlightComplete();
        }

        // 计算目标位置
        Vector3 targetPos = CalculateBezierPosition(FlightPhase.Climbing, progress);
        transform.position = Vector3.SmoothDamp(transform.position, targetPos, ref _vel, smoothTime);
    }

  
    private float GetClimbSpeed(float t)
    {
        // 保持恒定速度，结束时不变慢
        return Mathf.Lerp(initialClimbSpeed, climbSpeed, t);
    }

    private Vector3 CalculateBezierPosition(FlightPhase phase, float t)
    {
        if (phase == FlightPhase.Diving)
        {
            // 俯冲贝塞尔曲线：起点 -> 俯冲控制点 -> 终点(B点)
            // 使用二次贝塞尔曲线（3个点）
            return CalculateQuadraticBezierPoint(startPoint, diveControlPoint, targetA, t);
        }
        else
        {
            // 拉升贝塞尔曲线：A点 -> 拉升控制点 -> B点
            // 使用二次贝塞尔曲线（3个点）
            return CalculateQuadraticBezierPoint(targetA, climbControlPoint, targetB, t);
        }
    }

    // 二次贝塞尔曲线计算（3个点）
    private Vector3 CalculateQuadraticBezierPoint(Vector3 p0, Vector3 p1, Vector3 p2, float t)
    {
        float u = 1 - t;
        float uu = u * u;
        float tt = t * t;

        Vector3 point = uu * p0;
        point += 2 * u * t * p1;
        point += tt * p2;

        return point;
    }

    //计算持续时间
    private float CalculateDuration(float speed, Vector3 start, Vector3 end)
    {
        float distance = Vector3.Distance(start, end);
        return distance / speed;
    }

    private void OnFlightComplete()
    {


    }

    private void OnDrawGizmos()
    {
        if (!Application.isPlaying)
        {
            InitializePoints();
        }
        // 绘制俯冲曲线（绿色线条）
        Gizmos.color = Color.green;
        Vector3 prevDivePoint = CalculateBezierPosition(FlightPhase.Diving, 0f);
        for (float t = 0.05f; t <= 1; t += 0.05f)
        {
            Vector3 currentDivePoint = CalculateBezierPosition(FlightPhase.Diving, t);
            Gizmos.DrawLine(prevDivePoint, currentDivePoint);
            prevDivePoint = currentDivePoint;
        }

        // 绘制拉升曲线（黄色线条）
        Gizmos.color = Color.yellow;
        Vector3 prevClimbPoint = CalculateBezierPosition(FlightPhase.Climbing, 0f);
        for (float t = 0.05f; t <= 1; t += 0.05f)
        {
            Vector3 currentClimbPoint = CalculateBezierPosition(FlightPhase.Climbing, t);
            Gizmos.DrawLine(prevClimbPoint, currentClimbPoint);
            prevClimbPoint = currentClimbPoint;
        }

        // 绘制关键点
        Gizmos.color = Color.green;
        Gizmos.DrawSphere(startPoint, 5f);

        Gizmos.color = Color.yellow;
        Gizmos.DrawSphere(targetB, 5f);

        Gizmos.color = Color.blue;
        Gizmos.DrawSphere(targetA, 5f);

        // 绘制控制点
        Gizmos.color = Color.magenta;
        Gizmos.DrawSphere(diveControlPoint, 2.8f);
        Gizmos.DrawSphere(climbControlPoint, 2.8f);
    }


}
