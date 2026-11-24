using FPSGame.Core;
using FPSGame.GameContract;
using FPSGame.Utils;
using UnityEngine;

namespace FPSGame.Game
{
    /// <summary>
    /// 迫击炮阵地任务点：玩家进入 <see cref="_warnDistance"/> 米后播一次 Kotama 警告，并在信息栏挂一条提示；
    /// 离开即撤提示，再次进入才重新播（<see cref="NoticeCooldown"/> 秒冷却）。
    ///
    /// <para>▍覆写 <c>Update</c> 时**必须先调 <c>base.Update()</c>**：基类那份负责发现任务 / 战备范围 / 一次性暴露等逻辑，
    /// 漏掉就等于把 MissionView 的基础行为整体关掉。</para>
    /// </summary>
    [AddComponentMenu("任务/任务点显示（迫击炮警告）")]
    public class MissionViewMortarWarn : MissionView
    {
        private const float NoticeCooldown = 10f;

        [SerializeField, InspectorName("警告距离（米）")]
        private int _warnDistance = 150;

        [SerializeField, InspectorName("信息栏文字")]
        private string _infoTitle = "";

        /// <summary>距离判定缓存：信息栏 Add/Remove 与提示有效性共用同一口径</summary>
        private bool _inRange;

        /// <summary>上次播警告的时刻（实例级：相邻两个阵地各提示各的）</summary>
        private float _lastNoticeTime = float.NegativeInfinity;

        protected override void Update()
        {
            base.Update();
            if (!TryGetDistance(out float distance))
            {
                SetInRange(false);
                return;
            }

            SetInRange(distance < _warnDistance);
        }

        /// <summary>取与本地玩家的水平距离；前置数据没就绪时返回 false。</summary>
        private bool TryGetDistance(out float distance)
        {
            distance = 0;
            if (FPSGame.Data.FlowState.GameState != GameStateEnum.Game) return false;
            if (!mission || !mission.IsInitialized) return false;
            if (!FPSGame.Data.BattleState.IsStartBattle) return false;

            var player = ActorsManager.Player;
            if (player == null) return false;

            distance = Vector2.Distance(player.Pos.ToVector2(), Pos.ToVector2());
            return true;
        }

        /// <summary>只在跨越边界时动作（每帧调用，边界内早退）。</summary>
        private void SetInRange(bool inRange)
        {
            if (inRange == _inRange) return;
            _inRange = inRange;

            if (inRange)
            {
                WindowRegistry.Information.Add(_infoTitle);

                if (Time.time - _lastNoticeTime < NoticeCooldown) return;
                _lastNoticeTime = Time.time;
                // func 语义是"这条提示是否仍然有效"（NoticeWnd 出队时判定）⇒ 在范围内才保留
                CreatNotice("Kotama", "MortarWarning", IsInRange);
            }
            else
            {
                WindowRegistry.Information.Remove(_infoTitle);
            }
        }

        private bool IsInRange() => _inRange;

        /// <summary>
        /// 任务完成 / 失败 / 卸载都会走 <see cref="MissionBase.Uninit"/> → <c>enabled = false</c> ⇒ 提示条在这里撤。
        /// <para>⚠ 不能只在"离开范围"时撤：任务完成后玩家往往还站在 150m 内，那条信息就永远挂在屏幕上。</para>
        /// </summary>
        private void OnDisable()
        {
            _inRange = false;
            WindowRegistry.Information.Remove(_infoTitle);
        }
    }
}
