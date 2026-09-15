using System.Collections.Generic;
using Core;
using FPSGame.Attribute;
using UnityEngine;
using UnityEngine.UI;
using Utils;
using static WndTools.WndRootTool;

/// <summary>
/// 战备配置界面（船舰管理 - 战备配置）
/// 左侧为按类型分组的战备列表；右上展示战备模型（鼠标左右拖动旋转）；
/// 右下展示购买价格 / 已拥有状态，并额外提供偏好切换按钮。
///
/// 预制体节点约定：
///   _listContent   左侧列表内容容器（挂 VerticalLayoutGroup + ContentSizeFitter）
///   _groupPrefab   分组表头  子0=标题文本  子1(可选)=数量文本
///   _itemPrefab    战备条目  子0=图标      子1(可选)=选中框
///   _modelView     右上模型显示 RawImage（贴图由脚本赋值为运行时 RenderTexture）
///   _modelCamera   展示用相机（CullingMask 只勾选展示层，TargetTexture 由脚本赋值）
///   _modelRoot     模型挂点，相机对准此节点；拖动旋转的也是此节点
///   _costRoot      价格条目容器，每个子物件：子0=资源图标  子1=数量文本
///   _traitText     战备特性（由 subAirdrop 附属战备名称拼接）
/// </summary>
public class AirdropConfigWnd : Window
{
    /// <summary>模型每像素旋转角度</summary>
    private const float ModelRotateSpeed = 0.3f;
    /// <summary>模型展示用的渲染纹理尺寸</summary>
    private const int ModelViewSize = 1024;

    [Foldout("配置", true)]
    [SerializeField]
    private Transform _closeButton, _listContent, _nameText, _typeText, _descText, _traitText,
        _attrNameText, _attrValueText, _costRoot, _buyButton, _buyButtonText, _preferButton, _preferButtonText;

    [Foldout("配置", true)]
    [SerializeField]
    private GameObject _groupPrefab, _itemPrefab;

    [Foldout("模型展示", true)]
    [SerializeField]
    private RawImage _modelView;
    [SerializeField]
    private Camera _modelCamera;
    [SerializeField]
    private Transform _modelRoot;

    [SerializeField]
    [DisplayField]
    private ArchivesData_SO _arch;
    [SerializeField]
    [DisplayField]
    private AirdropData_SO _nowData;
    /// <summary>列表中的第一个战备，用于窗口打开时默认选中</summary>
    [SerializeField]
    [DisplayField]
    private AirdropData_SO _firstData;
    private readonly Dictionary<Transform, AirdropData_SO> _itemDatas = new();

    [SerializeField]
    [DisplayField]
    private RenderTexture _renderTexture;
    [SerializeField]
    [DisplayField]
    private GameObject _model;
    [SerializeField]
    [DisplayField]
    private float _yaw;
    [SerializeField]
    [DisplayField]
    private bool _dragging;
    [SerializeField]
    [DisplayField]
    private Vector3 _lastMousePosition;

    #region 生命周期

    public void Init()
    {
        WndManager.Instance.airdropConfigWnd = this;
    }

    public void Uninit()
    {
        WndManager.Instance.airdropConfigWnd = null;
    }

    protected override void FirstShowWnd()
    {
        _arch = ArchiveSvc.Archive;

        SetCilck(_closeButton, () =>
        {
            wndManager.PlaySound(new("UI/UI_Button_Back"));
            SetWndState(false);
        });
        SetCilck(_buyButton, Buy);
        SetCilck(_preferButton, TogglePrefer);

        SetupModelView();
    }

    protected override void ShowWnd()
    {
        WindowState = WindowStateEnum.UI;
        InputManager.AddListenerCancel(Cancel);
        //展示相机默认关闭（避免编辑器/场景里误渲染），打开窗口时才启用
        if (_modelCamera) _modelCamera.gameObject.SetActive(true);

        BuildList();
        SelectFirst();
    }

    protected override void HideWnd()
    {
        if (_modelCamera) _modelCamera.gameObject.SetActive(false);
        ClearModel();
        ClearList();

        WindowState = WindowStateEnum.Game;
        InputManager.RemoveListenerCancel(Cancel);
    }

    public override void OnDestroy()
    {
        base.OnDestroy();
        if (_renderTexture == null) return;
        if (_modelCamera) _modelCamera.targetTexture = null;
        if (_modelView) _modelView.texture = null;
        _renderTexture.Release();
        Destroy(_renderTexture);
        _renderTexture = null;
    }

    private bool Cancel()
    {
        if (!State) return false;
        wndManager.PlaySound(new("UI/UI_Button_Back"));
        SetWndState(false);
        return true;
    }

    private void LateUpdate()
    {
        SyncViewAspect();
        HandleModelDrag();
    }

    #endregion

    #region 列表

    /// <summary>按类型分组构建左侧战备列表，偏好战备排在同组最前</summary>
    private void BuildList()
    {
        ClearList();
        if (_listContent == null || _itemPrefab == null) return;

        var all = new List<AirdropData_SO>();
        foreach (var data in ResSvc.airdropDic.Values)
        {
            if (data == null || data.isHide) continue;
            all.Add(data);
        }
        all.Sort((a, b) => a.ID.CompareTo(b.ID));

        for (int t = 0; t < 5; ++t)
        {
            var type = (AirdropData_SO.AirdropType)t;
            var group = all.FindAll(item => item.type == type);
            if (group.Count == 0) continue;

            group.Sort((a, b) =>
            {
                int pa = _arch.IsAirdropPrefer(a.ID) ? 0 : 1;
                int pb = _arch.IsAirdropPrefer(b.ID) ? 0 : 1;
                return pa != pb ? pa - pb : a.ID.CompareTo(b.ID);
            });

            if (_groupPrefab)
            {
                var header = Instantiate(_groupPrefab, _listContent).transform;
                header.name = "Group_" + type;
                if (header.childCount > 0) SetText(header.GetChild(0), group[0].TypeName);
                if (header.childCount > 1) SetText(header.GetChild(1), group.Count + "/" + group.Count);
            }

            for (int i = 0; i < group.Count; ++i)
            {
                var data = group[i];
                var item = Instantiate(_itemPrefab, _listContent).transform;
                item.name = "AirdropItem_" + data.ID;
                if (item.childCount > 0)
                {
                    SetSprite(item.GetChild(0), data.icon);
                    SetColor(item.GetChild(0), data.IconColor);
                }

                _itemDatas[item] = data;
                if (_firstData == null) _firstData = data;
                SetCilck(item, () => Select(data));
            }
        }
    }

    private void ClearList()
    {
        if (_listContent)
        {
            for (int i = _listContent.childCount - 1; i >= 0; --i)
            {
                Tool.Destroy(_listContent.GetChild(i).gameObject);
            }
        }
        _itemDatas.Clear();
        _firstData = null;
    }

    private void SelectFirst()
    {
        if (_firstData != null) Select(_firstData);
    }

    private void Select(AirdropData_SO data)
    {
        _nowData = data;

        foreach (var kv in _itemDatas)
        {
            if (kv.Key.childCount > 1) SetActive(kv.Key.GetChild(1), kv.Value == data);
        }

        ShowInfo(data);
        ShowModel(data);
        RefreshCost(data);
        RefreshButtons(data);
    }

    #endregion

    #region 信息展示

    private void ShowInfo(AirdropData_SO data)
    {
        SetText(_nameText, data.showName);
        SetText(_typeText, data.TypeName);
        SetText(_descText, data.desc);
        SetText(_attrNameText, data.AttrName);
        SetText(_attrValueText, data.AttrValue);
        SetText(_traitText, BuildTraitText(data));
    }

    /// <summary>战备特性：由附属战备的名称拼接而成</summary>
    private string BuildTraitText(AirdropData_SO data)
    {
        var sb = new System.Text.StringBuilder();
        if (data.subAirdrop != null)
        {
            for (int i = 0; i < data.subAirdrop.Length; ++i)
            {
                if (!ResSvc.airdropDic.TryGetValue(data.subAirdrop[i], out var sub) || sub == null) continue;
                if (sb.Length > 0) sb.Append('\n');
                sb.Append("· ").Append(sub.showName);
            }
        }
        return sb.ToString();
    }

    /// <summary>右下价格条目：图标 + 需求/持有</summary>
    private void RefreshCost(AirdropData_SO data)
    {
        if (_costRoot == null) return;
        int count = data.cost != null ? data.cost.Count : 0;
        for (int i = 0; i < _costRoot.childCount; ++i)
        {
            var item = _costRoot.GetChild(i);
            if (!SetActive(item, i < count)) continue;

            var cost = data.cost[i];
            if (item.childCount > 0) SetSprite(item.GetChild(0), propertyManager.GetIcon(cost.Key));
            if (item.childCount > 1)
            {
                int have = propertyManager.GetCount(cost.Key);
                SetText(item.GetChild(1), cost.Value + "/" + have);
                SetColor(item.GetChild(1), cost.Value > have ? Color.red : Color.white);
            }
        }
    }

    #endregion

    #region 购买 / 偏好

    /// <summary>刷新右下按钮：未购买显示价格与购买按钮；已购买显示"已拥有"与偏好按钮</summary>
    private void RefreshButtons(AirdropData_SO data)
    {
        bool bought = _arch.IsAirdropBought(data.ID);

        SetActive(_costRoot, !bought);
        SetButtonInteractable(_buyButton, !bought);
        SetText(_buyButtonText ? _buyButtonText : _buyButton, bought ? "已拥有" : "购买");

        SetActive(_preferButton, bought);
        if (bought)
        {
            SetText(_preferButtonText ? _preferButtonText : _preferButton,
                _arch.IsAirdropPrefer(data.ID) ? "取消偏好" : "偏好");
        }
    }

    private void Buy()
    {
        if (_nowData == null || _arch.IsAirdropBought(_nowData.ID)) return;
        var data = _nowData;
        wndManager.CreatTip(new()
        {
            title = data.showName,
            desc = data.desc + "\n\n要购买这项战备吗?",
            costs = data.cost != null ? data.cost.ToArray() : null,
            optA_Text = "确认",
            optB_Text = "取消",
            optA_Click = () =>
            {
                if (data.cost != null)
                {
                    for (int i = 0; i < data.cost.Count; ++i)
                    {
                        propertyManager.SetCount(data.cost[i].Key, -data.cost[i].Value);
                    }
                }
                _arch.BuyAirdrop(data.ID);
                _arch.Save();

                RefreshCost(data);
                RefreshButtons(data);
                wndManager.PlaySound(new("UI/UI_Reward", volume: 0.25f));
            }
        });
    }

    private void TogglePrefer()
    {
        if (_nowData == null || !_arch.IsAirdropBought(_nowData.ID)) return;
        var data = _nowData;

        bool prefer = _arch.ToggleAirdropPrefer(data.ID);
        _arch.Save();

        wndManager.PlaySound(new(prefer ? "UI/UI_Ready" : "UI/UI_Button_Back"));

        // 偏好顺序会影响列表排序，重建后保持当前选中
        BuildList();
        Select(data);
    }

    #endregion

    #region 模型展示

    private void SetupModelView()
    {
        if (_modelView == null || _modelCamera == null) return;
        if (_renderTexture == null)
        {
            _renderTexture = new RenderTexture(ModelViewSize, ModelViewSize, 16, RenderTextureFormat.ARGB32)
            {
                name = "AirdropConfigWnd_RT"
            };
        }
        _modelCamera.targetTexture = _renderTexture;
        _modelView.texture = _renderTexture;
    }

    /// <summary>让展示相机的宽高比跟随显示区域，避免模型被拉伸变形</summary>
    private void SyncViewAspect()
    {
        if (!State || _modelCamera == null || _modelView == null) return;
        var size = _modelView.rectTransform.rect.size;
        if (size.x <= 1f || size.y <= 1f) return;
        float aspect = size.x / size.y;
        if (!Mathf.Approximately(_modelCamera.aspect, aspect)) _modelCamera.aspect = aspect;
    }

    /// <summary>展示战备模型；模型上的逻辑脚本与碰撞体一律关闭</summary>
    private void ShowModel(AirdropData_SO data)
    {
        ClearModel();
        if (_modelRoot == null || data == null || data.creatObect == null) return;

        _model = Instantiate(data.creatObect, _modelRoot);
        _model.name = "ShowModel_" + data.ID;
        _model.transform.localPosition = Vector3.zero;
        _model.transform.localRotation = Quaternion.identity;
        _model.transform.localScale = Vector3.one;

        _model.SetChildLayer(_modelRoot.gameObject.layer, true);

        foreach (var script in _model.GetComponentsInChildren<MonoBehaviour>(true))
        {
            script.enabled = false;
        }
        foreach (var collider in _model.GetComponentsInChildren<Collider>(true))
        {
            collider.enabled = false;
        }
        foreach (var body in _model.GetComponentsInChildren<Rigidbody>(true))
        {
            body.useGravity = false;
            body.isKinematic = true;
        }

        _yaw = 0f;
        _modelRoot.localRotation = Quaternion.identity;
    }

    private void ClearModel()
    {
        if (_model != null)
        {
            Tool.Destroy(_model);
            _model = null;
        }
        if (_modelRoot) _modelRoot.localRotation = Quaternion.identity;
        _dragging = false;
    }

    /// <summary>鼠标左键在模型显示区域内左右拖动旋转模型</summary>
    private void HandleModelDrag()
    {
        if (!State || _model == null || _modelView == null || _modelRoot == null) return;

        if (Input.GetMouseButtonDown(0))
        {
            _dragging = RectTransformUtility.RectangleContainsScreenPoint(
                _modelView.rectTransform, Input.mousePosition, GetViewCamera());
            _lastMousePosition = Input.mousePosition;
        }
        else if (_dragging && Input.GetMouseButton(0))
        {
            var current = Input.mousePosition;
            float delta = current.x - _lastMousePosition.x;
            _lastMousePosition = current;

            _yaw -= delta * ModelRotateSpeed;
            _modelRoot.localRotation = Quaternion.Euler(0f, _yaw, 0f);
        }
        else if (Input.GetMouseButtonUp(0))
        {
            _dragging = false;
        }
    }

    /// <summary>Overlay 画布下为 null，其余取画布相机</summary>
    private Camera GetViewCamera()
    {
        var canvas = _modelView.canvas;
        if (canvas == null || canvas.renderMode == RenderMode.ScreenSpaceOverlay) return null;
        return canvas.worldCamera;
    }

    #endregion
}
