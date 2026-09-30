using FPSGame.Core;

namespace FPSGame.GameContract
{
    [System.Serializable]
    public class TargetCfg
    {
        public UnitTypeEnum targetType= UnitTypeEnum.All;//可以是多类目
        public ActorState actorState = ActorState.Normal;//可以是多类目
        //public float selectRange=-1;

        public static TargetCfg EnemyAI = new() {targetType = UnitTypeEnum.All & ~UnitTypeEnum.Other };
        public static TargetCfg Enemy = new() { targetType = UnitTypeEnum.Enemy };
    }
}
