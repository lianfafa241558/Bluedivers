using System.Collections.Generic;
using System.Linq;
using FPSGame.Core;
using UnityEngine;
using FPSGame.Utils;
using FPSGame.Gameplay;

namespace FPSGame.UI
{
    using static FPSGame.WndTools.WndRootTool;
    using FPSGame.Data;
    using FPSGame.Managers;
    using FPSGame.GameData;
    /// <summary>
    /// 战备配置界面
    /// armamentRoot下的0-3是每个玩家的界面根
    /// armamentRoot.GetChild(i, 2)下是全部战备的列表
    /// armamentRoot.GetChild(i, 3)下是玩家信息(等级，名称等)
    /// armamentRoot.GetChild(i, 4)下是准备按钮
    /// armamentRoot.GetChild(i, 5)下是已选择的战备按钮，0-3是战备，4是全队强化(还没弄好)
    /// armamentRoot.GetChild(i, 6)下是任务赠送的额外战备
    /// armamentRoot.GetChild(i, 7)是选择第几个战备显示的框体
    /// armamentRoot.GetChild(i, 8)下是全部强化的列表
    /// </summary>
    [AddComponentMenu("UI/窗口/武装选择")]
    public class ArmamentWnd : Window, FPSGame.GameContract.IBridgeArmamentSink
    {
        public Transform mapName,enemyIcon, enemyName;

        public GameObject prefab,buttonPrefab,boosterButtonPrefab;
        public Transform armamentRoot;

        /// <summary>
        /// 每行一个**独立**渲染目标（`Assets/Images/PlayerRole1..4`）。行内角色由行自己那个相机渲到 RT 上显示，
        /// 而 <c>ArmamentItem</c> 预制体里写死的是 <c>PlayerRole1</c> ⇒ 不按行配就会 4 行共用一张、全行同一张脸
        /// （名字/等级/小头像各自来自正确模型，所以只有"大模型"看着不对，1 个玩家时看不出来）。
        /// </summary>
        public RenderTexture[] roleViews;

        public Transform tipRoot,frame;

        private bool[] ready;
        private Animator[] animators;
        /// <summary> 当前选择配置的战备位置</summary>
        private int selectAirdropIndex=-1;
        /// <summary> 全队强化面板是否展开</summary>
        private bool boosterExpanded;

        private Transform showSelect;
        private Dictionary<Transform, int> buttons;
        /// <summary> 全队强化 id -> 对应按钮（用于禁止重复选择时置灰）</summary>
        private Dictionary<int, Transform> boosterButtons;

        public void Init()
        {
            BridgeSys.Instance.armament = this;
        }

        protected override void FirstShowWnd()
        {
            int count = Constants.MaxPlayer;
            ready = new bool[count];
            animators = new Animator[count];
            buttons = new();
            boosterButtons = new();
            for (int i=0;i< count; ++i)
            {
                var go = Instantiate(prefab, armamentRoot).transform;
                var a = i;
                if (i == 0)//自己在显示上面固定第一个
                {
                    showSelect = go.Find("ShowSelect");
                    //装备
                    SetCilck(go.GetChild(4), () => {
                        SendPlayerReady(teamManager.SelfIndex, !ready[a]);
                        wndManager.PlaySound(new("Bridge/"+(!ready[a]? "Ready" : "CancelReady")));
                    });
                    for (int u = 0; u < 4; ++u)
                    {
                        var b = u;
                        //战备
                        SetCilck(go.GetChild(5, u), () => {
                            if (SelfReady) return;      // ★ 已就绪 ⇒ 锁住战备（要改先取消就绪）
                            if (selectAirdropIndex == -1)
                            {
                                go.GetComponent<Animator>().SetBool("Airdrop",true);
                                wndManager.PlaySound(new("Bridge/Expand"));
                                InputManager.AddListenerCancel(Cancel);
                            }
                            SetSelectIndex(b, true);
                        });
                    }
                    //全队强化槽位（index 4）
                    SetCilck(go.GetChild(5, 4), () => {
                        if (SelfReady) return;      // ★ 同战备：已就绪时锁住
                        ToggleBooster();
                    });
                }

                // 槽位固定 MaxPlayer 个，但房间里可能没这么多人 ⇒ **没有真人的槽位不要读 players**
                //（越界就是 ArgumentOutOfRangeException：2026-10-07 实测，撤离结束回战备时 1 人局炸在这）
                if (i < teamManager.players.Count) ApplyRowRequiredAD(armamentRoot.GetChild(i), teamManager.players[i]);

                for (int u = 0; u < 5; ++u)
                {
                    SetButton(armamentRoot.GetChild(i, 5, u), tipRoot,u==4, ShowTip);
                }

            }

            CaptureSlotEmptyColors();
            InitPlayerAirdrop();
            InitBoosterButtons();
        }


        private void SetSelectIndex(int index,bool showFrame)
        {
            selectAirdropIndex = index;
  
            if (showFrame)
            {
                if (index >-1)
                {
                    showSelect.position = armamentRoot.GetChild(0, 5, index).position;
                    showSelect.SetParent(armamentRoot.GetChild(0, 5, index));
                    showSelect.localScale = Vector3.one;
                    SetActive(showSelect, true);
                }
                else
                {
                    SetActive(showSelect, false);
                }
            }
        }

        protected override void ShowWnd()
        {
            ready.Clear();
            animators.Clear();
            SetSelectIndex(-1, true);

            WindowState = WindowStateEnum.UI;
            GetComponent<Animator>().Play("Entry");
            SetColor(enemyIcon, taskManager.nowTask.campData.Color);
            SetSprite(enemyIcon, taskManager.nowTask.campData.Sprite);
            SetText(mapName, taskManager.MapId);
            SetText(enemyName, "" + taskManager.nowTask.campData.ShowName + "控制");
            EnsureRoleViews();
            m_RowRole = null;                        // 每次打开都重挂模型（保持原来"打开即刷新"的行为）
            for (int i = 0; i < armamentRoot.childCount; ++i)
            {
                var item = armamentRoot.GetChild(i);
                if (i < teamManager.players.Count)
                {
                    var data = teamManager.players[i];
                    data.airdrop = new int[4];
                    data.boosterId = 0;              // 全队强化槽位：本局恢复为未选择
                    SetActive(item, true);
                    //重置全队强化展开状态与动画
                    item.GetComponent<Animator>().SetBool("Booster",false);
                    boosterExpanded = false;

                    BindRowIdentity(item, i, data);
                    ApplyRowAirdrop(item, i, data);  // 数据刚被重置 ⇒ 等于把 4 个战备格与强化格清空
                    ApplyRowReady(item, i, data);    // 就绪状态按数据渲染（事件只在变化时推一次，窗口后开会漏）
                }
                else
                {
                    SetActive(item, false);
                }

            }

        }
        #region 行内角色模型 → 渲染目标（每行一张 RT）

        /// <summary>行 → 该行专属渲染目标。首次解析后固定：兜底克隆出来的那几张由本窗口持有（窗口与行都常驻）。</summary>
        private RenderTexture[] m_RowViews;

        /// <summary>解析并缓存每行的渲染目标；万一预制体把同一张 RT 配给了多行，给后一行克隆一张同规格的兜底
        /// （克隆不释放：数量 ≤ 行数且只在首次解析时产生）。</summary>
        private void EnsureRoleViews()
        {
            int count = armamentRoot != null ? armamentRoot.childCount : 0;
            if (count == 0 || (m_RowViews != null && m_RowViews.Length == count)) return;

            m_RowViews = new RenderTexture[count];
            var used = new HashSet<RenderTexture>();
            for (int i = 0; i < count; ++i)
            {
                var src = roleViews != null && i < roleViews.Length ? roleViews[i] : null;
                if (src == null) continue;                  // 没配 ⇒ 保持预制体原样（单行场景仍然能用）

                if (!used.Add(src))
                {
                    var clone = new RenderTexture(src) { name = src.name + "_row" + i };
                    clone.Create();
                    m_RowViews[i] = clone;
                    continue;
                }
                m_RowViews[i] = src;
            }
        }

        /// <summary>把某一行绑到它自己的渲染目标上（RawImage 与它身上那个相机必须指向同一张）。</summary>
        private void BindRoleView(Transform modelRoot, int row)
        {
            if (modelRoot == null || m_RowViews == null || row < 0 || row >= m_RowViews.Length) return;

            var rt = m_RowViews[row];
            if (rt == null) return;

            var raw = modelRoot.GetComponent<UnityEngine.UI.RawImage>();
            if (raw != null) raw.texture = rt;

            var cam = modelRoot.GetComponent<Camera>();
            if (cam != null) cam.targetTexture = rt;
        }

        #endregion

        #region 名册变化 ⇒ 行刷新（有人退出，那一行必须消失）

        /// <summary>行 → 该行当前挂着的角色 id（"角色没变就别重挂模型"用）。null = 需要重挂。</summary>
        private string[] m_RowRole;

        /// <summary>槽位"空"时的底色：首次见到时从预制体抓一次（别写死白色，观感归作者）。</summary>
        private readonly Dictionary<Transform, Color> m_SlotEmptyColor = new Dictionary<Transform, Color>();

        /// <summary>【桥调用】名册变化（有人加入/退出）⇒ 刷新槽位。</summary>
        public void ReceiveRosterChanged() => RefreshPlayerRows();

        /// <summary>
        /// 名册变化时刷新：多余的槽位隐藏、在座的行按数据重填。
        ///
        /// <para>▍为什么需要：<see cref="ShowWnd"/> 只在窗口显示那一刻跑一次 —— 别人中途退出时那一行会一直留着
        /// （旧模型/旧名字/旧战备图标都在）。⚠ 名册重排会让下标漂移（`players[0]` 恒为自己）⇒ 不能只隐藏尾巴，得整行按数据回填。</para>
        /// </summary>
        private void RefreshPlayerRows()
        {
            if (armamentRoot == null || ready == null) return;   // 窗口还没显示过 ⇒ ShowWnd 会一次性建好

            for (int i = 0; i < armamentRoot.childCount; ++i)
            {
                var item = armamentRoot.GetChild(i);
                if (i >= teamManager.players.Count)
                {
                    SetActive(item, false);
                    continue;
                }

                SetActive(item, true);
                var data = teamManager.players[i];
                BindRowIdentity(item, i, data);
                ApplyRowAirdrop(item, i, data);
                ApplyRowRequiredAD(item, data);
                ApplyRowReady(item, i, data);
            }
        }

        /// <summary>把第 i 行挂成 <paramref name="data"/> 这个玩家（角色模型 + 名字 + 等级 + 小头像）。
        /// 同一个角色会跳过重挂（模型自己带着 ShowName/Portrait，不必重复 Instantiate）。</summary>
        private void BindRowIdentity(Transform item, int i, PlayerData data)
        {
            if (m_RowRole == null || m_RowRole.Length != armamentRoot.childCount) m_RowRole = new string[armamentRoot.childCount];

            // ⚠ 这里只能用 Unity 的 `!= null`（销毁过的对象会判成 false），不能用 IsValidMono
            //   —— 那个扩展挂在 IMonoVaild 上，Animator 不实现它（编译不过）
            var old = animators[i];
            if (m_RowRole[i] == data.roleName && old != null) return;
            m_RowRole[i] = data.roleName;

            var modelRoot = item.GetChild(1);
            // 清掉上一轮遗留的模型：本方法与 ShowWnd 都会重挂，不清就是叠罗汉（旧角色盖在前面）
            for (int k = modelRoot.childCount - 1; k >= 0; --k) Tool.Destroy(modelRoot.GetChild(k).gameObject);

            // ⚠ 角色还没定（名册刚重建 / 资料还没到 / 已退出的人）⇒ **不许**拼 "Prefabs/StudentModle/" + 空串：
            //   CreatPrefab 会报"没有找到…"并返回 null，紧接着 .transform / .GetComponent 就 NRE
            //   （2026-10-07 实测：撤离回大厅时窗口被刷一次，整条链崩在这）。
            //   ⚠ 也别把这种行记成"已绑定"（m_RowRole 已在上面写过 ⇒ 角色定下来后值不同，会重新走这里）。
            if (string.IsNullOrEmpty(data.roleName))
            {
                ClearRowIdentity(item);
                return;
            }

            var showModle = resManager.CreatPrefab("Prefabs/StudentModle/" + data.roleName, false);
            if (showModle == null)      // 资源真缺（角色名与预制体对不上）⇒ 同样不能继续
            {
                Debug.LogWarning($"[ArmamentWnd] 角色模型缺失：Prefabs/StudentModle/{data.roleName}（第 {i} 行留空）");
                ClearRowIdentity(item);
                return;
            }

            showModle.transform.parent = modelRoot;
            showModle.transform.localPosition = new(0, -2.4f, 980);
            showModle.transform.eulerAngles = new(0, 180, 0);
            showModle.transform.localScale = new(2, 2, 2);
            showModle.SetChildLayer(gameObject.layer, 3);
            BindRoleView(modelRoot, i);   // ★ 这一行用它自己的 RT（否则 4 行共用 PlayerRole1 ⇒ 全行同一张脸）
            foreach (var script in showModle.GetComponents<MonoBehaviour>()) script.enabled = false;   // 关闭注视等组件

            var animator = showModle.GetComponent<Animator>();
            animators[i] = animator;
            var bo = animator != null ? animator.GetComponent<BaseObject>() : null;   // 预制体上没挂 Animator/BaseObject 也不许崩
            SetText(item.GetChild(3, 0, 0), bo != null ? bo.ShowName : "");
            SetText(item.GetChild(3, 0, 1), data.roleLevel);
            SetSprite(item.GetChild(3, 1, 1), bo != null ? bo.Portrait : null);
        }

        /// <summary>把一行清成"没有身份"：模型已由调用方清空，这里只擦名字/等级/头像。</summary>
        private static void ClearRowIdentity(Transform item)
        {
            SetText(item.GetChild(3, 0, 0), "");
            SetText(item.GetChild(3, 0, 1), "");
            SetSprite(item.GetChild(3, 1, 1), null);
        }

        /// <summary>
        /// 回填这一行的"任务所需战备"（`ExtraAirdropRoot` 那 7 个小格）= 任务级 + **他自己角色**的默认战备。
        ///
        /// <para>▍为什么按行算：原先每行都读本机 `nowTask.RequiredAD`（= 任务级 + **本机角色**默认）⇒ 别人那一行
        /// 显示的是我的角色默认（2026-10-07 两人实测）。</para>
        /// </summary>
        private void ApplyRowRequiredAD(Transform item, PlayerData data)
        {
            var requiredAD = taskManager.RequiredADOf(data != null ? data.roleName : null);
            for (int u = 0, y = 0; u < 7; ++u)
            {
                // 超过 7 个的丢弃（格子就那么多个）；isHide 的不占格
                if (u < requiredAD.Count && ResSvc.airdropDic.TryGetValue(requiredAD[u], out var ad) && !ad.isHide)
                {
                    buttons[item.GetChild(6, y)] = requiredAD[u];
                    SetButton(item.GetChild(6, y), tipRoot, false, ShowTip);
                    SetSprite(item.GetChild(6, y, 0, 0), ad.icon);
                    SetColor(item.GetChild(6, y, 0, 0), ad.IconColor);
                    SetActive(item.GetChild(6, y, 0), true);
                    ++y;
                    continue;
                }
                SetActive(item.GetChild(6, u, 0), false);
            }
        }

        /// <summary>按数据回填这一行的 4 个战备格 + 全队强化格（名册重排后下标会变，不能沿用旧图标）。</summary>
        private void ApplyRowAirdrop(Transform item, int i, PlayerData data)
        {
            for (int u = 0; u < 4; ++u)
            {
                int id = data.airdrop != null && u < data.airdrop.Length ? data.airdrop[u] : 0;
                var slot = item.GetChild(5, u);
                if (id > 0 && ResSvc.airdropDic.TryGetValue(id, out var ad))
                {
                    SetSprite(slot.GetChild(0), ad.icon);
                    SetColor(slot.GetChild(0), ad.IconColor);
                    SetColor(slot, ad.Color);
                    buttons[slot] = id;
                }
                else SetSlotEmpty(slot);
            }

            var booster = item.GetChild(5, 4);
            if (data.boosterId > 0 && ResSvc.boostDic.TryGetValue(data.boosterId, out var bo))
            {
                SetSprite(booster.GetChild(0), bo.icon);
                SetColor(booster.GetChild(0), bo.color);
                SetColor(booster, Color.white);
                buttons[booster] = data.boosterId;
            }
            else SetSlotEmpty(booster);
        }

        /// <summary>把一个槽位恢复成"空"（图标清空、底色还原成预制体的原色、摘掉提示绑定）。</summary>
        private void SetSlotEmpty(Transform slot)
        {
            // 兜底：万一没抓到过（行是后来才建的）
            if (!m_SlotEmptyColor.ContainsKey(slot) && slot.TryGetComponent<UnityEngine.UI.Image>(out var img))
                m_SlotEmptyColor[slot] = img.color;

            SetSprite(slot.GetChild(0), wndManager.empty);
            if (m_SlotEmptyColor.TryGetValue(slot, out var c)) SetColor(slot, c);
            buttons.Remove(slot);
        }

        /// <summary>行建好后抓一次各槽位的"空"底色（必须在任何战备图标写进去之前，见 <see cref="FirstShowWnd"/>）。</summary>
        private void CaptureSlotEmptyColors()
        {
            if (m_SlotEmptyColor.Count > 0 || armamentRoot == null) return;

            for (int i = 0; i < armamentRoot.childCount; ++i)
            {
                for (int u = 0; u < 5; ++u)
                {
                    var slot = armamentRoot.GetChild(i, 5, u);
                    if (slot.TryGetComponent<UnityEngine.UI.Image>(out var img)) m_SlotEmptyColor[slot] = img.color;
                }
            }
        }

        /// <summary>本机自己是否已就绪 —— 就绪后**锁住战备编辑**（要改先点取消就绪）。</summary>
        private bool SelfReady => ready != null && teamManager != null
                                  && teamManager.SelfIndex >= 0 && teamManager.SelfIndex < ready.Length
                                  && ready[teamManager.SelfIndex];

        /// <summary>
        /// 按数据回填这一行的就绪显示。
        /// <para>▍为什么不能只靠 <c>ReceivePlayerReady</c>：它是"变化事件"，只在值变化时推一次 ——
        /// 窗口若是变化之后才打开的（客机进入战备阶段），就什么都收不到（2026-10-07 实测：客机看不到主机的就绪）。</para>
        /// </summary>
        private void ApplyRowReady(Transform item, int i, PlayerData data)
        {
            bool isReady = data != null && data.isReady;
            ready[i] = isReady;     // 与事件路径共用同一个数组 ⇒ "显示"与"全员就绪判定"不会打架
            SetText(item.GetChild(4, 1), isReady ? "就绪" : "尚未就绪");
            if (animators[i] != null) animators[i].SetBool("IsReady", isReady);
        }

        /// <summary>自己就绪时收起战备/强化面板（面板上的按钮此时已锁，留着会让人以为还能改）。</summary>
        private void CollapseOnReady(bool isReady)
        {
            if (!isReady || armamentRoot == null) return;
            var selfGo = armamentRoot.GetChild(teamManager.SelfIndex);
            selfGo.GetComponent<Animator>().SetBool("Airdrop", false);
            selfGo.GetComponent<Animator>().SetBool("Booster", false);
            boosterExpanded = false;
            SetSelectIndex(-1, true);
        }

        #endregion

        /// <summary>
        /// 显示可选战备（同类型内偏好战备排在最前，并点亮预制体第 1 个子物体）
        /// </summary>
        private void InitPlayerAirdrop()
        {

            var arch = ArchivesData_SO.Current;
            var layout = armamentRoot.GetChild(teamManager.Self.index, 2, 0, 0, 0);
            var airdropList = ResSvc.airdropDic.Values
                .OrderBy(item => arch.IsAirdropPrefer(item.ID) ? 0 : 1)
                .ThenBy(item => item.ID)
                .ToList();
            for (int i = 1; i <= 7; i += 2)
            {
                var root = layout.GetChild(i);
                var type = (AirdropData_SO.AirdropType)(i / 2);
                var list = airdropList.FindAll(item=>item.type== type&& !item.isHide);
                for (int u = 0; u < list.Count; ++u)
                {
                    var button=Instantiate(buttonPrefab, root).transform;
                    SetSprite(button.GetChild(0),list[u].icon);
                    SetColor(button.GetChild(0), list[u].IconColor);
                    SetColor(button, list[u].Color);
                    //第 1 个子物体用于显示是否为偏好战备
                    if (button.childCount > 1) SetActive(button.GetChild(1), arch.IsAirdropPrefer(list[u].ID));
                    buttons.Add(button, list[u].ID);

                    SetButton(button,tipRoot,false,ShowTip);
                    SetCilck(button,() => SelectAirdrop(button));
                }
            }
        }
        private void UninitAirdrop()
        {
            var layout = armamentRoot.GetChild(0, 2, 0, 0, 0);
            for (int i = 1; i <= 7; i += 2)
            {
                var item = layout.GetChild(i);
                for (int u = item.childCount - 1; u >= 0; --u)
                {
                    Tool.Destroy(item.GetChild(u).gameObject);
                }
            }
        }
        /// <summary>
        /// 点击战备按钮
        /// </summary>
        private void SelectAirdrop(Transform button)
        {
            if (SelfReady) return;      // 同 ToggleBooster：面板可能是"就绪之前"展开的，落点上再挡一次
            SendPlayerSelectAemament(teamManager.Self.index, ResSvc.airdropDic[buttons[button]].ID, selectAirdropIndex);
        }


        private bool Cancel()
        {
            if (boosterExpanded)
            {
                wndManager.PlaySound(new("UI/UI_Button_Back"));
                armamentRoot.GetChild(teamManager.Self.index).GetComponent<Animator>().SetBool("Booster",false);
                boosterExpanded = false;
                return true;
            }
            if (selectAirdropIndex==-1) return false;
            wndManager.PlaySound(new("UI/UI_Button_Back"));
            armamentRoot.GetChild(teamManager.Self.index).GetComponent<Animator>().SetBool("Airdrop",false);
            SetSelectIndex(-1, true);
            //selectAirdropIndex = -1;
            //SetSelectIndex(-1,false);
            return true;
        }

        protected override void HideWnd()
        {
            UninitAirdrop();
            UninitBooster();
        }

        //通过动画调用
        private void FinishReady()
        {
            GameState = GameStateEnum.Transition;//会因为切状态自己关??


        }

        private void SetButton(Transform trans,Transform tip, bool isBooster, System.Action<Transform,Transform,bool> action)
        {
            var item = trans.TryGetOrAddComponent<ButtonEnterDetector>();
            item.Enter = (data) => {
                SetActive(tip, true);
                action.Invoke(trans, tip, isBooster);
            };

            item.In = (data) => {
                tip.position = UICamera.uiCamera.ScreenToWorldPoint(Input.mousePosition)+100*Vector3.forward;
                //tip.position =new(Input.mousePosition.x, Input.mousePosition.y,tip.position.z);
            };
            item.Exit = (data) => {
                SetActive(tip, false);
            };

        }
        private void ShowTip(Transform trans, Transform tip,bool isBooster)
        {
            if (buttons.TryGetValue(trans, out var id))
            {
                if (isBooster)
                {
                    if (ResSvc.boostDic.TryGetValue(id, out var te))
                    {
                        SetSprite(tip.GetChild(0, 0), te.icon);
                        SetColor(tip.GetChild(0, 0), te.color);
                        SetColor(tip.GetChild(0), te.color);
                        SetText(tip.GetChild(1), te.showName);
                        SetText(tip.GetChild(2), "全队强化");
                        SetText(tip.GetChild(3), te.desc);
                        SetText(tip.GetChild(4), "");
                        SetText(tip.GetChild(5), "");
                        SetText(tip.GetChild(6), "");
                        return;
                    }
                }
                else
                {
                    if (ResSvc.airdropDic.TryGetValue(id, out var data))
                    {
                        SetSprite(tip.GetChild(0, 0), data.icon);
                        SetColor(tip.GetChild(0, 0), data.IconColor);
                        SetColor(tip.GetChild(0), data.Color);
                        SetText(tip.GetChild(1), data.showName);
                        SetText(tip.GetChild(2), data.TypeName);
                        SetText(tip.GetChild(3), data.desc);
                        SetText(tip.GetChild(4), data.AttrName);
                        SetText(tip.GetChild(5), data.AttrValue);
                        SetText(tip.GetChild(6), data.opter.OpterTMPString());
                        return;
                    }

                }

            }

            SetActive(tip, false);
        }

        /// <summary> 发送玩家选择战备的消息</summary>
        private void SendPlayerSelectAemament(int playerIndex, int id, int index)
        {
            BridgeSys.Instance.SendPlayerSelectArmament(playerIndex, id, index);
        }

        /// <summary>
        /// 点击第5槽位（全队强化）时切换展开/收起面板
        /// </summary>
        private void ToggleBooster()
        {
            if (SelfReady) return;      // 已就绪 ⇒ 不许再展开强化面板
            var selfGo = armamentRoot.GetChild(teamManager.Self.index);
            if (boosterExpanded)
            {
                wndManager.PlaySound(new("UI/UI_Button_Back"));
                selfGo.GetComponent<Animator>().SetBool("Booster", false);
                boosterExpanded = false;
            }
            else
            {
                wndManager.PlaySound(new("Bridge/Expand"));
                selfGo.GetComponent<Animator>().SetBool("Booster", true);
                InputManager.AddListenerCancel(Cancel);
                boosterExpanded = true;
            }
        }

        /// <summary>
        /// 界面打开时预创建全队强化按钮（GetChild(i,8,0,0,0,1) 即 RedList）
        /// 先清空避免重复打开叠加，随后按 ID 填充 buttonPrefab
        /// </summary>
        private void InitBoosterButtons()
        {
            var list = armamentRoot.GetChild(teamManager.Self.index, 8, 0, 0, 0, 1);
            boosterButtons.Clear();
            var boosterList = ResSvc.boostDic.Values.OrderBy(item => item.ID).ToList();
            foreach (var te in boosterList)
            {
                var button = Instantiate(boosterButtonPrefab, list).transform;
                SetSprite(button.GetChild(0), te.icon);
                buttons.Add(button, te.ID);
                boosterButtons.Add(te.ID, button);
                SetButton(button, tipRoot,true, ShowTip);
                SetCilck(button, () => SelectBooster(te.ID));
            }
            RefreshBoosterButtons();
        }

        /// <summary>
        /// 刷新自己面板里全队强化按钮的可用状态。
        /// 同一强化（id≠0）只允许一个玩家选择：已被其他玩家选中的强化按钮置灰禁用；
        /// 自己当前已选的强化按钮保持可用（允许反悔更换）。
        /// </summary>
        private void RefreshBoosterButtons()
        {
            if (boosterButtons == null) return;
            var selfIndex = teamManager.Self.index;
            // 统计其他玩家已选中的强化 id（忽略 0）
            var occupied = new HashSet<int>();
            for (int i = 0; i < teamManager.players.Count; ++i)
            {
                if (i == selfIndex) continue;
                int id = teamManager.players[i].boosterId;
                if (id > 0) occupied.Add(id);
            }
            int myBooster = teamManager.players[selfIndex].boosterId;
            foreach (var kv in boosterButtons)
            {
                // 自己已选的按钮保持可用；被其他玩家占用的才禁用
                bool interactable = kv.Key == myBooster || !occupied.Contains(kv.Key);
                SetButtonInteractable(kv.Value, interactable);
            }
        }
        private void UninitBooster()
        {
            /*
            var layout = armamentRoot.GetChild(teamManager.Self.index, 8, 0, 0, 0,1);
            for (int u = layout.childCount - 1; u >= 0; --u)
            {
                Tool.Destroy(layout.GetChild(u).gameObject);
            }*/
        }


        /// <summary> 点击某个强化按钮，选中后收起面板并广播 </summary>
        private void SelectBooster(int id)
        {
            SendPlayerSelectTeamEnhance(teamManager.Self.index, id);
            wndManager.PlaySound(new("Bridge/" + (id == 0 ? "CancelReady" : "Ready")));
            armamentRoot.GetChild(teamManager.Self.index).GetComponent<Animator>().SetBool("Booster", false);
            boosterExpanded = false;
        }

        /// <summary> 发送玩家选择全队强化的消息</summary>
        private void SendPlayerSelectTeamEnhance(int playerIndex, int id)
        {
            BridgeSys.Instance.SendPlayerSelectTeamEnhance(playerIndex, id);
        }

        /// <summary> 收到玩家选择全队强化的回调</summary>
        public void ReceivePlayerSelectTeamEnhance(int playerIndex, int id)
        {
            // 窗口还没第一次显示（buttons/armamentRoot 都是 FirstShowWnd 才建的）⇒ 只记账不动 UI。
            // 数据落点已上移到常驻的 TeamNetBridge.HandleBoosterSync，窗口显示时按表重建，不会丢。
            if (buttons == null || armamentRoot == null) return;
            if (playerIndex < 0 || playerIndex >= teamManager.players.Count)
            {
                Debug.LogError("收到玩家选择全队强化回调错误，玩家"+ playerIndex);
                return;
            }
            teamManager.players[playerIndex].boosterId = id;
            var child = armamentRoot.GetChild(playerIndex, 5, 4);
            if (ResSvc.boostDic.TryGetValue(id, out var data))
            {
                SetSprite(child.GetChild(0), data.icon);
                SetColor(child.GetChild(0), data.color);
                SetColor(child, Color.white);
            }
            else
            {
                SetSprite(child.GetChild(0), wndManager.empty);
                SetColor(child, Color.white);
            }
            // 绑定提示显示
            buttons[child] = id;
            // 刷新自己面板强化按钮的可用状态（禁止重复选择）
            RefreshBoosterButtons();
        }

 
        /// <summary> 收到玩家选择战备的回调</summary>
        public void ReceivePlayerSelectAemament(int playerIndex, int id, int index)
        {
            // 同 ReceivePlayerSelectTeamEnhance：窗口没显示过就只记账不动 UI（数据已由 TeamNetBridge 落表）
            if (buttons == null || armamentRoot == null) return;
            if(playerIndex<0||playerIndex>= teamManager.players.Count)
            {
                Debug.LogError("收到玩家选择战备的回调错误，玩家"+ playerIndex);
                return;
            }
            if (index < 0 || index >=4)
            {
                Debug.LogError("收到玩家选择战备的回调错误，战备" + index);
                return;
            }
            // 窗口显示之后才加入的玩家还没走过 ShowWnd 的初始化 ⇒ airdrop 可能为 null
            var playerData = teamManager.players[playerIndex];
            if (playerData.airdrop == null || playerData.airdrop.Length != 4) playerData.airdrop = new int[4];
            playerData.airdrop[index] = id;
            var child = armamentRoot.GetChild(playerIndex, 5, index);
            buttons[child] = id;
            SetSprite(child.GetChild(0),ResSvc.airdropDic[id].icon);
            SetColor(child.GetChild(0), ResSvc.airdropDic[id].IconColor);
            SetColor(child, ResSvc.airdropDic[id].Color);

            //SetButtonInteractable(button,false);//这个战备就不能重复选择
            //selectAirdropIndex = -1;
            if (playerIndex == teamManager.Self.index)
            {
                bool have = false;
                for (int i = 0; i < 4; ++i)
                {
                    if (teamManager.players[playerIndex].airdrop[i] == 0)
                    {
                        SetSelectIndex(i, true);
                        have = true;
                        break;
                    }
                }
                if (!have)
                {
                    Cancel();
                }
            }

        }


        /// <summary> 发送玩家准备的消息</summary>
        public void SendPlayerReady(int playerIndex, bool state)
        {
            // 本机自己的就绪：本地立刻记账（数据 + UI 都按它走，不等房主回程）
            if (teamManager.Self != null && playerIndex == teamManager.SelfIndex) teamManager.Self.isReady = state;
            BridgeSys.Instance.SendPlayerReady(playerIndex, state);
        }

        /// <summary> 收到玩家准备的回调</summary>
        public void ReceivePlayerReady(int playerIndex, bool state)
        {
            // ⚠ 本类在 Init() 里就把自己登记给 BridgeSys 了（`BridgeSys.Instance.armament = this`），
            //   而 ready/animators 是 **FirstShowWnd()** 才建的 ⇒ 联机时"别人准备/加入"的回调
            //   可能先于窗口第一次显示到达，直接索引就是 NullReferenceException（2026-10-06 打包版实测）。
            if (ready == null || animators == null) return;
            if (playerIndex < 0 || playerIndex >= ready.Length || playerIndex >= animators.Length) return;

            ready[playerIndex] = state;
            if (playerIndex < teamManager.players.Count) teamManager.players[playerIndex].isReady = state;   // 与数据保持一致
            //SetAlpha(go.GetChild(4, 0), ready[a] ? 0.2f : 0.01f);
            SetText(armamentRoot.GetChild(playerIndex, 4, 1), state ? "就绪" : "尚未就绪");
            if (playerIndex == teamManager.SelfIndex) CollapseOnReady(state);   // ★ 自己就绪 ⇒ 收起面板（此时面板是锁住的）
            //Debug.LogError("让"+ armamentRoot.GetChild(playerIndex)+"播放"+(state ? "Ready" : "UnReady"), armamentRoot.GetChild(playerIndex));
            //armamentRoot.GetChild(playerIndex).GetComponent<Animator>().Play(state ? "Ready" : "UnReady", 1, 0);
            // ⚠ 该槽位当时没有玩家 ⇒ ShowWnd 没给它挂模型 ⇒ animators[i] 仍是 null（数组大小 = MaxPlayer，不是人数）
            if (animators[playerIndex] != null) animators[playerIndex].SetBool("IsReady", state);
            if (IEnumerableUtils.Sum(ready) == teamManager.players.Count)
            {
                GetComponent<Animator>().Play("Exit", 0, 0);
                //Debug.LogError("让" + gameObject + "播放 Exit", gameObject);
            }

        }


    }
}
