
using FPSGame.Attributes;
using UnityEngine;
using FPSGame.GameContract;
using FPSGame.Gameplay;
using FPSGame.Core;
namespace FPSGame.Gameplay
{
    /// <summary>
    /// 装备交互组件(一定要有装备！)
    /// </summary>
    public class Furniture_Equip : Furniture_Attached
    {

        public override string Desc => (owner ? "卸载" : "装备") + ShowName;
        [Foldout("配置")]
        public GameObject[] ContGo;
        IEquippable equip;

        private void Start()
        {
            equip= GetComponent<IEquippable>();
        }


        public override void Operate()
        {
            var state = !inOperate;
            if (anim!=null)anim.SetBool(Constants.k_AnimIsActiveParameter, state);
            if(owner != null && owner.TryGetComponent(out EquipController equipController))
            {
                if (state)
                {
                    Debug.LogWarning("安装装备",gameObject);
                    equipController.InstallEquip(equip, this);
                    canOperate = false;
                }
                else
                {
                    Debug.LogWarning("卸载装备", gameObject);
                    equipController.UninstallEquip(equip);
                    canOperate = true;
                }
                foreach (var go in ContGo)
                {
                    go.SetActive(state);
                }
            }
            else
            {
                // 装不上的唯一入口：owner 为空、或它不是玩家预制体（远端重放 4036 时 owner = 盟友实例，应当带 EquipController）
                // ⚠ 原来这里静默跳过 ⇒ "装备没同步"在 Console 上查不到任何线索
                Debug.LogWarning($"[装备] {ShowName}({Id}) 装不上：owner={(owner != null ? owner.name : "<null>")} 没有 EquipController", gameObject);
            }

            base.Operate();
            inOperate = state;
        }

        public override bool CanOperate(GameObject unit)
        {
            if (owner == null || unit == owner) return base.CanOperate(unit);
            else return false;
        }

    }
}