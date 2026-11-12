using FPSGame.Attributes;
using FPSGame.GameData;
using FPSGame.Gameplay;
using PEMaths;
using UnityEngine;

namespace FPSGame.Weapon
{
    // ⚠ 别写 using WeaponType = ...：基类有个 string 字段就叫 WeaponType，成员名会遮蔽别名
    using WType = WeaponTypeEnum;

    /// <summary>
    /// 支援位（<see cref="WType.Support"/>）的"发射手雷的武器"：与玩家的**投掷物槽**共用弹药与伤害配置。
    ///
    /// <para>▍口径（用户口径）：两把都是能用的武器，**谁开火两边一起扣**，弹量始终是同一个数；
    /// 切到本武器时再把数据源的伤害配置整份抄过来。</para>
    ///
    /// <para>▍刻意**不**同步：子弹预制体 / 速度 / 下坠 / 继承初速 / 引信(<c>MaxRange</c>/<c>MinRange</c>) /
    /// 生命周期 —— 这些是"这把武器自己怎么把东西扔出去"，各按各的 prefab
    /// （用户明确：投掷物速度和下坠速度不变）。</para>
    ///
    /// <para>▍数据源：<see cref="SourceManager"/>（留空 ⇒ <c>Owner</c> 身上的 <see cref="PlayerWeaponsManager"/>）
    /// 的投掷物槽。源是运行时 Instantiate 的（<c>SetStatrtWeapon</c> / <c>RebuildRoleWeapons</c> 会重建），
    /// 所以绑定做成可重入：<see cref="LogicTick"/> 里发现源为空就按 0.5 秒节流重找一次。</para>
    /// </summary>
    [AddComponentMenu("武器/手雷联动武器(支援位)")]
    public class WeaponGrenadeLinkController : WeaponPlayerController
    {
        [Foldout("手雷联动", true)]
        [InspectorName("数据源(玩家武器管理器)")]
        [Tooltip("留空 = 从 Owner 身上找 PlayerWeaponsManager。数据源固定取它的投掷物槽(WeaponTypeEnum.Grenade)")]
        public PlayerWeaponsManager SourceManager;

        [InspectorName("同步伤害配置")]
        [Tooltip("切到本武器时把数据源的 DamageData 战斗部分抄过来；子弹预制体、速度、下坠、引信保持本武器自己的")]
        public bool SyncDamageConfig = true;

        [InspectorName("联动日志")]
        public bool LinkLog;

        /// <summary>数据源武器（玩家投掷物槽的实例）。会被换枪/重建替换，所以运行时绑定、不序列化。</summary>
        private WeaponPlayerController _source;

        /// <summary>还有一次"全量同步"没做成（自己或源还没 LogicInit 建出弹量属性时留到后面的逻辑帧）。</summary>
        private bool _pendingSync;

        /// <summary>下一次重找数据源的时刻：源可能还没生成 / 刚被销毁，按 0.5 秒节流，别每帧 GetComponent。</summary>
        private float _nextResolveTime;

        private int _warnedMagazineCap = int.MinValue;
        private int _warnedAmmoCap = int.MinValue;

        /// <summary>数据源的弹量变化事件是否已订阅（源的 Magazine/Ammo 要等它自己 LogicInit 跑完才存在）。</summary>
        private bool _ammoEventsBound;

        /// <summary>正在做"程序性同步"。⚠ 本类写源弹量会触发源的变化事件，
        /// 不挡住就会在"自己开火"时反推回来把自己多扣一次。</summary>
        private bool _syncing;

        // ==================== 生命周期 ====================

        public override void LogicInit()
        {
            base.LogicInit();

            if (WeaponTypeEnum != WType.Support)
            {
                Debug.LogWarning($"[手雷联动] {name} 的武器类型应配成 Support（要占支援槽 2），当前是 {WeaponTypeEnum}", this);
            }

            // 自己开火 ⇒ 把"扣完后的弹量"推给数据源（OnShoot 早于 UseMagazine，见 SyncAfterShot）
            OnShoot -= OnSelfShoot;
            OnShoot += OnSelfShoot;

            BindSource();
            _pendingSync = true;
            TrySyncAll();   // 有些路径是先 ShowWeapon(true) 再 Awake 的
        }

        public override void LogicUnInit()
        {
            OnShoot -= OnSelfShoot;
            UnbindSource();
            base.LogicUnInit();
        }

        /// <summary>"切到这把武器"的唯一入口。⚠ 调用方持有的是基类引用，所以必须 override（不能 new）。
        /// <para>源此刻可能还没建出弹量属性 ⇒ 真正落地在 <see cref="TrySyncAll"/> 里判空后交给后续逻辑帧。</para>
        /// </summary>
        public override void ShowWeapon(bool show)
        {
            base.ShowWeapon(show);
            if (!show) return;

            BindSource();
            _pendingSync = true;
            TrySyncAll();
        }

        public override void LogicTick()
        {
            base.LogicTick();

            if (_source == null) ResolveSourceSlowly();
            if (_pendingSync) TrySyncAll();
        }

        // ==================== 数据源绑定 ====================

        /// <summary>把数据源绑到"玩家武器管理器的投掷物槽"上（可重入：源变了才重订）。</summary>
        private void BindSource()
        {
            var manager = SourceManager != null
                ? SourceManager
                : (Owner != null ? Owner.GetComponent<PlayerWeaponsManager>() : null);

            var source = manager != null
                ? manager.GetWeaponAtSlotIndex(PlayerWeaponsManager.SlotOf(WType.Grenade))
                : null;

            // prefab 配错槽位（自己就占着投掷槽）时不做任何事，否则会自己同步自己
            if (source == this) source = null;

            if (source == _source) return;

            UnbindSource();
            _source = source;
            if (_source != null) _source.OnShoot += OnSourceShoot;
        }

        private void UnbindSource()
        {
            UnbindAmmoEvents();
            if (_source != null) _source.OnShoot -= OnSourceShoot;
            _source = null;
        }

        private void ResolveSourceSlowly()
        {
            if (Time.time < _nextResolveTime) return;
            _nextResolveTime = Time.time + 0.5f;

            BindSource();
            if (_source != null) _pendingSync = true;
        }

        // ==================== 弹药同步 ====================

        private void OnSelfShoot(WeaponBaseController shooter) => SyncAfterShot(this, _source);

        private void OnSourceShoot(WeaponBaseController shooter) => SyncAfterShot(_source, this);

        /// <summary>把"刚开火那一侧扣完后的弹量"抄到另一侧。
        /// <para>⚠ <c>OnShoot</c> 是在 <c>UseMagazine</c> **之前**抛的 ⇒ 回调里读到的是扣之前的数，
        /// 必须自己预扣一次；不预扣就成了"谁开火谁只扣自己，另一边永远少扣一发"。</para>
        /// <para>⚠ 只写对方、不写自己：自己随后会被基类的 <c>UseMagazine</c> 扣掉，写两次就双扣。</para>
        /// </summary>
        private void SyncAfterShot(WeaponReloadController shooter, WeaponReloadController target)
        {
            if (shooter == null || target == null || shooter == target) return;
            if (shooter.Magazine == null || shooter.Ammo == null) return;

            shooter.GetAmmoForSync(out int magazine, out int ammo);
            ConsumeAfterShot(shooter, ref magazine, ref ammo);

            _syncing = true;
            target.ApplyAmmoForSync(magazine, ammo);
            _syncing = false;

            if (LinkLog)
            {
                Debug.Log($"[手雷联动] {(shooter == this ? "本武器" : "投掷槽")}开火 ⇒ 同步弹量 {magazine}/{ammo}", this);
            }
        }

        /// <summary>按 <c>WeaponReloadController.UseMagazine</c> 的规则预扣一次（弹匣不够一发时余量从后备弹扣）。</summary>
        private static void ConsumeAfterShot(WeaponReloadController weapon, ref int magazine, ref int ammo)
        {
            if (magazine < 0) return;          // -1 = 该格无限（GetAmmoForSync 的约定），别动

            int cost = weapon.AmmoPerShot;
            if (cost <= 0) return;

            if (cost <= magazine)
            {
                magazine -= cost;
                return;
            }

            cost -= magazine;
            magazine = 0;
            if (ammo >= 0) ammo = Mathf.Max(0, ammo - cost);
        }

        // ==================== 非开火改动 ====================

        /// <summary>订阅源的弹量变化：补给（<c>PlayerWeaponsManager.UseThrow</c>）/ 自动换弹 / 背包补弹
        /// 这类**不经过 OnShoot** 的改动也要立刻抄过来，否则要切出再切回来才对齐。</summary>
        private void BindAmmoEvents()
        {
            if (_ammoEventsBound || _source == null) return;
            if (_source.Magazine == null || _source.Ammo == null) return;

            _source.Magazine.OnCurrValueChange += OnSourceAmmoChanged;
            _source.Ammo.OnCurrValueChange += OnSourceAmmoChanged;
            _ammoEventsBound = true;
        }

        private void UnbindAmmoEvents()
        {
            if (!_ammoEventsBound) return;
            _ammoEventsBound = false;
            if (_source == null) return;   // 源已销毁 ⇒ 事件随对象一起没了，再退订会抛
            if (_source.Magazine != null) _source.Magazine.OnCurrValueChange -= OnSourceAmmoChanged;
            if (_source.Ammo != null) _source.Ammo.OnCurrValueChange -= OnSourceAmmoChanged;
        }

        /// <summary>源弹量被改动（非开火路径）⇒ 按**当前真实值**快照对齐。
        /// <para>⚠ 只抄快照、不预扣：开火路径已由 <see cref="SyncAfterShot"/> 处理，随后 <c>UseMagazine</c>
        /// 真正写值时又会进这里一次，两次写的是同一个数（幂等）。</para>
        /// <para>⚠ 回调参数是**写入前的原始值**（setter 里 clamp 之后才生效），所以必须读 <c>CurrValue</c> 属性。</para>
        /// </summary>
        private void OnSourceAmmoChanged(PEInt _)
        {
            if (_syncing || _source == null) return;
            if (Magazine == null || Ammo == null || _source.Magazine == null || _source.Ammo == null) return;

            _source.GetAmmoForSync(out int magazine, out int ammo);

            _syncing = true;
            ApplyAmmoForSync(magazine, ammo);
            _syncing = false;

            if (LinkLog) Debug.Log($"[手雷联动] 投掷槽弹量变化 ⇒ 同步 {magazine}/{ammo}", this);
        }

        /// <summary>全量同步：当前弹量 + 伤害配置。任一侧还没建出属性就返回 false，保持 pending。</summary>
        private bool TrySyncAll()
        {
            if (_source == null || _source == this) return false;
            if (Magazine == null || Ammo == null) return false;
            if (_source.Magazine == null || _source.Ammo == null) return false;

            _source.GetAmmoForSync(out int magazine, out int ammo);
            ApplyAmmoForSync(magazine, ammo);

            if (SyncDamageConfig) SyncDamageConfigFromSource();

            WarnCapacityMismatch();
            BindAmmoEvents();   // 源的弹量属性已就绪 ⇒ 开始监听补给/换弹这类非开火改动

            _pendingSync = false;
            if (LinkLog) Debug.Log($"[手雷联动] 已从投掷槽 {_source.name} 同步弹量 {magazine}/{ammo}", this);
            return true;
        }

        /// <summary>把源的 DamageData 战斗部分抄到自己每一条配置上（子弹/速度/下坠/引信不动）。
        /// <para>⚠ 这里只改 <c>DamageData</c>，不改 <c>cfg</c> 里的 GameAttribute：本武器的改装加成与
        /// 参数面板仍按自己那份；一旦之后有人调 <c>ApplyUpgrade</c>，handler 会把 cfg 的终值重新写回
        /// <c>CurrentDamgeData</c>，届时以那次为准（切武器时会再同步一次）。</para>
        /// </summary>
        private void SyncDamageConfigFromSource()
        {
            if (_source.Damages == null || Damages == null) return;
            if (_source.Damages.Count == 0 || Damages.Count == 0) return;

            // 档位夹到本武器有效范围（信号枪那种运行时切档的用法不会出现在这里）
            UseDamageIndex = Mathf.Clamp(_source.UseDamageIndex, 0, Damages.Count - 1);

            int count = Mathf.Min(Damages.Count, _source.Damages.Count);
            for (int i = 0; i < count; ++i)
            {
                if (Damages[i] != null) Damages[i].CopyCombatConfigFrom(_source.Damages[i]);
            }
        }

        /// <summary>两边弹量上限不一致 ⇒ 共用的当前值会被本侧上限夹住（只提示一次，指路去配平 prefab）。</summary>
        private void WarnCapacityMismatch()
        {
            int magazineCap = Magazine.FinalValue.RawInt;
            int ammoCap = Ammo.FinalValue.RawInt;
            int sourceMagazineCap = _source.Magazine.FinalValue.RawInt;
            int sourceAmmoCap = _source.Ammo.FinalValue.RawInt;

            if (magazineCap == sourceMagazineCap && ammoCap == sourceAmmoCap) return;
            if (_warnedMagazineCap == magazineCap && _warnedAmmoCap == ammoCap) return;

            _warnedMagazineCap = magazineCap;
            _warnedAmmoCap = ammoCap;
            Debug.LogWarning(
                $"[手雷联动] {name} 与投掷槽 {_source.name} 的弹量上限不一致" +
                $"（弹匣 {magazineCap}/{sourceMagazineCap}，后备 {ammoCap}/{sourceAmmoCap}）" +
                " ⇒ 共用的当前值会被本侧上限夹住，建议在 prefab 上把两边配平。", this);
        }
    }
}
