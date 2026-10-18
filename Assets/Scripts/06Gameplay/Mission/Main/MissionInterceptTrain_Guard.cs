using System.Collections.Generic;
using FPSGame.Attributes;
using FPSGame.AI;
using FPSGame.Core;
using FPSGame.Game;
using FPSGame.GameContract;
using FPSGame.Gameplay;
using FPSGame.Utils;
using FPSGame.Weapon;
using UnityEngine;

namespace FPSGame.Mission
{
    /// <summary>
    /// 拦截火车 · 火车卫队（partial）：随列车发车生成（房主 <c>CreatPatrol</c> + 4052 复刻）、
    /// 按 Tick 节拍把**护航位**（车头两侧）写进 <c>HomePoint</c> 并自己推目的地、补跟随速度，任务收尾时静默移除。
    /// <para>⚠ 引导必须"先发车、后引导"且落在<b>同一帧</b>内（理由见 <c>Tick</c> 里的注释）。</para>
    /// <para>⚠ 用 <c>HomePoint</c>（到达即**待命**）而**不是** <c>PatrolPos</c>（到达即**自毁**）：
    /// 后者会让卫队追到护航位附近就 <c>Kill(true)</c> 自杀（2026-10-16 实测+代码取证，见 <see cref="GuideGuard"/>）。</para>
    /// </summary>
    public partial class MissionInterceptTrain
    {
        [Foldout("火车卫队", true)]
        [InspectorName("卫队落点后移(米)")]
        [SerializeField] private float guardSpawnBack = 25f;
        [InspectorName("护航位前导(米, 建议=火车速度×卫队刷新间隔)")]
        [SerializeField] private float guardLead = 24f;

        /// <summary>距上次刷护航位的秒数（见 <see cref="GuardRefreshInterval"/>）</summary>
        private float guardRefreshTimer;

        /// <summary>
        /// 卫队护航位的刷新间隔（秒）= 前导距离 / 火车速度。
        /// <para>⚠ 故意**不跟** <c>TickTime</c>：任务的 Tick 已改成 1 秒（为了提示里的发车倒计时），
        /// 但"目标点每秒挪前导距离"会让卫队冲到点就停（顿挫），寻路请求量也翻三倍 ⇒ 这里按"前导 = 两次刷新之间火车走的距离"自洽定节拍。</para>
        /// </summary>
        private float GuardRefreshInterval => guardLead / Mathf.Max(0.1f, trainSpeed);

        #region 调参常量（原 Inspector 旋钮；取值 = prefab 实际值）
        // 纯手感旋钮，要改直接改这里（改回字段即可回到 Inspector）。
        private const float GuardLateral = 10f;             // 护航位侧向(米, 车头两侧)
        private const float GuardLineSpacing = 8f;          // 护航位错开间距(米)
        private const float GuardHoldDistance = 15f;        // 速度分档距离(米)
        private const float GuardCatchUpSpeedRatio = 1.35f; // 追赶速度系数(×火车速度)
        #endregion

        #region 卫队

        /// <summary>
        /// 生成火车卫队（房主权威：<c>CreatPatrol</c> 内部就是房主生成 + 4052 复刻）。
        /// <para>⚠ 卫队里带 <see cref="ActorFlag.Boss"/> 的单位要**立刻摘掉**这个标：否则会占一个 boss 血条。</para>
        /// </summary>
        private void SpawnGuards(TrainRuntime runtime, int squads)
        {
            if (squads <= 0 || manager == null || !manager.IsPresent) return;
            Vector2 now = runtime.Unit != null ? runtime.Unit.Position : default;
            Vector3 behind = new(now.x, 0f, now.y);
            if (runtime.Engine != null) behind -= runtime.Engine.transform.forward * guardSpawnBack;
            Vector3 spawnPos = FpsHelper.GetNavMeshPoint(behind);

            for (int i = 0; i < squads; i++)
            {
                List<GameObject> units = manager.CreatPatrol(spawnPos);
                if (units == null || units.Count == 0) continue;
                for (int k = 0; k < units.Count; k++)
                {
                    GameObject unit = units[k];
                    if (unit == null) continue;
                    if (unit.TryGetComponent(out Actor actor) && actor.HasFlag(ActorFlag.Boss)) actor.RemoveFlag(ActorFlag.Boss);
                    GuideGuard(unit, spawnPos);
                    runtime.Guards.Add(unit);
                }
                // 联机：巡逻队是房主权威 ⇒ 上报"生成点 + HomePoint"（4052），成员端按 HomePoint 复刻同一支。
                // ⚠ 不报的话成员端**根本没有这支卫队**（CreatPatrol 在成员端被闸成空表）。口径同 MissionOilRefining。
                BattleEventBus.ReportPatrolSpawn(spawnPos, spawnPos, true);
            }
        }

        /// <summary>
        /// 把一只卫队切到"跟车待命"口径，两个字段缺一不可：
        /// <list type="bullet">
        /// <item><c>HomePoint</c> = 护航位：**到达即待命**（`EnterIdle` 里 `SetNavDestination(HomePoint)`），
        /// 跟 <c>PatrolPos</c> 的"到达即自毁"完全不同 —— 后者会让卫队追到目标 2m 内自己 <c>Kill(true)</c>。</item>
        /// <item><c>IsFixed</c> = true：关掉 Idle 的 `IdleMaxDuration`(120s) 超时自毁（`IdleBehavior` 里
        /// `!IsFixed &amp;&amp; 最近玩家 &gt; 80m ⇒ Kill(true)`）。该字段按 `Actor` 注释"只对 EnemyMobile 有效"，
        /// 全工程只有那一处读它 ⇒ 无副作用。</item>
        /// </list>
        /// <para>⚠ <c>PatrolPos</c> 必须**保持 default**：出生那一帧 `EnemyMobile.Start` 按它选状态
        /// （有值 ⇒ Patrol；无值 ⇒ Idle），设了就把上面两条全废掉。</para>
        /// </summary>
        private void GuideGuard(GameObject unit, Vector3 home)
        {
            if (unit == null) return;
            // ⚠ IsFixed 是 Actor 的**显式接口实现** ⇒ 必须走 IActor 取（GetComponent<Actor>().IsFixed 也能读，但写着容易误会）
            if (unit.GetComponent<IActor>() is { } actor) actor.IsFixed = true;
            if (unit.TryGetComponent(out EnemyController controller)) controller.HomePoint = home;
        }

        /// <summary>给所有活车刷一遍卫队护航位（由 <see cref="Tick"/> 按 <see cref="GuardRefreshInterval"/> 的节拍调用）</summary>
        private void RefreshAllGuards()
        {
            for (int i = 0; i < liveTrains.Count; i++) RefreshGuards(liveTrains[i]);
        }

        /// <summary>本趟车的"护航基准"：车头 XZ + 轨道前向 + 右向（Tick 刷目标点、按帧补速度都用它 ⇒ 两侧口径一致）</summary>
        private bool GetGuardFrame(TrainRuntime runtime, out Vector2 trainXZ, out Vector3 forward, out Vector3 right)
        {
            trainXZ = default;
            forward = Vector3.forward;
            right = Vector3.right;
            if (runtime == null || runtime.Unit == null) return false;

            TrainUnit train = runtime.Unit;
            trainXZ = train.Position;
            Vector3 f = train.Track != null ? train.Track.SampleForward(train.Distance) : Vector3.forward;
            f.y = 0f;
            forward = f.sqrMagnitude > 0.0001f ? f.normalized : Vector3.forward;
            right = new Vector3(forward.z, 0f, -forward.x);   // 俯视顺时针 90°
            return true;
        }

        /// <summary>
        /// 第 <paramref name="index"/> 只卫队的护航位（未投影）：**左右分边 + 同侧沿轨道错开**，
        /// 避免全队挤在一个点上互相顶。
        /// </summary>
        private Vector3 GuardSlotPosition(int index, Vector2 trainXZ, Vector3 forward, Vector3 right)
        {
            int side = (index & 1) == 0 ? 1 : -1;
            float along = guardLead - (index >> 1) * GuardLineSpacing;
            return new Vector3(trainXZ.x, 0f, trainXZ.y) + forward * along + right * (GuardLateral * side);
        }

        /// <summary>
        /// 把每只卫队的**护航位**（车头两侧 + 沿轨道错开）写进 <c>HomePoint</c>，并**自己推一次目的地**
        /// （Tick 节拍）。
        /// <para>▍为什么自己推目的地：`HomePoint` 只在"进入 Idle"那一刻被读一次
        /// （<c>EnemyMobile.EnterIdle</c>），而 <c>IdleBehavior</c> 不会重发 ⇒ 光写 <c>HomePoint</c> 人不会动。
        /// 自己推还顺带把 `Return`（战斗后 AI 会下"回出生点"）覆盖掉 —— `ReturnBehavior` 只有 `UpdateAutoRotate()`，
        /// 不会把目的地抢回去。</para>
        /// <para>▍护航位为什么带前导（<see cref="guardLead"/>，默认 24 = 8m/s × 3 秒刷新间隔）：
        /// 目标点是**两次刷新之间静态**的，若压在车头位置，卫队会在一个刷新周期内跑到点、停一下，等下一次刷新再冲 24m
        /// （顿挫感）；放在前方一个刷新周期的行程处，它就一直在跑、从不"到点停住"。</para>
        /// <para>⚠ 投影到 NavMesh 不能省：目标不可达时 SetDestination 会失败，而 <c>EnemyController</c> 的去重会把失败记成"已请求"。</para>
        /// <para>⚠ 成员端 <c>SetNavDestination</c> 会被 <c>RemoteDrivenMovement</c> 门掉（还会刷限流日志）⇒ 只在非成员端推；
        /// 成员端靠房主广播的目的地（<c>SetNavDestination → BattleEventBus.EnemyMove → 4024</c>）。</para>
        /// </summary>
        private void RefreshGuards(TrainRuntime runtime)
        {
            if (runtime.Ended || runtime.Guards.Count == 0) return;
            if (!GetGuardFrame(runtime, out Vector2 trainXZ, out Vector3 forward, out Vector3 right)) return;

            for (int i = runtime.Guards.Count - 1; i >= 0; i--)
            {
                GameObject unit = runtime.Guards[i];
                if (unit == null)
                {
                    runtime.Guards.RemoveAt(i);
                    continue;
                }
                if (!unit.TryGetComponent(out EnemyController controller)) continue;

                Vector3 slot = FpsHelper.GetNavMeshPoint(GuardSlotPosition(i, trainXZ, forward, right));
                controller.HomePoint = slot;   // 战斗后回 Idle 时会读到新点
                if (!EnemyController.RemoteDrivenMovement) controller.SetNavDestination(slot);
            }
        }

        /// <summary>
        /// 按帧兜卫队速度。⚠ 只靠 ~3 秒的护航位刷新补不够：`Patrol`/`Beware`/`Return` 每次切换，AI 自己会增删速度修饰
        /// （实测 `Patrol` → 2 m/s、`Return` → 3 m/s），那段时间卫队又被火车甩开。
        /// <para>补速度**不碰寻路**（没动 <c>PatrolPos</c>），不会给 `PathRequestManager` 加压 ——
        /// 这也是它敢放在按帧的 <c>Update</c> 里、而"挪目标点"必须留在 <see cref="GuardRefreshInterval"/> 节拍的原因。</para>
        /// </summary>
        private void RefreshGuardSpeeds()
        {
            for (int i = 0; i < liveTrains.Count; i++)
            {
                TrainRuntime runtime = liveTrains[i];
                if (runtime.Ended || runtime.Guards.Count == 0) continue;
                if (!GetGuardFrame(runtime, out Vector2 trainXZ, out Vector3 forward, out Vector3 right)) continue;

                for (int k = runtime.Guards.Count - 1; k >= 0; k--)
                {
                    GameObject unit = runtime.Guards[k];
                    if (unit == null)
                    {
                        runtime.Guards.RemoveAt(k);
                        continue;
                    }
                    if (!unit.TryGetComponent(out EnemyController controller)) continue;
                    Vector3 pos = unit.transform.position;
                    Vector3 slot = GuardSlotPosition(k, trainXZ, forward, right);
                    MatchGuardSpeed(controller, Vector2.Distance(new Vector2(pos.x, pos.z), new Vector2(slot.x, slot.z)));
                }
            }
        }

        /// <summary>
        /// 把卫队的移动速度补到"跑得到自己的护航位、又不会挤进车体"。
        /// <para>▍三段口径（阈值都相对<b>自己的护航位</b>，不是相对车头）：
        /// 远了（&gt; 1.5×<see cref="GuardHoldDistance"/>）⇒ 用 <see cref="GuardCatchUpSpeedRatio"/> 追；
        /// 到分档距离附近 ⇒ 与火车<b>等速</b>（×1，跟着走不再靠近）；
        /// 贴太近（&lt; 0.6×分档距离）⇒ 压到 0.9× 主动落回（别往车厢里钻）。</para>
        /// <para>⚠ Extra 修饰是**累加**的 ⇒ 想写绝对值必须"补差"（want − 当前 FinalValue），
        /// 这样哪怕 AI 进出 `Patrol`/`Beware`/`Return` 时自己增删过修饰（实测 2~3 m/s），下一次刷新也会自动纠回来。</para>
        /// </summary>
        private void MatchGuardSpeed(EnemyController controller, float distanceToSlot)
        {
            if (controller.Speed == null) return;

            float ratio = 1f;
            if (distanceToSlot > GuardHoldDistance * 1.5f) ratio = Mathf.Max(1f, GuardCatchUpSpeedRatio);
            else if (distanceToSlot < GuardHoldDistance * 0.6f) ratio = 0.9f;

            PEMaths.PEInt want = (PEMaths.PEInt)Mathf.Max(1f, trainSpeed * ratio);
            PEMaths.PEInt current = controller.Speed.FinalValue;
            if (current.RawInt == want.RawInt) return;
            controller.Speed.AddModifier(ModifierType.Extra, want - current);
        }

        /// <summary>静默移除卫队（<c>IsRemove</c> 语义：无死亡动画、不计击杀归属）</summary>
        private void RemoveGuards(TrainRuntime runtime)
        {
            for (int i = 0; i < runtime.Guards.Count; i++)
            {
                GameObject unit = runtime.Guards[i];
                if (unit == null) continue;
                if (unit.TryGetComponent(out I_AIController ai)) ai.Kill(true);
                else Destroy(unit);
            }
            runtime.Guards.Clear();
        }

        #endregion
    }
}