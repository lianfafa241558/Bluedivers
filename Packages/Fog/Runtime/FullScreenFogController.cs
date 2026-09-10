using UnityEngine;

namespace Meryuhi.Rendering
{
    /// <summary>
    /// 全屏雾全局控制器：运行时开关的统一入口（参照项目内 SnowController 模式）。
    /// SetEnabled(false) 时 FullScreenFogRendererFeature 直接跳过整个雾 Pass（无任何绘制开销），
    /// 仅保留 RenderSettings 内置雾；EnvironmentLightingModule 的逐帧参数写入不受影响（关闭期间不生效，重开后自动恢复）
    /// </summary>
    public static class FullScreenFogController
    {
        /// <summary>当前是否启用全屏雾（默认关闭，由需要雾的系统（如天气）调 SetEnabled(true) 开启）</summary>
        public static bool Enabled { get; private set; } = false;

        /// <summary>开关全屏雾（false 时跳过整个雾 Pass）</summary>
        public static void SetEnabled(bool enabled)
        {
            Enabled = enabled;
        }
    }
}
