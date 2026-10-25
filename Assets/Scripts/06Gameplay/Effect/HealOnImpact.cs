using FPSGame.Core;
using FPSGame.Core.Interface;
using FPSGame.GameContract;
using UnityEngine;

namespace FPSGame.Gameplay
{
    /// <summary>
    /// 直击治疗（游戏机手雷）：挂在**碰撞特效**预制体上，靠 <c>PlayImpactFx</c> 回传的命中碰撞体找到被打中的单位。
    /// 只认玩家与盟友（砸到敌人身上不生效）。
    /// <para>▍为什么放特效里：命中特效本来就由 <c>IVfxEffect.SetOrigin</c> 带着"来源 + 命中碰撞体"被创建，
    /// 挂在这上面就不用改子弹/伤害管线，也不用另发网络消息。</para>
    /// <para>▍同步：玩家与盟友的血量是**本机权威**（见 <c>Damageable.IsPlayerLike</c>）⇒ 哪端生成这颗特效就治哪端的单位，
    /// 两端各治各自的，不会翻倍。</para>
    /// </summary>
    [AddComponentMenu("特效/直击治疗")]
    public class HealOnImpact : MonoBehaviour, IVfxEffect
    {
        [InspectorName("治疗量")]
        [SerializeField]
        private float HealAmount = 40f;

        [InspectorName("只治疗玩家与盟友")]
        [SerializeField]
        private bool OnlyFriendly = true;

        public void SetOrigin(GameObject owner, GameObject weaponRoot, Collider target, Vector3 point)
        {
            if (!target) return;

            IActor actor = target.GetComponentInParent<IActor>();
            if (!actor.IsValidMono()) return;
            if (OnlyFriendly && actor.Type != UnitTypeEnum.Player && actor.Type != UnitTypeEnum.Friend) return;

            IHealth health = target.GetComponentInParent<IHealth>();
            if (!health.IsValidMono()) return;
            health.Heal(HealAmount);
        }
    }
}
