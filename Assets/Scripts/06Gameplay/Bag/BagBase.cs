using System;
using System.Collections.Generic;
using FPSGame.GameContract;
using FPSGame.Game;
using UnityEngine;
using UnityEngine.Events;

namespace FPSGame.Gameplay
{
    public class BagBase : MonoBehaviour, IEquippable, IVehicleUIController
    {
        Color defaultColor = new(0.9f, 0.96f, 1, 0.35f), overheateColor = new(1, 0.5f, 0.5f, 0.35f);


        #region 接口

        public UnityAction<bool, bool> SetWeaponState { get; set; }
        public UnityAction<bool> OnStateChange { get; set; }
        public UnityAction<bool, Color> OnColorChange { get; set; }
        public UnityAction<bool, float> OnFillChange { get; set; }
        public UnityAction<bool, string> OnTextChange { get; set; }
        public UnityAction<bool, Sprite> OnIconChange { get; set; }

        #endregion

        [InspectorName("补充所需的时间")]
        [Range(0, 20)]
        public float RefillDurationGrounded = 15f;

        [InspectorName("开始补充前的延迟时间")]
        [Range(0, 5)]
        public float RefillDelay = 1f;

        [SerializeField]
        private EquippableFlagEnum flag;

        public IActor Owner { get; private set; }

        /// <summary>是否"本机玩家"在驱动这件装备：只有本机玩家带 PlayerController + PlayerInputHandler。
        /// <para>⚠ 联机盟友实例（FriendController）两者都为 null ⇒ 读输入 / 读重力的逻辑（喷气、闪现）
        /// 与本人 HUD 都必须先看这个标记，否则就是"远端重放装上来 ⇒ 每帧 NRE / 别人的 HUD 亮在我屏幕上"。</para></summary>
        public bool IsLocalDriver { get; private set; }

        string IEquippable.ID => GetComponent<IFurniture>().Id;

        public event Action<IEquippable> OnEquipDestroy;

        protected PlayerController m_PlayerCharacterController;
        protected PlayerInputHandler m_InputHandler;

        [SerializeField]
        private float currentFillRatio = 1;

        [InspectorName("装备时")]
        [SerializeField]
        protected UnityEvent OnInstallEvent;

        [InspectorName("卸下时")]
        [SerializeField]
        protected UnityEvent OnUninstallEvent;

        /// <summary>剩余燃料(0-1)</summary>
        public float CurrentFillRatio
        {
            get => currentFillRatio;
            protected set
            {
                currentFillRatio = Mathf.Clamp01(value);

                OnColorChange?.Invoke(false, Color.Lerp(defaultColor, overheateColor, Mathf.InverseLerp(0.4f, 0.7f, 1 - CurrentFillRatio)));
                OnFillChange?.Invoke(false, CurrentFillRatio);
            }
        }

        protected float m_LastTimeOfUse;//上次使用时间


        public virtual void OnInstall(IActor actor, Func<IEnumerable<IEquippable>> getEquippableList)
        {
            Owner = actor;
            m_PlayerCharacterController = Owner.gameObject.GetComponent<PlayerController>();
            m_InputHandler = Owner.gameObject.GetComponent<PlayerInputHandler>();
            IsLocalDriver = m_PlayerCharacterController != null && m_InputHandler != null;
            // 挂到承载组件的背部点位；没有就退到"模型根"；两者都没有（联机盟友实例）时**保持原父级** ——
            // 绝不能再像原来那样直接取 .ModleRoot（PlayerController 为 null ⇒ 整条远端重放链炸掉）。
            var mountPoint = Owner.gameObject.GetComponent<PlayerMountPoint>();
            Transform anchor = mountPoint != null ? mountPoint.BackPoint : null;
            if (anchor == null && m_PlayerCharacterController != null && m_PlayerCharacterController.ModleRoot != null)
                anchor = m_PlayerCharacterController.ModleRoot.transform;
            if (anchor != null)
            {
                transform.parent = anchor;
                transform.localEulerAngles = Vector3.zero;
                transform.localPosition = Vector3.zero;
            }
            //CurrentFillRatio = 1f;
            OnInstallEvent?.Invoke();
        }

        public virtual void OnUninstall()
        {
            Owner = null;
            IsLocalDriver = false;
            m_PlayerCharacterController = null;
            m_InputHandler = null;
            transform.parent = null;
            OnStateChange?.Invoke(false);
            OnUninstallEvent?.Invoke();
        }
        protected virtual void OnDestroy()
        {
            OnEquipDestroy?.Invoke(this);
        }

        public bool NeedUninstall(IEquippable newEquip)
        {
            if (newEquip.ID.Contains("Bag"))
            {
                return true;
            }
            return false;
        }

        protected virtual void Update()
        {
            // 随着时间的推移，重新填充仪表
            if (CurrentFillRatio < 1 && Time.time - m_LastTimeOfUse >= RefillDelay)
            {
                float oldValue = CurrentFillRatio;
                float refillRate = 1 / RefillDurationGrounded;
                CurrentFillRatio = CurrentFillRatio + Time.deltaTime * refillRate;
                if (oldValue < 1 && CurrentFillRatio >= 1) OnStateChange?.Invoke(false);
            }


        }

        public bool HaveFlag(EquippableFlagEnum flag)
        {
           return this.flag.HasFlag(flag);
        }
    }
}