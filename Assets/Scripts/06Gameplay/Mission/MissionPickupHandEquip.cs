using System;
using System.Collections.Generic;
using FPSGame.Attributes;
using FPSGame.GameContract;
using FPSGame.Gameplay;
using UnityEngine;
using FPSGame.Utils;

namespace FPSGame.Mission
{
    /// <summary>
    /// 捡起实体内的手持物：任务实体（<see cref="MissionView"/>）下挂着若干可拾取的手持物
    /// （<see cref="Furniture_HandEquip"/>），玩家把它们捡起（拿在手里）达到指定数量即完成任务。
    /// <para>手持物被丢弃落地后可再次拾取，重复捡起是否重复计数由 <see cref="_countRepeatPick"/> 决定。</para>
    /// </summary>
    [AddComponentMenu("任务/捡起实体内的手持物", 30)]
    public class MissionPickupHandEquip : MissionBase
    {
        [SerializeField]
        [InspectorName("需要捡起的数量")]
        private int _meetCount = 1;

        [SerializeField]
        [InspectorName("只有指定名称的手持物才算（留空=实体内全部）")]
        private string[] _itemNames;

        [SerializeField]
        [InspectorName("丢下后再捡起是否重复计数")]
        private bool _countRepeatPick;

        private readonly List<Furniture_HandEquip> _targets = new List<Furniture_HandEquip>();
        private readonly HashSet<Furniture_HandEquip> _counted = new HashSet<Furniture_HandEquip>();

        protected override void StartMission()
        {
            NowProgress = 0;
            if (!entity.IsValid())
            {
                Debug.LogError($"[任务]{name} 未设置实体，无法查找手持物", this);
                return;
            }

            Furniture_HandEquip[] found = entity.GetComponentsInChildren<Furniture_HandEquip>(true);
            for (int i = 0; i < found.Length; i++)
            {
                Register(found[i]);
            }

            RefreshMaxProgress();
            if (missionTag.HasFlag(MissionTag.DisplayProgress)) percentage = 0.01f;
        }

        protected override void Uninit()
        {
            base.Uninit();
            ReleaseTargets();
            _counted.Clear();
        }

        /// <summary>
        /// 登记一个手持物为任务目标（供运行时动态生成的手持物补登记）。
        /// </summary>
        /// <param name="item">要登记的手持物</param>
        /// <returns>true = 登记成功并开始监听；false = 物体无效 / 不在名称白名单 / 已登记</returns>
        public bool Register(Furniture_HandEquip item)
        {
            if (!item.IsValid()) return false;
            if (_itemNames != null && _itemNames.Length > 0 && Array.IndexOf(_itemNames, item.Id) < 0) return false;
            if (_targets.Contains(item)) return false;

            _targets.Add(item);
            item.OnPicked.AddListener(OnPicked);
            return true;
        }

        /// <summary>
        /// 刷新进度上限：不重复计数时上限取"配置数量"与"已登记数量"的较小值，
        /// 避免配置数量多于实际手持物导致任务永远无法完成。
        /// </summary>
        private void RefreshMaxProgress()
        {
            MaxProgress = _countRepeatPick ? _meetCount : Mathf.Min(_meetCount, _targets.Count);
        }

        private void OnPicked(Furniture_HandEquip item)
        {
            // 不重复计数模式：同一件丢下再捡起只算一次
            if (!_countRepeatPick && !_counted.Add(item)) return;

            // 达成后走 CompleteMission → Uninit → ReleaseTargets 统一退订
            AddProgress(syncPercentage: true, updateBeforeComplete: true);
        }

        private void ReleaseTargets()
        {
            for (int i = 0; i < _targets.Count; i++)
            {
                if (_targets[i].IsValid()) _targets[i].OnPicked.RemoveListener(OnPicked);
            }
            _targets.Clear();
        }
    }
}
