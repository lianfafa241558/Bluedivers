using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Core;
using Core.Interface;
using FpsGame.Mission;
using GameContract;
using PEMaths;
using Unity.FPS.Game;
using UnityEngine;
using UnityEngine.UI;
using Utils;
using static WndTools.WndRootTool;
//using Action = System.Action;

/// <summary>
/// 小地图，仅战斗阶段创建，结束销毁
/// </summary>
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
    RectTransform PlayerRoot;
    [SerializeField]
    RectTransform EnemyRoot, OtherRoot, MissionPointRoot;

    [SerializeField]
    RawImage rawImage;//,gridImage;
    //Dictionary<RectTransform,Vector2> EnemyDic;
    List<(RectTransform Point, Vector2 Pos)> EnemyPoint;
    int EnemyCount;

    Dictionary<I_Entity, RectTransform> ActorPoint;

    float time;
    [SerializeField]
    int mapSize;//地图尺寸(比实际小32！很重要)

    public Doublet<float> mapScale;
    public Doublet<Vector2> center;

    float TargetMapSize => mapSize * mapScale.Target;//目标地图展示的尺寸
    float NowMapSize=> mapSize* mapScale.Now;//当前地图展示的尺寸
    Vector2 zeroPoint=>center.Now - Vector2.one* NowMapSize/2;//当前的视野起点

    int UISize;//小地图ui的尺寸

    I_Actor player;
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

        InputManager.BindDown(WindowStateEnum.Game, InputState.MiniMap, SwitchWnd);

        //此时玩家已经诞生
        GlobalEventSub.OnPlayerCreate += PlayerCreat;
        GlobalEventSub.OnFriendCreate += FriendCreat;
        //应该还有盟友离开游戏?
        BattleEventSub.OnSpecUnitCreate += OtherCreat;
        BattleEventSub.OnSpecUnitDead += OtherDeath;
        BattleEventSub.OnMissionStart += MissionPointCreat;
        BattleEventSub.OnMissionEntityShow += OnMissionShow;
        BattleEventSub.OnMissionUpdate += OnMissionUpdate;
        BattleEventSub.OnMissionEnd += MissionPointEnd;

        SetActive(gameObject, false);

    }
    public override void OnDestroy()
    {
        base.OnDestroy();
        InputManager.UnBindDown(WindowStateEnum.Game, InputState.MiniMap, SwitchWnd);
        GlobalEventSub.OnPlayerCreate -= PlayerCreat;
        GlobalEventSub.OnFriendCreate -= FriendCreat;
        //应该还有盟友离开游戏?
        BattleEventSub.OnSpecUnitCreate -= OtherCreat;
        BattleEventSub.OnSpecUnitDead -= OtherDeath;
        BattleEventSub.OnMissionStart -= MissionPointCreat;
        BattleEventSub.OnMissionEntityShow -= OnMissionShow;
        BattleEventSub.OnMissionUpdate -= OnMissionUpdate;
        BattleEventSub.OnMissionEnd -= MissionPointEnd;
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
        BattleEventSub.OnPlayerDead += OnPlayerDown;
    }

    protected override void HideWnd()
    {
        //InputManager.UnBind(WindowStateEnum.Game, InputState.Airdrop, GameSwitch);
        //InputManager.UnBind(WindowStateEnum.Airdrop, InputState.Airdrop, CloseWnd);
        //Debug.LogError("注销事件");
        WndManager.OnWindowStateChange -= OnWindowStateChange;
        BattleEventSub.OnPlayerDead -= OnPlayerDown;
    }



    private void Update()
    {
       
        if (/*player.IsValid()&&*/(!mapScale.Update()|!center.Update()))//不能使用短路或
        {
            rawImage.uvRect = new((zeroPoint - Vector2.one * border) / mapSize, Vector2.one * mapScale.Now);

            foreach (var item in ActorPoint)
            {
                item.Value.anchoredPosition = WorldPosToMapPos(item.Key.Pos.ToVector2());
                if(item.Key is I_MissionPoint mission&&mission.AreaRange>0)
                {
                    //*2.5是因为要稍微往外拓一点
                    int size = Mathf.CeilToInt(mission.AreaRange * areaMultScale / mapScale.Now);
                    SetSizeDelta(item.Value.GetChild(0), size, size);
                    if (mission.HaveTag(MissionTag.FollowAreaScale))
                    {
                        var iconSize = Mathf.Max(mission.HalfRange / mapScale.Now, 25);
                        SetSizeDelta(item.Value.GetChild(1, 1), iconSize, iconSize);
                    }
                }
            }
          
            for (int i=0; i < EnemyCount; ++i){
                EnemyPoint[i].Point.anchoredPosition = WorldPosToMapPos(EnemyPoint[i].Pos);
            }
        }



        if ((time += Time.deltaTime) > 2)
        {
            time -= 2;
            RefreshEnemy();
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
    private void OnPlayerDown(I_Actor _)
    {
        SetWndState(false);
    }
    /// <summary>
    /// 直接用搜索做显示
    /// </summary>
    void RefreshEnemy()
    {
        HashSet<I_Actor> list=new();
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

    private Vector2 WorldPosToMapPos(Vector2 vector) {
        //Debug.LogError("原位置"+vector);
        //var zeroPoint = new Vector2(Mathf.Clamp(targetCenter.x- targetNowMapSize,0,mapSize-targetNowMapSize), Mathf.Clamp(targetCenter.y - targetNowMapSize, 0, mapSize - targetNowMapSize));
        //Debug.LogError("零点" + targetZeroPoint);
        Vector2 re=(vector - zeroPoint);//计算出对于零点的偏移
        //Debug.LogError("偏移" + re+"映射系数"+(UISize/(float)targetNowMapSize));
        return re/ NowMapSize * UISize;//重映射为anchPos
    }

    private Vector2 MapPosToWorldPos(Vector2 vector)
    {
        return vector / UISize* NowMapSize + zeroPoint;
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
    void OnGenericMove(I_Entity entity)
    {
        if(entity.IsValid()) ActorPoint[entity].anchoredPosition = WorldPosToMapPos(entity.Pos.ToVector2());
    }
    /// <summary>
    /// 通用的设置旋转
    /// </summary>
    void OnGenericRotate(I_Entity entity)
    {
        if (entity.IsValid()) ActorPoint[entity].GetChild(0).eulerAngles = new(0, 0, -entity.Angles.y);
    }

    #region 玩家控制
    void PlayerCreat(I_Actor actor)
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

    void OnPlayerMove(I_Actor actor)
    {
        SetCenter();
    }
    #endregion

    #region 其他玩家控制
    void FriendCreat(I_Actor actor)
    {
        ActorPoint.Add(actor, Instantiate(FriendPrefab, PlayerRoot).transform.GetRect());
        SetColor(ActorPoint[actor].GetChild(0), actor.Color);
        SetSprite(ActorPoint[actor].GetChild(1),actor.ExtraPortrait);
        actor.OnPosChange += OnGenericMove;
        actor.OnAngleChange += OnGenericRotate;

        OnGenericRotate(actor);
        OnGenericMove(actor);
    }


    #endregion

    #region 特殊单位控制

    void OtherCreat(I_Actor actor)
    {
        if (actor.HasFlag(ActorFlag.MiniMapIgnore)) return;
        ActorPoint.Add(actor, Instantiate(OtherPrefab, OtherRoot).transform.GetRect());
        SetSprite(ActorPoint[actor], actor.ExtraPortrait);
        actor.OnPosChange += OnGenericMove;
        OnGenericMove(actor);
    }

    void OtherDeath(I_Actor actor)
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
        I_MissionPoint entity = mission.entity;
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
            int size = Mathf.CeilToInt(entity.AreaRange * areaMultScale / mapScale.Now);
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
        I_MissionPoint entity = mission.entity;
        //Debug.LogError("任务完成" + mission, mission);
        if (ActorPoint.TryGetValue(entity,out var go)){
            SetColor(go.GetChild(1,0), EndColor);
            SetColor(go.GetChild(1,1), EndColor);
            if (mission.HasTag(MissionTag.CompleHide))
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
                        int size = Mathf.CeilToInt(entity.AreaRange * areaMultScale / mapScale.Now);
                        SetSizeDelta(go.GetChild(0), size, size);
                    }
                },0.05f,40);
            }
        }

    }
    public void OnMissionShow(I_Entity entity)
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
