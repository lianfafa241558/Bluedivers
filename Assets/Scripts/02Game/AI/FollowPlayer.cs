using Unity.FPS.Game;
using UnityEngine;

namespace Unity.FPS.AI
{
    public class FollowPlayer : MonoBehaviour
    {
        Transform m_PlayerTransform;
        Vector3 m_OriginalOffset;

        void LateUpdate()
        {
            if (ActorsManager.Player!=null && (m_PlayerTransform != ActorsManager.Player.transform))
            {
                m_PlayerTransform = ActorsManager.Player.transform;
                m_OriginalOffset = transform.position - m_PlayerTransform.position;
            }
            if(m_PlayerTransform) transform.position = m_PlayerTransform.position + m_OriginalOffset;
        }
    }
}