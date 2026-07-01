using UnityEngine;

public class HemisphereCloud : MonoBehaviour
{
    [Header("云参数")]
    public Material cloudMaterial;
    public float radius = 500f;
    public int segments = 64;


    private Mesh hemisphereMesh;
    private GameObject cloudObject;

    void Start()
    {
        CreateHemisphereCloud();

      
    }

    void CreateHemisphereCloud()
    {
        // 生成半球网格
        CreateHemisphereMesh();

        // 创建半球云对象
        if (cloudObject == null)
        {
            cloudObject = new GameObject("HemisphereCloud");
            cloudObject.transform.SetParent(transform);
            cloudObject.transform.position = Vector3.zero;
        }

        // 为Mesh添加组件
        MeshFilter mf = cloudObject.GetComponent<MeshFilter>();
        if (mf == null) mf = cloudObject.AddComponent<MeshFilter>();
        mf.mesh = hemisphereMesh;

        MeshRenderer mr = cloudObject.GetComponent<MeshRenderer>();
        if (mr == null) mr = cloudObject.AddComponent<MeshRenderer>();
        mr.material = cloudMaterial;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = false;

        // 确保渲染顺序在天空盒之后
        if (cloudMaterial != null)
        {
            cloudMaterial.renderQueue = 3000;
        }
    }

    void CreateHemisphereMesh()
    {
        hemisphereMesh = new Mesh();

        int rings = segments / 2;
        int sectors = segments;

        Vector3[] vertices = new Vector3[(rings + 1) * (sectors + 1)];
        Vector2[] uv = new Vector2[(rings + 1) * (sectors + 1)];
        Vector3[] normals = new Vector3[(rings + 1) * (sectors + 1)];
        int[] triangles = new int[rings * sectors * 6];

        // 生成顶点
        for (int i = 0; i <= rings; i++)
        {
            float phi = Mathf.PI / 2 * (1 - (float)i / rings);
            float y = radius * Mathf.Sin(phi);
            float r = radius * Mathf.Cos(phi);

            for (int j = 0; j <= sectors; j++)
            {
                float theta = 2 * Mathf.PI * (float)j / sectors;
                float x = r * Mathf.Cos(theta);
                float z = r * Mathf.Sin(theta);

                int index = i * (sectors + 1) + j;
                vertices[index] = new Vector3(x, y, z);

                // 法线方向（从中心指向外）
                normals[index] = new Vector3(x, y, z).normalized;

                // UV坐标：u是水平角度，v是垂直角度
                uv[index] = new Vector2((float)j / sectors, (float)i / rings);
            }
        }

        // 生成三角形索引
        int triIndex = 0;
        for (int i = 0; i < rings; i++)
        {
            for (int j = 0; j < sectors; j++)
            {
                int current = i * (sectors + 1) + j;
                int next = (i + 1) * (sectors + 1) + j;

                triangles[triIndex++] = current;
                triangles[triIndex++] = next;
                triangles[triIndex++] = current + 1;

                triangles[triIndex++] = current + 1;
                triangles[triIndex++] = next;
                triangles[triIndex++] = next + 1;
            }
        }

        hemisphereMesh.vertices = vertices;
        hemisphereMesh.normals = normals;
        hemisphereMesh.uv = uv;
        hemisphereMesh.triangles = triangles;

        // 可选：为性能优化设置边界
        hemisphereMesh.bounds = new Bounds(Vector3.up * radius * 0.5f, Vector3.one * radius * 2);
    }

    void OnDestroy()
    {
        if (hemisphereMesh != null)
            DestroyImmediate(hemisphereMesh);
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0, 1, 1, 0.2f);
        Gizmos.DrawWireSphere(transform.position, radius);

        // 只显示上半球范围
        Gizmos.color = Color.cyan;
        Vector3 center = transform.position + Vector3.up * radius;
        Gizmos.DrawWireCube(center, new Vector3(radius * 2, radius, radius * 2));
    }
}