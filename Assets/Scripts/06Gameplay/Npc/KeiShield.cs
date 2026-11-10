using FPSGame.Data;
using FPSGame.Game;
using FPSGame.GameContract;
using UnityEngine;

namespace FPSGame.Gameplay
{
    /// <summary>
    /// 凯伊的护盾（全队强化「样本拯救者」）—— **独立单位**，不再挂在 Kei 的层级里。
    ///
    /// <para>▍谁创建它：<see cref="KeiShieldEffect"/>（挂在 Kei 根上）：房主/单机 <c>SpawnAt</c> 造一份并广播 4063，
    /// 成员收到后按可生成单位表复刻 ⇒ 两端是**同一个号、同一份血量**。本组件只负责"存在之后"的事。</para>
    ///
    /// <para>▍为什么必须有自己的 Actor：Kei 是 <c>ActorFlag.Invincible</c> 且没有 Damageable，而 <c>Health</c> 用
    /// <c>GetComponentInParent&lt;Actor&gt;()</c> —— 盾若挂在 Kei 下且**没有自己的 Actor**，会向上拿到无敌标记，
    /// <c>TakeDamage</c> 首行就 return，盾永远掉不下来。所以盾自带 <c>type=None</c> 的 Actor
    /// （不进查询网格、不发创建/死亡事件，只用来吃伤害 / 承载 NetId）。</para>
    ///
    /// <para>▍位置：每帧贴到 Kei 身上（<c>LateUpdate</c>）。**不要**给它做移动同步 —— 两端的 Kei 位置本来就一致，
    /// 跟随是确定性的派生，多发一条移动消息只会抖。</para>
    ///
    /// <para>▍数值（口径由策划定）：基础 100 盾，每提交一份团队样本 +10 盾、罩子 scale +0.5；破盾后 10 秒重塑。
    /// ⚠ 样本量以「已提交给凯伊」的累计为准（<see cref="TaskState.CollectProperty"/> 总和），不是背包里拿着的。</para>
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
        // ⚠ 护盾重塑由同 prefab 上的 ShieldRebuild 组件自己驱动（它自己订阅 Health 事件）⇒ 这里不需要引用它。
        [InspectorName("罩子物体")]
        [SerializeField]
        private GameObject _shieldVisual;
        [InspectorName("基础缩放（未提交样本时的罩子大小）")]
        [SerializeField]
        private float _baseScale = 6f;

        [InspectorName("跟随的单位 Id")]
        [SerializeField]
        private string _followId = "Kei";
        [InspectorName("相对凯伊的偏移")]
        [SerializeField]
        private Vector3 _followOffset = new Vector3(0f, 0.6f, 0f);

        /// <summary>跟随目标（首次 <c>LateUpdate</c> 时按 Id 找一次，之后缓存；被销毁会自动重找）。</summary>
        private Transform _follow;

        /// <summary>本局已提交的团队样本总数（开局读一次，之后按 <c>GlobalEventBus.OnKeiSubmit</c> 累加）</summary>
        private int _samples;

        private void Start()
        {
            // ⚠ 号的来源：房主在 <c>SpawnFromTable</c> 里领（ForceHostSpawnNetId）、成员由 4063 覆盖。
            //   这里**不要**再补派生号 —— 那会覆盖掉房主分配的号，两端就指向不同单位了。
            _samples = CountSubmittedSamples();
            Apply();
            GlobalEventBus.OnKeiSubmit += OnSubmitted;
        }

        private void OnDestroy()
        {
            GlobalEventBus.OnKeiSubmit -= OnSubmitted;
        }

        private void LateUpdate()
        {
            var target = ResolveFollow();
            if (target == null) return;
            transform.SetPositionAndRotation(target.position + _followOffset, target.rotation);
        }

        /// <summary>按 Id 在活动单位表里找 Kei（不用 Find：项目规范禁用，且 Kei 必然在 <c>ActorsManager.Actors</c> 里）。</summary>
        private Transform ResolveFollow()
        {
            if (_follow != null) return _follow;
            if (string.IsNullOrEmpty(_followId)) return null;

            var list = ActorsManager.Actors;
            for (int i = 0; i < list.Count; ++i)
            {
                var actor = list[i] as Actor;
                if (actor == null) continue;
                if (actor.Id != _followId) continue;

                _follow = actor.transform;
                return _follow;
            }
            return null;
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
            if (_health != null)
            {
                int max = BaseShield + ShieldPerSample * _samples;
                int add = max - _health.MaxShield;
                _health.MaxShield = max;
                if (add > 0) _health.RestoreShield(add);
            }

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
