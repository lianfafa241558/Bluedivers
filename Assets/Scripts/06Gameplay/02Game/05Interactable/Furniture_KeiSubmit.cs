using FPSGame.Core.Interface;
using System.Collections.Generic;
using System.Linq;
using FPSGame.Attributes;
using FPSGame.Furn;
using FPSGame.GameContract;
using FPSGame.Game;
using UnityEngine;
using FPSGame.Gameplay;
using FPSGame.Utils;

namespace FPSGame.Gameplay
{

/// <summary>
/// 凯伊(Kei)提交点。
/// <para>玩家靠近交互后**一次只交一种**：</para>
/// <para>1. 优先交出手里携带的、Id 命中白名单(<see cref="_submitItemIds"/>)的手持物 —— 提交即从装备栏卸下并移除，仅计入任务进度；</para>
/// <para>2. 手里没有可提交的手持物时，才把自身背包中的欧帕兹全部提交给凯伊，计入任务采集计数。</para>
/// <para>⚠ 两者都有时：本次只交手持物即结束，想交背包欧帕兹需要**再交互一次**。</para>
/// </summary>
[AddComponentMenu("交互/欧帕兹提交台")]
public class Furniture_KeiSubmit : Furniture_Attached
{
    [Foldout("可提交手持物", true)]
    [SerializeField]
    [InspectorName("可提交的手持物ID白名单")]
    [Tooltip("玩家手上(HandEquip)携带、且 Id 命中这里的物体可在此交出；提交即移除，仅计入任务进度。不区分大小写")]
    private List<string> _submitItemIds = new() { "Artifact" };

    public override string Desc => "提交欧帕兹给凯伊";

    /// <summary>
    /// 长按时间：手里拿着可提交的手持物（非欧帕兹，如神器）时**按下即完成**，不做长按；
    /// 其余情况（交背包里的欧帕兹）沿用配置的 <see cref="Furniture_Attached.meetTime"/>。
    /// <para>交互控制器(<see cref="PlayerOperationController"/>)按 <c>MeetTime == 0</c> 走"瞬间操作"分支，
    /// UI(OperationWnd / SubtitleWnd) 也会相应显示"按"而不是"长按"。</para>
    /// </summary>
    public override float MeetTime
    {
        get
        {
            var player = ActorsManager.Player;
            // 手里拿着白名单手持物 → 瞬时提交（非欧帕兹的物品不需要长按）
            if (player.IsValidMono() && HasHeldSubmittable(player.gameObject)) return 0f;
            return base.MeetTime;
        }
    }

    private void Start()
    {
        // 已有采集数据时直接显示历史提交
        if (FPSGame.GameContract.ServiceLocator.Task.CollectProperty.Count > 0)
        {
            foreach (var kvp in FPSGame.GameContract.ServiceLocator.Task.CollectProperty)
            {
                GlobalEventSub.KeiSubmit(kvp.Key, kvp.Value);
            }
        }
    }

    public override bool CanOperate(GameObject unit)
    {
        if (!base.CanOperate(unit)) return false;
        if (unit == null) return false;

        // 手里拿着白名单内的手持物也能提交
        if (HasHeldSubmittable(unit)) return true;

        // 只有携带了欧帕兹的玩家才能提交
        if (unit.TryGetComponent(out PlayerOOPartInventory bag))
        {
            return bag.CurrentCount > 0;
        }
        return false;
    }

    public override void Operate()
    {
        base.Operate();
        var user = owner;
        if (user != null)
        {
            // 一次交互只交一种：优先手持物；本次交过手持物就不再顺带交背包欧帕兹
            if (SubmitHeldItems(user) > 0)
            {
                // 交完手持物即结束本次交互，"想交欧帕兹需要再交互一次"
                GlobalEventSub.PlayMeetSpeech(user, SpeechTypeEnum.Responded);
            }
            else
            {
                // 手里没有可提交的手持物，才交背包里的欧帕兹
                SubmitBagOOParts(user);
                GlobalEventSub.PlayMeetSpeech(user, SpeechTypeEnum.Responded);
            }
        }

        // 复位运行态：允许在同一提交点反复提交（base.Operate() 会把 inOperate 置真，
        // 而 CanOperate 要求 !inOperate，不复位的话第二次就选不中它了）。同 Furniture_Artillery。
        EndHandle();
    }

    /// <summary>把玩家背包里的欧帕兹全部提交给凯伊，计入任务采集计数</summary>
    private static void SubmitBagOOParts(GameObject user)
    {
        if (user == null || !user.TryGetComponent(out PlayerOOPartInventory bag)) return;

        // 取快照：提交过程中会从背包移除条目，避免枚举时修改同一集合
        foreach (var kvp in bag.GetAll().ToList())
        {
            OOPartEnum type = kvp.Key;
            int count = kvp.Value;
            bag.Remove(type, count);
            // 计入任务采集计数
            FPSGame.GameContract.ServiceLocator.Battle.SubmitOOPart(user, type, count);
        }
    }

    /// <summary>指定 Id 是否命中可提交白名单（不区分大小写）</summary>
    private bool IsSubmittable(string id)
    {
        if (string.IsNullOrEmpty(id) || _submitItemIds == null) return false;
        for (int i = 0; i < _submitItemIds.Count; i++)
        {
            if (string.Equals(_submitItemIds[i], id, System.StringComparison.OrdinalIgnoreCase)) return true;
        }
        return false;
    }

    /// <summary>
    /// 玩家是否手里拿着白名单内的手持物。
    /// 无分配，可安全用于每帧的交互判定（<see cref="Furniture_Attached.CanOperate"/> 会被交互扫描反复调用）。
    /// </summary>
    private bool HasHeldSubmittable(GameObject unit)
    {
        if (!unit.TryGetComponent(out EquipController equip)) return false;
        foreach (var kv in equip.Equips)
        {
            if (IsAlive(kv.Value) && IsSubmittable(kv.Value.Id)) return true;
        }
        return false;
    }

    /// <summary>
    /// 取出装备栏里所有命中白名单的手持物。
    /// 返回快照——卸载过程会修改 <see cref="EquipController.Equips"/>。
    /// </summary>
    private List<KeyValuePair<IEquippable, IFurniture>> FindHeldSubmittables(EquipController equip)
    {
        var re = new List<KeyValuePair<IEquippable, IFurniture>>();
        foreach (var kv in equip.Equips)
        {
            var furn = kv.Value;
            if (!IsAlive(furn) || !IsSubmittable(furn.Id)) continue;
            re.Add(new KeyValuePair<IEquippable, IFurniture>(kv.Key, furn));
        }
        return re;
    }

    /// <summary>
    /// 提交玩家手上携带的白名单手持物，返回实际提交数量。
    /// 每个都先通报"被交出"（<see cref="ISubmittableHandItem"/>，任务等订阅方据此计数），
    /// 再从装备栏卸载（触发落地/复位）并销毁，即"提交了直接移除"。
    /// </summary>
    private int SubmitHeldItems(GameObject user)
    {
        if (!user.TryGetComponent(out EquipController equip)) return 0;

        var held = FindHeldSubmittables(equip);
        foreach (var kv in held)
        {
            if (kv.Value is ISubmittableHandItem submittable) submittable.NotifySubmitTo(user);
            equip.UninstallEquip(kv.Key);
            FPSGame.Utils.Tool.Destroy(kv.Value.gameObject);
        }
        return held.Count;
    }

    /// <summary>
    /// 物体是否仍然有效。接口引用用不到 Unity 的 <c>==</c> 重载（被销毁的对象仍"不为 null"），这里补一次 UnityEngine.Object 判定。
    /// </summary>
    private static bool IsAlive(IFurniture furn)
    {
        if (furn == null) return false;
        return !(furn is UnityEngine.Object uo) || uo != null;
    }
}
}
