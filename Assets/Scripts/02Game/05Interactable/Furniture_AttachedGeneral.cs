using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace FPSGame.Furn
{
    public class Furniture_AttachedGeneral : Furniture_Attached
    {
        protected FurnAction<Furniture_AttachedGeneral> action;

        private static WndManager wndManager => WndManager.Instance;

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