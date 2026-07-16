using System.Collections.Generic;
using System.Linq;
using FPSGame.Furn;
using GameContract;
using UnityEngine;

public class EquipController : MonoBehaviour
{

    Dictionary<IEquippable, IFurniture> equips;

    I_Actor m_actor;
 
    void Awake()
    {
        equips = new ();
        m_actor = GetComponent<I_Actor>();
    }

    public IEnumerable<IEquippable> AllEquips()=> equips.Keys;

    public IEnumerable<IFurniture> AllFurns() => equips.Values;

    public Dictionary<IEquippable, IFurniture> Equips=> equips;

    /// <summary>
    /// 只通过交互组件装载，自己不调用
    /// </summary>
    public void InstallEquip(IEquippable equip,IFurniture furniture)
    {
        if(equips.TryAdd(equip, furniture))
        {

            equip.OnInstall(m_actor, AllEquips);
            equip.OnEquipDestroy += HandleEquipDestroy;
            //不管最后通不通过，都会调用这个
            var toUninstall = equips.Where(kv => kv.Key.NeedUninstall(equip) && kv.Key != equip).ToList();
            foreach (var item in toUninstall)
            {
                Debug.Log("尝试卸载"+item.Key.ID);
                item.Value.Operate();
            }

        }
    }

    /// <summary>
    /// 只通过交互组件卸载，furn调用
    /// </summary>
    public void UninstallEquip(IEquippable equip)
    {
        if (equips.Remove(equip))
        {
            equip.OnUninstall();
            equip.OnEquipDestroy -= HandleEquipDestroy;
        }
    }

    public void HandleEquipDestroy(IEquippable equip)
    {
        equips.Remove(equip);
        //equip.OnEquipDestroy -= HandleEquipDestroy;
    }

}
