using PEMaths;
using UnityEngine;
using Utils;

namespace FPSGame.AI
{
    /// <summary>
    /// 侦测模块：监听战备空投事件（BattleEventSub.OnAirdrop），
    /// 收到空投时把落点设为当前目标并触发发现，令单位转向攻击该点。
    /// 不做强制锁定，攻击超时（AirdropAttackTimeout）后自动清除点位目标停止攻击，
    /// 期间/之后仍可被常规侦测的敌人目标覆盖。
    /// </summary>
    public class DetectionModuleAirdrop : DetectionModule
    {
        //[InspectorName("响应空投")]
        //public bool RespondAirdrop = true;

        //[InspectorName("空投攻击超时")]
        //[Tooltip("攻击空投点多久后自动清除目标停止攻击，0=不超时（永远攻击该点）")]
        //public float AirdropAttackTimeout = 4f;


        /// <summary>空投目标偏移半径上限</summary>
        private const float MaxOffsetRadius = 32f;
        /// <summary>单单位基准半径：r = 该值 × √(激活数)，1 个激活单位时即等于该半径</summary>
        private const float BaseUnitRadius = 8f;

        /// <summary>当前激活(OnEnable)的模块数量，随敌人数放大空投攻击目标的散布范围</summary>
        private static int _activeCount;

        /// <summary>当前激活的模块数量（只读，供外部/调试查看）</summary>
        public static int ActiveCount => _activeCount;

        /// <summary>记录最近一次收到空投的时刻，用于超时清除</summary>
        private float _lastAirdropTime;

        private void OnEnable()
        {
            _activeCount++;
            BattleEventSub.OnAirdrop += OnAirdrop;
        }
        private void OnDisable()
        {
            _activeCount--;
            BattleEventSub.OnAirdrop -= OnAirdrop;
        }

        private float GetRadius()
        {
            return Mathf.Min(BaseUnitRadius * Mathf.Sqrt(ActiveCount), MaxOffsetRadius);
        }

        private Vector3 HandleOffest()
        {
            // 圆盘面积 ∝ r²：让半径随激活数开方增长（r ∝ √n），
            // 面积随激活数线性增长，单位在圆盘内的分布密度保持均匀，
            // 避免线性增长导致激活数越大散布越稀疏。
            float radius = GetRadius();
            return (BattleManager.Instance.BattleRandom.InsideUnitCircle() * radius).ToVector3();
        }

        void OnAirdrop(GameObject source, GameObject beacon, Vector3 point, AirdropController.AirdropData data)
        {
            if (data.cfg.ID != Constants.ArtilleryId
                && data.cfg.ID != Constants.PlayerArtilleryAId
                && data.cfg.ID != Constants.PlayerArtilleryBId) return;

            //if (!RespondAirdrop) return;
            // 尝试搜索范围的单位
            var target=BattleManager.Instance.FindUnits(new PECircle(new(point), new(GetRadius())),TargetCfg.Enemy);
            if (target.Count > 0)
            {
                Target.Set(target.RandomTake(BattleManager.Instance.BattleRandom));
            }
            // 把空投落点设为当前目标并触发发现，令单位转向攻击该点
            else
            {
                Target.Set(point + HandleOffest());
            }

            LastKnownTargetPos = point;
            TimeLastSeenTarget = Time.time;
            IsSeeingTarget = true;
            _lastAirdropTime = Time.time;
            OnDetect();
        }

        public override bool Tick()
        {
            // 空投点位目标超时清除：停止攻击该点
            if (_lastAirdropTime > 0
                && KnownTargetTimeout > 0
                && Time.time - _lastAirdropTime > KnownTargetTimeout)
            {
                _lastAirdropTime = 0;
                Target.Set(Vector3.zero);
                OnLostTarget();
            }
            return base.Tick();
        }
    }
}
