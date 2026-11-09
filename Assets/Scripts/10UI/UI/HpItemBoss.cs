using System;
using FPSGame.AI;
using FPSGame.Audio;
using FPSGame.Core;
using FPSGame.Game;
using FPSGame.Utils;
using UnityEngine;

namespace FPSGame.UI
{
using static FPSGame.WndTools.WndRootTool;

    /// <summary>
    /// Boss 单位血条项（含护甲与异常状态）。
    /// </summary>
    [AddComponentMenu("UI/HUD/Boss 血条")]
    public class HpItemBoss : HpItemBase
    {
        private const float ShowDistance = 70f;
        private const float HideDelay = 15f;

        [SerializeField]
        private AudioClip showClip,deathCilp;
        [SerializeField]
        private Transform armorLayout;
        private PartController enemy;
        private int lenght;
        private Damageable[] damageables;
        private bool _visible;
        private float _hideTimer;

        // 无敌装甲同步 / 无敌高亮
        private Color _originalRColor;     // FillR 原始颜色，退出无敌时恢复
        private bool _isInvincibleShown;   // 当前是否处于无敌高亮，避免每帧重复 SetColor

        public override void Set(GameObject go)
        {
            base.Set(go);
            this.enemy = go.GetComponent<PartController>();

            SetFill(FillW, 1);
            SetFill(FillR, 1);
            // ★ 不是每个 Boss 都有部件系统：爆裂者家族（Dec_Bane_Boss / Ung_Bane_Boss）只挂 Actor，
            //   没有 PartController ⇒ 原样去读 invincibleArmor 就是 NRE，整条 Boss 血条建不出来（2026-11-09 实测）。
            //   无护甲的单位只显示血条，护甲排整排隐藏。
            if (enemy != null)
            {
                RebuildArmor();
            }
            else if (armorLayout != null)
            {
                SetActive(armorLayout, false);
            }
            _originalRColor = GetColor(FillR);
            _isInvincibleShown = false;

            _hideTimer = HideDelay;
            // 一开始就在 70m 内则直接播放 Entry 显示，否则初始隐藏
            if (IsPlayerNear())
            {
                _visible = false;
                Show();
            }
            else
            {
                _visible = false;
                SetActive(gameObject, false);
            }
        }

        /// <summary>受击刷新：命中即显示，并把隐藏计时重置回满（之后由 Tick 里远离时递减到超时才隐藏）</summary>
        public override void Refresh()
        {
            base.Refresh();
            Show();
            _hideTimer = HideDelay;
        }

        /// <summary>按当前 PartController.invincibleArmor 重建护甲 UI 与事件订阅（增/减/清空降级均可同步）</summary>
        private void RebuildArmor()
        {
            // 清理旧订阅，避免重复/泄漏
            if (damageables != null)
            {
                for (int u = 0; u < damageables.Length; ++u)
                {
                    if (damageables[u] != null) damageables[u].OnDamage -= OnDamage;
                }
            }

            if (enemy == null) return;   // 无部件系统（见 Set）：事件残留订阅时直接退出

            damageables = enemy.invincibleArmor.Count > 0 ? enemy.invincibleArmor.ToArray() : enemy.deathArmor;
            lenght = damageables.Length;

            // 护甲格数由 Art 决定：护甲比格子多时不截断，Tick/GetChild 会越界抛 ArgumentOutOfRange。
            int slots = armorLayout.childCount;
            if (lenght > slots)
            {
                Debug.LogWarning($"Boss 血条护甲格不足：{enemy.name} 有 {lenght} 块护甲，UI 只有 {slots} 格", this);
                lenght = slots;
            }

            int i = 0;
            for (; i < lenght; ++i)
            {
                var item = damageables[i];
                Transform slot = armorLayout.GetChild(i);
                if (item == null)   // 预制体漏引用 / 已销毁的部位：空格子隐藏，别订阅
                {
                    SetActive(slot, false);
                    continue;
                }
                SetActive(slot, true);
                SetFill(armorLayout.GetChild(i, 1, 0), 1);
                SetFill(armorLayout.GetChild(i, 1, 1), 1);
                item.OnDamage += OnDamage;
            }
            for (; i < slots; ++i)
            {
                SetActive(armorLayout.GetChild(i), false);
            }

            // 订阅列表变化事件（先 -= 防重复）
            enemy.OnInvincibleArmorListChanged -= RebuildArmor;
            enemy.OnInvincibleArmorListChanged += RebuildArmor;
        }

        /// <summary>显示：播放 Entry 动画，首次显示播放 showClip</summary>
        private void Show()
        {
            if (_visible) return;
            _visible = true;
            SetActive(gameObject, true);
            anim.Play("Entry");
            AudioSvc.PlaySound(new(showClip, FPSGame.Core.AudioGroups.UI));
            AudioSvc.PlayMusic(AudioSvc.MusicGroup.Boss,1);
            AudioSvc.SetLockMusic(true);
        }

        /// <summary>隐藏：播放 Hide 动画淡出</summary>
        private void Hide()
        {
            if (!_visible) return;
            _visible = false;
            anim.Play("Hide");
            RestoreGameMusic();
        }

        /// <summary>把 Boss 音乐还给战斗音乐。
        /// <para>⚠ 顺序不能反：<c>AudioSvc.PlayMusic(MusicGroup, float)</c> 开头就 <c>if (musicLock) return;</c>
        /// —— 先解锁再切歌，否则这一句是个空操作（Boss 音乐照旧压着，2026-11-08 实测"死亡后音乐没变回去"）。</para></summary>
        private void RestoreGameMusic()
        {
            AudioSvc.SetLockMusic(false);
            AudioSvc.PlayMusic(AudioSvc.MusicGroup.Game, 0.5f);
        }

        public override void Tick()
        {
            SetFill(FillW, health.GetHpRatio() - 0.02f, Time.deltaTime * 2);
            for (int i = 0; i < lenght; ++i)
            {
                SetFill(armorLayout.GetChild(i,1, 0),GetFill(armorLayout.GetChild(i, 1, 1)), Time.deltaTime * 2);
            }

            // 单位自身无敌时 FillR 变白，恢复时还原原色
            bool invincible = actor.HasFlag(ActorFlag.Invincible);
            if (invincible != _isInvincibleShown)
            {
                _isInvincibleShown = invincible;
                SetColor(FillR, invincible ? Color.white : _originalRColor);
            }

            if (IsPlayerNear())
            {
                // 范围内：显示并刷新隐藏冷却
                _hideTimer = HideDelay;
                if (!_visible) Show();
            }
            else if (_visible)
            {
                // 离开范围：15 秒后才隐藏
                _hideTimer -= Time.deltaTime;
                if (_hideTimer <= 0f)
                {
                    Hide();
                }
            }
        }

        private bool IsPlayerNear()
        {
            if (ActorsManager.Player == null || actor == null) return false;
            return Vector3.Distance(ActorsManager.Player.Pos, actor.Pos) <= ShowDistance;
        }

        public override void End()
        {
            if (damageables != null)
            {
                for (int i = 0; i < damageables.Length; ++i)
                {
                    if (damageables[i] != null) damageables[i].OnDamage -= OnDamage;
                }
            }
            if (enemy != null) enemy.OnInvincibleArmorListChanged -= RebuildArmor;
            anim.Play("Death");
            AudioSvc.PlaySound(new(deathCilp, FPSGame.Core.AudioGroups.UI));

            // ★ Boss 死亡走的是 Death 动画，**不经过 Hide()** ⇒ 音乐得在这里自己还回去。
            //   否则 Show() 里那次 SetLockMusic(true) 没人解锁：Boss 音乐一直压着，战斗音乐再也起不来（2026-11-08 实测）。
            //   ⚠ 只在"确实是我们放的音乐"（_visible）时还：血条因为 70m 外 / 超时先隐藏过的话，Hide() 早就还过了，
            //   这里再还一次等于把正在播的战斗音乐重头重放。
            if (_visible)
            {
                _visible = false;
                RestoreGameMusic();
            }
        }

        void OnDamage(Damageable damageable)
        {
            int index = damageables.FindIndex(item=>item==damageable);
            if(index>=0) SetFill(armorLayout.GetChild(index,1,1), damageable.GetArmorRatio());

            // 受击自动显示并重置隐藏冷却
            if (!_visible) Show();
            _hideTimer = HideDelay;
        }

        public override bool CanRecycle()
        {
            if (actor == null) return true;
            if (actor.ActorState == ActorState.Dead) return true;
            return false;
        }

    }
}
