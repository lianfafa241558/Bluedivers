using System;
using System.Collections.Generic;
using FPSGame.Core.Interface;
using FPSGame.GameContract;
using UnityEngine;

namespace FPSGame.Gameplay
{
    /// <summary>
    /// 弹药背包（抽象基类）：一格视觉 = 一次可用次数。
    /// <para>▍规则：<see cref="TryUse"/> 吃掉一格 ⇒ 隐藏该格下面的全部物体；<see cref="TryRefill"/> 补一次
    /// ⇒ 还原刚才那格。用掉之后做什么留给子类的 <see cref="OnUse"/>，补回之后做什么留给 <see cref="OnRefill"/>。</para>
    /// <para>⚠ 不复用 <see cref="BagBase.Update"/> 的时间充能：这里的次数只由"用"和"补"两条路径改变，
    /// 让它按时间回满会和格子数打架。</para>
    /// </summary>
    public abstract class AmmoBagBase : BagBase
    {
        #region 参数

        [InspectorName("弹药格（一格 = 一次使用）")]
        [Tooltip("每个元素代表一次可用量：使用时隐藏该元素下所有子物体，补充时逐个还原。")]
        [SerializeField]
        private List<Transform> _slots = new List<Transform>();

        [InspectorName("从末尾开始消耗")]
        [Tooltip("勾选后先吃最后一个（视觉上从后往前掉），取消则从第一个开始。")]
        [SerializeField]
        private bool _consumeFromTail = true;

        #endregion

        /// <summary>已经被吃掉的格数</summary>
        private int _usedCount;

        public int SlotCount => _slots != null ? _slots.Count : 0;

        /// <summary>还剩几次</summary>
        public int RemainCount => Mathf.Max(0, SlotCount - _usedCount);

        public bool IsEmpty => RemainCount == 0;

        /// <summary>
        /// 使用一次。子类说"不成立"（<see cref="OnUse"/> 返回 false）时**不扣格**，
        /// 否则会出现"投掷物已经满着、背包却白掉一格"。
        /// </summary>
        public bool TryUse()
        {
            if (IsEmpty) return false;
            if (!OnUse()) return false;

            SetSlotVisible(_slots[SlotIndex(_usedCount)], false);
            _usedCount++;
            RefreshUI();
            return true;
        }

        /// <summary>补充一次：还原一格后再调 <see cref="OnRefill"/>。</summary>
        public bool TryRefill()
        {
            if (_usedCount <= 0) return false;

            _usedCount--;
            SetSlotVisible(_slots[SlotIndex(_usedCount)], true);
            RefreshUI();
            OnRefill();
            return true;
        }

        /// <summary>吃掉的瞬间子类要做的事。返回 false = 这次使用不成立（不扣格）。</summary>
        protected abstract bool OnUse();

        /// <summary>补回一格的瞬间子类要做的事（只还原视觉的子类留空即可）。</summary>
        protected abstract void OnRefill();

        /// <summary>第 offset 个被吃掉的格在列表里的下标</summary>
        private int SlotIndex(int offset) => _consumeFromTail ? SlotCount - 1 - offset : offset;

        /// <summary>隐藏/还原一格：只动子物体；一格自身就是模型（没有子物体）时才切它自己。</summary>
        private static void SetSlotVisible(Transform slot, bool visible)
        {
            if (slot == null) return;

            if (slot.childCount == 0)
            {
                slot.gameObject.SetActive(visible);
                return;
            }

            for (int i = 0; i < slot.childCount; i++)
            {
                slot.GetChild(i).gameObject.SetActive(visible);
            }
        }

        /// <summary>把剩余格数推到 UI（填充条 + "剩/总"文本）</summary>
        private void RefreshUI()
        {
            OnFillChange?.Invoke(true, SlotCount == 0 ? 0f : (float)RemainCount / SlotCount);
            OnTextChange?.Invoke(true, RemainCount + "/" + SlotCount);
        }

        protected override void Update()
        {
            // 不调 base.Update()：那是按时间充能（见类注释），弹药格子只能靠"用/补"变化
            if (!Owner.IsValidMono() || !IsLocalDriver) return; // 远端盟友实例没有输入源，读输入会 NRE
            if (m_InputHandler.GetUseBagDown()) TryUse();
        }

        public override void OnInstall(IActor actor, Func<IEnumerable<IEquippable>> getEquippableList)
        {
            base.OnInstall(actor, getEquippableList);

            // 每次装上都是满的：装背包 = 现场补给，不继承上一个使用者的残量
            _usedCount = 0;
            for (int i = 0; i < SlotCount; i++) SetSlotVisible(_slots[i], true);
            RefreshUI();
            OnStateChange?.Invoke(true);
        }
    }
}
