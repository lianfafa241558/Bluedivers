using System.Collections;
using System.Collections.Generic;
using Core;
using FpsGame.MapUtils;

using UnityEngine;

public class ModifyTerrain : MonoBehaviour
{
    [SerializeField]
    List<ModifyTerrainData> datas=new();
    [SerializeField]
    Terrain additionTerrain;
    [SerializeField]
    float transitionDistance=5;
    [SerializeField]
    [InspectorName("清除植被的原型范围（含头含尾，7-10=树）")]
    Vector2Int clearVegetationRange = new Vector2Int(7, 10);

    [SerializeField]
    [InspectorName("测试时使用，在start修改地形")]
    bool StartModify;
    private void Awake()
    {
        if(!StartModify) StartCoroutine(nameof(Modify));
    }
    private void Start()
    {
        if (StartModify) StartCoroutine(nameof(Modify));
    }
    private IEnumerator Modify()
    {
        float y = TerrainUtils.WSToHeight(transform.position);
        if (additionTerrain)
        {
            var size = additionTerrain.terrainData.size.x * -0.5f;
            additionTerrain.transform.position = transform.position + (new Vector3(size, additionTerrain.transform.localPosition.y, size));
        }

        transform.position = new(transform.position.x,y,transform.position.z);
        //if (additionTerrain) Debug.LogError("贴图,0的高度为"+ additionTerrain.WSToHeight(additionTerrain.GetPosition()+5*Vector3.one), gameObject);

        //Debug.LogError("修改高度" + transform.position);
        if (BattleManager.Instance)
        {
            foreach (var data in datas)
            {
                Vector3 pos = transform.TransformPoint(data.localPos);
                //Debug.LogError("修改高度"+ pos+"  "+ data.outerRadius,gameObject);
                yield return TerrainUtils.ModifyHeightMap(pos, data.innerRadius, data.outerRadius, data.depth, ShapeType.Circle, true, false);
                //弹坑范围内的树整片清除（"清除"不走爆炸白名单；只清 clearVegetationRange，默认 7-10 的树，石块 0-6 保留）
                TreeDestructor.ClearInRadius(pos, data.outerRadius, clearVegetationRange.x, clearVegetationRange.y);
                //弹坑范围内的细节（草/花）一并擦除
                TerrainDetailEraser.ClearInRadius(pos, data.outerRadius);
                //Debug.LogError("修改了地形" + gameObject);
            }
            if (additionTerrain)
            {
                //附加地形是"角点=position、XZ=size"的方块，中心 = 角点 + 半尺寸（上面已把它摆到 transform.position 附近）
                //按矩形而非外接圆清除，避免多清掉区域外的树（附加地形未被旋转，轴对齐矩形判定是精确的）
                var addSize = additionTerrain.terrainData.size;
                Vector3 addCenter = additionTerrain.transform.position + new Vector3(addSize.x * 0.5f, 0f, addSize.z * 0.5f);
                TreeDestructor.ClearInRectXZ(addCenter, new Vector2(addSize.x * 0.5f, addSize.z * 0.5f),
                    clearVegetationRange.x, clearVegetationRange.y);
                //附加地形覆盖区域的细节（草/花）同样擦除
                TerrainDetailEraser.ClearInRectXZ(addCenter, new Vector2(addSize.x * 0.5f, addSize.z * 0.5f));
                yield return TerrainUtils.AdditionTerrain(additionTerrain, transitionDistance, 360 - transform.eulerAngles.y, y, false);
                Destroy(additionTerrain.gameObject);
                //Debug.LogError("附加了地形" + gameObject);
            }
        }

        Destroy(this);
    }


    private void OnDrawGizmosSelected()
    {
        if (Application.isPlaying) return;
        foreach (var data in datas)
        {
            var pos = transform.TransformPoint(data.localPos);
            //var pos = transform.position;
            
            if (Physics.Raycast(pos + Vector3.up * 100, Vector3.down, out var hit, 1000, LayerMask.GetMask("Ground")))
            {
                Gizmos.color = Color.blue;
                Vector3 height = -Vector3.up * (data.depth-(pos.y- hit.point.y));

                DrawCircle(hit.point,data.outerRadius,Vector3.up);
                DrawCircle(hit.point+ height, data.innerRadius, Vector3.up);
                Gizmos.DrawLine(hit.point - data.outerRadius*transform.TransformDirection(Vector3.forward), hit.point + data.outerRadius * transform.TransformDirection(Vector3.forward));
                Gizmos.DrawLine(hit.point - data.outerRadius * transform.TransformDirection(Vector3.left), hit.point + data.outerRadius * transform.TransformDirection(Vector3.left));
                
                Gizmos.DrawLine(hit.point + height - data.innerRadius * transform.TransformDirection(Vector3.forward), hit.point + height + data.innerRadius * transform.TransformDirection(Vector3.forward));
                Gizmos.DrawLine(hit.point + height - data.innerRadius * transform.TransformDirection(Vector3.left), hit.point + height + data.innerRadius * transform.TransformDirection(Vector3.left));

            }
            else
            {
                Gizmos.color = Color.red;
                Gizmos.DrawLine(pos, pos + Vector3.up);
            }
        }


        void DrawCircle(Vector3 center, float radius, Vector3 normal)
        {
            Vector3 forward = Vector3.Cross(normal, Vector3.right);
            if (forward.magnitude < 0.01f)
            {
                forward = Vector3.Cross(normal, Vector3.forward);
            }

            for (int i = 0; i < 16; i++)
            {
                float angle1 = (float)i / 16 * Mathf.PI * 2f;
                float angle2 = (float)(i + 1) / 16 * Mathf.PI * 2f;

                Vector3 point1 = center + (Quaternion.AngleAxis(angle1 * Mathf.Rad2Deg, normal) * forward).normalized * radius;
                Vector3 point2 = center + (Quaternion.AngleAxis(angle2 * Mathf.Rad2Deg, normal) * forward).normalized * radius;

                Gizmos.DrawLine(point1, point2);
            }
        }
    }

    [System.Serializable]
    struct ModifyTerrainData
    {
        public Vector3 localPos;
        public ShapeType shape;
        public int innerRadius;
        public int outerRadius;
        public float depth;
    }
}
