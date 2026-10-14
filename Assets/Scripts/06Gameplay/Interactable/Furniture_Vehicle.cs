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
        if (_riderHealth != null)
        {
            _riderHealth.OnDie -= OnRiderDie;
            _riderHealth = null;
        }

        inOperate = false;   // 本地交互状态：不然 Update→InOperateUpdate 还会跑（死后按 F 会把尸体搬走）
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
        if (_riderHealth != null)
        {
            _riderHealth.OnDie -= OnRiderDie;
            _riderHealth = null;
        }
    }

    protected override void OnDisable()
    {
        base.OnDisable();
        if (actor != null)
        {
            actor.OnDeath -= OnActorDeath;
        }
    }

    public override bool CanOperate(GameObject unit)
    {
        return !isDead && base.CanOperate(unit);
    }


}
}
