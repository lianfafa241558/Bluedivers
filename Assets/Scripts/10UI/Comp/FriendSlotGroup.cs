using System.Collections.Generic;
using FPSGame.Core.Interface;
using FPSGame.Game;
using FPSGame.Gameplay;
using FPSGame.Managers;
using FPSGame.Utils;
using UnityEngine;
using static FPSGame.WndTools.WndRootTool;

namespace FPSGame.UI
{
    /// <summary>
    /// 「队友位」组视图：驱动一组"有人 / 空位 + 头像 + 边框色 + 名字"的槽位。
    ///
    /// <para>▍挂哪里：挂在 <c>FriendRoot</c> 节点上。目前两处**同构**共用它 ——
    /// <c>BridgeWnd/PlayerStateSelf/FriendRoot</c>（舰桥队友位，**勾** <see cref="hideWhenOffline"/>）
    /// 与 <c>SettingWnd/ExpandRoot/stateWnd/PlayerStateSelf/FriendRoot</c>（设置窗口的玩家位，
    /// **不勾**：那里的"空位"是"创建房间"入口，单机时正是要显示的东西）。</para>
    ///
    /// <para>▍槽位节点命名（2026-10-09 已在两份预制体里规范化）：
    /// <c>PlayerState(n)/Occupied/{Icon/{BG, portrait}, frame, name}</c> = 有人（默认 inactive）；
    /// <c>PlayerState(n)/Empty/{frame, Shadow/name}</c> = 空位（默认 active）。
    /// ⚠ 规范化**之前**"头像 / 六边形边框 / 空位框"**都叫 `Image`** ⇒ 本类按名取并**保留下标兜底**，
    /// 兼容被回退到改名前的老资产。</para>
    ///
    /// <para>▍数据源 = **场上的盟友实体**（<c>ActorsManager.Players</c> 里带 <see cref="FriendController"/> 的项）：
    /// 与"盟友状态行"同一套口径 ⇒ UI 不依赖网络层（<c>10_UI</c> 看不见 <c>02_Net</c>），
    /// 房主 / 成员同一份代码，也不用管"名单消息什么时候到"。</para>
    ///
    /// <para>▍每帧对账、**刻意不做"变了才重填"的签名缓存**：头像/颜色是模型**异步挂载**之后才有的，
    /// 只按 sid 判"没变就跳过"会让第一帧填进去的空头像永远留着；3 个槽的开销可忽略。</para>
    /// </summary>
    [AddComponentMenu("UI/组件/队友位组")]
    public class FriendSlotGroup : MonoBehaviour
    {
        /// <summary>不在联机房间（单机）时**整块隐藏**。
        /// <para>⚠ 默认**不勾**：设置窗口的玩家位要在单机时照样显示"空位"（那是"创建房间"的入口）。</para></summary>
        [InspectorName("不在房间时整块隐藏")]
        [Tooltip("勾选：不在联机房间（单机）时隐藏所有队友位。舰桥队友位要勾；设置窗口的玩家位不要勾")]
        [SerializeField] private bool hideWhenOffline;

        /// <summary>一条队友位（节点引用一次性缓存）。</summary>
        private class Slot
        {
            /// <summary>`PlayerState(n)`（整格；"整块隐藏"就是把它关掉）。</summary>
            public Transform Root;
            /// <summary>`Occupied`：有玩家（头像 + 边框 + 名字）。</summary>
            public GameObject Occupied;
            /// <summary>`Empty`：空位（显示「空位」）。</summary>
            public GameObject Empty;
            /// <summary>`Occupied/Icon/portrait`（头像）。</summary>
            public Transform Portrait;
            /// <summary>`Occupied/frame`（六边形边框）。</summary>
            public Transform Frame;
            /// <summary>`Occupied/name`。</summary>
            public Transform Name;
        }

        private Slot[] _slots;

        /// <summary>本帧收集到的队友（按 <c>NetSid</c> 升序，避免行序每次重组乱跳）。</summary>
        private readonly List<FriendController> _friends = new List<FriendController>();

        /// <summary>当前显示出来的队友数（= 场上盟友数，封顶到槽位数）。</summary>
        public int FriendCount { get; private set; }

        private void OnEnable()
        {
            Refresh();
        }

        private void Update()
        {
            Refresh();
        }

        /// <summary>
        /// 对账一次：整块显隐（可选）→ 逐槽切换"有人 / 空位" → 填头像、边框色、名字。
        /// <para>▍由 <c>OnEnable</c> / <c>Update</c> 自动调用；窗口想在显示时立刻刷新也可以自己调一次。</para>
        /// </summary>
        public void Refresh()
        {
            EnsureSlots();

            if (hideWhenOffline && !BridgeSys.InOnlineRoom)
            {
                // ⚠ **不能关自己**：本组件的 Update 就在这个物体上，关掉自己 = 再也没有人把它打开
                //   （Update 不跑就没机会判断"已经回到房间了"）。改成关掉**每个槽** —— `FriendRoot` 本身
                //   没有 Image / LayoutGroup，只当容器用 ⇒ 视觉上等价于"整块隐藏"，而且驱动还能继续跑。
                HideAllSlots();
                FriendCount = 0;
                return;
            }

            CollectFriends();

            for (int i = 0; i < _slots.Length; ++i)
            {
                var slot = _slots[i];
                bool has = i < _friends.Count;

                SetActive(slot.Root, true);
                SetActive(slot.Occupied, has);
                SetActive(slot.Empty, !has);
                if (!has) continue;

                var fc = _friends[i];
                var actor = fc.GetComponent<Actor>();          // 与 FriendController 同物体（05_UnitCore）

                SetSprite(slot.Portrait, actor != null ? actor.Portrait : null);
                SetColor(slot.Frame, actor != null ? actor.Color : Color.white);

                // 显示名：盟友的 `Actor.ShowName` **刻意没搬**（见 FriendController.AttachModel：盟友用玩家名）
                // ⇒ 优先取 FriendController 里的玩家名，实在没有才退角色的 ShowName。
                string name = fc.PlayerName;
                if (string.IsNullOrEmpty(name) && actor != null) name = actor.ShowName;
                SetText(slot.Name, name);
            }

            FriendCount = Mathf.Min(_friends.Count, _slots.Length);
        }

        private void HideAllSlots()
        {
            for (int i = 0; i < _slots.Length; ++i) SetActive(_slots[i].Root, false);
        }

        /// <summary>缓存槽位（只做一次）；节点名见类注释，找不到名字时退回下标（兼容改名前的老资产）。</summary>
        private void EnsureSlots()
        {
            if (_slots != null) return;

            var list = new List<Slot>();
            for (int i = 0; i < transform.childCount; ++i)
            {
                var slot = transform.GetChild(i);
                if (slot.childCount < 2) continue;             // 约定：子0 = 有人、子1 = 空位

                var occupied = slot.Find("Occupied");
                if (occupied == null) occupied = slot.GetChild(0);

                var empty = slot.Find("Empty");
                if (empty == null) empty = slot.GetChild(1);

                var portrait = occupied.Find("Icon/portrait");
                if (portrait == null && occupied.childCount > 0 && occupied.GetChild(0).childCount > 1)
                    portrait = occupied.GetChild(0).GetChild(1);   // 旧资产：遮罩的子1 = 头像（子0 是底图）

                var frame = occupied.Find("frame");
                if (frame == null && occupied.childCount > 1) frame = occupied.GetChild(1);

                var name = occupied.Find("name");
                if (name == null && occupied.childCount > 2) name = occupied.GetChild(2);

                list.Add(new Slot
                {
                    Root = slot,
                    Occupied = occupied.gameObject,
                    Empty = empty.gameObject,
                    Portrait = portrait,
                    Frame = frame,
                    Name = name,
                });
            }
            _slots = list.ToArray();
        }

        /// <summary>
        /// 收集队友实体（只认带 <see cref="FriendController"/> 的，即"别人"；本机玩家不在 <c>ActorsManager.Players</c> 的这批里）。
        /// ⚠ 盟友离场时 <c>ActorsManager.Players</c> 里可能残留**已销毁**的引用 ⇒ 必须跳过无效项。
        /// </summary>
        private void CollectFriends()
        {
            _friends.Clear();

            var actors = ActorsManager.Players;
            for (int i = 0; i < actors.Count; ++i)
            {
                var actor = actors[i];
                if (actor == null || !actor.IsValidMono()) continue;   // 接口引用不能用 == null 判活

                if (actor is Component comp)
                {
                    var fc = comp.GetComponent<FriendController>();
                    if (fc != null) _friends.Add(fc);
                }
            }

            _friends.Sort(CompareBySid);
        }

        private static int CompareBySid(FriendController a, FriendController b) => a.NetSid.CompareTo(b.NetSid);
    }
}
