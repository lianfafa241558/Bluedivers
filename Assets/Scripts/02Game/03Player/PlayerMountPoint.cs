using FPSGame.Attribute;
using RootMotion.FinalIK;
using UnityEngine;

namespace Unity.FPS.Game
{
    /// <summary>
    /// 玩家承载点位组件：集中管理玩家身上的挂载点与手部 IK，供各类装备/武器使用。
    /// - HandPoint：拾取道具点（手持装备 HandEquip 用）
    /// - BackPoint：背部点位（背包装备 BagBase 用，通过 BackPointTag 在动态模型内查找）
    /// - SetHandIK/ClearHandIK：统一封装 FullBodyBipedIK 左右手吸附，避免各系统各自操作 m_fullIK
    /// 
    /// 注意：玩家模型是动态加载的，fullIK 与 BackPoint 都在动态模型内，
    /// 因此不在 Awake 解析，而是订阅 PlayerController.OnBodySet（模型就绪）后再解析。
    /// </summary>
    public class PlayerMountPoint : MonoBehaviour
    {
        [SerializeField]
        [InspectorName("拾取道具点（手持装备挂载于此）")]
        private Transform handPoint;

        [DisplayField]
        [SerializeField]
        [Tooltip("背部点位（背包装备挂载于此）")]
        private Transform backPoint;

        [DisplayField]
        [SerializeField]
        [InspectorName("玩家全身 IK（手部吸附用，模型就绪后自动从模型内解析）")]
        private FullBodyBipedIK fullIK;

        [InspectorName("背部点位物体标签")]
        public string BackPointTag = "BackPoint";

        PlayerController m_Player;

        /// <summary>拾取道具点（手持装备挂载点）</summary>
        public Transform HandPoint => handPoint;

        /// <summary>背部点位（背包装备挂载点）</summary>
        public Transform BackPoint => backPoint;

        void Awake()
        {
            m_Player = GetComponent<PlayerController>();

            // 兼容旧配置：handPoint 未配置时，从 PlayerController.HandPoint 读取（迁移前的 Inspector 赋值）
            if (handPoint == null && m_Player != null)
            {
                handPoint = m_Player.HandPoint;
            }

            // fullIK / backPoint 依赖动态模型，不在 Awake 解析，等模型就绪
            if (m_Player != null)
            {
                m_Player.OnBodySet += OnBodySet;
            }
        }

        void OnDestroy()
        {
            if (m_Player != null)
            {
                m_Player.OnBodySet -= OnBodySet;
            }
        }

        /// <summary>
        /// 模型加载完成后解析动态模型内的 IK 与背部点位。
        /// 每次换模型都重新解析（覆盖旧模型引用，避免引用已销毁的旧 IK/点位）。
        /// </summary>
        void OnBodySet()
        {
            if (m_Player == null) return;

            // 重新解析模型内的手部 IK
            fullIK = GetComponentInChildren<FullBodyBipedIK>();

            // 按标签解析背部点位（在动态模型内递归查找）
            backPoint = m_Player.ModleRoot != null
                ? FindInChildren(m_Player.ModleRoot, BackPointTag)
                : null;
        }

        /// <summary>递归查找带指定标签的子物体</summary>
        static Transform FindInChildren(Transform root, string tag)
        {
            if (root == null) return null;
            if (root.CompareTag(tag)) return root;

            for (int i = 0; i < root.childCount; i++)
            {
                var found = FindInChildren(root.GetChild(i), tag);
                if (found != null) return found;
            }
            return null;
        }

        /// <summary>设置左右手 IK 目标（装备/武器握点），null 的手不吸附</summary>
        public void SetHandIK(Transform lHand, Transform rHand)
        {
            if (fullIK == null) return;

            fullIK.solver.leftHandEffector.target = lHand;
            fullIK.solver.rightHandEffector.target = rHand;
            fullIK.solver.leftHandEffector.positionWeight = lHand ? 1 : 0;
            fullIK.solver.rightHandEffector.positionWeight = rHand ? 1 : 0;
            fullIK.solver.leftHandEffector.rotationWeight = lHand ? 1 : 0;
            fullIK.solver.rightHandEffector.rotationWeight = rHand ? 1 : 0;
        }

        /// <summary>清除左右手 IK 目标（放下装备/武器时调用）</summary>
        public void ClearHandIK()
        {
            if (fullIK == null) return;

            fullIK.solver.leftHandEffector.target = null;
            fullIK.solver.rightHandEffector.target = null;
            fullIK.solver.leftHandEffector.positionWeight = 0;
            fullIK.solver.rightHandEffector.positionWeight = 0;
            fullIK.solver.leftHandEffector.rotationWeight = 0;
            fullIK.solver.rightHandEffector.rotationWeight = 0;
        }
    }
}
