using FPSGame.Core;
using FPSGame.Game;
using FPSGame.GameContract;
using UnityEngine;

namespace FPSGame.AI
{
    /// <summary>联机 · **成员端 AI LOD**：本端敌人的移动意图由房主 4024 下发（<c>SetNavDestination</c> 一律作废），
    /// 它的 AI 决策只剩"表现"价值 ⇒ 按距离把**没在交战**的敌人的感知 / 决策降频，砍掉远处杂兵的开销。
    ///
    /// <para>▍两条硬门（踩了就是玩法事故）：
    /// ① **只有成员端降**（<c>EnemyController.RemoteDrivenMovement</c>）—— 房主是决策端，他的 AI 一格都不降；
    /// ② **交战中（Follow/Attack/Beware）不降** —— 敌人开火节奏在成员端是**本机算的**，而"敌人打我"的伤害也由本机权威
    /// （玩家血量各自管）⇒ 降频会让玩家少挨打（这就是 <c>engaged</c> 参数的存在理由）。</para>
    ///
    /// <para>▍只降敌人（<c>UnitTypeEnum.Enemy</c>）：凯伊 / 炮台这类 SpecUnit 的数量少、且走动与开火各自算，收益小风险大，不碰。</para>
    ///
    /// <para>▍不降的层：远端意图落地（<c>SyncedNavMover</c>）、伤害与生死（统一通道）、状态与技能 tick（牵扯血量结算）、
    /// 炮台锁头 <c>LateUpdate</c>、动画与 FX。</para>
    ///
    /// <para>▍⚠ 载具（机甲 / 可驾驶炮台）的伤害落点**已钉在房主**：非权威端"AI 来源"的载具伤害被
    /// <c>Damageable.LocalDamageGate</c> 在伤害入口直接挡掉（口径见 <c>BattleState.IsVehicleDamageAuthority</c>）
    /// ⇒ 它与本 LOD 无耦合。改 LOD 范围时别绕开那道门（要让远处载具"少挨打"是另一回事，得先改权威口径）。</para>
    ///
    /// <para>⚠ 降频只是"少跑几次"、**不补 deltaTime** ⇒ 帧内用 <c>Time.deltaTime</c> 累加的东西（转向 / 计时器）在远处会变慢；
    /// 这正是把范围限死在"非交战 + 敌人 + 成员端"的原因。</para>
    /// </summary>
    public static class AiLod
    {
        /// <summary>总开关（排查"远处单位看着发呆"时可临时置 false）。</summary>
        public static bool Enabled = true;

        const float NearSqr = 60f * 60f;     // ≤60m：每帧
        const float MidSqr = 120f * 120f;    // ≤120m：隔 1 帧
        const float FarSqr = 200f * 200f;    // ≤200m：隔 3 帧；更远：隔 7 帧

        static int _skipped, _total;

        /// <summary>本帧该不该跑这个单位的 AI（房主 / 单机 / 非敌人 / 交战中 / 近处 ⇒ 恒 true）。</summary>
        /// <param name="engaged">本单位当前是否"在打 / 在追"（由调用方按自己的状态机给，见 <c>AIInputBaseController.AiLodEngaged</c>）。</param>
        public static bool ShouldTick(IActor actor, bool engaged = false)
        {
            // ① 只有成员端降：房主 / 单机的 AI 质量就是游戏难度
            if (!Enabled || !EnemyController.RemoteDrivenMovement) return true;
            if (engaged) return true;
            if (actor == null || actor.Type != UnitTypeEnum.Enemy) return true;

            var self = actor as Component;          // ⚠ 接口引用不能用 ?. / ==null（Unity 假 null）⇒ 借组件引用判
            if (self == null) return true;

            var player = ActorsManager.Player as Component;   // LOD 看的是**本机玩家**（盟友看他那台自己的画面）
            if (player == null) return true;                  // 本机玩家还没建出来：不猜、不降

            float sqr = (self.transform.position - player.transform.position).sqrMagnitude;
            int period = sqr <= NearSqr ? 1 : sqr <= MidSqr ? 2 : sqr <= FarSqr ? 4 : 8;

            ++_total;
            if (period == 1) return true;

            // ⚠ 相位按单位错开：否则整批单位会在同一帧一起跑 / 一起不跑（帧时间尖峰，比不降还糟）
            int key = actor.NetId != 0 ? actor.NetId : actor.IndexID;
            if (((Time.frameCount + key) & (period - 1)) == 0) return true;

            ++_skipped;
            return false;
        }

        /// <summary>排查用：确认 LOD 真的在降（<c>execute_code</c> 里读一次即可，读完归零）。</summary>
        public static string Dump()
        {
            string s = $"AiLod Enabled={Enabled} 成员端={EnemyController.RemoteDrivenMovement} " +
                       $"降频帧={(_total > 0 ? 100f * _skipped / _total : 0f):F1}% （跳过 {_skipped}/{_total}）";
            _skipped = 0;
            _total = 0;
            return s;
        }
    }
}
