using System.Collections.Generic;
using FPSGame.Core;
using FPSGame.GameData;
using FPSGame.Weapon;
using RootMotion.FinalIK;
using UnityEngine;

namespace FPSGame.Gameplay
{
    /// <summary>
    /// 盟友的武器槽位（**表现层**）：按角色配置实例化武器、只显示当前槽位、并负责开火表现。
    ///
    /// <para>▍为什么不直接用 <see cref="PlayerWeaponsManager"/>：它是本地玩家专属 ——
    /// <c>[RequireComponent(PlayerInputHandler)]</c> 且强依赖 <c>PlayerController</c>（相机 / <c>IsThirdPerson</c>）、
    /// <c>EquipController</c>、<c>PlayerMountPoint</c>、<c>WeaponCamera</c> ⇒ 盟友预制体上这些都没有
    /// （也不能有：会抢输入/相机）。所以这里只做"槽位 + 显示 + 开枪表现"。</para>
    ///
    /// <para>▍槽位口径沿用玩家那套：<see cref="PlayerWeaponsManager.SlotOf"/>（主0/副1/支援2/特殊3/投掷4/信号枪5/空手6），
    /// 网络消息里的 <c>SlotIndex</c> 直接就是它 ⇒ 两端不需要各自维护映射表。</para>
    ///
    /// <para>▍装载由 09 侧的桥调用（<c>Setup</c>）：桥才有 <c>ResSvc</c>/档案，也才拿得到 <c>PlayerProfile</c> 的武器与改装。</para>
    /// </summary>
    [AddComponentMenu("玩家/盟友武器槽(表现)")]
    public class FriendWeaponView : MonoBehaviour
    {
        [InspectorName("武器挂点")]
        [Tooltip("留空则直接挂在自身（建议挂在 ModelSocket 下的 WeaponSocket）")]
        [SerializeField] private Transform weaponSocket;

        /// <summary>
        /// 【持握姿态】武器挂点在"模型根空间"里的位置 —— 复刻**玩家瞄准态**：
        /// <c>FirstPersonSocket(0,1.15,0.10) + AimingWeaponPosition(0,0.06,0.24) = (0,1.21,0.34)</c>，
        /// 每个武器再叠加自己的 <c>AimOffset</c>（见 <see cref="ApplyAimHold"/>）。
        /// <para>⚠ 这两个点位在**玩家预制体**（`Player.prefab/FirstPersonSocket/` 下），动态模型里没有
        /// ⇒ 盟友只能按常量复刻；实机观感要微调就改这里（盟友预制体上覆盖）。</para>
        /// </summary>
        [InspectorName("瞄准态武器点(模型根空间)")]
        [SerializeField] private Vector3 aimSocketOffset = new Vector3(0f, 1.21f, 0.34f);

        /// <summary>挂模型的根（用来复刻玩家那套"侧身角"）。</summary>
        private Transform _modelRoot;

        /// <summary>槽位容器（下标 = 槽位，口径同 <see cref="PlayerWeaponsManager.SlotOf"/>）。</summary>
        private readonly WeaponPlayerController[] _slots = new WeaponPlayerController[9];

        /// <summary>枪械动画参数（枪自身控制器用它播开火动画，见 WeaponController 的 HandleShoot 注释）。</summary>
        private static readonly int AnimAttack = Constants.k_AnimAttackParameter;

        private FullBodyBipedIK _ik;
        private int _active = -1;

        /// <summary>当前显示的槽位（-1 = 没有）。</summary>
        public int ActiveSlot => _active;

        public bool HasWeapons
        {
            get
            {
                for (int i = 0; i < _slots.Length; i++) if (_slots[i] != null) return true;
                return false;
            }
        }

        /// <summary>
        /// 【桥调用】按角色武器预制体清单装配槽位（预制体为 null 的项跳过），并应用改装。
        /// <para>▍改装来源 = 网络资料里的 <c>PlayerProfile.Upgrades</c>（按 <see cref="WeaponTypeEnum"/> 索引，
        /// 与本地玩家 <c>ArchivesData_SO.GetWeaponUpgrade</c> 同形）⇒ 各端看到盟友的枪与本人一致。</para>
        /// </summary>
        /// <param name="prefabs">角色携带的武器预制体（可含 null）</param>
        /// <param name="upgrades">每类武器的改装选择（可为 null）；下标 = (int)WeaponTypeEnum</param>
        /// <param name="modules">每类武器**选中的模组下标**（可为 null）；下标 = (int)WeaponTypeEnum，&lt;0 = 无模组。
        /// <para>▍以前没有它 ⇒ 模组恒按 0（= 空模组）装，别人看不到你装的模组；来源见 <c>PlayerLoadoutMsg.Modules</c>。</para></param>
        public void Setup(IList<WeaponPlayerController> prefabs, int[][] upgrades, int[] modules = null)
        {
            Clear();

            if (prefabs == null) return;
            var parent = weaponSocket != null ? weaponSocket : transform;

            for (int i = 0; i < prefabs.Count; i++)
            {
                var prefab = prefabs[i];
                if (prefab == null) continue;

                int slot = PlayerWeaponsManager.SlotOf(prefab.WeaponTypeEnum);
                if (slot < 0 || slot >= _slots.Length || _slots[slot] != null) continue;

                var inst = Instantiate(prefab, parent);
                inst.transform.localPosition = Vector3.zero;
                inst.transform.localRotation = Quaternion.identity;
                inst.Owner = gameObject;   // 归属：命中特效/音效按它归属（枪伤不结算，但特效要认得是谁打的）
                inst.ShowWeapon(false);      // 先全部收起，由 SetActiveSlot 决定显示哪把
                _slots[slot] = inst;

                ApplyUpgrade(inst, upgrades, modules);
            }

            // 有武器就默认拿主手（与玩家 EquipGroundWeapon 的"自动切换"一致）
            if (_active < 0) SetActiveSlot(FindFirstSlot());

            ApplyAimHold(GetSlot(_active));   // ⚠ 显式再摆一次：SetActiveSlot 在同槽位时会早退，装配首帧可能没走到
        }

        /// <summary>切换显示的武器槽（网络同步的落点；-1/越界 = 只收起）。</summary>
        public void SetActiveSlot(int slotIndex)
        {
            if (slotIndex == _active) return;

            var old = GetSlot(_active);
            if (old != null) old.ShowWeapon(false);

            _active = slotIndex;
            var now = GetSlot(_active);
            if (now != null) now.ShowWeapon(true);

            ApplyHandIK(now);
            ApplyAimHold(now);   // 每把武器的 AimOffset / 侧身角不同 ⇒ 换槽要重摆
        }

        /// <summary>
        /// 【开火表现】枪口闪光 + 枪响 + 弹道 + 枪械开火动画（伤害由开枪者本机结算，这里不重放）。
        /// <para>⚠ 不走 <c>HandleShoot()</c>：那会真发子弹、真结算伤害（走的 <c>SpawnVisualBullet</c> 只发"表现弹"）。</para>
        /// </summary>
        /// <param name="direction">开火瞬间的射击方向（世界空间）；零向量 = 用枪口朝向</param>
        /// <param name="aimPoint">**开枪者准心实指的目标点**（世界空间）；零向量 = 未知。
        /// <para>▍有它就把方向重算成"枪口 → 目标点"：表现弹是**本地模拟**的（<c>SpawnVisualBullet</c>），
        /// 只给方向时落点由本端地形与枪口偏移决定 ⇒ 与开枪者看到的落点对不上（2026-10-07 实测）。</para></param>
        /// <param name="damageIndex">开枪那把枪的伤害档位（<c>WeaponBaseController.UseDamageIndex</c>）。
        /// <para>▍必须用**他的**档位：命中特效/表现弹都取自 <c>CurrentDamgeData</c>。信号枪 0 = 标记、1 = 呼叫战备
        /// （由 <c>PlayerWeaponsManager.OnInputCompletedAirdrop</c> 本地切）—— 用本端档位会把"客机叫的空投"重放成"标记"，
        /// 房主端凭空多一个标记（2026-10-10 实测）。越界一律夹到有效范围。</para></param>
        public void PlayShoot(Vector3 direction = default, Vector3 aimPoint = default, int damageIndex = 0)
        {
            var w = GetSlot(_active);
            if (w == null)
            {
                // 数据说话：最常见的一种"对端开火本端毫无反应" —— 当前槽位没有武器实例
                // （盟友的武器是按角色配置装配的，槽位对不上 / 装配失败都会走到这里）
                FPSGame.Utils.NetSyncLog.Warn("远端开火", $"当前槽 {_active} 没有武器实例 ⇒ 这一发没有表现（有武器的槽：{SlotList()}）");
                return;
            }

            // ★ 先落到"他的档位"，再生成表现弹/命中特效（旧版发送端档位恒 0 ⇒ 与改造前一致）
            if (w.Damages != null && w.Damages.Count > 0)
                w.UseDamageIndex = Mathf.Clamp(damageIndex, 0, w.Damages.Count - 1);

            var muzzle = w.GetMuzzle(0);

            // ★ 目标点优先：弹道改成穿过开枪者瞄的那一点（旧版发送端没有目标点 ⇒ 保持原方向）
            if (muzzle != null && aimPoint.sqrMagnitude > 0.0001f)
            {
                var toAim = aimPoint - muzzle.position;
                if (toAim.sqrMagnitude > 0.0001f) direction = toAim.normalized;
            }

            // 数据说话：这一发到底"有没有收到瞄准点"、方向算成什么
            FPSGame.Utils.NetSyncLog.BulletLog("远端开火", $"槽{_active} 档位={w.UseDamageIndex} 方向={direction:F2} 瞄准点={(aimPoint.sqrMagnitude > 0.0001f ? aimPoint.ToString("F2") : "<无>")}" +
                (muzzle != null ? $" 枪口={muzzle.position:F2} 枪口到瞄准点={(aimPoint - muzzle.position).magnitude:F1}m" : " 枪口=<无>"));
            if (muzzle != null && w.MuzzleFlashPrefab != null)
            {
                var fx = VfxPool.Creat(w.MuzzleFlashPrefab, muzzle.position, muzzle.rotation, muzzle);
                if (fx != null) fx.transform.localScale = Vector3.one * w.FlashSizeScale;
            }

            // 枪响：走武器的公开入口（内部复用 SFXRange / 武器音频组），不产生射击逻辑
            w.PlayShootSfx();

            // 弹道：生成一颗"不结算伤害"的真子弹（飞行/尾迹/命中特效照常；伤害由他自己机器算）
            // ⚠ 每"次"射击只给一颗：连发与多弹丸不必在对端逐颗重放
            // ★ aimPoint 一并传下去（2026-10-09）：只给方向时那颗子弹会一路飞到射程尽头、从目标身上"穿过去"，
            //   看起来就是"没命中" —— 有终点后飞抵即收尾并按命中播火花（见 ProjectileBase.SetVisualEndPoint）。
            w.SpawnVisualBullet(muzzle, direction, aimPoint);

            var anim = w.GetComponentInChildren<Animator>(true);
            if (anim != null) anim.SetTrigger(AnimAttack);
        }

        /// <summary>
        /// 【俯仰】把武器挂点按"对方抬没抬头"转一个俯仰角（网络同步的落点，见 <c>PoseSnapshot.Pitch</c>）。
        ///
        /// <para>▍为什么转挂点而不是转模型：本地玩家也是这么做的 —— 身体只吃 yaw，**武器（FirstPersonSocket）**
        /// 吃相机俯仰（见 <c>PlayerController</c> 里 <c>FirstPersonSocket.rotation = Euler(垂直角, 身体yaw, 0)</c>）
        /// ⇒ 转模型会变成"整个人前倾/后仰"，转挂点才是"抬枪"。</para>
        /// </summary>
        public void SetAimPitch(float pitch)
        {
            if (weaponSocket == null) return;
            if (Mathf.Abs(_aimPitch - pitch) < 0.05f) return;   // 每帧都被调用 ⇒ 变化很小就别写（省脏标记）
            _aimPitch = pitch;
            weaponSocket.localEulerAngles = new Vector3(pitch, 0f, 0f);
        }

        /// <summary>最近一次应用的俯仰角（避免每帧重复写 transform）。</summary>
        private float _aimPitch;

        /// <summary>【模型换好后调用】重新解析模型内的全身 IK（手要吸到枪上）。</summary>
        public void BindModel(Transform modelRoot)
        {
            _modelRoot = modelRoot;
            _ik = modelRoot != null ? modelRoot.GetComponentInChildren<FullBodyBipedIK>(true) : null;
            ApplyHandIK(GetSlot(_active));
            ApplyAimHold(GetSlot(_active));   // 新模型 ⇒ 侧身角与持枪位置重新应用
        }

        /// <summary>清空所有武器实例（换角色/离场）。</summary>
        public void Clear()
        {
            for (int i = 0; i < _slots.Length; i++)
            {
                if (_slots[i] != null) Destroy(_slots[i].gameObject);
                _slots[i] = null;
            }
            _active = -1;
            ApplyHandIK(null);
        }

        // ==================== 内部 ====================

        private WeaponPlayerController GetSlot(int slot) =>
            slot >= 0 && slot < _slots.Length ? _slots[slot] : null;

        /// <summary>当前装了武器的槽位清单（诊断日志用：判断"是槽位对不上还是压根没装配"）。</summary>
        private string SlotList()
        {
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < _slots.Length; ++i)
            {
                if (_slots[i] == null) continue;
                if (sb.Length > 0) sb.Append(',');
                sb.Append(i).Append(':').Append(_slots[i].name);
            }
            return sb.Length > 0 ? sb.ToString() : "<一个都没有>";
        }

        private int FindFirstSlot()
        {
            // 只有可切换槽位（0-3）优先，没有才退到任意槽
            for (int i = 0; i <= PlayerWeaponsManager.MaxSwitchableSlotIndex; i++)
                if (_slots[i] != null) return i;
            for (int i = 0; i < _slots.Length; i++)
                if (_slots[i] != null) return i;
            return -1;
        }

        /// <summary>应用改装：<c>select</c> 与 <c>module</c> 都用资料里的数组（模组缺失/越界时退回 0 = 空模组）。</summary>
        private static void ApplyUpgrade(WeaponPlayerController inst, int[][] upgrades, int[] modules = null)
        {
            if (inst == null) return;

            int type = (int)inst.WeaponTypeEnum;
            if (type < 0) return;

            int[][] selectArr = upgrades != null && type < upgrades.Length ? upgrades : null;
            var select = selectArr != null ? selectArr[type] : null;
            if (select == null || select.Length == 0) return;

            int module = modules != null && type < modules.Length ? modules[type] : 0;
            if (module < 0) module = 0;   // -1 = 未选 ⇒ 与"空模组 0"同义

            inst.ApplyUpgrade(select, module);
        }

        /// <summary>手部 IK：照 <c>PlayerMountPoint.SetHandIK</c> 的写法，把模型双手吸到武器的握点上。</summary>
        private void ApplyHandIK(WeaponPlayerController w)
        {
            if (_ik == null) return;

            var l = w != null ? w.LHand : null;
            var r = w != null ? w.RHand : null;

            _ik.solver.leftHandEffector.target = l;
            _ik.solver.rightHandEffector.target = r;
            _ik.solver.leftHandEffector.positionWeight = l ? 1f : 0f;
            _ik.solver.rightHandEffector.positionWeight = r ? 1f : 0f;
            _ik.solver.leftHandEffector.rotationWeight = l ? 1f : 0f;
            _ik.solver.rightHandEffector.rotationWeight = r ? 1f : 0f;
        }

        /// <summary>
        /// 【持握姿态】把武器挂点摆成"**玩家瞄准态**"，并复刻玩家那套"侧身角"。
        ///
        /// <para>▍玩家侧（<c>PlayerWeaponsManager.LateUpdate</c>）：武器父点位被 lerp 到
        /// <c>DefaultWeaponPosition</c>（未瞄准）或 <c>AimingWeaponPosition + 武器的 AimOffset</c>（瞄准）；
        /// 同时把模型根的 Y 设成 <c>activeWeapon.playerAngle</c>（"装备时玩家模型旋转角度"，与是否瞄准无关）。
        /// 盟友没有输入、也没有那套点位 ⇒ 这里**固定按瞄准态**摆（用户明确要求）：远程玩家看起来是"持枪待发"，
        /// 而不是垂枪/斜抱。</para>
        ///
        /// <para>⚠ 那两个点位在**玩家预制体**里（<c>Player.prefab/FirstPersonSocket/</c>），动态模型里没有
        /// ⇒ 只能按 <see cref="aimSocketOffset"/> 常量复刻；实机观感要微调就改盟友预制体上的那个字段。</para>
        /// </summary>
        private void ApplyAimHold(WeaponPlayerController w)
        {
            if (weaponSocket != null)
            {
                // ⚠ 玩家的 Point 是"玩家根空间"的（WeaponSocket 挂在 ModelSocket 下、ModelSocket 在原点 ⇒ 同一空间）
                weaponSocket.localPosition = aimSocketOffset + (w != null ? w.AimOffset : Vector3.zero);
            }

            if (_modelRoot != null)
            {
                _modelRoot.localEulerAngles = new Vector3(0f, w != null ? w.playerAngle : 0f, 0f);
            }
        }
    }
}
