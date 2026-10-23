using System.Collections.Generic;
using FPSGame.Attributes;
using FPSGame.Core;
using FPSGame.Core.Interface;
using FPSGame.Game;
using FPSGame.GameContract;
using UnityEngine;

namespace FPSGame.AI
{
    /// <summary>
    /// 信号干扰（区域光环）：半径内叫不出战备（UI 显示"受到干扰"），
    /// 落点也在半径内的信标直接作废（<c>VFXAirdropEffect</c> 抛信标前判一次）。
    /// </summary>
    /// <remarks>
    /// ▍不继承 <see cref="UnitSkill_Base"/>：那套绑死 <c>EnemyController</c> 的目标/接战/冷却判定，
    /// 而干扰是"宿主活着就一直生效"的静态光环 —— 没有冷却也没有目标，独立组件最省。
    /// ▍宿主死了就失效：查询时看一眼 <see cref="ActorState.Dead"/>，不订阅死亡事件（省一次退订）。
    /// </remarks>
    [AddComponentMenu("单位/_技能/信号干扰", 30)]
    [DisallowMultipleComponent]
    public class SignalJammer : MonoBehaviour
    {
        [InspectorName("干扰半径")]
        [SerializeField]
        private float m_Radius = 20f;

        /// <summary>当前生效的干扰源（<see cref="OnEnable"/> 注册 / <see cref="OnDisable"/> 注销 ⇒ 池化复用、销毁都走这两条）。</summary>
        private static readonly List<SignalJammer> _active = new();

        private Transform _tf;

        /// <summary>宿主单位（可空：纯道具/场景物直接挂就是永久生效）。</summary>
        private Actor _actor;

        public float Radius => m_Radius;

        private void Awake()
        {
            _tf = transform;
            _actor = GetComponent<Actor>();
        }

        private void OnEnable() => _active.Add(this);

        private void OnDisable() => _active.Remove(this);

        /// <summary>该点是否落在任一存活干扰源的半径内。</summary>
        public static bool IsBlocked(Vector3 point)
        {
            for (int i = _active.Count - 1; i >= 0; --i)
            {
                var jammer = _active[i];
                // 未走 OnDisable 就被销毁的残留（编辑器里删物体 / 父级连带销毁）⇒ 顺手清掉
                if (jammer == null)
                {
                    _active.RemoveAt(i);
                    continue;
                }
                // 开关/激活状态以 isActiveAndEnabled 为准：这里显式判一次，组件 enable=false（或物体未激活）时立刻失效，
                // 不依赖"OnDisable 有没有及时跑到"（注册表只是加速索引用）
                if (!jammer.isActiveAndEnabled) continue;
                if (jammer.IsOwnerDead) continue;

                float r = jammer.m_Radius;
                if ((jammer._tf.position - point).sqrMagnitude < r * r) return true;
            }
            return false;
        }

        /// <summary>该单位所在位置是否被干扰（无效单位视为未被干扰）。</summary>
        public static bool IsBlocked(IActor actor) => actor.IsValidMono() && IsBlocked(actor.Pos);

        private bool IsOwnerDead => _actor != null && _actor.ActorState == ActorState.Dead;

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, m_Radius);
        }
    }
}
