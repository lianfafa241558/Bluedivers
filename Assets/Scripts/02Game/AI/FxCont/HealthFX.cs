using GameContract;
using PEMaths;
using UnityEngine;

namespace FPSGame.AI
{
    /// <summary>
    /// 只有「受击 / 死亡」的表现控制器：**与 I_AIController 脱钩，只依赖 IHealth**。
    /// 用于没有 AI 但有生命值的物体（道具 / 建筑 / 护盾等）。
    /// <para>受击 → <c>IHealth.OnDamaged</c>；死亡 → <c>IHealth.OnDie</c>。
    /// 表现本身走 <see cref="FxControllerBase"/> 的管线：闪变（rendererSet）／音效与粒子（fxEvent）／Animator 触发器。</para>
    /// <para>需要额外时机（如诞生、攻击）时继承本类并订阅自己的事件即可。</para>
    /// </summary>
    [AddComponentMenu("表现/简要表现", 999)]
    public class HealthFX : FxControllerBase
    {
        /// <summary>生命值所在物体（接口不能序列化，所以存物体再取组件；留空则取自身/子物体）</summary>
        [InspectorName("生命值所在物体")]
        [SerializeField]
        private GameObject _healthGo;

        /// <summary>运行时解析出的生命值</summary>
        private IHealth _health;

        protected virtual void Start()
        {
            InitFx();

            ResolveHealth();
            if (_health == null)
            {
                Debug.LogWarning(gameObject + "：HitDeathFX 找不到 IHealth，受击/死亡表现不会触发。", gameObject);
                return;
            }

            _health.OnHit += OnDamaged;
            _health.OnDie += OnDie;
            _health.OnRevive += OnRevive;
        }

        private void OnDestroy()
        {
            if (_health == null) return;
            _health.OnHit -= OnDamaged;
            _health.OnDie -= OnDie;
            _health.OnRevive -= OnRevive;
        }

        protected virtual void Update()
        {
            UpdateFx();
        }

        /// <summary>受击   来源，受击点,法线，是弱点</summary>
        protected virtual void OnDamaged(GameObject _, Vector3 pos, Vector3 normal, bool _2)
        {
            PlayHitFx(pos, normal);
        }

        /// <summary>死亡</summary>
        protected virtual void OnDie(GameObject source)
        {
            PlayDieFx(transform.position);
        }

        /// <summary>复活：解除死亡表现标记，之后仍能继续播放受击/死亡表现</summary>
        protected virtual void OnRevive()
        {
            PlayResolveFx(transform.position);
        }

        /// <summary>解析生命值：优先配置的物体，其次自身，最后子物体（含未激活）</summary>
        private void ResolveHealth()
        {
            _health = _healthGo != null ? _healthGo.GetComponent<IHealth>() : GetComponent<IHealth>();
            if (_health == null) _health = GetComponentInChildren<IHealth>(true);
        }
    }
}
