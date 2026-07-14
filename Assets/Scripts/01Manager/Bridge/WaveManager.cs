using System.Collections.Generic;
using System.Linq;
using Core;
using Core.Interface;
using GameContract;

using Unity.FPS.AI;
using Unity.FPS.Game;
using UnityEngine;
using UnityEngine.AI;
using Utils;
using Random = System.Random;
using UnitWeightCfg = CampData_SO.UnitWeightCfg;

public class WaveManager : TickBehaviour
{
    [SerializeField]
    string Suffix;
    int WaveCool;




    Dictionary<UnitTier, List<KVP<int, UnitWeightCfg>> > TierItemWeight;
    [SerializeField]
    List<SKVP<int,UnitTier>> TierWeight;

    List<KVP<int,List<UnitTier>>> Patrol;
    //List<Wave> waveGroup;
    List<GameObject> WaveUseObject;

    float m_lastWaveTime=Mathf.NegativeInfinity;
    int waveValue;

    public int WaveCount => ticks.Count - 1;

    Random random;
    BattleManager manager;
    EnemyVarietyType enemyVarietyType;

    private void Awake()
    {
        manager = BattleManager.Instance;
        random = manager.BattleRandom;
        var task = TaskManager.Instance.nowTask;
        var cfg = task.campData;
        Debug.Log(cfg.ShowName+" "+cfg.Suffix+ task.campData.ShowName, task.campData);
        Suffix = cfg.Suffix;
        WaveCool = cfg.WaveCool;
        WaveUseObject = cfg.WaveUseObject;
        enemyVarietyType = cfg.enemyVarietyType;
        var tmp = cfg.templates.RandomTake();


        TierWeight = tmp.template.Select(item => new SKVP<int, UnitTier>(item.weight, item.tier)).Where(item=>item.Key>0).ToList();
        
        TierItemWeight = new Dictionary<UnitTier, List<KVP<int, UnitWeightCfg>>>();
       
        foreach (var kvp in tmp.template)
        {
            var list = kvp.unitWeights
                .Select(cfg => new KVP<int, UnitWeightCfg>(kvp.weight, cfg))
                .ToList();

            //Debug.LogError(string.Join(",",list.Select(item=>item.Value.unit.name).ToList()));

            if (TierItemWeight.ContainsKey(kvp.tier))
                TierItemWeight[kvp.tier] = list;
            else
                TierItemWeight.Add(kvp.tier, list);
        }
        
        //Debug.LogError(cfg.ShowName + "选择" + tmp.name + "模板");

        Patrol = tmp.patrolTemplate
            .Where(kvp => kvp.Value > 0)
            .Select(kvp =>
            {
                var patrolCfg = cfg.patrolCfgs.FirstOrDefault(p => p.name == kvp.Key);
                if (patrolCfg == null) return null;
                var units = patrolCfg.units
                    .SelectMany(item => Enumerable.Repeat(0, item.Value)
                        .Select(_ => (UnitTier)item.Key)
                    ).ToList();
                return new KVP<int, List<UnitTier>>(kvp.Value, units);
            })
            .Where(kvp => kvp != null)
            .ToList();


        waveValue = (int)((int)(task.difficulty) * 35 * (1 + 0.33f * task.ExtraDifficulty[2]));
    }

    public bool CreatWave(WaveCreateParams param)
    {
        //时间没到或者不是强制刷新
        if (!param.extraWave && Time.time < m_lastWaveTime + WaveCool) return false;
        m_lastWaveTime = Time.time;
        switch (enemyVarietyType.ToEnemyType())
        {
            case EnemyType.Kaiser:
                ticks.Add(new RobotWave(param, InitWaveUnits(param.scale), WaveUseObject));
                break;
            case EnemyType.Decagrammaton:
                ticks.Add(new ZergWave(param, InitWaveUnits(param.scale), WaveUseObject));
                break;
            case EnemyType.Colour:
                ticks.Add(new ZergWave(param, InitWaveUnits(param.scale), WaveUseObject));
                break;
        }

        return true;
    }


    protected override void Update()
    {
        base.Update();
        if(Input.GetKeyUp(KeyCode.K))
        {
            CreatWave(new() {
                center = ActorsManager.Player.Pos,
                extraWave = false,
                range = 30,
                scale = 1,
                tip = true
            });
        }
    }

    public override bool Tick()
    {
        return true;
    }

    Stack<GameObject> InitWaveUnits(float scale)
    {
        Stack<GameObject> re = new();
        var remain = waveValue* scale;
        
        while (remain > 0)
        {
            UnitTier tier = TierWeight.WeightTake(100, random);
            var item = TierItemWeight[tier].WeightTake(100, random);
            remain -= item.size;
            re.Push(item.unit);
        }
        return re;
    }

    public GameObject CreatUnit(UnitTier tier, Vector3 pos,float range, bool IsFixed = true)
    {
        //var random = BattleManager.Instance.BattleRandom;
        var item = TierItemWeight[tier].WeightTake(100, random);
        //先取到地点
        if (NavMesh.SamplePosition(pos, out var hit, 50, NavMesh.AllAreas))
        {
            pos = hit.position;
        }

        //再随机偏移
        if (NavMesh.SamplePosition(pos + random.RandomVector2().ToVector3() * range, out hit, 10, UnityEngine.AI.NavMesh.AllAreas))
        {
            pos = hit.position;
        }
        else
        {
            Debug.LogError("错误:创建单位的目标点"+ pos+"不存在");
        }

        var go = Object.Instantiate(item.unit, pos, Quaternion.Euler(random.RandomVector2().ToVector3()), manager.ACCont.transform);
        if (IsFixed)
        {
            go.GetComponent<I_AIController>().BirthDuration = 0;
            go.GetComponent<I_Actor>().IsFixed = true;
        }
        return go;
    }

    public List<GameObject> CreatPatrol(Vector3 pos)
    {
        var re = new List<GameObject>();
        var temp = Patrol.WeightTake(100,random);
        temp.ForEach(item=> re.Add(CreatUnit(item,pos,5,false)));
        return re;
    }


}

public enum WaveState{
    Start,//开始
    Ongoing,//进行中
    NearEnd,//即将结束
    End//结束
}
public class ZergWave :I_TickClass, System.IDisposable
{

    List<GameObject> waveUseObject;
    List<GameObject> creatObject;
    Stack<GameObject> creats;
    List<Actor> units;

    WaveState state;
    int perTickCreat;
    int time;
    Vector3[] points;
    Vector3 center;
    bool completeCreat;
    bool tip;

    bool IsDisposed;

    System.Random random;
    public ZergWave(WaveCreateParams param, Stack<GameObject> creats, List<GameObject> waveUseObject)
    {
        random = new Random(RandomUtils.Range(0,1000));
        this.waveUseObject = new(waveUseObject);
        this.creats = creats;
        this.tip = param.tip;

        creatObject = new();
        units = new();
        center = param.center;

        BattleEventSub.OnEnemyDead += OnUnitDeath;

        if (param.points == null)
        {
            perTickCreat = Mathf.Max(1, Mathf.CeilToInt(creats.Count / 45f));//保底1个
            points = new Vector3[perTickCreat];
            float theta = random.Range(0, 2 * Mathf.PI);
            for (int i = 0; i < perTickCreat; ++i)
            {
                var dx = random.Range(-1, 1f);//范围20度
                points[i] = center + new Vector3(Mathf.Cos(theta + dx), 0, Mathf.Sin(theta + dx)) * random.Range(param.range, param.range + 10);

                if (NavMesh.SamplePosition(points[i], out var hit, 100, NavMesh.AllAreas))
                {
                    points[i] = hit.position;
                }
                else
                {
                    points[i] = new Vector3(points[i].x, center.y, points[i].z);
                }
            }
        }
        else
        {
            perTickCreat = param.points.Length;
            points = new Vector3[perTickCreat];
            for (int i = 0; i < perTickCreat; ++i)
            {
                points[i] = param.points[i] + random.RandomVector2().ToVector3() * random.Range(0,param.range);

                if (NavMesh.SamplePosition(points[i], out var hit, 100,NavMesh.AllAreas))
                {
                    points[i] = hit.position;
                }
                else
                {
                    points[i] = new Vector3(points[i].x, center.y, points[i].z);
                }
            }
        }
        Trans(WaveState.Start);
    }
    public void Dispose()
    {
        if (IsDisposed) return;
        BattleEventSub.OnEnemyDead -= OnUnitDeath;

        units?.Clear();
        waveUseObject?.Clear();
        creatObject?.Clear();
        creats?.Clear();

        waveUseObject = null;
        creatObject = null;
        creats = null;
        units = null;
        points = null;
        random = null;

        IsDisposed = true;
    }

    public bool Tick()
    {

        --time;
        switch (state)
        {
            case WaveState.Start:
                if (time == -5)
                {
                    for (int i = 0; i < perTickCreat; ++i)
                    {
                        var go = VFXManager.Creat(waveUseObject[0], points[i]);
                        go.GetComponent<LimitedLife>().ResetLift(51);
                        creatObject.Add(go);
                    }
                }
                if (time == -10)
                {
                    Trans(WaveState.Ongoing);
                }
                break;
            case WaveState.Ongoing:
                if (creats.Count>3)
                {
                    for (int i = 0; i < perTickCreat; ++i)
                    {
                        if (creats.TryPop(out var tmp))
                        {
                            var dir = Quaternion.LookRotation(points[i]-center);
                            dir.eulerAngles = new Vector3(dir.eulerAngles.x,0, dir.eulerAngles.z);

                            var go=Object.Instantiate(tmp,points[i]+ random.InsideUnitCircle().ToVector3()*10, dir,null);
                            units.Add(go.GetComponent<Actor>());

                            go.GetComponent<EnemyController>().SetNavDestination(
                                center + random.InsideUnitCircle().ToVector3() * 5
                            );

                        }
                        else
                        {
                            break;
                        }
                    }

                }
                else if(completeCreat==false)
                {
                    completeCreat = true;
                    for(int i=0;i< creatObject.Count; ++i)
                    {
                        creatObject[i].GetComponentInChildren<Animator>().Play("End");
                        creatObject[i].GetComponent<LimitedLife>().ResetLift(6);
                    }

                }
                else if(time%5==0)
                {
                    if (units.Count <= 3)
                    {
                        Trans(WaveState.NearEnd);
                    }
                }

                break;
            case WaveState.NearEnd:
                if (time % 5 == 0)
                {
                    if (units.Count == 0)
                    {
                        Trans(WaveState.End);
                    }
                }
                break;
            case WaveState.End:
                Dispose();
                return false;
        }
        return true;
    }

    void OnUnitDeath(Actor actor)
    {
        units.Remove(actor);
    }
    void Trans(WaveState state)
    {
        this.state = state;
        switch (state)
        {
            case WaveState.Start:
                if(tip) WndManager.Instance.CreatNotice("Yuuka", "WaveStart_Zerg");

                AudioSvc.PlayMusic(AudioSvc.MusicGroup.Wave, 0.3f);
                break;
            case WaveState.Ongoing:
                time = 0;
                break;
            case WaveState.NearEnd:
                if (tip) WndManager.Instance.CreatNotice("Yuuka", "WaveEnd_Zerg");
                break;
            case WaveState.End:
                AudioSvc.PlayMusic(AudioSvc.MusicGroup.Game,0.2f);
                break;
        }
    }

}

public class RobotWave : I_TickClass, System.IDisposable
{
    List<GameObject> waveUseObject;
    Stack<GameObject> creats;
    List<Actor> units;
    List<EagleGroupInfo> groups;

    WaveState state;
    int time;
    Vector3 center;
    Vector3[] points;
    float range;
    bool completeCreat;
    bool tip;
    bool IsDisposed;

    System.Random random;

    float lastGroupSpawnTime;
    float spawnInterval;

    static readonly Vector3[] s_RelativePositions = new Vector3[]
    {
        new Vector3(-7, -1, -5),
        new Vector3(7, -1, -5),
        new Vector3(-7, -1, 0),
        new Vector3(7, -1, 0),
        new Vector3(-7, -1, 5),
        new Vector3(7, -1, 5)
    };

    class EagleGroupInfo
    {
        public GameObject eagle;
        public List<GameObject> unitObjects;
        public float waitStartTime; // -1表示尚未进入等待阶段
        public bool dropped;
    }

    public RobotWave(WaveCreateParams param, Stack<GameObject> creats, List<GameObject> waveUseObject)
    {
        random = new Random(RandomUtils.Range(0, 1000));
        this.waveUseObject = new(waveUseObject);
        this.creats = creats;
        this.tip = param.tip;
        range = param.range;
        points = param.points;
        units = new();
        groups = new();
        center = param.center;

        spawnInterval = Mathf.Max(1f, 360f / Mathf.Max(1, creats.Count));
        lastGroupSpawnTime = Time.time;

        BattleEventSub.OnEnemyDead += OnUnitDeath;

        Trans(WaveState.Start);
    }

    public void Dispose()
    {
        if (IsDisposed) return;
        BattleEventSub.OnEnemyDead -= OnUnitDeath;

        foreach (var g in groups)
        {
            if (g.eagle && g.eagle.TryGetComponent(out PhoenixEagleController ctrl))
            {
                ctrl.onWait.RemoveAllListeners();
            }
        }
        groups?.Clear();
        units?.Clear();
        waveUseObject?.Clear();
        creats?.Clear();

        waveUseObject = null;
        creats = null;
        units = null;
        groups = null;
        random = null;

        IsDisposed = true;
    }

    public bool Tick()
    {
        --time;
        switch (state)
        {
            case WaveState.Start:
                if (time == -10)
                {
                    Trans(WaveState.Ongoing);
                }
                break;

            case WaveState.Ongoing:
                // 检查鹰群是否需要投放单位（等待阶段开始后3秒）
                for (int i = groups.Count - 1; i >= 0; --i)
                {
                    var g = groups[i];
                    if (g.dropped) continue;
                    if (g.waitStartTime > 0 && Time.time - g.waitStartTime >= 3f)
                    {
                        DropGroupUnits(g);
                        g.dropped = true;
                    }
                }

                // 周期性生成新鹰群
                if (creats.Count > 0)
                {
                    if (Time.time - lastGroupSpawnTime >= spawnInterval)
                    {
                        lastGroupSpawnTime = Time.time;
                        SpawnGroup();
                    }
                }
                else if (!completeCreat)
                {
                    completeCreat = true;
                }
                else if (time % 5 == 0)
                {
                    if (units.Count <= 3)
                    {
                        Trans(WaveState.NearEnd);
                    }
                }
                break;

            case WaveState.NearEnd:
                if (time % 5 == 0)
                {
                    if (units.Count == 0)
                    {
                        Trans(WaveState.End);
                    }
                }
                break;

            case WaveState.End:
                Dispose();
                return false;
        }
        return true;
    }

    void SpawnGroup()
    {
        // 从栈取出最多6个单位
        int count = Mathf.Min(6, creats.Count);
        List<GameObject> popped = new();
        for (int i = 0; i < count; ++i)
        {
            if (creats.TryPop(out var tmp))
            {
                popped.Add(tmp);
            }
        }

        if (popped.Count == 0) return;

        // 检查是否有大型单位（HalfRange >= 1）
        int bigIndex = -1;
        for (int i = 0; i < popped.Count; ++i)
        {
            var entity = popped[i].GetComponent<I_Entity>();
            if (entity != null && entity.HalfRange >= 1f)
            {
                bigIndex = i;
                break;
            }
        }
        Vector3 eaglePos;
        if (points == null)
        {
            // 在center附近创建waveUseObject[0]
            eaglePos = FpsHelper.GetNavMeshPoint(VectorUtils.GetRandomPointInCircle(center, range+5, range + 15));
        }
        else
        {
            // 在points附近创建waveUseObject[0]
            eaglePos = FpsHelper.GetNavMeshPoint(VectorUtils.GetRandomPointInCircle(points.RandomTake(), range, range + 10));
        }
       
        var eagle = VFXManager.Creat(waveUseObject[0], eaglePos, Quaternion.AngleAxis(RandomUtils.Range(0f, 360f), Vector3.up), null);
        if (!eagle) return;

        var eagleCtrl = eagle.GetComponent<PhoenixEagleController>();
        if (eagleCtrl)
        {
            // 订阅等待阶段开始事件
            eagleCtrl.onWait.RemoveAllListeners();
            eagleCtrl.onWait.AddListener(() =>
            {
                var g = groups.Find(x => x.eagle == eagle);
                if (g != null)
                {
                    g.waitStartTime = Time.time;
                }
            });
        }

        EagleGroupInfo group = new()
        {
            eagle = eagle,
            unitObjects = new(),
            waitStartTime = -1,
            dropped = false
        };

        if (bigIndex >= 0)
        {
            // 只实例化大型单位，其余重新入栈
            var bigUnit = popped[bigIndex];
            for (int i = 0; i < popped.Count; ++i)
            {
                if (i != bigIndex)
                {
                    creats.Push(popped[i]);
                }
            }

            var go = Object.Instantiate(bigUnit,FpsHelper.GetNavMeshPoint(center),default,null);
            foreach (var item in go.GetComponents<Behaviour>()) item.enabled = false;

            go.transform.parent = eagle.transform;
            go.transform.localPosition = new Vector3(0, -10, 0);
            group.unitObjects.Add(go);

            var actor = go.GetComponent<Actor>();
            if (actor) units.Add(actor);

            // 禁用Animator（挂在飞行器上时不应播放落地动画）
            var fx = go.GetComponent<EnemyControllerFX>();
            if (fx && fx.Animator)
            {
                fx.Animator.enabled = false;
            }
        }
        else
        {
            // 正常创建6个单位，挂在鹰上
            int actualCount = Mathf.Min(popped.Count, s_RelativePositions.Length);
            for (int i = 0; i < actualCount; ++i)
            {
                var go = Object.Instantiate(popped[i],FpsHelper.GetNavMeshPoint(center), default, null);
                //Debug.LogWarning("运输船坐标" + eagle.transform.position+"创建坐标"+go.transform.position,go);
                foreach (var item in go.GetComponents<Behaviour>()) item.enabled = false;
                go.transform.parent = eagle.transform;
                go.transform.localPosition = s_RelativePositions[i];
                //Debug.LogWarning("修改后坐标" + go.transform.position+"相对坐标"+ go.transform.localPosition, go);
                group.unitObjects.Add(go);

                var actor = go.GetComponent<Actor>();
                if (actor) units.Add(actor);

                var fx = go.GetComponent<EnemyControllerFX>();
                if (fx && fx.Animator)
                {
                    fx.Animator.enabled = false;
                }
            }
        }

        groups.Add(group);
    }

    void DropGroupUnits(EagleGroupInfo group)
    {
        if (!group.eagle) return;

        foreach (var unit in group.unitObjects)
        {
            if (!unit) continue;

            unit.transform.SetParent(null);
            var pos = unit.transform.position;
            unit.transform.position = FpsHelper.GetNavMeshPoint(pos);
            foreach (var item in unit.GetComponents<Behaviour>()) item.enabled = true;
            // 启用Animator（开始落地动画）
            var fx = unit.GetComponent<EnemyControllerFX>();
            if (fx && fx.Animator)
            {
                fx.Animator.enabled = true;
            }
        }
    }

    void OnUnitDeath(Actor actor)
    {
        units.Remove(actor);
    }

    void Trans(WaveState state)
    {
        this.state = state;
        switch (state)
        {
            case WaveState.Start:
                //TODO:现在没有对应的语音
                if (tip) WndManager.Instance.CreatNotice("Yuuka", "WaveStart_Zerg");
                AudioSvc.PlayMusic(AudioSvc.MusicGroup.Wave, 0.3f);
                break;
            case WaveState.Ongoing:
                time = 0;
                break;
            case WaveState.NearEnd:
                if (tip) WndManager.Instance.CreatNotice("Yuuka", "WaveEnd_Zerg");
                break;
            case WaveState.End:
                AudioSvc.PlayMusic(AudioSvc.MusicGroup.Game, 0.2f);
                break;
        }
    }
}