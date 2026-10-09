using System.Collections.Generic;
using FPSGame.AI;
using FPSGame.Audio;
using FPSGame.Core;
using FPSGame.Game;
using FPSGame.GameContract;
using FPSGame.Gameplay;
using FPSGame.Utils;
using UnityEngine;
using UnityEngine.AI;
using Random = System.Random;

namespace FPSGame.Managers
{
    /// <summary>
    /// 波次基类：承载所有波次共用的生命周期(Start→Ongoing→NearEnd→End)、单位账本、
    /// 随机源、中心点跟踪、提示语音与结束回调。
    /// 子类只需实现开场倒计时(<see cref="TickStart"/>)与本波次特有的刷怪方式(<see cref="TickOngoing"/>)。
    /// </summary>
    public abstract class WaveBase : I_TickClass, System.IDisposable
    {
        /// <summary>阶段判定节拍(Tick)：每隔该数量 Tick 检查一次单位数量</summary>
        protected const int PhaseCheckInterval = 5;

        /// <summary>Start 阶段默认倒计时(Tick)：到点即进入 Ongoing</summary>
        protected const int StartDelayTick = -4;

        protected List<GameObject> waveUseObject;
        protected Stack<GameObject> creats;
        protected List<Actor> units;

        protected WaveState state;
        protected int time;
        protected Vector3 center;
        protected Vector3[] points;
        protected float range;
        protected bool completeCreat;
        protected bool tip;
        protected bool IsDisposed;
        protected System.Action onEnd;
        protected System.Func<Vector3> centerGetter;

        protected Random random;

        protected string startTipName;
        protected string infoTitle;

        /// <summary>
        /// 【波次确定性】这是本局第几波（由 <see cref="WaveManager"/> 在 new 波次**紧邻之前**设好）。
        /// 用它当随机流的细分键 ⇒ 两端"同一波"抽到同一份单位构成与同一批落点（2026-10-07）。
        /// ⚠ 静态字段，但只在构造那一瞬被赋值（单线程、无并发构造）⇒ 不会被别的波次抢走。
        /// </summary>
        internal static int PendingWaveIndex;

        protected WaveBase(WaveCreateParams param, Stack<GameObject> creats, List<GameObject> waveUseObject)
        {
            // ⚠ 原来是 `new Random(RandomUtils.Range(0,1000))`：种子取自**全局静态流**，而那条流会被音效/弹孔/
            //   武器散布推进 ⇒ 两端必然抽到不同的波（怪物/巡逻队"完全没同步"的直接原因）。
            //   有权威种子时改走派生流，purpose 用 "Wave*1000 + 波序"（与 SeedStream.Puzzle 的细分约定一致）。
            int seed = FPSGame.Data.TaskState.Seed;
            random = seed != 0
                ? new Random(SeedUtil.Derive(seed, (int)SeedStream.Wave * 1000 + PendingWaveIndex))
                : new Random(RandomUtils.Range(0, 1000));
            this.waveUseObject = new(waveUseObject);
            this.creats = creats;
            tip = param.tip;
            onEnd = param.onEnd;
            centerGetter = param.centerGetter;
            range = param.range;
            points = param.points;
            center = param.center;
            units = new();

            UnitEventBus.OnEnemyDead += OnUnitDeath;

            //注意：子类字段初始化完成后需自行调用 Trans(WaveState.Start)
        }

        public void Dispose()
        {
            if (IsDisposed) return;
            IsDisposed = true;

            UnitEventBus.OnEnemyDead -= OnUnitDeath;

            //先让子类回收自身资源(此时基类的集合仍然可用)
            OnDispose();

            units?.Clear();
            waveUseObject?.Clear();
            creats?.Clear();

            waveUseObject = null;
            creats = null;
            units = null;
            points = null;
            random = null;
            centerGetter = null;

            //波次结束(所有单位清空)回调，用于续航/续刷
            var callback = onEnd;
            onEnd = null;
            callback?.Invoke();
        }

        /// <summary>子类释放自身额外资源(基类的订阅、集合与回调已由 <see cref="Dispose"/> 统一处理)</summary>
        protected virtual void OnDispose() { }

        public bool Tick()
        {
            --time;

            //中心点持续跟踪(追击)：有 centerGetter 时每 Tick 刷新，新空投的单位会走向最新位置
            if (centerGetter != null) OnCenterUpdated(centerGetter());

            switch (state)
            {
                case WaveState.Start:
                    TickStart();
                    break;

                case WaveState.Ongoing:
                    TickOngoing();
                    break;

                case WaveState.NearEnd:
                    TickNearEnd();
                    break;

                case WaveState.End:
                    Dispose();
                    return false;
            }
            return true;
        }

        /// <summary>中心点刷新：默认仅更新 center，子类可覆写做"偏离基准过远即重新部署空投点"等额外处理</summary>
        protected virtual void OnCenterUpdated(Vector3 newCenter)
        {
            center = newCenter;
        }

        /// <summary>Start 阶段：默认固定倒计时后进入 Ongoing，子类可覆写做开场空投</summary>
        protected virtual void TickStart()
        {
            if (time == StartDelayTick) Trans(WaveState.Ongoing);
        }

        /// <summary>Ongoing 阶段：子类实现各自刷怪逻辑，收尾判定调用 <see cref="TryEnterNearEnd"/></summary>
        protected abstract void TickOngoing();

        /// <summary>NearEnd 阶段：单位全部清空即结束</summary>
        protected virtual void TickNearEnd()
        {
            TryEnterNearEnd(0);
        }

        /// <summary>收尾判定：每隔 <see cref="PhaseCheckInterval"/> Tick 检查一次，单位数不超过阈值则切到 NearEnd</summary>
        protected void TryEnterNearEnd(int threshold = 3)
        {
            if (time % PhaseCheckInterval != 0) return;
            if (units.Count <= threshold) Trans(WaveState.NearEnd);
        }

        /// <summary>状态切换：阶段计时、提示语音与背景音乐统一在此处理</summary>
        protected virtual void Trans(WaveState state)
        {
            this.state = state;
            switch (state)
            {
                case WaveState.Start:
                    if (tip)
                    {
                        WndManager.Instance.CreatNotice("Yuuka", startTipName);
                        AudioSvc.PlayMusic(AudioSvc.MusicGroup.Wave, 0.5f);
                        WindowRegistry.Information.Add(infoTitle);
                        
                    }
                    break;

                case WaveState.Ongoing:
                    time = 0;
                    break;

                case WaveState.NearEnd:
                    if (tip)
                    {
                        WndManager.Instance.CreatNotice("Yuuka", "WaveEnd_Zerg");
                        WindowRegistry.Information.Remove(infoTitle);
                        //TODO:玩家看不见的就消失
                    }
                    break;

                case WaveState.End:
                    if (tip) AudioSvc.PlayMusic(AudioSvc.MusicGroup.Game, 0.5f);
                    break;
            }
        }

        /// <summary>单位死亡：从账本移除(订阅自 UnitEventSub.OnEnemyDead)</summary>
        protected virtual void OnUnitDeath(Actor actor)
        {
            units.Remove(actor);
        }

        /// <summary>把新建单位登记进账本并返回其 Actor(空引用则不入账)</summary>
        protected Actor RegisterUnit(GameObject go)
        {
            var actor = go.GetComponent<Actor>();
            if (actor) units.Add(actor);
            return actor;
        }

        /// <summary>设置单位的长期落点并让它走向该点(到达后不移除，途中被中断会继续走向该点)</summary>
        protected void SetUnitHome(GameObject go, float radius = 5f)
        {
            if (!go) return;
            if (!go.TryGetComponent(out EnemyController ec)) return;

            ec.HomePoint = center + RandomOffset(radius);
            ec.SetNavDestination(ec.HomePoint);
        }

        /// <summary>批量启用/禁用单位身上的行为组件(IHealth 除外，血条/承伤组件始终可用)</summary>
        protected static void SetBehaviourEnabled(GameObject go, bool value)
        {
            foreach (var item in go.GetComponents<Behaviour>())
            {
                if (item is not IHealth) item.enabled = value;
            }
        }

        /// <summary>启用/禁用单位的 Animator(空投前"冻结待机"、投放时的落地动画)</summary>
        protected static void SetUnitAnimator(GameObject go, bool value)
        {
            if (go.TryGetComponent(out EnemyControllerFX fx) && fx.Animator) fx.Animator.enabled = value;
        }

        /// <summary>确保单位所有 Collider 启用(Animator 关键帧可能在启用后把碰撞体关掉)</summary>
        protected static void EnableUnitColliders(GameObject go)
        {
            foreach (var col in go.GetComponentsInChildren<Collider>()) col.enabled = true;
        }

        /// <summary>随机水平朝向(空投特效/单位朝向用)</summary>
        protected Quaternion RandomYaw()
        {
            return Quaternion.Euler(0f, random.Range(0f, 360f), 0f);
        }

        /// <summary>以 center 为原点取一个圆内随机偏移量</summary>
        protected Vector3 RandomOffset(float radius)
        {
            return random.InsideUnitCircle().ToVector3() * radius;
        }

        /// <summary>把点投影到 NavMesh，失败时退化为保持原水平位置、取中心点高度</summary>
        protected Vector3 SnapToNavMesh(Vector3 point)
        {
            if (NavMesh.SamplePosition(point, out var hit, 100, NavMesh.AllAreas)) return hit.position;
            return new Vector3(point.x, center.y, point.z);
        }

        /// <summary>
        /// 取一个已投影到 NavMesh 的空投点：points 为 null 时围绕 center 取环，
        /// 否则从预设点中随机取一个并在其周围取环；<paramref name="pad"/> 为环内边距。
        /// </summary>
        protected Vector3 GetDropPoint(float pad)
        {
            // ⚠ 两处都必须走本波的随机流（原来 `points.RandomTake()` 与 `GetRandomPointInCircle(...)` 都落在
            //   全局静态流上 ⇒ 两端落点不同）
            Vector3 anchor = points == null ? center : points.RandomTake(random);
            return FpsHelper.GetNavMeshPoint(anchor.GetRandomPointInCircle(random, range + pad, range + pad + 10f));
        }
    }
}
