using System.Collections.Generic;
using FPSGame.Core;
using FPSGame.Core.Interface;
using FPSGame.Attributes;
using FPSGame.GameContract;

using FPSGame.Game;
using UnityEngine;
using UnityEngine.UI;
using FPSGame.Utils;
using FPSGame.Gameplay;

namespace FPSGame.UI
{
using static FPSGame.WndTools.WndRootTool;
using FPSGame.Managers;
using FPSGame.GameData;


/// <summary>
/// 字幕、标记与交互提示窗口。
/// </summary>
[AddComponentMenu("UI/窗口/字幕")]
public class SubtitleWnd : Window
{
    [Foldout("喊话", true)]

    [SerializeField]
    private SubtitleBase ShoutPrefab, MarkPrefab, SpecUnitPrefab, RolePrefab,NPCPrefab;
    [SerializeField]
    private SubtitleAirdrop AirdropPrefab;

    [Foldout("交互预制件", true)] 
    [SerializeField]
    private Image ConcernPrefab;//与家具交互显示的预制件
    [SerializeField]
    private Transform ThirdPersonConcernPrefab;//第三人称交互提示预制件

    [Foldout("获得物品提示", true)]
    [SerializeField]
    RectTransform ObjectIcon, ObjectName, ObjectCount, GainType;
    [SerializeField]
    CanvasGroup GainObjectTipRoot;

    List<SubtitleBase> Subtitles;
    List<Image> Concerns;
    private Transform m_ThirdPersonConcernInstance;
    private bool m_IsThirdPerson;
    AutoObjectPool<SubtitleAirdrop> AirDropSubtitlesPool;
    bool alwaysShow = true;
    Queue<GainObjectInfo> GainObjectQueen;
    GainObjectInfo nowGainObject;
    float GainObjectTime;

    public List<KVP<GameObject, Vector3>> show;

    //检视器调用
    public void Init()
    {
        OnViewSwitch(ArchivesData_SO.Current.GetSetting("默认操作视角")>0);
        GlobalEventBus.OnViewSwitch += OnViewSwitch;
        GlobalEventBus.OnSceneChange += OnSceneChange;
    }

    protected override void FirstShowWnd()
    {
        GainObjectQueen = new();
        Subtitles = new();
        Concerns = new();
        for (int i = 0; i < 8; ++i)
        {
            Concerns.Add(Instantiate(ConcernPrefab, transform));
        }
        if (ThirdPersonConcernPrefab)
        {
            m_ThirdPersonConcernInstance = Instantiate(ThirdPersonConcernPrefab, transform);
            m_ThirdPersonConcernInstance.gameObject.SetActive(false);
        }
        AirDropSubtitlesPool = new(AirdropPoolUpdate, AirdropPoolAdd, AirdropPoolEnqueue, 0);

        GainObjectTipRoot.alpha = 0;

    }


    protected override void ShowWnd()
    {
        BattleEventBus.OnAirdrop += OnAirdrop;
        GlobalEventBus.OnSettingCange += OnSettingCange;
        UnitEventBus.OnUnitDeath += OnActorDeath;
        UnitEventBus.OnFriendLeave += OnFriendLeave;   // 盟友离场：清掉"以他为标记目标"的字幕（对象被销毁，不清就会每帧在死引用上取位置）
        GlobalEventBus.OnOOPartCollect += OOPartCollect;

        
        GainObjectTime = -5;
    }

    protected override void HideWnd()
    {
        BattleEventBus.OnAirdrop -= OnAirdrop;
        GlobalEventBus.OnSettingCange -= OnSettingCange;
        UnitEventBus.OnUnitDeath -= OnActorDeath;
        UnitEventBus.OnFriendLeave -= OnFriendLeave;
        GlobalEventBus.OnOOPartCollect -= OOPartCollect;
        //OnSceneChange(null);
    }

    public override void OnDestroy()
    {
        GlobalEventBus.OnSceneChange -= OnSceneChange;
        GlobalEventBus.OnViewSwitch -= OnViewSwitch;
        base.OnDestroy();
    }

    private void OnViewSwitch(bool isThirdPerson)
    {
        m_IsThirdPerson = isThirdPerson;
    }


    private void Update()
    {
        if ((GameState== GameStateEnum.Game|| GameState == GameStateEnum.Bridge) && ActorsManager.OnActorCreat.Count > 0) OnActorCreat(ActorsManager.OnActorCreat.Dequeue());
        //if (ActorsManager.Player != null && ActorsManager.OnActorCreat.Count > 0) OnActorCreat(ActorsManager.OnActorCreat.Dequeue());
        // ⚠ 不用 Camera.main：它按 tag 挑"第一台启用的相机"，主相机被关掉时会指向俯视/自由相机，
        //   ⇒ 交互提示的位置与朝向判断都会用错的参照点。只认玩家真正出图的那台。
        var camera = GameplayCamera.Current;
        if (camera == null) return;
        var pos = camera.transform.position;
        var forward = camera.transform.forward;
        int useIndex = 0;
        show.Clear();

        // ⚠ Player 是 IActor（接口引用）：`Player == null` 判不出"已销毁"。
        //   舰船→新手教程这类单场景加载会先销毁玩家、后注册新玩家，中间这几帧若放行就是
        //   MissingReferenceException（读已销毁 Actor 的 transform）。
        if (!ActorsManager.Player.IsValidMono()) return;
        var player = ActorsManager.Player;

        // 第三人称时提前获取最近目标，避免 ConcernPrefab 与 ThirdPersonConcernPrefab 重叠
        var playerOp = m_IsThirdPerson ? player.transform.GetComponent<PlayerOperationController>() : null;
        IFurniture thirdPersonTarget = playerOp ? playerOp.target : null;

        // 显示交互提示（第一/第三人称共用 Concerns）
        {
            foreach (var item in Furniture_Attached.list.Values)
            {
                // 便宜的判断放前面：CanOperate/HaveFlag 只读 bool，CenterPos 要调 Collider.bounds（原生调用）
                if (!item.CanOperate(player.gameObject) || item.HaveFlag(FurnitureFlag.AutoOperate)) continue;
                if (item == WndHub.Operation.furn || item.InOperate || item == thirdPersonTarget) continue;

                // CenterPos 每项只取一次（原来在距离/Angle/投影里取了 3 次），
                // 它内部会读 _collider.bounds，是这条循环里最贵的一次调用。
                // 已销毁的对象它返回 zero（自带保护），这里据此跳过。
                Vector3 center = item.CenterPos;
                if (center == Vector3.zero) continue;

                Vector3 delta = center - pos;
                float sqr = delta.x * delta.x + delta.y * delta.y + delta.z * delta.z;
                if (sqr >= 400f) continue;              // 20m（先用平方距离，省一次 sqrt）
                float dis = Mathf.Sqrt(sqr);

                // 角度判定放到相机投影之前：不满足的一个矩阵变换都不做
                if (Vector3.Angle(forward, item.Forward) <= 120f && !item.HaveFlag(FurnitureFlag.AnyAngle)) continue;

                var viewPos = camera.WorldToViewportPoint(center);
                if (viewPos.z <= 0f || !Tool.In2D(viewPos, Vector3.zero, Vector3.one)) continue;

                // 8 个槽位满了就不必再扫剩余家具（原实现是继续遍历但什么都不做）
                if (useIndex >= 8) break;

                SetActive(Concerns[useIndex], true);
                Concerns[useIndex].transform.position = camera.WorldToScreenPoint(center);
                Concerns[useIndex].color = new Color(1, 1, 1, (1.5f - dis / 6));
                ++useIndex;
                show.Add(new(item.gameObject, viewPos));
            }
            for (; useIndex < 8; ++useIndex)
            {
                SetActive(Concerns[useIndex], false);
            }
        }

        if (m_IsThirdPerson)
        {
            if (thirdPersonTarget != null && m_ThirdPersonConcernInstance)
            {
                m_ThirdPersonConcernInstance.gameObject.SetActive(true);
                var screenPos = camera.WorldToScreenPoint(thirdPersonTarget.CenterPos);
                screenPos.x = Mathf.Round(screenPos.x);
                screenPos.y = Mathf.Round(screenPos.y);
                m_ThirdPersonConcernInstance.position = screenPos;

                var descText = m_ThirdPersonConcernInstance.GetChild(0);
                SetText(descText, thirdPersonTarget.Desc);
                SetActive(descText.parent, !string.IsNullOrEmpty(thirdPersonTarget.Desc));

                var barRoot = m_ThirdPersonConcernInstance.GetChild(1);
                var bar = barRoot.GetChild(0);
                if (thirdPersonTarget.MeetTime > 0 && playerOp && playerOp.target == thirdPersonTarget)
                {
                    SetActive(barRoot, true);
                    SetFill(bar, thirdPersonTarget.Press / thirdPersonTarget.MeetTime);
                }
                else
                {
                    SetActive(barRoot, false);
                }

                var typeText = m_ThirdPersonConcernInstance.GetChild(2).GetChild(0);
                SetText(typeText, thirdPersonTarget.MeetTime > 0 ? "长按" : "按");
            }
            else if (m_ThirdPersonConcernInstance)
            {
                m_ThirdPersonConcernInstance.gameObject.SetActive(false);
            }
        }
        else
        {
            if (m_ThirdPersonConcernInstance)
                m_ThirdPersonConcernInstance.gameObject.SetActive(false);
        }

        AirDropSubtitlesPool.Update();


        if (InputManager.GetDown(InputState.FlareGun) && !alwaysShow)
        {
            for (int i = Subtitles.Count - 1; i >= 0; --i)
            {
                if (Subtitles[i] == null)
                {
                    Subtitles.RemoveAt(i);
                    continue;
                }
                Subtitles[i].TryActive(true);
            }
        }
        else if (InputManager.GetUp(InputState.FlareGun) && !alwaysShow)
        {
            for (int i = Subtitles.Count - 1; i >= 0; --i)
            {
                if (Subtitles[i] == null)
                {
                    Subtitles.RemoveAt(i);
                    continue;
                }
                Subtitles[i].TryActive(false);
            }
        }

        UpdateGainObject();
    }
    private void OnSettingCange(string key, float value)
    {
        if (key == "显示物体标记")
        {
            switch ((int)value)
            {
                case 0:
                    alwaysShow = true;
                    break;
                case 1:
                    alwaysShow = false;
                    break;
            }
            for (int i = Subtitles.Count - 1; i >= 0; --i)
            {
                if (Subtitles[i] == null)
                {
                    Subtitles.RemoveAt(i);
                    continue;
                }
                Subtitles[i].SetShow(alwaysShow);
            }
        }
    }

    private void OnActorCreat(KVP<UnitTypeEnum, IActor> item)
    {
        if (Subtitles==null)
        {
            FirstShowWnd();
        }
        switch (item.Key)
        {
            case UnitTypeEnum.Player:
                //Debug.Log("创建玩家框体"+ item.Value);
                Subtitles.Add(Instantiate(ShoutPrefab).Creat(item.Value, item.Value.gameObject, transform, alwaysShow));
                Subtitles.Add(Instantiate(MarkPrefab).Creat(item.Value, null, transform, alwaysShow));

                break;
            case UnitTypeEnum.Friend:
                Subtitles.Add(Instantiate(MarkPrefab).Creat(item.Value, null, transform, alwaysShow));
                // ⚠ owner 必须是**观察者（本机玩家）**，不能传盟友本人：RolePrefab 是 SubtitleSpecUnit，
                //   它在 Creat 里按 tarActor != owner 决定 SetActive、GetDistance 又拿 owner 当基准
                //   ⇒ 传自己等于 tarActor==owner：节点被关掉 + 距离恒≈0（alpha 恒 0），
                //   表现就是"远端喊话能听见语音、看不到字"（语音在事件回调里照播，与节点开关无关）。
                Subtitles.Add(Instantiate(RolePrefab).Creat(ActorsManager.Player, item.Value.gameObject, transform, alwaysShow));
                break;
            case UnitTypeEnum.SpecUnit:
                //Debug.LogError($"创建特殊单位玩家{ActorsManager.Player}，目标{item.Value}");
                //Debug.LogError($"目标的单位{item.Value.gameObject}");
                Subtitles.Add(Instantiate(GameRoot.GameState == GameStateEnum.Bridge? NPCPrefab: SpecUnitPrefab).Creat(ActorsManager.Player, item.Value.gameObject, transform, alwaysShow));
                break;
        }

    }

    private void OnActorDeath(Actor actor)
    {
        if (actor.Type == UnitTypeEnum.Player || actor.Type == UnitTypeEnum.Friend) return;

        for (int i = Subtitles.Count - 1; i >= 0; --i)
        {
            if (actor == (Actor)Subtitles[i].owner)
            {
                Tool.Destroy(Subtitles[i].gameObject);
                Subtitles.RemoveAt(i);
            }
        }
    }

    private void OOPartCollect(GameObject user, OOPartEnum type, int count)
    {
        //Debug.LogError("采集样本 来自 "+user+"玩家 "+ ActorsManager.Player.gameObject);
        if (user != null && user != ActorsManager.Player.gameObject) return;
        string name = propertyManager.GetName(type);
        Sprite icon = propertyManager.GetIcon(type);
        if (Time.time > GainObjectTime + 4)
        {
            DisplayGainObject(new(icon, name, count));
        }
        else if(GetText(ObjectName) == name)
        {
            DisplayGainObject(new(icon, name, count + nowGainObject.count));
        }
        else
        {
            GainObjectQueen.Enqueue(new(icon, name, count));
        }
    }

    void DisplayGainObject(GainObjectInfo info)
    {
        //nowGainObject = info;
        GainObjectTime = Time.time;
        SetText(ObjectName,info.name);
        SetSprite(ObjectIcon, info.icon);
        var size = info.icon.rect.size;
        var scale = size.x / size.y;
        ObjectIcon.sizeDelta = new(scale * ObjectIcon.sizeDelta.y, ObjectIcon.sizeDelta.y);
        SetText(ObjectCount, string.Format("<color=#{1}>{0}{2}</color>",
            info.count >= 0 ? "+" : "",
            ColorUtility.ToHtmlStringRGB(info.count >= 0 ? new(0.2f, 1, 0.2f) : new(1, 0.2f, 0.2f)),
        info.count));
        SetText(GainType, info.count >= 0 ? "已获得" : "");
        RefreshLayout(GainObjectTipRoot.transform);
    }
    void UpdateGainObject()
    {
        //可以换DOTween
        var time =Time.time - GainObjectTime;
        if (time<=5)
        {
            GainObjectTipRoot.alpha = Mathf.Clamp01(5 - time);
            if (GainObjectQueen.Count > 0 && time>3)
            {
                DisplayGainObject(GainObjectQueen.Dequeue());
            }
        }
        else if(GainObjectQueen.Count > 0)
        {
            DisplayGainObject(GainObjectQueen.Dequeue());
        }

    }


    private bool AirdropPoolUpdate(SubtitleAirdrop subtitle) {

        return true;
    }
    private SubtitleAirdrop AirdropPoolAdd() {
        var go = Instantiate(AirdropPrefab);
        go.gameObject.SetActive(false);
        go.Creat(ActorsManager.Player, null, transform, alwaysShow);
        return go;
    }
    private void AirdropPoolEnqueue(SubtitleAirdrop subtitle) {

    }

    private void OnAirdrop(GameObject owner, GameObject target, Vector3 point, AirdropData data) {
        var subtitle = AirDropSubtitlesPool.Get();
        subtitle.OnAirdrop(owner, target, point, data);
    }

    /// <summary>
    /// 切换场景时直接移除旧??
    /// </summary>
    /// <param name="_"></param>
    /// <summary>
    /// 盟友**离场**（对象被销毁）⇒ 清掉"以他为 target"的字幕。
    /// <para>⚠ 与 <see cref="OnActorDeath"/> 的口径不同：那个比的是 <c>owner</c>（宿主 = 本机玩家，他是被标记者时才清理），
    /// 而盟友在字幕里是 **target**（被别人标记）⇒ 必须比 <c>target</c>，否则这条永远匹配不上。</para>
    /// </summary>
    private void OnFriendLeave(Actor friend)
    {
        if (friend == null || Subtitles == null) return;

        var go = friend.gameObject;
        for (int i = Subtitles.Count - 1; i >= 0; --i)
        {
            var s = Subtitles[i];
            if (s == null) { Subtitles.RemoveAt(i); continue; }

            // ⚠ 两种口径都要认（见 SubtitleWnd 创建处的 owner/target 用法）：
            //   玩家/盟友自己的标记（SubtitleMark / SubtitleRole）里"他自己"是 **owner**；
            //   而 NPC 那条（SubtitleNPC）里 owner 是本机玩家、被标记者才是 **target**。
            if (s.target == go || ReferenceEquals(s.owner, friend))
            {
                Tool.Destroy(s.gameObject);
                Subtitles.RemoveAt(i);
            }
        }
    }

    public void OnSceneChange(string _)
    {
        //Debug.LogError("切换场景，清空组??+ Subtitles.Count);
        foreach (var item in Subtitles)
        {
            Tool.Destroy(item.gameObject);
        }
        Subtitles.Clear();
    }


    struct GainObjectInfo
    {
        public Sprite icon;
        public string name;
        public int count;

        public GainObjectInfo(Sprite icon, string name, int count) : this()
        {
            this.icon = icon;
            this.name = name;
            this.count = count;
        }
    }
        
}

}
