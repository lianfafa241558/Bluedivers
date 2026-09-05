using Core;
using UnityEngine;

namespace FPSGame.AI
{
    /// <summary>
    /// 敌人事件特效 SO：只持有 fxDic（受击/死亡/发现目标等时机的音效、粒子、挂点、护甲破坏表现）。
    /// 与渲染模板（EnemyFxData_SO.rendererSet）解耦，便于同类单位/变体共享同一份事件特效配置。
    /// </summary>
    [CreateAssetMenu(fileName = "EFXE_", menuName = "Data/敌人特效事件")]
    public class EnemyFxEventData_SO : ScriptableObject
    {
        [Tooltip("各时机（受击/攻击/死亡等）触发的音效/粒子/挂点")]
        public DisplayDic<OccasionTypeEnum, FxSetConfig> fxDic = new(true);
    }
}
