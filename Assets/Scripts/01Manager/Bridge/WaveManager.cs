using System.Collections.Generic;
using System.Linq;
using Core;
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

    List<List<UnitTier>> Patrol;
    //List<Wave> waveGroup;
    List<GameObject> WaveUseObject;

    float m_lastWaveTime=Mathf.NegativeInfinity;
    int waveValue;

    public int WaveCount => ticks.Count - 1;

    Random random;
    BattleManager manager;

    private void Awake()
    {
        manager = BattleManager.Instance;
        random = manager.BattleRandom;
        var task = TaskManager.Instance.nowTask;
        var cfg = task.campData;
        Debug.LogError(cfg.ShowName+" "+cfg.Suffix+ task.campData.ShowName, task.campData);
        Suffix = cfg.Suffix;
        WaveCool = cfg.WaveCool;
        WaveUseObject = cfg.WaveUseObject;
        var tmp = cfg.templates.RandomTake();


        TierWeight = tmp.template.Select(item => new SKVP<int, UnitTier>(item.weight, item.tier)).Where(item=>item.Key>0).ToList();
        
        TierItemWeight = new Dictionary<UnitTier, List<KVP<int, UnitWeightCfg>>>();
       
        foreach (var kvp in tmp.template)
        {
            var list = kvp.unitWeights
                .Select(cfg => new KVP<int, UnitWeightCfg>(kvp.weight, cfg))
                .ToList();

            Debug.LogError(string.Join(",",list.Select(item=>item.Value.unit.name).ToList()));

            if (TierItemWeight.ContainsKey(kvp.tier))
                TierItemWeight[kvp.tier] = list;
            else
                TierItemWeight.Add(kvp.tier, list);
        }
        
        Debug.LogError(cfg.ShowName + "选择" + tmp.name + "模板");

        Patrol = cfg.patrolCfgs
            .Where(item => tmp.PatrolTemplate.Contains(item.name))
            .Select(group => group.units
                .SelectMany(item => Enumerable.Repeat(0, item.Value)
                    .Select(_ => item.Key)
                ).ToList()
            )
            .ToList();


        waveValue = (int)((int)(task.difficulty) * 35 * (1 + 0.33f * task.ExtraDifficulty[2]));
    }

    public bool CreatWave(WaveCreateParams param)
    {
        //时间没到或者不是强制刷新
        if (!param.extraWave && Time.time < m_lastWaveTime + WaveCool) return false;

        m_lastWaveTime = Time.time;
        var item = new ZergWave(param, InitWaveUnits(param.scale), WaveUseObject);
        ticks.Add(item);
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
        var temp = Patrol.RandomTake(random);
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