using FPSGame.GameContract;
using UnityEngine;

namespace FPSGame.Game
{
    /// <summary>
    /// 护盾重塑：护盾被打破后延迟收起护盾，再延时重塑并复活单位。
    /// 只负责"实际生效"的护盾行为（收起 → 延时 → 复活 → 展开）；
    /// UI（填充条/文本/状态）由对接方（如 <c>ShieldBag</c>）订阅 Health 事件自行完成。
    /// <para>时间轴（均从单位被击毁那一刻起算）：
    /// t=0 击毁 → t=<see cref="_hideDelay"/> 收起护盾物体（给破碎/死亡动画留时间）
    /// → t=max(<see cref="_rebuildDelay"/>, <see cref="_hideDelay"/>) 复活单位 + 展开护盾。</para>
    /// </summary>
    [AddComponentMenu("技能/护盾重塑", 30)]
    public class ShieldRebuild : MonoBehaviour
    {
        /// <summary>默认重塑延时（秒）</summary>
        private const float DefaultRebuildDelay = 15f;

        [InspectorName("生命组件所在物体")]
        [Tooltip("留空则依次找自身、子物体上的 IHealth")]
        [SerializeField]
        private GameObject _healthGo;


        [InspectorName("护盾物体")]
        [Tooltip("被打破时收起、重塑时重新展开的物体（通常是有罩子碰撞体的子物体）")]
        [SerializeField]
        private GameObject _shieldVisual;

        [InspectorName("护盾发射器肢体")]
        [SerializeField]
        private Damageable emitter;

        [InspectorName("隐藏延时")]
        [Tooltip("单位被击毁后延迟多久才收起护盾物体（给破碎/死亡动画留时间）；0=立即收起")]
        [SerializeField]
        private float _hideDelay;

        [InspectorName("重塑延时")]
        [Tooltip("单位被击毁后多久重塑护盾（从击毁时刻起算，小于隐藏延时时会被推到隐藏之后）")]
        [SerializeField]
        private float _rebuildDelay = DefaultRebuildDelay;

        /// <summary>护盾发射器已被摧毁，技能失效</summary>
        private bool m_EmitterBroken;

        private IHealth _health;


        private void OnEnable()
        {
            ResolveHealth();
            if (_health != null) _health.OnDie += OnUnitDie;
            if (emitter != null) emitter.OnDestroyPart += OnEmitterDestroyed;
        }

        private void OnDisable()
        {
            if (_health != null) _health.OnDie -= OnUnitDie;
            if (emitter != null) emitter.OnDestroyPart -= OnEmitterDestroyed;
            CancelInvoke(nameof(HideVisual));
            CancelInvoke(nameof(Rebuild));
        }

        /// <summary>解析生命值：优先配置的物体，其次自身，最后子物体（含未激活）</summary>
        private void ResolveHealth()
        {
            _health = _healthGo != null ? _healthGo.GetComponent<IHealth>() : GetComponent<IHealth>();
            if (_health == null) _health = GetComponentInChildren<IHealth>(true);
        }

        /// <summary>
        /// 外部总开关（装备装卸等）：展开或收起护盾。
        /// 收起时会取消待执行的隐藏/重塑计时，避免"已卸下的护盾被延时逻辑重新展开"。
        /// </summary>
        public void SetShieldActive(bool active)
        {
            if (active) Rebuild();
            else HideShield();
        }

        /// <summary>收起护盾并取消所有待执行计时（外部调用用这个）</summary>
        public void HideShield()
        {
            CancelInvoke(nameof(HideVisual));
            CancelInvoke(nameof(Rebuild));
            HideVisual();
        }

        /// <summary>只收起护盾物体，不碰计时（延迟收起走这个，避免把待重塑也取消掉）</summary>
        private void HideVisual()
        {
            if (_shieldVisual != null) _shieldVisual.SetActive(false);
        }

        /// <summary>单位被击毁</summary>
        private void OnUnitDie(GameObject _) => StartRebuild();

        /// <summary>击毁后：延迟收起护盾物体，再延时重塑</summary>
        private void StartRebuild()
        {
            // 护盾发射器已被摧毁：不再重塑
            if (m_EmitterBroken) return;

            CancelInvoke(nameof(HideVisual));
            CancelInvoke(nameof(Rebuild));

            if (_hideDelay > 0f) Invoke(nameof(HideVisual), _hideDelay);
            else HideVisual();

            // 重塑从击毁时刻起算；配置比隐藏还早时往后推到隐藏之后，避免"刚重塑又被隐藏"
            Invoke(nameof(Rebuild), Mathf.Max(_rebuildDelay, _hideDelay));
        }

        /// <summary>护盾发射器被摧毁：护盾失效，不再重塑</summary>
        private void OnEmitterDestroyed(Damageable _)
        {
            m_EmitterBroken = true;
            HideShield();
        }

        /// <summary>重塑护盾：复活单位、展开视觉</summary>
        private void Rebuild()
        {
            CancelInvoke(nameof(HideVisual));
            CancelInvoke(nameof(Rebuild));
            // 只在真正死亡时复活（死了 CurrentHealth 会归 0），避免把活着的单位白回满血
            if (_health != null && _health.GetHpCurrent() <= 0) _health.Revive();
            if (_shieldVisual != null) _shieldVisual.SetActive(true);
        }
    }
}
