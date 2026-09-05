using System.Collections.Generic;
using Core;
using UnityEngine;

namespace FPSGame.AI
{
    /// <summary>
    /// 敌人特效共享配置：每敌人类型/prefab 变体一个资产，多个实例共享引用。
    /// 运行时为只读数据，不要修改资产内容。
    /// </summary>
    [CreateAssetMenu(fileName = "EFX_", menuName = "Data/敌人特效")]
    public class EnemyFxData_SO : ScriptableObject
    {
        [Header("MPB 颜色闪变")]
        [Tooltip("与旧 prefab 内联 rendererSet 对应；每项按目标材质匹配渲染器槽位，运行时逐实例构建私有状态")]
        public List<RendererSetConfig> rendererSet = new();

        [Header("事件特效")]
        [Tooltip("各时机（受击/攻击/死亡等）触发的音效/粒子/挂点")]
        public DisplayDic<OccasionTypeEnum, FxSetConfig> fxDic = new(true);
    }
}
