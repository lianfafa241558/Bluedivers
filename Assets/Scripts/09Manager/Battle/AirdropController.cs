using System.Collections.Generic;
using System.Linq;
using FPSGame.Core;
using FPSGame.GameContract;
using FPSGame.Game;
using FPSGame.Gameplay;
using UnityEngine;
using FPSGame.Audio;
using FPSGame.Data;
using FPSGame.Utils;
using FPSGame.GameData;
using FPSGame.AI;

namespace FPSGame.Managers
{

    /// <summary>
    /// 战备控制器，管理战备的输入、释放与状态切换。
    /// 
    /// 战备释放有两种流程：
    /// 
    /// 【普通释放】（isDirect = false）
    ///   Open → Input（输入方向键）→ OnWaitRelease（state=Wait）→ 关闭面板
    ///   → 玩家选点 → VFXAirdropEffect.SetOwner → BattleEventBus.Airdrop
    ///   → OnRelease（state=Arrive）→ 落地 → state=Sustain → 结束 → state=Cool/Unavailable
    /// 
    /// 【直接释放】（isDirect = true，如飞鹰装填、HealBag）
    ///   Open → Input（输入方向键）→ OnWaitRelease（state=Wait）→ 关闭面板
    ///   → 触发 OnStateChange(Wait) 回调执行效果 → state=Ready（不经过 VFX/OnRelease）
    /// 
    /// 直接释放的效果逻辑写在 OnAirdropStateChange 的 AirdropState.Wait 分支中。
    /// </summary>
    [AddComponentMenu("战备/战备控制器")]
    public class AirdropController : TickBehaviour
    {

        /// <summary>当前待释放的空投。**实际状态已下沉玩法层**（见 <c>AirdropReleaseState</c>：
        /// 它的类型属玩法层，且要被原样传进 `BattleEventBus` 事件 ⇒ 无法走契约投影），
        /// 这里只保留同名转发属性，让管理器/表现层既有调用点零改动。</summary>
        public static AirdropData WaitRelease
        {
            get => AirdropReleaseState.WaitRelease;
            set => AirdropReleaseState.WaitRelease = value;
        }

        private const float _PowerMax = 10;
        private const float _PowerReSpeed = 0.167f;//60s回满

        public List<AirdropData> useAd;

        IActor Player => ActorsManager.Player;

        /// <summary>玩家此刻是否处在信号干扰范围（见 <see cref="SignalJammer"/>）：干扰期间战备都叫不出，UI 据此显示"受到干扰"。</summary>
        public bool IsJammed => SignalJammer.IsBlocked(Player);

        /// <summary>该战备此刻是否被干扰：干扰区内且没勾 <see cref="AirdropData_SO.ignoreJammer"/>。
        /// 输入与 UI 共用这一条判据，别再各写一遍（漏一处就会出现"UI 灰的却叫得出来"）。</summary>
        public bool IsJammerBlocked(AirdropData item) => IsJammed && !item.cfg.ignoreJammer;

        private Dictionary<int,AirdropData_SO> adDic=> ResSvc.airdropDic;

        [SerializeField]
        private List<DirectionEnum> inputDir;

        /*
        public void Init()
        {

        }*/

        protected override void Start()
        {
            base.Start();
            TickTime = 0.02f;
            InputManager.BindDown(WindowStateEnum.Game,InputState.Airdrop, Open);
            InputManager.BindDown(WindowStateEnum.Airdrop, InputState.Airdrop, Close);
            BattleEventBus.OnCancelAirdrop += OnCancel;
            BattleEventBus.OnAirdrop += OnRelease;
            UnitEventBus.OnPlayerDead += OnPlayerDeath;
        }

        public override bool Tick()
        {
            useAd.ForEach(item => item.Update());
            return true;
        }

        private void OnDestroy()
        {
            InputManager.UnBindDown(WindowStateEnum.Game, InputState.Airdrop, Open);
            InputManager.UnBindDown(WindowStateEnum.Airdrop, InputState.Airdrop, Close);
            BattleEventBus.OnCancelAirdrop -= OnCancel;
            BattleEventBus.OnAirdrop -= OnRelease;
            UnitEventBus.OnPlayerDead -= OnPlayerDeath;
        }


        protected override void Update()
        {
            base.Update();
            if(WndManager.WindowState == WindowStateEnum.Airdrop)
            {
                if (InputManager.GetDown(InputState.Left)) Input(DirectionEnum.Left);
                else if (InputManager.GetDown(InputState.Up)) Input(DirectionEnum.Up);
                else if (InputManager.GetDown(InputState.Right)) Input(DirectionEnum.Right);
                else if (InputManager.GetDown(InputState.Down)) Input(DirectionEnum.Down);
            }
        }


        public void Init()
        {

            inputDir = new();


            useAd = new();
            List<int> subAd = new();
            // 读取**本机玩家**的任务所需战备 = 任务级 + 自己角色的默认战备
            //（角色默认是每人一份 ⇒ 别再读 nowTask.RequiredAD，那份只放任务级，见 TaskManager.RequiredADOf）
            var required = TaskManager.Instance.RequiredADOf(TeamManager.Instance.Self.roleName);
            for (int i = 0; i < required.Count; ++i)
            {
                //Debug.LogError("添加任务所需战备"+ ResManager.airdropDic[required[i]].showName);
                useAd.Add(new(ResSvc.airdropDic[required[i]], true));
            }
            //读取玩家携带的战备
            var arr = TeamManager.Instance.Self.airdrop;

            //if(arr.Any(id=> ResSvc.airdropDic[id].deliveryType == AirdropDeliveryEnum.Jet))
            //{
            //    useAd.Add(new(ResSvc.airdropDic[Constants.EagleReloadId],true));
            //}
            for (int i = 0; i < arr.Length; ++i)
            {
                if (arr[i] <= 0) continue;
                var subArr = ResSvc.airdropDic[arr[i]].subAirdrop;
                if (!subArr.IsValid()) continue;
                for (int u = 0; u < subArr.Length; ++u)
                {
                    int sub = subArr[u];
                    if (sub > 0 && !subAd.Contains(sub))
                    {
                        useAd.Add(new(ResSvc.airdropDic[sub], false));
                        subAd.Add(sub);
                    }
                }
            }



            for (int i = 0; i < arr.Length; ++i)
            {
                useAd.Add(new(ResSvc.airdropDic[arr[i]], false));
            }

            foreach(var item in useAd)
            {
                item.OnStateChange += OnAirdropStateChange;//这样只有自己叫的才考虑
            }
            if (BattleManager.Instance.HaveBooster(BoosterType.ReinforcementBudget))
            {
                ApplyReinforcementBudget();
            }
        }

        /// <summary>
        /// 应用"增加增援预算"强化：提高增援（HealBag）的可用次数。
        /// </summary>
        public void ApplyReinforcementBudget()
        {
            int addCount = 2; // 额外增援次数
            foreach (var item in useAd)
            {
                if (item.cfg.ID == Constants.HealBag)
                {
                    item.arriveCount += addCount;
                    item.cool -= 30;
                    if (item.State != AirdropState.Unavailable) item.count += addCount;
                    break;
                }
            }
        }


        private void Open()
        {
            //if (Player.ActorState == ActorState.Hide) return;

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
            // 干扰中：只有勾了"忽略干扰"的战备还能叫，其余输入一律吃掉并给一声"无效"反馈。
            // ⚠ 必须挡在匹配里，不能只靠 UI 虚化（键盘输入不看 UI，只虚化会出现"看着灰的还能叫出信标"）
            bool jammed = IsJammed;

            inputDir.Add(dir);
            //如果和当前战备全部不符合就清空
            bool keep = false;
            foreach (var item in useAd) 
            {
                if (!item.IsCurrentlyAvailable(Player)) continue;//当前不可用的战备跳过
                if (jammed && !item.cfg.ignoreJammer) continue;//干扰中只放行"忽略干扰"的战备
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
            BattleEventBus.InputAirdrop(inputDir);

        }
        /// <summary>完成输入，等待释放</summary>
        private void OnWaitRelease(AirdropData item)
        {
            item.State = AirdropState.Wait;
            WaitRelease = item;
            Close();
            //通过这个事件来让对应的类调用来直接强制释放
            BattleEventBus.SelectAirdrop(Player.gameObject,item);

            if (item.cfg.isDirect)//直接释放（飞鹰装填和HealBag）
            {
                // 次数耗尽时保持 Unavailable：Wait 分支扣减后置为 Unavailable，此处不得覆盖回 Ready，
                // 否则会同时导致"用完后仍能继续呼叫"和"团灭判负无法触发"
                if (item.State != AirdropState.Unavailable)
                {
                    item.State = AirdropState.Sustain;
                }
                WaitRelease = null;
            }
            else if (Player.ActorState == ActorState.Hide)
            {
                var camera = Player.transform.GetComponent<PlayerController>().PlayerCamera;
                var targetPoint = camera.transform.TransformPoint(0,0,30);
                if (Physics.Raycast(camera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0)), out var hit, 30))
                {
                    targetPoint = hit.point;
                }
                var beacon = VFXManager.Creat(ResSvc.Instance.LoadObject<GameObject>("Prefabs/Airdrop/VFX_AirdropPoint"), targetPoint, Quaternion.Euler(0, camera.transform.eulerAngles.y, 0), null);
                beacon.GetComponent<IVfxEffect>()?.SetOrigin(Player.gameObject, Player.transform.GetComponent<PlayerWeaponsManager>().GetWeapon(WeaponTypeEnum.FlareGun).gameObject, null, targetPoint);
                //BattleEventBus.Airdrop(Player.gameObject, null, targetPoint, item);
            }

        }

        /// <summary>释放战备</summary>
        private void OnRelease(GameObject owner, GameObject target, Vector3 point, AirdropData data)
        {
            if (owner == null) return;
            if (data == null) {
                Debug.LogError("战备丢失");
                return; }
            data.State = AirdropState.Arrive;
            if (owner.TryGetComponent(out PlayerController player))
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
            if ((state && data.authorizeCounter == 1) || (!state && data.authorizeCounter == 0)) BattleEventBus.AuthorizeAirdrop();
            //Debug.LogError(ad.cfg.showName+"授权状态"+ad.authorizeCounter+ " "+ad.IsAuthorize);
        }

        /// <summary>
        /// 团队冷却（<see cref="AirdropData_SO.teamCool"/>）：把本机玩家那份**同名战备**置为冷却。
        ///
        /// <para>▍为什么要外部来调：战备表是**各端各自一份**的（<see cref="useAd"/> 只装本机玩家那份），
        /// 队友放的那一发在本机只是"呼叫同步"（4027/4028）⇒ 由 <c>NetFriendBridge.HandleAirdropCall</c> 调本方法冻住本机这份。
        /// 判据（<c>cfg.teamCool</c>）在这里查，调用方只管给 id。</para>
        /// <para>⚠ <paramref name="except"/> 传"本机正在释放的那一发"：它自己走 Arrive→Sustain→Cool，
        /// 且 <c>Cool</c> 的时长是 <c>cool - 持续 - 部署</c> ⇒ 结束时刻与这里写的 <c>time = cool</c> 正好对齐，别再去动它。</para>
        /// </summary>
        /// <param name="airdropId">被释放的战备 id（<c>AirdropData_SO.ID</c>）。</param>
        /// <param name="except">本机自己正在释放的那一发，跳过。</param>
        public void ApplyTeamCool(int airdropId, AirdropData except = null)
        {
            if (airdropId <= 0 || useAd == null) return;
            var dic = adDic;
            if (dic == null || !dic.TryGetValue(airdropId, out var cfg) || cfg == null || !cfg.teamCool) return;

            foreach (var item in useAd)
            {
                if (item == except || item.cfg == null || item.cfg.ID != airdropId) continue;
                // 次数耗尽的（Unavailable）不动：冻它没意义，还会破坏"用完了"这个判据（团灭判负读它）
                if (item.State == AirdropState.Unavailable) continue;
                item.State = AirdropState.Cool;
                item.time = item.cool;   // 同 coolGroup 口径：从"释放瞬间"起算完整冷却
                NetSyncLog.SyncLog("战备/团队冷却", $"{cfg.showName}(id={airdropId}) {item.cool}s");
            }
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
                        if (!haveVaild)
                        {
                            var reloadAd = useAd.FirstOrDefault(item=> item.cfg.ID == Constants.EagleReloadId);
                            if (reloadAd != null)
                                OnWaitRelease(reloadAd);
                        }
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
                    //飞鹰装填
                    if (data.cfg.deliveryType == AirdropDeliveryEnum.Jet)
                    {
                        foreach (var item in useAd)
                        {
                            if (item.cfg.ID == Constants.EagleReloadId && !item.IsAuthorize)
                            {
                                _Authorize(item, true);
                            }
                        }
                    }
                    //战备共CD
                    if (!string.IsNullOrEmpty(data.cfg.coolGroup))
                    {
                        foreach (var item in useAd)
                        {
                            if (item != data && item.cfg.coolGroup == data.cfg.coolGroup)
                            {
                                if (item.State != AirdropState.Unavailable) item.State = AirdropState.Cool;
                                item.time = item.cool;//因为正常cd阶段是减去了持续和呼叫时间
                            }
                        }
                    }
                    //团队冷却：这一发勾了 teamCool ⇒ 本机其余同名条目立刻进 CD；队友那几台靠呼叫同步（4027/4028）
                    //到 NetFriendBridge.HandleAirdropCall 后各调一次 ApplyTeamCool。
                    //⚠ 直接释放(isDirect)的战备不走信标、也到不了这里 ⇒ 目前对它们不生效（见 AirdropData_SO.teamCool）
                    ApplyTeamCool(data.cfg.ID, data);
                    break;
                case AirdropState.Cool:
                    if (data.cfg.ID == Constants.HealBag)
                    {
                        //还有层数的时候不读条
                        if (data.arriveCount > 0)
                        {
                            data.State = AirdropState.Ready;
                            data.time = 0;
                        }

                    }
                    break;
            }
        

        }

        }
}
