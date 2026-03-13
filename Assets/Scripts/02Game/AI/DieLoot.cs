using System.Collections;
using System.Collections.Generic;
using Unity.BaseTool;
using Unity.FPS.Game;
using UnityEngine;
namespace Unity.FPS.AI
{
    public class DieLoot : MonoBehaviour
    {
        [CustomLabel("此敌人死亡时可以掉落的物体")]
        public GameObject LootPrefab;

        [CustomLabel("物体掉落的数量")]
        public Vector2Int DropRate = Vector2Int.zero;
        //创建时就已决定
        private int lootCount;

        private void Awake()
        {
            lootCount = Random.Range(DropRate.x, DropRate.y);
        }

        void Start()
        {
           var m_Health = GetComponent<HealthEnemy>();
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