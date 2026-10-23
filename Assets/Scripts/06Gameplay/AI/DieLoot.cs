using FPSGame.GameContract;
using UnityEngine;
namespace FPSGame.AI
{
    /// <summary>
    /// 单位死亡后掉落配置好的物品。
    /// </summary>
    [AddComponentMenu("单位/死亡掉落")]
    public class DieLoot : MonoBehaviour
    {
        [InspectorName("此敌人死亡时可以掉落的物体")]
        public GameObject LootPrefab;

        [InspectorName("物体掉落的数量")]
        public Vector2Int DropRate = Vector2Int.zero;
        //创建时就已决定
        private int lootCount;

        void Start()
        {
            // ★ 静态同步（2026-10-12）：掉落数量改走**派生流** —— 原来 `UnityEngine.Random` 是全局流 ⇒ 两端数量必然不同。
            //   键 = 本单位的 NetId（两端一致：波次同序 / 场景派生号 / 4052 房主分配）⇒ 两端数量一致，**零消息**。
            //   ⚠ 必须放 Start：同一 GameObject 上 Actor 与 DieLoot 的 Awake 顺序不保证 ⇒ Awake 里读不到 NetId。
            lootCount = RollLootCount();

            var m_Health = GetComponent<IHealth>();
            m_Health.OnDie += OnDie;
        }

        /// <summary>掉落数量：有权威种子 ⇒ 按 (种子, NetId) 派生；无种子（单机 / 旧版房主）⇒ 退回原来的全局随机。</summary>
        private int RollLootCount()
        {
            if (DropRate.y <= DropRate.x) return DropRate.x;

            int seed = FPSGame.Data.TaskState.Seed;
            if (seed == 0) return Random.Range(DropRate.x, DropRate.y);

            var actor = GetComponent<IActor>();
            int netId = actor != null ? actor.NetId : 0;
            var rand = new System.Random(
                FPSGame.Utils.SeedUtil.Derive(FPSGame.Utils.SeedUtil.Derive(seed, FPSGame.Utils.SeedStream.DieLoot), netId));
            return DropRate.x + rand.Next(DropRate.y - DropRate.x);   // 与 UnityEngine.Random.Range(int,int) 同口径：上界不含
        }

        void OnDie(GameObject caused, GameObject origin)
        {
            if (lootCount > 0)
            {
                for (int i=0;i<lootCount;++i)
                {
                    Instantiate(LootPrefab, transform.position+new Vector3(Mathf.Sin(i), Mathf.Cos(i), Mathf.Sin(-i)*0.2f), Quaternion.identity);
                }
            }
        }

    }
}
