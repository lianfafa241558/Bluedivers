using UnityEngine;

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

        /// <summary>记录最近一次收到空投的时刻，用于超时清除</summary>
        private float _lastAirdropTime;

        private void OnEnable()
        {
            BattleEventSub.OnAirdrop += OnAirdrop;
        }
        private void OnDisable()
        {
            BattleEventSub.OnAirdrop -= OnAirdrop;
        }

        void OnAirdrop(GameObject source, GameObject beacon, Vector3 point, AirdropController.AirdropData data)
        {
            if (data.cfg.ID != Constants.ArtilleryId
                && data.cfg.ID != Constants.PlayerArtilleryAId
                && data.cfg.ID != Constants.PlayerArtilleryBId) return;

            //if (!RespondAirdrop) return;
            // 把空投落点设为当前目标并触发发现，令单位转向攻击该点
            Target.Set(point);
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
