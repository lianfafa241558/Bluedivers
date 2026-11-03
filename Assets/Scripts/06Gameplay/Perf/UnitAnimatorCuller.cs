using FPSGame.Attributes;
using FPSGame.Core;
using FPSGame.Game;
using FPSGame.GameContract;
using UnityEngine;

namespace FPSGame.Gameplay
{
    /// <summary>
    /// 按**距相机距离**把远处单位的 Animator 整个关掉（`Animator.enabled = false`），专治
    /// <c>DirectorUpdateAnimationBegin</c> 那几毫秒 —— 交战时 860+ 个 Animator，其中大半在 100 m 外。
    ///
    /// <para>▍与 <c>Actor.InitAnimatorCulling</c> 的分工：那里把 `cullingMode` 从 AlwaysAnimate 改成
    /// `CullUpdateTransforms`（离屏只跳过 transform 写入，**状态机与动画事件照跑**，不影响逻辑）；
    /// 本组件是更狠的一档 —— 直接停掉 Animator，连状态机一起冻住。所以只对远处单位用，且有滞回。</para>
    ///
    /// <para>▍⚠ 关掉 Animator 后**动画事件不再触发**。如果某个单位的玩法（开火/落脚）依赖动画事件，
    /// 把 <see cref="FarDistance"/> 调到"该单位绝不会在外的距离"之外即可，不要为了省 CPU 调到 60 m。</para>
    ///
    /// <para>▍为什么遍历 <see cref="ActorsManager.Actors"/> 而不是 <c>UnitQueryGrid</c>：
    /// 网格擅长"给我某个范围内的单位"，而这里要的恰恰是**范围外的那一批** —— 用网格得先查近处、
    /// 再对全集做差，反而多一次遍历。这里 0.2 s 才全扫一次，1300 个单位的距离比较 ≈ 每帧 0.01 ms。</para>
    /// </summary>
    [AddComponentMenu("性能/单位动画距离剔除")]
    public class UnitAnimatorCuller : MonoBehaviour
    {
        [SerializeField]
        [InspectorName("启用")]
        private bool Enable = true;

        [SerializeField]
        [InspectorName("近距(米)")]
        [Tooltip("进到这个距离内恢复动画。必须比 FarDistance 小，两个距离之间就是滞回区")]
        private float NearDistance = 100f;

        [SerializeField]
        [InspectorName("远距(米)")]
        [Tooltip("超过这个距离关掉 Animator。⚠ 关掉后动画事件不再触发，别调到近处")]
        private float FarDistance = 120f;

        [SerializeField]
        [InspectorName("检查间隔(秒)")]
        [Tooltip("多久扫一次全体单位。0.2s 足够，肉眼看不出来")]
        private float Interval = 0.2f;

        private float _nextCheck;
        private Camera _cam;

        private void OnValidate()
        {
            if (FarDistance < NearDistance) FarDistance = NearDistance;
        }

        private void Update()
        {
            if (!Enable) return;

            float now = Time.time;
            if (now < _nextCheck) return;
            _nextCheck = now + Mathf.Max(0.05f, Interval);

            Vector3 center = ResolveCenter();
            float farSqr = FarDistance * FarDistance;
            float nearSqr = NearDistance * NearDistance;

            var actors = ActorsManager.Actors;
            for (int i = 0; i < actors.Count; ++i)
            {
                IActor unit = actors[i];
                if (unit == null) continue;

                Actor actor = unit as Actor;
                if (actor == null) continue;
                // 倒地/等待救援可能还要播动画，别冻（也避免"救起来才发现动画停了"）
                if (actor.ActorState == ActorState.Dead) continue;

                float sqr = (unit.Pos - center).sqrMagnitude;
                if (!actor.AnimatorsOff)
                {
                    // ⚠ 一次性表现（入场"从天而降"）播到一半不能冻：状态机停住 ⇒ 模型钉在天上，
                    //   而逻辑坐标早在地上 ⇒ 玩家在远处看见它悬空，走近才落下来
                    if (sqr > farSqr && !actor.AnimatorOneShotPlaying) actor.SetAnimatorsEnabled(false);
                }
                else if (sqr < nearSqr)
                {
                    actor.SetAnimatorsEnabled(true);
                }
            }
        }

        /// <summary>判定中心：主相机优先（没有相机时退到玩家位置，再退到自身）。</summary>
        private Vector3 ResolveCenter()
        {
            if (_cam == null) _cam = Camera.main;
            if (_cam != null) return _cam.transform.position;

            IActor player = ActorsManager.Player;
            return player != null ? player.Pos : transform.position;
        }

        private void OnDisable()
        {
            // 组件被关掉时还原全场，否则场上会静止在"远处全冻住"的状态
            var actors = ActorsManager.Actors;
            if (actors == null) return;
            for (int i = 0; i < actors.Count; ++i)
            {
                Actor actor = actors[i] as Actor;
                if (actor != null && actor.AnimatorsOff) actor.SetAnimatorsEnabled(true);
            }
        }
    }
}
