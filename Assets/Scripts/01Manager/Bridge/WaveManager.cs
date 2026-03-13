using System.Collections.Generic;
using Unity.FPS.Game;
using UnityEngine;
using System.Linq;
using Random = System.Random;
using Unity.BaseTool;
using Core;
using Utils;
using GameContract;

public class WaveManager : TickBehaviour
{
    string Suffix;
    int WaveCool;
    List<KVP<GameObject, Vector2Int>> Template;
    List<List<GameObject>> Patrol;
    //List<Wave> waveGroup;
    List<GameObject> WaveUseObject;

    float m_lastWaveTime=Mathf.NegativeInfinity;
    int waveValue;

    public int WaveCount => ticks.Count - 1;


    protected override void Start()
    {
        base.Start();
        var task = TaskManager.Instance.nowTaskCfg;
        var cfg = task.campData;
        Suffix = cfg.Suffix;
        WaveCool = cfg.WaveCool;
        WaveUseObject = cfg.WaveUseObject;
        var tmp = cfg.Templates.Values.RandomTake();
        Template = tmp.Template;
        Patrol = cfg.Patrol.Where(item => tmp.PatrolName.Contains(item.Key)).Select(item=>item.Value).ToList();
        waveValue = (int)((int)(task.difficulty) * 35*(1+0.33f*task.ExtraDifficulty[2]));
    }

    public bool CreatWave(Vector3 point, bool extraWave)
    {
        //时间没到或者不是强制刷的
        if (!extraWave && Time.time < m_lastWaveTime + WaveCool) return false;

        m_lastWaveTime = Time.time;
        var item = new ZergWave(point, InitWaveUnits(),WaveUseObject);
        ticks.Add(item);
        return true;
    }

    protected override void Update()
    {
        base.Update();
        if(Input.GetKeyUp(KeyCode.K))
        {
            CreatWave(ActorsManager.Player.Pos,false);
        }
    }

    public override bool Tick()
    {
        return true;
    }

    Stack<GameObject> InitWaveUnits()
    {
        Stack<GameObject> re = new();
        var remain = waveValue;
        int TotalWeight = Template.Sum(item=>item.Value.x);
        while (remain > 0)
        {
            int randomValue = RandomUtils.Range(0, TotalWeight);
            int accumulatedWeight = 0;
            for (int i = 0; i < Template.Count; i++)
            {
                accumulatedWeight += Template[i].Value.x;
                if (randomValue < accumulatedWeight)
                {
                    remain -= Template[i].Value.y;
                    re.Push(Template[i].Key);
                    break;
                }
            }
        }


        return re;
    }
}

public enum WaveState{
    Start,//开始
    Ongoing,//进行中
    NearEnd,//即将结束
    End//结束
}
public class ZergWave :I_TickClass
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



    System.Random random;
    public ZergWave(Vector3 point,Stack<GameObject> creats, List<GameObject> waveUseObject)
    {
        random = new Random(RandomUtils.Range(0,1000));
        this.waveUseObject = waveUseObject;
        this.creats = creats;

        creatObject = new();
        units = new();

        perTickCreat =Mathf.Max(2,Mathf.CeilToInt(creats.Count/45f));//保底2烟
        center = point;
        points = new Vector3[perTickCreat];

        float theta = random.Range(0, 2 * Mathf.PI);
        for(int i = 0; i < perTickCreat; ++i)
        {
            var dx= random.Range(-1,1f);//约120度
            points[i] = center+new Vector3(Mathf.Cos(theta+ dx),0, Mathf.Sin(theta+ dx))* random.Range(30, 40);

            if (Physics.Raycast(new Ray(points[i]+Vector3.up*250, Vector3.down), out var hit, 300, LayerDefinition.GroundLayers))
            {
                points[i] = hit.point;
            }
            else
            {
                points[i] = new Vector3(points[i].x,point.y, points[i].z);
            }
        }
        Trans(WaveState.Start);
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
                    if (units.ToVaild().Count <= 5)
                    {
                        Trans(WaveState.NearEnd);
                    }
                }

                break;
            case WaveState.NearEnd:
                if (time % 5 == 0)
                {
                    if (units.ToVaild().Count == 0)
                    {
                        Trans(WaveState.End);
                    }
                }
                break;
            case WaveState.End:
                return false;
        }
        return true;
    }

    void Trans(WaveState state)
    {
        this.state = state;
        switch (state)
        {
            case WaveState.Start:
                WndManager.Instance.CreatNotice("Yuuka2", "WaveStart_Zerg");

                AudioManager.PlayMusic(AudioManager.MusicGroup.Wave, 0.3f);
                break;
            case WaveState.Ongoing:
                time = 0;
                break;
            case WaveState.NearEnd:
                WndManager.Instance.CreatNotice("Yuuka2", "WaveEnd_Zerg");
                break;
            case WaveState.End:
                AudioManager.PlayMusic(AudioManager.MusicGroup.Game,0.2f);
                break;
        }
    }

}