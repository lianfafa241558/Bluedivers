using FPSGame.Gameplay;
using FPSGame.Net;
using UnityEngine;

namespace FPSGame.Managers
{
    /// <summary>
    /// 联机 · **玩家状态类**桥（09）：两条"跟人绑定"的同步，键都是 **sid**（不是家具/敌人的确定性键）。
    ///
    /// <list type="number">
    ///   <item><b>扶起队友</b>：盟友实例身上的 <c>Furniture_PlayerDown</c> 被操作 ⇒ 按 sid 让**他本机**复活
    ///         （<c>PlayerReviveNtf</c>(4050)。⚠ 不能蹭家具那条 4036：<c>SyncId</c> 含位置量化，而盟友出生点与
    ///         本人出生点差 0.2m(Y) ⇒ 两端键必然对不上，那条会被"本端找不到这个家具"丢掉）；</item>
    ///   <item><b>背包武器开火</b>：<c>WeaponBag</c> 不在 <c>PlayerWeaponsManager</c> 的槽位里 ⇒ 那条
    ///         <c>OnShoot</c> 永远不响，这里用 <c>BattleEventBus.OnBagShoot</c> 补一条（复用 4015/4016，
    ///         槽位用虚拟值 <see cref="BagSlotIndex"/>）。</item>
    /// </list>
    ///
    /// <para>▍为什么是独立一个类：它是 <c>NetFriendBridge</c> 的姊妹职责，而那条桥已经很长；本类靠
    /// <c>[RuntimeInitializeOnLoadMethod]</c> 自举常驻（与 <c>SceneUnitMoveSink</c> 同款）⇒ 不需要宿主，
    /// 也不需要任何 Install/Uninstall 调用点。</para>
    /// </summary>
    public static class NetPlayerStateBridge
    {
        /// <summary>**虚拟槽位**：背包武器开火（真实槽位只有 0~6，见 <c>PlayerWeaponsManager.SlotOf</c>）。</summary>
        public const int BagSlotIndex = 7;

        static bool installed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap()
        {
            if (installed) return;
            installed = true;

            GlobalEventBus.OnFurnitureOperate += OnLocalFurnitureOperate;   // 本端操作了"某人倒地"的交互
            BattleEventBus.OnBagShoot += OnLocalBagShoot;                   // 本端背包武器开火
            NetRoomFlow.OnPlayerRevive += OnPlayerReviveMsg;                // 收到"某人被扶起"
        }

        // ==================== 扶起队友（4050） ====================

        /// <summary>本端操作了盟友实例的"倒地"交互 ⇒ 按 sid 请他本机复活（本地这一下只改盟友的镜像状态）。</summary>
        static void OnLocalFurnitureOperate(GameObject user, IFurniture furn)
        {
            if (!(furn is Furniture_PlayerDown down)) return;

            // ⚠ 只认"盟友实例身上"的那一份（本机玩家自己身上也有一份）；判据 = 家具所在物体挂着 FriendController
            var fc = down.GetComponent<FriendController>();
            if (fc == null) return;

            var flow = NetRoomFlow.Instance;
            if (flow == null) return;

            FPSGame.Utils.NetSyncLog.SyncLog("救人·发", $"sid={fc.NetSid}（操作盟友倒地实例 ⇒ 4050；不走 4036，键对不上）");
            flow.SendPlayerRevive(fc.NetSid);
        }

        /// <summary>
        /// 收到"某人被扶起"。
        /// <para>▍房主收到成员的请求 ⇒ 转发全体，若被救的是房主自己则本地应用；成员只应用（sid 必然是自己，
        /// 别人的倒地由他自己的机器处理）。</para>
        /// </summary>
        static void OnPlayerReviveMsg(uint sid)
        {
            var flow = NetRoomFlow.Instance;
            if (flow == null) return;

            if (flow.IsHost) flow.SendPlayerRevive(sid);   // 只广播成员（本地应用就在下面，不做自派发）

            if (sid != flow.SelfSid) return;

            var self = FPSGame.Game.ActorsManager.Player as Component;
            if (self == null) return;

            var down = self.GetComponent<Furniture_PlayerDown>();
            if (down != null)
            {
                down.ReviveSelf();
            }
            else
            {
                var hp = self.GetComponent<FPSGame.Game.Health>();
                if (hp != null) hp.Revive();
            }

            FPSGame.Utils.NetSyncLog.SyncLog("救人·收", $"sid={sid} 本端被救 ⇒ 已本地复活");
        }

        // ==================== 背包武器开火（4015/4016 + 虚拟槽位） ====================

        /// <summary>本机背包武器开火 ⇒ 上报（对端用盟友身上那把背包武器播表现；伤害仍在本机结算）。</summary>
        static void OnLocalBagShoot(GameObject owner, Vector3 direction, int damageIndex)
        {
            if (owner == null) return;

            var self = FPSGame.Game.ActorsManager.Player;
            if (self == null || self.gameObject != owner) return;   // 只报"我的"（盟友实例那把永不本机开火）

            var flow = NetRoomFlow.Instance;
            if (flow == null) return;

            FPSGame.Utils.NetSyncLog.BulletLog("背包开火·发", $"方向={direction:F2} 档位={damageIndex} ⇒ 虚拟槽位={BagSlotIndex}");
            flow.SendShoot(BagSlotIndex, direction, default, damageIndex, 0);
        }
    }
}
