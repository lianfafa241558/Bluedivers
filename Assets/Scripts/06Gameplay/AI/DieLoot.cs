using FPSGame.GameContract;
using UnityEngine;
namespace FPSGame.AI
{
    /// <summary>
    /// 单位死亡后掉落配置好的物品。
    /// </summary>
    [AddComponentMenu("AI/死亡掉落")]
    public class DieLoot : MonoBehaviour
    {
        [InspectorName("此敌人死亡时可以掉落的物体")]
        public GameObject LootPrefab;

        [InspectorName("物体掉落的数量")]
        public Vector2Int DropRate = Vector2Int.zero;
        //创建时就已决定
        private int lootCount;

        private void Awake()
        {
            lootCount = Random.Range(DropRate.x, DropRate.y);
        }

        void Start()
        {
           var m_Health = GetComponent<IHealth>();
            m_Health.OnDie += OnDie;
        }

        void OnDie(GameObject source)
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
