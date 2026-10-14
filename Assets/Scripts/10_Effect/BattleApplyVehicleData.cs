using RootMotion.FinalIK;
using FPSGame.Game;
using UnityEngine;
using UnityEngine.Experimental.GlobalIllumination;
using FPSGame.Utils;
using FPSGame.Data;
using FPSGame.Gameplay;
using FPSGame.GameData;
using FPSGame.Weapon;

namespace FPSGame.Effect
{

/// <summary>
/// 把载具数据应用到模型与贴图。
///
/// <para>▍归属口径（联机，用户 2026-10-10）：载具一般由**战备呼叫**生成 ⇒ **谁呼叫的算谁的**，
/// 由 <c>VFXAirdropEffect</c> 生成后调 <see cref="SetOwnerSid"/> 打标；不是呼叫出来的
/// （场景摆好的 / 任务脚本放的）⇒ **按房主（sid 0，默认值）**。</para>
///
/// <para>▍可被反复刷新：<c>VehicleCustomState.RegisterRefresh</c> 让 09 的桥在"配置/名单到达"时
/// 回调 <see cref="Apply"/>（<c>Apply</c> 自带"无变化跳过"）。</para>
/// </summary>
[AddComponentMenu("变体/载具数据应用")]
public class BattleApplyVehicleData : MonoBehaviour
{

    public VehicleData_SO data;
    public Transform weaponPointL, weaponPointR;
    public Transform LookPoint;
    [HideInInspector]
    public MpbController mpb;

    /// <summary>这辆车的**归属玩家** sid（0 = 房主）。默认房主 —— 场景摆好的 / 任务脚本放的载具都按房主的配置渲染。</summary>
    private uint _ownerSid;

    /// <summary>上一次应用的快照（用于跳过重复重刷：名单每次同步都会触发 <see cref="VehicleCustomState.RefreshAll"/>）。</summary>
    private bool _applied;
    private uint _appliedSid;
    private ArchivesData_SO.ArchVehicleData _appliedCfg;

    private VehicleWeaponsManager _manager;

    /// <summary>注册到 <see cref="VehicleCustomState"/> 的重刷回调（存成字段才能反注册）。</summary>
    private System.Action _refreshHandler;

    // ---- 皮肤属性：除了渲染器级的块，还必须写进"受击/溶解的共享槽位块"（见 FxControllerBase.GetSharedSlotBlock）----

    private static readonly int SkinBasePid = Shader.PropertyToID("_BaseMap");
    private static readonly int SkinBlendMapPid = Shader.PropertyToID("_BlendingMap");
    private static readonly int SkinBlendScalePid = Shader.PropertyToID("_BlendingScale");

    private Texture _skinBaseTex;
    private Texture _skinBlendTex;
    private float _skinBlendScale = 1f;

    /// <summary>"共享槽位还没盖上"的重试次数（<see cref="FPSGame.AI.FxControllerBase"/> 的槽位表是在它的
    /// <c>Start</c> 里建的，而本组件在 <c>Awake</c> 就贴了外观 ⇒ 那会儿还没有槽位）。用低频重试代替跨层回调。</summary>
    private int _slotStampRetry;

    /// <summary>低频补盖：等表现控制器把槽位表建好后，把皮肤补进共享块（最多重试 40 次 ≈ 10 秒）。</summary>
    private void Update()
    {
        if (_slotStampRetry <= 0) return;
        if (Time.frameCount % 15 != 0) return;   // 约 4 次/秒
        if (StampSkinToSharedSlots())
        {
            _slotStampRetry = 0;
            NetSyncLog.SyncLog("载具/外观",
                $"{name} 皮肤已补进受击共享槽位块（贴图={(_skinBaseTex != null ? _skinBaseTex.name : "<无>")}）");
            return;
        }
        --_slotStampRetry;
    }

    private void Awake()
    {
        _manager = GetComponent<VehicleWeaponsManager>();

        // 联机：配置/名单到达时重刷外观。09 的桥看不见本层（10 在 09 之上）⇒ 只能反向注册回调。
        _refreshHandler = Apply;
        VehicleCustomState.RegisterRefresh(_refreshHandler);

        // 默认按**房主**渲染：场景摆好的车 / 任务脚本放的车都算房主的；
        // 呼叫出来的车随后由 VFXAirdropEffect 用 SetOwnerSid(呼叫者) 纠正 ⇒ "谁叫的算谁的"。
        Apply();
    }

    private void OnDestroy()
    {
        if (_refreshHandler != null) VehicleCustomState.UnregisterRefresh(_refreshHandler);
    }

    /// <summary>
    /// 【空投 / 桥调用】设定这辆车的**归属玩家**（0 = 房主），并立刻按它重装外观。
    /// <para>▍口径（用户 2026-10-10）：载具一般由战备呼叫生成 ⇒ **谁呼叫的算谁的**；
    /// 不是呼叫出来的 ⇒ 按房主（就是这里的默认值）。</para>
    /// </summary>
    public void SetOwnerSid(uint sid)
    {
        _ownerSid = sid;
        Apply();
    }

    /// <summary>
    /// 按当前归属（<see cref="_ownerSid"/>）重装外观。
    /// <para>▍可重入：先清掉上一次装的武器 ⇒ 重复调用（归属纠正 / 配置到达）不会装两份武器。</para>
    /// <para>▍**无变化直接跳过**：<c>RefreshAll</c> 在名单每次同步时都会被调用（切准备状态也会），
    /// 每次都重建武器/贴图是白烧。</para>
    /// <para>▍取不到该 sid 的配置时退回本机存档（至少不是空外观）；配置到了 <c>RefreshAll</c> 会再刷一次。</para>
    /// </summary>
    public void Apply()
    {
        if (data == null) return;

        bool found = VehicleCustomState.TryGet(_ownerSid, data.vehicleName, out var cfg, out bool fromLocalArchive);

        // ⚠ 该 sid **有同步表、但表里没有这辆车** ⇒ **不许**回退本机存档（那就是"用我的配置冒充别人的"，
        //   与 VehicleCustomState.TryGet 的口径冲突）。此时保持当前外观不动，等真配置到（RefreshAll 会再调一次）。
        if (!found && VehicleCustomState.HasSid(_ownerSid))
        {
            NetSyncLog.SyncLog("载具/外观",
                $"{name} 跳过：sid={_ownerSid} 的同步表里没有 {data.vehicleName} ⇒ 保持当前外观；表={VehicleCustomState.DumpSid(_ownerSid)}");
            return;
        }

        if (_applied && _appliedSid == _ownerSid && SameCustom(_appliedCfg, cfg))
        {
            // ⚠ 这条以前是**完全静默**的 ⇒ 用户看到"皮肤不对"时无从判断"到底重刷过没有、用的是谁的配置"
            NetSyncLog.SyncLog("载具/外观",
                $"{name} 应用跳过(无变化)：sid={_ownerSid} 车={data.vehicleName} 皮肤={(cfg != null ? cfg.skinIndex : -1)}" +
                $" 来源={(fromLocalArchive ? "本机存档(兜底)" : "同步表")}");
            return;
        }

        _applied = true;
        _appliedSid = _ownerSid;
        _appliedCfg = Clone(cfg);   // 存档里那份是"活对象"（VehicleWnd 会原地改）⇒ 必须快照才能比较出变化

        // 数据说话：两端的"谁的车 / 哪辆车 / 皮肤几号 / 数据哪来的"要能一眼对上
        NetSyncLog.SyncLog("载具/外观",
            $"{name} 应用：sid={_ownerSid} 车={data.vehicleName} 皮肤={(cfg != null ? cfg.skinIndex : -1)}" +
            $" 来源={(fromLocalArchive ? "本机存档(兜底)" : "同步表")}" +
            (fromLocalArchive ? $"，该 sid 有表={VehicleCustomState.HasSid(_ownerSid)} 表={VehicleCustomState.DumpSid(_ownerSid)}" : ""));

        ApplyCustom(cfg);
    }

    /// <summary>【空投/表现切换点调用】无视"无变化跳过"**强制重贴**一次外观。
    ///
    /// <para>▍为什么需要：本组件的外观是**渲染器上的实例数据**（<c>MaterialPropertyBlock</c>），
    /// 而项目里还有别的表现系统也在写同一批渲染器：受击闪白/溶解走 <c>FxControllerBase</c> 的
    /// <c>RendererSlot</c>（<b>按材质下标</b> <c>SetPropertyBlock</c>，会把整块覆盖掉），
    /// 诞生表现走 <c>EnemyControllerFX</c>（在 <c>BirthDuration</c> 后 <c>RestoreMat()</c> 把
    /// <c>sharedMaterials</c> 整块写回）。两者都**不看**本组件写了什么 ⇒ 载具在"诞生/落地"这类
    /// 表现切换点上会**丢掉皮肤**（实测：天上挂运输机时是对的，落地卸载后皮肤变回默认）。</para>
    ///
    /// <para>▍所以：空投落地（<c>VFXAirdropEffect.UpdateMedivac</c> 卸载那一刻）必须重贴一次。
    /// 重贴走的是同一份配置（<see cref="Apply"/> 的"无变化跳过"会拦住，故这里直接落）。</para>
    /// </summary>
    public void ForceApply()
    {
        if (data == null) return;

        bool found = VehicleCustomState.TryGet(_ownerSid, data.vehicleName, out var cfg, out bool fromLocalArchive);
        if (!found && VehicleCustomState.HasSid(_ownerSid))
        {
            NetSyncLog.SyncLog("载具/外观", $"{name} 强制重贴被跳过：sid={_ownerSid} 的同步表里没有 {data.vehicleName}");
            return;
        }

        _applied = true;
        _appliedSid = _ownerSid;
        // ⚠ 配置**没变**时不要重建武器：那会销毁/重建武器模型（实测渲染器 14→18），既白烧又会把
        //   武器的外观重置回默认 —— 重贴只该重贴外观。
        bool sameCfg = SameCustom(_appliedCfg, cfg);
        _appliedCfg = Clone(cfg);   // 同步"已应用值"，避免下次 Apply 又重贴一遍

        NetSyncLog.SyncLog("载具/外观",
            $"{name} 强制重贴：sid={_ownerSid} 车={data.vehicleName} 皮肤={(cfg != null ? cfg.skinIndex : -1)}" +
            $" 来源={(fromLocalArchive ? "本机存档(兜底)" : "同步表")} 重建武器={!sameCfg}");

        ApplyCustom(cfg, !sameCfg);
    }

    /// <summary>【诊断】把"渲染器上**真正生效**的外观"打出来。
    /// <para>▍判读三件套：① 应用日志里的 <c>皮肤=N</c> = 配置侧查到的值；② 这里的 <c>期望贴图</c> = 本组件上次
    /// 落下去的贴图；③ <c>渲染器级Base</c> / <c>逐下标[idxX=有/无]</c> = 渲染器上实际生效的。
    /// ①与②不符 ⇒ 配置/索引问题；②与③不符 ⇒ 被别的表现系统盖掉了（见 <see cref="FxControllerBase"/> 的槽位块）。</para>
    /// <para>▍⚠ **只列本体渲染器**：粒子（<c>VFX_HitSparks</c> 这类）排在子物体最前面，
    /// 按默认顺序取前几个只会看到它们（2026-10-14 就踩了这个坑，日志全是特效渲染器）。</para>
    /// </summary>
    public void LogAppearance(string tag)
    {
        if (data == null)
        {
            NetSyncLog.SyncLog("载具/外观", $"{name} [{tag}] data=null");
            return;
        }

        string expect = _skinBaseTex != null ? _skinBaseTex.name : "<未贴过>";
        var rs = GetComponentsInChildren<Renderer>(true);
        var blk = new MaterialPropertyBlock();
        var fx = GetComponentInParent<FPSGame.AI.FxControllerBase>();
        var sb = new System.Text.StringBuilder();
        int shown = 0;
        int fxCount = 0;

        for (int i = 0; i < rs.Length; ++i)
        {
            var r = rs[i];
            if (r == null) continue;

            // 特效（粒子/拖尾）渲染器与外观无关：混进来只会把日志刷满
            if (r.GetComponentInParent<ParticleSystem>() != null || r.GetComponentInParent<TrailRenderer>() != null)
            {
                ++fxCount;
                continue;
            }
            if (shown >= 4) continue;

            // 逐个材质下标：带下标的那份块（受击闪白/溶解）会**盖住**渲染器级那份 —— 这是"皮肤莫名变回默认"的判据
            var detail = new System.Text.StringBuilder();
            int n = r.sharedMaterials.Length;
            for (int k = 0; k < n; ++k)
            {
                r.GetPropertyBlock(blk, k);
                bool has = blk.HasProperty(SkinBasePid) && blk.GetTexture(SkinBasePid) != null;
                detail.Append("idx").Append(k).Append(has ? "=有" : "=无")
                      .Append(fx != null && fx.HasSharedSlot(r, k) ? "(受击槽)" : "").Append(' ');
            }

            r.GetPropertyBlock(blk);
            var baseTex = blk.HasProperty(SkinBasePid) ? blk.GetTexture(SkinBasePid) : null;
            sb.Append(r.name).Append('(').Append(n).Append("材)")
              .Append(" mat=").Append(r.sharedMaterial != null ? r.sharedMaterial.name : "null")
              .Append(" 渲染器级Base=").Append(baseTex != null ? baseTex.name : "<无>")
              .Append(" 逐下标[").Append(detail).Append("]; ");
            ++shown;
        }

        NetSyncLog.SyncLog("载具/外观",
            $"{name} [{tag}] ownerSid={_ownerSid} 车={data.vehicleName} 期望贴图={expect} 渲染器={rs.Length}(特效{fxCount}) {sb}");
    }

    /// <summary>两份载具改装是否等价（只比字段值；null 与 null 视为相同）。</summary>
    private static bool SameCustom(ArchivesData_SO.ArchVehicleData a, ArchivesData_SO.ArchVehicleData b)
    {
        if (a == null || b == null) return a == null && b == null;

        return a.leftWeaponIndex == b.leftWeaponIndex
            && a.rightWeaponIndex == b.rightWeaponIndex
            && a.skinIndex == b.skinIndex
            && a.blendIndex == b.blendIndex
            && Mathf.Approximately(a.blendScale.RawFloat, b.blendScale.RawFloat);
    }

    /// <summary>值快照（不保留引用 —— 本机那份是存档里的活对象）。</summary>
    private static ArchivesData_SO.ArchVehicleData Clone(ArchivesData_SO.ArchVehicleData src)
    {
        if (src == null) return null;
        return new ArchivesData_SO.ArchVehicleData
        {
            leftWeaponIndex = src.leftWeaponIndex,
            rightWeaponIndex = src.rightWeaponIndex,
            skinIndex = src.skinIndex,
            blendIndex = src.blendIndex,
            blendScale = src.blendScale.RawFloat,
        };
    }

    /// <summary>
    /// 把一份载具改装落到模型上（**可重入**：先清掉上一次装的武器，避免重复调用装两份武器）。
    /// <para><paramref name="arch"/> 为 null 时退回本机存档（保持改造前的行为）。</para>
    /// </summary>
    private void ApplyCustom(ArchivesData_SO.ArchVehicleData arch, bool rebuildWeapons = true)
    {
        if (data == null) return;

        // ⚠ 这里**刻意不再**回退本机存档：兜底该不该给已经由 `VehicleCustomState.TryGet` 按"该 sid 有没有同步表"
        //   判过了（见它里面那句 `return false`）。在这里再兜一次 = 把"这台车取不到配置"静默掉，
        //   并且会把本机玩家的外观套到别人的载具上。
        if (arch == null)
        {
            NetSyncLog.SyncLog("载具/外观", $"{name} 无可用的载具配置（sid={_ownerSid} 车={data.vehicleName}）⇒ 保持当前外观");
            return;
        }

        // 防止存档 index 越界
        int rightIdx = Mathf.Clamp(arch.rightWeaponIndex, 0, data.weaponRights.Length - 1);
        int leftIdx = Mathf.Clamp(arch.leftWeaponIndex, 0, data.weaponLefts.Length - 1);
        int skinIdx = Mathf.Clamp(arch.skinIndex, 0, data.Diffs.Length - 1);
        int blendIdx = Mathf.Clamp(arch.blendIndex, 0, data.Blends.Length - 1);

        // ⚠ 只有配置**真的变了**才重建武器：武器模型是"销毁 + 重建"（实测渲染器数 14→18），
        //   纯粹重贴外观时没必要，而且旧 renderer 会留在 mpb 的列表里变悬空。
        if (rebuildWeapons)
        {
            _manager?.ClearWeapons();

            AimIK ik;
            if (data.weaponRights.Length > 0)
            {
                var rightModel = Instantiate(data.weaponRights[rightIdx].go, weaponPointR);
                rightModel.transform.localPosition = Vector3.zero;
                rightModel.transform.localRotation = Quaternion.identity;

                if (rightModel.transform.TryGetComponentInChildren(out ik))
                {
                    ik.solver.target = LookPoint;
                }
                _manager?.AddWeapon(rightModel.GetComponent<WeaponController>(), data.weaponRights[rightIdx].battleIcon);
            }

            if (data.weaponLefts.Length > 0)
            {
                var leftModel = Instantiate(data.weaponLefts[leftIdx].go, weaponPointL);
                leftModel.transform.localPosition = Vector3.zero;
                leftModel.transform.localRotation = Quaternion.identity;
                if (leftModel.transform.TryGetComponentInChildren(out ik))
                {
                    ik.solver.target = LookPoint;
                }
                _manager?.AddWeapon(leftModel.GetComponent<WeaponController>(), data.weaponLefts[leftIdx].battleIcon);
            }

            mpb = new(transform);   // 武器模型是刚重建的 ⇒ 渲染器列表要重新采（旧那份里已销毁的会报"渲染器为空"）
        }
        else if (mpb == null) mpb = new(transform);

        _skinBaseTex = data.Diffs[skinIdx].texture;
        _skinBlendTex = data.Blends[blendIdx].texture;
        _skinBlendScale = arch.blendScale.RawFloat;

        mpb.Set("_BaseMap", _skinBaseTex)
        .Set("_BlendingScale", _skinBlendScale)
        .Set("_BlendingMap", _skinBlendTex).Apply();

        // ★ 同一份皮肤**再写进"受击/溶解的共享槽位块"**（见 FxControllerBase.GetSharedSlotBlock）：
        //   `SetPropertyBlock(block)`（渲染器级）与 `SetPropertyBlock(block, index)`（按下标）是**两套**存储，
        //   受击闪白 Flush 时按下标整块替换，会把上面那份**永久顶掉** —— 实测后果就是
        //   "机甲空投落地摔一下触发闪白，皮肤从此变回默认"。写进同一个块就谁也不盖谁。
        if (!StampSkinToSharedSlots()) _slotStampRetry = 40;   // 槽位表要等 FxControllerBase.Start 才建 ⇒ 低频补盖
    }

    /// <summary>把当前皮肤参数写进"受击/溶解的共享槽位块"。
    /// <returns>false = 还没有槽位表（表现控制器的 <c>Start</c> 还没跑到）⇒ 调用方应稍后重试。</returns>
    /// </summary>
    private bool StampSkinToSharedSlots()
    {
        var fx = GetComponentInParent<FPSGame.AI.FxControllerBase>();
        if (fx == null) return true;   // 没有表现控制器 ⇒ 不会有人按下标 Flush 这些渲染器，收工

        bool any = false;
        var rs = GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < rs.Length; ++i)
        {
            var r = rs[i];
            if (r == null) continue;

            int n = r.sharedMaterials.Length;
            for (int k = 0; k < n; ++k)
            {
                var blk = fx.GetSharedSlotBlock(r, k);
                if (blk == null) continue;   // 该下标不是"生效材质" ⇒ 闪白碰不到它，渲染器级那份够用

                blk.SetTexture(SkinBasePid, _skinBaseTex);
                blk.SetTexture(SkinBlendMapPid, _skinBlendTex);
                blk.SetFloat(SkinBlendScalePid, _skinBlendScale);
                fx.MarkSlotDirty(r, k);
                any = true;
            }
        }
        return any;
    }
}
}
