using System.Collections;
using System.Collections.Generic;
using FPSGame.GameContract;

using UnityEngine;
using FPSGame.Utils;
using FPSGame.Managers;
using PEMaths;
using FPSGame.Game;
using FPSGame.AI;

namespace FPSGame.Effect
{

/// <summary>
/// 单位创建器，单例模式，随创随用。
/// 单例对象持续存活，不随某次创建结束而销毁，避免创建器生命周期竞态导致 CreateInfo 被吞。
/// </summary>
[AddComponentMenu("创建场景物体/单位")]
public class CreatEnemy : MonoBehaviour, INetSpawner
{
    /// <summary>
    /// 每帧最长阻塞时间
    /// </summary>
    private const float maxTimePerFrame = 0.01f;

    private static CreatEnemy instance;

    /// <summary>
    /// 待处理创建队列。static 保证所有创建器写入同一个队列，
    /// 不依赖 instance 指向的对象的实例字段，避免单例被替换时队列丢失。
    /// </summary>
    private static Queue<CreateInfo> queue;

    [SerializeField]
    float range;
    [Range(0,100)]
    [SerializeField]
    float probability = 50;
    [SerializeField]
    [InspectorName("类别")]
    UnitTier tier;

    private void Awake()
    {
        var info = new CreateInfo() {
            tier = tier,
            range = range,
            probability = probability,
            vector = transform.position,
        };

        if (!instance)
        {
            instance = this;
            // 静态队列懒初始化，避免 instance 存在但 queue 未建导致空引用
            queue = new();
        }

        // 无论是否首个，都入队到静态队列；非单例对象自身销毁即可
        queue.Enqueue(info);
        if (instance != this)
        {
            Destroy(gameObject);
        }
    }



    private void Update()
    {
        // ⚠ 判定用 BattleState.IsStartBattle 而不是 GameState == Game：
        //   BattleManager 在 `GameState = Game` 之后还有一个 yield 才 DrainInitQueue / IsStartBattle，
        //   这段间隙里本方法会**先消费 BattleRandom**，打乱初始化窗口内的随机顺序（联机两端会分叉）。
        if (!FPSGame.Data.BattleState.IsStartBattle) return;

        if (queue == null || queue.Count == 0)
        {
            return;
        }

        // ★ 联机（2026-10-12）：**成员端完全不跑**（不抽随机、不生成）—— 生成时机与随机全由房主定，
        //   本端等 4052 复刻。各生各的会让 `Actor.Awake` 的号段与房主错开（其后所有单位错位）。
        //   ⚠ 本程序集看不见 07_NetGame ⇒ 成员判定走 BattleManager.IsNetMember（09）。
        if (BattleManager.Instance != null && BattleManager.Instance.IsNetMember)
        {
            queue.Clear();
            return;
        }

        float startTime = Time.realtimeSinceStartup;
        while (queue.Count > 0)
        {
            var info = queue.Dequeue();
            if (BattleManager.Instance.BattleRandom.Bool(info.probability))
            {
                //Debug.LogError("创建类型为  "+ info.tier+"单位"+"  在" + info.vector+"  范围" + info.range);
                // ⚠ 走"统一入口"（房主唯一生成 + 上报 4052），别再用旧的那条 tier 重载（它两端各自生成、不发消息）
                BattleManager.Instance.CreatSpawnerUnit(info.tier, info.vector, info.range, this);
            }
            if (Time.realtimeSinceStartup - startTime >= maxTimePerFrame)
            {
                return;
            }
        }
    }

    #region 联机 · 房主唯一生成 / 成员复刻（4052）

    /// <summary>生成者身份键（房主上报、成员据此找回本端这一份）。</summary>
    public int SpawnSourceKey => NetSpawnKey.Of(gameObject);

    /// <summary>复刻即建即得，没有"等前置对象"的挂起状态。</summary>
    public bool HasPending => false;

    /// <summary>登记为"生成器"（⚠ 必须在 OnEnable：复刻出来的单位在 Instantiate 内只跑过 Awake/OnEnable）。</summary>
    private void OnEnable() => SpawnerRegistry.Register(this);

    private void OnDisable() => SpawnerRegistry.Unregister(this);

    /// <summary>【成员】按 (tier, prefabId) 回同一张 tier 表取回房主抽中的那把 prefab 复刻一只。
    /// ⚠ 覆盖 <c>NetId</c> 必须早于任何用到号的动作；<c>BirthDuration</c>/<c>IsFixed</c> 与房主侧对齐。</summary>
    public GameObject ReplaySpawn(int slot, int netId, Vector3 pos, Quaternion rot, Vector3 target, string prefabId)
    {
        var battle = BattleManager.Instance;
        var wave = battle != null ? battle.WaveCont : null;
        if (wave == null) return null;

        var prefab = wave.FindTierPrefab((UnitTier)slot, prefabId);
        if (prefab == null) return null;

        var go = Instantiate(prefab, pos, rot,
            ActorsManager.Instance != null ? ActorsManager.Instance.transform : null);
        var actor = go.GetComponent<Actor>();
        if (actor != null) actor.NetId = netId;

        var ai = go.GetComponent<I_AIController>();
        if (ai != null) ai.BirthDuration = 0;
        var ia = go.GetComponent<IActor>();
        if (ia != null) ia.IsFixed = true;
        return go;
    }

    #endregion

    private struct CreateInfo{
        public UnitTier tier;
        public float range;
        public Vector3 vector;
        public float probability;
    }

}
}
