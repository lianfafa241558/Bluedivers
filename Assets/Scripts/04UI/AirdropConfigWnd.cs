using System.Collections;
using System.Collections.Generic;
using Core;
using FPSGame.Attribute;
using UnityEngine;
using UnityEngine.UI;
using Utils;
using static WndTools.WndRootTool;

/// <summary>
/// 战备配置界面（船舰管理 - 战备配置）
/// 左侧为按类型 / 标签分组的战备列表；右上展示战备模型（鼠标左右拖动旋转）；
/// 右下展示购买价格 / 已拥有状态，并额外提供偏好切换按钮。
///
/// 预制体节点约定：
///   _listContent   左侧列表内容容器（挂 VerticalLayoutGroup + ContentSizeFitter）
///   _groupPrefab   分组表头  子0=标题文本  子1(可选)=数量文本
///   _itemPrefab    战备条目  子0=图标      子1(可选)=选中框
///   _modelView     右上模型显示 RawImage（贴图由脚本赋值为运行时 RenderTexture）
///   _modelCamera   展示用相机（CullingMask 只勾选展示层，TargetTexture 由脚本赋值）
///   _modelRoot     模型挂点，相机对准此节点；拖动旋转的也是此节点
///                  （模型实例会按自身包围盒自动缩放并对齐到该节点，见 FitModelScale）
///   _costRoot      价格条目容器，每个子物件：子0=资源图标  子1=数量文本
///   _traitText     战备特性（由 subAirdrop 附属战备名称拼接）
/// </summary>
public class AirdropConfigWnd : Window
{
    /// <summary>模型每像素旋转角度</summary>
    private const float ModelRotateSpeed = 0.3f;
    /// <summary>模型展示用的渲染纹理尺寸</summary>
    private const int ModelViewSize = 1024;

    /// <summary>范围 / 贴花类材质的名字关键字（命中则不参与展示尺寸计算，见 IsRangeDisplayRenderer）</summary>
    private static readonly string[] RangeMaterialKeys = new string[] { "Range", "DistGround", "Decal", "Focu" };

    /// <summary>
    /// 列表分组规则：同一类型的战备按标签细分为多个分组。
    /// 匹配方式为"按顺序首个命中"，因此每条类型最后一条规则是不限制标签的兜底分组；
    /// 没有任何规则命中的类型（如补给型）不会显示在列表中。
    /// </summary>
    private static readonly AirdropGroupRule[] GroupRules = new AirdropGroupRule[]
    {
        // 轰炸型：带飞鹰标签的归"轨道打击"，其余归"凤鹰空袭"
        new("轨道打击", AirdropData_SO.AirdropType.Red, AirdropLabelEnum.Jet),
        new("凤鹰空袭", AirdropData_SO.AirdropType.Red),
        // 装备型：背包 / 无人机 / 其余支援武器
        new("战术背包", AirdropData_SO.AirdropType.Blue, AirdropLabelEnum.Bag),
        new("无人机", AirdropData_SO.AirdropType.Blue, AirdropLabelEnum.Drone),
        new("支援武器", AirdropData_SO.AirdropType.Blue),
        // 炮台型：地雷 / 其余哨戒炮
        new("地雷发射器", AirdropData_SO.AirdropType.Greed, AirdropLabelEnum.Mine),
        new("哨戒炮", AirdropData_SO.AirdropType.Greed),
        // 载具型：不细分，标题取该类型的默认名称
        new(null, AirdropData_SO.AirdropType.Orange),
    };

    /// <summary>单个列表分组的归属规则</summary>
    private readonly struct AirdropGroupRule
    {
        /// <summary>分组标题；为 null 时使用所属类型的默认名称</summary>
        public readonly string Title;
        /// <summary>所属战备类型</summary>
        public readonly AirdropData_SO.AirdropType Type;
        /// <summary>需要携带的标签；0 表示不限制标签（兜底分组）</summary>
        public readonly AirdropLabelEnum Label;

        public AirdropGroupRule(string title, AirdropData_SO.AirdropType type, AirdropLabelEnum label = 0)
        {
            Title = title;
            Type = type;
            Label = label;
        }

        /// <summary>该战备是否归入本分组</summary>
        public bool Match(AirdropData_SO data)
        {
            if (data == null || data.type != Type) return false;
            return Label == 0 || (data.labels & Label) != 0;
        }
    }

    [Foldout("配置", true)]
    [SerializeField]
    private Transform _bg,_closeButton, _listContent, _nameText, _typeText, _descText, _traitText,
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
    /// <summary>模型在展示区视野里占用的比例（1 = 刚好贴到视野边缘）</summary>
    [SerializeField]
    [Range(0.1f, 1f)]
    [InspectorName("模型展示占比")]
    private float _modelViewFill = 0.85f;
    /// <summary>模型默认朝向；拖动旋转会在 Y 轴上叠加（模型正面朝镜头用 180）</summary>
    [SerializeField]
    [InspectorName("模型默认旋转")]
    private Vector3 _modelBaseEuler = new Vector3(0f, 180f, 0f);

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
    /// <summary>模型的未激活挂点：实例先建在这里，保证其组件在被清理前不会执行 Awake</summary>
    [SerializeField]
    [DisplayField]
    private GameObject _modelHolder;
    /// <summary>模型实例自身的基础缩放（预制体上的缩放），自适应缩放会在它之上叠加</summary>
    [SerializeField]
    [DisplayField]
    private Vector3 _modelBaseScale = Vector3.one;
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
        SetSprite(_bg,FpsHelper.CameraCaptureToSprite(Camera.main));
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

    /// <summary>按分组规则构建左侧战备列表，偏好战备排在同组最前</summary>
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

        //分桶：每个战备只归入首个命中的分组，未命中任何规则的类型不会出现在列表中
        var buckets = new List<AirdropData_SO>[GroupRules.Length];
        for (int r = 0; r < buckets.Length; ++r) buckets[r] = new List<AirdropData_SO>();

        for (int i = 0; i < all.Count; ++i)
        {
            for (int r = 0; r < GroupRules.Length; ++r)
            {
                if (!GroupRules[r].Match(all[i])) continue;
                buckets[r].Add(all[i]);
                break;
            }
        }

        for (int r = 0; r < GroupRules.Length; ++r)
        {
            var group = buckets[r];
            if (group.Count == 0) continue;

            group.Sort((a, b) =>
            {
                int pa = _arch.IsAirdropPrefer(a.ID) ? 0 : 1;
                int pb = _arch.IsAirdropPrefer(b.ID) ? 0 : 1;
                return pa != pb ? pa - pb : a.ID.CompareTo(b.ID);
            });

            if (_groupPrefab)
            {
                int bought = 0;
                for (int i = 0; i < group.Count; ++i)
                {
                    if (_arch.IsAirdropBought(group[i].ID)) ++bought;
                }

                var header = Instantiate(_groupPrefab, _listContent).transform;
                header.name = "Group_" + r;
                if (header.childCount > 0) SetText(header.GetChild(0), GroupRules[r].Title ?? group[0].TypeName);
                //数量文本：已拥有 / 总数
                if (header.childCount > 1) SetText(header.GetChild(1), bought + "/" + group.Count);
            }

            for (int i = 0; i < group.Count; ++i)
            {
                var data = group[i];
                var item = Instantiate(_itemPrefab, _listContent).transform;
                item.name = "AirdropItem_" + data.ID;
                SetSprite(item.GetChild(0), data.icon);
                SetColor(item.GetChild(0), data.IconColor);

                SetText(item.GetChild(2), data.showName);


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

                //已拥有数量会变，重建列表刷新分组表头的数量文本（Select 内部会刷新右侧信息与按钮）
                BuildList();
                Select(data);
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
        if (Mathf.Approximately(_modelCamera.aspect, aspect)) return;
        _modelCamera.aspect = aspect;
        //展示区比例变了，视野范围跟着变，重新贴一次大小
        FitModelScale();
    }

    /// <summary>
    /// 展示战备模型。
    /// 实例先挂在未激活的挂点上（此时对象不在激活层级里，任何组件的 Awake / OnEnable 都不会执行），
    /// 清理掉根物体上除渲染 / 动画以外的组件后再激活，避免模型上的逻辑脚本产生副作用。
    /// </summary>
    private void ShowModel(AirdropData_SO data)
    {
        ClearModel();
        if (_modelRoot == null || data == null || data.creatObect == null) return;

        _modelHolder = new GameObject("ShowModelHolder");
        _modelHolder.layer = _modelRoot.gameObject.layer;
        _modelHolder.transform.SetParent(_modelRoot, false);
        _modelHolder.SetActive(false);

        _model = Instantiate(data.creatObect, _modelHolder.transform);
        _model.name = "ShowModel_" + data.ID;
        //保留预制体自身的旋转与缩放（比如炮台根节点自带 45°，会和挂点上的默认朝向叠加），
        //只把预制体根节点那串随手存下来的位置归零：模型后面会按包围盒重新摆到视线中心
        _modelBaseScale = _model.transform.localScale;
        _model.transform.localPosition = Vector3.zero;

        StripModelRoot(_model);
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
        ApplyModelRotation();

        StartCoroutine(ActiveModelNextFrame(_modelHolder));
    }

    /// <summary>
    /// 把挂点旋转设为"模型默认朝向 + 拖动角度"。
    /// 模型实例会保留预制体自身的旋转，两者叠加（纯 Y 轴时就是角度相加，如 180+45=225）。
    /// </summary>
    private void ApplyModelRotation()
    {
        if (_modelRoot == null) return;
        _modelRoot.localRotation = Quaternion.Euler(_modelBaseEuler.x, _modelBaseEuler.y + _yaw, _modelBaseEuler.z);
    }

    /// <summary>等被移除的组件在本帧末真正销毁后再激活挂点，否则它们仍会抢到一次 Awake</summary>
    private IEnumerator ActiveModelNextFrame(GameObject holder)
    {
        yield return null;
        if (!holder) yield break;
        holder.SetActive(true);

        //先把 Animator 的姿势算出来：绑定姿势和动画播放后的位置可能差很远
        //（Healdrone 的默认动画会把机身往下挪 1.7 米，按绑定姿势量出来的包围盒中心就偏高）
        foreach (var animator in holder.GetComponentsInChildren<Animator>(true))
        {
            if (animator != null && animator.runtimeAnimatorController != null) animator.Update(0f);
        }

        //再等一帧，等蒙皮网格的包围盒刷新完才量；Renderer.bounds 在未激活时是零体积，必须在激活后量
        yield return null;
        if (!holder) yield break;
        FitModelScale();
    }

    /// <summary>
    /// 按模型的包围盒缩放到相机视野内（大体型缩小、小体型放大），并把包围盒中心对齐到相机视线中心，
    /// 这样不同体型的战备在展示区里看起来大小一致、居中，左右拖动旋转时也不会转出视野。
    /// </summary>
    private void FitModelScale()
    {
        if (_model == null || _modelRoot == null) return;

        var tr = _model.transform;
        Vector3 lastScale = tr.localScale;
        Vector3 lastPos = tr.localPosition;
        //先复位再量尺寸，保证重复调用（展示区比例变化时）拿到的是同一份基础尺寸
        tr.localScale = _modelBaseScale;
        tr.localPosition = Vector3.zero;

        //量尺寸时把模型自身的旋转临时归零：世界包围盒会跟着旋转被"撑大"
        //（炮台预制体自带 45° 时水平尺寸会虚高四成，模型就会被算小），量完再还原
        Quaternion baseRotation = tr.localRotation;
        tr.localRotation = Quaternion.identity;
        bool hasBounds = TryGetModelBounds(_model, out Bounds bounds);
        //旋转归零时算出来的中心就是"模型自身局部坐标"，和后面用的旋转是对得上的
        Vector3 center = hasBounds ? tr.InverseTransformPoint(bounds.center) : Vector3.zero;
        tr.localRotation = baseRotation;

        if (!hasBounds)
        {
            //量不到有效包围盒（比如对象还没激活，Renderer.bounds 是零体积），保持原状
            tr.localScale = lastScale;
            tr.localPosition = lastPos;
            return;
        }

        //水平按 XZ 半对角线算（绕 Y 拖到任意角度都不会超出视野），竖直按 Y 半高算
        float horizontal = new Vector2(bounds.extents.x, bounds.extents.z).magnitude;
        float vertical = bounds.extents.y;

        //相机视线中心：挂点不一定是相机正对的那个点（本预制体就差了 0.5），
        //按视线中心对齐才是画面正中，否则模型会整体偏上 / 偏下
        Vector3 focus = _modelRoot.position;
        float halfHeight = 0f;
        float halfWidth = 0f;
        if (_modelCamera != null)
        {
            var camT = _modelCamera.transform;
            float depth = Vector3.Dot(_modelRoot.position - camT.position, camT.forward);
            focus = camT.position + camT.forward * depth;
            //模型所在深度处的视野半高 / 半宽
            halfHeight = _modelCamera.orthographic
                ? _modelCamera.orthographicSize
                : Mathf.Abs(depth) * Mathf.Tan(_modelCamera.fieldOfView * 0.5f * Mathf.Deg2Rad);
            halfWidth = halfHeight * _modelCamera.aspect;
        }

        //小体型的战备（背包 / 无人机）要放大到同样占满展示区，大体型的再缩小，所以这里是双向的
        float scale = 0f;
        if (halfWidth > 0f && horizontal > 0.0001f) scale = halfWidth * _modelViewFill / horizontal;
        if (halfHeight > 0f && vertical > 0.0001f)
        {
            float verticalScale = halfHeight * _modelViewFill / vertical;
            scale = scale > 0f ? Mathf.Min(scale, verticalScale) : verticalScale;
        }
        if (scale <= 0f) scale = 1f;

        //把包围盒中心挪到视线中心：center 是包围盒中心在模型自身局部空间的坐标，
        //按当前世界旋转 / 缩放换算成世界偏移即可（模型自身旋转此刻已还原）
        tr.localScale = Vector3.Scale(_modelBaseScale, Vector3.one * scale);
        tr.position = focus - tr.rotation * Vector3.Scale(center, tr.lossyScale);
    }

    /// <summary>
    /// 量取展示模型的世界包围盒。
    /// 战备预制体里混着不少"不是模型本体"的渲染物，直接 Encapsulate 会被它们撑爆，
    /// 所以先只按实体网格量；全模型只有特效（比如轨道激光的光束）时再退回按全部 Renderer 量。
    /// </summary>
    private static bool TryGetModelBounds(GameObject model, out Bounds bounds)
    {
        var renderers = model.GetComponentsInChildren<Renderer>(true);
        return CollectModelBounds(renderers, true, out bounds) || CollectModelBounds(renderers, false, out bounds);
    }

    /// <summary>合并可用的 Renderer 包围盒；<paramref name="solidOnly"/> 为 true 时忽略粒子 / 线段 / 拖尾</summary>
    private static bool CollectModelBounds(Renderer[] renderers, bool solidOnly, out Bounds bounds)
    {
        bounds = default;
        bool hasBounds = false;
        for (int i = 0; i < renderers.Length; ++i)
        {
            var renderer = renderers[i];
            if (renderer == null) continue;
            //压根不会被画出来的（物体被关掉 / 渲染器被禁用）不参与
            if (!renderer.enabled || !renderer.gameObject.activeInHierarchy) continue;
            if (solidOnly && (renderer is ParticleSystemRenderer || renderer is LineRenderer || renderer is TrailRenderer)) continue;
            if (IsRangeDisplayRenderer(renderer)) continue;

            var rendererBounds = renderer.bounds;
            //零体积的包围盒（未播放的粒子系统等）会把它们所在的点带进包围盒，直接丢掉
            if (rendererBounds.size.sqrMagnitude <= 0.000001f) continue;

            if (!hasBounds)
            {
                bounds = rendererBounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(rendererBounds);
            }
        }
        return hasBounds;
    }

    /// <summary>
    /// 是否是"影响范围 / 贴花"这类示意网格。
    /// 这些网格往往比本体大出一个数量级（比如 HaloBomb 周围 9 片 BombRange 挡墙、旗帜外围 20 米的 Range_Sphere），
    /// 算进去模型本体就会被缩成一个小点，所以按材质名关键字排除掉。
    /// </summary>
    private static bool IsRangeDisplayRenderer(Renderer renderer)
    {
        var materials = renderer.sharedMaterials;
        for (int i = 0; i < materials.Length; ++i)
        {
            var material = materials[i];
            if (material == null) continue;
            string materialName = material.name;
            for (int k = 0; k < RangeMaterialKeys.Length; ++k)
            {
                if (materialName.IndexOf(RangeMaterialKeys[k], System.StringComparison.OrdinalIgnoreCase) >= 0) return true;
            }
        }
        return false;
    }

    /// <summary>
    /// 移除模型根物体上除渲染 / 动画以外的组件（逻辑脚本、碰撞体、刚体、音源、相机、灯光、导航等）。
    /// 注意：<c>enabled = false</c> 是拦不住 Awake 的，只有在激活之前把组件移除，它们的 Awake / OnEnable 才不会执行。
    /// </summary>
    private static void StripModelRoot(GameObject model)
    {
        var components = model.GetComponents<Component>();
        for (int i = components.Length - 1; i >= 0; --i)
        {
            var component = components[i];
            if (component == null || IsModelVisualComponent(component)) continue;
            Tool.Destroy(component);
        }
    }

    /// <summary>展示模型必须保留的组件：只负责把模型画出来 / 播放动画，本身不含逻辑</summary>
    private static bool IsModelVisualComponent(Component component)
    {
        return component is Transform
            || component is Animator
            || component is Renderer
            || component is MeshFilter
            || component is LODGroup
            || component is ParticleSystem;
    }

    private void ClearModel()
    {
        if (_model != null)
        {
            Tool.Destroy(_model);
            _model = null;
        }
        if (_modelHolder != null)
        {
            Tool.Destroy(_modelHolder);
            _modelHolder = null;
        }
        _yaw = 0f;
        ApplyModelRotation();
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
            ApplyModelRotation();
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
