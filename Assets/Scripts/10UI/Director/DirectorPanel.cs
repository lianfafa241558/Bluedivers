using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using FPSGame.Data;
using FPSGame.Effect;

namespace FPSGame.UI
{
/// <summary>
/// PV 导演面板（纯代码构建）。
/// <para>▍为什么不搭 prefab：FreeCameraWnd.prefab 里有 <c>_groundLayer</c> 等已配好的字段，
/// MCP 存 prefab 会按内存旧值回写、把它们清掉（已实测）；面板全代码构建 ⇒ prefab 零改动。</para>
/// </summary>
[AddComponentMenu("UI/导演面板")]
public class DirectorPanel : MonoBehaviour
{
    private const float PanelWidth = 380f;
    private const float RowHeight = 30f;

    private const float MinBlendSeconds = 0.05f;
    private const float MaxBlendSeconds = 1.5f;
    private const float DefaultBlendSeconds = 0.35f;

    private static readonly Color PanelColor = new(0.04f, 0.05f, 0.08f, 0.88f);
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

    private readonly Image[] _shotImages = new Image[9];
    private readonly List<Row> _unitRows = new();
    private readonly List<Row> _airdropRows = new();

    private TextMeshProUGUI _status;
    private TextMeshProUGUI _pauseLabel;
    private Slider _blendSlider;
    private TextMeshProUGUI _blendLabel;
    private TMP_FontAsset _font;
    private RectTransform _root;

    private DirectorUnitEntry _selectedUnit;
    private AirdropData_SO _selectedAirdrop;

    public DirectorUnitEntry SelectedUnit => _selectedUnit;
    public AirdropData_SO SelectedAirdrop => _selectedAirdrop;

    private sealed class Row
    {
        public Image background;
        public object data;
    }

    #region 构建

    public void Build(Transform parent, TMP_FontAsset font, List<DirectorUnitEntry> units, IEnumerable<AirdropData_SO> airdrops)
    {
        _font = font;

        _root = CreateRect("DirectorPanel", parent);
        _root.anchorMin = new Vector2(0f, 0f);
        _root.anchorMax = new Vector2(0f, 1f);
        _root.pivot = new Vector2(0f, 0.5f);
        _root.sizeDelta = new Vector2(PanelWidth, 0f);
        _root.anchoredPosition = Vector2.zero;
        _root.SetAsLastSibling();

        var bg = _root.gameObject.AddComponent<Image>();
        bg.color = PanelColor;
        bg.raycastTarget = true;

        var layout = _root.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(10, 10, 10, 10);
        layout.spacing = 6f;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        BuildHeader();
        BuildRateRow();
        BuildShotGrid();
        BuildUnitList(units);
        BuildAirdropList(airdrops);
        BuildLegend();
    }

    private void BuildHeader()
    {
        var title = CreateText("Title", _root, "PV 导演", 22, TextAlignmentOptions.MidlineLeft);
        AddHeight(title.gameObject, 28f);

        _status = CreateText("Status", _root, string.Empty, 15, TextAlignmentOptions.TopLeft);
        _status.color = HintColor;
        AddHeight(_status.gameObject, 86f);
    }

    private void BuildRateRow()
    {
        var row = CreateRect("RateRow", _root);
        AddHeight(row.gameObject, 28f);

        var layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = 4f;
        layout.childControlWidth = true;
        // 高度不交给 HLG：它自己也是 ILayoutElement，会和外层 LayoutElement 抢着决定行高（实测被放大到 158px）
        layout.childControlHeight = false;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        CreateButton(row, "更慢 [", () => SlowerClicked?.Invoke());
        var pause = CreateButton(row, "暂停 J", () => PauseClicked?.Invoke());
        _pauseLabel = pause.GetComponentInChildren<TextMeshProUGUI>();
        CreateButton(row, "更快 ]", () => FasterClicked?.Invoke());
    }

    private void BuildShotGrid()
    {
        // 机位标题 + 过渡速度滑动条（左慢右快），实际时长还会被相机移动速度缩放
        var row = CreateRect("ShotRow", _root);
        AddHeight(row.gameObject, 24f);

        var rowLayout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
        rowLayout.spacing = 6f;
        rowLayout.childControlWidth = true;
        rowLayout.childControlHeight = false;
        rowLayout.childForceExpandWidth = false;
        rowLayout.childForceExpandHeight = false;

        var label = CreateText("ShotLabel", row, "机位", 14, TextAlignmentOptions.MidlineLeft);
        label.color = HintColor;
        AddWidth(label.gameObject, 34f);

        _blendSlider = CreateSlider(row, 0f, 1f, SecondsToSlider(DefaultBlendSeconds), OnBlendSliderChanged);
        AddWidth(_blendSlider.gameObject, 140f, 1f);

        _blendLabel = CreateText("BlendLabel", row, $"{DefaultBlendSeconds:0.00}s", 13, TextAlignmentOptions.MidlineRight);
        _blendLabel.color = HintColor;
        AddWidth(_blendLabel.gameObject, 64f);

        // 2 行 × 5 列（9 个机位占不满末格，省一行高度给列表）
        var grid = CreateRect("ShotGrid", _root);
        AddHeight(grid.gameObject, 64f);

        var layout = grid.gameObject.AddComponent<GridLayoutGroup>();
        layout.cellSize = new Vector2(68f, 30f);
        layout.spacing = new Vector2(4f, 4f);
        layout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        layout.constraintCount = 5;

        for (int i = 0; i < 9; i++)
        {
            int index = i;
            var btn = CreateButton(grid, (i + 1).ToString(), () => ShotClicked?.Invoke(index));
            _shotImages[i] = btn.GetComponent<Image>();
        }
    }

    private void BuildUnitList(List<DirectorUnitEntry> units)
    {
        var label = CreateText("UnitLabel", _root, "单位（拖到场景放置）", 14, TextAlignmentOptions.MidlineLeft);
        label.color = HintColor;
        AddHeight(label.gameObject, 20f);

        var scroll = CreateScroll(_root, out var content);
        if (units == null || units.Count == 0)
        {
            CreateText("Empty", content, "未配置 GameData/Director/DirectorUnitData", 14, TextAlignmentOptions.MidlineLeft).color = HintColor;
            return;
        }

        for (int i = 0; i < units.Count; i++)
        {
            var entry = units[i];
            if (entry == null || entry.prefab == null) continue;

            string name = string.IsNullOrEmpty(entry.showName) ? entry.prefab.name : entry.showName;
            _unitRows.Add(CreateRow(content, $"[{CategoryTag(entry.category)}] {name}", entry, OnUnitRowClicked));
        }

        scroll.verticalNormalizedPosition = 1f;
    }

    private void BuildAirdropList(IEnumerable<AirdropData_SO> airdrops)
    {
        var label = CreateText("AirdropLabel", _root, "战备（拖到场景释放）", 14, TextAlignmentOptions.MidlineLeft);
        label.color = HintColor;
        AddHeight(label.gameObject, 20f);

        var scroll = CreateScroll(_root, out var content);
        if (airdrops == null)
        {
            CreateText("Empty", content, "ResSvc 未就绪", 14, TextAlignmentOptions.MidlineLeft).color = HintColor;
            return;
        }

        foreach (var data in airdrops)
        {
            if (data == null || data.isHide) continue;
            string name = string.IsNullOrEmpty(data.showName) ? data.name : data.showName;
            _airdropRows.Add(CreateRow(content, $"{data.ID}  {name}", data, OnAirdropRowClicked));
        }

        scroll.verticalNormalizedPosition = 1f;
    }

    private void BuildLegend()
    {
        var legend = CreateText("Legend", _root,
            "Tab 解锁鼠标 / F 回自由飞行 / 1-9 跳机位\n" +
            "Alt+1-9 存机位 · J 暂停 · , 单帧 · [ ] 倍率\n" +
            "中键 微调镜头 · 拖列表到场景 · H 显隐",
            12, TextAlignmentOptions.TopLeft);
        legend.color = HintColor;
        AddHeight(legend.gameObject, 58f);
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

        for (int i = 0; i < _shotImages.Length; i++)
        {
            if (_shotImages[i] == null) continue;
            _shotImages[i].color = mode.HasShot(i) ? ShotReadyColor : ShotEmptyColor;
        }
    }

    private static string UnitName(DirectorUnitEntry entry) =>
        entry == null ? "—" : (string.IsNullOrEmpty(entry.showName) ? entry.prefab.name : entry.showName);

    private static string AirdropName(AirdropData_SO data) =>
        data == null ? "—" : $"{data.ID} {data.showName}";

    public void SetVisible(bool visible)
    {
        if (_root != null) _root.gameObject.SetActive(visible);
    }

    public bool Visible => _root != null && _root.gameObject.activeSelf;

    private void OnUnitRowClicked(object data)
    {
        _selectedUnit = data as DirectorUnitEntry;
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

    private static string CategoryTag(DirectorUnitCategory category) => category switch
    {
        DirectorUnitCategory.Friend => "友",
        DirectorUnitCategory.Vehicle => "载",
        DirectorUnitCategory.Prop => "物",
        _ => "敌",
    };

    #endregion

    #region 构件

    private static RectTransform CreateRect(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }

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

    private Button CreateButton(Transform parent, string label, Action onClick)
    {
        var go = new GameObject("Btn_" + label, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);

        // 新 RectTransform 默认 sizeDelta=(100,100)。HorizontalLayoutGroup 没有 LayoutElement 时会拿它当行高，
        // 于是整行被撑到 100px+（实测 RateRow 100.5 / 158）。用 LayoutElement 钉死高度才不会被父级反馈放大。
        ((RectTransform)go.transform).sizeDelta = new Vector2(0f, 28f);
        var btnLayout = go.AddComponent<LayoutElement>();
        btnLayout.minHeight = 28f;
        btnLayout.preferredHeight = 28f;
        btnLayout.flexibleHeight = 0f;

        var img = go.GetComponent<Image>();
        img.color = RowColor;

        var btn = go.GetComponent<Button>();
        btn.targetGraphic = img;
        btn.onClick.AddListener(() => onClick?.Invoke());

        var text = CreateText("Text", go.transform, label, 16, TextAlignmentOptions.Center);
        var rt = text.rectTransform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(4f, 0f);
        rt.offsetMax = new Vector2(-4f, 0f);
        return btn;
    }

    private ScrollRect CreateScroll(Transform parent, out RectTransform content)
    {
        var go = new GameObject("Scroll", typeof(RectTransform), typeof(Image), typeof(ScrollRect));
        go.transform.SetParent(parent, false);
        go.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.3f);

        var viewport = CreateRect("Viewport", go.transform);
        viewport.anchorMin = Vector2.zero;
        viewport.anchorMax = Vector2.one;
        viewport.offsetMin = new Vector2(2f, 2f);
        viewport.offsetMax = new Vector2(-2f, -2f);
        viewport.gameObject.AddComponent<RectMask2D>();

        var contentGo = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        contentGo.transform.SetParent(viewport, false);
        content = (RectTransform)contentGo.transform;
        content.anchorMin = new Vector2(0f, 1f);
        content.anchorMax = new Vector2(1f, 1f);
        content.pivot = new Vector2(0.5f, 1f);
        content.anchoredPosition = Vector2.zero;
        content.sizeDelta = Vector2.zero;

        var vlg = contentGo.GetComponent<VerticalLayoutGroup>();
        vlg.spacing = 2f;
        vlg.childControlWidth = true;
        vlg.childControlHeight = true;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;

        var fitter = contentGo.GetComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        var scroll = go.GetComponent<ScrollRect>();
        scroll.viewport = viewport;
        scroll.content = content;
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 30f;

        var le = go.AddComponent<LayoutElement>();
        le.minHeight = 120f;
        le.preferredHeight = 160f;
        le.flexibleHeight = 1f;
        return scroll;
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

    private static void AddHeight(GameObject go, float height)
    {
        var le = go.GetComponent<LayoutElement>();
        if (le == null) le = go.AddComponent<LayoutElement>();
        le.minHeight = height;
        le.preferredHeight = height;
    }

    private static void AddWidth(GameObject go, float width, float flexible = -1f)
    {
        var le = go.GetComponent<LayoutElement>();
        if (le == null) le = go.AddComponent<LayoutElement>();
        le.minWidth = width;
        le.preferredWidth = width;
        if (flexible >= 0f) le.flexibleWidth = flexible;
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

    /// <summary>uGUI 滑动条的最小可用结构：背景 + Handle（不画 Fill，够用且省节点）。</summary>
    private Slider CreateSlider(Transform parent, float min, float max, float value, Action<float> onChange)
    {
        var go = new GameObject("Slider", typeof(RectTransform), typeof(Slider));
        go.transform.SetParent(parent, false);
        ((RectTransform)go.transform).sizeDelta = new Vector2(150f, 18f);

        var bg = CreateRect("Background", go.transform);
        bg.anchorMin = new Vector2(0f, 0.5f);
        bg.anchorMax = new Vector2(1f, 0.5f);
        bg.sizeDelta = new Vector2(0f, 6f);
        // 底色别太暗：深色面板上 0.16 几乎看不见轨道（实测截图里只剩一个手柄在飘）
        bg.gameObject.AddComponent<Image>().color = new Color(0.32f, 0.36f, 0.42f, 1f);

        var area = CreateRect("HandleSlideArea", go.transform);
        area.anchorMin = Vector2.zero;
        area.anchorMax = Vector2.one;
        area.offsetMin = new Vector2(7f, 0f);
        area.offsetMax = new Vector2(-7f, 0f);

        var handle = CreateRect("Handle", area);
        handle.sizeDelta = new Vector2(14f, 14f);
        var handleImage = handle.gameObject.AddComponent<Image>();
        handleImage.color = new Color(0.35f, 0.95f, 1f, 1f);

        var slider = go.GetComponent<Slider>();
        slider.targetGraphic = handleImage;
        slider.handleRect = handle;
        slider.direction = Slider.Direction.LeftToRight;
        // 不参与键盘导航：否则 Tab/方向键会选到它
        slider.navigation = new Navigation { mode = Navigation.Mode.None };
        slider.minValue = min;
        slider.maxValue = max;
        slider.value = value;
        slider.onValueChanged.AddListener(v => onChange(v));

        // ⚠ uGUI 的 Slider 一旦被点中就会 Select，之后会吃 "Horizontal" 轴 —— 项目里 A/D 就是 Horizontal
        //   ⇒ 不取消选中的话，拖完滑条再按 A/D 走动会顺手改过渡时长。松手即清选中。
        var trigger = go.AddComponent<EventTrigger>();
        var pointerUp = new EventTrigger.Entry { eventID = EventTriggerType.PointerUp };
        pointerUp.callback.AddListener(_ => ClearSelection());
        trigger.triggers.Add(pointerUp);
        return slider;
    }

    private static void ClearSelection()
    {
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
    }

    #endregion
}
}
