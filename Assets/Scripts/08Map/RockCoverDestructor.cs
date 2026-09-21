using System.Collections.Generic;
using FPSGame.Attribute;
using UnityEngine;

namespace FpsGame.MapUtils
{
    /// <summary>
    /// 地形覆盖物（巨型悬崖等）管理器。
    /// <para>记录每个已放置实例与它的占地圆，支持按范围清除（挖地形 / 覆盖附加地形时用），</para>
    /// <para>并负责把占地圆同步到 <c>TerrainUtils.AreaCircles</c> 这份纯数据上——清除时一并撤回，
    /// 避免"石头已经没了、别人还在躲它"。</para>
    /// <para>与 <see cref="TreeDestructor"/> / <see cref="TerrainDetailEraser"/> 同款：由
    /// <see cref="GenerateNoiseTerrain"/> 生成地形时自动创建，使用方只调静态 API，无需手动挂载。</para>
    /// </summary>
    [AddComponentMenu("地形/地形覆盖物管理", 42)]
    public class RockCoverDestructor : MonoBehaviour
    {
        #region 单例

        /// <summary>当前场景的实例（地形生成时自动创建）</summary>
        public static RockCoverDestructor Instance { get; private set; }

        #endregion

        #region Inspector

        [Header("运行时状态（只读）")]
        [InspectorName("覆盖物数量")]
        [SerializeField] private int _coverCount;

        #endregion

        #region 运行时数据

        /// <summary>一个已登记的覆盖物：实例 + 占地圆</summary>
        private class Cover
        {
            public GameObject Go;
            public Vector2 Center;
            public float Radius;
        }

        private readonly List<Cover> _covers = new();

        #endregion

        #region 静态 API

        /// <summary>
        /// 为指定地形创建/复用管理器，并清空上一次的登记与纯数据。
        /// <para>由 <see cref="GenerateNoiseTerrain"/> 在放置覆盖物之前调用。</para>
        /// </summary>
        /// <param name="terrain">目标地形，为空时直接返回 null</param>
        /// <returns>管理器实例</returns>
        public static RockCoverDestructor Rebuild(Terrain terrain)
        {
            if (terrain == null) return null;
            if (!terrain.TryGetComponent(out RockCoverDestructor destructor))
            {
                destructor = terrain.gameObject.AddComponent<RockCoverDestructor>();
            }
            destructor.ClearAll();
            return destructor;
        }

        /// <summary>登记一个刚放下的覆盖物，并把它的占地圆注入 <c>TerrainUtils.AreaCircles</c></summary>
        /// <param name="instance">覆盖物实例</param>
        /// <param name="center">世界坐标 XZ 圆心</param>
        /// <param name="radius">占地半径（米），&lt;= 0 会被忽略</param>
        public static void Register(GameObject instance, Vector2 center, float radius)
        {
            Instance?.RegisterInternal(instance, center, radius);
        }

        /// <summary>清除与圆形区域相交的覆盖物（挖地形 / 弹坑用）</summary>
        /// <param name="center">中心点（世界坐标，只取 XZ）</param>
        /// <param name="radius">半径（米）</param>
        /// <returns>清除数量</returns>
        public static int RemoveInRadius(Vector3 center, float radius)
            => Instance != null ? Instance.RemoveInternal(center, radius, Vector2.zero, true) : 0;

        /// <summary>清除与 XZ 轴对齐矩形相交的覆盖物（附加地形覆盖用）</summary>
        /// <param name="center">中心点（世界坐标，只取 XZ）</param>
        /// <param name="halfSize">半尺寸（x = X 方向半宽，y = Z 方向半宽）</param>
        /// <returns>清除数量</returns>
        public static int RemoveInRectXZ(Vector3 center, Vector2 halfSize)
            => Instance != null ? Instance.RemoveInternal(center, 0f, halfSize, false) : 0;

        /// <summary>清除全部覆盖物与纯数据（重新生成地形前，一般由 <see cref="Rebuild"/> 代劳）</summary>
        public static void Clear()
        {
            if (Instance != null) Instance.ClearAll();
        }

        #endregion

        #region 生命周期

        private void Awake()
        {
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        #endregion

        #region 内部实现

        private void RegisterInternal(GameObject instance, Vector2 center, float radius)
        {
            if (radius <= 0f) return;

            _covers.Add(new Cover { Go = instance, Center = center, Radius = radius });
            _coverCount = _covers.Count;
            TerrainUtils.AddAreaCircle(center, radius);
        }

        /// <summary>清空登记表、销毁已登记的实例，并撤回纯数据</summary>
        private void ClearAll()
        {
            for (int i = 0; i < _covers.Count; i++)
            {
                DestroyInstance(_covers[i].Go);
            }
            _covers.Clear();
            _coverCount = 0;
            TerrainUtils.ClearAreaCircles();
        }

        /// <summary>清除与区域相交的覆盖物（圆形 / 轴对齐矩形）</summary>
        private int RemoveInternal(Vector3 center, float radius, Vector2 halfSize, bool isCircle)
        {
            int removed = 0;
            for (int i = _covers.Count - 1; i >= 0; i--)
            {
                Cover cover = _covers[i];
                bool hit = isCircle
                    ? IsOverlapCircle(cover, center.x, center.z, radius)
                    : IsOverlapRect(cover, center.x, center.z, halfSize);
                if (!hit) continue;

                DestroyInstance(cover.Go);
                _covers.RemoveAt(i);
                removed++;
            }

            if (removed <= 0) return 0;

            // 纯数据同步：撤销被清掉的那些圆，避免后续布点还躲着已经不存在的石头
            SyncAreaCircles();
            _coverCount = _covers.Count;
            Debug.Log($"[地形覆盖] 清除 {removed} 处（剩余 {_covers.Count}）");
            return removed;
        }

        /// <summary>两个圆是否相交：占地圆是保守外接圆，相交即视为"压到了"</summary>
        private static bool IsOverlapCircle(Cover cover, float x, float z, float radius)
        {
            float sumRadius = radius + cover.Radius;
            float dx = cover.Center.x - x;
            float dz = cover.Center.y - z;
            return dx * dx + dz * dz <= sumRadius * sumRadius;
        }

        /// <summary>圆（覆盖物的占地圆）与轴对齐矩形是否相交</summary>
        private static bool IsOverlapRect(Cover cover, float x, float z, Vector2 halfSize)
        {
            float dx = Mathf.Max(Mathf.Abs(cover.Center.x - x) - halfSize.x, 0f);
            float dz = Mathf.Max(Mathf.Abs(cover.Center.y - z) - halfSize.y, 0f);
            return dx * dx + dz * dz <= cover.Radius * cover.Radius;
        }

        /// <summary>把仍然存活的覆盖物重新注入纯数据列表（清除后调用）</summary>
        private void SyncAreaCircles()
        {
            TerrainUtils.ClearAreaCircles();
            for (int i = 0; i < _covers.Count; i++)
            {
                TerrainUtils.AddAreaCircle(_covers[i].Center, _covers[i].Radius);
            }
        }

        /// <summary>销毁实例：编辑器里（非运行期）用 DestroyImmediate，运行期用 Destroy</summary>
        private static void DestroyInstance(GameObject go)
        {
            if (go == null) return;
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                DestroyImmediate(go);
                return;
            }
#endif
            Destroy(go);
        }

        #endregion
    }
}
