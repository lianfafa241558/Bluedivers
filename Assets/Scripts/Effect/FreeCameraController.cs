using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FreeCameraController : MonoBehaviour
{
    private const float _Min = 50,_Max=25;

    new private Camera camera;

    private float speed = 4;

    private void OnEnable()
    {
        if(!camera) camera = GetComponent<Camera>();
        camera.fieldOfView = _Min;
    }

    
    void Update()
    {
        //设置速度
        if (InputManager.GetDown(InputState.Acceler)) speed=Mathf.Min(speed+0.5f,15);
        if (InputManager.GetDown(InputState.Deceler)) speed = Mathf.Max(speed - 0.5f, 1);

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
        transform.Rotate(new Vector3((GetMouseAxis(true) * 150), (GetMouseAxis(false) * 150), 0f), Space.Self);
        Vector3 currentRotation = transform.eulerAngles;
        currentRotation.z = 0f;
        transform.eulerAngles = currentRotation;

        Vector3 dir = Vector3.zero;
        if (InputManager.Get(InputState.Up)) dir+= transform.TransformDirection(Vector3.forward);
        if (InputManager.Get(InputState.Down)) dir += transform.TransformDirection(Vector3.back);
        if (InputManager.Get(InputState.Left)) dir += transform.TransformDirection(Vector3.left);
        if (InputManager.Get(InputState.Right)) dir += transform.TransformDirection(Vector3.right);
        if (InputManager.Get(InputState.Rise)) dir += transform.TransformDirection(Vector3.up);
        if (InputManager.Get(InputState.Fall)) dir += transform.TransformDirection(Vector3.down);
        dir.Normalize();
        transform.position =Vector3.Lerp(transform.position,transform.position+ dir, Time.unscaledDeltaTime * speed);
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

        float speed = GameRoot.GetSetting(SensitivityKeys[keyPrefix]);
        float sigh = GameRoot.GetSetting(InvertKeys[keyPrefix]) > 0 ? -1 : 1;
        float inputValue = Input.GetAxisRaw(AxisKeys[keyPrefix]);
        float i = inputValue * sigh * speed * 0.0001f;

#if UNITY_WEBGL
        // 由于鼠标加速，在WebGL中鼠标往往更敏感，因此请进一步减少它
        i *= 0.3f;
#endif
        return i;
    }
}
