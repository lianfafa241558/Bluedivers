using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using FPSGame.Attributes;
using FPSGame.Core;
using FPSGame.Core.Interface;
using FPSGame.Mission;
using FPSGame.GameContract;
using PEMaths;
using FPSGame.Game;
using UnityEngine;
using UnityEngine.UI;
using FPSGame.Utils;
using FPSGame.Gameplay;

namespace FPSGame.UI
{
using static FPSGame.WndTools.WndRootTool;
using FPSGame.Managers;
//using Action = System.Action;

/// <summary>
/// 小地图，仅战斗阶段创建，结束销毁
/// </summary>
[AddComponentMenu("UI/窗口/小地图")]
public class MiniMapWnd : Window
{
    private float areaMultScale = 3;


    [SerializeField]
    Color ExtraColor, MainColor,EndColor,DisableColor;
    [SerializeField]
    Sprite ExtraIcon, MainIcon;

    [Space(16)]
    [SerializeField]
    GameObject EnemyPointPrefab;
    [SerializeField]
    GameObject PlayerPrefab, FriendPrefab,OtherPrefab,MissionPointPrefab, InterestPointPrefab;
    
    [Space(16)]
    [SerializeField]
    [InspectorName("显示铁轨路线")]
    bool DrawRailRoutes = true;
    [SerializeField]
    [InspectorName("铁轨路线颜色")]
    Color RailColor = new(1f, 1f, 1f, 0.5f);   // 白 50%（想要更低调就直接压成灰）
    [SerializeField]
    [InspectorName("铁轨路线宽度(像素)")]
    float RailWidth = 3f;

    [SerializeField]
    RectTransform PlayerRoot;
    [SerializeField]
    RectTransform EnemyRoot, OtherRoot, MissionPointRoot;

    [SerializeField]
    RawImage rawImage;//,gridImage;
    //Dictionary<RectTransform,Vector2> EnemyDic;
    List<(RectTransform Point, Vector2 Pos)> EnemyPoint;
    int EnemyCount;

    Dictionary<IEntity, RectTransform> ActorPoint;

    /// <summary>
    /// 火车任务点图标（按"在跑的列车"动态创建/回收）。
    /// <para>▍为什么不能走 <see cref="ActorPoint"/>：拦截火车任务**没有 MissionView（entity）**，
    /// <see cref="MissionPointCreat"/> 拿不到实体就直接 return 了；而列车是任务运行时自己 Instantiate 的，
    /// 唯一能问"车现在在哪"的地方是 <c>MissionInterceptTrain.GetLiveTrain()</c> ⇒ 单独一张表按帧跟位置。</para>
    /// </summary>
    readonly List<RectTransform> TrainPoints = new();

    /// <summary>铁轨路线线（首次显示时建，之后一直复用）</summary>
    MiniMapRailGraphic railGraphic;
    /// <summary>世界 XZ → 小地图锚点坐标（交给线渲染器做投影回调）</summary>
    Func<Vector2, Vector2> projectToMap;
    /// <summary>重建路线时复用的临时表（<c>SetRoutes</c> 会深拷贝，这里可以放心复用）</summary>
    readonly List<List<Vector2>> railRoutes = new();

    float time;
    [SerializeField]
    int mapSize;//地图尺寸(比实际小32！很重要)

    public Doublet<float> mapScale;
    public Doublet<Vector2> center;

    float TargetMapSize => mapSize * mapScale.Target;//目标地图展示的尺寸
    float NowMapSize=> mapSize* mapScale.Now;//当前地图展示的尺寸
    Vector2 zeroPoint=>center.Now - Vector2.one* NowMapSize/2;//当前的视野起点

    /// <summary>
    /// 当前缩放系数（**兜底成合法正数**）。
    /// <para>▍为什么要兜底（2026-10-10）：本类所有换算都是"除 NowMapSize / 除 mapScale.Now"。它们为 0 时
    /// 算出 Inf/NaN 写进 RectTransform（anchoredPosition/sizeDelta），Canvas 就会**每帧**刷
    /// "<c>Invalid AABB inAABB</c>"（Unity 原生日志、不带对象名，极难定位）⇒ 统一在这里夹住。</para>
    /// </summary>
    float Zoom
    {
        get
        {
            float v = mapScale != null && Tool.IsFinite(mapScale.Now) ? mapScale.Now : 1f;
            // 极小正数同样会把图标尺寸顶成天文数字（画面上就是一块糊满屏的图标）⇒ 与缩放上限 32 倍同量级地兜一下
            return v >= 0.01f ? v : 1f;
        }
    }

    /// <summary>视野尺寸是否可用于换算（false ⇒ 除零 ⇒ 结果必为 Inf/NaN）</summary>
    bool MapSizeValid => mapSize > 0 && Tool.IsFinite(NowMapSize) && NowMapSize > 0f;

    int UISize;//小地图ui的尺寸

    IActor player;
    float border;

    public void Init()
    {
        gameObject.SetActive(false);
        SetWndState(true);
        SetWndState(false);
    }


    protected override void FirstShowWnd()
    {
        ActorPoint = new();
        EnemyPoint = new();

        for (int i = 0; i < 20; ++i)
        {
            AddEnemyPoint();
        }
        var root = TaskManager.Instance.nowTask;
        mapSize = root.CameraSize;
        UISize = (int)rawImage.rectTransform.rect.width;
        border = root.MapBorder;

        mapScale = new(1, 8, 0.01f, Mathf.Lerp, Tool.Difference);
        center = new(Vector2.one * (TaskManager.Instance.nowTask.MapSize) / 2, 8, 0.1f, Vector2.Lerp, Vector2.Distance);
        //Debug.LogError("中心"+center.Target);

        // 铁轨路线：轨道在任务 InitMission 阶段就规划好了（铺轨协程第一个 yield 之前）⇒ 这里一定读得到
        // （真出问题时可以把这个开关关掉：整条功能不建对象、不进画线代码）
        if (DrawRailRoutes)
        {
            projectToMap = WorldPosToMapPos;
            CreateRailGraphic();
            RefreshRailRoutes();
        }

        InputManager.BindDown(WindowStateEnum.Game, InputState.MiniMap, SwitchWnd);

        //此时玩家已经诞生
        UnitEventBus.OnPlayerCreate += PlayerCreat;
        UnitEventBus.OnFriendCreate += FriendCreat;
        UnitEventBus.OnFriendLeave += FriendLeave;   // ★ 补上"离场"（2026-10-09）：原来只有创建 ⇒ 队友强退后小地图上的点会一直留着
        UnitEventBus.OnSpecUnitCreate += OtherCreat;
        UnitEventBus.OnSpecUnitDead += OtherDeath;
        BattleEventBus.OnMissionStart += MissionPointCreat;
        BattleEventBus.OnMissionEntityShow += OnMissionShow;
        BattleEventBus.OnMissionUpdate += OnMissionUpdate;
        BattleEventBus.OnMissionEnd += MissionPointEnd;

        SetActive(gameObject, false);

    }
    public override void OnDestroy()
    {
        base.OnDestroy();
        InputManager.UnBindDown(WindowStateEnum.Game, InputState.MiniMap, SwitchWnd);
        UnitEventBus.OnPlayerCreate -= PlayerCreat;
        UnitEventBus.OnFriendCreate -= FriendCreat;
        UnitEventBus.OnFriendLeave -= FriendLeave;
        UnitEventBus.OnSpecUnitCreate -= OtherCreat;
        UnitEventBus.OnSpecUnitDead -= OtherDeath;
        BattleEventBus.OnMissionStart -= MissionPointCreat;
        BattleEventBus.OnMissionEntityShow -= OnMissionShow;
        BattleEventBus.OnMissionUpdate -= OnMissionUpdate;
        BattleEventBus.OnMissionEnd -= MissionPointEnd;
        WndManager.OnWindowStateChange -= OnWindowStateChange;

        ActorPoint = null;
        EnemyPoint = null;
    }

    //考虑到如果使用事件方法，会出现每个单位都有一个图标的问题，这里使用传统方法
    //没有涉及逻辑，可以使用精度不高的写法
    protected override void ShowWnd()
    {
        //InputManager.Bind(WindowStateEnum.Game, InputState.Airdrop, GameSwitch);
        //InputManager.Bind(WindowStateEnum.Airdrop, InputState.Airdrop, CloseWnd);
        //Debug.LogError("注册事件");
        WndManager.OnWindowStateChange += OnWindowStateChange;
        UnitEventBus.OnPlayerDead += OnPlayerDown;

        // 每次打开都重投一次路线：隐藏期间轨道可能才铺完（条数会变），网格也可能没赶上上一帧的尺寸
        RefreshRailRoutesIfChanged();
        if (railGraphic != null) railGraphic.SetVerticesDirty();
    }

    protected override void HideWnd()
    {
        //InputManager.UnBind(WindowStateEnum.Game, InputState.Airdrop, GameSwitch);
        //InputManager.UnBind(WindowStateEnum.Airdrop, InputState.Airdrop, CloseWnd);
        //Debug.LogError("注销事件");
        WndManager.OnWindowStateChange -= OnWindowStateChange;
        UnitEventBus.OnPlayerDead -= OnPlayerDown;
    }



    private void Update()
    {
       
        if (/*player.IsValid()&&*/(!mapScale.Update()|!center.Update()))//不能使用短路或
        {
            if (mapSize > 0)
                rawImage.uvRect = new((zeroPoint - Vector2.one * border) / mapSize, Vector2.one * Zoom);

            foreach (var item in ActorPoint)
            {
                item.Value.anchoredPosition = WorldPosToMapPos(item.Key.Pos.ToVector2());
                if(item.Key is IMissionPoint mission&&mission.AreaRange>0)
                {
                    //*2.5是因为要稍微往外拓一点
                    int size = Mathf.CeilToInt(mission.AreaRange * areaMultScale / Zoom);
                    SetSizeDelta(item.Value.GetChild(0), size, size);
                    if (mission.HaveTag(MissionTag.FollowAreaScale))
                    {
                        var iconSize = Mathf.Max(mission.HalfRange / Zoom, 25);
                        SetSizeDelta(item.Value.GetChild(1, 1), iconSize, iconSize);
                    }
                }
            }
          
            for (int i=0; i < EnemyCount; ++i){
                EnemyPoint[i].Point.anchoredPosition = WorldPosToMapPos(EnemyPoint[i].Pos);
            }

            // 铁轨是折线网格：地图平移/缩放的每一帧都要重新投影（静止后这个分支不再进入，也就不会重画）
            if (railGraphic != null) railGraphic.SetVerticesDirty();
        }

        // 火车图标跟列车实际位置走（车在动 ⇒ 必须每帧，不能放进上面的"地图插值"分支）
        RefreshTrainPoints();



        //单位图标显隐跟随"组件是否启用"：组件 enabled 变化不会派发事件，只能每帧同步
        //（不能放在上面的地图插值分支里——该分支在地图缩放/居中到位后不再进入）
        if (ActorPoint != null)
        {
            foreach (var item in ActorPoint)
            {
                if (item.Key is not IActor actor) continue;
                bool visible = MiniMapVisible(actor);
                if (item.Value.gameObject.activeSelf != visible) SetActive(item.Value, visible);
            }
        }

        if ((time += Time.deltaTime) > 2)
        {
            time -= 2;
            RefreshEnemy();
            RefreshRailRoutesIfChanged();
        }
        var value = Input.GetAxis("Mouse ScrollWheel");
        if (value != 0)
        {
            if (value > 0)//放大
            {
                //Debug.LogError("放大");
                if (TargetMapSize>128)
                {
                    mapScale.Target /= 2;
                    //gridImage.uvRect = new(gridImage.uvRect.position, gridImage.uvRect.size/2);
                    //grid.pixelsPerUnitMultiplier /= 2;
                    SetCenter();
                }
            }
            else//缩小
            {
                //Debug.LogError("缩小");
                if (mapScale.Target < 1)
                {
                    mapScale.Target *= 2;
                    //gridImage.uvRect = new(gridImage.uvRect.position, gridImage.uvRect.size * 2);
                    //grid.pixelsPerUnitMultiplier *= 2;
                    SetCenter();

                }
            }

        }
    }

    //临时
    private void OnWindowStateChange(WindowStateEnum oldState, WindowStateEnum state)
    {
        if (state == WindowStateEnum.UI)
        {
            //Debug.LogError("切换显示状态"+ (!GetActive(this)));
            SetWndState(false);
        }
    }
    private void OnPlayerDown(IActor _)
    {
        SetWndState(false);
    }
    /// <summary>
    /// 直接用搜索做显示
    /// </summary>
    void RefreshEnemy()
    {
        HashSet<IActor> list=new();
        foreach (var item in ActorsManager.Players)
        {
            //先临时用50，之后再想办法；全队强化"无人机侦察助推器"可提高雷达射程
            float radarRange = 50 * (BattleManager.Instance.HaveBooster(BoosterType.Radar)?1.5f:1);
            list.UnionWith(BattleManager.Instance.FindUnits(new PECircle(item.LogicPos, (PEInt)radarRange), TargetCfg.Enemy, (actor) =>!actor.HasFlag(ActorFlag.MiniMapIgnore)));
        }
        int i = 0;
        EnemyCount = 0;
        foreach (var actor in list)
        {
            
            if (EnemyPoint.Count == i) AddEnemyPoint();
            var group = EnemyPoint[i];
            SetActive(group.Point, true);

            group.Item2 = actor.Pos.ToVector2();
            group.Point.anchoredPosition = WorldPosToMapPos(EnemyPoint[i].Item2);

            group.Point.sizeDelta = Vector2.one * actor.HalfRange * 10;
            EnemyPoint[i] = group;
            ++i;
        }
        EnemyCount = i;
        for (;i<EnemyPoint.Count;++i) SetActive(EnemyPoint[i].Point, false);
    }

    private RectTransform AddEnemyPoint()
    {
        var re=(RectTransform)Instantiate(EnemyPointPrefab, EnemyRoot).transform;
        EnemyPoint.Add((re,Vector2.zero));
        SetActive(re,false);
        return re;
    }

    #region 火车图标

    /// <summary>
    /// 每帧把"在跑的列车"映射成小地图图标：车没了就隐藏，多列同时在跑就多摆几个。
    /// <para>数据源是 <see cref="MissionInterceptTrain.GetLiveTrain"/>（车头 <see cref="TrainUnit.Position"/> = 当前世界 XZ）。</para>
    /// </summary>
    private void RefreshTrainPoints()
    {
        MissionInterceptTrain train = MissionInterceptTrain.Active;
        int count = train != null ? train.LiveTrainCount : 0;
        for (int i = 0; i < count; i++)
        {
            TrainUnit unit = train.GetLiveTrain(i);
            if (unit == null) continue;                 // 刚销毁/到站的那一帧
            while (TrainPoints.Count <= i) AddTrainPoint(train);
            RectTransform point = TrainPoints[i];
            point.anchoredPosition = WorldPosToMapPos(unit.Position);
            if (!point.gameObject.activeSelf) SetActive(point, true);
        }
        // 多出来的图标全部收掉（上一班车开走了）
        for (int i = count; i < TrainPoints.Count; i++)
        {
            if (TrainPoints[i].gameObject.activeSelf) SetActive(TrainPoints[i], false);
        }
    }

    /// <summary>造一个"任务点样式"的火车图标（复用 <see cref="MissionPointPrefab"/>：关掉区域圈与外框，只留图标体）</summary>
    private void AddTrainPoint(MissionInterceptTrain train)
    {
        RectTransform point = (RectTransform)Instantiate(MissionPointPrefab, MissionPointRoot).transform;
        point.gameObject.name = "Train_" + TrainPoints.Count;
        SetActive(point.GetChild(0), false);        // 0 = 区域圈（列车没有 AreaRange）
        SetActive(point.GetChild(1), true);         // 1 = 图标体
        SetActive(point.GetChild(1, 0), false);     // 1,0 = 外框（任务才有的装饰）
        // 图标为空时 Image 不生成几何（= 不可见）⇒ 这里留一条日志，别让人对着"没有图标"猜
        if (train.icon == null) Debug.LogWarning("拦截火车：任务没有配图标 ⇒ 小地图上的火车图标不会显示", train);
        SetSprite(point.GetChild(1, 1), train.icon);
        SetColor(point.GetChild(1, 1), MainColor);
        TrainPoints.Add(point);
    }

    #endregion

    #region 铁轨路线

    /// <summary>
    /// 运行时建一条"铁轨路线线"（完全由代码驱动，场景/预制体不用配任何东西）。
    /// <para>挂在 <see cref="MissionPointRoot"/> 下（同尺寸、同样铺满）：那里正好在底图之上、任务点图标之下；
    /// 小地图根节点上的 <c>Mask</c> 会顺带把出界的部分挡掉。</para>
    /// </summary>
    private void CreateRailGraphic()
    {
        if (railGraphic != null) return;

        // ⚠ 必须显式带 CanvasRenderer：`[RequireComponent]` 只在编辑器 Add Component 菜单生效，
        //   **运行时 AddComponent 不会自动补** ⇒ 少了它，Graphic 的网格提交不上去（Hierarchy 里就是个空物体）。
        GameObject go = new("RailRoutes", typeof(RectTransform), typeof(CanvasRenderer));
        go.layer = gameObject.layer;
        RectTransform rect = (RectTransform)go.transform;
        rect.SetParent(MissionPointRoot, false);
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.localScale = Vector3.one;
        rect.SetSiblingIndex(0);

        railGraphic = go.AddComponent<MiniMapRailGraphic>();
        railGraphic.raycastTarget = false;
        railGraphic.color = RailColor;
        railGraphic.Width = RailWidth;
    }

    /// <summary>
    /// 把当前这局的铁轨折线交给 <see cref="railGraphic"/>。
    /// <para>⚠ 轨道点阵的 XZ 就是世界坐标（<c>WSToTS</c> 只改高度），所以直接喂 <c>x/z</c> 即可。</para>
    /// <para>任务结束后 <see cref="MissionInterceptTrain.Active"/> 为空 ⇒ 什么都不做：已铺的轨道留作场景遗迹，
    /// 小地图上的线也跟着留着。</para>
    /// </summary>
    private void RefreshRailRoutes()
    {
        if (railGraphic == null) return;
        MissionInterceptTrain train = MissionInterceptTrain.Active;
        if (train == null) return;

        railRoutes.Clear();
        IReadOnlyList<RailTrack> tracks = train.Tracks;
        for (int i = 0; i < tracks.Count; i++)
        {
            List<Vector3> points = tracks[i].Points;
            if (points == null || points.Count < 2) continue;
            List<Vector2> route = new(points.Count);
            for (int j = 0; j < points.Count; j++) route.Add(new Vector2(points[j].x, points[j].z));
            railRoutes.Add(route);
        }
        railGraphic.SetRoutes(railRoutes, projectToMap);
    }

    /// <summary>轨道条数变了才重建（轨道是开局一次性规划完的，正常只重建一次）</summary>
    private void RefreshRailRoutesIfChanged()
    {
        if (railGraphic == null) return;
        MissionInterceptTrain train = MissionInterceptTrain.Active;
        if (train == null) return;                       // 任务已结束：保留现有路线
        if (train.Tracks.Count == railGraphic.RouteCount) return;
        RefreshRailRoutes();
    }

    #endregion

    private Vector2 WorldPosToMapPos(Vector2 vector) {
        //Debug.LogError("原位置"+vector);
        //var zeroPoint = new Vector2(Mathf.Clamp(targetCenter.x- targetNowMapSize,0,mapSize-targetNowMapSize), Mathf.Clamp(targetCenter.y - targetNowMapSize, 0, mapSize - targetNowMapSize));
        //Debug.LogError("零点" + targetZeroPoint);
        // ⚠ 兜底：divisor 为 0（初始化那一帧 / 地图尺寸没配）会算出 Inf/NaN，写进 anchoredPosition 就是
        //   Canvas "Invalid AABB inAABB" + 图标飞到天外 ⇒ 这种情况退回原点（0 = 小地图左上，至少不出错）。
        if (!MapSizeValid || UISize <= 0) return Vector2.zero;
        Vector2 re=(vector - zeroPoint);//计算出对于零点的偏移
        //Debug.LogError("偏移" + re+"映射系数"+(UISize/(float)targetNowMapSize));
        Vector2 mapped = re/ NowMapSize * UISize;//重映射为anchPos
        return Tool.IsFinite(mapped) ? mapped : Vector2.zero;
    }

    private Vector2 MapPosToWorldPos(Vector2 vector)
    {
        // ⚠ 兜底：UISize 为 0 ⇒ Inf 世界坐标（点一下地图就可能把玩家挪到无穷远）⇒ 退回视野原点
        if (!MapSizeValid || UISize <= 0) return zeroPoint;
        Vector2 mapped = vector / UISize* NowMapSize + zeroPoint;
        return Tool.IsFinite(mapped) ? mapped : zeroPoint;
    }

    private void OnDrawGizmos()
    {
        var _center = center.Now.ToVector3()+Vector3.up*20;

        Gizmos.color = Color.yellow;
        float range = NowMapSize/2;
        for (int i = 0; i < 36; ++i)
        {
            Gizmos.DrawLine(
                _center + new Vector3(Mathf.Sin(Mathf.PI / 18 * i) * range, 0, Mathf.Cos(Mathf.PI / 18 * i) * range),
                _center + new Vector3(Mathf.Sin(Mathf.PI / 18 * (i + 1)) * range, 0, Mathf.Cos(Mathf.PI / 18 * (i + 1)) * range)
            );
        }
        Gizmos.DrawLine(_center - new Vector3(range,0,0),_center + new Vector3(range, 0, 0));
        Gizmos.DrawLine(_center - new Vector3(0, 0, range), _center + new Vector3(0, 0, range));


    }

    /// <summary>
    /// 按M呼出/关闭界面
    /// </summary>
    void SwitchWnd()
    {
        SetWndState(!GetActive(this));
    }
    /// <summary>
    /// 通用的设置移动
    /// </summary>
    void OnGenericMove(IEntity entity)
    {
        if(entity.IsValidMono()) ActorPoint[entity].anchoredPosition = WorldPosToMapPos(entity.Pos.ToVector2());
    }
    /// <summary>
    /// 通用的设置旋转
    /// </summary>
    void OnGenericRotate(IEntity entity)
    {
        if (entity.IsValidMono()) ActorPoint[entity].GetChild(0).eulerAngles = new(0, 0, -entity.Angles.y);
    }

    #region 玩家控制
    void PlayerCreat(IActor actor)
    {
        player = actor;
        Debug.Log("设置玩家"+player);
        ActorPoint.Add(actor, Instantiate(PlayerPrefab, PlayerRoot).transform.GetRect());
        SetColor(ActorPoint[actor].GetChild(0), actor.Color);
        SetSprite(ActorPoint[actor].GetChild(1), actor.ExtraPortrait);
        actor.OnPosChange += OnPlayerMove;
        actor.OnAngleChange += OnGenericRotate;
        SetCenter();
    }

    void OnPlayerMove(IActor actor)
    {
        SetCenter();
    }
    #endregion

    #region 其他玩家控制
    void FriendCreat(IActor actor)
    {
        ActorPoint.Add(actor, Instantiate(FriendPrefab, PlayerRoot).transform.GetRect());
        SetColor(ActorPoint[actor].GetChild(0), actor.Color);
        SetSprite(ActorPoint[actor].GetChild(1),actor.ExtraPortrait);
        actor.OnPosChange += OnGenericMove;
        actor.OnAngleChange += OnGenericRotate;

        OnGenericRotate(actor);
        OnGenericMove(actor);
    }

    /// <summary>
    /// 盟友**离场**（实例被销毁：掉线 / 被清退 / 换场景）⇒ 把为他建的小地图点收掉，与 <see cref="FriendCreat"/> 成对。
    ///
    /// <para>▍为什么必须有（2026-10-09）：原来只订阅了创建 ⇒ 队友强退后那个图标会一直留在图上
    /// （越退越多，且 <c>Update</c> 还会继续读已销毁的 <c>actor.Pos</c>）。语义与"倒地"不同：
    /// 倒地走 <c>OnUnitDeath</c>（还能被救起，Should 留点），这里是真的不会回来了。</para>
    /// </summary>
    void FriendLeave(FPSGame.Game.Actor actor)
    {
        if (actor == null || ActorPoint == null) return;
        if (!ActorPoint.TryGetValue(actor, out var point)) return;

        ActorPoint.Remove(actor);
        actor.OnPosChange -= OnGenericMove;
        actor.OnAngleChange -= OnGenericRotate;
        if (point != null) Tool.Destroy(point.gameObject);
        FPSGame.Utils.NetSyncLog.SyncLog("盟友离场", $"小地图点已收掉 actor={actor.name}（剩余点位 {ActorPoint.Count}）");
    }


    #endregion

    #region 特殊单位控制

    void OtherCreat(IActor actor)
    {
        if (actor.HasFlag(ActorFlag.MiniMapIgnore)) return;
        ActorPoint.Add(actor, Instantiate(OtherPrefab, OtherRoot).transform.GetRect());
        SetSprite(ActorPoint[actor], actor.ExtraPortrait);
        actor.OnPosChange += OnGenericMove;
        OnGenericMove(actor);
        //新注册单位若其组件处于禁用态（含 prefab 上初始关闭），先不上图，等启用后由 Update 同步
        SetActive(ActorPoint[actor], MiniMapVisible(actor));
    }

    /// <summary>
    /// 单位图标是否应当显示：该单位 <see cref="MonoBehaviour.enabled"/> 为 false（含 prefab 上初始关闭）
    /// 或带 <see cref="ActorFlag.MiniMapIgnore"/> 标记时不显示。
    /// 组件 enabled 变化不会派发事件，故由 <see cref="Update"/> 每帧同步。
    /// </summary>
    private static bool MiniMapVisible(IActor actor)
    {
        if (actor is not MonoBehaviour mono) return false;
        if (!mono.enabled) return false;
        return !actor.HasFlag(ActorFlag.MiniMapIgnore);
    }

    void OtherDeath(IActor actor)
    {
        if (actor.HasFlag(ActorFlag.MiniMapIgnore)) return;
        
        if (ActorPoint.TryGetValue(actor,out var rect))
        {
            //姑且如此，以后再搞好
            Tool.Destroy(rect.gameObject);
            ActorPoint.Remove(actor);
            actor.OnPosChange -= OnGenericMove;
            actor.OnAngleChange -= OnGenericRotate;
        }
        
    }
    #endregion

    #region 任务/兴趣点控制

    void MissionPointCreat(MissionBase mission)
    {
        IMissionPoint entity = mission.entity;
        if (entity == null)
        {
            Debug.LogWarning("警告任务"+mission.name+"没有实体");
            return;
        }
        //Debug.LogError("任务" + mission.title+"实体"+ entity, entity.gameObject);
        if (!ActorPoint.TryGetValue(entity, out var go))
        {
           
            go = Instantiate(MissionPointPrefab, MissionPointRoot).transform.GetRect();

            SetActive(go.GetChild(1, 0), mission.HasTag(MissionTag.DisplayFrame));
            SetSprite(go.GetChild(1, 1), mission.icon);
            SetActive(go.GetChild(0), entity.AreaRange > 0);

            //*2.5是因为要稍微往外拓一点
            int size = Mathf.CeilToInt(entity.AreaRange * areaMultScale / Zoom);
            //Debug.LogError("任务" + mission.title + "区域"+size+"像素 区域范围"+ entity.AreaRange);
            SetSizeDelta(go.GetChild(0), size, size);
            if (mission.entity.HaveTag(MissionTag.FollowAreaScale))
            {
                var iconSize = Mathf.Max(size / areaMultScale, 25);
                SetSizeDelta(go.GetChild(1, 1), iconSize, iconSize);
            }
            else
            {
                var iconSize = go.GetChild(1, 1).GetRect().sizeDelta.x*entity.IconSizeScale;
                SetSizeDelta(go.GetChild(1, 1), iconSize, iconSize);
            }


            //等待显示状态变化
            SetActive(go.GetChild(1), false);
            go.gameObject.name = "Mission_" + mission.name;

            bool targetActive = mission.HasTag(MissionTag.IsActive);

            if (targetActive)
            {
                Color color = mission.missionType switch {
                    MissionType.Main => MainColor,
                    MissionType.Extra => ExtraColor,
                    MissionType.Nest => mission.color,
                    MissionType.Sub => MainColor,
                    _ => Color.red,
                };
                SetColor(go.GetChild(1, 1), color);
                SetColor(go.GetChild(1, 0), color);
            }
            else
            {
                SetColor(go.GetChild(1, 1), DisableColor);
                SetColor(go.GetChild(1, 0), DisableColor);
            }
            
            ActorPoint.Add(entity, go);
            OnGenericMove(entity);

        }
        else
        {
            Debug.LogError("警告任务" + mission.name + "重复注册？");
        }

    }
    void MissionPointEnd(MissionBase mission)
    {
        if (mission.entity == null) return;
        IMissionPoint entity = mission.entity;
        //Debug.LogError("任务完成" + mission, mission);
        if (ActorPoint.TryGetValue(entity,out var go)){
            SetColor(go.GetChild(1,0), EndColor);
            SetColor(go.GetChild(1,1), EndColor);
            if (mission.HasTag(MissionTag.CompleteHide))
            {
                SetActive(go.GetChild(1), false);
            }
            if (entity.AreaRange > 0)
            {
                var size=GetSizeDelta(go.GetChild(0));
                GameRoot.CreateTimer((count)=> {
                    entity.AreaRange = Mathf.Lerp(entity.HalfRange, 0, count / 40f);
                    if (mapScale.Comple)
                    {
                        //*2.5是因为要稍微往外拓一点
                        int size = Mathf.CeilToInt(entity.AreaRange * areaMultScale / Zoom);
                        SetSizeDelta(go.GetChild(0), size, size);
                    }
                },0.05f,40);
            }
        }

    }
    public void OnMissionShow(IEntity entity)
    {
        if (ActorPoint.TryGetValue(entity, out var go))//任务
        {
            //显示之后就不再隐藏
            SetActive(go.GetChild(1), true);
        }
        else//兴趣点
        {
            go = Instantiate(InterestPointPrefab, MissionPointRoot).transform.GetRect();
            go.gameObject.name = "Inter" + entity.gameObject.name;
            Debug.Log("添加"+ entity.Id+" "+entity.gameObject);
            ActorPoint.Add(entity, go);
            OnGenericMove(entity);
        }
    }
    public void OnMissionUpdate(MissionBase mission)
    {
        if (!mission.entity) return;
        if (ActorPoint.TryGetValue(mission.entity, out var go))
        {
            bool targetActive = mission.HasTag(MissionTag.IsActive);
            bool selfActive = GetColor(go.GetChild(1, 1)).GetGray() > DisableColor.GetGray();
            if (selfActive!= targetActive)
            {
                if (targetActive)
                {
                    Color color = mission.missionType switch {
                        MissionType.Main => MainColor,
                        MissionType.Extra => ExtraColor,
                        MissionType.Nest => mission.color,
                        _ => default,
                    };
                    SetColor(go.GetChild(1, 1), color);
                    SetColor(go.GetChild(1, 0), color);
                }
                else
                {
                    SetColor(go.GetChild(1, 1), DisableColor);
                    SetColor(go.GetChild(1, 0), DisableColor);
                }
            }
        }
    }

    #endregion

    private void SetCenter()
    {
        var pos = player.Pos;
        float radius = TargetMapSize / 2;
        float min = radius + border;
        float max = mapSize - radius + border;


        //Debug.LogError("半径"+radius+"坐标"+ pos+"限制"+ min+" , "+max);
        center.Target = new Vector2(Mathf.Clamp(pos.x, min, max), Mathf.Clamp(pos.z, min, max));

        //rawImage.uvRect = new((targetZeroPoint - Vector2.one * border) / mapSize, Vector2.one * mapScale);

        ActorPoint[player].anchoredPosition = WorldPosToMapPos(pos.ToVector2());
        //PlayerPoint[player].GetChild(0).eulerAngles = new(0, 0, -player.Angles.y);
    }

    [Serializable]
    /// <summary>
    /// 将now回正为target
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class Doublet<T>
    {
        public T Target;
        public T Now;
        float _updateSpeed;
        float _threshold;

        Func<T,T,float,T> UpdateMethod;
        Func<T,T, float> CompareMethod;
        public Doublet(T target, float updateSpeed, float threshold, Func<T, T, float, T> updateMethod, Func<T,T, float> compareMethod)
        {
            Now = Target = target;
            _updateSpeed = updateSpeed;
            _threshold = threshold;
            UpdateMethod = updateMethod;
            CompareMethod = compareMethod;

        }
        /// <summary>
        /// 更新
        /// </summary>
        /// <returns>是否已经回归</returns>
        public bool Update()
        {
            if (!Comple)
            {
                Now = UpdateMethod.Invoke(Now, Target, _updateSpeed * Time.deltaTime);
                if (CompareMethod.Invoke(Target,Now) < 0.01f)
                {
                    Now = Target;
                }
                return false;
            }
            return true;
        }
        public bool Comple => CompareMethod.Invoke(Target, Now) <= _threshold;
    }

}
}
