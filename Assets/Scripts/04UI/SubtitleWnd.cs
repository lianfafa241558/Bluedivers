using System.Collections.Generic;
using Core;
using GameContract;
using Unity.BaseTool;
using Unity.FPS.Game;
using UnityEngine;
using UnityEngine.UI;
using Utils;
using static WndTools.WndRootTool;


public class SubtitleWnd : WindowRoot
{
    [Foldout("喊话", true)]

    [SerializeField]
    private SubtitleBase ShoutPrefab, MarkPrefab, SpecUnitPrefab, RolePrefab;
    [SerializeField]
    private SubtitleAirdrop AirdropPrefab;

    [Foldout("交互点", true)]
    [SerializeField]
    private Image ConcernPrefab;//与家具交互显示的点

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



    public override void Init()
    {
    }
    public override void UnInit()
    {
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
        AirDropSubtitlesPool = new(AirdropPoolUpdate, AirdropPoolAdd, AirdropPoolEnqueue, 0);

        GlobalEventManager.OnAirdrop += OnAirdrop;
        GlobalEventManager.OnSettingCange += OnSettingCange;
        GlobalEventManager.OnUnitDeath += OnActorDeath;
        GlobalEventManager.OnOOPartCollect += OOPartCollect;
    }
    private void OnDestroy()
    {
        GlobalEventManager.OnAirdrop -= OnAirdrop;
        GlobalEventManager.OnSettingCange -= OnSettingCange;
        GlobalEventManager.OnUnitDeath -= OnActorDeath;
        GlobalEventManager.OnOOPartCollect -= OOPartCollect;
    }

    protected override void ShowWnd()
    {
        GainObjectTime = -5;
    }

    protected override void HideWnd()
    {

    }


    private void Update()
    {
        if (ActorsManager.Player != null && ActorsManager.OnActorCreat.Count > 0) OnActorCreat(ActorsManager.OnActorCreat.Dequeue());
        var camera = Camera.main;
        var pos = camera.transform.position;
        var forward = camera.transform.forward;
        int useIndex = 0;
        show.Clear();
        foreach (var item in Furniture_Base.list.Values)
        {
            if (!item.canOperate || item.HaveFlag(FurnitureFlag.AutoOperate)) continue;
            float dis = Vector3.Distance(item.CenterPos, pos);
            if (dis < 20 && item != wndManager.operationWnd.furn)
            {
                float angle = Vector3.Angle(forward, item.Forward);
                var viewPos = camera.WorldToViewportPoint(item.CenterPos);
                show.Add(new(item.gameObject, viewPos));

                if (((angle > 120 && viewPos.z > 0) || item.HaveFlag(FurnitureFlag.AnyAngle)) && Tool.In2D(viewPos, Vector3.zero, Vector3.one))
                {
                    if (useIndex < 8)
                    {
                        SetActive(Concerns[useIndex], true);
                        Concerns[useIndex].transform.position = camera.WorldToScreenPoint(item.CenterPos);
                        Concerns[useIndex].color = new Color(1, 1, 1, (1.5f - dis / 6));
                        ++useIndex;
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
        switch (item.Key)
        {
            case UnitTypeEnum.Player:
                Subtitles.Add(Instantiate(ShoutPrefab).Creat(item.Value, item.Value.gameObject, transform, alwaysShow));
                Subtitles.Add(Instantiate(MarkPrefab).Creat(item.Value, null, transform, alwaysShow));

                break;
            case UnitTypeEnum.Friend:
                Subtitles.Add(Instantiate(MarkPrefab).Creat(item.Value, null, transform, alwaysShow));
                Subtitles.Add(Instantiate(RolePrefab).Creat(item.Value, item.Value.gameObject, transform, alwaysShow));
                break;
            case UnitTypeEnum.SpecUnit:
                Subtitles.Add(Instantiate(SpecUnitPrefab).Creat(ActorsManager.Player, item.Value.gameObject, transform, alwaysShow));
                break;
        }

    }

    private void OnActorDeath(Actor actor)
    {
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
        //Debug.LogError("采集样本 来源"+user+"玩家"+ ActorsManager.Player.gameObject);
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
        SetText(GainType, info.count >= 0 ? "已获取" : "已掉落");
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
