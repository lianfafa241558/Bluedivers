using System.Collections.Generic;
using FPSGame.Attribute;
using GameContract;
using UnityEngine;

namespace FpsGame.Mission
{
    /// <summary>
    /// 采集虫蛋(神器)的主任务。
    /// <para>4 个伴生子任务的场地里各有一个 ArtifactPlane，它会生成一件神器(ArtifactA~D = <see cref="Furniture_HandEquip"/>)。</para>
    /// <para>子任务本身不会被完成（只作为场地/氛围），任务进度只看"提交的神器数量"：</para>
    /// <para>① 任务启动时<b>直接收集这几件神器实例</b>并逐件订阅其拾取事件 —— 神器首次被玩家从地面拾起时，
    /// 由本任务在该处引来一波额外波次（丢弃后重新捡起不再触发）；</para>
    /// <para>② 玩家把神器交给凯伊(<see cref="Furniture_KeiSubmit"/>)提交后进度 +1（提交即移除，不作存储）；</para>
    /// <para>③ 提交够指定数量即任务完成。</para>
    /// </summary>
    [AddComponentMenu("任务/主要/采集虫蛋", 30)]
    public class MissionCollectEggs : MissionBase
    {
        [Foldout("采集虫蛋", true)]
        [SerializeField]
        [InspectorName("需要提交的神器数量")]
        [Tooltip("0 表示改用任务数据(MainTask)里配置的目标数量")]
        private int _needSubmitCount;

        [SerializeField]
        [InspectorName("神器物品ID")]
        [Tooltip("在子任务场地实体里按该 Id 筛选神器（与凯伊提交点的白名单对应）；留空则只按 Furniture_HandEquip 类型取")]
        private string _artifactId = "Artifact";

        [SerializeField]
        [InspectorName("首次拾取额外波次的规模倍率")]
        private float _waveScale = 0.5f;

        /// <summary>本任务收集到的神器实例</summary>
        private readonly List<Furniture_HandEquip> _artifacts = new();

        /// <summary>还没因"首次拾取"刷过波次的神器；一旦刷过就移出，丢弃后重拾不会重复刷</summary>
        private readonly List<Furniture_HandEquip> _wavePending = new();

        /// <summary>已提交的神器数量</summary>
        private int _submitted;

        protected override void StartMission()
        {
            base.StartMission();

            MaxProgress = ResolveNeedCount();
            NowProgress = 0;
            _submitted = 0;
            if (missionTag.HasFlag(MissionTag.DisplayProgress)) percentage = 0f;

            CollectArtifacts();

            UpdateTip($"收集神器并交给凯伊 [{NowProgress}/{MaxProgress}]");
        }

        protected override void Uninit()
        {
            foreach (var artifact in _artifacts)
            {
                if (!artifact) continue;
                artifact.OnPicked -= OnArtifactPicked;
                artifact.OnSubmitted -= OnArtifactSubmitted;
            }
            _artifacts.Clear();
            _wavePending.Clear();

            base.Uninit();
        }

        /// <summary>需要提交的数量：优先用脚本上配置的数量，未配置时退回任务数据的目标数量</summary>
        private int ResolveNeedCount()
        {
            if (_needSubmitCount > 0) return _needSubmitCount;
            if (data != null && data.targetCount > 0) return data.targetCount;
            return 1;
        }

        /// <summary>
        /// 启动时从各子任务的实体里拿到神器实例并逐件订阅（不做全局扫描）。
        /// <para>神器由子任务场地里的 ArtifactPlane 生成、且是场地实体的子物体，而 ArtifactPlane 在开场初始化队列
        /// (<see cref="BattleManager.EnqueueInit"/>)里生成，该队列在 <c>IsStartBattle</c> 之前执行，
        /// 任务是之后才 <c>EventStart</c> 的，所以此处能一次找齐。</para>
        /// </summary>
        private void CollectArtifacts()
        {
            _artifacts.Clear();
            _wavePending.Clear();

            if (subTask == null || subTask.Length == 0)
            {
                Debug.LogWarning("[采集虫蛋] 没有伴生子任务，收集不到神器", this);
                return;
            }

            foreach (var sub in subTask)
            {
                // 子任务的场地实体（未配置实体的子任务直接跳过）
                if (!sub || !sub.entity) continue;

                var found = sub.entity.GetComponentsInChildren<Furniture_HandEquip>(true);
                foreach (var item in found)
                {
                    if (!item) continue;
                    // _artifactId 留空表示只看类型不过滤 Id
                    if (!string.IsNullOrEmpty(_artifactId) && item.Id != _artifactId) continue;
                    // 子任务场地理论上互不重叠，保险起见去个重
                    if (_artifacts.Contains(item)) continue;

                    _artifacts.Add(item);
                    _wavePending.Add(item);
                    item.OnPicked += OnArtifactPicked;
                    item.OnSubmitted += OnArtifactSubmitted;
                }
            }

            Debug.Log($"[采集虫蛋] 从 {subTask.Length} 个子任务实体里收集到 {_artifacts.Count} 件神器", this);
        }

        /// <summary>神器被玩家从地面拾起：只认首次，在该神器所在位置引来一波额外波次</summary>
        private void OnArtifactPicked(Furniture_HandEquip artifact)
        {
            if (!artifact || !_wavePending.Remove(artifact)) return;

            BattleManager.Instance.CreatWave(WaveCreateParams.Extra.Set(artifact.transform.position).Scale(_waveScale));
        }

        /// <summary>神器被提交点（凯伊）交出：计入进度，提交够数量即完成</summary>
        private void OnArtifactSubmitted(Furniture_HandEquip artifact)
        {
            if (completed) return;

            ++_submitted;
            NowProgress = Mathf.Min(_submitted, MaxProgress);
            percentage = MaxProgress > 0 ? (float)NowProgress / MaxProgress : 1f;

            if (NowProgress < MaxProgress)
            {
                UpdateTip($"收集神器并交给凯伊 [{NowProgress}/{MaxProgress}]");
                return;
            }

            UpdateMission();
            CompleteMission();
        }
    }
}
