using System.Collections.Generic;
using System.Linq;
using FPSGame.Attribute;
using FPSGame.Furn;
using Unity.FPS.Game;
using UnityEngine;
using Utils;

/// <summary>
/// 炮弹架交互家具：玩家需手持指定 ID 的物体（如炮弹）才能交互。
/// 每次成功交互放置一个，进度 +1；仅当进度小于上限时才可交互。
/// 交互时视为玩家放下该物体：禁用物体上的家具组件、关闭落地重力并放置到指定点位。
/// 配合 KeyScreen 的 Artillery（炮弹架）阶段使用，放置时通过 base.Operate() 触发
/// GlobalEventSub.OnFurnitureOperate 事件推进阶段进度。
/// </summary>
public class Furniture_Artillery : Furniture_Base
{
    [SerializeField]
    [InspectorName("所需物体ID")]
    [Tooltip("玩家必须手持 Id 等于该值的物体才能交互")]
    private string requireId;

    [DisplayField]
    [SerializeField]
    [InspectorName("已放置的炮弹")]
    private List<int> shells=new();

    [DisplayField]
    [SerializeField]
    [InspectorName("已放置的炮弹")]
    private WeaponController m_weapon;


    /// <summary>当前已放置进度（运行时只读）</summary>
    public int Progress => shells.Count();

    public override string Desc => "放置[" + requireId + "]";


    protected override void Awake()
    {
        base.Awake();
        m_weapon = GetComponentInChildren<WeaponController>();
        m_weapon.OnShoot += OnShoot;
    }
    private void OnDestroy()
    {
        m_weapon.OnShoot -= OnShoot;
        m_weapon = null;
    }

    private void OnShoot(WeaponBaseController weapon)
    {
        int index;  
        if (shells.Count > 0)
        {

            index = shells[shells.Count - 1];
            shells.RemoveAt(shells.Count - 1);
        }
        else
        {
            index = 0;
        }
        weapon.UseDamageIndex = index;
    }

    /// <summary>
    /// 交互判断：基类条件 && 进度未满 && 玩家手持匹配物体。
    /// </summary>
    public override bool CanOperate(GameObject unit)
    {
        if (!base.CanOperate(unit)) return false;
        if (Progress >= ExtFloatParameter) return false;
        return HasRequireItem(unit);
    }

    /// <summary>
    /// 交互操作：放下玩家手持的匹配物体（禁用其家具、关闭重力、移到放置点位），
    /// 进度 +1，随后调用 base.Operate() 触发家具操作事件推进 KeyScreen 阶段。
    /// 复位运行态，允许连续放置多个直到达到上限。
    /// </summary>
    public override void Operate()
    {
        var user = owner;
        PlaceHeldItem(user);
        base.Operate();
        // 复位运行态：允许在同一架子上连续放置多个物体直到进度满
        inOperate = false;
        owner = null;
    }

    /// <summary>是否玩家手持匹配 ID 的物体</summary>
    private bool HasRequireItem(GameObject user)
    {
        if (user == null || string.IsNullOrEmpty(requireId))
        {
            return false;
        }
        if (!user.TryGetComponent(out EquipController equip))
        {
            return false;
        }
        foreach (var furn in equip.AllFurns())
        {
            Debug.LogWarning("furn"+(furn != null? furn.Id : ""));
            if (furn != null && furn.Id == requireId) return true;
        }
        Debug.LogWarning("没有找到对应id的物品");
        return false;
    }

    /// <summary>放下玩家手持的匹配物体：从装备移除、禁用家具、关闭重力、移到放置点位</summary>
    private void PlaceHeldItem(GameObject user)
    {
        if (user == null || !user.TryGetComponent(out EquipController equip)) return;
        foreach (var kv in equip.Equips.ToList())
        {
            var furn = kv.Value;
            if (furn == null || furn.Id != requireId) continue;

            var go = furn.gameObject;
            // 视为放下：从玩家装备列表移除（触发卸载）
            if (kv.Key != null) equip.UninstallEquip(kv.Key);
            // 禁用物体上的家具，避免再次被拾取/交互
            if (furn is MonoBehaviour mb) mb.enabled = false;
            // 关闭落地重力，使物体固定在放置点位
            if (go.TryGetComponent(out CharacterController cc)) cc.enabled = false;
            // 放到指定点位
            if (relatedTrans != null)
            {
                //go.transform.position = relatedTrans.position;
                go.transform.SetParent(relatedTrans, false);
                go.transform.localPosition = Vector3.zero;
                go.transform.localEulerAngles = Vector3.zero;
                Tool.Destroy(go,0.5f);
            }
            int index = (int)((Furniture_Base)furn).ExtFloatParameter;
            shells.Add(index);
            m_weapon.UseDamageIndex = index;
            m_weapon.Magazine.CurrValue = shells.Count;
            break;
        }
    }
    /// <summary>
    /// event检视器调用
    /// </summary>
    public void AuthorizeAirdrop()
    {
        BattleManager.Instance.Authorize(Constants.ArtilleryId,true);
    }


}
