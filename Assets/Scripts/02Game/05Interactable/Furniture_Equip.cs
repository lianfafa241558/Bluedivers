
using FPSGame.Attribute;
using UnityEngine;
namespace FPSGame.Furn
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

        protected override void Start()
        {
            base.Start();
            equip= GetComponent<IEquippable>();
        }


        public override void Operate()
        {
            var state = !inOperate;
            if (anim!=null)anim.SetBool(Constants.k_AnimIsActiveParameter, state);
            if(owner.TryGetComponent(out EquipController equipController))
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