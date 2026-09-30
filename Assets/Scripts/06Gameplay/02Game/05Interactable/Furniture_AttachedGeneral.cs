using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace FPSGame.Furn
{
    /// <summary>
    /// 按 Id 配置表分发的通用附加家具。
    /// </summary>
    [AddComponentMenu("交互/通用附加家具")]
    public class Furniture_AttachedGeneral : Furniture_Attached
    {
        protected FurnAction<Furniture_AttachedGeneral> action;

        // 原 `private static WndManager wndManager => WndManager.Instance;` 已于 2026-09-30 删除：
    // 全类未使用，且玩法层不该点名上层具体类型（要窗口/UI 一律走 ServiceLocator.Wnd）。

        private static Dictionary<string, FurnAction<Furniture_AttachedGeneral>> furnData = new Dictionary<string, FurnAction<Furniture_AttachedGeneral>>() 
        {
            /*
            ["GuardDog"] = new() {
                _Operate = (furn) => {
                    Debug.LogWarning("操作了可装备家具");
                    if(furn.TryGetComponent(out IEquippable equip))
                    {
                        if (furn.owner == null) {
                            Debug.LogWarning("装备");
                            furn.owner.GetComponent<EquipController>().InstallEquip(equip);
                        }
                        else {
                            Debug.LogWarning("卸载");
                            furn.owner.GetComponent<EquipController>().UninstallEquip(equip);
                        }
                        furn.BaseOp();
                    }
                },
                _CanOperate = (furn, unit) => {
                    if (furn.owner == null||unit == furn.owner) return furn.BaseCanOp(unit);
                    else return false;
                }
            },
            */
        };



        #region 实现

        public override void EndHandle()
        {
            action._EndOperate?.Invoke(this);
            base.EndHandle();
        }

        private bool BaseCanOp(GameObject unit) => base.CanOperate(unit);
        private void BaseOp() => base.Operate();

        protected void Start()
        {
            if (!furnData.TryGetValue(Id, out action))
            {
                action = new();
            }
            action._Start?.Invoke(this);
        }

        public override void Operate()
        {
            Debug.LogWarning(Id + " "+ action._Operate);
            action._Operate?.Invoke(this);
        }

        public override bool CanOperate(GameObject unit) => action._CanOperate != null ? action._CanOperate(this, unit) : base.CanOperate(unit);

        protected override void InOperateUpdate() => action._InOperateUpdate?.Invoke(this);
        #endregion

    }
}
