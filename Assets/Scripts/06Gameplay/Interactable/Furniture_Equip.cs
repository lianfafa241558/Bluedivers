
using System.Globalization;
using FPSGame.Attributes;
using UnityEngine;
using FPSGame.GameContract;
using FPSGame.Gameplay;
using FPSGame.Core;
using FPSGame.Game;
using FPSGame.Weapon;
namespace FPSGame.Gameplay
{
    /// <summary>
    /// 装备交互组件(一定要有装备！)
    /// </summary>
    public class Furniture_Equip : Furniture_Attached
    {
        // —— 4059 状态码（本类专用；"重放"那条不带 StateKey，不会撞） ——
        /// <summary>装备弹量（StateKey = "弹匣,备弹"，-1 = 无限/未知）</summary>
        const int StateAmmo = 2;
        /// <summary>打光消失（StateKey = "gone"）</summary>
        const int StateGone = 3;
        const string GoneKey = "gone";

        public override string Desc => (owner ? "卸载" : "装备") + ShowName;

        /// <summary>装备类（背包 / 护卫犬）一律上图，不必逐个 prefab 勾"小地图显示"。</summary>
        protected override bool ShowOnMinimap => true;

        /// <summary>
        /// 在地面时上图；被装上（<see cref="Furniture_Attached.inOperate"/>）后不再单独标点 —— 它就挂在玩家背上跟着走。
        /// <para>身份是 Actor 的（护卫犬）不登记标记，改为在装/卸时切 <see cref="ActorFlag.MiniMapIgnore"/>
        /// 让**单位通道**的图标跟着显隐（见 <see cref="Operate"/>）。</para>
        /// </summary>
        protected override bool MapMarkActive => Identity is not IActor && !inOperate;

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
                // 装不上的唯一入口：owner 为空、或它不是玩家预制体（远端重放 4059 时 owner = 盟友实例，应当带 EquipController）
                // ⚠ 原来这里静默跳过 ⇒ "装备没同步"在 Console 上查不到任何线索
                Debug.LogWarning($"[装备] {ShowName}({Id}) 装不上：owner={(owner != null ? owner.name : "<null>")} 没有 EquipController", gameObject);
            }

            base.Operate();
            inOperate = state;

            // 状态变了 ⇒ 小地图跟上：背包走标记通道（装上就不单独标点），
            // 护卫犬是 Actor（走单位通道）⇒ 用 MiniMapIgnore 让它跟着"装没装上"显隐。
            // ⚠ 远端重放（4059）也走这里 ⇒ 两端自然一致，不用再单开消息。
            RefreshMapMark();
            if (Identity is Actor unit)
            {
                if (state) unit.AddFlag(ActorFlag.MiniMapIgnore);
                else unit.RemoveFlag(ActorFlag.MiniMapIgnore);
            }

            // ★【联机】卸下时把**这件装备的弹药**交给对端：对端那份副本从没被开过枪，
            //   不写就是满弹 ⇒ "用剩的背包卸下来给别人捡"会变回满弹药。
            //   ⚠ 必须在 base.Operate()（已派发 4059 重放）之后再发，KCP 可靠有序 ⇒ 对端先还原装备状态再写弹量。
            if (!state) ReportAmmo();
        }

        /// <summary>【联机】把当前弹量上报（走 4059 的 StateKey 分支：对端直接写值，**不重放** Operate）。</summary>
        private void ReportAmmo()
        {
            if (!TryGetSyncWeapon(out WeaponReloadController weapon)) return;

            weapon.GetAmmoForSync(out int magazine, out int ammo);
            ReportState(StateAmmo, string.Format(CultureInfo.InvariantCulture, "{0},{1}", magazine, ammo));
        }

        /// <summary>【联机】"打光消失"由 <see cref="WeaponBag"/> 弹药耗尽时调用：让对端也把这一份收掉。
        /// <para>▍为什么需要单独一条：背包开火只同步**表现**（无弹药字段），对端的副本永远不满不了弹、
        /// 也就永远走不到自动卸载 ⇒ 不通知的话队友背上的背包会一直留着。</para></summary>
        public void ReportGone()
        {
            if (!gameObject.activeSelf) return;
            ReportState(StateGone, GoneKey);
        }

        /// <summary>本类能同步弹药的那把武器（背包武器在 <see cref="WeaponBag.weapon"/> 上；普通装备没有枪 ⇒ false）。</summary>
        private bool TryGetSyncWeapon(out WeaponReloadController weapon)
        {
            var bag = GetComponent<WeaponBag>();
            weapon = bag != null ? bag.weapon : null;
            if (weapon == null) weapon = GetComponent<WeaponReloadController>();
            return weapon != null;
        }

        /// <summary>远端状态（StateKey 分支，不重放 Operate）：<c>StateGone</c> = 收掉本端这一份；
        /// <c>StateAmmo</c> = 写入权威弹量。</summary>
        public override void ApplyRemoteState(int code, string key)
        {
            if (code == StateGone)
            {
                if (gameObject.activeSelf) gameObject.SetActive(false);
                return;
            }

            if (code != StateAmmo || string.IsNullOrEmpty(key)) return;

            string[] parts = key.Split(',');
            if (parts.Length != 2) return;
            if (!int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out int magazine)) return;
            if (!int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int ammo)) return;

            if (TryGetSyncWeapon(out WeaponReloadController weapon)) weapon.ApplyAmmoForSync(magazine, ammo);
        }

        public override bool CanOperate(GameObject unit)
        {
            if (owner == null || unit == owner) return base.CanOperate(unit);
            else return false;
        }

    }
}