using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using FPSGame.Utils;
using FPSGame.Managers;
using FPSGame.Gameplay;
using FPSGame.Game;
using FPSGame.GameData;

namespace FPSGame.Effect
{

/// <summary>
/// 自由相机的移动与旋转控制。
/// </summary>
[AddComponentMenu("特效/自由相机控制")]
public class FreeCameraController : MonoBehaviour
{
    private const float _Min = 50,_Max=25;

    new private Camera camera;

    private float speed = 4;

    [SerializeField] private LayerMask _groundLayer;
    [SerializeField] private float _orbitSpeed = 60f;

    private Vector3 targetPoint;
    private float _scrollMomentum;

    /// <summary>关掉后 <see cref="Update"/> 不再读输入（导演的固定机位/选点态用）。
    /// <para>▍为什么不用 <c>enabled = false</c>：那样 <see cref="LateUpdate"/> 的到位插值也停，
    /// 机位过渡改位置后会被残留的 <c>targetPoint</c> 慢慢拉回去。</para></summary>
    public bool InputEnabled { get; set; } = true;

    /// <summary>是否响应鼠标转视角。导演的"选点态"要关掉它：鼠标已解锁，转视角会让光标乱飘；
    /// 但 WASD 平移仍要能用（边选点边挪机位）。</summary>
    public bool AllowLook { get; set; } = true;

    /// <summary>地面拾取用的层（纯 Ground；⚠ 不要用含 Default 的 <c>LayerDefinition.GroundLayers</c>）。</summary>
    public LayerMask GroundMask => _groundLayer;

    /// <summary>本相机组件（导演取景/拾取共用）。</summary>
    public Camera Cam => camera;

    public float Fov => camera ? camera.fieldOfView : _Min;

    /// <summary>当前移动速度（1..35，`=`/`-` 调）。导演用它换算机位过渡时长：飞得快 ⇒ 跳得也快。</summary>
    public float Speed => speed;

    private void OnEnable()
    {
        if(!camera) camera = GetComponent<Camera>();
        camera.fieldOfView = _Min;
        targetPoint = transform.position;
    }

    /// <summary>直接摆到指定位姿。必须同步重置 <c>targetPoint</c>，否则 LateUpdate 会从旧点插值过来。</summary>
    public void SnapTo(Vector3 position, Quaternion rotation, float fov)
    {
        transform.SetPositionAndRotation(position, rotation);
        targetPoint = position;
        _scrollMomentum = 0f;
        if (camera) camera.fieldOfView = fov;
    }

    public void SetFov(float fov)
    {
        if (camera) camera.fieldOfView = fov;
    }

    
    void Update()
    {
        if (!InputEnabled) return;

        //设置速度
        if (InputManager.GetDown(InputState.Acceler)) speed=Mathf.Min(speed+1f,35);
        if (InputManager.GetDown(InputState.Deceler)) speed = Mathf.Max(speed - 1f, 1);

        //缩放镜头
        if (InputManager.Get(InputState.Aim))
        {
            camera.fieldOfView = Mathf.Lerp(camera.fieldOfView, _Max, Time.unscaledDeltaTime*4f);
        }
        else
        {
            camera.fieldOfView = Mathf.Lerp(camera.fieldOfView, _Min, Time.unscaledDeltaTime*4f);
        }
        //旋转视角（选点态关掉：鼠标解锁时转视角会跟着光标乱飘）
        if (AllowLook)
        {
            if (Input.GetKey(KeyCode.C))
            {
                Ray ray = camera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0));
                if (Physics.Raycast(ray, out RaycastHit hit, 1000f, _groundLayer))
                {
                    // 按住C自动围绕地面落点顺时针旋转
                    float rotY = _orbitSpeed * Time.unscaledDeltaTime;
                    Vector3 posBeforeOrbit = transform.position;
                    transform.RotateAround(hit.point, Vector3.up, rotY);
                    targetPoint += transform.position - posBeforeOrbit;
                    // X轴保持正常旋转
                    transform.Rotate(GetMouseAxis(true) * 150, 0f, 0f, Space.Self);
                }
                else
                {
                    // 未命中地面时回退到正常旋转
                    transform.Rotate(new Vector3((GetMouseAxis(true) * 150), (GetMouseAxis(false) * 150), 0f), Space.Self);
                }
            }
            else
            {
                transform.Rotate(new Vector3((GetMouseAxis(true) * 150), (GetMouseAxis(false) * 150), 0f), Space.Self);
            }
            Vector3 currentRotation = transform.eulerAngles;
            currentRotation.z = 0f;
            transform.eulerAngles = currentRotation;
        }

        Vector3 dir = Vector3.zero;
        if (InputManager.Get(InputState.Up)) dir+= transform.TransformDirection(Vector3.forward).Mult(new(1,0,1));
        if (InputManager.Get(InputState.Down)) dir += transform.TransformDirection(Vector3.back).Mult(new(1, 0, 1));
        if (InputManager.Get(InputState.Left)) dir += transform.TransformDirection(Vector3.left).Mult(new(1, 0, 1));
        if (InputManager.Get(InputState.Right)) dir += transform.TransformDirection(Vector3.right).Mult(new(1, 0, 1));
        if (InputManager.Get(InputState.Rise)) dir += Vector3.up;
        if (InputManager.Get(InputState.Fall)) dir += Vector3.down;
        // 滚轮动量：累积后平滑衰减，避免一顿一顿
        float scroll = Input.GetAxis("Mouse ScrollWheel");
        _scrollMomentum += scroll;
        if (Mathf.Abs(_scrollMomentum) > 0.001f)
            dir += transform.TransformDirection(Vector3.forward) * _scrollMomentum * 100f;
        dir.Normalize();
        targetPoint = targetPoint + dir* speed* Time.unscaledDeltaTime;
        _scrollMomentum = Mathf.Lerp(_scrollMomentum, 0, Time.unscaledDeltaTime * 3f);
    }

    private void LateUpdate()
    {
        transform.position = Vector3.Lerp(transform.position, targetPoint, Time.unscaledDeltaTime * 20);
        
    }


    private static readonly string[] SensitivityKeys = { "水平灵敏度", "垂直灵敏度" };
    private static readonly string[] InvertKeys = { "反转X轴", "反转Y轴" };
    private static readonly string[] AxisKeys = { "Mouse X", "Mouse Y" };
    /// <summary>
    /// 获得鼠标轴
    /// </summary>
    /// <returns></returns>
    float GetMouseAxis(bool isY)
    {
        int keyPrefix = isY ? 1 : 0;

        float speed = ArchivesData_SO.Current.GetSetting(SensitivityKeys[keyPrefix]);
        float sigh = ArchivesData_SO.Current.GetSetting(InvertKeys[keyPrefix]) > 0 ? -1 : 1;
        float inputValue = Input.GetAxisRaw(AxisKeys[keyPrefix]);
        float i = inputValue * sigh * speed * 0.0001f;

#if UNITY_WEBGL
        // 由于鼠标加速，在WebGL中鼠标往往更敏感，因此请进一步减少它
        i *= 0.3f;
#endif
        return i;
    }
}
}
