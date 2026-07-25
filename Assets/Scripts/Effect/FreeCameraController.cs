using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Utils;

public class FreeCameraController : MonoBehaviour
{
    private const float _Min = 50,_Max=25;

    new private Camera camera;

    private float speed = 4;

    [SerializeField] private LayerMask _groundLayer;
    [SerializeField] private float _orbitSpeed = 60f;

    private Vector3 targetPoint;
    private float _scrollMomentum;

    private void OnEnable()
    {
        if(!camera) camera = GetComponent<Camera>();
        camera.fieldOfView = _Min;
        targetPoint = transform.position;
    }

    
    void Update()
    {
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
        //旋转视角
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

        float speed = ArchiveSvc.GetSetting(SensitivityKeys[keyPrefix]);
        float sigh = ArchiveSvc.GetSetting(InvertKeys[keyPrefix]) > 0 ? -1 : 1;
        float inputValue = Input.GetAxisRaw(AxisKeys[keyPrefix]);
        float i = inputValue * sigh * speed * 0.0001f;

#if UNITY_WEBGL
        // 由于鼠标加速，在WebGL中鼠标往往更敏感，因此请进一步减少它
        i *= 0.3f;
#endif
        return i;
    }
}
