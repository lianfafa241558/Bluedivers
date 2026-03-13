using UnityEngine;

namespace Unity.FPS.Game
{
    /// <summary>
    /// TODO:限时生命，这玩意现在不用了，到时候删了
    /// </summary>
    public class TimedSelfDestruct : MonoBehaviour
    {
        public float LifeTime = 1f;

        float m_SpawnTime;

        void Awake()
        {
            m_SpawnTime = Time.time;
        }

        void Update()
        {
            if (Time.time > m_SpawnTime + LifeTime)
            {
                Destroy(gameObject);
            }
        }
    }
}