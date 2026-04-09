using System.Collections.Generic;
using System.Linq;
using Core;
using GameContract;
using Unity.BaseTool;
using Unity.FPS.Game;
using UnityEngine;
public class AirdropController : MonoBehaviour
{

    public static AirdropData WaitRelease;

    private const float _PowerMax = 10;
    private const float _PowerReSpeed = 0.167f;//60s回满

    public List<AirdropData> useAd;

    I_Actor Player => ActorsManager.Player;

    private Dictionary<int,AirdropData_SO> adDic=> ResManager.airdropDic;

    [SerializeField]
    private List<DirectionEnum> inputDir;

    /*
    public void Init()
    {

    }*/

    private void Start()
    {

        InputManager.Bind(WindowStateEnum.Game,InputState.Airdrop, Open);
        InputManager.Bind(WindowStateEnum.Airdrop, InputState.Airdrop, Close);
        GlobalEventManager.OnCancelAirdrop += OnCancel;
        GlobalEventManager.OnAirdrop += OnRelease;
        GlobalEventManager.OnPlayerDead += OnDeath;
    }


    private void OnDestroy()
    {
        InputManager.UnBind(WindowStateEnum.Game, InputState.Airdrop, Open);
        InputManager.UnBind(WindowStateEnum.Airdrop, InputState.Airdrop, Close);
        GlobalEventManager.OnCancelAirdrop -= OnCancel;
        GlobalEventManager.OnAirdrop -= OnRelease;
    }


    void Update()
    {

        if(GameRoot.WindowState == WindowStateEnum.Airdrop)
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
            useAd.Add(new() {
                cfg = ResManager.airdropDic[required[i]],
                isGift = true,
            });
        }
        //读取玩家携带的战备
        var arr = RoomManager.Instance.Self.airdrop;
        for (int i=0;i< arr.Length; ++i)
        {
            useAd.Add(new() {
                cfg = ResManager.airdropDic[arr[i]],
                isGift = false,
            });
        }

    }


    private void Open()
    {
        if (Player.ActorState == ActorState.Dead) return;

        GameRoot.WindowState = WindowStateEnum.Airdrop;
        inputDir.Clear();
        OnCancel(Player.gameObject,WaitRelease);
    }
    private void Close()
    {
        GameRoot.WindowState = WindowStateEnum.Game;
    }

    private void Input(DirectionEnum dir)
    {
        inputDir.Add(dir);
        //如果和目前战备全都不符合就清空
        bool keep = false;
        foreach (var item in useAd) 
        {
            if (!item.IsAuthorize) continue;//没有授权的战备跳过
            bool state = item.State==AirdropState.Ready && item.cfg.opter.Compare(inputDir);
            keep |= state;
            if(state&& inputDir.Count == item.cfg.opter.Length)
            {
                AudioManager.PlaySound(new("AirDrop/superbeacon_active"));
                OnWaitRelease(item);
                inputDir.Clear();
                return;
            }
        }

        if (keep)
        {
            AudioManager.PlaySound(new("AirDrop/superbeacon_button"));
        }
        else
        {
            inputDir.Clear();
            AudioManager.PlaySound(new("AirDrop/superbeacon_throw"));
        }
        GlobalEventManager.InputAirdrop(inputDir);

    }
    /// <summary>完成输入，等待释放</summary>
    private void OnWaitRelease(AirdropData item)
    {
        item.State = AirdropState.Wait;
        WaitRelease = item;
        Close();
        GlobalEventManager.SelectAirdrop(Player.gameObject,item);
    }

    /// <summary>释放战备</summary>
    private void OnRelease(GameObject owner, GameObject target, Vector3 point, AirdropData data)
    {
        if (owner == null) return;
        if (GameRoot.GameState == GameStateEnum.Game && owner.TryGetComponent(out PlayerController player))
        {
            BattleManager.Instance.AddBattleDataItem(player.PlayerIndex, "呼叫战备次数");
        }
        data.State = AirdropState.Arrive;
        WaitRelease = null;
    }

    /// <summary>取消战备</summary>
    private void OnCancel(GameObject go,AirdropData item)
    {
        if (!item.IsValid()||go !=Player.gameObject) return;
        Debug.LogError("取消准备中的战备"+item);
        item.State = AirdropState.Ready;

        WaitRelease = null;
    }

    void OnDeath(Actor _)
    {
        if (WaitRelease != null)
        {
            OnCancel(Player.gameObject, WaitRelease);
        }
        if (GameRoot.WindowState == WindowStateEnum.Airdrop)
        {
            Close();
        }
    }

    public void Authorize(int id,bool state)
    {
        //Debug.LogError("尝试授权"+id+state);
        var ad=useAd.Find(item =>item.cfg.ID==id);
        if (ad != null)
        {
            ad.authorizeCounter += state ? 1 : -1;
            if ((state && ad.authorizeCounter==1)||(!state&& ad.authorizeCounter == 0)) GlobalEventManager.AuthorizeAirdrop();
            //Debug.LogError(ad.cfg.showName+"授权状态"+ad.authorizeCounter+ " "+ad.IsAuthorize);
        }
    }


    [System.Serializable]
    public class AirdropData {
        public AirdropData_SO cfg;
        public bool isGift;
        public float time;
        public bool isTmp;
        /// <summary>
        /// 允许使用的计数器，=0时无法使用(只对cfg.Authorize有效)
        /// </summary>
        public int authorizeCounter;

        public float TimeScale
        {
            get
            {
                switch (state)
                {
                    case AirdropState.Cool:
                        return time/cfg.cool;
                    case AirdropState.Arrive:
                        return time / cfg.arriveTime;
                    case AirdropState.Sustain:
                        return time /cfg.sustainTime;
                    default: return 0;
                }
            }
        }

        public bool IsAuthorize => !cfg.authorize || authorizeCounter > 0;

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
                        time = cfg.cool;
                        break;
                    case AirdropState.Arrive:
                        time = cfg.arriveTime;
                        break;
                    case AirdropState.Sustain:
                        time = cfg.sustainTime;
                        break;
                    case AirdropState.Ready:

                        break;
                    case AirdropState.Wait:

                        break;
                }
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
                            State = AirdropState.Cool;
                            break;
                    }
                }
            }
        }
    }
    public enum AirdropState {
        /// <summary>就绪</summary>
        [CustomLabel("就绪")] Ready,
        /// <summary>冷却</summary>
        [CustomLabel("冷却")] Cool,
        /// <summary>等待释放</summary>
        [CustomLabel("等待释放")] Wait,
        /// <summary>即将抵达</summary>
        [CustomLabel("即将抵达")] Arrive,
        /// <summary>正在持续</summary>
        [CustomLabel("正在持续")] Sustain,
    }
}
