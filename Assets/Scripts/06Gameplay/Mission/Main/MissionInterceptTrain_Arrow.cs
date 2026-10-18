using System.Collections.Generic;
using FPSGame.Attributes;
using FPSGame.Utils;
using UnityEngine;

namespace FPSGame.Mission
{
    /// <summary>
    /// 拦截火车 · 轨道方向箭头（partial）：铺轨时在**每两节 railPrefab 的接缝**处放一个箭头并缓存，
    /// 发车时把这条轨道上的箭头点亮（MPB 改 <c>_EmittIntensity</c>），这趟车收尾后再熄灭。
    /// <para>▍为什么用 MPB 而不是改 Material：箭头共用同一个材质资产，改材质会把"点亮"传染给所有轨道；
    /// 也不能给每个箭头 new 一份材质（白白多一批材质实例）。</para>
    /// <para>⚠ 熄灭不能"谁结束就关"：同一条轨道上可能同时跑两列车（发车间隔 &lt; 跑完全程）
    /// ⇒ 一律按"这条轨道上还有没有活着的火车"重算，见 <see cref="RefreshTrackArrows"/>。</para>
    /// </summary>
    public partial class MissionInterceptTrain
    {
        [Foldout("轨道", true)]
        [InspectorName("方向箭头预制体")]
        [SerializeField] private GameObject trainArrowPrefab;

        /// <summary>箭头沿**自身法线**（局部 +Y）抬高：贴完轨面再抬这么多，免得埋进道砟 / 轨枕</summary>
        private const float ArrowLift = 1f;

        /// <summary>自发光强度属性名（<c>Custom/EmittBlend</c> 的 <c>_EmittIntensity</c>）</summary>
        private static readonly int EmittIntensityId = Shader.PropertyToID("_EmittIntensity");

        /// <summary>一条轨道上的全部箭头（按轨道整体点亮 / 熄灭）</summary>
        private sealed class TrackArrowSet
        {
            public readonly List<GameObject> Objects = new();
            /// <summary>提前缓存渲染器：发车 / 收尾时只写属性块，不做 GetComponent</summary>
            public readonly List<Renderer> Renderers = new();
        }

        /// <summary>轨道 ⇒ 该轨道上的箭头（键用 <see cref="RailTrack"/> 实例：发车时刻表里的下标与 <c>tracks</c> 不完全等价）</summary>
        private readonly Dictionary<RailTrack, TrackArrowSet> arrowsByTrack = new();

        /// <summary>
        /// 写属性块用的缓存（避免每次 SetPropertyBlock 都 new）。
        /// <para>⚠ 不能写成字段初始化器 `= new()`：<c>MaterialPropertyBlock</c> 是 Unity 原生对象，
        /// 在 MonoBehaviour 的构造期（含字段初始化器）创建会抛 <c>CreateImpl is not allowed…</c>（2026-10-18 实测）。</para>
        /// </summary>
        private MaterialPropertyBlock arrowMpb;


        /// <summary>
        /// 在一个接缝处放一个方向箭头（由 <c>FinishTrack</c> 铺完一节后调用）。
        /// <para>落点 / 朝向口径与铺轨一致：接缝的 XZ + 该节的路基高程；朝向 = 该节前向（含纵坡俯仰）
        /// × 预制体长轴修正（<see cref="RailMetrics.YawOffset"/>，箭头长轴在 Z ⇒ 0）。</para>
        /// </summary>
        /// <param name="joint">接缝序号（= 后一节的序号，1 基 = 第几个接缝）</param>
        /// <param name="pointXZ">接缝的水平位置</param>
        /// <param name="y">接缝处该节的轨面高度（与轨道节同一套算法）</param>
        private void SpawnTrackArrow(RailTrack track, int joint, Vector2 pointXZ, float y, Vector3 forward,
            GameObject prefab, RailMetrics metrics)
        {
            Quaternion rot = Quaternion.LookRotation(forward) * Quaternion.Euler(0f, metrics.YawOffset, 0f);
            // 抬升沿箭头**自身的法线**（局部 +Y = rot * up）：斜轨上抬的是倾斜的法线，不是世界 Y
            // ⚠ 这里必须写 new Vector3(...)：目标类型推断的 new(...) 不能直接当 + 的左操作数（CS8310）
            Vector3 pos = new Vector3(pointXZ.x, y - metrics.PivotToBottom, pointXZ.y) + rot * Vector3.up * ArrowLift;
            // ⚠ 必须用带 parent 的 Instantiate：预制体根上残留着建模时的 (0,0.64,-12)、还带一点手工斜置
            GameObject arrow = Instantiate(prefab, pos, rot, transform);
            arrow.name = $"TrainArrow_{track.Index + 1}_{joint}";

            if (!arrowsByTrack.TryGetValue(track, out TrackArrowSet set))
            {
                set = new TrackArrowSet();
                arrowsByTrack[track] = set;
            }
            set.Objects.Add(arrow);
            set.Renderers.AddRange(arrow.GetComponentsInChildren<Renderer>(true));
        }

        /// <summary>一条轨道的箭头放完：先全灭（材质资产里的强度默认是亮的）</summary>
        private void SealTrackArrows(RailTrack track)
        {
            if (!arrowsByTrack.TryGetValue(track, out TrackArrowSet set)) return;
            SetArrowIntensity(set, 0f);
            NetSyncLog.LoadLog("任务", $"[拦截火车] 轨道{track.Index + 1} 方向箭头 {set.Objects.Count} 个（接缝数）");
        }

        /// <summary>按"这条轨道上还有没有活着的火车"决定箭头亮（1）/ 灭（0）</summary>
        private void RefreshTrackArrows(RailTrack track)
        {
            if (track == null || !arrowsByTrack.TryGetValue(track, out TrackArrowSet set)) return;
            bool lit = false;
            for (int i = 0; i < liveTrains.Count; i++)
            {
                TrainRuntime runtime = liveTrains[i];
                if (runtime.Unit != null && runtime.Unit.Track == track)
                {
                    lit = true;
                    break;
                }
            }
            SetArrowIntensity(set, lit ? 1f : 0f);
            NetSyncLog.LoadLog("任务", $"[拦截火车] 轨道{track.Index + 1} 箭头{(lit ? "点亮" : "熄灭")}（{set.Objects.Count} 个）");
        }

        /// <summary>给一组箭头写自发光强度。<c>GetPropertyBlock</c> 先取再改，别把别的系统写在块里的属性清掉</summary>
        private void SetArrowIntensity(TrackArrowSet set, float value)
        {
            if (arrowMpb == null) arrowMpb = new MaterialPropertyBlock();   // 懒创建（见字段注释）
            for (int i = 0; i < set.Renderers.Count; i++)
            {
                Renderer renderer = set.Renderers[i];
                if (renderer == null) continue;
                renderer.GetPropertyBlock(arrowMpb);
                arrowMpb.SetFloat(EmittIntensityId, value);
                renderer.SetPropertyBlock(arrowMpb);
            }
        }


    }
}
