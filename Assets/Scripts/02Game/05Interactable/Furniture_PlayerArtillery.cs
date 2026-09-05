using System;
using System.Collections.Generic;
using FPSGame.Attribute;
using Unity.FPS.Game;
using UnityEngine;

/// <summary>
/// 玩家火炮炮弹架（欧帕兹装填）。
///
/// 继承 <see cref="Furniture_OOPartDepositBase"/>：按住交互键以 10/秒 的速率，把玩家背包里的
/// 欧帕兹(任意类型)逐个装填进本架，每个矿石即转化为一发炮弹(<see cref="shells"/>)，矿石不退回。
///
/// 装填节奏（与基类一致）：
/// 本次允许长按时长 = min(剩余上限 capacity-held, 玩家携带矿石数) / 10 秒；
/// 例如已装 1 发、上限 10、手上有 5 个 → 可长按 min(9,5)/10 = 0.5 秒。
/// 长按 0.3 秒即已装 3 发；松开时已装矿石已转成炮弹并保留（不退回），
/// 下次可长按时长 = min(10-4, 手上矿石)/10，按剩余继续累计，无需从 0 开始。
///
/// 每装填一发，用 <see cref="BattleManager.BattleRandom"/> 从 {0,1,3,4,5}(无 2) 随机取一个
/// 伤害档位 index 存入 <see cref="shells"/>，并同步 <see cref="m_weapon"/> 的弹匣数为已装发数。
/// 射击时 <see cref="OnShoot"/> 从 <see cref="shells"/> 末尾弹出档位并赋给
/// <see cref="WeaponBaseController.UseDamageIndex"/>。
/// </summary>
public class Furniture_PlayerArtillery : Furniture_OOPartDepositBase
{
    /// <summary>装填的炮弹可选伤害档位（无 2）</summary>
    private static readonly int[] ShellIndexPool = { 0, 1, 3, 4, 5 };

    [DisplayField]
    [SerializeField]
    [InspectorName("已装填的炮弹")]
    private List<int> shells = new();

    [DisplayField]
    [SerializeField]
    [InspectorName("武器")]
    private WeaponController m_weapon;

    /// <summary>当前已装填发数</summary>
    public int Progress => shells.Count;

    public override string Desc => "装填炮弹[" + ShowName + "]";

    protected override void Awake()
    {
        base.Awake();
        m_weapon = GetComponentInChildren<WeaponController>();
        m_weapon.OnShoot += OnShoot;
        // 若场景/配置里预置了已装填炮弹，让进度与上限计算从该基数继续
        held = shells.Count;
    }

    private void OnDestroy()
    {
        if (m_weapon) m_weapon.OnShoot -= OnShoot;
        m_weapon = null;
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        BattleEventSub.OnAirdrop += OnAirdrop;
        BattleManager.Instance.Authorize(Constants.PlayerArtilleryAId, true);
        BattleManager.Instance.Authorize(Constants.PlayerArtilleryBId, true);
        Invoke(nameof(StartSubmit),0.1f);
    }

    protected override void OnDisable()
    {
        base.OnDisable();
        BattleEventSub.OnAirdrop -= OnAirdrop;
        BattleManager.Instance.Authorize(Constants.PlayerArtilleryAId, false);
        BattleManager.Instance.Authorize(Constants.PlayerArtilleryBId, false);
    }

    void StartSubmit()
    {
        shells.Clear();
        //初始送3发
        var rand = BattleManager.Instance.BattleRandom;
        for (int i = 0; i < 3; ++i)
        {
            int idx = ShellIndexPool[rand.Range(0, ShellIndexPool.Length)];
            shells.Add(idx);
            m_weapon.UseDamageIndex = idx;
        }
        m_weapon.Magazine.CurrValue = shells.Count;
    }

    /// <summary>
    /// 监听战备释放：极速射(PlayerArtilleryA)释放即由本架立即补一发 0 档位炮弹（马上会被打出去）。
    /// </summary>
    private void OnAirdrop(GameObject source, GameObject beacon, Vector3 point, AirdropController.AirdropData data)
    {

        if (data.cfg.ID == Constants.PlayerArtilleryAId)
        {
            shells.Add(0);
            m_weapon.UseDamageIndex = 0;
            m_weapon.Magazine.CurrValue = shells.Count;
            //Debug.LogWarning("发射战备前:炮弹数量" + shells.Count + "弹匣数量" + m_weapon.Magazine.CurrValue);
        }
    }

    /// <summary>
    /// 发射时调用：从已装填炮弹中弹出末发档位用于本次射击，并同步剩余进度以便继续装填。
    /// </summary>
    private void OnShoot(WeaponBaseController weapon)
    {
        int index;
        if (shells.Count > 0)
        {
            index = shells[shells.Count - 1];
            shells.RemoveAt(shells.Count - 1);
            //刚好到0时
            if (shells.Count==0)
            {
                BattleManager.Instance.Authorize(Constants.PlayerArtilleryBId, false);
            }
        }
        else
        {
            index = 0;
        }

        weapon.UseDamageIndex = index;
        // 与 shells 保持同步：装填进度随发射消耗而减少，腾出空间供再次装填
        held = shells.Count;
        if (held < capacity) canOperate = true;
    }

    /// <summary>
    /// 每装填 1 个欧帕兹触发一次：转化为 1 发炮弹，随机决定其伤害档位。
    /// </summary>
    protected override void OnSubmit(GameObject user, OOPartEnum type)
    {
        //重新有了
        if (shells.Count == 0)
        {
            BattleManager.Instance.Authorize(Constants.PlayerArtilleryBId, true);
        }
        var rand = BattleManager.Instance.BattleRandom;
        int idx = ShellIndexPool[rand.Range(0, ShellIndexPool.Length)];
        shells.Add(idx);
        // 弹匣量 = 已装填发数（供 UI 与可发射数）
        m_weapon.Magazine.CurrValue = shells.Count;
        m_weapon.UseDamageIndex = idx;
        Debug.LogWarning("炮弹数量"+ shells.Count+"弹匣数量"+ m_weapon.Magazine.CurrValue);
    }

    /// <summary>
    /// 玩家松开（未装满即松手）时触发：已装填炮弹保留，仅作占位/可覆盖处理。
    /// </summary>
    protected override void OnPressCancel(GameObject user, int transferred)
    {
        // 本架按累计保留模型：松开不清空，下次按剩余继续装填
    }
}
