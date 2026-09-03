using FPSGame.Attribute;
using UnityEngine;

/// <summary>
/// 欧帕兹提交容纳家具（抽象基类，通用任意类型）。
///
/// 与 <see cref="PlayerOperationController"/> 的"接管按压"模式(<see cref="IStepPress"/>)配合：
/// 1. 可交互条件：玩家欧帕兹背包(<see cref="PlayerOOPartInventory"/>)携带有欧帕兹(任意类型，取总量)，
///    且本容器尚未满(<see cref="held"/> &lt; <see cref="capacity"/>)；
/// 2. 玩家按下交互键瞬间(<see cref="BeginPress"/>)锁定本次需求 need = min(剩余容量, 玩家携带总量)，
///    总长按时长 = need / <see cref="SubmitPerSecond"/>（只在按下时计算一次，不做逐帧刷新）；
/// 3. 按住期间(<see cref="StepPress"/>)按 <see cref="SubmitPerSecond"/> 每秒个的速率，从背包逐个
///    取出任意类型的欧帕兹转移进本容器，每成功转移 1 个调用一次抽象钩子 <see cref="OnSubmit"/>；
/// 4. 转满 need（即玩家背包清空或本容器已满）时结束，控制器调用 <see cref="Operate"/> 收尾；
///    中途松开(<see cref="CancelPress"/>)则保留已转移部分，下次按下按剩余继续。
/// </summary>
public abstract class Furniture_OOPartDepositBase : Furniture_Base, IStepPress
{
    /// <summary>每秒提交到容器的欧帕兹数量（恒定速率）</summary>
    public const int SubmitPerSecond = 10;

    /// <summary>单个欧帕兹所需的长按时间（秒）</summary>
    private const float PerUnitTime = 1f / SubmitPerSecond;

    [Foldout("欧帕兹提交", true)]
    /// <summary>容纳上限（int 字段）：本容器最多存放该数量的欧帕兹</summary>
    [SerializeField]
    [InspectorName("容纳上限")]
    protected int capacity;

    [SerializeField]
    [DisplayField(DisplayFieldEnum.RunRead)]
    [InspectorName("已容纳数量")]
    protected int held;

    // ---- 接管按压运行时状态 ----
    private bool _pressing;          // 是否正在长按转移中
    private GameObject _pressUser;   // 正在操作的玩家
    private int _need;               // 本次按下锁定的需转移个数
    private int _transferred;        // 本次已转移个数
    private float _stepAcc;          // 转移节拍累加器

    /// <summary>容纳上限</summary>
    public int Capacity => capacity;

    /// <summary>当前已容纳数量</summary>
    public int Held => held;

    /// <summary>本次尚可接受的欧帕兹个数</summary>
    protected int RemainingSpace => Mathf.Max(0, capacity - held);

    protected override void Awake()
    {
        base.Awake();
        if (capacity <= 0) capacity = 1;
    }

    /// <summary>
    /// 基础可操作判定：容器未满、玩家背包携带总量非空。
    /// </summary>
    private bool CoreCanOperate(GameObject unit)
    {
        if (!base.CanOperate(unit)) return false;
        if (unit == null || !unit.TryGetComponent(out PlayerOOPartInventory bag)) return false;
        return bag.CurrentCount > 0 && RemainingSpace > 0;
    }

    public override bool CanOperate(GameObject unit)
    {
        // 正在转移中：豁免瞄准循环，避免因途中包空/容器将满被提前清掉目标而打断收尾
        if (_pressing && unit != null && unit == _pressUser) return true;
        return CoreCanOperate(unit);
    }

    bool IStepPress.CanOperateStepped(GameObject unit) => CoreCanOperate(unit);

    bool IStepPress.BeginPress(GameObject unit)
    {
        if (unit == null || !unit.TryGetComponent(out PlayerOOPartInventory bag)) return false;

        // 只在按下瞬间锁定需求与总时长
        _need = Mathf.Min(RemainingSpace, bag.CurrentCount);
        if (_need <= 0) return false;

        _pressing = true;
        _pressUser = unit;
        _transferred = 0;
        _stepAcc = 0;
        pressTime = 0;
        // 供 UI(OperationWnd) 显示总进度分母
        meetTime = _need * PerUnitTime;
        return true;
    }

    bool IStepPress.StepPress(float deltaTime)
    {
        if (!_pressing) return false;
        if (!_pressUser || !_pressUser.TryGetComponent(out PlayerOOPartInventory bag))
        {
            ResetPressState();
            return false;
        }

        pressTime += deltaTime;
        _stepAcc += deltaTime;

        // 按 10/s 的节拍，逐步把矿石从玩家包转移进本容器
        float interval = PerUnitTime;
        while (_transferred < _need && _stepAcc >= interval)
        {
            _stepAcc -= interval;
            if (RemoveAnyOne(bag, out OOPartEnum type))
            {
                held++;
                _transferred++;
                OnSubmit(_pressUser, type);
            }
            else
            {
                // 玩家包提前被取空，视为完成
                _transferred = _need;
                break;
            }
        }

        return _transferred >= _need;
    }

    /// <summary>玩家松开/中断长按时调用：复位并触发抽象钩子 <see cref="OnPressCancel"/></summary>
    void IStepPress.CancelPress()
    {
        GameObject user = _pressUser;
        int transferred = _transferred;
        ResetPressState();
        if (user != null)
            OnPressCancel(user, transferred);
    }

    /// <summary>复位按压运行时状态（不触发任何钩子，供完成收尾等内部使用）</summary>
    private void ResetPressState()
    {
        _pressing = false;
        _pressUser = null;
        _need = 0;
        _transferred = 0;
        _stepAcc = 0;
        pressTime = 0;
    }

    public override void Operate()
    {
        base.Operate();

        // 正常转满收尾：仅复位按压状态，不触发"松开"钩子（完成由本方法收尾表达）
        ResetPressState();

        // 容器已满则不再接受后续交互；未满则等待玩家下次携带再来
        if (held >= capacity) canOperate = false;
    }

    /// <summary>
    /// 从玩家背包取出任意类型 1 个欧帕兹，成功返回 true 并输出其类型。
    /// </summary>
    private static bool RemoveAnyOne(PlayerOOPartInventory bag, out OOPartEnum type)
    {
        foreach (var kv in bag.GetAll())
        {
            if (kv.Value > 0 && bag.Remove(kv.Key, 1) > 0)
            {
                type = kv.Key;
                return true;
            }
        }
        type = default;
        return false;
    }

    /// <summary>
    /// 提交钩子（抽象）：每成功向本容器转移 1 个欧帕兹调用一次，供子类覆盖。
    /// </summary>
    /// <param name="user">正在提交的玩家物体</param>
    /// <param name="type">本次转移的欧帕兹类型</param>
    protected abstract void OnSubmit(GameObject user, OOPartEnum type);

    /// <summary>
    /// 松开钩子（抽象）：仅当玩家松开交互键或中断长按时触发一次（装满完成走 <see cref="Operate"/>，
    /// 不会触发本钩子）。已成功转移进容器的矿石会保留（<paramref name="transferred"/> 即本次已转移个数），
    /// 下次按下会按剩余需求继续。
    /// </summary>
    /// <param name="user">本次交互的玩家物体</param>
    /// <param name="transferred">本次长按期间已成功转移的欧帕兹个数</param>
    protected abstract void OnPressCancel(GameObject user, int transferred);
}
