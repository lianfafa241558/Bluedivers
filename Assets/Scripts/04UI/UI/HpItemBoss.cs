using System;
using Unity.FPS.AI;
using Unity.FPS.Game;
using UnityEngine;
using Utils;
using static WndTools.WndRootTool;

public class HpItemBoss : HpItemBase
{
    public Transform armorLayout;
    private PartController enemy;
    private int lenght;

    private Damageable[] damageables;
    public override void Set(GameObject go)
    {
        base.Set(go);
        this.enemy = go.GetComponent<PartController>();

        damageables = enemy.invincibleArmor.Length > 0 ? enemy.invincibleArmor : enemy.deathArmor;
        lenght = damageables.Length;

        int i=0;
        for (i=0; i< lenght; ++i)
        {
            var item = damageables[i];
            SetActive(armorLayout.GetChild(i), true);
            SetFill(armorLayout.GetChild(i, 1, 0), item.GetArmorRatio());
            SetFill(armorLayout.GetChild(i, 1, 1), item.GetArmorRatio());
            item.OnDamage += OnDamage;
        }
        for (; i < armorLayout.childCount; ++i)
        {
            SetActive(armorLayout.GetChild(i),false);
        }
        SetFill(FillW, 1);
        SetFill(FillR, 1);
    }

    public override void Tick()
    {
        SetFill(FillW, health.GetHpRatio() - 0.02f, Time.deltaTime * 2);
        for (int i = 0; i < lenght; ++i)
        {
            SetFill(armorLayout.GetChild(i,1, 0),GetFill(armorLayout.GetChild(i, 1, 1)), Time.deltaTime * 2);
        }
       
    }

    public override void End()
    {
        for (int i = 0; i < lenght; ++i)
        {
            damageables[i].OnDamage -= OnDamage;
        }
    }

    void OnDamage(Damageable damageable)
    {
        int index = damageables.FindIndex(item=>item==damageable);
        SetFill(armorLayout.GetChild(index,1,1), damageable.GetArmorRatio());
    }
}
