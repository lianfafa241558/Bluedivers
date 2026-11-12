using System.Globalization;
using FPSGame.Core;
using FPSGame.Attributes;
using FPSGame.GameContract;
using FPSGame.Game;
using UnityEngine;
using FPSGame.Gameplay;
using FPSGame.GameData;
using FPSGame.Weapon;

namespace FPSGame.Gameplay
{
    /// <summary>
    /// 武器拾取交互组件：挂在支援武器预制体上，玩家靠近按交互键拾取。
    /// 仅允许拾取 WeaponTypeEnum=Support 的武器，且仅当支援槽（槽2）为空时。
    /// 武器被携带时本组件应被禁用（脱离 Furniture_Attached.list，不被交互扫描），
    /// 卸载落地时 re-enable 重新注册可被再次拾取。
    /// </summary>
    [AddComponentMenu("交互/武器拾取")]
    public class Furniture_WeaponPickup : Furniture_Attached
    {
        // —— 4059（CmdId.FurnitureOperateNtf）的"地面武器在/不在"状态码 ——
        // ⚠ 接收端只有在 StateKey **非空**时才会走 ApplyRemoteState（不重放 Operate），见
        //   NetFurnitureBridge.OnRemoteOperate ⇒ 两个状态都必须带 key，否则会掉进"重放交互"分支。
        /// <summary>在地上 / 刚落地（StateKey = "x,y,z,弹匣,备弹"，弹量 -1 = 无限/未知）</summary>
        const int StateOnGround = 1;
        /// <summary>被拿走（入槽）</summary>
        const int StateTaken = 0;
        /// <summary>被拿走时的 StateKey（只需非空，内容不代表位置）</summary>
        const string TakenKey = "taken";
        /// <summary>弹尽被弃（<c>WeaponFlag.AutoDeath</c>）：两端各自清掉，别再当"回到地上"</summary>
        const int StateSpent = 2;
        /// <summary>弹尽被弃时的 StateKey（同 TakenKey：只需非空）</summary>
        const string SpentKey = "spent";

        /// <summary>弹尽被弃后到销毁的延迟(秒)</summary>
        const float SpentDestroyDelay = 5f;

        WeaponPlayerController m_Weapon;

        /// <summary>本实例是否已经历过"开局那次 OnEnable"（见 <see cref="OnEnable"/> 里的广播条件）</summary>
        bool sawFirstEnable;

        /// <summary>弹尽被弃：不可再交互 / 不上小地图 / <see cref="SpentDestroyDelay"/> 秒后销毁（见 <see cref="MarkSpent"/>）</summary>
        bool m_Spent;


        protected override void Awake()
        {
            base.Awake();
            m_Weapon = GetComponent<WeaponPlayerController>();
        }

        public override string ShowName => m_Weapon.WeaponName;

        public override string Id => m_Weapon ? m_Weapon.WeaponName : name;

        protected override Sprite Icon => m_Weapon ? m_Weapon.WeaponIcon : null;

        public override string Desc => "拾取 " + ShowName;

        /// <summary>支援武器一律上图（不必在每个武器 prefab 上逐个勾"小地图显示"）；显隐就是"还在不在手上"。</summary>
        protected override bool ShowOnMinimap => true;

        /// <summary>弹尽被弃的那把不再上图（否则躺在地上等销毁的 5 秒里还会被当成可捡的支援武器）。</summary>
        protected override bool MapMarkActive => ShowOnMinimap && !m_Spent;

        /// <summary>
        /// "这把枪现在还在地上"= 本组件 enabled：入槽时 <c>PlayerWeaponsManager.EquipGroundWeapon</c> 会 disable，
        /// 卸载落地时 <c>WeaponPlayerController.OnUninstall</c> 会 enable ⇒ 直接拿 enabled 当开关，不另存状态量；
        /// 小地图标记与 4059 状态广播都挂在这一对回调上。
        ///
        /// <para>▍为什么广播的是"状态"而不是走重放：<see cref="Operate"/> 刻意不调 <c>base.Operate()</c>
        /// （避免语音/事件副作用）⇒ 不进 <c>GlobalEventBus.FurnitureOperate</c>；而且武器是独占资源、两端各一份实例，
        /// 对端重放 <c>Operate()</c> 也只会往盟友幽灵的装备表里塞一条（幽灵没有 <c>PlayerWeaponsManager</c>），
        /// 世界表现一点不变 ⇒ 只能用"在地/不在 + 落点"这条状态通道。</para>
        /// </summary>
        protected override void OnEnable()
        {
            base.OnEnable();   // 小地图标记由基类按 ShowOnMinimap 统一登记

            // 弹尽被弃后落地（卸载流程会 re-enable 本组件）：StateSpent 已在 MarkSpent 里报过，
            // 这里再报"回到地上 + 落点"会让对端把刚清掉的枪又摆回来。
            if (m_Spent) return;

            // ⚠ 开局那次不广播：两端场景里本来就各有一份同位置的枪，广播只会互相覆盖"权威状态"
            //   （典型翻车：队友已把它的那份藏了，此时有人中途加入 ⇒ 他的开局广播会把枪又放出来）。
            //   只有之后的 enable（= 卸载落地）才需要告诉对端"回到地上 + 落点"。
            if (!sawFirstEnable)
            {
                sawFirstEnable = true;
                return;
            }

            // ⚠ 单机 / 没进房时 OnLocalStateChanged 会自己早退，这里不用判。
            // ★ 落点 + **弹药**一起发：两端各一份实例，本端手里这份才是权威值
            //   （对端那份从没被开过枪）⇒ 不带上弹量，队友捡起来就是满弹。
            ReportState(StateOnGround, FormatGroundState());
        }

        protected override void OnDisable()
        {
            // 被拿走（入槽）。⚠ 换场景销毁时也会走到这里多发一条，对端当场也在销毁 ⇒ 无副作用。
            // ⚠ 弹尽被弃后的销毁不算"被拿走"：报 StateTaken 会让对端把枪挂到谁身上，两端的账就对不上了。
            if (!m_Spent) ReportState(StateTaken, TakenKey);
            base.OnDisable();   // 小地图标记由基类注销（与 OnEnable 成对）
        }

        /// <summary>
        /// 远端状态（走 4059 的 StateKey 分支，**不重放** <see cref="Operate"/>）：
        /// <c>StateTaken</c> = 挂到拿走它的那个盟友手上（拿不到盟友时退回"本端这份收起"）；
        /// <c>StateOnGround</c> = 从盟友手里摘下、复位到 StateKey 里的落点；
        /// <c>StateSpent</c> = 他那把打空被弃 ⇒ 本端这份跟着 <see cref="MarkSpent"/>。
        /// </summary>
        /// <param name="remoteUser">拿走的那个盟友（4059 的 <c>Sid</c> 解析结果；解析不到为 null）</param>
        public override void ApplyRemoteState(int code, string key, GameObject remoteUser)
        {
            // 本端也正拿着它（两端几乎同时去捡的竞态）⇒ 不跟着远端把手里这把收掉 / 挪走
            if (inOperate) return;

            if (code == StateSpent)
            {
                // 队友那把支援武器打空被自动丢弃：本端这份镜像（可能正挂在他手上）跟着消失。
                // ⚠ 两端各一份实例，各删各的，只同步"该删了"这件事。
                DetachFromCarrier();
                MarkSpent();
                return;
            }

            if (code == StateTaken)
            {
                // ★ 以前这里只 SetActive(false)：盟友手上没有这把枪 ⇒ 他切到支援槽看不见枪、
                //   开火在本端也毫无表现（FriendWeaponView.PlayShoot 会打"当前槽没有武器实例"）。
                //   现在把**这一份**挂进他的表现槽：两端各有一份同位置的世界物件，不用再同步"我捡了哪把枪"。
                var view = remoteUser != null ? remoteUser.GetComponentInChildren<FriendWeaponView>(true) : null;
                if (view != null && m_Weapon != null && view.AttachEquippedWeapon(m_Weapon))
                {
                    if (!gameObject.activeSelf) gameObject.SetActive(true);
                    listBySyncId[SyncId] = this;   // 他丢下时那条 StateOnGround 还要能找到这里
                    return;
                }

                // 拿不到盟友（单机 / 键对不上的那条消息）⇒ 退回旧行为：本端这份收起
                if (!gameObject.activeSelf) return;
                gameObject.SetActive(false);
                // ⚠ base.OnDisable 会把本家具从 listBySyncId 摘掉 ⇒ 之后队友丢下时那条 StateOnGround 在桥上
                //   FindBySyncId 找不到人、会被整条丢弃，枪在远端永远不回来。这里把它补回键表：
                //   交互表 list 保持移除（物体已不激活，本来就扫不到），只有"能被找回"这一件事要留着。
                listBySyncId[SyncId] = this;
                return;
            }

            if (!TryParseGroundState(key, out Vector3 pos, out int magazine, out int ammo)) return;

            // 被他丢下了：先从盟友手上摘掉再摆回落点（否则会跟着人走，且模型还是收起的）
            DetachFromCarrier();
            transform.position = pos;
            if (m_Weapon != null) m_Weapon.ApplyAmmoForSync(magazine, ammo);
            if (!gameObject.activeSelf) gameObject.SetActive(true);   // 重新激活会走 OnEnable ⇒ 重新注册交互与标记
            // 走"挂到盟友手上"那条分支时 GO 一直没关、只有本组件被关掉了 ⇒ 这里补开（回环由 NetApplyGuard 挡）
            if (!enabled) enabled = true;
        }

        /// <summary>从当前携带者（盟友）的槽位里摘掉本武器；没人携带就是空操作。</summary>
        private void DetachFromCarrier()
        {
            if (m_Weapon == null) return;

            var view = GetComponentInParent<FriendWeaponView>(true);
            if (view == null) return;

            view.DetachEquippedWeapon(m_Weapon);
            transform.SetParent(null, true);   // 回到世界根：紧接着那条落点才有意义
        }

        /// <summary>落点 + 弹量编码成 "x,y,z,弹匣,备弹"（固定 InvariantCulture，别让小数逗号跟着系统区域走）。</summary>
        private string FormatGroundState()
        {
            Vector3 p = transform.position;
            m_Weapon.GetAmmoForSync(out int magazine, out int ammo);
            return string.Format(CultureInfo.InvariantCulture, "{0:F2},{1:F2},{2:F2},{3},{4}",
                p.x, p.y, p.z, magazine, ammo);
        }

        /// <summary>解析 <see cref="FormatGroundState"/> 的产物（只在状态消息里调用，频率极低，不在热路径）。</summary>
        private static bool TryParseGroundState(string key, out Vector3 pos, out int magazine, out int ammo)
        {
            pos = default;
            magazine = -1;
            ammo = -1;
            if (string.IsNullOrEmpty(key)) return false;

            string[] parts = key.Split(',');
            if (parts.Length != 5) return false;
            if (!float.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out float x)) return false;
            if (!float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float y)) return false;
            if (!float.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out float z)) return false;
            if (!int.TryParse(parts[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out magazine)) return false;
            if (!int.TryParse(parts[4], NumberStyles.Integer, CultureInfo.InvariantCulture, out ammo)) return false;

            pos = new Vector3(x, y, z);
            return true;
        }

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
                user.TryGetComponent(out PlayerWeaponsManager wm);
                // 拾取：若支援槽已有武器，先卸下手里的（触发其 OnUninstall 落地）再换上捡起的
                if (wm != null)
                {
                    int supportSlot = PlayerWeaponsManager.SlotOf(WeaponTypeEnum.Support);
                    var oldWeapon = wm.GetWeaponAtSlotIndex(supportSlot);
                    if (oldWeapon != null && oldWeapon != m_Weapon)
                    {
                        // 替换式卸载：紧接着 InstallEquip 会播"安装"语音，两条连着播会打架
                        equipController.UninstallEquip(oldWeapon, silent: true);
                    }
                }

                equipController.InstallEquip(m_Weapon, this);
                inOperate = true;
                // 入槽后由 EquipGroundWeapon 禁用本组件

                // ★ 拾取后自动切到这把支援武器。EquipGroundWeapon 只在"手上没有任何武器"时才自动切，
                //   常规（手里有枪）必须显式切一次；已切过就别再 force 一遍（会重放一次收/拔枪动画）
                int slot = PlayerWeaponsManager.SlotOf(WeaponTypeEnum.Support);
                if (wm != null && wm.GetWeaponAtSlotIndex(slot) == m_Weapon && wm.ActiveWeaponIndex != slot)
                {
                    wm.SwitchToWeaponIndex(slot, force: true, allowDual: false);
                }
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
            // ⚠ 弹尽被弃的那把不能再复位成"可拾取"：卸载流程（OnUninstall）落地时照样会调到这里
            if (m_Spent) return;

            canOperate = true;
            inOperate = false;
            owner = null;
            pressTime = 0;
        }

        /// <summary>
        /// 子弹打空（<c>WeaponFlag.AutoDeath</c>）：不再可交互、从小地图消失，<see cref="SpentDestroyDelay"/> 秒后销毁。
        /// <para>▍调用点两处：<c>WeaponPlayerController</c> 打空自动丢弃时（本端真持有者）；
        /// 对端收到 <c>StateSpent</c> 时（见 <see cref="ApplyRemoteState"/>）。</para>
        /// <para>▍⚠ 必须在"卸载落地"**之前**调：<c>OnUninstall</c> 会把本组件 re-enable 并复位，
        /// 先置位才能让 <see cref="ResetForPickup"/> / <see cref="OnEnable"/> 跳过那套"重新可捡"的状态。</para>
        /// </summary>
        public void MarkSpent()
        {
            if (m_Spent) return;
            m_Spent = true;
            canOperate = false;
            inOperate = false;
            owner = null;
            RefreshMapMark();                 // 被携带时本组件是禁用的，这一步其实是空操作；这里只为覆盖"就地打空"的路径
            ReportState(StateSpent, SpentKey);
            Destroy(gameObject, SpentDestroyDelay);
        }
    }
}
