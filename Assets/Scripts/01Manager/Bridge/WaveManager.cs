using System.Collections.Generic;
using Unity.FPS.Game;
using UnityEngine;
using System.Linq;
using Random = System.Random;
using Unity.BaseTool;
using Core;
using Utils;
using GameContract;
using UnitWeightCfg = CampData_SO.UnitWeightCfg;

public class WaveManager : TickBehaviour
{
    string Suffix;
    int WaveCool;
    Dictionary<UnitTier, List<KVP<int, UnitWeightCfg>> > TierItemWeight;
    List<KVP<int,UnitTier>> TierWeight;

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
        Suffix = cfg.Suffix;
        WaveCool = cfg.WaveCool;
        WaveUseObject = cfg.WaveUseObject;
        var tmp = cfg.Templates.Values.RandomTake();

        TierWeight = tmp.Template.Select(item => new KVP<int, UnitTier>(item.Value.weight, item.Key)).ToList();
        
        TierItemWeight = new Dictionary<UnitTier, List<KVP<int, UnitWeightCfg>>>();
        foreach (var kvp in tmp.Template)
        {
            var list = kvp.Value.unitWeights
                .Select(cfg => new KVP<int, UnitWeightCfg>(kvp.Value.weight, cfg))
                .ToList();

            if (TierItemWeight.ContainsKey(kvp.Key))
                TierItemWeight[kvp.Key] = list;
            else
                TierItemWeight.Add(kvp.Key, list);
        }

        Patrol = cfg.Patrol
            .Where(item => tmp.PatrolTemplate.Contains(item.Key))
            .Select(group => group.Value.Template
                .SelectMany(item => Enumerable.Repeat(0, item.Value)
                    .Select(_ => item.Key)
                ).ToList()
            )
            .ToList();


        waveValue = (int)((int)(task.difficulty) * 35 * (1 + 0.33f * task.ExtraDifficulty[2]));
    }

    public bool CreatWave(WaveCreateParams param)
    {
        //时间没到或者不是强制刷的
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

    public GameObject CreatUnit(UnitTier tier, Vector3 pos,float range, bool NoVfx = true)
    {
        //var random = BattleManager.Instance.BattleRandom;
        var item = TierItemWeight[tier].WeightTake(100, random);

        if (UnityEngine.AI.NavMesh.SamplePosition(pos, out var hit, 10, UnityEngine.AI.NavMesh.AllAreas))
        {
            pos = hit.position;
        }

        var go = Object.Instantiate(item.unit, pos + random.RandomVector2().ToVector3() * range, Quaternion.Euler(random.RandomVector2().ToVector3()), null);
        if (NoVfx) go.GetComponent<I_AIController>().BirthDuration = 0;
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

        GlobalEventManager.OnEnemyDead += OnUnitDeath;

        if (param.points == null)
        {
            perTickCreat = Mathf.Max(1, Mathf.CeilToInt(creats.Count / 45f));//保底1烟
            points = new Vector3[perTickCreat];
            float theta = random.Range(0, 2 * Mathf.PI);
            for (int i = 0; i < perTickCreat; ++i)
            {
                var dx = random.Range(-1, 1f);//约120度
                points[i] = center + new Vector3(Mathf.Cos(theta + dx), 0, Mathf.Sin(theta + dx)) * random.Range(param.range, param.range + 10);

                if (UnityEngine.AI.NavMesh.SamplePosition(points[i], out var hit, 100, UnityEngine.AI.NavMesh.AllAreas))
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

                if (UnityEngine.AI.NavMesh.SamplePosition(points[i], out var hit, 100, UnityEngine.AI.NavMesh.AllAreas))
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
        GlobalEventManager.OnEnemyDead -= OnUnitDeath;

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
                            go.GetComponent<Unity.FPS.AI.EnemyController>().SetNavDestination(center + random.InsideUnitCircle().ToVector3() * 5);

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
                if(tip) WndManager.Instance.CreatNotice("Yuuka2", "WaveStart_Zerg");

                AudioManager.PlayMusic(AudioManager.MusicGroup.Wave, 0.3f);
                break;
            case WaveState.Ongoing:
                time = 0;
                break;
            case WaveState.NearEnd:
                if (tip) WndManager.Instance.CreatNotice("Yuuka2", "WaveEnd_Zerg");
                break;
            case WaveState.End:
                AudioManager.PlayMusic(AudioManager.MusicGroup.Game,0.2f);
                break;
        }
    }

}