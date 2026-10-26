using System.Collections.Generic;
using FPSGame.Attributes;

using UnityEngine;
using FPSGame.Audio;
using FPSGame.Utils;
using FPSGame.Core;
using FPSGame.Game;
using FPSGame.GameContract;
namespace FPSGame.AI
{
    /// <summary>
    /// 工厂型敌人的状态机
    /// </summary>
    [AddComponentMenu("单位/_状态机/巢穴")]
    public class EnemyNestBuild : AIInputBaseController<EnemyNestBuild.AIState>, INetSpawner
    {
        public enum AIState
        {
            Idle,
            Active,
            Death,
        }
        [SerializeField]
        [InspectorName("创建点")]
        private Transform creatPoint;
        [SerializeField]
        [InspectorName("创建音效")]
        private AudioClip creatCilp;
        [SerializeField]
        [InspectorName("生产单位")]
        private List<SKVP<GameObject,int>> creatData;
        [SerializeField]
        [InspectorName("脱战时间")]
        private int damagedTime=12;
        [SerializeField]
        [DisplayField(DisplayFieldEnum.RunRead)]
        private float lastDamagedTime = Mathf.NegativeInfinity, creatTime= Mathf.Infinity;

        #region 联机 · 房主唯一生成 / 成员复刻（4052）

        /// <summary>登记为"生成器"（复刻时由桥按来源键找回）。
        /// ⚠ 必须在 OnEnable：复刻出来的单位在 <c>Instantiate</c> 内**只跑过 Awake/OnEnable**，Start 还没跑。</summary>
        private void OnEnable() => SpawnerRegistry.Register(this);

        private void OnDisable() => SpawnerRegistry.Unregister(this);

        /// <summary>生成者身份键（房主上报、成员据此找回本端这一份）。</summary>
        public int SpawnSourceKey => NetSpawnKey.Of(gameObject);

        /// <summary>复刻即建即得，没有"等前置对象"的挂起状态。</summary>
        public bool HasPending => false;

        /// <summary>【成员】按房主下发的槽位/号复刻一只。⚠ 覆盖 <c>NetId</c> 必须早于设目标点（同帧就要用号）。</summary>
        public GameObject ReplaySpawn(int slot, int netId, Vector3 pos, Quaternion rot, Vector3 target, string prefabId)
        {
            if (creatData == null || slot < 0 || slot >= creatData.Count) return null;

            var go = Instantiate(creatData[slot].Key, pos, rot,
                ActorsManager.Instance != null ? ActorsManager.Instance.transform : null);
            var actor = go.GetComponent<Actor>();
            if (actor != null) actor.NetId = netId;

            var ctrl = go.GetComponent<EnemyController>();
            if (ctrl != null && target.sqrMagnitude > 0.0001f) ctrl.ApplyRemoteDestination(target);
            return go;
        }

        #endregion

        protected override Dictionary<AIState, AiStateHook> InitState()
        {
            return new Dictionary<AIState, AiStateHook>
            {
                [AIState.Idle] = new AiStateHook(),
                [AIState.Active] = new AiStateHook
                {
                    onUpdate = ActiveBehavior,
                },
                [AIState.Death] = new AiStateHook(),
            };
        }

        /// <summary>Active：按生产节奏生成单位并派遣（★ 联机：只有房主真生成，成员等 4052 复刻）。</summary>
        private void ActiveBehavior()
        {
            if (Time.time <= creatTime) return;

            // ⚠ 抽取必须走**本单位的派生流**：原来 `RandomUtils.RandomTake` 用的是全局静态流（被音效/弹孔/散布推进）
            //   ⇒ 两端抽到不同的 prefab。槽位 = 在本表里的下标（成员按同一下标取回同一把）。
            var item = EnemyRandom.For(this).RandomTake(creatData);
            if (item == null) return;
            creatTime = Time.time + item.Value;

            var ctrl = GetComponent<EnemyController>();
            Vector3 target = ctrl != null ? ctrl.Target.Pos : default;

            // 目标点随 4052 一起下发（成员复刻后同帧设上，否则"复刻出来原地罚站"）
            BattleHub.Current.CreatUnit(item.Key, creatPoint.position + creatPoint.forward, creatPoint.rotation,
                this, creatData.IndexOf(item), target);

            if(creatCilp) AudioSvc.PlaySound(new(creatCilp, transform.position, 40, FPSGame.Core.AudioGroups.Enemy));
        }

        protected override void UpdateCurrentAiState()
        {
            InvokeCurrentState();
        }
        protected override void UpdateAiStateTransitions()
        {
            switch (AiState)
            {
                case AIState.Idle:
                    if (Time.time - damagedTime < lastDamagedTime)
                    {
                        SwitchState(AIState.Active);

                        // ★ 生产间隔走本单位的确定性流（否则两端出兵节奏不同）
                    creatTime = Time.time + FPSGame.AI.EnemyRandom.For(this).Range(5f, 10f);//第一个生产时间随机
                    }
                    break;
                case AIState.Active:
                    if (Time.time - damagedTime > lastDamagedTime)
                    {
                        SwitchState(AIState.Idle);
                    }
                    break;
            }
        }

        /// <summary>
        /// 受击
        /// </summary>
        protected override void OnDamaged(Collider collider)
        {
            lastDamagedTime = Time.time;
        }
        protected override void OnDetectedTarget()
        {
            lastDamagedTime = Time.time;
        }
        protected override void OnLostTarget()
        {

        }


        protected override void OnDie()
        {
            SwitchState(AIState.Death);
        }
    }
}
