using System;
using FPSGame.GameContract;
using PEMaths;
using UnityEngine;
using UnityEngine.AI;

namespace FPSGame.Game
{

/// <summary>
/// 单位 / 噪声 / 命中 相关事件的**下层总线**（2026-09-30 从 BattleEventSub 拆出）。
///
/// <para>▍为什么单独拆：按"一个事件该放哪一层 = min(发布者层, 订阅者层)"的判据，
/// 本组事件的<strong>发布者全部在单位内核</strong>（Actor / Health / Damageable / ActorsManager / FpsHelper，
/// 将来进 05_UnitCore），所以本文件将来要随 05_UnitCore 一起下沉；
/// 其余事件（任务 / 空投 / 撤离 / 团灭 / 窗口…）的发布者与订阅者都在 managers / gameplay / UI 层，
/// 留在上层总线 BattleEventSub。</para>
///
/// <para>▍为什么没有把 Actor 改成 I_Actor：本文件与 Actor 类<strong>同处一个程序集</strong>（现在都是
/// Assembly-CSharp，将来一起进 05_UnitCore），签名里可以直接用具体类型 Actor，
/// 因此 12 个事件与全部订阅处理函数一个字都不用改（避免 20 处无谓改动与 cast 风险）。</para>
/// </summary>
public static class UnitEventBus
{
    public static event Action<IActor> OnUnitPosChange;
    /// <summary>单位位置改变</summary>
    public static void UnitPosChange(IActor unit) => OnUnitPosChange?.Invoke(unit);

    /// <summary>单位死亡</summary>
    public static event Action<Actor> OnUnitDeath;
    public static void UnitDeath(Actor unit) => OnUnitDeath?.Invoke(unit);

    /// <summary>单位被杀死</summary>
    public static event Action<Actor, Actor> OnUnitKill;
    public static void UnitKill(Actor attacker, Actor victim) => OnUnitKill?.Invoke(attacker, victim);

    /// <summary>单位被击中(受击者，来源)</summary>
    public static event Action<GameObject, GameObject> OnUnitHit;
    public static void UnitHit(GameObject victim, GameObject attacker) => OnUnitHit?.Invoke(victim, attacker);


    /// <summary>表现层：子弹击中地面(近战，超射程消失也算)，给 HUD 做擦弹/受击提示用</summary>
    public static event Action<GameObject, PEVector3, PEInt> OnBulletHit;
    /// <summary>表现层：子弹击中地面(近战，超射程消失也算)，给 HUD 做擦弹/受击提示用</summary>
    public static void BulletHit(GameObject source, PEVector3 pos, PEInt rauids) => OnBulletHit?.Invoke(source, pos, rauids);


    /// <summary>
    /// 逻辑层噪声：AI 听觉的统一入口。开枪(枪口)、命中(弹着点/爆心)各发一条，由 DetectionModule 统一消费。
    /// 与表现层分开：音效播放距离走 WeaponBaseController.SFXRange / DamageData.SoundRadius(交给 AudioSvc)，
    /// 本事件只服务 AI 听觉(噪声点的位置 + 响度)，两者互不影响。
    /// </summary>
    public static event Action<NoiseData> OnNoise;
    /// <summary>逻辑层噪声：AI 听觉的统一入口(开枪/命中/爆炸都发这里)</summary>
    public static void Noise(NoiseData noise) => OnNoise?.Invoke(noise);

    /// <summary>
    /// AI 寻路请求：单位把"给这个 NavMeshAgent 设目标点"交给寻路服务统一处理（去重 / 真失败重试 / 投影兜底）。
    ///
    /// <para>▍2026-10-01 用它取代 <c>ServiceLocator.Path</c> 槽：该调用是**无返回值的命令**
    /// （失败由订阅方自己 <c>LogWarning</c>、发布方按节流重发）⇒ 事件是最合适的载具，不必做注入。</para>
    /// <para>▍层归属：发布者 = AI（<c>06_Gameplay</c>），订阅者 = <c>PathRequestManager</c>（<c>09_Managers</c>）
    /// ⇒ 按"事件放 <c>min(发布者层, 订阅者层)</c>"判据，本总线（<c>05_UnitCore</c>）正确。</para>
    /// <para>⚠ 无订阅者时**静默丢弃**（与原来空对象 <c>NullPathService</c> 的语义一致）。</para>
    /// </summary>
    public static event Action<NavMeshAgent, Vector3, bool> OnPathRequest;
    /// <summary>请求寻路（agent / 目标点 / 是否打印调试日志）</summary>
    public static void PathRequest(NavMeshAgent agent, Vector3 destination, bool log) => OnPathRequest?.Invoke(agent, destination, log);


    #region 单位

    /// <summary>敌人被创建</summary>
    public static event Action<Actor> OnEnemyCreate;
    public static void EnemyCreate(Actor unit) => OnEnemyCreate?.Invoke(unit);

    /// <summary>敌人死亡</summary>
    public static event Action<Actor> OnEnemyDead;

    /// <summary>敌人死亡 </summary>
    public static void EnemyDead(Actor unit) => OnEnemyDead?.Invoke(unit);

    /// <summary> 特殊单位被创建</summary>
    public static event Action<Actor> OnSpecUnitCreate;
    public static void SpecUnitCreate(Actor unit) => OnSpecUnitCreate?.Invoke(unit);
    /// <summary> 特殊单位倒地 </summary>
    public static event Action<Actor> OnSpecUnitDead;
    public static void SpecUnitDead(Actor unit) => OnSpecUnitDead?.Invoke(unit);

    //盟友创建还没有

    /// <summary>玩家倒地</summary>
    public static event Action<Actor> OnPlayerDead;
    public static void PlayerDead(Actor unit) => OnPlayerDead?.Invoke(unit);

    /// <summary>玩家复活 </summary>
    public static event Action<Actor> OnPlayerRevive;
    public static void PlayerRevive(Actor unit) => OnPlayerRevive?.Invoke(unit);

    /// <summary>玩家被创建（原 GlobalEventSub，2026-09-30 迁入：发布者是 Actor.cs）</summary>
    public static event Action<IActor> OnPlayerCreate;
    public static void PlayerCreate(IActor unit) => OnPlayerCreate?.Invoke(unit);

    /// <summary> 盟友被创建（原 GlobalEventSub，2026-09-30 迁入：发布者是 Actor.cs）</summary>
    public static event Action<Actor> OnFriendCreate;
    public static void FriendCreate(Actor unit) => OnFriendCreate?.Invoke(unit);

    /// <summary>
    /// 盟友的**角色已确定**（模型挂好、<c>Actor.Id/ShowName/Portrait/Color</c> 就位；发布者是 <c>FriendController.AttachModel</c>）。
    /// <para>▍为什么不能只靠 <see cref="OnFriendCreate"/>：那是 <c>Actor</c> 刚被创建时发的，
    /// 那一刻还没有资料/模型 ⇒ <c>Actor.Id</c> 是空的，任何"按角色 id 判断"的逻辑都会漏。</para>
    /// </summary>
    public static event Action<Actor> OnFriendRoleChanged;
    public static void FriendRoleChanged(Actor unit) => OnFriendRoleChanged?.Invoke(unit);

    /// <summary>
    /// 盟友**离场**（对象被销毁；发布者是 <c>Actor.OnDestroy</c>）。
    /// <para>▍与"倒地"的区别：倒地走 <see cref="OnUnitDeath"/>（单位还在场上、能被救起）；
    /// 离场是实例被销毁（掉线/被房主清退/换场景），<b>不可能再回来</b> —— 所有为它建过的实例（UI 行、
    /// 小地图点、桥的字典…）都必须在此时收尾。</para>
    /// <para>▍为什么要这个事件：<c>ActorsManager.Unregister</c> 只会把它从注册表摘掉，
    /// **不会通知任何人**；UI 若靠"遍历注册表"发现它消失，就永远拿不到"谁来清理我"的时机
    /// （尤其资源/实例是"为它专门创建"的时候）。</para>
    /// </summary>
    public static event Action<Actor> OnFriendLeave;
    public static void FriendLeave(Actor unit) => OnFriendLeave?.Invoke(unit);

    #endregion
}

/// <summary>
/// 逻辑层噪声(只给 AI 听觉用)：声源 + 噪声点 + 响度(半径/米)。
/// 响度越大越"响"；AI 只允许"更响"的噪声覆盖当前警惕点，避免弹着点的小动静把枪声点顶掉。
/// </summary>
public struct NoiseData
{
    /// <summary>噪声源(开火者/爆炸物持有者)，用于队伍过滤；为空时该噪声被忽略</summary>
    public GameObject source;
    /// <summary>噪声点：枪口 / 弹着点 / 爆心</summary>
    public PEVector3 pos;
    /// <summary>噪声半径(米，逻辑层)：越大越响，<=0 视为无效噪声</summary>
    public PEInt radius;
}
}
