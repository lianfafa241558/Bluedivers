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
    public override void Set(GameObject enemy)
    {
        base.Set(enemy);
        this.enemy = enemy.GetComponent<PartController>();
        lenght = this.enemy.invincibleArmor.Length;
        Debug.Log("长度"+ lenght);
        int i=0;
        for (i=0; i< lenght; ++i)
        {
            var item = this.enemy.invincibleArmor[i];
            SetActive(armorLayout.GetChild(i), true);
            SetFill(armorLayout.GetChild(i, 1, 0), item.GetArmorRatio());
            SetFill(armorLayout.GetChild(i, 1, 1), item.GetArmorRatio());
            item.OnDamage += OnDamage;
        }
        for (; i < armorLayout.childCount; ++i)
        {
            SetActive(armorLayout.GetChild(i),false);
        }
    }

    public override void Tick()
    {
        SetFill(FillW, health.GetHpRatio() - 0.02f, Time.deltaTime * 2);
        for (int i = 0; i < lenght; ++i)
        {
            var item = enemy.invincibleArmor[i];
            SetFill(armorLayout.GetChild(i,1, 0), item.GetArmorRatio(), Time.deltaTime * 2);
        }
       
    }

    public override void End()
    {
        for (int i = 0; i < lenght; ++i)
        {
            enemy.invincibleArmor[i].OnDamage -= OnDamage;
        }
    }

    void OnDamage(Damageable damageable)
    {
        int index = enemy.invincibleArmor.FindIndex(item=>item==damageable);
        SetFill(armorLayout.GetChild(index,1,1), damageable.GetArmorRatio());
    }
}
