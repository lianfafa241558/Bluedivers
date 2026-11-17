using FPSGame.Data;
using FPSGame.GameData;
using FPSGame.Utils;
using UnityEngine;

namespace FPSGame.Managers
{
    /// <summary>
    /// 全局画质管理：开战时按存档「环境质量」档位设置地形细节（草/花）渲染距离。
    ///
    /// <para>▍为什么替代原 TerrainDetailQuality 组件：那组件依赖在场景里手动挂（实测无任何场景/prefab 引用 ⇒ 从未生效），
    /// 且 OnEnable 早于战斗世界就绪。本类 <c>[RuntimeInitializeOnLoadMethod]</c> 自举常驻，监听
    /// <see cref="BattleState.OnBattleStart"/>（世界已就绪）再应用，不依赖任何场景配置。</para>
    ///
    /// <para>▍生效范围：只改 <c>Terrain.detailObjectDistance</c>，不动地形网格/LOD。3 种 VertexLit 细节原型 ×
    /// 28 万实例，距离从 100m 降下来时实例数与 <c>DetailInstances.BatchingWithCB</c> / <c>Terrain.Details.Render</c>
    /// 耗时近似线性下降。<c>drawInstanced=true</c> 反而更慢（SetPass 98→162），别开。</para>
    ///
    /// <para>▍跨局：每局 MarkBattleStarted 都触发一次 Apply，读的是当时最新的存档档位；
    /// 设置面板改档后要下一局才生效（这是当前约定的触发口径，不做即时刷新）。</para>
    /// </summary>
    public static class QualityManager
    {
        private const string SettingKey = "环境质量";
        private static readonly float[] DetailDistances = { 40f, 60f, 80f, 100f };

        private static bool _installed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (_installed) return;
            _installed = true;
            BattleState.OnBattleStart += OnBattleStart;
            Apply();
        }

        private static void Apply()
        {

        }

        private static void OnBattleStart()
        {
            ArchivesData_SO archive = ArchivesData_SO.Current;

            int index = Mathf.Clamp((int)archive.GetSetting(SettingKey), 0, DetailDistances.Length - 1);
            float distance = DetailDistances[index];
            TerrainUtils.Main.detailObjectDistance = distance;
        }
    }
}
