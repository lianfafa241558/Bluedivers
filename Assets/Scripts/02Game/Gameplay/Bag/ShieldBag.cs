using System;
using System.Collections;
using System.Collections.Generic;
using FPSGame.Gameplay;
using GameContract;
using PEMaths;
using Unity.FPS.Game;
using UnityEngine;
using UnityEngine.Events;

public class ShieldBag : BagBase
{
    #region 参数

    public HealthPlayer m_health;

    [InspectorName("激活时的物体")]
    public GameObject activeGo;

    [InspectorName("特效的粒子系统")]
    public GameObject[] vfxs;

    public int restoreTime = 10;

    #endregion

    private void Start()
    {
        //m_health = GetComponent;
        m_health.OnHit += Hit;
        m_health.OnRestoreShield += RestoreShield;
        m_health.OnDie += Die;
    }
    protected override void OnDestroy()
    {
        base.OnDestroy();
        if (!m_health) return;
        m_health.OnHit -= Hit;
        m_health.OnRestoreShield -= RestoreShield;
        m_health.OnDie -= Die;
        m_health = null;
    }

    protected override void Update()
    {
        base.Update();//恢复充电总是执行

        if (!Owner.IsValid()) return;
        
    }
    public override void OnUninstall()
    {
        base.OnUninstall();
        activeGo.SetActive(false);
    }
    public override void OnInstall(I_Actor actor, Func<IEnumerable<IEquippable>> getEquippableList)
    {
        base.OnInstall(actor, getEquippableList);
        activeGo.SetActive(true); 
    }


    public void Hit(GameObject _, Vector3 point,bool _2)
    {
        OnFillChange?.Invoke(true, m_health.CurrentShield.RawFloat / m_health.MaxShield);
        OnTextChange?.Invoke(true, m_health.CurrentShield.RawInt + "/" + m_health.MaxShield);

        OnStateChange?.Invoke(true);
        m_LastTimeOfUse=Time.time;
    }

    public void RestoreShield(PEInt _)
    {
        OnFillChange?.Invoke(true, m_health.CurrentShield.RawFloat / m_health.MaxShield);
        OnTextChange?.Invoke(true, m_health.CurrentShield.RawInt + "/" + m_health.MaxShield);
    }
    private void Die(GameObject _)
    {
        Invoke(nameof(Restore), restoreTime);

    }
    private void Restore()
    {
        m_health.Revive();
    }

}
