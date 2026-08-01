using FPSGame.Attribute;
using Unity.AI.Navigation;
using UnityEngine;
namespace FpsGame.MapUtils
{
    public class MapRoot : MonoBehaviour
    {
        [Foldout("地形设置", true)] 
        public Terrain terrain;
        [Header("里面的位置是指起点，不是指中心！")]
        public BoundsInt rect;
        [InspectorName("空气墙")]
        public GameObject wallPrefab;

        public void Init(bool isNormal)
        {
            //Debug.LogError("maprootInit的大小" + terrain.terrainData.size);
            //TerrainUtils.Main = terrain;
            if(isNormal)GenerateTerrainRect();
            CreatAirWall();
        }
        /*
        private void Awake()
        {
            TerrainUtils.Main = terrain;
            GenerateTerrainRect();
            CreatAirWall();
        }*/

        private void OnDestroy()
        {
            TerrainUtils.Main = null;
        }

        public static Vector3Int ToInt(Vector3 a) => new Vector3Int(Mathf.RoundToInt(a.x), Mathf.RoundToInt(a.y), Mathf.RoundToInt(a.z));


        /// <summary>
        /// 生成Rect并且同步到NavMeshSurface
        /// </summary>
        [ContextMenu("生成Rect")]
        public void GenerateTerrainRect()
        {

           
            var border = Constants.TaskBorder;
            var locpos = terrain.GetPosition();
            var size = terrain.terrainData.size;
            //Debug.LogError("maproot地形生成的大小"+ size);
            Vector3Int startPos =ToInt((locpos + border * new Vector3(1, 0, 1)));
            Vector3Int rectSize = ToInt(size) - border * new Vector3Int(2, 0, 2);

            rect = new(startPos, rectSize);
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