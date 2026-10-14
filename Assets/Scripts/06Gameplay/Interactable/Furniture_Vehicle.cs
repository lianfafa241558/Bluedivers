using System.Collections;
using System.Collections.Generic;
using FPSGame.GameContract;
using FPSGame.Game;
using UnityEngine;
using FPSGame.Utils;
using FPSGame.Gameplay;

namespace FPSGame.Gameplay
{

/// <summary>
/// 载具组件
/// </summary>
[AddComponentMenu("交互/载具交互")]
public class Furniture_Vehicle : Furniture_Attached
{

    private IDrivable controller;
    private IActor actor;
    private bool isDead;

    /// <summary>驾驶者**自己**的生命组件（上车时订阅 ⇒ 驾驶者死亡也要强制下车）。存成字段才能可靠退订。</summary>
    private IHealth _riderHealth;

    /// <summary>【联机】这辆车现在**被远端骑手占着** ⇒ 本机玩家**不能**上（2026-10-15）。
    ///
    /// <para>▍为什么必须有它：本类的 <see cref="CanOperate"/> 继承自
    /// <see cref="Furniture_Attached.CanOperate"/>，它只看**本端**的 <c>inOperate</c>；而远端骑手是 09 的桥
    /// 用 <see cref="ApplyRemoteMount"/> 挂上去的（**刻意不置** <c>inOperate</c>，见它的注释）⇒
    /// 两端各自都以为自己能上 ⇒ 实测"玩家 a 上了之后玩家 b 也能上，同一台机甲里坐了两个人"。
    /// 由 09 的桥在 <c>ApplyMount/ApplyUnmount</c> 里推过来（06 看不见 09）。</para>
    ///
    /// <para>⚠ 只标记**远端**骑手：本机玩家自己在开时<u>不</u>置位 —— 否则 <c>CanOperate</c> 变 false，
    /// 交互提示（"退出驾驶"）会被 <c>OperationWnd</c> 收掉。</para></summary>
    private bool _remoteOccupied;

    public override void Operate()
    {
        if (isDead)
        {
            return;
        }
        if (owner)
        {
            owner.transform.position = transform.TransformPoint(0,0,1);
        }
        base.Operate();
        controller = relatedTrans2.GetComponent<IDrivable>();
        actor = relatedTrans2.GetComponent<IActor>();
        controller.SetOwener(owner);
        desc = inOperate ? "退出驾驶" : "进入驾驶";
        if (owner)
        {
            owner.transform.parent = relatedTrans2;
            actor.OnDeath += OnActorDeath;

            // ★【驾驶者死亡 ⇒ 强制下车】原来这里只接"**车**死了"（actor.OnDeath）⇒ 车没死而**人**被打死时
            //   没人清交互状态：人就一直挂在车上、还能接着开（2026-10-14 实测）。
            //   ⚠ 与 VehicleController 那侧各管一半，互不重复：那边管"把人从接管里放出来"（含死亡态的守卫），
            //     这边管**本地交互状态**（inOperate / owner）—— 不清的话死后按 F 会走 Operate() 把尸体搬到入场点。
            _riderHealth = owner.GetComponent<IHealth>();
            if (_riderHealth != null)
            {
                _riderHealth.OnDie -= OnRiderDie;   // 幂等：反复上车 / 上次没退干净时不会双挂
                _riderHealth.OnDie += OnRiderDie;
            }
        }
    }

    /// <summary>
    /// 【驾驶者死亡 ⇒ 就地强制下车】清掉本家具侧的交互状态，并让控制器放人（唯一收口）。
    ///
    /// <para>▍为什么不复用 <see cref="Operate"/>：它第一句是"把驾驶者搬到入场点 <c>transform.TransformPoint(0,0,1)</c>"
    /// （本地上下机的那套动作）—— 死亡下车是把**倒地的人留在原地**，搬过去就穿帮了。</para>
    ///
    /// <para>▍<c>controller.SetOwener(null)</c> 是上下机的唯一收口：它抛的
    /// <c>BattleEventBus.VehicleOwnerChanged</c> 是 09 的桥上报 4053 的唯一来源 ⇒ 远端才会把骑手摘掉。</para>
    /// </summary>
    private void OnRiderDie(GameObject caused, GameObject origin)
    {
        // ⚠ 这条订阅**不是唯一保险**：共享 OnDie 列表里任何一个订阅者抛异常，排在后面的就都不执行
        //   ⇒ VehicleController 那侧另有一条"轮询驾驶者血量"的兜底（见它的 LateUpdate）。
        //   这里负责**本地交互状态**（inOperate / owner）：不清的话死后按 F 会把尸体搬走。
        UnhookRiderHealth();

        inOperate = false;
        owner = null;
        controller?.SetOwener(null);
    }

    /// <summary>【联机 · 远端应用】把某个**远端实例**挂上 / 摘下载具。
    /// <para>▍为什么不复用 <c>VehicleController.SetOwener</c>：那条路硬要 <c>PlayerController</c>（读相机 / 关 CC /
    /// 改 <c>ActorState</c> / 关 <c>WeaponCamera</c>），盟友实例只有 <c>FriendController</c> ⇒ 必 NRE；
    /// 且那些"本机玩家专属开关"在远端本来就不该执行。</para>
    /// <para>▍**挂到哪**：本地那份的最终状态（<see cref="InOperateUpdate"/> 的 1s 贴合跑完之后）
    /// = 位置 <c>relatedTrans.position</c>、朝向 <c>relatedTrans.rotation</c>，父级 = <c>relatedTrans2</c>
    /// ⇒ 远端必须直接落在这个**终点**上。
    /// ⚠ 2026-10-14 修：原来落的是**入场点** <c>transform.TransformPoint(0,0,1)</c>（<see cref="Operate"/> 的第一句），
    /// 那是"刚上车时"的位置，与座位差着一段 ⇒ 实测客机上骑手停在机甲外/上方的入口位置（"进驻位置不对"）。</para>
    /// <para>⚠ 刻意**不置** <c>inOperate</c>：那会让 <c>Update → InOperateUpdate</c> 跑起来，而它读的
    /// <c>controller</c>/<c>InputHandler</c> 是"本机玩家按了退出键就走 <c>Operate()</c>"的路子 ⇒ 在远端会误触发。</para>
    /// </summary>
    public void ApplyRemoteMount(GameObject rider, bool mounted)
    {
        if (rider == null) return;

        var t = rider.transform;
        if (mounted)
        {
            if (relatedTrans2 == null)
            {
                FPSGame.Utils.NetSyncLog.Warn("载具/上机·收", $"{name} 的 relatedTrans2（载具根）没配 ⇒ 骑手只能停在挂点位置上");
            }

            if (relatedTrans != null)
            {
                // 先摆到**座位的世界位姿**（= 本地 1s 贴合后的终点），再"保持世界位姿"挂到载具根下
                t.SetPositionAndRotation(relatedTrans.position, relatedTrans.rotation);
            }
            else
            {
                // 没配座位 ⇒ 退化成入场点（<see cref="Operate"/> 的第一句），至少不是原点
                t.position = transform.TransformPoint(0f, 0f, 1f);
            }
            t.SetParent(relatedTrans2, true);
        }
        else
        {
            t.SetParent(null, true);
        }
    }

    /// <summary>【联机 · 桥调用】标记"这辆车有**远端**骑手"（见 <see cref="_remoteOccupied"/>）。
    /// <para>▍设置点只有两个：<c>NetVehicleBridge.ApplyMount</c>（收到"某人上机"并真把他挂上来了）与
    /// <c>ApplyUnmount</c>（下机 / 被顶 / 死亡 / 离场 / 换场景）。</para></summary>
    public void SetRemoteOccupied(bool occupied) => _remoteOccupied = occupied;

    /// <summary>【联机 · 桥调用】本机玩家被"抢车 / 被别人顶掉"时**强制下车**（等价于替他按一次 F）。
    ///
    /// <para>▍为什么必须走 <see cref="Operate"/>：它是上下机的**唯一收口** —— 里面才有
    /// <c>controller.SetOwener(null)</c> 与它抛的 <c>BattleEventBus.VehicleOwnerChanged</c>
    /// （= 4053 上报的唯一来源）。在这儿自己散着清几个字段，远端那边会留一个"人还在车里"的幽灵。</para>
    ///
    /// <para>▍⚠ 只在"他**真的**在车里"（<c>inOperate</c>）时才动：<see cref="Operate"/> 第一句是
    /// "把 owner 搬到入场点 <c>transform.TransformPoint(0,0,1)</c>"（那是**上车**动作）⇒ 对一个站在旁边的人
    /// 调它 = 把人凭空搬走。</para>
    /// </summary>
    public void ForceLocalExit()
    {
        if (!inOperate || owner == null) return;

        FPSGame.Utils.NetSyncLog.SyncLog("载具/上机·收", $"{name} 强制本机下车（{owner.name}）");
        Operate();
    }

    protected override void InOperateUpdate()
    {
        var delay = Time.time - lastOperatetime;

        if (Tool.In(delay, -1f, 1) && owner)
        {
            var owner = this.owner.transform;
            var point = relatedTrans;
            owner.rotation = Quaternion.Slerp(owner.rotation, point.rotation, Time.deltaTime * 10);
            if (Vector3.Distance(point.position, owner.position) > 0.15f)
            {
                owner.position = Vector3.Lerp(owner.position, point.position, Time.deltaTime * 4);
            }
        }
        else
        {
            if (owner && controller.TryExit())
            {
                Operate();
                actor.OnDeath -= OnActorDeath;
            }
        }
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        isDead = false;
    }

    private void OnActorDeath()
    {
        actor.OnDeath -= OnActorDeath;
        Operate();
        isDead = true;

        // 车没了 ⇒ 驾驶者的死亡订阅也退掉（否则会挂在"已下车的驾驶者"身上，下次上车再挂一次）
        UnhookRiderHealth();
    }

    protected override void OnDisable()
    {
        base.OnDisable();
        if (actor != null)
        {
            actor.OnDeath -= OnActorDeath;
        }

        // ★【必须退订驾驶者的 OnDie】(2026-10-15)
        //   订阅挂在**驾驶者**（本机玩家 / 盟友实例）那份 Health 上，而它是**共享**的（同一份列表里还挂着
        //   Actor / PlayerController / 玩家窗 / HUD 等一圈）。原来这里不退 ⇒ 探针实测打出"目标已销毁、
        //   方法还挂在表里"（`Furniture_Vehicle.OnRiderDie@<noObj>`）—— 那不只是泄漏：它在列表里排**前面**，
        //   一旦抛异常，后面排着的 `VehicleController.OnDriverDie` 就**整段不执行**
        //   （表现 = 驾驶者死了没被移出载具、还能接着控制和开火）。
        UnhookRiderHealth();
    }

    /// <summary>退掉驾驶者生命的死亡订阅（**幂等**：没订阅过 / 已退过都安全）。</summary>
    private void UnhookRiderHealth()
    {
        if (_riderHealth != null) _riderHealth.OnDie -= OnRiderDie;
        _riderHealth = null;
    }

    public override bool CanOperate(GameObject unit)
    {
        // 【联机 · 一车一人】(2026-10-15) 远端骑手正在开这辆 ⇒ 本机玩家**不能**上。
        // 交互提示（OperationWnd / SubtitleWnd）都用 CanOperate 判定 ⇒ 提示会一起收起来；
        // Furniture_Attached.Handle 也过这道 ⇒ 绕过 UI 也上不去。没有它，两端各坐一个（实测"两人同车"）。
        return !isDead && !_remoteOccupied && base.CanOperate(unit);
    }


}
}
