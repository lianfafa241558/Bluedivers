using System.Collections;
using System.Collections.Generic;
using Core;
using FpsGame.MapUtils;
using FPSGame.Attribute;
using UnityEngine;

public class ModifyTerrain : MonoBehaviour
{
    /// <summary>[Gizmos] 判定"物体已落在自己的地面上"的最大高差（米）。
    /// <para>超过它就认为向下射线打到了别处（典型：在 Prefab Mode 里编辑时，射线会穿过空的预制体场景、
    /// 打到主场景的地形上，实测高差 60m），此时不该把框画到那个命中点上。</para></summary>
    private const float GizmoGroundSnapRange = 20f;

    [SerializeField]
    List<ModifyTerrainData> datas=new();
    [SerializeField]
    Terrain additionTerrain;
    [SerializeField]
    float transitionDistance=5;


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
                if (data.shape == ShapeType.Rectangle)
                {
                    //矩形：外框（淡化到 0 的位置、地表物清除范围）与内框（完全拉平的范围）各自可填，
                    //留空则按 内外半径 回落；按矩形而不是外接圆清除，避免多清掉区域外的树
                    Vector2 outerHalfSize = data.GetOuterHalfSize();
                    Vector2 innerHalfSize = data.GetInnerHalfSize(outerHalfSize);
                    //⚠ 矩形要跟着本物体的**世界 Y 旋转**：兴趣点/任务点实例在运行时会被随机旋转，
                    //   TransformPoint 只保证中心对；不把角度传下去的话矩形本身仍是世界轴对齐的，
                    //   4 条边就会拧成"风车"（每条位置对、方向错）
                    yield return TerrainUtils.ModifyHeightMapRect(pos, innerHalfSize, outerHalfSize, data.depth,
                        transform.eulerAngles.y, true, false);
                    //过渡带（transitionDistance）里的树/石块也要清：否则它们会被抬/挖了一半，表现为半悬空
                    //⚠ TerrainClearer.ClearInRectXZ 目前只支持**轴对齐**矩形：物体带 Y 旋转时这块清除范围不会跟着转
                    TerrainClearer.ClearInRectXZ(pos, outerHalfSize - Vector2.one * transitionDistance, TerrainClearTarget.All,
                        Vector2Int.one * -1, Vector2Int.one * -1);
                }
                else
                {
                    //圆形/椭圆：内外半径都是"米"，形状直接透传给 TerrainUtils
                    yield return TerrainUtils.ModifyHeightMap(pos, data.innerRadius, data.outerRadius, data.depth, data.shape, true, false);
                    //弹坑范围内的地表物统一清除：树 + 石块（走"清除"语义，忽略可被摧毁白名单）+ 草花 + 悬崖覆盖物。
                    //石块必须一起清：地面被挖低之后，留在原地的石块会整块悬空（实测 167 处悬空就是这么来的）
                    TerrainClearer.ClearInRadius(pos, data.outerRadius - transitionDistance, TerrainClearTarget.All,
                        Vector2Int.one * -1, Vector2Int.one*-1);
                    //Debug.LogError("修改了地形" + gameObject);
                }
            }
            if (additionTerrain)
            {
                //附加地形是"角点=position、XZ=size"的方块，中心 = 角点 + 半尺寸（上面已把它摆到 transform.position 附近）
                //按矩形而非外接圆清除，避免多清掉区域外的树（附加地形未被旋转，轴对齐矩形判定是精确的）
                var addSize = additionTerrain.terrainData.size;
                Vector3 addCenter = additionTerrain.transform.position + new Vector3(addSize.x * 0.5f, 0f, addSize.z * 0.5f);
                // 附加地形改的是"矩形 + transitionDistance 过渡带"，清除范围要把过渡带一起算进去，
                // 否则过渡带里的树/石块会被抬/挖了一半，表现为半悬空
                Vector2 halfSize = new Vector2(addSize.x * 0.5f - transitionDistance, addSize.z * 0.5f - transitionDistance);
                //矩形范围内的地表物统一清除（树 + 石块 + 草花 + 悬崖覆盖物）
                TerrainClearer.ClearInRectXZ(addCenter, halfSize, TerrainClearTarget.All,
                    Vector2Int.one * -1, Vector2Int.one * -1);
                yield return TerrainUtils.AdditionTerrain(additionTerrain, transitionDistance, 360 - transform.eulerAngles.y, y, false);
                Destroy(additionTerrain.gameObject);
                //Debug.LogError("附加了地形" + gameObject);
            }

            // 地形刚被改过：标脏 + 立刻兑现一次（跳过 4/s、8/s 限流与 RefreshInterval），
            // 一次性完成"树/草提交 + 树碰撞体重建 + NavMesh 重烘"，避免"地面已经变了、树和石块还悬在半空"
            TerrainClearer.MarkTerrainChanged();
            TerrainClearer.Flush();
        }

        Destroy(this);
    }


    private void OnDrawGizmosSelected()
    {
        if (Application.isPlaying) return;
        int groundMask = LayerMask.GetMask("Ground");
        foreach (var data in datas)
        {
            var pos = transform.TransformPoint(data.localPos);

            //⚠ 框一律画在 pos（物体自身位置）上，不要画在"向下射线命中点"上：
            //  ① 在 Prefab Mode（隔离编辑预制体）里没有地形，射线会打到主场景的地形上——实测本预制体 pos.y=0，
            //     命中 TestScene 的 Terrain 于 y=60.59，于是整组框被画到 60m 高空，表现为"什么都没显示"；
            //  ② 运行时 ModifyHeightMap 用的中心就是 pos.xz（Y 只作基准高度），画在 pos 才与运行范围一致。
            //射线只用来判断"物体是否已经落在自己的地面上"（命中点与物体高差过大就当作打到别处），据此上色。
            bool onGround = Physics.Raycast(pos + Vector3.up * 100, Vector3.down, out var hit, 1000, groundMask)
                            && Mathf.Abs(hit.point.y - pos.y) <= GizmoGroundSnapRange;
            Gizmos.color = onGround ? Color.blue : Color.red;
            //内框 = "压完 depth 之后的那个平面"（比物体基准低 depth），与外圈的差值就是过渡带
            Vector3 height = Vector3.down * data.depth;

            if (data.shape == ShapeType.Rectangle)
            {
                //矩形跟着物体的 Y 旋转（与传给 ModifyHeightMapRect 的 transform.eulerAngles.y 一致），
                //所以用物体自己的 X / Z 轴来画，而不是世界轴
                Vector2 outerHalfSize = data.GetOuterHalfSize();
                //内框直接取参数（留空才按 内/外半径 比例回落）：画出来的就是实际"完全拉平"的范围
                Vector2 innerHalfSize = data.GetInnerHalfSize(outerHalfSize);
                DrawRect(pos, outerHalfSize);
                DrawRect(pos + height, innerHalfSize);
            }
            else
            {
                DrawCircle(pos, data.outerRadius, Vector3.up);
                DrawCircle(pos + height, data.innerRadius, Vector3.up);
                Gizmos.DrawLine(pos - data.outerRadius*transform.TransformDirection(Vector3.forward), pos + data.outerRadius * transform.TransformDirection(Vector3.forward));
                Gizmos.DrawLine(pos - data.outerRadius * transform.TransformDirection(Vector3.left), pos + data.outerRadius * transform.TransformDirection(Vector3.left));
                
                Gizmos.DrawLine(pos + height - data.innerRadius * transform.TransformDirection(Vector3.forward), pos + height + data.innerRadius * transform.TransformDirection(Vector3.forward));
                Gizmos.DrawLine(pos + height - data.innerRadius * transform.TransformDirection(Vector3.left), pos + height + data.innerRadius * transform.TransformDirection(Vector3.left));
            }

            if (!onGround)
            {
                //没落在自己的地面上：补一根 1m 竖线当锚点（红=射线没打到物体脚下的地面）
                Gizmos.DrawLine(pos, pos + Vector3.up);
            }
        }


        /// <summary>画矩形四边。用物体自己的 X / Z 轴 ⇒ 物体带 Y 旋转时框跟着转（与 ModifyHeightMapRect 的算法一致）</summary>
        void DrawRect(Vector3 center, Vector2 halfSize)
        {
            Vector3 right = transform.right * halfSize.x;
            Vector3 forward = transform.forward * halfSize.y;
            Vector3 a = center - right - forward;
            Vector3 b = center + right - forward;
            Vector3 c = center + right + forward;
            Vector3 d = center - right + forward;
            Gizmos.DrawLine(a, b);
            Gizmos.DrawLine(b, c);
            Gizmos.DrawLine(c, d);
            Gizmos.DrawLine(d, a);
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
        [Compare("shape", (int)ShapeType.Circle, CompareOperate.Equal)]
        public int innerRadius;
        [Compare("shape", (int)ShapeType.Circle, CompareOperate.Equal)]
        public int outerRadius;

        [InspectorName("矩形内半尺寸(X,Z)")]
        [Compare("shape", (int)ShapeType.Rectangle, CompareOperate.Equal)]
        public Vector2 rectInnerHalfSize;
        [InspectorName("矩形外半尺寸(X,Z)")]
        [Compare("shape",(int)ShapeType.Rectangle,CompareOperate.Equal)]
        public Vector2 rectHalfSize;

        public float depth;
        /// <summary>
        /// 取矩形的**外框** XZ 半长（米）：地形改动/淡化到 0 的位置，同时决定地表物清除范围。
        /// <para>逐轴回落：某个分量 &lt;= 0 时该轴用 <see cref="outerRadius"/>，
        /// 这样"只填一轴"也能表达出来，并兼容项目里"矩形只给一个半长度"的惯例（Actor.shape）。</para>
        /// </summary>
        public Vector2 GetOuterHalfSize()
            => new(rectHalfSize.x > 0f ? rectHalfSize.x : outerRadius,
                   rectHalfSize.y > 0f ? rectHalfSize.y : outerRadius);

        /// <summary>
        /// 取矩形的**内框** XZ 半长（米）：该范围内地形完全按目标高度拉平。
        /// <para>逐轴回落：某个分量 &lt;= 0 时该轴按 外框 × (内半径 / 外半径) 推导，
        /// 与圆形的"内圈 = 外圈 × 内/外比例"语义一致。</para>
        /// </summary>
        /// <param name="outerHalfSize">外框半长（米），由 <see cref="GetOuterHalfSize"/> 给出</param>
        public Vector2 GetInnerHalfSize(Vector2 outerHalfSize)
        {
            float scale = innerRadius / (outerRadius + 0f);
            return new(rectInnerHalfSize.x > 0f ? rectInnerHalfSize.x : outerHalfSize.x * scale,
                       rectInnerHalfSize.y > 0f ? rectInnerHalfSize.y : outerHalfSize.y * scale);
        }
    }
}
