using System;
using System.Collections.Generic;
using FPSGame.Core;
using FPSGame.Data;
using FPSGame.GameContract;
using FPSGame.GameData;
using FPSGame.Managers;
using FPSGame.Net;
using FPSGame.Utils;
using KCPNet;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static FPSGame.WndTools.WndRootTool;

namespace FPSGame.UI
{
    /// <summary>
    /// 「房间列表」子面板。挂在 <c>SelectMapWnd/Rooms</c> 上，由 <c>SelectMapWnd</c> 的 Server 按钮驱动显隐。
    ///
    /// <para>▍行 = **搜索到的房间**（KCPNet 局域网 UDP 广播发现，不限于专用服务器），
    /// 数据来自 <see cref="LanDiscoverer.GetRooms"/>（房间模型 <see cref="LanRoomInfo"/>）。</para>
    ///
    /// <para>▍列（节点名对应 <c>Assets/Resources/UI/SelectMap/Row.prefab</c>）：
    /// <b>任务</b> = 任务类型名称（副行 房主名，两者 + 图标 + 边框都染成**该任务类型的颜色**）
    /// + 该地图的**区域背景图** <c>Task/AreaMask/Area</c>（外面的 <c>AreaMask</c> 把它裁成行高）
    /// + 里侧图标 <c>Task/Icon</c>（**任务类型图标**，查不到任务配置时才退回地图图标）与它外框 <c>Task/Frame</c>、
    /// <b>难度</b>=难度名、<b>队伍</b>=<c>PlayerCount</c> 个球员位、<b>类型</b>=服务器 / 局域网（已开局显示「进行中」）。</para>
    ///
    /// <para>▍筛选：搜索框（匹配房间名/地图）+ 四个**浮层下拉**（地图 / 难度 / 人数 / 联机类型）——
    /// 点 Title 弹出选项，选项**不占筛选栏布局**（浮层容器带 <c>LayoutElement.ignoreLayout</c>），
    /// 展开时挪到 <c>Filter/DropdownLayer</c> 画在最上层；再点 Title、点选项、点右侧列表（<c>Blocker</c>）都会收起。
    /// 地图项 = <c>TaskManager.MapData</c> 的**全部**地图（标签取 <c>MapData_SO.AreaName</c>，值取字典键），
    /// 难度项 = <see cref="DifficultyEnum"/> 的**全部**成员；两者的第 0 项都是「任意地图 / 任意难度」。</para>
    ///
    /// <para>▍<b>加入房间</b>：点**已选中的那一行**（再点一次）或点右下「加入」按钮 ⇒ 触发
    /// <see cref="OnRoomActivated"/>；房间有密码时先弹 <see cref="PasswordWnd"/> 收密码再回调。
    /// 面板**只负责收集**（哪个房、密码），真正的"回连 + 入房 + 超时"由 <c>NetRoomFlow</c> 负责。</para>
    ///
    /// <para>⚠ <b>难度 / 任务类型 / 来源 / 是否开局</b>：本面板**不自己判缺省值**，一律走 <see cref="RoomMeta"/>
    /// （房间字段的唯一适配点）。库 2026-10-08 起已在广播里带上这四样（<c>difficulty / taskMain / inGame / source</c>）；
    /// 任务**类型名**库不带、只有枚举 ⇒ 用本地 <c>TaskManager.FindMainMission</c> 反查（两端任务资产一致）。</para>
    ///
    /// <para>⚠ 一次性搭建工作在 <see cref="Build"/> 里做（首次 <see cref="Open"/> 调用），别放 <c>Awake</c>；
    /// 面板根节点在预制体里是 **active** 的，靠 <c>SelectMapWnd.ShowWnd()</c> 调 <see cref="Close"/> 收起
    /// ⇒ <see cref="Close"/> / <see cref="SetOpenGroup"/> 必须能承受「<see cref="Build"/> 还没跑过」。</para>
    /// </summary>
    [AddComponentMenu("UI/组件/房间列表")]
    public class ServerListPanel : MonoBehaviour
    {
        /// <summary>房间的「联机类型」——由房间主机地址判定，见 <see cref="RoomNetType"/>。</summary>
        public enum NetTypeEnum
        {
            /// <summary>远程 / 专用服务器主机（非内网地址）。</summary>
            Server = 0,
            /// <summary>局域网内的主机（内网 / 环回 / 地址不明）。</summary>
            Lan = 1,
        }

        /// <summary>下拉项的高度。展开时**真实占高**（把后面的组往下推），所以项高×项数要能塞进筛选栏
        /// ⇒ 12 张地图时 13×34 + 间距/padding ≈ 514px，加 4 个 Title（224）仍在 Panel 高度内。</summary>
        private const float ItemHeight = 34f;

        private const string AnyMapLabel = "任意地图";
        private const string AnyDiffLabel = "任意难度";
        private const string AnyCountLabel = "任意人数";
        private const string NotFullLabel = "未满员";

        /// <summary>联机类型下拉的文案，下标 = 预制体里项的顺序（0 任意 / 1 服务器 / 2 局域网）。</summary>
        private static readonly string[] NetTypeNames = { "任意类型", "服务器", "局域网" };

        private const int NetServerIndex = 1;
        private const int NetLanIndex = 2;

        /// <summary>筛选组里承载「下拉项」的子节点名（预制体约定，见 <see cref="_dropdowns"/>）。</summary>
        private const string DropdownName = "Dropdown";

        /// <summary>与 <see cref="DifficultyEnum"/> 成员顺序一一对应（InspectorName 只作用于编辑器，运行时要自己带）。</summary>
        private static readonly string[] DiffNames = { "普通", "困难", "非常困难", "硬核", "极限", "疯狂", "煎熬", "癫狂" };

        private static readonly Color NormalColor = new Color(0.060f, 0.070f, 0.090f, 0.90f);
        private static readonly Color SelectColor = new Color(0.05f, 0.26f, 0.42f, 0.96f);
        private static readonly Color AccentColor = new Color(0.651f, 0.867f, 0.867f, 1f);
        /// <summary>「服务器」类型的高亮色（与局域网的青色区分开）。</summary>
        private static readonly Color ServerColor = new Color(0.95f, 0.72f, 0.35f, 1f);
        private static readonly Color TextMain = new Color(0.92f, 0.94f, 0.96f, 1f);
        private static readonly Color TextDim = new Color(0.62f, 0.66f, 0.70f, 1f);

        [Header("列表")]
        [InspectorName("滚动视图")]
        [SerializeField] private ScrollRect scroll;

        [InspectorName("行容器")]
        [SerializeField] private RectTransform content;

        [InspectorName("行模板")]
        [SerializeField] private Transform rowTemplate;

        [Header("面板按钮")]
        [InspectorName("刷新按钮（Rooms/Server）")]
        [SerializeField] private Transform refreshBtn;

        [InspectorName("关闭按钮（Rooms/Cancel）")]
        [SerializeField] private Transform closeBtn;

        [InspectorName("加入按钮（Rooms/Join）")]
        [SerializeField] private Transform joinBtn;

        [Header("搜索")]
        [InspectorName("搜索框")]
        [SerializeField] private TMP_InputField searchInput;

        [Header("筛选下拉（点 Title 展开）")]
        [InspectorName("地图组")]
        [SerializeField] private Transform mapGroup;

        [InspectorName("地图当前值")]
        [SerializeField] private Transform mapValue;

        [InspectorName("难度组")]
        [SerializeField] private Transform diffGroup;

        [InspectorName("难度当前值")]
        [SerializeField] private Transform diffValue;

        [InspectorName("人数组")]
        [SerializeField] private Transform countGroup;

        [InspectorName("人数当前值")]
        [SerializeField] private Transform countValue;

        [InspectorName("联机类型组")]
        [SerializeField] private Transform netTypeGroup;

        [InspectorName("联机类型当前值")]
        [SerializeField] private Transform netTypeValue;

        [Header("状态提示")]
        [InspectorName("底部状态文本（Rooms/Panel/Status）")]
        [SerializeField] private Transform statusText;

        [Header("房间发现")]
        [InspectorName("监听端口（-1 = 库默认 29800）")]
        [SerializeField] private int listenPort = -1;

        [InspectorName("自动重扫间隔（秒）")]
        [SerializeField] private float rescanInterval = 3f;

        /// <summary>展开 / 收起时回调（true = 已展开）。<c>SelectMapWnd</c> 用它隐藏「地图 + 按钮栏」。</summary>
        public Action<bool> OnVisibleChanged;

        /// <summary>
        /// 请求加入某个房间（参数 = 房间 + 密码，无密码传空串）。
        /// <para>面板只收集"选哪个房 / 密码是什么"，回连与入房交给 <c>NetRoomFlow</c>（含超时与失败原因）。</para>
        /// </summary>
        public Action<LanRoomInfo, string> OnRoomActivated;

        /// <summary>当前选中的房间（没选中 = null）。</summary>
        public LanRoomInfo SelectedRoom => _selected >= 0 && _selected < _rooms.Count ? _rooms[_selected] : null;

        private bool _built;
        private LanDiscoverer _discoverer;
        private bool _listenFailed;
        /// <summary>发现器当前是否**正在听**。⚠ 与 <c>_discoverer != null</c> 不同：
        /// <see cref="Close"/> 只 <c>Stop()</c>（对象留着、内部 socket 拆掉），要靠本字段才知道要不要重开。</summary>
        private bool _listening;

        private readonly List<Transform> _rows = new List<Transform>();
        /// <summary>地图下拉项（下标 0 = 任意地图）。</summary>
        private readonly List<Transform> _mapItems = new List<Transform>();
        /// <summary>地图项的**值**（= <c>TaskManager.MapData</c> 的键；下标 0 = 空串 = 任意）。筛选时与它比对。</summary>
        private readonly List<string> _mapValues = new List<string>();
        /// <summary>地图项对应的数据（下标 0 = null = 任意地图）。
        /// 名称 / 颜色 / 背景图都从这里取，见 <see cref="RefreshMapItemVisual"/>。</summary>
        private readonly List<MapData_SO> _mapData = new List<MapData_SO>();
        /// <summary>动态生成的难度下拉项（下标 0 = 任意难度）。</summary>
        private readonly List<Transform> _diffItems = new List<Transform>();
        private readonly List<int> _diffValues = new List<int>();
        /// <summary>人数下拉的固定 2 项（任意人数 / 未满员）。</summary>
        private readonly List<Transform> _countItems = new List<Transform>();
        /// <summary>联机类型下拉的固定 3 项（任意类型 / 服务器 / 局域网）。</summary>
        private readonly List<Transform> _netTypeItems = new List<Transform>();

        private readonly List<LanRoomInfo> _rooms = new List<LanRoomInfo>();

        private int _selected = -1;
        private int _mapIndex;
        private int _diffIndex;
        private int _countIndex;
        private int _netTypeIndex;
        private string _keyword = string.Empty;
        private Transform _openGroup;
        /// <summary>
        /// 行的**常态底色**——Build 时从行模板（<c>Row.prefab</c>）的 Image 上抓一次。
        /// ▍为什么不再写死：行底色是 <c>Row.prefab</c> 的观感参数（作者现在调的是 <c>#17171780</c>），
        /// 代码写死会把它盖掉，prefab 一改就"看不见效果"。选中色仍由代码给（要有明确反馈）。
        /// </summary>
        private Color _rowNormal = NormalColor;
        /// <summary>
        /// 任务列三件套的**本色**（图标 <c>Task/Icon</c> / 六边形边框 <c>Task/Frame</c> / 标题 <c>Task/Name</c>），
        /// Build 时从行模板抓一次（同 <see cref="_rowNormal"/> 的道理：观感参数的唯一来源是预制体）。
        /// <para>▍用途：**查不到任务类型**（旧版房主 / 名字是兜底的地图名）时把这三处**复位**成预制体本色
        /// —— 行是**复用**的（<see cref="Fill"/> 反复写同一批行），不复位就会残留上一个房间的任务色。</para>
        /// </summary>
        private Color _taskIconColor = Color.white;
        private Color _taskFrameColor = Color.white;
        private Color _taskNameColor = TextMain;
        /// <summary>四个筛选组与各自的「项容器」——下标一一对应，Build 时抓一次。</summary>
        private Transform[] _groups;
        private Transform[] _dropdowns;
        /// <summary>地图/难度项是否已建过（这两个是静态字典：全部地图 / 全部难度，不随搜到的房间变化）。</summary>
        private bool _optionsBuilt;
        private float _scanTimer;
        private string _signature = string.Empty;
        /// <summary>地图 / 难度下拉的项模板（首次重建时从组里就地取，之后只克隆它，不再当项用）。</summary>
        private Transform _mapTemplate;
        private Transform _diffTemplate;

        /// <summary>面板当前是否展开。</summary>
        public bool IsOpen => gameObject.activeSelf;

        /// <summary>展开面板：启动房间发现、建 UI、首次刷新。</summary>
        public void Open()
        {
            gameObject.SetActive(true);
            if (!_built) Build();

            OnVisibleChanged?.Invoke(true);

            StartListening();
            Scan();
            Refresh(true);
        }

        /// <summary>收起面板：停止房间发现并释放端口。</summary>
        public void Close()
        {
            OnVisibleChanged?.Invoke(false);

            StopListening();
            SetOpenGroup(null);
            if (gameObject.activeSelf) gameObject.SetActive(false);
        }

        /// <summary>展开 / 收起。</summary>
        public void Toggle()
        {
            if (IsOpen) Close();
            else Open();
        }

        private void OnDisable()
        {
            StopListening();
            SetOpenGroup(null);
            _selected = -1;
            RefreshJoinButton();
        }

        private void OnDestroy()
        {
            DisposeDiscoverer();
        }

        private void Update()
        {
            _scanTimer -= Time.unscaledDeltaTime;
            if (_scanTimer <= 0f)
            {
                _scanTimer = Mathf.Max(1f, rescanInterval);
                Scan();
            }

            // 房间集合（含人数/密码/名字）或筛选状态变了才重建
            string sig = BuildSignature();
            if (sig == _signature) return;
            _signature = sig;
            Refresh(true);
        }

        #region 搭建

        private void Build()
        {
            _built = true;

            if (rowTemplate != null) rowTemplate.gameObject.SetActive(false);

            // 行的常态底色**取预制体自己的**（Row.prefab 是观感参数的唯一来源），
            // 别用代码里写死的 NormalColor 把它盖掉；只有"选中态"才由代码给色。
            var rowImage = rowTemplate != null ? rowTemplate.GetComponent<Image>() : null;
            _rowNormal = rowImage != null ? rowImage.color : NormalColor;

            // 任务列（图标 / 边框 / 标题）的本色也抓一次：查不到任务类型时要复位到它们（见 _taskIconColor）
            if (rowTemplate != null)
            {
                var tplIcon = rowTemplate.Find("Task/Icon");
                if (tplIcon != null) _taskIconColor = GetColor(tplIcon);
                var tplFrame = rowTemplate.Find("Task/Frame");
                if (tplFrame != null) _taskFrameColor = GetColor(tplFrame);
                var tplName = rowTemplate.Find("Task/Name");
                if (tplName != null) _taskNameColor = GetColor(tplName);
            }

            SetCilck(refreshBtn, () =>
            {
                PlayClick();
                Scan();
                Refresh(true);
            });

            SetCilck(closeBtn, () =>
            {
                PlayClick();
                Close();
            });

            // 加入按钮：与"当前有没有选中房间"联动（RefreshJoinButton 里刷新可用态）
            SetCilck(joinBtn, () =>
            {
                PlayClick();
                Activate(SelectedRoom);
            });
            RefreshJoinButton();

            // 四个筛选组：抓出各自的项容器，并把 Title 绑成「点一下开/关下拉」
            _groups = new[] { mapGroup, diffGroup, countGroup, netTypeGroup };
            _dropdowns = new Transform[_groups.Length];
            for (int i = 0; i < _groups.Length; ++i)
            {
                var group = _groups[i];
                if (group == null) continue;
                _dropdowns[i] = group.Find(DropdownName);
                BindGroup(group, () => SetOpenGroup(_openGroup == group ? null : group));
            }

            if (searchInput != null)
            {
                _keyword = searchInput.text;
                searchInput.onValueChanged.AddListener(value =>
                {
                    _keyword = value;
                    Refresh(false);
                });
            }

            // 人数 / 联机类型的项都是预制好的固定选项（人数 2 项、联机类型 3 项），直接绑
            BindFixedItems(countGroup, _countItems, index => _countIndex = index);
            BindFixedItems(netTypeGroup, _netTypeItems, index => _netTypeIndex = index);

            SetOpenGroup(null);
            RefreshFilterVisual();
        }

        /// <summary>把组里的「Title」绑成下拉开关（点一下开、再点一下关）。</summary>
        private static void BindGroup(Transform group, Action onClick)
        {
            if (group == null) return;
            var title = group.Find("Title");
            if (title != null) SetCilck(title, () => onClick());
        }

        /// <summary>组 → 项容器。</summary>
        private Transform DropdownOf(Transform group)
        {
            if (group == null || _groups == null) return null;
            for (int i = 0; i < _groups.Length; ++i)
            {
                if (_groups[i] == group) return _dropdowns[i];
            }
            return null;
        }

        /// <summary>取容器里的所有直接子物体（= 下拉项）。</summary>
        private static void CollectItems(Transform container, List<Transform> target)
        {
            target.Clear();
            if (container == null) return;
            for (int i = 0; i < container.childCount; ++i)
            {
                var child = container.GetChild(i);
                if (child.name == "Title") continue;
                target.Add(child);
            }
        }

        /// <summary>绑定「项是预制好的固定选项」的下拉组（人数 / 联机类型）。</summary>
        private void BindFixedItems(Transform group, List<Transform> cache, Action<int> onPick)
        {
            CollectItems(DropdownOf(group), cache);
            for (int i = 0; i < cache.Count; ++i)
            {
                int index = i;
                SetItemClick(cache[i], () =>
                {
                    onPick(index);
                    PlayClick();
                    SetOpenGroup(null);
                    Refresh(false);
                });
            }
        }

        /// <summary>
        /// 展开 / 收起筛选下拉（手风琴：同一时刻只开一个）。
        ///
        /// <para>▍展开的项是**真实占高**的：容器就在组里、由组的 VLG 正常排版（预制体上
        /// <c>LayoutElement.ignoreLayout</c> 已关），组长高后 Filter 的 VLG 顺势把**后面的组整体往下推**。</para>
        ///
        /// <para>▍所以不再需要"把容器挪到浮层 / 拿拦截层挡列表"那套（已删）；代价是绘制层级不再特殊处理，
        /// 项多时筛选栏会变高（项高 <see cref="ItemHeight"/>×项数，12 张地图 ≈ 514px，仍在 Panel 内）。</para>
        /// </summary>
        private void SetOpenGroup(Transform group)
        {
            _openGroup = group;

            // ⚠ Build() 之前也会被调到：SelectMapWnd.ShowWnd() 一进来就 serverPanel.Close()，
            // 此时 _built 还是 false、_groups 还是 null，这里必须早退（老实现逐个判空所以没暴露）。
            if (_groups == null) return;

            for (int i = 0; i < _groups.Length; ++i)
            {
                var g = _groups[i];
                if (g == null) continue;

                bool open = g == group;
                var dropdown = _dropdowns[i];
                if (dropdown != null) SetActive(dropdown, open);
                SetArrowExpanded(g, open);
            }

            // 立即重排一次：Filter 的 VLG 要按新的项高度把后面的组推下去
            var filter = mapGroup != null ? mapGroup.parent as RectTransform : null;
            if (filter != null) LayoutRebuilder.ForceRebuildLayoutImmediate(filter);
        }

        /// <summary>
        /// 展开时把 Title 右端的箭头翻成朝上，收起时朝下。
        /// ▍为什么用旋转而不是换 sprite：<c>Arrow_Up</c> 与 <c>Arrow_Down</c> 观感几乎一致（都是淡色三角形），
        /// 换图看不出区别，还得给组件多挂一个 Sprite 引用；而箭头 pivot 在右中(1,0.5)、节点无旋转，
        /// 绕 z 转 180° 就是**原地**翻转，且四个组共用一套逻辑。
        /// </summary>
        private static void SetArrowExpanded(Transform group, bool expanded)
        {
            var arrow = group.Find("Title/Arrow");
            if (arrow == null) return;

            var euler = arrow.localEulerAngles;
            euler.z = expanded ? 180f : 0f;
            arrow.localEulerAngles = euler;
        }

        /// <summary>
        /// 给下拉项挂点击。⚠ 你做的项节点（FilterMapItem / FilterDiffItem）**只有 Title 带 Button**，
        /// 项本身没有，所以这里就地补一个 Button，并把 raycast 目标指到项内第一张图（Mask/bg）。
        /// </summary>
        private static void SetItemClick(Transform item, UnityEngine.Events.UnityAction action)
        {
            if (item == null) return;

            var graphic = item.GetComponentInChildren<Image>();
            var btn = item.TryGetOrAddComponent<Button>();
            btn.transition = Selectable.Transition.None;
            if (graphic != null)
            {
                graphic.raycastTarget = true;
                btn.targetGraphic = graphic;
            }
            btn.onClick.RemoveAllListeners();
            btn.onClick.AddListener(action);
        }

        #endregion

        #region 房间发现

        /// <summary>
        /// 启动 / 恢复监听：优先库默认端口，失败则降级到「同机自测端口」。
        /// <para>⚠ 发现器对象是**复用**的：库的 <c>Stop()</c> 只是把内部 socket / 线程拆掉
        /// （实测 2026-10-10：Stop 后 <c>_udp/_cts</c> 置空，再 <c>StartListening()</c> 能重新起来），
        /// 而本方法的入口 <see cref="Open"/> 每次都会调它 ⇒ 之前那句 <c>if (_discoverer != null) return;</c>
        /// 会让「关掉面板再打开」之后再也收不到任何广播（列表永远空）。改为按 <see cref="_listening"/> 判断。</para>
        /// </summary>
        private void StartListening()
        {
            if (_discoverer == null)
            {
                int port = listenPort > 0 ? listenPort : LanDiscoveryConfig.ListenPort;
                if (TryListen(port)) return;
                if (port != NetConfig.LanSelfBroadcastPort && TryListen(NetConfig.LanSelfBroadcastPort)) return;

                _listenFailed = true;
                Debug.LogWarning("[ServerListPanel] 房间发现不可用：UDP 端口 " + port + " 绑定失败（可能被本机开房占用或防火墙拦截）");
                return;
            }

            if (_listening) return;

            try
            {
                _discoverer.StartListening();
                _listening = true;
                _listenFailed = false;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[ServerListPanel] 恢复房间发现失败：" + e.Message);
            }
        }

        private bool TryListen(int port)
        {
            try
            {
                var discoverer = new LanDiscoverer(port);
                discoverer.StartListening();
                _discoverer = discoverer;
                _listening = true;
                _listenFailed = false;
                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[ServerListPanel] 监听端口 " + port + " 失败：" + e.Message);
                return false;
            }
        }

        private void StopListening()
        {
            if (!_listening) return;
            _listening = false;
            if (_discoverer == null) return;
            try
            {
                _discoverer.Stop();
            }
            catch (Exception e)
            {
                Debug.LogWarning("[ServerListPanel] 停止房间发现失败：" + e.Message);
            }
        }

        private void DisposeDiscoverer()
        {
            if (_discoverer == null) return;
            StopListening();
            try
            {
                _discoverer.Dispose();
            }
            catch (Exception e)
            {
                Debug.LogWarning("[ServerListPanel] 释放房间发现器失败：" + e.Message);
            }
            _discoverer = null;
        }

        /// <summary>主动扫描一次（异步，结果进 <see cref="LanDiscoverer.GetRooms"/>）。</summary>
        public void Scan()
        {
            if (_discoverer == null || !_listening) return;
            try
            {
                _discoverer.Scan();
            }
            catch (Exception e)
            {
                Debug.LogWarning("[ServerListPanel] 扫描房间失败：" + e.Message);
            }
        }

        /// <summary>
        /// 【快速匹配】确保房间发现在跑 —— 面板没打开过（或已 <see cref="Close"/> 掉）时发现器没在听广播，
        /// <c>SelectMapWnd</c> 的「加入」要在**不展开面板**的前提下也能搜到房间（见 <c>SelectMapWnd.MatchRoutine</c>）。
        /// <para>⚠ 只拉起发现器，**不触发 <see cref="OnVisibleChanged"/>**（不隐藏地图/按钮栏）。</para>
        /// </summary>
        public void EnsureDiscovery()
        {
            if (_listening) return;
            StartListening();
        }

        /// <summary>
        /// 【快速匹配】找一个**能直接进**的房间：地图与主任务类型都对得上，且无密码、未开局、未满员。
        /// <para>▍返回 null = 没有可加入的房间（调用方据此"自己开一个"）。
        /// 任务枚举对不上的房间算不匹配（旧版房主没带该字段 ⇒ -1，同样不匹配）；
        /// 人数相同时挑人多的那间。</para>
        /// <para>▍难度**不**参与匹配：难度是进房后由房主说了算，而玩家此刻还没调过难度。</para>
        /// </summary>
        /// <param name="mapName">地图短名（= <c>MapData</c> 的键，也是房主写进广播 <c>MapName</c> 的那个）</param>
        /// <param name="taskMain">主任务枚举值（<c>MissionEnum</c> 的 int；&lt;0 = 不筛任务）</param>
        public LanRoomInfo FindJoinableRoom(string mapName, int taskMain)
        {
            // 没在听广播 ⇒ 表里只会有上一次的残留 ⇒ 一律当"没搜到"（宁可自己开房，也别回连一个可能已经关掉的房）
            if (_discoverer == null || !_listening) return null;

            LanRoomInfo best = null;
            foreach (var room in _discoverer.GetRooms())
            {
                if (room == null) continue;
                if (room.PasswordProtected) continue;                                     // 有密码：不能静默加入
                if (RoomMeta.InGame(room)) continue;                                      // 已开局
                if (room.MaxPlayers > 0 && room.PlayerCount >= room.MaxPlayers) continue;  // 满员
                if (!string.IsNullOrEmpty(mapName) && RoomMeta.MapName(room) != mapName) continue;
                if (taskMain >= 0 && RoomMeta.TaskEnum(room) != taskMain) continue;

                if (best == null || room.PlayerCount > best.PlayerCount) best = room;
            }
            return best;
        }

        private static string RoomKey(LanRoomInfo room) => room.HostIp + ":" + room.HostPort;

        private List<LanRoomInfo> QueryRooms()
        {
            var list = new List<LanRoomInfo>();
            if (_discoverer == null) return list;
            foreach (var room in _discoverer.GetRooms())
            {
                if (room != null) list.Add(room);
            }
            return list;
        }

        /// <summary>用于判断「房间集合或筛选是否变化」。</summary>
        private string BuildSignature()
        {
            if (_discoverer == null) return _listenFailed ? "fail" : string.Empty;

            var parts = new List<string>();
            foreach (var r in _discoverer.GetRooms())
            {
                if (r == null) continue;
                // ⚠ 签名必须带上"会影响显示/筛选的一切"：难度 / 是否开局 / 来源改了也要重建
                parts.Add(RoomKey(r) + "|" + r.RoomName + "|" + r.MapName + "|" + r.PlayerCount + "/" + r.MaxPlayers + "|" + r.PasswordProtected
                    + "|" + RoomMeta.Difficulty(r) + "|" + RoomMeta.InGame(r) + "|" + RoomMeta.Source(r));
            }
            parts.Sort();
            return string.Join(";", parts) + "#" + _mapIndex + "#" + _diffIndex + "#" + _countIndex + "#" + _netTypeIndex + "#" + _keyword;
        }

        #endregion

        #region 列表

        /// <param name="rebuildOptions">是否重建下拉项（房间集合变了 / 首次打开时为 true）。</param>
        private void Refresh(bool rebuildOptions)
        {
            _selected = -1;

            var raw = QueryRooms();
            if (rebuildOptions) RebuildOptions();

            _rooms.Clear();
            foreach (var room in raw)
            {
                if (PassFilter(room)) _rooms.Add(room);
            }

            _rooms.Sort((a, b) =>
            {
                int byCount = b.PlayerCount.CompareTo(a.PlayerCount);
                // 人数相同的按房间名，保证列表顺序稳定
                return byCount != 0 ? byCount : string.CompareOrdinal(RoomMeta.RoomName(a), RoomMeta.RoomName(b));
            });

            EnsureRows(_rooms.Count);
            for (int i = 0; i < _rows.Count; ++i)
            {
                bool show = i < _rooms.Count;
                SetActive(_rows[i], show);
                if (show) Fill(_rows[i], _rooms[i], i);
            }

            RefreshFilterVisual();
            RefreshJoinButton();
            RefreshStatus(raw.Count);

            // ⚠ TMP 度量要到 PreRender 才更新：先刷画布 → 重排 → 再刷一次（重排会把图元标脏）
            Canvas.ForceUpdateCanvases();
            if (content != null) LayoutRebuilder.ForceRebuildLayoutImmediate(content);
            Canvas.ForceUpdateCanvases();

            // 展开中的下拉高度由项数决定（真实占高），项重建后要立刻重排一次
            if (_openGroup != null) LayoutRebuilder.ForceRebuildLayoutImmediate(DropdownOf(_openGroup) as RectTransform);

            if (scroll != null) scroll.verticalNormalizedPosition = 1f;
        }

        /// <summary>
        /// 底部状态提示（空列表 / 监听失败 / 筛选后为空 / 正常统计）。
        /// <para>⚠ 原来这里只有 <c>Debug.Log</c>，玩家看不到；<c>Rooms/Panel/Status</c> 节点是补上的。</para>
        /// </summary>
        private void RefreshStatus(int rawCount)
        {
            if (statusText == null) return;

            if (_listenFailed)
            {
                SetColor(statusText, ServerColor);
                SetText(statusText, "房间发现不可用：UDP 端口被占用或被防火墙拦截");
                return;
            }

            if (_discoverer == null)
            {
                SetColor(statusText, TextDim);
                SetText(statusText, "正在搜索局域网房间…");
                return;
            }

            if (rawCount == 0)
            {
                SetColor(statusText, TextDim);
                SetText(statusText, "未搜索到房间（先让房主开房，再点 Server 重扫）");
                return;
            }

            if (_rooms.Count == 0)
            {
                SetColor(statusText, ServerColor);
                SetText(statusText, "搜到 " + rawCount + " 个房间，当前筛选条件下 0 个");
                return;
            }

            SetColor(statusText, TextDim);
            SetText(statusText, "共 " + _rooms.Count + " 个房间");
        }

        /// <summary>加入按钮：**没选中房间时整个隐藏**；选中了但房间已开局则置灰不可点。</summary>
        private void RefreshJoinButton()
        {
            if (joinBtn == null) return;
            var room = _rooms.Count > 0 && _selected >= 0 && _selected < _rooms.Count ? _rooms[_selected] : null;
            SetActive(joinBtn, room != null);
            SetButtonInteractable(joinBtn, room != null && !RoomMeta.InGame(room));
        }

        /// <summary>
        /// 重建「地图 / 难度」下拉项（**静态全量，只建一次**）：
        /// <list type="bullet">
        ///   <item>地图：第 0 项「任意地图」（值 = 空串），其后是 <c>TaskManager.MapData</c> 的**全部**地图
        ///         （值 = 字典键 = 房主写进广播 <c>MapName</c> 的那个短名；标签 = <c>MapData_SO.AreaName</c>）；</item>
        ///   <item>难度：第 0 项「任意难度」（值 = -1），其后是 <see cref="DifficultyEnum"/> 的**全部**成员。</item>
        /// </list>
        /// ▍为什么不再"按搜到的房间动态生成"：下拉是全量字典（选完再等房间出现很正常），
        /// 且房主/成员的地图键本来就来自同一份 <c>MapData</c> ⇒ 静态列表反而更稳（不会因为搜到 0 个房间就没有选项）。
        /// </summary>
        private void RebuildOptions()
        {
            // 地图项来自 TaskManager.MapData：若上次建项时它还没就绪（列表里只剩「任意地图」），这次补建一次
            if (_optionsBuilt && _mapValues.Count <= 1) _optionsBuilt = false;

            if (_optionsBuilt) return;
            _optionsBuilt = true;

            // ---- 地图：任意 + TaskManager.MapData 全部（MapData 由 TaskManager.Init 从 Resources/GameData/Map 装载） ----
            _mapValues.Clear();
            _mapData.Clear();
            _mapValues.Add(string.Empty);   // 任意地图：值 = 空串、数据 = null
            _mapData.Add(null);

            var taskMgr = TaskManager.Instance;
            if (taskMgr != null && taskMgr.MapData != null)
            {
                foreach (var kv in taskMgr.MapData)
                {
                    if (kv.Value == null) continue;
                    _mapValues.Add(kv.Key);
                    _mapData.Add(kv.Value);
                }
            }

            // ---- 难度：任意 + DifficultyEnum 全部成员 ----
            _diffValues.Clear();
            _diffValues.Add(-1);
            for (int i = 0; i < Tool.EnumLenght<DifficultyEnum>(); ++i) _diffValues.Add(i);

            RebuildItemList(DropdownOf(mapGroup), _mapItems, ref _mapTemplate, _mapValues, GetMapLabel, i =>
            {
                _mapIndex = i;
                PlayClick();
                SetOpenGroup(null);
                Refresh(false);
            });

            RebuildItemList(DropdownOf(diffGroup), _diffItems, ref _diffTemplate, _diffValues, GetDiffLabel, i =>
            {
                _diffIndex = i;
                PlayClick();
                SetOpenGroup(null);
                Refresh(false);
            });

            // 越界保护（配置改过 / 首次建项时）
            if (_mapIndex >= _mapValues.Count) _mapIndex = 0;
            if (_diffIndex >= _diffValues.Count) _diffIndex = 0;
        }

        /// <summary>
        /// 用容器里第一个子物体当**模板**（保持 inactive，永不当项用），克隆出 <paramref name="labels"/>.Count 个项。
        /// 每次重建先销毁上一批克隆体，避免刷新时越堆越多。
        /// </summary>
        private void RebuildItemList<T>(Transform container, List<Transform> cache, ref Transform template,
            List<T> labels, Func<int, string> labelOf, Action<int> onClick)
        {
            for (int i = 0; i < cache.Count; ++i)
            {
                var old = cache[i];
                if (old == null || old == template) continue;
                // 编辑器下（含 MCP 离屏验证）只能用 DestroyImmediate
                if (Application.isPlaying) Destroy(old.gameObject);
                else DestroyImmediate(old.gameObject);
            }
            cache.Clear();
            if (container == null) return;

            if (template == null)
            {
                for (int i = 0; i < container.childCount; ++i)
                {
                    var child = container.GetChild(i);
                    if (child.name == "Title") continue;
                    template = child;
                    break;
                }
                if (template == null) return;
                template.gameObject.SetActive(false);
            }

            for (int i = 0; i < labels.Count; ++i)
            {
                var item = Instantiate(template, container, false);
                item.name = template.name + i;
                // ⚠ 模板是 inactive 的，克隆体会**继承**这个关闭状态；现在展开只点亮容器，
                // 所以必须在这里显式点亮，否则地图/难度下拉弹出来是空的（固定项的组不受影响）。
                item.gameObject.SetActive(true);
                // 项高统一由代码定：展开是**真实占高**的，"项高 × 项数"直接决定筛选栏要往下长多少
                var le = item.TryGetOrAddComponent<LayoutElement>();
                le.preferredHeight = ItemHeight;
                le.minHeight = ItemHeight;
                SetText(item.Find("Name"), labelOf(i));
                int index = i;
                SetItemClick(item, () => onClick(index));
                cache.Add(item);
            }
        }

        private string GetMapLabel(int index) => index >= 0 && index < _mapData.Count ? MapLabelOf(_mapData[index]) : AnyMapLabel;

        private string GetDiffLabel(int index)
        {
            if (index <= 0 || index >= _diffValues.Count) return AnyDiffLabel;
            int value = _diffValues[index];
            return value >= 0 && value < DiffNames.Length ? DiffNames[value] : "难度" + value;
        }

        /// <summary>联机类型下拉的当前值文案。</summary>
        private static string GetNetTypeLabel(int index)
        {
            return index > 0 && index < NetTypeNames.Length ? NetTypeNames[index] : NetTypeNames[0];
        }

        private bool PassFilter(LanRoomInfo room)
        {
            if (_mapIndex > 0 && _mapIndex < _mapValues.Count && RoomMeta.MapName(room) != _mapValues[_mapIndex]) return false;

            // 难度：未知（旧版房主没带该字段，= -1）不会被具体难度筛中 —— 显示不出难度就没法按难度筛
            if (_diffIndex > 0 && _diffIndex < _diffValues.Count && RoomMeta.Difficulty(room) != _diffValues[_diffIndex])
                return false;

            // 人数筛选 = 「未满员 **且** 没有开局」：只有人数判据时，排除不了"已进游戏但人还没满"的房间
            if (_countIndex == 1 && room.MaxPlayers > 0 && room.PlayerCount >= room.MaxPlayers) return false;
            if (_countIndex == 1 && RoomMeta.InGame(room)) return false;

            // 联机类型：0 = 任意；服务器 / 局域网按广播里的来源字段（旧版房主缺该字段 ⇒ 默认局域网）
            if (_netTypeIndex == NetServerIndex && RoomNetType(room) != NetTypeEnum.Server) return false;
            if (_netTypeIndex == NetLanIndex && RoomNetType(room) != NetTypeEnum.Lan) return false;

            if (string.IsNullOrEmpty(_keyword)) return true;
            string key = _keyword.Trim();
            if (key.Length == 0) return true;
            // 搜房间名（剥后缀）、地图名、任务类型（"歼灭"也能搜到，后缀反而是个可搜字段）
            return Contains(RoomMeta.RoomName(room), key)
                || Contains(RoomMeta.MapName(room), key)
                || Contains(RoomTaskType(room, FindMapData(room)), key);
        }

        private static bool Contains(string source, string key)
        {
            return !string.IsNullOrEmpty(source) && source.IndexOf(key, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private void EnsureRows(int count)
        {
            while (_rows.Count < count)
            {
                int index = _rows.Count;
                var row = Instantiate(rowTemplate, content, false);
                row.name = "Row" + index;
                SetCilck(row, () => Select(index));
                _rows.Add(row);
            }
        }

        /// <summary>
        /// 把房间数据写进行模板克隆体。节点名一一对应
        /// <c>Assets/Resources/UI/SelectMap/Row.prefab</c>：
        /// <list type="bullet">
        ///   <item><c>Task/AreaMask/Area</c> ← 该地图的**区域背景图** <c>MapData_SO.AreaBackground</c>
        ///         （退 <c>Map</c> / <c>Icon</c>，见 <see cref="PickMapImage"/>）——外面套着
        ///         <c>AreaMask</c>(RectMask2D)，600×300 的图会被裁成一行高 ⇒ 整行左侧就是一块地图缩略背景；
        ///         查不到这张地图的资料时**连背景和图标一起隐藏**，免得张冠李戴显示出别的图；</item>
        ///   <item><c>Task/Icon</c> ← **任务类型图标** <c>MissionMainData_SO.sprite</c>
        ///         （按 <see cref="RoomTaskMission"/> 反查；查不到才退回 <c>MapData_SO.Icon</c> 这张地图图标），
        ///         其外框 <c>Task/Frame</c> + <c>Task/Name</c> 一并染成**任务类型颜色**；</item>
        ///   <item><c>Task/Name</c> ← **任务类型名称**（见 <see cref="RoomTaskType"/>：按广播的主任务枚举
        ///         反查本地任务配置，取不到退回地图名）；
        ///         <c>Task/Host</c> ← **房主名**（广播 <c>PlayerNames</c> 末尾的真名，取不到退 <c>HostIp</c>）；</item>
        ///   <item><c>Diff/Text</c> = 难度名、<c>Team/Pips</c> = 前 <c>PlayerCount</c> 个球员位、
        ///         <c>Type/Text</c> = 服务器 / 局域网（已开局优先显示「进行中」）。</item>
        /// </list>
        /// </summary>
        private void Fill(Transform row, LanRoomInfo room, int index)
        {
            bool inGame = RoomMeta.InGame(room);
            SetColor(row, index == _selected ? SelectColor : _rowNormal);

            var data = FindMapData(room);
            var task = row.Find("Task");
            if (task != null)
            {
                // 地图背景图（行左侧那块画）+ 地图图标：同一份 MapData_SO，查一次表两处共用
                var area = FindRowArea(task);
                if (area != null)
                {
                    var mapImage = PickMapImage(data);
                    SetActive(area, mapImage != null);
                    SetSprite(area, mapImage);
                }

                // ★ 任务类型三件套（图标 / 边框 / 标题）—— 广播只带**主任务枚举**（RoomMeta.TaskEnum），
                //   图标 / 颜色 / 名字都在本地的 `MissionMainData_SO` 里 ⇒ 按枚举精确反查（两端任务资产一致）。
                //   查不到就整体退回"地图图标 + 预制体本色"，**不编假数据**。
                var mission = RoomTaskMission(room);
                string taskName = RoomTaskType(room, data);

                // 里侧那颗图标：换成**任务类型图标**（原来的 `MapData_SO.Icon` 只在查不到任务配置时兜底）
                var icon = task.Find("Icon");
                if (icon != null)
                {
                    var iconSprite = mission != null && mission.sprite != null
                        ? mission.sprite
                        : (data != null ? data.Icon : null);
                    SetActive(icon, iconSprite != null);
                    SetSprite(icon, iconSprite);
                    SetColor(icon, mission != null ? mission.color : _taskIconColor);
                }

                // 框住图标的六边形边框（`Task/Frame`）与右边的标题 = **任务类型颜色**
                // ⚠ 预制体里 `Frame` 与 `Icon` 是**同级**（都在 `Task` 下），但 `Frame` 就是"框住图标"的那张边框图
                //   —— `Task` 自己只是个布局容器（无 Image），所以"图标外框"实际落在这张同级图上。
                SetColor(task.Find("Frame"), mission != null ? mission.color : _taskFrameColor);
                var nameText = task.Find("Name");
                //SetColor(nameText, mission != null ? mission.color : _taskNameColor);

                SetText(nameText, taskName);

                string host = RoomHostName(room);
                SetText(task.Find("Host"), string.IsNullOrEmpty(host) ? room.HostIp : host);
            }

            int diff = RoomMeta.Difficulty(room);
            SetText(row.Find("Diff/Text"), diff >= 0 && diff < DiffNames.Length ? DiffNames[diff] : "-");

            // 类型列：已开局的房间优先显示「进行中」（加入按钮同时会置灰）
            var type = RoomNetType(room);
            var typeText = row.Find("Type/Text");
            if (inGame)
            {
                SetText(typeText, "进行中");
                SetColor(typeText, ServerColor);
            }
            else
            {
                SetText(typeText, NetTypeNames[type == NetTypeEnum.Lan ? NetLanIndex : NetServerIndex]);
                SetColor(typeText, type == NetTypeEnum.Lan ? AccentColor : ServerColor);
            }

            FillTeamPips(row.Find("Team/Pips"), room.PlayerCount);
        }

        /// <summary>
        /// 行里那张**地图背景图**的节点。⚠ 预制体里它是 <c>Task/AreaMask/Area</c>
        /// （外面套一层 <c>RectMask2D</c> 把 600×300 的图裁成一行高，见 <c>Row.prefab</c> 层级），
        /// 所以不能直接 <c>task.Find("Area")</c>；这里按 <see cref="FindMapItemBg"/> 的同款写法带一层兼容。
        /// </summary>
        private static Transform FindRowArea(Transform task)
        {
            var area = task.Find("AreaMask/Area");
            return area != null ? area : task.Find("Area");
        }

        /// <summary>
        /// 房间的**任务类型配置**（图标 / 颜色 / 名字的来源）。
        ///
        /// <para>▍按广播里的**主任务枚举**精确取。
        /// 必须用枚举：任务类型**名字不唯一**（<c>GameData/Mission/Main</c> 里 3 份「进攻任务」、
        /// 2 份「歼灭任务」…**颜色各不相同**），只靠名字会给错图标/颜色。</para>
        ///
        /// <para>▍返回 null 的情形（旧版房主没带该字段 / 还没选任务 / 两端任务资产不一致）：
        /// 调用方退回"地图图标 + 预制体本色"，不编假数据。</para>
        /// </summary>
        private static MissionMainData_SO RoomTaskMission(LanRoomInfo room)
        {
            var taskMgr = TaskManager.Instance;
            if (taskMgr == null) return null;

            int mainEnum = RoomMeta.TaskEnum(room);
            return mainEnum >= 0 ? taskMgr.FindMainMission((MissionEnum)mainEnum) : null;
        }

        /// <summary>
        /// 行主标题 = **任务类型的名称**（= 主任务 <c>MissionMainData_SO.name</c>，如「歼灭」/「护送」）。
        ///
        /// <para>▍广播里只带**枚举**（<c>LanRoomInfo.TaskMain</c>）⇒ 名字用本地任务配置反查
        /// （<see cref="RoomTaskMission"/>，两端任务资产一致）。</para>
        ///
        /// <para>▍查不到（旧版房主没带枚举 / 还没选任务 / 两端资产不一致）时退**地图名**（真实信息），别留空。</para>
        /// </summary>
        private static string RoomTaskType(LanRoomInfo room, MapData_SO data)
        {
            var mission = RoomTaskMission(room);
            if (mission != null && !string.IsNullOrEmpty(mission.name)) return mission.name;

            string raw = RoomMeta.MapName(room);
            if (!string.IsNullOrEmpty(raw)) return raw;
            return data != null ? MapLabelOf(data) : "未知任务";
        }

        /// <summary>
        /// 房主名。⚠ 广播里 <c>PlayerNames[0]</c> 是**合成名** <c>Host_&lt;HHmmssfff&gt;</c>
        /// （成员端靠它排除"自己开的房"，见 <c>NetHostSvc.BuildHostPlayerNames</c>），
        /// 真房主名被追加在**末尾** ⇒ 这里从后往前取第一个"非合成名"。
        /// 老版本房主（只有合成名）或空数组返回 null，由调用方退回 IP。
        /// </summary>
        private static string RoomHostName(LanRoomInfo room)
        {
            var names = room != null ? room.PlayerNames : null;
            if (names == null) return null;

            for (int i = names.Length - 1; i >= 0; --i)
            {
                if (!string.IsNullOrEmpty(names[i]) && !names[i].StartsWith("Host_", StringComparison.Ordinal)) return names[i];
            }
            return null;
        }

        /// <summary>按人数点亮球员位（最多模板里的 4 个）。</summary>
        private static void FillTeamPips(Transform pips, int count)
        {
            if (pips == null) return;
            for (int i = 0; i < pips.childCount; ++i)
            {
                SetActive(pips.GetChild(i), i < count);
            }
        }

        /// <summary>
        /// 点行：第一次选中，**再点同一行 = 加入**（与右下「加入」按钮等价）。
        /// </summary>
        private void Select(int index)
        {
            int old = _selected;
            bool activate = old == index;
            _selected = activate ? -1 : index;

            if (old >= 0 && old < _rows.Count) SetColor(_rows[old], _rowNormal);
            if (_selected >= 0 && _selected < _rows.Count) SetColor(_rows[_selected], SelectColor);

            RefreshJoinButton();
            PlayClick();

            if (activate) Activate(index >= 0 && index < _rooms.Count ? _rooms[index] : null);
        }

        /// <summary>
        /// 请求加入房间：有密码的房间先弹 <see cref="PasswordWnd"/> 收密码，再把「房间 + 密码」交给
        /// <see cref="OnRoomActivated"/>（真正的回连/入房/超时是 <c>NetRoomFlow</c> 的活）。
        /// </summary>
        private void Activate(LanRoomInfo room)
        {
            if (room == null) return;

            // ★ 已经在这个房间里了（自己开的房 / 已经连着的那个房主）⇒ 只提示"已经在里面了"，
            //   不再回连一次（对已连着的房主再 ConnectToRoom 会把现有会话顶掉、白重连一遍）。
            //   ⚠ 必须放在下面那句"已开局不给进"**之前**：自己那间房开打后 InGame=true，
            //     排在后面前面就先被那句静默挡掉了，玩家点自己房间什么反应都没有。
            var flow = NetRoomFlow.Instance;
            if (flow != null && flow.IsInRoom(room))
            {
                WndHub.Tip?.Creat(new TipWndInfo
                {
                    title = "已经在这个房间里",
                    desc = flow.IsHostOfRoom(room)
                        ? "这个房间是你自己开的，直接按「准备」就能开局。"
                        : "你已经在这个房间里了，不用再加入一次。",
                });
                return;
            }

            if (RoomMeta.InGame(room)) return; // 已开局：不给进

            // 无密码：直接抛给上层
            if (!room.PasswordProtected)
            {
                OnRoomActivated?.Invoke(room, string.Empty);
                return;
            }

            // ⚠ 真实密码**不在广播里**（LanRoomInfo 只有 PasswordProtected 这个 bool）⇒ 只能让玩家手输
            var wnd = WndHub.Password;
            if (wnd == null)
            {
                WndHub.Tip?.Creat(new TipWndInfo
                {
                    title = "需要密码",
                    desc = "该房间设有密码，但密码输入窗未加载（场景里缺 PasswordWnd）。",
                });
                return;
            }

            wnd.Creat(new PasswordWndInfo
            {
                title = "输入房间密码",
                desc = string.IsNullOrEmpty(RoomMeta.RoomName(room)) ? "该房间设有密码" : "「" + RoomMeta.RoomName(room) + "」设有密码",
                placeholder = "密码",
                maxLength = 32,
                isPassword = true,
                onConfirm = pwd => OnRoomActivated?.Invoke(room, pwd),
            });
        }

        #endregion

        #region 数据映射

        // ============================================================================
        // 以下"取值"全部转发给 FPSGame.Net.RoomMeta —— 房间字段的**唯一适配点**
        // （缺省值归一 + 空引用保护）。库 2026-10-08 起已在广播里带上
        // Difficulty / TaskMain / InGame / Source。
        // ============================================================================

        /// <summary>
        /// 房间的联机类型：来源是局域网广播 ⇒ <see cref="NetTypeEnum.Lan"/>，否则 ⇒ <see cref="NetTypeEnum.Server"/>。
        /// </summary>
        private static NetTypeEnum RoomNetType(LanRoomInfo room)
        {
            return RoomMeta.Source(room) == RoomMeta.SourceEnum.LanBroadcast
                ? NetTypeEnum.Lan
                : NetTypeEnum.Server;
        }

        /// <summary>
        /// 房间地图 → <see cref="MapData_SO"/>（同时接受字典键与 <c>AreaName</c> 两种写法）。
        /// 返回 null = 这张地图在当前配置里没有资料 ⇒ 行里据此**隐藏地图背景图与图标**。
        /// </summary>
        private static MapData_SO FindMapData(LanRoomInfo room)
        {
            string mapName = RoomMeta.MapName(room);
            if (string.IsNullOrEmpty(mapName)) return null;

            var taskMgr = TaskManager.Instance;
            if (taskMgr == null || taskMgr.MapData == null) return null;

            MapData_SO data;
            if (taskMgr.MapData.TryGetValue(mapName, out data)) return data;

            foreach (var kv in taskMgr.MapData)
            {
                if (kv.Value != null && kv.Value.AreaName == mapName) return kv.Value;
            }
            return null;
        }

        #endregion

        #region 筛选栏与状态

        private void RefreshFilterVisual()
        {
            SetText(mapValue, GetMapLabel(_mapIndex));
            SetText(diffValue, GetDiffLabel(_diffIndex));
            SetText(countValue, _countIndex == 1 ? NotFullLabel : AnyCountLabel);
            SetText(netTypeValue, GetNetTypeLabel(_netTypeIndex));

            SetColor(mapValue, _mapIndex > 0 ? AccentColor : TextDim);
            SetColor(diffValue, _diffIndex > 0 ? AccentColor : TextDim);
            SetColor(countValue, _countIndex > 0 ? AccentColor : TextDim);
            SetColor(netTypeValue, _netTypeIndex > 0 ? AccentColor : TextDim);

            RefreshMapItemVisual();
            HighlightItems(_diffItems, _diffIndex);
            HighlightItems(_countItems, _countIndex);
            HighlightItems(_netTypeItems, _netTypeIndex);
        }

        /// <summary>选中项把 Checkmark 打开、未选中关掉。</summary>
        private static void HighlightItems(List<Transform> items, int selected)
        {
            for (int i = 0; i < items.Count; ++i)
            {
                bool on = i == selected;
                var name = items[i].Find("Name");
                if (name != null) SetColor(name, on ? TextMain : TextDim);

                var check = items[i].Find("Background/Checkmark");
                if (check != null) SetActive(check, on);
            }
        }

        /// <summary>
        /// 地图项的外观——**全部按 <see cref="MapData_SO"/> 的内容配置**：
        /// <list type="bullet">
        ///   <item>名称：<c>AreaName</c>（空则退回字典键），文字颜色 = <c>color</c>；</item>
        ///   <item>背景图：<c>bg</c> 节点的 Image，贴图 = <c>AreaBackground</c>（没有就依次退回 <c>Map</c> / <c>Icon</c>）；</item>
        ///   <item>选择框 <c>Background</c> 也用 <c>color</c> 染色，选中时更亮、未选中压暗。</item>
        /// </list>
        /// <para>第 0 项「任意地图」没有数据 ⇒ 隐藏背景图、文字用默认色。
        /// 不走 <see cref="HighlightItems"/> 是因为它会把名称统一改成 TextMain/TextDim，会盖掉地图主题色。</para>
        /// </summary>
        private void RefreshMapItemVisual()
        {
            for (int i = 0; i < _mapItems.Count; ++i)
            {
                var item = _mapItems[i];
                if (item == null) continue;

                bool selected = i == _mapIndex;
                var data = i >= 0 && i < _mapData.Count ? _mapData[i] : null;

                var name = item.Find("Name");
                var bg = FindMapItemBg(item);
                var frame = item.Find("Background");
                var check = item.Find("Background/Checkmark");
                if (check != null) SetActive(check, selected);

                if (data == null)   // 任意地图
                {
                    if (bg != null) SetActive(bg, false);
                    if (name != null)
                    {
                        SetText(name, AnyMapLabel);
                        SetColor(name, selected ? TextMain : TextDim);
                    }
                    if (frame != null) SetColor(frame, selected ? TextMain : TextDim);
                    continue;
                }

                if (bg != null)
                {
                    var sprite = PickMapImage(data);
                    SetActive(bg, sprite != null);
                    SetSprite(bg, sprite);
                }
                // 名称 = AreaName、颜色 = 地图主题色（选中不透明 / 未选中压暗，保留色相）
                if (name != null)
                {
                    SetText(name, MapLabelOf(data));
                    SetColor(name, WithAlpha(data.color, selected ? 1f : 0.72f));
                }
                if (frame != null) SetColor(frame, WithAlpha(data.color, selected ? 1f : 0.5f));
            }
        }

        /// <summary>
        /// 地图项的**背景图节点**。⚠ 预制体里它是 <c>Mask/bg</c>（外面套一层 Mask 把 380×214 的图裁成项高，
        /// 见 <c>FilterMapItem</c> 层级），所以不能直接 <c>item.Find("bg")</c>；这里带一层兼容。
        /// </summary>
        private static Transform FindMapItemBg(Transform item)
        {
            var bg = item.Find("Mask/bg");
            return bg != null ? bg : item.Find("bg");
        }

        /// <summary>地图项的显示名：<c>AreaName</c> → 没有就退回地图键（<c>"未命名"</c>兜底）。</summary>
        private static string MapLabelOf(MapData_SO data)
        {
            if (data == null) return AnyMapLabel;
            if (!string.IsNullOrEmpty(data.AreaName)) return data.AreaName;
            return data.name;
        }

        /// <summary>地图项的背景图：优先 <c>AreaBackground</c>（区域背景），没有则 <c>Map</c>（地图图），再退 <c>Icon</c>。</summary>
        private static Sprite PickMapImage(MapData_SO data)
        {
            if (data == null) return null;
            if (data.AreaBackground != null) return data.AreaBackground;
            if (data.Map != null) return data.Map;
            return data.Icon;
        }

        private static Color WithAlpha(Color color, float alpha)
        {
            color.a = alpha;
            return color;
        }

        private static void PlayClick()
        {
            if (WndManager.Instance == null) return;
            WndManager.Instance.PlaySound(new AudioPlayInfo("UI/UI_Bubble"));
        }

        #endregion
    }
}
