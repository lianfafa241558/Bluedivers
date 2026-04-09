using System.Collections;
using System.Collections.Generic;
using GameContract;
using Unity.BaseTool;
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
    [CustomLabel("测试时使用:在start修改地形")]
    bool StartModify;
    private void Awake()
    {
        if(!StartModify) Modify();
    }
    private void Start()
    {
        Modify();
    }
    private void Modify()
    {
        float y = 0;
        if (additionTerrain)
        {
            var size = additionTerrain.terrainData.size.x * -0.5f;
            additionTerrain.transform.position = transform.position + (new Vector3(size, additionTerrain.transform.localPosition.y, size));

            for(int i = -2; i <= 2; ++i)
            {
                for (int u = -2; u <= 2; ++u)
                {
                    y += TerrainUtils.WSToHeight(transform.position + new Vector3(i, 0, u) * size/2);
                }
            }
            y /= 25;
           
        }
        else
        {
            //Debug.LogError("修改地形"+gameObject,gameObject);
            y = (TerrainUtils.WSToHeight(transform.position)
                +TerrainUtils.WSToHeight(transform.position + Vector3.left * 5)
                +TerrainUtils.WSToHeight(transform.position + Vector3.right * 5)
                +TerrainUtils.WSToHeight(transform.position + Vector3.forward * 5)
                +TerrainUtils.WSToHeight(transform.position + Vector3.back * 5)
            )/5;
        }

        transform.position = new(transform.position.x,y,transform.position.z);
        //if (additionTerrain) Debug.LogError("点0,0的高度为"+ additionTerrain.WSToHeight(additionTerrain.GetPosition()+5*Vector3.one), gameObject);

        //Debug.LogError("修改高度" + transform.position);
        if (BattleManager.Instance)
        {
            foreach (var data in datas)
            {
                Vector3 pos = transform.TransformPoint(data.localPos);
                //Debug.LogError("修改高度"+ pos+"  "+ data.outerRadius,gameObject);
                TerrainUtils.ModifyHeightMap(pos, data.innerRadius, data.outerRadius, data.depth, ShapeType.Circle, true, false);
                //Debug.LogError("修改了地形" + gameObject);
            }
            if (additionTerrain)
            {
                //好像那边和世界的概念不一样，要用负数
                //这里不刷新
                TerrainUtils.AdditionTerrain(additionTerrain, transitionDistance, 360 - transform.eulerAngles.y, false);
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
