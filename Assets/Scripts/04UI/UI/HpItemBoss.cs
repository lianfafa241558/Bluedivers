using System;
using Core;
using Unity.FPS.AI;
using Unity.FPS.Game;
using UnityEngine;
using Utils;
using static WndTools.WndRootTool;

public class HpItemBoss : HpItemBase
{
    private const float ShowDistance = 50f;
    private const float HideDelay = 15f;

    [SerializeField]
    private AudioClip showClip,deathCilp;
    [SerializeField]
    private Transform armorLayout;
    private PartController enemy;
    private int lenght;
    private Damageable[] damageables;
    private bool _visible;
    private float _hideTimer;

    public override void Set(GameObject go)
    {
        base.Set(go);
        this.enemy = go.GetComponent<PartController>();

        damageables = enemy.invincibleArmor.Count > 0 ? enemy.invincibleArmor.ToArray() : enemy.deathArmor;
        lenght = damageables.Length;

        int i=0;
        for (i=0; i< lenght; ++i)
        {
            var item = damageables[i];
            SetActive(armorLayout.GetChild(i), true);
            SetFill(armorLayout.GetChild(i, 1, 0), 1);
            SetFill(armorLayout.GetChild(i, 1, 1), 1);
            item.OnDamage += OnDamage;
        }
        for (; i < armorLayout.childCount; ++i)
        {
            SetActive(armorLayout.GetChild(i),false);
        }
        SetFill(FillW, 1);
        SetFill(FillR, 1);
        _hideTimer = HideDelay;
        // 一开始就在 50m 内则直接播放 Entry 显示，否则初始隐藏
        if (IsPlayerNear())
        {
            _visible = false;
            Show();
        }
        else
        {
            _visible = false;
            SetActive(gameObject, false);
        }
    }

    /// <summary>显示：播放 Entry 动画，首次显示播放 showClip</summary>
    private void Show()
    {
        if (_visible) return;
        _visible = true;
        SetActive(gameObject, true);
        anim.Play("Entry");
        AudioSvc.PlaySound(new(showClip, Core.AudioGroups.UI));

    }

    /// <summary>隐藏：播放 Hide 动画淡出</summary>
    private void Hide()
    {
        if (!_visible) return;
        _visible = false;
        anim.Play("Hide");
    }

    public override void Tick()
    {
        SetFill(FillW, health.GetHpRatio() - 0.02f, Time.deltaTime * 2);
        for (int i = 0; i < lenght; ++i)
        {
            SetFill(armorLayout.GetChild(i,1, 0),GetFill(armorLayout.GetChild(i, 1, 1)), Time.deltaTime * 2);
        }

        if (IsPlayerNear())
        {
            // 范围内：显示并刷新隐藏冷却
            _hideTimer = HideDelay;
            if (!_visible) Show();
        }
        else if (_visible)
        {
            // 离开范围：15 秒后才隐藏
            _hideTimer -= Time.deltaTime;
            if (_hideTimer <= 0f)
            {
                Hide();
            }
        }
    }

    private bool IsPlayerNear()
    {
        if (ActorsManager.Player == null || actor == null) return false;
        return Vector3.Distance(ActorsManager.Player.Pos, actor.Pos) <= ShowDistance;
    }

    public override void End()
    {
        for (int i = 0; i < lenght; ++i)
        {
            damageables[i].OnDamage -= OnDamage;
        }
        anim.Play("Death");
        AudioSvc.PlaySound(new(deathCilp, Core.AudioGroups.UI));
    }

    void OnDamage(Damageable damageable)
    {
        int index = damageables.FindIndex(item=>item==damageable);
        if(index>=0) SetFill(armorLayout.GetChild(index,1,1), damageable.GetArmorRatio());

        // 受击自动显示并重置隐藏冷却
        if (!_visible) Show();
        _hideTimer = HideDelay;
    }

    public override bool CanRecycle()
    {
        if (actor == null) return true;
        if (actor.ActorState == ActorState.Dead) return true;
        return false;
    }

}
