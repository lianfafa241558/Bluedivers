using Core;
using FPSGame.Attribute;
using GameContract;
using Unity.FPS.Game;
using Unity.FPS.Gameplay;
using UnityEngine;

namespace FPSGame.Furn
{
    /// <summary>
    /// 武器拾取交互组件：挂在支援武器预制体上，玩家靠近按交互键拾取。
    /// 仅允许拾取 WeaponTypeEnum=Support 的武器，且仅当支援槽（槽2）为空时。
    /// 武器被携带时本组件应被禁用（脱离 Furniture_Attached.list，不被交互扫描），
    /// 卸载落地时 re-enable 重新注册可被再次拾取。
    /// </summary>
    public class Furniture_WeaponPickup : Furniture_Attached
    {
        WeaponPlayerController m_Weapon;


        protected override void Awake()
        {
            base.Awake();
            m_Weapon = GetComponent<WeaponPlayerController>();
        }

        /// <summary>武器上无 I_Actor，基类 ShowName 取 _actor 会 NRE，改为武器名</summary>
        public override string ShowName => m_Weapon.WeaponName;

        /// <summary>武器上无 I_Actor，覆写避免 NRE</summary>
        public override string Id => m_Weapon ? m_Weapon.WeaponName : name;

        /// <summary>武器上无 I_Actor，覆写 Icon 避免 NRE（供轮盘图标显示）</summary>
        protected override Sprite Icon => m_Weapon ? m_Weapon.WeaponIcon : null;

        public override string Desc => "拾取 " + ShowName;

        /// <summary>
        /// 交互判断：基类条件 && 是支援武器 && 交互者必须是玩家。
        /// 支援槽有武器时也允许交互，捡起时会先卸下手里再换上。
        /// </summary>
        public override bool CanOperate(GameObject unit)
        {
            if (!base.CanOperate(unit)) return false;
            if (m_Weapon == null || m_Weapon.WeaponTypeEnum != WeaponTypeEnum.Support) return false;

            // 仅限玩家交互（轮盘卸载不走 CanOperate，无影响）
            return unit != null && unit.GetComponent<PlayerWeaponsManager>() != null;
        }

        /// <summary>
        /// 交互操作。被携带时组件为禁用态（不在交互列表中），但仍会被轮盘回调调用：
        /// - inOperate=false（地面可拾取态）：拾取。若支援槽已有武器，先卸下手里的（落地）再换上捡起的，
        ///   最后安装到玩家的 EquipController（内部调武器 OnInstall 入支援槽），入槽后由 EquipGroundWeapon 禁用本组件。
        /// - inOperate=true（已被携带，轮盘点击卸载）：卸载，走 EquipController.UninstallEquip，
        ///   内部调武器 OnUninstall 落地并 re-enable 本组件（ResetForPickup 复位）。
        /// 不调用 base.Operate()，避免触发 FurnitureOperate 事件/语音副作用。
        /// </summary>
        public override void Operate()
        {
            var user = owner;
            if (user == null || m_Weapon == null) return;
            if (!user.TryGetComponent(out EquipController equipController)) return;

            if (!inOperate)
            {
                // 拾取：若支援槽已有武器，先卸下手里的（触发其 OnUninstall 落地）再换上捡起的
                if (user.TryGetComponent(out PlayerWeaponsManager wm))
                {
                    int supportSlot = PlayerWeaponsManager.SlotOf(WeaponTypeEnum.Support);
                    var oldWeapon = wm.GetWeaponAtSlotIndex(supportSlot);
                    if (oldWeapon != null && oldWeapon != m_Weapon)
                    {
                        equipController.UninstallEquip(oldWeapon);
                    }
                }

                equipController.InstallEquip(m_Weapon, this);
                inOperate = true;
                // 入槽后由 EquipGroundWeapon 禁用本组件
            }
            else
            {
                equipController.UninstallEquip(m_Weapon);
                // 卸载落地后由武器 OnUninstall 调 ResetForPickup 复位并 re-enable
            }
        }

        /// <summary>
        /// 卸载落地后复位本组件，使其可被再次拾取。
        /// </summary>
        public void ResetForPickup()
        {
            canOperate = true;
            inOperate = false;
            owner = null;
            pressTime = 0;
        }
    }
}
