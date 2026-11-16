using System;
using FPSGame.Attributes;
using FPSGame.GameData;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FPSGame.Gameplay
{
    /// <summary>
    /// 按存档设置里的「环境质量」档位，动态设置地形细节（草/花）的渲染距离。
    ///
    /// <para>▍为什么值得调：本工程草地是 <b>3 种 VertexLit 细节原型 × 28 万实例</b>，`drawInstanced=false`，
    /// 每帧要走 <c>DetailInstances.BatchingWithCB</c> + <c>Terrain.Details.Render</c>。实测
    /// `detailObjectDistance` 从 100m 降下来，实例数与这两个样本的耗时会近似线性下降
    /// （`drawInstanced=true` 反而更慢：SetPass 98→162，别开）。</para>
    ///
    /// <para>▍生效时机：① 组件启用时读一次存档；② <c>GlobalEventBus.OnSettingCange</c> 里改即时生效
    /// （设置面板拖档位不用重开图）；③ 场景加载完重新套一次 —— 战斗场景是动态生成的，
    /// 地形可能在组件启用之后才进 <c>Terrain.activeTerrains</c>。</para>
    ///
    /// <para>▍⚠ 它只改"细节距离"，不动地形网格/LOD；关掉本组件不会自动还原，
    /// 地形会停在最后一次设置的距离上（这是有意的：离场时不想把画质偷偷调回去）。</para>
    /// </summary>
    [AddComponentMenu("性能/地形细节质量")]
    public class TerrainDetailQuality : MonoBehaviour
    {
        [InspectorName("设置项名")]
        [Tooltip("存档设置里的键名，值取 0..N 作为档位下标")]
        public string SettingKey = "环境质量";

        [InspectorName("各档细节距离(米)")]
        [Tooltip("下标 = 设置值：低/中/高/极高")]
        public float[] Distances = { 40f, 60f, 80f, 100f };

        /// <summary>最近一次真正写下去的距离（场景重载后照它重套）</summary>
        private float _lastDistance = -1f;

        private void OnEnable()
        {
            GlobalEventBus.OnSettingCange += OnSettingCange;
            SceneManager.sceneLoaded += OnSceneLoaded;

            ArchivesData_SO archive = ArchivesData_SO.Current;
            if (archive != null) Apply(archive.GetSetting(SettingKey));
            else if (_lastDistance > 0f) ApplyDistance(_lastDistance);
        }

        private void OnDisable()
        {
            GlobalEventBus.OnSettingCange -= OnSettingCange;
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        private void OnSettingCange(string key, float value)
        {
            if (key != SettingKey) return;   // 事件会广播所有设置项，键比较用全等
            Apply(value);
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            // 新场景的地形可能还没进 activeTerrains，下一帧再套一次
            if (_lastDistance > 0f) Invoke(nameof(Reapply), 0f);
        }

        /// <summary>把最近一次的距离再套一遍（场景切换 / 地形后加载出来后调用）</summary>
        public void Reapply()
        {
            if (_lastDistance > 0f) ApplyDistance(_lastDistance);
        }

        private void Apply(float settingValue)
        {
            if (Distances == null || Distances.Length == 0) return;
            int index = Mathf.Clamp((int)settingValue, 0, Distances.Length - 1);
            ApplyDistance(Distances[index]);
        }

        private void ApplyDistance(float distance)
        {
            _lastDistance = distance;

            Terrain[] terrains = Terrain.activeTerrains;
            for (int i = 0; i < terrains.Length; ++i)
            {
                if (terrains[i] == null) continue;
                terrains[i].detailObjectDistance = distance;
            }
        }

        /// <summary>排查用：当前档位 / 实际写下去的距离 / 场景里几块地形吃到了</summary>
        public string Dump()
        {
            int hit = 0;
            Terrain[] terrains = Terrain.activeTerrains;
            for (int i = 0; i < terrains.Length; ++i) if (terrains[i] != null) hit++;

            var sb = new System.Text.StringBuilder();
            sb.Append("TerrainDetailQuality[键=").Append(SettingKey)
              .Append(" 距离=").Append(_lastDistance.ToString("F0")).Append("m 生效地形=").Append(hit).Append("]");
            for (int i = 0; i < terrains.Length; ++i)
            {
                if (terrains[i] == null) continue;
                sb.Append("  ").Append(terrains[i].name).Append("=")
                  .Append(terrains[i].detailObjectDistance.ToString("F0")).Append("m");
            }
            return sb.ToString();
        }
    }
}
