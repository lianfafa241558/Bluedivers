using FPSGame.Data;
using FPSGame.Game;
using FPSGame.GameContract;
using UnityEngine;

namespace FPSGame.Gameplay
{
    /// <summary>
    /// 凯伊的护盾（全队强化「样本拯救者」）：独立罩子挂在 Kei 下，替罩内队友挡子弹。
    ///
    /// <para>▍为什么不把盾值加到 Kei 的 Health 上：Kei 是 <c>ActorFlag.Invincible</c> 且没有 Damageable，
    /// 而 <c>Health.Awake</c> 用 <c>GetComponentInParent&lt;Actor&gt;()</c> —— 盾若挂在 Kei 下并**没有自己的 Actor**，
    /// 会向上拿到 Kei 的无敌标记，<c>Health.TakeDamage</c> 首行就 return，盾永远掉不下来。
    /// 所以罩子自带一个 <c>type=None</c> 的 Actor（只用来吃伤害 / 取 NetId，不进查询网格、不发创建死亡事件）。</para>
    ///
    /// <para>▍数值（口径由策划定）：基础 100 盾，每提交一份团队样本 +5 盾、罩子 scale +0.1；破盾后 10 秒重塑。</para>
    /// <para>⚠ 样本量以「已提交给凯伊」的累计为准（<see cref="TaskState.CollectProperty"/> 总和），不是背包里拿着的。</para>
    /// </summary>
    [AddComponentMenu("单位/凯伊护盾")]
    public class KeiShield : MonoBehaviour
    {
        /// <summary>基础盾值</summary>
        private const int BaseShield = 100;
        /// <summary>每份已提交样本增加的盾值</summary>
        private const int ShieldPerSample = 10;
        /// <summary>每份已提交样本增加的罩子缩放（transform.localScale）</summary>
        private const float ScalePerSample = 0.5f;

        [InspectorName("护盾生命")]
        [SerializeField]
        private HealthShield _health;
        [InspectorName("护盾重塑")]
        [SerializeField]
        private ShieldRebuild _rebuild;
        [InspectorName("罩子物体")]
        [SerializeField]
        private GameObject _shieldVisual;
        [InspectorName("基础缩放（未提交样本时的罩子大小）")]
        [SerializeField]
        private float _baseScale = 6f;

        /// <summary>本局已提交的团队样本总数（开局读一次，之后按 <c>GlobalEventBus.OnKeiSubmit</c> 累加）</summary>
        private int _samples;

        private void Start()
        {
            if (!BattleState.HaveBooster(BoosterType.SampleSaver))
            {
                // 没点强化 ⇒ 整块关掉：别留一个 100 盾的罩子挡队友子弹
                if (_shieldVisual != null) _shieldVisual.SetActive(false);
                enabled = false;
                return;
            }

            // ⚠ 号：盾的 Actor 故意用 type=None（不进查询网格、不发创建/死亡事件），代价是
            //   `Actor.Awake` 里只给 Enemy / SpecUnit 发自增号 ⇒ 开战后创建时 NetId 会是 0（同步静默失效）。
            //   这里按"Id + 落点"补一个派生号：Kei 的落点由任务实体决定，两端算出同一个值。
            var actor = GetComponent<Actor>();
            if (actor != null && actor.NetId == 0)
                actor.NetId = ActorsManager.DerivedNetId(actor.Id, actor.transform.position);

            _samples = CountSubmittedSamples();
            Apply();
            GlobalEventBus.OnKeiSubmit += OnSubmitted;
        }

        private void OnDestroy()
        {
            GlobalEventBus.OnKeiSubmit -= OnSubmitted;
        }

        private void OnSubmitted(OOPartEnum _, int count)
        {
            if (count <= 0) return;
            _samples += count;
            Apply();
        }

        /// <summary>按当前样本量刷新盾值与罩子大小。⚠ <c>ShieldRebuild</c> 重塑时只把 Current 拉回 Max，
        /// 所以 Max 必须自己维护；这里同时补上增量，避免"盾值涨了但当前值是空的"。</summary>
        private void Apply()
        {
            int max = BaseShield + ShieldPerSample * _samples;
            int add = max - _health.MaxShield;
            _health.MaxShield = max;
            if (add > 0) _health.RestoreShield(add);

            if (_shieldVisual != null)
            {
                float scale = _baseScale + ScalePerSample * _samples;
                _shieldVisual.transform.localScale = new Vector3(scale, scale, scale);
            }
        }

        /// <summary>本局已提交给凯伊的样本总数（跨玩家共享；写入点见 <c>BattleManager.SubmitOOPart</c>）</summary>
        private static int CountSubmittedSamples()
        {
            var dic = TaskState.CollectProperty;
            if (dic == null) return 0;

            int total = 0;
            foreach (var kv in dic) total += kv.Value;
            return total;
        }
    }
}
