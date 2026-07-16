using System.Collections.Generic;
using Core;
using FPSGame.Attribute;
using FPSGame.Furn;
using GameContract;

using Unity.FPS.Game;
using UnityEngine;
using UnityEngine.UI;
using Utils;
using static WndTools.WndRootTool;


public class SubtitleWnd : Window
{
    [Foldout("喊话", true)]

    [SerializeField]
    private SubtitleBase ShoutPrefab, MarkPrefab, SpecUnitPrefab, RolePrefab;
    [SerializeField]
    private SubtitleAirdrop AirdropPrefab;

    [Foldout("交互预制件", true)] 
    [SerializeField]
    private Image ConcernPrefab;//与家具交互显示的预制件

    [Foldout("获得物品提示", true)]
    [SerializeField]
    RectTransform ObjectIcon, ObjectName, ObjectCount, GainType;
    [SerializeField]
    CanvasGroup GainObjectTipRoot;

    List<SubtitleBase> Subtitles;
    List<Image> Concerns;
    AutoObjectPool<SubtitleAirdrop> AirDropSubtitlesPool;
    bool alwaysShow = true;
    Queue<GainObjectInfo> GainObjectQueen;
    GainObjectInfo nowGainObject;
    float GainObjectTime;

    public List<KVP<GameObject, Vector3>> show;



    protected override void FirstShowWnd()
    {
        GainObjectQueen = new();
        Subtitles = new();
        Concerns = new();
        for (int i = 0; i < 8; ++i)
        {
            Concerns.Add(Instantiate(ConcernPrefab, transform));
        }
        AirDropSubtitlesPool = new(AirdropPoolUpdate, AirdropPoolAdd, AirdropPoolEnqueue, 0);

        GainObjectTipRoot.alpha = 0;

    }


    protected override void ShowWnd()
    {

        BattleEventSub.OnAirdrop += OnAirdrop;
        GlobalEventSub.OnSettingCange += OnSettingCange;
        BattleEventSub.OnUnitDeath += OnActorDeath;
        GlobalEventSub.OnOOPartCollect += OOPartCollect;
        //GlobalEventManager.OnSceneChange += OnSceneChange;
        GainObjectTime = -5;
    }

    protected override void HideWnd()
    {
        BattleEventSub.OnAirdrop -= OnAirdrop;
        GlobalEventSub.OnSettingCange -= OnSettingCange;
        BattleEventSub.OnUnitDeath -= OnActorDeath;
        GlobalEventSub.OnOOPartCollect -= OOPartCollect;
        //GlobalEventManager.OnSceneChange -= OnSceneChange;
        OnSceneChange(null);
    }


    private void Update()
    {
        if ((GameState== GameStateEnum.Game|| GameState == GameStateEnum.Bridge) && ActorsManager.OnActorCreat.Count > 0) OnActorCreat(ActorsManager.OnActorCreat.Dequeue());
        //if (ActorsManager.Player != null && ActorsManager.OnActorCreat.Count > 0) OnActorCreat(ActorsManager.OnActorCreat.Dequeue());
        var camera = Camera.main;
        var pos = camera.transform.position;
        var forward = camera.transform.forward;
        int useIndex = 0;
        show.Clear();

        if (ActorsManager.Player == null) return;
        foreach (var item in Furniture_Attached.list.Values)
        {
            if (!item.CanOperate(ActorsManager.Player.gameObject) || item.HaveFlag(FurnitureFlag.AutoOperate)) continue;
            float dis = Vector3.Distance(item.CenterPos, pos);
            if (dis < 20 && item != wndManager.operationWnd.furn)
            {
                float angle = Vector3.Angle(forward, item.Forward);
                var viewPos = camera.WorldToViewportPoint(item.CenterPos);
                

                if ((angle > 120 || item.HaveFlag(FurnitureFlag.AnyAngle)) && Tool.In2D(viewPos, Vector3.zero, Vector3.one) && viewPos.z > 0)
                {
                    if (useIndex < 8)
                    {
                        SetActive(Concerns[useIndex], true);
                        Concerns[useIndex].transform.position = camera.WorldToScreenPoint(item.CenterPos);
                        Concerns[useIndex].color = new Color(1, 1, 1, (1.5f - dis / 6));
                        ++useIndex;
                        show.Add(new(item.gameObject, viewPos));
                    }
                }
            }
        }
        for (; useIndex < 8; ++useIndex)
        {
            SetActive(Concerns[useIndex], false);
        }

        AirDropSubtitlesPool.Update();

        if (InputManager.GetDown(InputState.Crouch) && !alwaysShow)
        {
            foreach (var item in Subtitles)
            {
                item.TryActive(true);
            }
        }
        else if (InputManager.GetUp(InputState.Crouch) && !alwaysShow)
        {
            foreach (var item in Subtitles)
            {
                item.TryActive(false);
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
            foreach (var item in Subtitles)
            {
                item.SetShow(alwaysShow);
            }
        }
    }

    private void OnActorCreat(KVP<UnitTypeEnum, I_Actor> item)
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
                Subtitles.Add(Instantiate(RolePrefab).Creat(item.Value, item.Value.gameObject, transform, alwaysShow));
                break;
            case UnitTypeEnum.SpecUnit:
                //Debug.LogError($"创建特殊单位玩家{ActorsManager.Player}，目标{item.Value}");
                //Debug.LogError($"目标的单位{item.Value.gameObject}");
                Subtitles.Add(Instantiate(SpecUnitPrefab).Creat(ActorsManager.Player, item.Value.gameObject, transform, alwaysShow));
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

    private void OnAirdrop(GameObject owner, GameObject target, Vector3 point, AirdropController.AirdropData data) {
        var subtitle = AirDropSubtitlesPool.Get();
        subtitle.OnAirdrop(owner, target, point);
    }

    /// <summary>
    /// 切换场景时直接移除旧??
    /// </summary>
    /// <param name="_"></param>
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
