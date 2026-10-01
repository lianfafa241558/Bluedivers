using System.Collections.Generic;
using UnityEngine;

namespace FPSGame.AI
{
    /// <summary>
    /// 敌人渲染特效模板 SO：只持有 rendererSet（颜色闪白/溶解等 MPB 条目），条目材质通常留空，
    /// 运行时以单位组件 EnemyControllerFX.fxMaterial 作为生效材质（config.material 非空可覆盖）。
    /// 事件特效（音效/粒子）见 EnemyFxEventData_SO。
    /// </summary>
    [CreateAssetMenu(fileName = "EFX_", menuName = "Data/敌人特效")]
    public class EnemyFxData_SO : ScriptableObject
    {
        [Header("MPB 颜色闪变")]
        [Tooltip("与旧 prefab 内联 rendererSet 对应；条目材质留空时使用组件 fxMaterial，运行时逐实例构建私有状态")]
        public List<RendererSetConfig> rendererSet = new();
    }
}
