using System.Collections.Generic;
using System.Linq;
using Core;
using Core.Interface;
using FPSGame.Attribute;
using GameContract;


using Unity.FPS.Game;
using UnityEngine;
using UnityEngine.AI;
using Utils;
using Random = System.Random;
using UnitWeightCfg = CampData_SO.UnitWeightCfg;

namespace FPSGame.Game
{
    public class WaveManager : TickBehaviour
    {
        [SerializeField]
        string Suffix;
        int WaveCool;
        /// <summary>波次间隔倍率（定位混淆强化：加长波次间隔），1 表示无加成</summary>
        float WaveCoolMul = 1f;




        Dictionary<UnitTier, List<KVP<int, UnitWeightCfg>>> TierItemWeight;
        [SerializeField]
        List<SKVP<int, UnitTier>> TierWeight;

        List<KVP<int, List<UnitTier>>> Patrol;
        //List<Wave> waveGroup;
        List<GameObject> WaveUseObject;

        float m_lastWaveTime = Mathf.NegativeInfinity;
        [SerializeField]
        int waveValue;

        public int WaveCount => ticks.Count - 1;

        Random random;
        BattleManager manager;
        [DisplayField]
        EnemyVarietyType enemyVarietyType;

        private void Awake()
        {
            manager = BattleManager.Instance;
            random = manager.BattleRandom;
            var task = TaskManager.Instance.nowTask;
            var cfg = task.campData;
            Debug.Log(cfg.ShowName + " " + cfg.Suffix + task.campData.ShowName, task.campData);
            Suffix = cfg.Suffix;
            WaveCool = cfg.WaveCool;
            WaveUseObject = cfg.WaveUseObject;
            enemyVarietyType = cfg.enemyVarietyType;
            var tmp = cfg.templates.RandomTake();


            TierWeight = tmp.template.Select(item => new SKVP<int, UnitTier>(item.weight, item.tier)).Where(item => item.Key > 0).ToList();

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
                .Select(kvp => {
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

            waveValue = (int)((1 + 0.15f * task.ExtraDifficulty[2]) * 100
                * task.difficulty switch {
                    DifficultyEnum.Normal => 0.6f,
                    DifficultyEnum.Hard => 0.75f,
                    DifficultyEnum.VeryHard => 0.9f,
                    DifficultyEnum.HardCode => 1.0f,
                    DifficultyEnum.Extreme => 1.1f,
                    DifficultyEnum.Insane => 1.2f,
                    DifficultyEnum.Torment => 1.3f,
                    DifficultyEnum.Lunatic => 1.5f,
                    _ => 1f,
                });
            //理论上的极限是100*1.5*1.45=217，之前的极限是35*7*2=490
            if (manager.HaveBooster(BoosterType.PositionConfusion)) WaveCoolMul = 1.4f;
        }



        public bool CreatWave(WaveCreateParams param)
        {
            //时间没到或者不是强制刷新
            if (!param.extraWave && Time.time < m_lastWaveTime + WaveCool * WaveCoolMul) return false;
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
#if UNITY_EDITOR

        protected override void Update()
        {
            base.Update();
            if (Input.GetKeyUp(KeyCode.K))
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
#endif

        public override bool Tick()
        {
            return true;
        }

        Stack<GameObject> InitWaveUnits(float scale)
        {
            Stack<GameObject> re = new();
            var remain = waveValue * scale;
            bool hasBoss = false;
            int skipCount = 0;

            while (remain > 0)
            {
                UnitTier tier = TierWeight.WeightTake(100, random);
                var item = TierItemWeight[tier].WeightTake(100, random);

                // 首领只能出现一个：已出现首领后，再随机到首领单位则跳过本次，接着重新随机
                if (IsBossUnit(item.unit))
                {
                    if (hasBoss)
                    {
                        // 防死循环：连续多次都是首领（如某 tier 只有首领单位）时，强制接受并告警
                        if (++skipCount >= 20)
                        {
                            Debug.LogWarning($"InitWaveUnits: 连续 {skipCount} 次随机到首领，可能某 tier 只含首领单位，强制接受该单位");
                            hasBoss = true;
                            remain -= item.size;
                            re.Push(item.unit);
                            continue;
                        }
                        continue;
                    }
                    hasBoss = true;
                }

                remain -= item.size;
                re.Push(item.unit);
            }
            return re;
        }

        /// <summary>判断单位预制体是否为首领（拥有 ActorFlag.Boss 标签）</summary>
        static bool IsBossUnit(GameObject unit)
        {
            return unit && unit.GetComponent<Actor>() is { } actor && actor.HasFlag(ActorFlag.Boss);
        }

        public GameObject CreatUnit(UnitTier tier, Vector3 pos, float range, bool IsFixed = true)
        {
            //var random = BattleManager.Instance.BattleRandom;
            if (!TierItemWeight.TryGetValue(tier, out var weightList) || weightList.Count == 0)
            {
                // 降级：如果指定 tier 不存在，尝试使用字典中任意可用的 tier
                Debug.LogWarning($"CreatUnit: tier '{tier}' 在当前模板配置中不存在，降级到默认 tier");
                var firstKvp = TierItemWeight.FirstOrDefault();
                if (firstKvp.Value == null || firstKvp.Value.Count == 0)
                {
                    Debug.LogError("CreatUnit: 模板配置中没有可用的 tier，无法创建单位");
                    return null;
                }
                weightList = firstKvp.Value;
            }
            var item = weightList.WeightTake(100, random);
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
                Debug.LogError("错误:创建单位的目标点" + pos + "不存在");
            }

            var go = Object.Instantiate(item.unit, pos, Quaternion.Euler(random.RandomVector2().ToVector3()), manager.ACCont.transform);
            if (IsFixed)
            {
                go.GetComponent<I_AIController>().BirthDuration = 0;
                go.GetComponent<I_Actor>().IsFixed = true;
            }
            //Debug.LogWarning("创建单位   " + tier+"  " +go);
            return go;
        }

        public List<GameObject> CreatPatrol(Vector3 pos)
        {
            var re = new List<GameObject>();
            var temp = Patrol.WeightTake(100, random);
            temp.ForEach(item => re.Add(CreatUnit(item, pos, 5, false)));
            return re;
        }


    }

    public enum WaveState
    {
        Start,//开始
        Ongoing,//进行中
        NearEnd,//即将结束
        End//结束
    }
}