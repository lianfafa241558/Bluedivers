using UnityEngine;
using UnityEngine.AI;
using Utils;
namespace FpsGame.MapUtils
{

    public class MapRoot : MonoBehaviour
    {
        //[Foldout("地形设置", true)]
        public Terrain terrain;
        [Header("里面的位置是指起点，不是指中心！")]
        public BoundsInt rect;
        [Header("空气墙")]
        public GameObject wallPrefab;



        private void Awake()
        {
            TerrainUtils.Main = terrain;
            GenerateTerrainRect();
            CreatAirWall();
        }

        private void OnDestroy()
        {
            TerrainUtils.Main = null;
        }

        /// <summary>
        /// 生成Rect并且同步到NavMeshSurface
        /// </summary>
        [ContextMenu("生成Rect")]
        public void GenerateTerrainRect()
        {
            var locpos = terrain.GetPosition();
            locpos.y = -Constants.MapBorder / 2;
            var size = terrain.terrainData.size;
            rect = new((locpos + Vector3.one * +Constants.MapBorder / 2).ToInt(), new Vector3Int((int)(size.x - Constants.MapBorder), (int)size.y, (int)(size.z - Constants.MapBorder)));
            var sur = GetComponent<NavMeshSurface>();
            sur.size = size;
            sur.center = rect.center;


        }

        public void CreatAirWall()
        {
            float radius = rect.size.x / 2/* * EffectiveRange*/;
            float perimeter = radius * 3.1416f / 9f;//2Pi*r/36
            for (int i = 0; i < 360; i += 20)
            {
                var go = Instantiate(wallPrefab, terrain.transform).transform;
                go.gameObject.name = "airWall" + (i);
                go.position = rect.center + (Quaternion.Euler(0, i, 0) * Vector3.forward) * radius - (rect.center.y + 10) * Vector3.up;
                go.eulerAngles = new(0, i, 0);
                go.localScale = perimeter / 8.34f * Vector3.one;

            }

        }

        void OnDrawGizmosSelected()
        {
            //碰撞范围
            //Gizmos.color = Color.yellow;
            //Gizmos.DrawWireCube(rect.center, rect.size);
            Gizmos.color = Color.green;
            float range = rect.size.x / 2;
            for (int i = 0; i < 36; ++i)
            {
                Gizmos.DrawLine(
                    rect.center + new Vector3(Mathf.Sin(Mathf.PI / 18 * i) * range, 0, Mathf.Cos(Mathf.PI / 18 * i) * range),
                    rect.center + new Vector3(Mathf.Sin(Mathf.PI / 18 * (i + 1)) * range, 0, Mathf.Cos(Mathf.PI / 18 * (i + 1)) * range)
                );
            }

        }

    }
}