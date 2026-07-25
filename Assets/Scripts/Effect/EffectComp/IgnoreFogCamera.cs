using UnityEngine;

public class IgnoreFogCamera : MonoBehaviour
{
    private bool _originalFogState;
    private bool _isEnabled = false;

    void OnEnable()
    {
        // 记录当前雾效状态
        _originalFogState = RenderSettings.fog;
        // 关闭雾效
        RenderSettings.fog = false;
        _isEnabled = true;

        // 可选：在URP下额外强制设置Shader全局变量，确保生效
        //Shader.SetGlobalFloat("_FogEnabled", 0);
    }

    void OnDisable()
    {
        // 只有当初启用时记录过状态，才恢复
        if (_isEnabled)
        {
            // 恢复雾效到原始状态
            RenderSettings.fog = _originalFogState;

            // 恢复Shader全局变量
            //Shader.SetGlobalFloat("_FogEnabled", _originalFogState ? 1 : 0);

            _isEnabled = false;
        }
    }
}