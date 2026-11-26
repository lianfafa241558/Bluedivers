using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using FPSGame.Core;
using FPSGame.Data;
using FPSGame.Effect;

namespace FPSGame.UI
{
/// <summary>
/// PV 导演面板。
/// <para>▍静态结构在 <c>FreeCameraWnd.prefab</c>（布局/配色/字号直接在 Inspector 调），本类只做绑定与刷新；
/// 单位/战备列表是数据驱动的，行由代码建到 prefab 里的 Content 下。</para>
/// </summary>
[AddComponentMenu("UI/导演面板")]
public class DirectorPanel : MonoBehaviour
{
    private const float RowHeight = 30f;
    private const float TabHeight = 22f;

    private const float MinBlendSeconds = 0.05f;
    private const float MaxBlendSeconds = 1.5f;
    private const float DefaultBlendSeconds = 0.35f;

    private static readonly Color RowColor = new(0.13f, 0.15f, 0.2f, 0.95f);
    private static readonly Color RowSelectedColor = new(0.18f, 0.58f, 0.75f, 1f);
    private static readonly Color ShotEmptyColor = new(0.18f, 0.18f, 0.22f, 0.9f);
    private static readonly Color ShotReadyColor = new(0.14f, 0.38f, 0.5f, 0.95f);
    private static readonly Color TextColor = new(0.85f, 0.92f, 0.96f, 1f);
    private static readonly Color HintColor = new(0.62f, 0.7f, 0.76f, 1f);

    public Action<int> ShotClicked;
    public Action PauseClicked;
    public Action SlowerClicked;
    public Action FasterClicked;
    // 生成/释放/清除的按钮行已删除：交互统一走"从列表拖到场景"

    /// <summary>机位过渡的基准时长被滑动条改动（秒）。</summary>
    public Action<float> BlendChanged;

    /// <summary>列表行被拖出：参数 = (条目数据, 屏幕坐标)。由窗口接到 <c>DirectorMode</c> 的拖拽放置。</summary>
    public Action<object, Vector2> RowDragBegan;
    public Action<object, Vector2> RowDragging;
    public Action<object, Vector2> RowDragEnded;

    [InspectorName("状态栏")][SerializeField] private TextMeshProUGUI _status;
    [InspectorName("暂停按钮文字")][SerializeField] private TextMeshProUGUI _pauseLabel;
    [InspectorName("倍率按钮：更慢/暂停/更快")][SerializeField] private Button[] _rateButtons;
    [InspectorName("机位按钮 1-9")][SerializeField] private Button[] _shotButtons;
    [InspectorName("过渡时长滑动条")][SerializeField] private Slider _blendSlider;
    [InspectorName("过渡时长数值")][SerializeField] private TextMeshProUGUI _blendLabel;
    [InspectorName("单位类型按钮区")][SerializeField] private RectTransform _unitTabs;
    [InspectorName("单位列表 Content")][SerializeField] private RectTransform _unitContent;
    [InspectorName("战备列表 Content")][SerializeField] private RectTransform _airdropContent;

    private readonly List<Row> _unitRows = new();
    private readonly List<Row> _airdropRows = new();
    private readonly List<Image> _tabImages = new();
    /// <summary>按钮顺序 → 分组下标（空分组被跳过，两者不再一一对应）。</summary>
    private readonly List<int> _tabGroups = new();

    private List<DirectorUnitGroup> _groups;

    private Image[] _shotImages;
    private TMP_FontAsset _font;
    private RectTransform _root;

    private GameObject _selectedUnit;
    private AirdropData_SO _selectedAirdrop;

    public GameObject SelectedUnit => _selectedUnit;
    public AirdropData_SO SelectedAirdrop => _selectedAirdrop;

    private sealed class Row
    {
        public Image background;
        public object data;
        /// <summary>所属分组下标：切组时靠 SetActive 过滤，行本身不销毁重建。</summary>
        public int group;
    }

    #region 绑定

    /// <summary>绑定 prefab 控件并填充两个列表。字体取 prefab 文本上已配好的，不再运行时找字体资源。</summary>
    public void Bind(List<DirectorUnitGroup> groups, IEnumerable<AirdropData_SO> airdrops)
    {
        // prefab 是策划/美术可改的，引用漏配就当"面板不可用"早退，别让后面 Refresh 一片空引用
        if (_status == null || _shotButtons == null || _unitTabs == null || _unitContent == null || _airdropContent == null)
        {
            Debug.LogError("[导演] FreeCameraWnd.prefab 的 DirectorPanel 引用未配全，面板不生效");
            return;
        }

        _root = (RectTransform)transform;
        _font = _status.font;

        if (_rateButtons != null && _rateButtons.Length == 3)
        {
            _rateButtons[0].onClick.AddListener(() => SlowerClicked?.Invoke());
            _rateButtons[1].onClick.AddListener(() => PauseClicked?.Invoke());
            _rateButtons[2].onClick.AddListener(() => FasterClicked?.Invoke());
        }

        _shotImages = new Image[_shotButtons.Length];
        for (int i = 0; i < _shotButtons.Length; i++)
        {
            int index = i;
            var btn = _shotButtons[i];
            if (btn == null) continue;
            btn.onClick.AddListener(() => ShotClicked?.Invoke(index));
            _shotImages[i] = btn.targetGraphic as Image;
        }

        if (_blendSlider != null)
        {
            _blendSlider.SetValueWithoutNotify(SecondsToSlider(DefaultBlendSeconds));
            _blendSlider.onValueChanged.AddListener(OnBlendSliderChanged);

            // ⚠ uGUI 的 Slider 一旦被点中就会 Select，之后会吃 "Horizontal" 轴 —— 项目里 A/D 就是 Horizontal
            //   ⇒ 不取消选中的话，拖完滑条再按 A/D 走动会顺手改过渡时长。松手即清选中。
            var trigger = _blendSlider.GetComponent<EventTrigger>();
            if (trigger == null) trigger = _blendSlider.gameObject.AddComponent<EventTrigger>();
            var pointerUp = new EventTrigger.Entry { eventID = EventTriggerType.PointerUp };
            pointerUp.callback.AddListener(_ => ClearSelection());
            trigger.triggers.Add(pointerUp);
        }

        BuildUnits(groups);
        FillAirdrops(airdrops);
    }

    /// <summary>顶部按"分组"生成切换按钮，列表行一次性全建好、靠 SetActive 切组（VLG 忽略 inactive，不留空档）。</summary>
    private void BuildUnits(List<DirectorUnitGroup> groups)
    {
        _groups = groups;
        if (groups == null || groups.Count == 0)
        {
            CreateText("Empty", _unitContent, "未配置 GameData/Director/DirectorUnitData", 14, TextAlignmentOptions.MidlineLeft).color = HintColor;
            return;
        }

        for (int g = 0; g < groups.Count; g++)
        {
            if (!HasUnits(groups[g])) continue;   // 空分组不建按钮，免得顶上挂一排点开是空的分组
            CreateTab(g, groups[g]);

            for (int i = 0; i < groups[g].units.Count; i++)
            {
                var prefab = groups[g].units[i];
                if (prefab == null) continue;

                var row = CreateRow(_unitContent, RowText(prefab), prefab, OnUnitRowClicked);
                row.group = g;
                _unitRows.Add(row);
            }
        }

        if (_unitRows.Count == 0)
        {
            CreateText("Empty", _unitContent, "单位表里还没挂 prefab", 14, TextAlignmentOptions.MidlineLeft).color = HintColor;
            return;
        }

        SelectGroup(_tabGroups[0]);
    }

    private static bool HasUnits(DirectorUnitGroup group)
    {
        if (group.units == null) return false;
        for (int i = 0; i < group.units.Count; i++)
            if (group.units[i] != null) return true;
        return false;
    }

    private void CreateTab(int index, DirectorUnitGroup group)
    {
        var go = new GameObject("Tab_" + index, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(_unitTabs, false);

        var img = go.GetComponent<Image>();
        img.color = RowColor;

        var btn = go.GetComponent<Button>();
        btn.targetGraphic = img;
        btn.onClick.AddListener(() => SelectGroup(index));

        // ⚠ 这两行别删：UnitTabs 的 HLG 开了 childControlHeight，没有 LayoutElement 行高会塌成 0；
        //   而高度失控的真正原因是 HLG 的 Child Force Expand Height（会让 LayoutGroup 自报 flexibleHeight≥1）。
        ((RectTransform)go.transform).sizeDelta = new Vector2(0f, TabHeight);
        var le = go.AddComponent<LayoutElement>();
        le.minHeight = TabHeight;
        le.preferredHeight = TabHeight;
        le.flexibleHeight = 0f;
        le.flexibleWidth = 1f;

        var label = CreateText("Text", go.transform, string.IsNullOrEmpty(group.name) ? "未命名" : group.name, 13, TextAlignmentOptions.Center);
        var rt = label.rectTransform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(2f, 0f);
        rt.offsetMax = new Vector2(-2f, 0f);

        _tabImages.Add(img);
        _tabGroups.Add(index);
    }

    /// <summary>行文字 = 单位名（<see cref="BaseObject.ShowName"/>）+ 预制体名，用来区分同名模型的不同 prefab。</summary>
    private static string RowText(GameObject prefab)
    {
        var baseObject = prefab.GetComponent<BaseObject>();
        string showName = baseObject != null ? baseObject.ShowName : null;
        if (string.IsNullOrEmpty(showName) || showName == prefab.name) return prefab.name;
        return $"{showName}（{prefab.name}）";
    }

    private void SelectGroup(int index)
    {
        if (_groups == null || index < 0 || index >= _groups.Count) return;

        for (int i = 0; i < _tabImages.Count; i++)
            if (_tabImages[i] != null) _tabImages[i].color = _tabGroups[i] == index ? RowSelectedColor : RowColor;

        for (int i = 0; i < _unitRows.Count; i++)
            if (_unitRows[i].background != null)
                _unitRows[i].background.gameObject.SetActive(_unitRows[i].group == index);
    }

    private void FillAirdrops(IEnumerable<AirdropData_SO> airdrops)
    {
        if (airdrops == null)
        {
            CreateText("Empty", _airdropContent, "ResSvc 未就绪", 14, TextAlignmentOptions.MidlineLeft).color = HintColor;
            return;
        }

        foreach (var data in airdrops)
        {
            if (data == null || data.isHide) continue;
            string name = string.IsNullOrEmpty(data.showName) ? data.name : data.showName;
            _airdropRows.Add(CreateRow(_airdropContent, $"{data.ID}  {name}", data, OnAirdropRowClicked));
        }
    }

    #endregion

    #region 刷新

    public void Refresh(DirectorMode mode)
    {
        if (_status == null || mode == null) return;

        string state = mode.State switch
        {
            DirectorState.Pointer => mode.IsPlacing ? "拖拽放置中" : "选点（相机锁定）",
            DirectorState.Shot => "固定机位（F 回飞行）",
            _ => "自由飞行",
        };
        string rate = mode.Paused ? "暂停" : $"{mode.Rate:0.###}x";
        string marker = mode.HasMarker
            ? $"({mode.MarkerPoint.x:F1}, {mode.MarkerPoint.y:F1}, {mode.MarkerPoint.z:F1})"
            : "未设置";

        _status.text = $"状态：{state}    倍率：{rate}\n落点：{marker}\n" +
                       $"选中单位：{UnitName(_selectedUnit)}\n选中战备：{AirdropName(_selectedAirdrop)}";

        if (_pauseLabel != null) _pauseLabel.text = mode.Paused ? "继续 J" : "暂停 J";
        // 显示"实际"过渡时长（已被移动速度缩放），拖滑动条时看到的就是最终生效值
        if (_blendLabel != null) _blendLabel.text = $"{mode.BlendDuration:0.00}s";

        if (_shotImages == null) return;
        for (int i = 0; i < _shotImages.Length; i++)
        {
            if (_shotImages[i] == null) continue;
            _shotImages[i].color = mode.HasShot(i) ? ShotReadyColor : ShotEmptyColor;
        }
    }

    private static string UnitName(GameObject prefab) =>
        prefab == null ? "—" : prefab.name;

    private static string AirdropName(AirdropData_SO data) =>
        data == null ? "—" : $"{data.ID} {data.showName}";

    public void SetVisible(bool visible)
    {
        if (_root != null) _root.gameObject.SetActive(visible);
    }

    public bool Visible => _root != null && _root.gameObject.activeSelf;

    private void OnUnitRowClicked(object data)
    {
        _selectedUnit = data as GameObject;
        RefreshRows(_unitRows, _selectedUnit);
    }

    private void OnAirdropRowClicked(object data)
    {
        _selectedAirdrop = data as AirdropData_SO;
        RefreshRows(_airdropRows, _selectedAirdrop);
    }

    private static void RefreshRows(List<Row> rows, object selected)
    {
        for (int i = 0; i < rows.Count; i++)
        {
            if (rows[i].background == null) continue;
            rows[i].background.color = ReferenceEquals(rows[i].data, selected) ? RowSelectedColor : RowColor;
        }
    }

    #endregion

    #region 构件（只有数据驱动的列表行还在运行时建）

    private TextMeshProUGUI CreateText(string name, Transform parent, string text, float size, TextAlignmentOptions align)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);

        var tmp = go.AddComponent<TextMeshProUGUI>();
        if (_font != null) tmp.font = _font;
        tmp.text = text;
        tmp.fontSize = size;
        tmp.color = TextColor;
        tmp.alignment = align;
        tmp.raycastTarget = false;
        return tmp;
    }

    private Row CreateRow(Transform content, string label, object data, Action<object> onSelect)
    {
        var go = new GameObject("Row", typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(content, false);

        var img = go.GetComponent<Image>();
        img.color = RowColor;

        var btn = go.GetComponent<Button>();
        btn.targetGraphic = img;
        btn.onClick.AddListener(() => onSelect(data));

        // 同一行既能点选、也能拖到场景里放置（Unity 场景视图那种手感）
        var drag = go.AddComponent<DirectorDragItem>();
        drag.Data = data;
        drag.DragBegan = (item, pos) => RowDragBegan?.Invoke(item.Data, pos);
        drag.Dragging = (item, pos) => RowDragging?.Invoke(item.Data, pos);
        drag.DragEnded = (item, pos) => RowDragEnded?.Invoke(item.Data, pos);

        var le = go.AddComponent<LayoutElement>();
        le.minHeight = RowHeight;
        le.preferredHeight = RowHeight;

        var text = CreateText("Text", go.transform, label, 15, TextAlignmentOptions.MidlineLeft);
        var rt = text.rectTransform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(8f, 0f);
        rt.offsetMax = new Vector2(-8f, 0f);

        return new Row { background = img, data = data };
    }

    /// <summary>秒数 → 滑动条值（左端最慢、右端最快，符合"往右拖更快"的直觉）。</summary>
    private static float SecondsToSlider(float seconds)
        => (MaxBlendSeconds - seconds) / (MaxBlendSeconds - MinBlendSeconds);

    private void OnBlendSliderChanged(float value)
    {
        float seconds = Mathf.Lerp(MaxBlendSeconds, MinBlendSeconds, value);
        BlendChanged?.Invoke(seconds);
        if (_blendLabel != null) _blendLabel.text = $"{seconds:0.00}s";
    }

    private static void ClearSelection()
    {
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
    }

    #endregion
}
}
