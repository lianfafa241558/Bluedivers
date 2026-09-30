using System;
using System.Collections.Generic;
using FPSGame.Gameplay;
using GameContract;
using PEMaths;
using Unity.FPS.Game;
using UnityEngine;

/// <summary>
/// 护盾包（护盾充能器）：**只负责把护盾数据对接给 UI**（填充条 / 文本 / 状态）。
/// 护盾的实际行为（被打碎 → 收起 → 延时重塑 → 复活展开）由 <see cref="ShieldRebuild"/> 负责，
/// 本组件不再订阅 OnDie、也不再自己收起/展开护盾。
/// </summary>
public class ShieldBag : BagBase
{
    #region 参数

    [InspectorName("护盾所在物体")]
    [Tooltip("只用于读护盾值刷 UI；留空则取自身/子物体上的 IHealth")]
    [SerializeField]
    private GameObject _healthGo;

    [InspectorName("护盾重塑")]
    [Tooltip("实际生效的护盾行为：装卸时通过它收起/展开护盾")]
    [SerializeField]
    private ShieldRebuild _rebuild;

    #endregion

    /// <summary>运行时解析出的护盾（接口不能序列化，所以存物体再取组件）</summary>
    private IHealth _health;

    private void Start()
    {
        ResolveHealth();
        if (_health == null)
        {
            Debug.LogWarning(gameObject + "：ShieldBag 找不到 IHealth，UI 不会刷新。", gameObject);
            return;
        }

        // 只订阅"需要刷 UI"的事件；生死与重塑由 ShieldRebuild 负责
        _health.OnHit += Hit;
        _health.OnRestoreShield += RestoreShield;
    }

    /// <summary>解析护盾：优先配置的物体，其次自身，最后子物体（含未激活）</summary>
    private void ResolveHealth()
    {
        _health = _healthGo != null ? _healthGo.GetComponent<IHealth>() : GetComponent<IHealth>();
        if (_health == null) _health = GetComponentInChildren<IHealth>(true);
    }

    protected override void OnDestroy()
    {
        base.OnDestroy();
        if (_health == null) return;
        _health.OnHit -= Hit;
        _health.OnRestoreShield -= RestoreShield;
    }

    public override void OnInstall(I_Actor actor, Func<IEnumerable<IEquippable>> getEquippableList)
    {
        base.OnInstall(actor, getEquippableList);
        if (_rebuild != null) _rebuild.SetShieldActive(true);
    }

    public override void OnUninstall()
    {
        base.OnUninstall();
        // 收起护盾并取消待重塑计时，避免卸下后又被延时逻辑展开
        if (_rebuild != null) _rebuild.SetShieldActive(false);
    }

    /// <summary>护盾受击：刷新填充 / 文本 / 状态</summary>
    public void Hit(GameObject _, Vector3 point,Vector3 _2, bool _3)
    {
        RefreshShieldUI();
        OnStateChange?.Invoke(true);
        m_LastTimeOfUse = Time.time;
    }

    /// <summary>护盾恢复：刷新填充 / 文本</summary>
    public void RestoreShield(PEInt _)
    {
        RefreshShieldUI();
    }

    /// <summary>把当前护盾值推到 UI</summary>
    private void RefreshShieldUI()
    {
        if (_health == null || _health.GetShieldRatio() <= 0) return;
        OnFillChange?.Invoke(true, _health.GetShieldRatio());
        OnTextChange?.Invoke(true, _health.GetShieldCurrent() + "/" + _health.GetShieldMax());
    }
}
