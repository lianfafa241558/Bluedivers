using System.Collections.Generic;
using System.Linq;
using Core;
using GameContract;
using Unity.FPS.Game;
using UnityEngine;

/// <summary>
/// 战备控制器，管理战备的输入、释放与状态切换。
/// 
/// 战备释放有两种流程：
/// 
/// 【普通释放】（isDirect = false）
///   Open → Input（输入方向键）→ OnWaitRelease（state=Wait）→ 关闭面板
///   → 玩家选点 → VFXAirdropEffect.SetOwner → BattleEventSub.Airdrop
///   → OnRelease（state=Arrive）→ 落地 → state=Sustain → 结束 → state=Cool/Unavailable
/// 
/// 【直接释放】（isDirect = true，如飞鹰装填、HealBag）
///   Open → Input（输入方向键）→ OnWaitRelease（state=Wait）→ 关闭面板
///   → 触发 OnStateChange(Wait) 回调执行效果 → state=Ready（不经过 VFX/OnRelease）
/// 
/// 直接释放的效果逻辑写在 OnAirdropStateChange 的 AirdropState.Wait 分支中。
/// </summary>
public class AirdropController : MonoBehaviour
{

    public static AirdropData WaitRelease;

    private const float _PowerMax = 10;
    private const float _PowerReSpeed = 0.167f;//60s回满

    public List<AirdropData> useAd;

    I_Actor Player => ActorsManager.Player;

    private Dictionary<int,AirdropData_SO> adDic=> ResSvc.airdropDic;

    [SerializeField]
    private List<DirectionEnum> inputDir;

    /*
    public void Init()
    {

    }*/

    private void Start()
    {

        InputManager.BindDown(WindowStateEnum.Game,InputState.Airdrop, Open);
        InputManager.BindDown(WindowStateEnum.Airdrop, InputState.Airdrop, Close);
        BattleEventSub.OnCancelAirdrop += OnCancel;
        BattleEventSub.OnAirdrop += OnRelease;
        BattleEventSub.OnPlayerDead += OnPlayerDeath;
    }


    private void OnDestroy()
    {
        InputManager.UnBindDown(WindowStateEnum.Game, InputState.Airdrop, Open);
        InputManager.UnBindDown(WindowStateEnum.Airdrop, InputState.Airdrop, Close);
        BattleEventSub.OnCancelAirdrop -= OnCancel;
        BattleEventSub.OnAirdrop -= OnRelease;
        BattleEventSub.OnPlayerDead -= OnPlayerDeath;
    }


    void Update()
    {

        if(WndManager.WindowState == WindowStateEnum.Airdrop)
        {
            if (InputManager.GetDown(InputState.Left)) Input(DirectionEnum.Left);
            else if (InputManager.GetDown(InputState.Up)) Input(DirectionEnum.Up);
            else if (InputManager.GetDown(InputState.Right)) Input(DirectionEnum.Right);
            else if (InputManager.GetDown(InputState.Down)) Input(DirectionEnum.Down);
        }
        useAd.ForEach(item=>item.Update());
    }


    public void Init()
    {

        inputDir = new();


        useAd = new();
        //读取任务所需战备
        var required = TaskManager.Instance.nowTask.RequiredAD;
        for (int i = 0; i < required.Count; ++i)
        {
            //Debug.LogError("添加任务所需战备"+ ResManager.airdropDic[required[i]].showName);
            useAd.Add(new(ResSvc.airdropDic[required[i]], true));
        }
        //读取玩家携带的战备
        var arr = RoomManager.Instance.Self.airdrop;
        if(arr.Any(id=> ResSvc.airdropDic[id].deliveryType == AirdropDeliveryEnum.Jet))
        {
            useAd.Add(new(ResSvc.airdropDic[Constants.EagleReloadId],true));
        }
        for (int i = 0; i < arr.Length; ++i)
        {
            useAd.Add(new(ResSvc.airdropDic[arr[i]],false));
        }

        foreach(var item in useAd)
        {
            item.OnStateChange += OnAirdropStateChange;//这样只有自己叫的才考虑
        }

    }


    private void Open()
    {
        if (Player.ActorState == ActorState.Hide) return;
        // 死亡状态下，只有存在 deathEnable 战备时才允许打开面板
        if (Player.ActorState == ActorState.Dead && !useAd.Any(item => item.IsCurrentlyAvailable(Player)))
            return;
        WndManager.WindowState = WindowStateEnum.Airdrop;
        inputDir.Clear();
        OnCancel(Player.gameObject,WaitRelease);
    }
    private void Close()
    {
        WndManager.WindowState = WindowStateEnum.Game;
    }

    private void Input(DirectionEnum dir)
    {
        inputDir.Add(dir);
        //如果和当前战备全部不符合就清空
        bool keep = false;
        foreach (var item in useAd) 
        {
            if (!item.IsCurrentlyAvailable(Player)) continue;//当前不可用的战备跳过
            bool state = item.State==AirdropState.Ready && item.cfg.opter.Compare(inputDir);
            keep |= state;
            if(state && inputDir.Count == item.cfg.opter.Length)
            {
                AudioSvc.PlaySound(new("AirDrop/superbeacon_active"));
                OnWaitRelease(item);
                inputDir.Clear();
                return;
            }
        }

        if (keep)
        {
            AudioSvc.PlaySound(new("AirDrop/superbeacon_button"));
        }
        else
        {
            inputDir.Clear();
            AudioSvc.PlaySound(new("AirDrop/superbeacon_throw"));
        }
        BattleEventSub.InputAirdrop(inputDir);

    }
    /// <summary>完成输入，等待释放</summary>
    private void OnWaitRelease(AirdropData item)
    {
        item.State = AirdropState.Wait;
        WaitRelease = item;
        Close();
        //通过这个事件来让对应的类调用来直接强制释放
        BattleEventSub.SelectAirdrop(Player.gameObject,item);

        if (item.cfg.isDirect)//直接释放（飞鹰装填和升旗）
        {
            //通过 item.OnStateChange事件来执行对应效果 
            item.State = AirdropState.Ready;
            WaitRelease = null;
        }
        //else if (Player.ActorState == ActorState.Hide)
        //{
        //    OnRelease(Player.gameObject, GameObject target, Vector3 point, item);
        //}

    }

    /// <summary>释放战备</summary>
    private void OnRelease(GameObject owner, GameObject target, Vector3 point, AirdropData data)
    {
        if (owner == null) return;
        if (data == null) {
            Debug.LogError("战备丢失");
            return; }
        data.State = AirdropState.Arrive;
        if (GameRoot.GameState == GameStateEnum.Game && owner.TryGetComponent(out PlayerController player))
        {
            BattleManager.Instance.AddBattleDataItem(player.PlayerIndex, "呼叫战备次数");

            WaitRelease = null;
        }

    }

    /// <summary>取消战备</summary>
    private void OnCancel(GameObject go,AirdropData item)
    {
        if (!item.IsValid()||go !=Player.gameObject) return;
        Debug.LogWarning("取消准备中的战备"+item);
        item.State = AirdropState.Ready;

        WaitRelease = null;
    }

    void OnPlayerDeath(Actor _)
    {
        if (WaitRelease != null)
        {
            OnCancel(Player.gameObject, WaitRelease);
        }
        if (WndManager.WindowState == WindowStateEnum.Airdrop)
        {
            Close();
        }
    }

    /// <summary>
    /// 为战备提供授权
    /// </summary>
    /// <param name="id"></param>
    /// <param name="state"></param>
    public void Authorize(int id,bool state)
    {
        //Debug.LogError("尝试授权"+id+state);
        var ad=useAd.Find(item =>item.cfg.ID==id);
        if (ad != null)
        {
            _Authorize(ad, state);
        }
    }
    private void _Authorize(AirdropData data,bool state)
    {
        data.authorizeCounter += state ? 1 : -1;
        if ((state && data.authorizeCounter == 1) || (!state && data.authorizeCounter == 0)) BattleEventSub.AuthorizeAirdrop();
        //Debug.LogError(ad.cfg.showName+"授权状态"+ad.authorizeCounter+ " "+ad.IsAuthorize);
    }

    private void OnAirdropStateChange(AirdropData data,AirdropState state)
    {

        switch (state)
        {
            case AirdropState.Unavailable:
                //飞鹰自动重新装填
                if (data.cfg.deliveryType == AirdropDeliveryEnum.Jet)
                {
                    bool haveVaild = useAd.Any(item=>item.cfg.deliveryType == AirdropDeliveryEnum.Jet&&item.State != AirdropState.Unavailable);
                    if (!haveVaild) OnWaitRelease(useAd.FirstOrDefault(item=> item.cfg.ID == Constants.EagleReloadId));
                }
                break;
            case AirdropState.Wait:
                if (data.cfg.ID == Constants.EagleReloadId)//飞鹰装填
                {
                    _Authorize(data, false);
                    foreach (var item in useAd)
                    {
                        if (item.cfg.deliveryType == AirdropDeliveryEnum.Jet)
                        {
                            Debug.Log("所有飞鹰重新装填");
                            item.State = AirdropState.Cool;//所有飞鹰共装填
                            item.time = data.cool;
                            item.count = item.arriveCount;
                        }
                    }
                }
                // HealBag：直接释放，对每个死亡玩家位置释放治疗包
                if (data.cfg.ID == Constants.HealBag)
                {
                    foreach (var player in ActorsManager.Players)
                    {
                        if (player.ActorState == ActorState.Dead)
                        {
                            BattleManager.Instance.ReleaseAirdrop(player.Pos, Constants.HealBag);
                        }
                    }
                    // 扣一次使用次数
                    if (data.arriveCount > 0)
                        data.count--;
                    if (data.count <= 0)
                        data.State = AirdropState.Unavailable;
                }
                break;
            case AirdropState.Arrive:
                if (data.cfg.deliveryType == AirdropDeliveryEnum.Jet)
                {
                    //飞鹰共CD
                    foreach (var item in useAd)
                    {
                        if (item.cfg.ID == Constants.EagleReloadId && !item.IsAuthorize)
                        {
                            _Authorize(item, true);
                        }
                        else if (item != data && item.cfg.deliveryType == AirdropDeliveryEnum.Jet)
                        {
                            //Debug.LogError("所有飞鹰共CD");
                            if (item.State != AirdropState.Unavailable) item.State = AirdropState.Cool;
                            item.time = item.cool;//因为正常cd阶段是减去了持续和呼叫时间
                        }
                    }
                }
                break;
        }
        

    }





    [System.Serializable]
    public class AirdropData {
        public event System.Action<AirdropData, AirdropState> OnStateChange;

        public AirdropData_SO cfg;
        public bool isGift;
        public float time;
        public int count;
        public bool isTmp;

        [InspectorName("冷却时间")]
        public int cool;
        [InspectorName("部署时间")]
        public int arriveTime;
        [InspectorName("部署次数")]
        public int arriveCount;

        public AirdropData(AirdropData_SO cfg,bool isGift)
        {
            this.cfg = cfg;
            this.isGift = isGift;
            count = cfg.arriveCount;
            cool = cfg.cool;
            arriveTime = cfg.arriveTime;
            arriveCount = cfg.arriveCount;
        }

        public AirdropData(AirdropData_SO cfg):this(cfg,true)
        {
            isTmp = true;
            State = AirdropState.Arrive;
        }


        /// <summary>
        /// 允许使用的计数器 0=隐藏和无法使用 只对cfg.Authorize有效
        /// </summary>
        public int authorizeCounter;

        /// <summary>
        /// UI显示的时间进度[0-1]
        /// </summary>
        public float TimeScale
        {
            get
            {
                float re;
                switch (state)
                {
                    case AirdropState.Cool:
                        re= time/Mathf.Max(cool,0.1f);
                        break;
                    case AirdropState.Arrive:
                        re = time / Mathf.Max(arriveTime, 0.1f);
                        break;
                    case AirdropState.Sustain:
                        re = time / Mathf.Max(cfg.sustainTime, 0.1f);
                        break;
                    case AirdropState.Unavailable:
                        return 1;
                    default:
                        return 0;
                }
                return Mathf.Clamp01(re);
            }
        }

        public bool IsAuthorize => !cfg.authorize || authorizeCounter > 0;

        /// <summary>
        /// UI 是否应该显示此战备。
        /// 有授权：始终显示；
        /// 无授权但 unAuthorizeVisible：显示（虚化）；
        /// 无授权且无 unAuthorizeVisible：隐藏。
        /// </summary>
        public bool IsVisible => IsAuthorize || cfg.unAuthorizeVisible;

        /// <summary>
        /// 根据玩家死亡状态判断当前战备是否可用。
        /// deathEnable 战备：dead 时也可用（活着时正常可用）；
        /// 普通战备：dead 时不可用，非 dead 时可用。
        /// </summary>
        public bool IsCurrentlyAvailable(I_Actor player)
        {
            if (State == AirdropState.Unavailable)
                return false;
            if (!IsAuthorize)
                return false;
            bool isDead = player != null && player.ActorState == ActorState.Dead;
            if (isDead && !cfg.deathEnable)
                return false; // 死亡时，只有 deathEnable 战备可用
            return true;
        }

        /// <summary>
        /// 是否仅因死亡状态而不可用（授权和 State 都 OK，只是死亡且没有 deathEnable）。
        /// 用于 UI 判断：授权不满足时隐藏，死亡不可用时虚化显示。
        /// </summary>
        public bool IsOnlyDeathMismatch(I_Actor player)
        {
            if (State == AirdropState.Unavailable)
                return false;
            if (!IsAuthorize)
                return false;
            bool isDead = player != null && player.ActorState == ActorState.Dead;
            return isDead && !cfg.deathEnable;
        }

        [SerializeField]
        private AirdropState state;
        public AirdropState State { 
            get => state; 
            set 
            {
                state = value;
                switch (value)
                {

                    case AirdropState.Cool:
                        time = cool- cfg.sustainTime- arriveTime;//真的吗（woc好像是真的）
                        break;
                    case AirdropState.Arrive:
                        time = arriveTime;
                        break;
                    case AirdropState.Sustain:
                        time = cfg.sustainTime;
                        break;
                    case AirdropState.Ready:

                        break;
                    case AirdropState.Wait:

                        break;
                    case AirdropState.Unavailable:

                        break;
                }
                OnStateChange?.Invoke(this, value);
            }
        }
        public void Update()
        {
            if (time >= 0)
            {
                if ((time -= Time.deltaTime) < 0)
                {
                    switch (State)
                    {
                        case AirdropState.Cool:
                            State = AirdropState.Ready;
                            break;
                        case AirdropState.Arrive:
                            State = AirdropState.Sustain;
                            break;
                        case AirdropState.Sustain:
                            if (arriveCount>0 &&--count<=0)
                            {
                                State = AirdropState.Unavailable;
                            }
                            else
                            {
                                //Debug.LogError(cfg.name+"正常进CD");
                                State = AirdropState.Cool;
                            }
                            break;
                    }
                }
            }
        }
    }
    public enum AirdropState {
        /// <summary>就绪</summary>
        [InspectorName("就绪")] Ready,
        /// <summary>冷却</summary>
        [InspectorName("冷却")] Cool,
        /// <summary>等待释放</summary>
        [InspectorName("等待释放")] Wait,
        /// <summary>即将抵达</summary>
        [InspectorName("即将抵达")] Arrive,
        /// <summary>正在持续</summary>
        [InspectorName("正在持续")] Sustain,
        /// <summary>不可用</summary>
        [InspectorName("不可用")] Unavailable,
    }
}
