using FPSGame.Game;
using UnityEngine;
using System.Collections.Generic;
using FPSGame.Gameplay;

using FPSGame.Core;
using FPSGame.Core.Interface;
using FPSGame.Utils;
using FPSGame.GameContract;
using FPSGame.Data;
using FPSGame.GameData;
using FPSGame.Managers;
using FPSGame.Audio;
using FPSGame.AI;
using FPSGame.Weapon;

namespace FPSGame.Effect
{

/// <summary>
/// 空投全流程特效（舱、轰炸、飞鹰、运输机）。
/// </summary>
[AddComponentMenu("特效/空投特效")]
public class VFXAirdropEffect : MonoBehaviour, IVfxEffect, IAirdropEffect
{
    /// <summary>
    /// 契约实现（<see cref="IAirdropEffect"/>）：按 id 解析配置后走原重载。
    /// ▍为什么这么开：<c>BattleManager</c> 原先写 <c>GetComponent&lt;VFXAirdropEffect&gt;()</c>
    /// 并传 `AirdropData_SO`（属 `04_Data`）⇒ 管理器反向依赖表现层、契约层也表达不了那个类型。
    /// 现在契约只吃 `int`，解析放这里（Effect 在管理器之上 ⇒ 可直连 `ResSvc`）。
    /// </summary>
    public void TmpAirdrop(Vector3 point, int airdropId, System.Action<GameObject> action)
        => TmpAirdrop(point, ResSvc.Instance.GetAirdrop(airdropId), action);


    public System.Action<GameObject> OnCreatObject;

    [SerializeField]
    private GameObject normalPod;
    [SerializeField]
    private GameObject eagle;
    [SerializeField]
    private GameObject neoNimbusVehicle;

    //[SerializeField]
    //private List<NoticeData_SO> warning;
    //[Display(true,false,true)]
    //[HideInInspector]
    public AirdropData data;

    [SerializeField]
    private Transform effectRangeCube,effectRangeCircle;
    [SerializeField]
    private new Light light;

    [SerializeField]
    private Transform m_creatObject;
    /// <summary>预计的降落时间</summary>
    private float m_ExpectedDuration;

    public float showTime = 0;

    private LimitedLife m_Lift;
    private GameObject m_owner;

    /// <summary>本次呼叫者的会话 sid（0 = 房主；-1 = 还没落定，两个入口各自补上）。</summary>
    private int _callerSid = -1;

    /// <summary>
    /// 本次呼叫者的 sid —— 这次生成的载具（外骨骼/炮台/运输机）按它取载具改装。
    /// <para>▍两个入口分别落定：<see cref="SetOwner"/>（**本机玩家自己**叫的）⇒ 本机 sid；
    /// <see cref="TmpAirdrop(Vector3, int, System.Action{GameObject})"/>（远端复现 / 任务脚本）⇒
    /// <see cref="SetCallerSid"/> 带来的值，没人带就是 <c>0</c>（房主）。</para>
    /// </summary>
    private uint CallerSid => _callerSid < 0 ? 0u : (uint)_callerSid;

    /// <summary>契约实现：远端复现时由 <c>BattleManager.ReleaseAirdrop</c> 带上呼叫者 sid（0 = 房主）。</summary>
    public void SetCallerSid(uint sid) => _callerSid = (int)sid;

    /// <summary>
    /// 把"本次呼叫者"的归属写到刚生成的载具上（含子物体）。
    /// <para>▍为什么是"生成后再打标"而不是"生成时就知道"：<c>Instantiate</c> 会**同步**跑完
    /// <c>Awake</c>（那时只知道默认归属）⇒ 必须生成后调 <c>SetOwnerSid</c> 纠正一次
    /// （<c>BattleApplyVehicleData</c> 的应用是**可重入**的，代价只是重建一次武器/贴图）。</para>
    /// </summary>
    private void StampCaller(params Transform[] roots)
    {
        for (int r = 0; r < roots.Length; ++r)
        {
            var root = roots[r];
            if (root == null) continue;

            var targets = root.GetComponentsInChildren<BattleApplyVehicleData>(true);
            for (int i = 0; i < targets.Length; ++i) targets[i].SetOwnerSid(CallerSid);
        }
    }

    /// <summary>本次呼叫是否跳过信标等待阶段("取消空投准备时间"强化)，回收时复位</summary>
    private bool m_SkipDeployWait;
    /// <summary>本次呼叫空投舱的落地速度倍率("空投降落速度"强化，1=配置值)，回收时复位</summary>
    private float m_PodFallScale = 1f;

    public float m_lastWarnTime = 0;
    public void SetOwner(GameObject owner, GameObject weaponRoot, Collider collider, Vector3 point) {
        //其实这里有个bug，如果连续放，就会变成同时落地，但是实际上拍空投有Cd，所以直接不管！
        data = AirdropController.WaitRelease;
        // ⚠ 没有待释放的战备时**直接忽略**：远端重放"开枪命中特效"也会走到这里（命中特效/伤害配置都是
        //   本端自己的状态），那时 WaitRelease 为 null ⇒ 后面 data.cfg 必 NRE。
        //   ⚠ 必须顺手**回收信标**：只 return 的话组件还激活、`data` 是 null ⇒ Update 每帧 NRE（2026-10-10 打包端实测）。
        if (data == null || owner == null)
        {
            Debug.LogWarning("[VFXAirdropEffect] 释放信标但没有待释放的战备（远端重放/脚本误触发）⇒ 回收信标");
            RecycleSelf();
            return;
        }
        //同时,锁定的点总是会是实际的脚下而不是单位头顶
        if (Physics.Raycast(point+Vector3.up*10,Vector3.down, out var hit,200, LayerDefinition.GroundLayers))
        {
            //Debug.LogError("point"+point+"击中点"+hit.point);
            transform.position = point = hit.point;
        }
        // ⚠ 朝向必须在**抛事件之前**定好：`NetFriendBridge.HandleLocalAirdrop` 正是在这个事件里读信标的
        //   eulerAngles 当"释放朝向"发出去的；原来这两句写在事件之后 ⇒ 过网的是特效创建时的旋转
        //   （identity / 命中面法线），远端那份信标朝向与释放者无关（2026-10-10 用户实测"方向不对"）。
        transform.parent = null;
        transform.eulerAngles = new(0, owner.transform.eulerAngles.y, 0);
        m_owner = owner;

        BattleEventBus.Airdrop(owner, gameObject, point, data);
        //武器参数取自信号枪(weaponRoot)，信标特效物体自身不含武器组件
        if (weaponRoot.IsValid()
            && weaponRoot.TryGetComponent(out WeaponPlayerController weapon)
        ){
            //取消空投准备时间：跳过等待阶段，直接抵达
            if (weapon.GetAttr("取消空投准备时间") > 0
                && data.cfg.type == AirdropData_SO.AirdropType.Blue
            ){
                m_SkipDeployWait = true;
            }
            //自费空投：冷却*0.9（以配置值为基准换算，避免多次呼叫被反复缩减）
            if (weapon.GetAttr("自费空投") > 0
                && data.cfg.type == AirdropData_SO.AirdropType.Blue
            ){
                data.cool = Mathf.RoundToInt(data.cfg.cool * 0.9f);
            }
            //空投降落速度：空投舱落地速度*2
            if (weapon.GetAttr("空投降落速度") > 0
                && data.cfg.deliveryType == AirdropDeliveryEnum.Pod
            ){
                m_PodFallScale = 2f;
            }
        }

        // 本机玩家自己叫的（信号枪/超级信标）⇒ 归属就是本机 ⇒ 生成的载具按**本机**载具改装渲染
        if (_callerSid < 0) _callerSid = (int)VehicleCustomState.LocalSid;
        Init();
    }
    /// <summary>
    /// 回收"没有数据可用"的信标本体（回池 + 关掉）。
    /// <para>▍为什么必须关掉：本组件的 <see cref="Update"/> 每帧都跑，而 <c>data</c> 为 null 时第一句就 NRE
    /// ⇒ 打包端每帧刷异常（2026-10-10 实测：<c>VFXAirdropEffect.Update</c> NRE 刷屏）。</para>
    /// </summary>
    private void RecycleSelf()
    {
        data = null;
        VFXManager.Release(gameObject);
        gameObject.SetActive(false);
    }

    public void TmpAirdrop(Vector3 point, AirdropData_SO data, System.Action<GameObject> action)
    {
        // 兜底：id 解析失败（配置被删/旧包）时别把信标留成一个"激活但无数据"的对象（Update 会每帧 NRE）
        if (data == null)
        {
            Debug.LogWarning("[VFXAirdropEffect] TmpAirdrop 收到空配置（战备 id 解析失败？）⇒ 回收信标");
            RecycleSelf();
            return;
        }
        this.data = new(data);

        if (Physics.Raycast(point + Vector3.up * 100, Vector3.down, out var hit, 150, LayerDefinition.GroundLayers))
        {
            transform.position = point = hit.point;
        }
        BattleEventBus.Airdrop(null, gameObject, point, this.data);

        transform.parent = null;
        //transform.eulerAngles = Vector3.zero;
        m_owner = ActorsManager.Player.gameObject;
        // ⚠ 上面那句 m_owner 是给表现/归属用的，**不代表呼叫者**（远端复现时也会被写成我）
        //   ⇒ 呼叫者只认 SetCallerSid 带来的值；没人带（任务脚本自己放的）就是房主。
        if (_callerSid < 0) _callerSid = 0;
        if(action.IsValid()) OnCreatObject += action;
        Init();
    }

    private void Init()
    {

        m_creatObject = null;
        if (m_SkipDeployWait) ApplySkipDeployWait();
        SetDisplay();
        switch (data.cfg.deliveryType)
        {
            case AirdropDeliveryEnum.Pod:
                StartPod();
                break;
            case AirdropDeliveryEnum.Bomb:
                StartBomb();
                break;
            case AirdropDeliveryEnum.Jet:
                StartJet();
                break;
            case AirdropDeliveryEnum.Medivac:
                StartMedivac();
                break;
        }
    }

    /// <summary>
    /// 应用"取消空投准备时间"：把部署时间压缩到最短，跳过信标等待阶段。
    /// 空投舱必须保留自由落体所需时间，其余投送方式(轰炸/飞鹰/运输机)立即抵达。
    /// 注意：释放时 State 已进入 Arrive 并按原部署时间初始化了剩余时间，这里必须同步 data.time。
    /// </summary>
    private void ApplySkipDeployWait()
    {
        data.arriveTime = data.cfg.deliveryType == AirdropDeliveryEnum.Pod
            ? Mathf.Max(1, Mathf.CeilToInt(PodFallTime()))
            : 0;
        data.time = data.arriveTime;
    }

    /// <summary>空投舱自由落体所需时间(不含缓冲)：t=√(2h/g)，g取20 → √(h/10)；落地速度倍率越高用时越短</summary>
    private float PodFallTime() => Mathf.Sqrt(data.cfg.arriveHeight * 0.1f) / Mathf.Max(m_PodFallScale, 0.01f);

    /// <summary>战备计时的**固定逻辑步长**（与 <c>Constants.LoginFrame</c> 同源；理由见 <see cref="Update"/>）。</summary>
    private static readonly float LogicStep = FPSGame.Core.Constants.LoginFrame.RawFloat;

    /// <summary>单个渲染帧最多补几步（0.02 × 10 = 0.2s）：长卡顿 / 断点后别一次补完几十帧。</summary>
    private const int MaxLogicSteps = 10;

    /// <summary>欠下的逻辑帧时间（累加器，按墙钟补步）。</summary>
    private float _logicLeft;

    private void Update()
    {
        if (data == null) return;   // 兜底：本组件被激活但没有数据（见 SetOwner/TmpAirdrop 的早退）⇒ 什么都不做

        // ⚠⚠ 这里原来是"每渲染帧 `data.Update()`"，而 `AirdropData.Update()` 内部是 `time -= 逻辑帧时长(0.02)`
        //   ⇒ 战备计时**随帧率变快**（60fps 快 1.2 倍、144fps 快 2.88 倍）。单机看不出，
        //   联机时两端帧率不同 ⇒ 同一发战备一边"即将抵达"、一边已经"正在进行"（2026-10-07 实测）。
        //   现在按墙钟补帧：固定 0.02s 一步，与本机那份（`AirdropController.Tick`，定步 0.02）同速。
        if (data.isTmp)
        {
            _logicLeft += Time.deltaTime;
            int steps = 0;
            while (_logicLeft >= LogicStep && steps < MaxLogicSteps)
            {
                _logicLeft -= LogicStep;
                data.Update();
                ++steps;
            }
            // 欠账超过一步就别再攒（否则下一帧要连补几十步 ⇒ 瞬间跳到结束）
            if (_logicLeft > LogicStep) _logicLeft = LogicStep;
        }

        showTime += Time.deltaTime;
        switch (data.cfg.deliveryType)
        {
            case AirdropDeliveryEnum.Pod:
                UpdatePod();
                break;
            case AirdropDeliveryEnum.Bomb:
                UpdateBomb();
                break;
            case AirdropDeliveryEnum.Jet:
                UpdateJet();
                break;
            case AirdropDeliveryEnum.Medivac:
                UpdateMedivac();
                break;
        }
        TryWarning();
    }

    private void OnDisable()
    {
        //强化状态只对本次呼叫有效，特效被回收(池化复用)前必须复位
        m_SkipDeployWait = false;
        m_PodFallScale = 1f;
        _callerSid = -1;   // 同上：呼叫者也只对本次呼叫有效（信标是池化复用的）

        if (!m_creatObject.IsValid()) return;

        switch (data.cfg.deliveryType)
        {
            case AirdropDeliveryEnum.Pod:
                EndPod();
                break;
            case AirdropDeliveryEnum.Bomb:
                EndBomb();
                break;
            case AirdropDeliveryEnum.Jet:
                EndJet();
                break;
            case AirdropDeliveryEnum.Medivac:
                EndMedivac();
                break;
        }
        if (m_creatObject&&m_creatObject.TryGetComponent(out AirdropPod pro))
        {
            pro.OnHit -= PodHit;
        }
        OnCreatObject = null;
        data = null;
        m_creatObject = null;
        //m_particle.Stop(true);
        //m_Lift.SetLift(1);
        
    }

    #region 空投舱系
    void StartPod()
    {
        //重力加速度g=10,公式h=1/2*g*t^2=5*t^2;
        //反转取时间就是t=sqrt(s/5)开根号
        m_ExpectedDuration = PodFallTime()+0.5f;
        if (m_ExpectedDuration > data.arriveTime) { Debug.LogError(data.cfg.showName + "设置的高度不足以使其在限时内自由落体落地"+"预计需要的时间"+m_ExpectedDuration); }
    }
    void UpdatePod()
    {
        if (data.State== AirdropState.Arrive && !m_creatObject.IsValid())
        {
            
            if (data.time < m_ExpectedDuration)
            {
                AudioSvc.PlaySound(new("AirDrop/PodIntA_1", transform.position + 10 * Vector3.up, 50, AudioGroups.Weapon,0.8f));
                //使用标准空投舱
                if (data.cfg.useNormalPod)
                {
                    m_creatObject = VFXManager.Creat(normalPod, transform.position + Vector3.up * data.cfg.arriveHeight, transform.rotation, null).transform;
                }
                //直接使用自定义物??
                else
                {
                    m_creatObject = Instantiate(data.cfg.creatObect, transform.position + Vector3.up * data.cfg.arriveHeight, transform.rotation).transform;
                    DontDestroyOnLoad(this);
                    if (m_creatObject.TryGetComponentInChildren(out WeaponBaseController weapon))
                    {
                        weapon.Owner = m_owner;
                    }
                    if (m_creatObject.TryGetComponentInChildren(out Actor actor))
                    {
                        actor.Team = m_owner.GetComponent<IActor>().Team;
                        actor.Owner = m_owner.GetComponent<IActor>();
                    }
                }

                StampCaller(m_creatObject);   // ★ 归属：这次呼叫者（谁叫的算谁的）

                if (m_creatObject.TryGetComponentInChildren(out AirdropPod pro))//补给舱
                {
                    pro.SetFallSpeedScale(m_PodFallScale);
                    pro.Launch(m_owner);
                    pro.OnHit += PodHit;
                }

                var list = m_creatObject.GetComponentsInChildren<Animator>();
                foreach(var anim in list)
                {
                    anim.enabled = false;
                }

            }
        }
        else if(data.State == AirdropState.Sustain && !m_creatObject.IsValid()&& data.time> 0.5f)
        {
            data.time = 0.5f;
            //m_Lift.ResetLift(0.5f);
        }

    }


    void PodHit(ProjectileHitData hitData)
    {
        if (hitData.collider&&!LayerDefinition.GroundLayers.Contains(1<<hitData.collider.gameObject.layer))
        {
            //Debug.LogError("截获撞击的物体 "+ hitData.collider.gameObject + " 层级 "+hitData.collider.gameObject.layer +"  "+ System.Convert.ToString((1<<hitData.collider.gameObject.layer),2)+" 地面层级 " + System.Convert.ToString(LayerDefinition.GroundLayers.value,2), hitData.collider.gameObject);
            return;
        }
        AudioSvc.Stop("PodIntA_1");

        //AudioManager.PlaySound(new("AirDrop/PodDoor_Stop_1", transform.position, 50, AudioGroups.WeaponShoot));
        AudioSvc.PlaySound(new("AirDrop/SupplyPod/SupplyPodSpawnImpactCombinedA_1", hitData.pos, 50, AudioGroups.Weapon,0.25f));
            //直接在落地的时候就该创建而不是结束
            //Debug.LogError("落地位置 "+ m_creatObject.position+"标记位置"+ transform.position+"碰撞位置"+hitData.pos);
        m_creatObject.position = transform.position;
        

        var list = m_creatObject.GetComponentsInChildren<Animator>();
        foreach (var anim in list)
        {
            anim.enabled = true;
        }

        if (data.cfg.sustainHideBeacon)
        {
            for (int i = 1; i < 5; ++i)
            {
                transform.GetChild(i).gameObject.SetActive(false);//部分关闭
            }
        }
        if (data.cfg.useNormalPod)
        {
            if (data.cfg.permanentPod)
            {
                m_creatObject.GetComponent<LimitedLife>().ResetLift(9999);
            }
            if (m_creatObject.TryGetComponentInChildren(out AirdropPod pro))
            {
                pro.OnHit -= PodHit;
            }
            //Debug.LogError("创建位置" + transform.position);
            //创建实际物体
            var go = m_creatObject=Instantiate(data.cfg.creatObect, transform.position + 0.2f * Vector3.up, transform.rotation).transform;
            if (go.TryGetComponentInChildren(out WeaponBaseController weapon))
            {
                weapon.Owner = m_owner;
            }
            if (go.TryGetComponentInChildren(out Actor actor))
            {
                actor.Team = m_owner.GetComponent<IActor>().Team;
                actor.Owner = m_owner.GetComponent<IActor>();
            }
        }
        else
        {
            if (m_creatObject.TryGetComponent(out AirdropPod pro))
            {
                pro.OnHit -= PodHit;
            }
        }

        StampCaller(m_creatObject);   // ★ 归属：这次呼叫者
        OnCreatObject?.Invoke(m_creatObject.gameObject);
    }

    void EndPod()
    {
        
    }

    #endregion

    #region 轰炸区

    void StartBomb()
    {
        //Destroy(m_creatObject.gameObject);

    }

    /// <summary>
    /// 该战备那把武器的**确定性随机种子**：同一战备在各端取同一个值 ⇒ 弹道散布/落点一致
    /// （轨道轰炸这类"各端各自执行一次"的战备必须这样，否则看起来就是没同步）。
    ///
    /// <para>▍只按 <c>ID</c> 派生（不带落点）：落点是各端各自那份副本算出来的，拿它当种子反而会分叉。
    /// 代价是同一战备重复呼叫时图案一样 —— 观感上可接受，等你要求"每次不同"再改成带序号（序号要随呼叫同步）。</para>
    /// <para>无权威种子（单机 / 旧版房主）⇒ 返回 0 ⇒ 武器保持原来的全局随机行为。</para>
    /// </summary>
    private static int AirdropWeaponSeed(int airdropId)
    {
        int seed = FPSGame.Data.TaskState.Seed;
        if (seed == 0) return 0;
        return FPSGame.Utils.SeedUtil.Derive(FPSGame.Utils.SeedUtil.Derive(seed, FPSGame.Utils.SeedStream.Weapon), airdropId);
    }
    void UpdateBomb()
    {
        if (!m_creatObject)
        {
            if (data.State == AirdropState.Sustain)
            {
                m_creatObject = Instantiate(data.cfg.creatObect,transform.position, transform.rotation).transform;
                if (m_creatObject.TryGetComponentInChildren(out WeaponBaseController weapon))
                {
                    weapon.Owner = m_owner;
                    weapon.RandomSeed = AirdropWeaponSeed(data.cfg.ID);   // ★ 两端同一片火海（见方法注释）
                }
                if (data.cfg.sustainHideBeacon)
                {
                    transform.ForEach(item => item.gameObject.SetActive(false));
                }

                StampCaller(m_creatObject);   // ★ 归属：这次呼叫者
            }
        }
    }
    void EndBomb()
    {
        Tool.Destroy(m_creatObject.gameObject);
        transform.ForEach(item => item.gameObject.SetActive(true));

    }

    #endregion

    #region 飞鹰区

    void StartJet()
    {
        var size = data.cfg.showRange;
        Quaternion rotation= transform.rotation;
        if (size.y > 0&& size.x > size.y)//横向
        {
            rotation*=Quaternion.Euler(0, 90, 0);
        }
        //Debug.LogError("创建位置"+ transform.position);
        var go= VFXManager.Creat(eagle, transform.position, rotation, null).transform;
        m_creatObject = Instantiate(data.cfg.creatObect, go.TransformPoint(0,-5,-2), rotation,go).transform;
        if (m_creatObject.TryGetComponentInChildren(out WeaponBaseController weapon))
        {
            weapon.Owner = m_owner;
            weapon.RandomSeed = AirdropWeaponSeed(data.cfg.ID);
        }
        StampCaller(go, m_creatObject);   // ★ 归属：这次呼叫者（运输机 go 本体也带 BattleApplyVehicleData）

        //重新设置引导物体的位置
        foreach (var item in m_creatObject.GetComponentsInChildren<GuidedShelling>())
        {
            item.transform.position = transform.position;
        }
        
        if (data.cfg.sustainHideBeacon)
        {
            transform.ForEach(item => item.gameObject.SetActive(false));
        }
        
        
    }
    void UpdateJet()
    {

    }
    void EndJet()
    {
        Tool.Destroy(m_creatObject.gameObject,2);
        transform.ForEach(item => item.gameObject.SetActive(true));
    }

    #endregion

    #region 运输机系

    void StartMedivac()
    {
        var size = data.cfg.showRange;
        Quaternion rotation = transform.rotation;
        var go = VFXManager.Creat(neoNimbusVehicle, transform.position, rotation, null).transform;
        //TODO:单位暂时还不能回收
        if (data.cfg.creatObect.GetComponent<IActor>().IsValidMono())
        {
            var comp = data.cfg.creatObect.GetComponent<CharacterController>();
            m_creatObject = Instantiate(data.cfg.creatObect, go.TransformPoint(0, -2.5f + comp.center.y - comp.height, 1.5f), rotation, go).transform;
            m_creatObject.GetComponent<CharacterController>().enabled = false;
            if (m_creatObject.TryGetComponent(out BaseSelfController bsc)) bsc.enabled = false;

        }
        else//TODO:小型道具比如复活可以回收
        {
            var comp = data.cfg.creatObect.GetComponent<CharacterController>();
            m_creatObject = VFXManager.Creat(data.cfg.creatObect, go.TransformPoint(0, -2.5f + comp.center.y - comp.height, 1.5f), rotation, go).transform;
            m_creatObject.GetComponent<CharacterController>().enabled = false;
        }

        StampCaller(go, m_creatObject);   // ★ 归属：这次呼叫者（NeoNimbus_Vehicle 本体带 BattleApplyVehicleData）

        // 根据 arriveTime 调整运输机俯冲时长：俯冲阶段 = arriveTime - 2 秒
        const float diveArriveOffset = 2f;
        float targetDiveDuration = data.arriveTime - diveArriveOffset;
        if (go.TryGetComponent(out PhoenixEagleController eagleController))
        {
            eagleController.SetDiveDuration(targetDiveDuration);
        }

        if (m_creatObject.TryGetComponentInChildren(out WeaponBaseController weapon))
        {
            weapon.Owner = m_owner;
            weapon.RandomSeed = AirdropWeaponSeed(data.cfg.ID);
        }

        if (data.cfg.sustainHideBeacon)
        {
            transform.ForEach(item => item.gameObject.SetActive(false));
        }


    }

    void UpdateMedivac()
    {
        // 俯冲 = arriveTime - 2 秒，到达后停滞 2 秒，总计 arriveTime 秒后进入 Sustain 状态，此时卸载
        if (data.State == AirdropState.Sustain && m_creatObject.IsValid() && m_creatObject.transform.parent != null)
        {
            Debug.Log("卸载");
            m_creatObject.transform.parent = null;
            m_creatObject.GetComponent<CharacterController>().enabled = true;
            if (m_creatObject.TryGetComponent(out BaseSelfController bsc)) bsc.enabled = true;
        }
    }
    void EndMedivac()
    {
        transform.ForEach(item => item.gameObject.SetActive(true));
    }

    #endregion

    void SetDisplay()
    {
        AudioSvc.PlaySound(new ("AirDrop/superbeacon_impact",transform.position,60, AudioGroups.Weapon,0.5f));
        m_Lift = GetComponent<LimitedLife>();
        //Debug.LogError("持续时间"+ (data.cfg.arriveTime + data.cfg.sustainTime));
        m_Lift.SetLift(data.arriveTime + data.cfg.sustainTime);
        //var main = m_particle.main;
        //main.startLifetime = new(data.cfg.arriveTime + data.cfg.sustainTime);
        //main.duration = data.cfg.arriveTime + data.cfg.sustainTime;

        Color color = Color.LerpUnclamped(Color.white * data.cfg.Color.GetValue(), data.cfg.Color,1.7f);
        for (int i = 1; i < 5; ++i)
        {
            transform.GetChild(i).gameObject.SetActive(data.arriveTime>0);
        }
        transform.ForEach(item => SetColor(item, color));
        light.color = color;

        var size = data.cfg.showRange;
        //Debug.LogError("空袭的显示范??+size);
        if (size.x > 0 && size.y > 0)
        {//矩形
            if (size.y > size.x)
            {
                effectRangeCube.localEulerAngles = new(90, 0, 90);
                float tmp = size.y;
                size.y = size.x;
                size.x = tmp;
                effectRangeCube.localPosition = new(0, 0, size.x * 0.9f);
            }
            else
            {
                effectRangeCube.localEulerAngles = new(-90, 0, 0);
                effectRangeCube.localPosition = new(0, 0, 0);
            }
            effectRangeCube.gameObject.SetActive(true);
            effectRangeCircle.gameObject.SetActive(false);
            effectRangeCube.localScale = new(2 * size.x, 2 * size.y, size.x+size.y);
            //ebug.LogError("尺寸数据"+size+"实际数据"+ effectRangeCube.localScale);
        }
        else if (size.x > 0)
        {//圆形
            effectRangeCube.gameObject.SetActive(false);
            effectRangeCircle.gameObject.SetActive(true);
            effectRangeCircle.localScale = Vector3.one * size.x * 2;
        }
        else
        {
            effectRangeCube.gameObject.SetActive(false);
            effectRangeCircle.gameObject.SetActive(false);
        }
    }

    protected void TryWarning()
    {
        if (!data.cfg.useWarning||m_lastWarnTime + 10 > Time.time) return;
        bool meetWarn = InRange();
        if (meetWarn)
        {
            m_lastWarnTime = Time.time;
            WndManager.Instance.CreatNotice("Yuuka","Warning", InRange,vaildTime:5);
        }
    }
    private bool InRange()
    {
        if (GameRoot.GameState != GameStateEnum.Game||!ActorsManager.Player.IsValidMono()||!data.IsValid()) return false;
         Vector3 pos = ActorsManager.Player.transform.position;
        bool meetWarn = false;
        var size = data.cfg.showRange;
        if (size.x > 0 && size.y > 0)
        {
            Vector3 relativePos = transform.InverseTransformPoint(pos);
            if (Mathf.Abs(relativePos.x) < size.x && Mathf.Abs(relativePos.z) < size.y)
            {
                meetWarn = true;
            }
        }
        else if (size.x > 0)
        {//圆形
            if (Vector3.Distance(pos, transform.position) < size.x)
            {
                meetWarn = true;
            }
        }
        return meetWarn;
    }


    /// <summary>设置信标组件的颜色</summary>
    protected void SetColor(Transform transform,Color color) {
        if (transform.TryGetComponent(out ParticleSystem ps)) {
            var main = ps.main;
            main.startColor = new ParticleSystem.MinMaxGradient(color);
        }
        else if (transform.TryGetComponent(out MeshRenderer mr)) {
            mr.SetColor(color);
        }
        else if (transform.TryGetComponent(out LineRenderer lr)) {
            Color.RGBToHSV(color,out float h, out float s, out float v);
            color = Color.HSVToRGB(h,1,1);
            lr.startColor = lr.endColor = color;
        }
    }
}
}
