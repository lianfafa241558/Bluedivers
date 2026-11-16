// ============================================================================
// 【已归档 · 2026-11-16】虚拟碰撞体（VirtualCollider）Step 1 原型 —— 整份注释掉，不参与编译
//
// 结论：不采用。Play 实测（299 Actor / 863 部位碰撞体）Physics.SyncColliderTransform
//       ≈ 0.000~0.013 ms/帧，不是瓶颈；而数学求交单条查询比物理 SphereCast 慢 2~30 倍
//       （UnitQuery 粗筛 6.8~22 µs vs 物理 0.73 µs）⇒ 替代后是净亏。
// 恢复：删掉本段与文件末尾的 */ 即可（细节见 .codebuddy/memory/topics/virtual-collider.md）
// ============================================================================
/*
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

using FPSGame.Game;
using FPSGame.GameContract;
using PEMaths;

using UnityEditor;
using UnityEngine;

using Debug = UnityEngine.Debug;

namespace FPSGame.EditorExt
{
    /// <summary>
    /// 虚拟碰撞体 · Step 1 原型基准（只测量，不接管主流程）。
    ///
    /// <para>▍要回答两个问题：</para>
    /// <para>① **贵不贵**：同一条射线，"Physics.SphereCast vs UnitQuery+逐肢体求交"各花多少 µs；</para>
    /// <para>② **准不准**：两种算法给出的"打中哪个部位"有多少条不一致（部位错 / 单位错 / 单边命中）。</para>
    ///
    /// <para>⚠ 必须在 **Play 模式**下用：<c>Actor.Damageables</c> 是 <c>Awake</c> 里采集的，
    /// 编辑态下为空 ⇒ 一个部件都烘焙不出来。</para>
    /// </summary>
    public class VirtualColliderBenchWindow : EditorWindow
    {
        /// <summary>一条采样射线（起点 / 单位方向 / 最大距离）</summary>
        private struct RaySample
        {
            public Vector3 Origin;
            public Vector3 Dir;
            public float Dist;
        }

        private const int HitBufferSize = 32;

        // ===== 参数 =====
        [SerializeField] private int _rayCount = 1000;
        [SerializeField] private float _bulletRadius = 0.05f;
        [SerializeField] private float _aimJitter = 0.25f;
        /// <summary>单帧位移长度：真实子弹每帧只扫过这么长，不是"一路射到目标"的 40m 长线</summary>
        [SerializeField] private float _stepLength = 1.5f;
        [SerializeField] private int _rounds = 3;
        [SerializeField] private int _bulletsPerFrame = 30;

        /// <summary>外部实测的 Physics.SyncColliderTransform 单帧耗时，用于算净收益（默认取 2026-11-16 采样值）</summary>
        [SerializeField] private float _syncColliderTransformMs = 0.83f;

        [SerializeField] private bool _useUnitQueryScreen;

        // ===== 状态 =====
        private List<Actor> _actors;
        private VirtualUnitCollider[] _units;
        private RaySample[] _rays;

        private int[] _physUnit;
        private int[] _physPart;
        private int[] _mathUnit;
        private int[] _mathPart;

        // 懒刷新用：每条单位上次刷新世界坐标是在哪一"帧"
        private int[] _stamp;
        private int _frameId;

        /// <summary>按帧模拟出来的"每帧数学成本"（ms），比线性外推可信</summary>
        private double perFrameMathMs = double.MaxValue;

        private readonly Dictionary<int, int> _colliderUnit = new();
        private readonly Dictionary<int, int> _colliderPart = new();

        // UnitQuery 返回的是 IActor：反查下标不能线性扫（224 单位 × 每次候选 = 一条射线几千次比较）
        private readonly Dictionary<int, int> _unitByActorId = new();

        private readonly RaycastHit[] _hitBuf = new RaycastHit[HitBufferSize];
        private readonly List<IActor> _screenBuf = new(64);
        private TargetCfg _screenCfg = new();
        private PECircle _screenCircle;

        private string _report = "（尚未运行）";
        private Vector2 _scroll;
        private int _unitMask;
        private bool _maskFallback;

        [MenuItem("Tools/虚拟碰撞体基准测试")]
        private static void Open()
        {
            GetWindow<VirtualColliderBenchWindow>("虚拟碰撞体基准");
        }

        /// <summary>
        /// 无界面跑一遍完整基准（给 MCP / 批处理调用），返回报告文本。
        /// <para>⚠ 采集的是**当前打开的场景**的所有 Actor —— 跑之前请先打开战斗场景。</para>
        /// </summary>
        public static string RunHeadless(int rayCount, float bulletRadius, float aimJitter, int rounds,
            int bulletsPerFrame, bool useUnitQueryScreen = false, float stepLength = 1.5f)
        {
            VirtualColliderBenchWindow w = CreateInstance<VirtualColliderBenchWindow>();
            try
            {
                w._rayCount = Mathf.Clamp(rayCount, 10, 20000);
                w._bulletRadius = bulletRadius;
                w._aimJitter = aimJitter;
                w._stepLength = Mathf.Clamp(stepLength, 0.1f, 20f);
                w._rounds = Mathf.Clamp(rounds, 1, 10);
                w._bulletsPerFrame = Mathf.Clamp(bulletsPerFrame, 1, 300);
                w._useUnitQueryScreen = useUnitQueryScreen;

                w.CollectUnits();
                string collect = w._report;
                w.GenRays();
                w.RunBench();
                return collect + "\n\n" + w._report;
            }
            finally
            {
                DestroyImmediate(w);
            }
        }

        private void OnEnable()
        {
            minSize = new Vector2(560f, 480f);
        }

        // ============================================================
        // GUI
        // ============================================================
        private void OnGUI()
        {
            DrawToolbar();
            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox(
                    "当前不在 Play 模式：部位表退化为层级序（与 Awake 同序），可以扫场景静态摆位，" +
                    "但动画静止、单位不会动 —— 作为上界够用，真实战斗请进 Play 再跑一次。",
                    MessageType.Info);
            }

            EditorGUILayout.BeginVertical(GUI.skin.box);
            EditorGUILayout.LabelField("参数", EditorStyles.boldLabel);
            _rayCount = EditorGUILayout.IntSlider("采样射线数", _rayCount, 10, 20000);
            _bulletRadius = EditorGUILayout.Slider("弹体半径 (m)", _bulletRadius, 0f, 0.5f);
            _aimJitter = EditorGUILayout.Slider("瞄准抖动（越大越多擦边/脱靶）", _aimJitter, 0f, 1f);
            _stepLength = EditorGUILayout.Slider("单帧位移 (m)", _stepLength, 0.1f, 20f);
            _rounds = EditorGUILayout.IntSlider("计时回合数（取最快）", _rounds, 1, 10);
            _bulletsPerFrame = EditorGUILayout.IntSlider("每帧子弹数（外推用）", _bulletsPerFrame, 1, 300);
            _syncColliderTransformMs = EditorGUILayout.FloatField("Physics.SyncColliderTransform (ms，外部实测)",
                _syncColliderTransformMs);
            _useUnitQueryScreen = EditorGUILayout.ToggleLeft(
                "粗筛用 UnitQuery 网格（不勾 = 全表包围球，结果更保守）", _useUnitQueryScreen);
            EditorGUILayout.EndVertical();

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("1) 采集场景单位", GUILayout.Height(26))) CollectUnits();
            using (new EditorGUI.DisabledScope(_units == null || _units.Length == 0))
            {
                if (GUILayout.Button("2) 生成采样射线", GUILayout.Height(26))) GenRays();
            }
            using (new EditorGUI.DisabledScope(_rays == null || _rays.Length == 0))
            {
                if (GUILayout.Button("3) 跑基准", GUILayout.Height(26))) RunBench();
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.LabelField(
                $"单位={(_units?.Length ?? 0)}  射线={(_rays?.Length ?? 0)}  " +
                $"命中层mask=0x{_unitMask:X}{(_maskFallback ? "（静态值未注入，用 Default|Ground|Unit 兜底）" : "")}  " +
                $"UnitQuery={(UnitQuery.IsReady ? "已接管" : "未接管")}",
                EditorStyles.miniLabel);

            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            EditorGUILayout.TextArea(_report, GUILayout.ExpandHeight(true));
            EditorGUILayout.EndScrollView();
        }

        private void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            GUILayout.Label("虚拟碰撞体 Step 1 · 耗时与部位一致性", EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("复制报告", EditorStyles.toolbarButton, GUILayout.Width(70)))
            {
                EditorGUIUtility.systemCopyBuffer = _report;
            }
            EditorGUILayout.EndHorizontal();
        }

        // ============================================================
        // 1) 采集
        // ============================================================
        /// <summary>
        /// 物理侧与虚拟侧必须用同一个 mask，否则会出现"数学命中、物理看不见"的幽灵命中。
        /// <para>⚠ 用 <c>HittableLayers</c> 而不是 <c>UnitLayers</c>：肢体碰撞体有一半挂在 **Default(0)** 层，
        /// 而 `groundLayers = 9`（bit0 Default + bit3 Ground）⇒ 真正的子弹命中集是 `0x49 = Default|Ground|Unit`。
        /// 只按 `UnitLayers(0x40)` 筛会把那一半肢体当成"物理看不见"，测出 50%+ 的假幽灵命中。</para>
        /// </summary>
        private void ResolveUnitMask()
        {
            _unitMask = LayerDefinition.HittableLayers.value;
            _maskFallback = _unitMask == 0;
            // 非战斗场景里 LayerConfigInitializer 没跑过（静态值全 0）⇒ 用 GameRoot.prefab 的实际配置兜底
            if (_maskFallback) _unitMask = (1 << 0) | (1 << 3) | (1 << 6);
        }

        private void CollectUnits()
        {
            var sw = Stopwatch.StartNew();
            ResolveUnitMask();

            _actors = new List<Actor>(FindObjectsOfType<Actor>(true));
            _colliderUnit.Clear();
            _colliderPart.Clear();
            _unitByActorId.Clear();

            var baked = new List<VirtualUnitCollider>(_actors.Count);
            int filtered = 0;
            for (int i = 0; i < _actors.Count; ++i)
            {
                VirtualUnitCollider vuc = VirtualUnitCollider.Bake(_actors[i], _unitMask);
                filtered += VirtualUnitCollider.LastBakeFilteredCount;
                if (vuc == null) continue;

                int unitIndex = baked.Count;
                baked.Add(vuc);
                _unitByActorId[_actors[i].GetInstanceID()] = unitIndex;

                for (int l = 0; l < vuc.Limbs.Length; ++l)
                {
                    Collider src = vuc.Limbs[l].Source;
                    if (src == null) continue;
                    int id = src.GetInstanceID();
                    _colliderUnit[id] = unitIndex;
                    _colliderPart[id] = vuc.Limbs[l].PartIndex;
                }
            }

            _units = baked.ToArray();
            _rays = null;
            sw.Stop();

            int totalLimbs = 0, maxLimbs = 0, zeroPart = 0;
            for (int i = 0; i < _units.Length; ++i)
            {
                int n = _units[i].Limbs.Length;
                totalLimbs += n;
                if (n > maxLimbs) maxLimbs = n;
                for (int l = 0; l < n; ++l)
                {
                    if (_units[i].Limbs[l].PartIndex < 0) zeroPart++;
                }
            }

            _report =
                $"【采集】Actor {_actors.Count} 个，可命中 {_units.Length} 个，肢体代理 {totalLimbs} 个" +
                $"（avg {( _units.Length > 0 ? totalLimbs / (float)_units.Length : 0f):F1} / max {maxLimbs}），" +
                $"部位下标缺失 {zeroPart} 个，层级/启用过滤掉 {filtered} 个，" +
                $"一次性烘焙 {sw.Elapsed.TotalMilliseconds:F1} ms  [mask=0x{_unitMask:X}]";
        }

        // ============================================================
        // 2) 采样射线
        // ============================================================
        private void GenRays()
        {
            if (_units == null || _units.Length == 0) return;

            _rays = new RaySample[_rayCount];
            float totalLimbs = 0f;
            for (int i = 0; i < _units.Length; ++i) totalLimbs += _units[i].Limbs.Length;

            for (int i = 0; i < _rays.Length; ++i)
            {
                // 按肢体数量加权挑单位：多肢体的大单位被扫到的概率本来就更高
                VirtualUnitCollider unit = PickWeightedUnit(totalLimbs);
                int limb = UnityEngine.Random.Range(0, unit.Limbs.Length);

                unit.Limbs[limb].GetWorldPose(out Vector3 a, out Vector3 b, out _);
                Vector3 aim = Vector3.Lerp(a, b, UnityEngine.Random.value);

                // 抖动瞄准点（不是方向）：让一部分弹帧整个脱靶，否则命中率永远 100%，测不出分歧
                if (_aimJitter > 0f)
                {
                    aim += UnityEngine.Random.insideUnitSphere * _aimJitter;
                }

                // 单帧位移只有 stepLength 这么长，线段以瞄准点为中心 —— 这与
                // ProjectileStandard.TryHit（m_LastRootPosition → Tip）的扫掠段同量级
                Vector3 dir = UnityEngine.Random.onUnitSphere;
                Vector3 from = aim - dir * (_stepLength * 0.5f);

                _rays[i] = new RaySample
                {
                    Origin = from,
                    Dir = dir,
                    Dist = _stepLength,
                };
            }

            _report = $"【采样】{_rays.Length} 条射线已生成（瞄准点 = 随机肢体轴上的随机点 + ±{_aimJitter:F2}m 抖动，" +
                      $"单帧位移 {_stepLength:F2}m）";
        }

        private VirtualUnitCollider PickWeightedUnit(float totalLimbs)
        {
            float pick = UnityEngine.Random.value * totalLimbs;
            float acc = 0f;
            for (int i = 0; i < _units.Length; ++i)
            {
                acc += _units[i].Limbs.Length;
                if (pick <= acc) return _units[i];
            }
            return _units[_units.Length - 1];
        }

        // ============================================================
        // 3) 基准
        // ============================================================
        private void RunBench()
        {
            if (_units == null || _units.Length == 0 || _rays == null || _rays.Length == 0) return;

            Physics.SyncTransforms();

            int n = _rays.Length;
            _physUnit = new int[n];
            _physPart = new int[n];
            _mathUnit = new int[n];
            _mathPart = new int[n];

            // ===== 计时 =====
            double physMs = double.MaxValue;
            double mathMs = double.MaxValue;
            double worldMs = double.MaxValue;

            _stamp = new int[_units.Length];

            for (int r = 0; r < _rounds; ++r)
            {
                // --- 物理侧 ---
                var sw = Stopwatch.StartNew();
                for (int i = 0; i < n; ++i) EvalPhysics(i);
                sw.Stop();
                if (sw.Elapsed.TotalMilliseconds < physMs) physMs = sw.Elapsed.TotalMilliseconds;

                // --- 全量刷新一遍（只作对照：这是"每帧把所有单位都算一遍"的成本） ---
                sw.Restart();
                for (int u = 0; u < _units.Length; ++u) _units[u].UpdateWorld();
                sw.Stop();
                if (sw.Elapsed.TotalMilliseconds < worldMs) worldMs = sw.Elapsed.TotalMilliseconds;

                // --- 数学侧：懒刷新（只有真的被某颗子弹扫到的单位才刷世界坐标） ---
                _frameId++;
                for (int u = 0; u < _stamp.Length; ++u) _stamp[u] = -1;

                sw.Restart();
                for (int i = 0; i < n; ++i) EvalMath(i);
                sw.Stop();
                if (sw.Elapsed.TotalMilliseconds < mathMs) mathMs = sw.Elapsed.TotalMilliseconds;

                // --- 按帧模拟：每帧只打 _bulletsPerFrame 发，帧内共享一次世界坐标刷新 ---
                // ⚠ 一次性跑 1000 条会把"懒刷新"摊平（每发都摊到全量刷新），必须按帧切才测得准
                int frames = n / _bulletsPerFrame;
                sw.Restart();
                for (int f = 0; f < frames; ++f)
                {
                    _frameId++;
                    for (int u = 0; u < _stamp.Length; ++u) _stamp[u] = -1;

                    int begin = f * _bulletsPerFrame;
                    for (int i = begin; i < begin + _bulletsPerFrame; ++i) EvalMath(i);
                }
                sw.Stop();
                if (frames > 0)
                {
                    double perFrame = sw.Elapsed.TotalMilliseconds / frames;
                    if (perFrame < perFrameMathMs) perFrameMathMs = perFrame;
                }
            }

            _report = BuildReport(physMs, mathMs, worldMs);
        }

        private void EvalPhysics(int i)
        {
            RaySample ray = _rays[i];
            _physUnit[i] = -1;
            _physPart[i] = -1;

            int count = Physics.SphereCastNonAlloc(ray.Origin, _bulletRadius, ray.Dir, _hitBuf, ray.Dist,
                _unitMask, QueryTriggerInteraction.Collide);

            float best = float.MaxValue;
            for (int h = 0; h < count; ++h)
            {
                RaycastHit hit = _hitBuf[h];
                int id = hit.collider.GetInstanceID();
                if (!_colliderUnit.TryGetValue(id, out int unit)) continue;
                if (hit.distance >= best) continue;

                best = hit.distance;
                _physUnit[i] = unit;
                _physPart[i] = _colliderPart[id];
            }
        }

        private void EvalMath(int i)
        {
            RaySample ray = _rays[i];
            _mathUnit[i] = -1;
            _mathPart[i] = -1;

            Vector3 to = ray.Origin + ray.Dir * ray.Dist;

            if (_useUnitQueryScreen && UnitQuery.IsReady)
            {
                // 网格粗筛：用一个覆盖整条线段的圆（半径 = 半长 + 余量）
                Vector3 mid = (ray.Origin + to) * 0.5f;
                _screenCircle = new PECircle((PEVector2)mid, (PEInt)(ray.Dist * 0.5f + 4f));
                UnitQuery.FindUnits(_screenCircle, _screenCfg, null, _screenBuf);

                float bestT = float.MaxValue;
                for (int k = 0; k < _screenBuf.Count; ++k)
                {
                    int unit = IndexOfUnit(_screenBuf[k]);
                    if (unit < 0) continue;
                    TryMathUnit(unit, ray.Origin, to, i, ref bestT);
                }
                return;
            }

            // 全表 + 包围球粗筛（网格不可用时的上界模型）
            float best = float.MaxValue;
            for (int u = 0; u < _units.Length; ++u)
            {
                TryMathUnit(u, ray.Origin, to, i, ref best);
            }
        }

        private void TryMathUnit(int unit, Vector3 from, Vector3 to, int rayIndex, ref float bestT)
        {
            VirtualUnitCollider vuc = _units[unit];

            // 粗筛用包围球（只碰 Actor 位置 + 烘焙半径），过了才值得付"刷新世界坐标"的钱
            if (!vuc.OverlapsSegment(from, to)) return;

            if (_stamp[unit] != _frameId)
            {
                vuc.UpdateWorld();
                _stamp[unit] = _frameId;
            }

            if (!vuc.TryHit(from, to, _bulletRadius, out int limb, out float t, out _)) return;
            if (t >= bestT) return;

            bestT = t;
            _mathUnit[rayIndex] = unit;
            _mathPart[rayIndex] = vuc.Limbs[limb].PartIndex;
        }

        private int IndexOfUnit(IActor actor)
        {
            if (actor == null) return -1;
            Component c = actor as Component;
            if (c == null) return -1;
            return _unitByActorId.TryGetValue(c.GetInstanceID(), out int idx) ? idx : -1;
        }

        // ============================================================
        // 报告
        // ============================================================
        private string BuildReport(double physMs, double mathMs, double worldMs)
        {
            int n = _rays.Length;
            int samePart = 0, diffPart = 0, diffUnit = 0, physOnly = 0, mathOnly = 0, bothMiss = 0;

            for (int i = 0; i < n; ++i)
            {
                bool ph = _physUnit[i] >= 0;
                bool ma = _mathUnit[i] >= 0;

                if (!ph && !ma) { bothMiss++; continue; }
                if (ph && !ma) { physOnly++; continue; }
                if (!ph && ma) { mathOnly++; continue; }

                if (_physUnit[i] != _mathUnit[i]) { diffUnit++; continue; }
                if (_physPart[i] != _mathPart[i]) { diffPart++; continue; }
                samePart++;
            }

            double physUs = physMs * 1000.0 / n;
            double mathUs = mathMs * 1000.0 / n;              // 已含"懒刷新"世界坐标
            double physTotalMs = physUs * _bulletsPerFrame / 1000.0;
            // 优先用"按帧模拟"的实测值；没跑出有效帧时才退回线性外推
            double mathTotalMs = perFrameMathMs < double.MaxValue
                ? perFrameMathMs
                : mathUs * _bulletsPerFrame / 1000.0;
            double netMs = (_syncColliderTransformMs + physTotalMs) - mathTotalMs;

            int totalLimbs = 0, touched = 0;
            for (int i = 0; i < _units.Length; ++i) totalLimbs += _units[i].Limbs.Length;
            for (int i = 0; i < _stamp.Length; ++i) if (_stamp[i] == _frameId) touched++;

            var sb = new StringBuilder(2048);
            sb.AppendLine("======== 虚拟碰撞体 Step 1 基准 ========");
            sb.AppendLine($"单位 {_units.Length} 个 / 肢体代理 {totalLimbs} 个 / 射线 {n} 条 / 弹体半径 {_bulletRadius:F3}m / " +
                          $"单帧位移 {_stepLength:F2}m");
            sb.AppendLine($"粗筛方式：{(_useUnitQueryScreen && UnitQuery.IsReady ? "UnitQuery 网格" : "全表包围球（保守上界）")}");
            sb.AppendLine();

            sb.AppendLine("-- 命中分布 --");
            AppendLine(sb, "部位一致", samePart, n);
            AppendLine(sb, "同单位·部位不一致", diffPart, n);
            AppendLine(sb, "不同单位", diffUnit, n);
            AppendLine(sb, "仅物理命中", physOnly, n);
            AppendLine(sb, "仅数学命中", mathOnly, n);
            AppendLine(sb, "两边都没中", bothMiss, n);
            int hitTotal = samePart + diffPart + diffUnit + physOnly + mathOnly;
            int agree = samePart;
            sb.AppendLine($"一致率（同一部位）：{(hitTotal > 0 ? agree * 100.0 / hitTotal : 0):F1}%  " +
                          $"（分母 = 至少一边命中的 {hitTotal} 条）");
            sb.AppendLine();

            sb.AppendLine("-- 耗时 --");
            sb.AppendLine($"物理 SphereCast        {physMs,9:F3} ms / {n} 条  ⇒  {physUs,7:F2} µs/条");
            sb.AppendLine($"数学 求交+懒刷新       {mathMs,9:F3} ms / {n} 条  ⇒  {mathUs,7:F2} µs/条" +
                          $"   （本次被扫到并刷新的单位 {touched}/{_units.Length} 个）");
            sb.AppendLine($"　 对照：全量刷新一次  {worldMs,9:F3} ms / {_units.Length} 单位  ⇒  " +
                          $"{( _units.Length > 0 ? worldMs * 1000.0 / _units.Length : 0):F2} µs/单位" +
                          $"（{(_units.Length > 0 ? worldMs * 1000.0 / Mathf.Max(1, totalLimbs) : 0):F3} µs/肢体）");
            sb.AppendLine();

            sb.AppendLine($"-- 每帧成本（{_bulletsPerFrame} 颗子弹，按帧切分实测） --");
            sb.AppendLine($"现状：SyncColliderTransform {_syncColliderTransformMs:F2} ms + 物理查询 {physTotalMs:F3} ms " +
                          $"= {_syncColliderTransformMs + physTotalMs:F3} ms/帧");
            sb.AppendLine($"虚拟化：数学求交(含懒刷新) {mathTotalMs:F3} ms/帧" +
                          (perFrameMathMs < double.MaxValue ? "" : "（无有效帧，退回线性外推）"));
            sb.AppendLine($"净收益：{netMs:+0.000;-0.000} ms/帧  " +
                          $"（{(netMs >= 0 ? "虚拟化更省" : "虚拟化更贵")}；SyncColliderTransform 由外部实测填入，肢体 collider 全删才是这个数）");
            sb.AppendLine();
            sb.AppendLine("⚠ 物理侧按「预先建好 collider→部位表」计时；生产链路每次命中还要 GetComponent，" +
                          "所以这里的物理耗时会偏低一点。");
            sb.AppendLine("⚠ 懒刷新的成本与「被扫到的单位数」成正比：单位越密、弹体半径越大，数学侧越贵。");

            return sb.ToString();
        }

        private static void AppendLine(StringBuilder sb, string label, int count, int total)
        {
            sb.AppendLine($"  {label,-18}{count,7}  ({count * 100.0 / total,5:F1}%)");
        }
    }
}
*/
