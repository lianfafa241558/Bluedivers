using System.Collections.Generic;
using System.Linq;

using UnityEditor;
using UnityEditor.SceneManagement;

using UnityEngine;

namespace Unity.FPS.Game
{
    /// <summary>
    /// 肢体编辑器窗口：自动读取当前打开预制体的 Damageable / TransferDamageable 组件，
    /// 左侧分类列出、中间模型预览、右侧序列化显示选中组件字段，并在选中 Damageable 时
    /// 下方依次显示其关联的 TransferDamageable。
    /// </summary>
    public class LimbEditorWindow : EditorWindow
    {
        // ========== 状态 ==========
        private GameObject _stageRoot;              // 当前预制体根物体
        private int _stageVersion = -1;             // 预制体阶段指纹，用于检测切换

        private List<Damageable> _damageables = new();
        private List<TransferDamageable> _transfers = new();

        private int _selectedDamageableIndex = -1;
        private int _selectedTransferIndex = -1;

        private Vector2 _damageableScroll;
        private Vector2 _transferScroll;
        private Vector2 _rightScroll;

        // ========== 缓存的 Inspector Editor（保持折叠状态） ==========
        private UnityEditor.Editor _selectedEditor;
        private Object _selectedTarget;
        private readonly List<UnityEditor.Editor> _assocEditors = new();
        private List<Object> _assocTargets = new();

        // ========== 预览 ==========
        private GameObject _previewAsset;      // 当前预制体资产（Project 内 .prefab），用于预览渲染
        private PreviewRenderUtility _preview;
        private readonly List<Mesh> _tempMeshes = new();    // 渲染用的临时 Mesh，EndPreview 后统一销毁
        private readonly List<Material> _tempMaterials = new(); // 渲染用的临时材质，EndPreview 后统一销毁
        private float _previewYaw = 30f;
        private float _previewPitch = 20f;
        private float _previewDist = 3f;

        // ========== 样式 ==========
        private GUIStyle _groupHeaderStyle;
        private GUIStyle _titleStyle;
        private bool _stylesBuilt;

        // ========== 窗口入口 ==========
        [MenuItem("Tools/肢体编辑器")]
        public static void ShowWindow()
        {
            var window = GetWindow<LimbEditorWindow>("肢体编辑器");
            window.minSize = new Vector2(900, 600);
            window.Show();
        }

        private void OnDisable()
        {
            DestroyCachedEditors();
        }

        // ========== 样式 ==========
        private void BuildStyles()
        {
            if (_stylesBuilt) return;
            _stylesBuilt = true;

            _groupHeaderStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 12,
                margin = new RectOffset(4, 4, 6, 4),
            };
            _titleStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 13,
                alignment = TextAnchor.MiddleCenter,
                margin = new RectOffset(4, 4, 4, 4),
            };
        }

        // ========== 主 GUI ==========
        private void OnGUI()
        {
            BuildStyles();
            RefreshStageIfChanged();

            DrawHeader();

            if (_stageRoot == null)
            {
                DrawEmptyHint();
                return;
            }

            // 三栏布局
            var leftRect = new Rect(0, 40, 220, position.height - 40);
            var rightRect = new Rect(position.width - 340, 40, 340, position.height - 40);
            var middleRect = new Rect(leftRect.xMax + 2, 40,
                Mathf.Max(100, rightRect.xMin - leftRect.xMax - 4),
                position.height - 40);

            DrawLeftList(leftRect);
            DrawMiddlePreview(middleRect);
            DrawRightInspector(rightRect);
        }

        private void DrawHeader()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            GUILayout.Label("当前预制体: " + (_stageRoot != null ? _stageRoot.name : "（无）"),
                EditorStyles.boldLabel);
            if (GUILayout.Button("刷新", EditorStyles.toolbarButton, GUILayout.Width(60)))
            {
                RefreshAll();
            }
            EditorGUILayout.EndHorizontal();
        }

        private void DrawEmptyHint()
        {
            GUILayout.BeginArea(new Rect(0, 40, position.width, position.height - 40));
            GUILayout.FlexibleSpace();
            EditorGUILayout.HelpBox("请先在场景中打开一个预制体（Prefab 模式），\n窗口会自动读取其中的 Damageable / TransferDamageable 组件。",
                MessageType.Info);
            GUILayout.FlexibleSpace();
            GUILayout.EndArea();
        }

        // ========== 左侧列表 ==========
        private void DrawLeftList(Rect rect)
        {
            GUILayout.BeginArea(rect);
            EditorGUILayout.BeginVertical(GUI.skin.box);

            // 上方：Damageable
            EditorGUILayout.LabelField("Damageable（肢体）", _groupHeaderStyle);
            DrawComponentList(_damageables.Count,
                i => _damageables[i].gameObject.name,
                i => i == _selectedDamageableIndex,
                i =>
                {
                    _selectedDamageableIndex = i;
                    _selectedTransferIndex = -1;
                    RebuildSelectedEditor(_damageables[i]);
                },
                () => _selectedDamageableIndex = -1,
                ref _damageableScroll);

            EditorGUILayout.Space(6);

            // 下方：TransferDamageable
            EditorGUILayout.LabelField("TransferDamageable（附属肢体）", _groupHeaderStyle);
            DrawComponentList(_transfers.Count,
                i => _transfers[i].gameObject.name,
                i => i == _selectedTransferIndex,
                i =>
                {
                    _selectedTransferIndex = i;
                    _selectedDamageableIndex = -1;
                    RebuildSelectedEditor(_transfers[i]);
                },
                () => _selectedTransferIndex = -1,
                ref _transferScroll);

            EditorGUILayout.EndVertical();
            GUILayout.EndArea();
        }

        private void DrawComponentList(int count,
            System.Func<int, string> getName,
            System.Func<int, bool> isSelected,
            System.Action<int> onSelect,
            System.Action onClear,
            ref Vector2 scroll)
        {
            using (var scope = new EditorGUILayout.ScrollViewScope(scroll, GUILayout.ExpandHeight(true)))
            {
                scroll = scope.scrollPosition;
                if (count == 0)
                {
                    EditorGUILayout.HelpBox("未找到组件", MessageType.None);
                }

                for (int i = 0; i < count; i++)
                {
                    var selected = isSelected(i);
                    if (selected)
                    {
                        // 选中态用蓝色背景染亮默认按钮
                        GUI.backgroundColor = new Color(0.16f, 0.45f, 0.85f, 1f);
                    }

                    if (GUILayout.Button(getName(i), GUILayout.Height(24)))
                    {
                        if (selected)
                        {
                            onClear();
                        }
                        else
                        {
                            onSelect(i);
                        }
                    }

                    if (selected)
                    {
                        GUI.backgroundColor = Color.white;
                    }
                }
            }
        }

        // ========== 中间模型预览 ==========
        private void DrawMiddlePreview(Rect rect)
        {
            GUILayout.BeginArea(rect);
            EditorGUILayout.LabelField("模型预览", _titleStyle);

            var box = GUILayoutUtility.GetRect(rect.x, rect.width, 300, 400, GUILayout.ExpandHeight(true));
            box = new Rect(4, 60, rect.width - 8, rect.height - 70);

            if (_previewAsset == null)
            {
                EditorGUI.HelpBox(box, "无预览对象", MessageType.Info);
                GUILayout.EndArea();
                return;
            }

            // 鼠标拖拽旋转
            var evt = Event.current;
            if (evt.type == EventType.MouseDrag && box.Contains(evt.mousePosition))
            {
                if (evt.button == 0)
                {
                    _previewYaw += evt.delta.x * 0.5f;
                    _previewPitch = Mathf.Clamp(_previewPitch - evt.delta.y * 0.5f, -80f, 80f);
                }
                else if (evt.button == 2)
                {
                    _previewDist = Mathf.Clamp(_previewDist + evt.delta.y * 0.02f, 0.5f, 30f);
                }
                evt.Use();
            }

            try
            {
                RenderPreview(box);
            }
            catch (System.Exception e)
            {
                EditorGUI.HelpBox(box, "预览渲染失败:\n" + e.Message, MessageType.Warning);
            }

            GUILayout.EndArea();
        }

        /// <summary>用 PreviewRenderUtility 渲染模型，并叠加绘制选中肢体的 Collider 线框</summary>
        private void RenderPreview(Rect box)
        {
            InitPreview();
            if (_preview == null) return;

            _preview.BeginPreview(box, GUIStyle.none);
            _preview.camera.clearFlags = CameraClearFlags.SolidColor;
            _preview.camera.backgroundColor = new Color(0.22f, 0.22f, 0.22f, 1f);
            _preview.camera.enabled = false;

            // 计算模型包围盒并定位相机
            var bounds = CalculatePreviewBounds(_previewAsset);
            if (bounds.size == Vector3.zero)
            {
                _preview.EndPreview();
                EditorGUI.HelpBox(box, "此预制体无可用预览", MessageType.Info);
                return;
            }

            float radius = Mathf.Max(bounds.extents.magnitude, 0.01f);
            var center = bounds.center;

            _preview.camera.transform.position = center + Quaternion.Euler(_previewPitch, _previewYaw, 0f) * (Vector3.forward * (_previewDist * radius));
            _preview.camera.transform.LookAt(center);
            _preview.camera.nearClipPlane = 0.01f;
            _preview.camera.farClipPlane = 100f;
            _preview.camera.fieldOfView = 30f;

            // 渲染模型网格
            RenderModelMeshes(_previewAsset);

            // 叠加选中肢体的 Collider 线框
            RenderSelectedColliders();

            // PreviewRenderUtility.DrawMesh 只是把 mesh 注册到相机，需显式调用 Render 才会真正绘制到 RT
            // 且开启 SRP 支持以正确渲染 URP 材质（updatefov=false，fov 已手动设置）
            _preview.Render(true, false);

            var tex = _preview.EndPreview();
            GUI.DrawTexture(box, tex, ScaleMode.StretchToFill, false);

            // EndPreview 已实际渲染，此刻才可安全销毁全部临时 Mesh/材质
            DestroyTempPreviewAssets();

            // 提示
            EditorGUI.LabelField(new Rect(box.x, box.y, box.width, 18), "左键拖拽旋转 · 中键缩放",
                new GUIStyle(GUI.skin.label) { normal = { textColor = new Color(0.8f, 0.8f, 0.8f, 0.9f) } });
        }

        /// <summary>销毁本帧渲染产生的临时 Mesh 与材质</summary>
        private void DestroyTempPreviewAssets()
        {
            foreach (var m in _tempMeshes)
            {
                if (m != null) Object.DestroyImmediate(m);
            }
            _tempMeshes.Clear();

            foreach (var mat in _tempMaterials)
            {
                if (mat != null) Object.DestroyImmediate(mat);
            }
            _tempMaterials.Clear();
        }

        private void InitPreview()
        {
            if (_preview == null)
            {
                _preview = new PreviewRenderUtility();
                _preview.camera.fieldOfView = 30f;
                _preview.camera.nearClipPlane = 0.01f;
                _preview.camera.farClipPlane = 100f;
            }
        }

        /// <summary>计算整个预制体的包围盒（含所有 Renderer 与 Collider）</summary>
        private Bounds CalculatePreviewBounds(GameObject root)
        {
            var bounds = new Bounds(root.transform.position, Vector3.one);
            bool has = false;

            var renderers = root.GetComponentsInChildren<Renderer>(true);
            foreach (var r in renderers)
            {
                if (!has) { bounds = r.bounds; has = true; }
                else bounds.Encapsulate(r.bounds);
            }

            var colliders = root.GetComponentsInChildren<Collider>(true);
            foreach (var c in colliders)
            {
                if (!has) { bounds = c.bounds; has = true; }
                else bounds.Encapsulate(c.bounds);
            }

            if (!has) bounds = new Bounds(root.transform.position, Vector3.one);
            return bounds;
        }

        /// <summary>
        /// 渲染模型的所有网格（含骨骼蒙皮）。
        /// PreviewRenderUtility.DrawMesh 只接受 (Mesh, Vector3, Quaternion, Material, ...)，
        /// 且不含 scale，故把所有网格顶点烘焙到世界坐标后以原点位置渲染。
        /// </summary>
        private void RenderModelMeshes(GameObject root)
        {
            var defaultMat = CreateWireMaterial();
            var renderers = root.GetComponentsInChildren<Renderer>(true);

            foreach (var r in renderers)
            {
                if (r is SkinnedMeshRenderer smr)
                {
                    var src = smr.sharedMesh;
                    if (src == null) continue;

                    var baked = new Mesh { hideFlags = HideFlags.HideAndDontSave };
                    smr.BakeMesh(baked);
                    if (baked.vertexCount == 0) { Object.DestroyImmediate(baked); continue; }

                    var verts = baked.vertices;
                    var matrix = smr.transform.localToWorldMatrix;
                    var worldVerts = new Vector3[verts.Length];
                    for (int i = 0; i < verts.Length; i++)
                        worldVerts[i] = matrix.MultiplyPoint3x4(verts[i]);
                    baked.vertices = worldVerts;
                    baked.RecalculateBounds();
                    _tempMeshes.Add(baked);

                    // 逐子网格绘制，保证多材质模型完整显示
                    var mats = smr.sharedMaterials;
                    int subCount = baked.subMeshCount;
                    for (int s = 0; s < subCount; s++)
                    {
                        var mat = s < mats.Length ? mats[s] : defaultMat;
                        _preview.DrawMesh(baked, Matrix4x4.identity, mat, s);
                    }
                }
                else if (r is MeshRenderer)
                {
                    var mf = r.GetComponent<MeshFilter>();
                    if (mf == null || mf.sharedMesh == null) continue;

                    var baked = CreateBakedWorldMesh(mf.sharedMesh, mf.transform.localToWorldMatrix);
                    _tempMeshes.Add(baked);

                    var mats = r.sharedMaterials;
                    _preview.DrawMesh(baked, Matrix4x4.identity, mats.Length > 0 ? mats[0] : defaultMat, 0);
                }
            }

            // defaultMat 可能被 DrawMesh 延迟渲染引用，须存活到 EndPreview 后统一销毁
            _tempMaterials.Add(defaultMat);
        }

        /// <summary>复制网格并把顶点变换到世界坐标（用于 DrawMesh 无 scale 的限制）</summary>
        private Mesh CreateBakedWorldMesh(Mesh src, Matrix4x4 matrix)
        {
            var mesh = new Mesh { hideFlags = HideFlags.HideAndDontSave };
            var verts = src.vertices;
            var worldVerts = new Vector3[verts.Length];
            for (int i = 0; i < verts.Length; i++)
                worldVerts[i] = matrix.MultiplyPoint3x4(verts[i]);
            mesh.vertices = worldVerts;
            mesh.uv = src.uv;
            mesh.SetTriangles(src.triangles, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>创建线框/默认材质（Hidden/Internal-Colored，带顶点色）</summary>
        private Material CreateWireMaterial()
        {
            var mat = new Material(Shader.Find("Hidden/Internal-Colored"))
            {
                hideFlags = HideFlags.HideAndDontSave
            };
            mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off);
            mat.SetInt("_ZWrite", 0);
            return mat;
        }

        /// <summary>
        /// 叠加绘制选中肢体的 Collider 线框（蓝色）。
        /// 选中 Damageable 时高亮其 LineArmor 关联的 TransferDamageable 的 Collider；
        /// 选中 TransferDamageable 时高亮它自身及其子物体的 Collider。
        /// </summary>
        private void RenderSelectedColliders()
        {
            var colliders = CollectTargetColliders();
            if (colliders == null || colliders.Count == 0) return;

            // 收集所有线段（世界坐标，两两成对）
            var segmentList = new List<Vector3>(64);
            foreach (var col in colliders)
            {
                DrawColliderWire(col, col.transform.localToWorldMatrix, segmentList);
            }
            if (segmentList.Count == 0) return;

            // 每条线段扩展成一个细长 Box（三角形拓扑）
            var tris = new List<Vector3>(segmentList.Count * 36);
            for (int i = 0; i + 1 < segmentList.Count; i += 2)
            {
                AddThickSegment(tris, segmentList[i], segmentList[i + 1], 0.02f);
            }
            if (tris.Count == 0) return;

            var indices = new int[tris.Count];
            for (int i = 0; i < indices.Length; i++) indices[i] = i;

            var wireMesh = new Mesh { hideFlags = HideFlags.HideAndDontSave };
            wireMesh.vertices = tris.ToArray();
            wireMesh.SetIndices(indices, MeshTopology.Triangles, 0);
            wireMesh.RecalculateBounds();

            _tempMeshes.Add(wireMesh);
            var wireMaterial = CreateWireMaterial();
            _tempMaterials.Add(wireMaterial);

            // 顶点已是世界坐标，DrawMesh 以单位矩阵渲染
            _preview.DrawMesh(wireMesh, Matrix4x4.identity, wireMaterial, 0);
        }

        /// <summary>
        /// 收集当前选中肢体应高亮的 Collider：
        /// - 选中 Damageable：取它自身（主肢体）的 Collider，加上 LineArmor 列表里引用的 TransferDamageable 的 Collider
        /// - 选中 TransferDamageable：取它自身及其子物体的 Collider
        /// </summary>
        private List<Collider> CollectTargetColliders()
        {
            var result = new List<Collider>();

            if (_selectedDamageableIndex >= 0 && _selectedDamageableIndex < _damageables.Count)
            {
                var dmg = _damageables[_selectedDamageableIndex];

                // 主肢体自身的 Collider（含其子物体）
                result.AddRange(dmg.GetComponentsInChildren<Collider>(true));

                // LineArmor 关联的 TransferDamageable 的 Collider
                var so = new SerializedObject(dmg);
                var lineArmor = so.FindProperty("LineArmor");
                if (lineArmor != null)
                {
                    for (int i = 0; i < lineArmor.arraySize; i++)
                    {
                        var td = lineArmor.GetArrayElementAtIndex(i).objectReferenceValue as TransferDamageable;
                        if (td != null)
                        {
                            result.AddRange(td.GetComponentsInChildren<Collider>(true));
                        }
                    }
                }
            }
            else if (_selectedTransferIndex >= 0 && _selectedTransferIndex < _transfers.Count)
            {
                var td = _transfers[_selectedTransferIndex];
                result.AddRange(td.GetComponentsInChildren<Collider>(true));
            }

            return result;
        }

        /// <summary>把一条线段（A→B）扩展成细长 Box，三角形顶点追加到 tris（世界坐标）</summary>
        private void AddThickSegment(List<Vector3> tris, Vector3 a, Vector3 b, float thickness)
        {
            Vector3 dir = b - a;
            float len = dir.magnitude;
            if (len < 0.0001f) return;
            dir /= len;

            // 构造两个垂直于 dir 的基向量 u、v
            Vector3 refVec = Mathf.Abs(Vector3.Dot(dir, Vector3.up)) < 0.9f ? Vector3.up : Vector3.right;
            Vector3 u = Vector3.Cross(dir, refVec).normalized * thickness;
            Vector3 v = Vector3.Cross(dir, u).normalized * thickness;

            var c = a + dir * (len * 0.5f);

            var p0 = c - dir * (len * 0.5f) - u - v;
            var p1 = c - dir * (len * 0.5f) + u - v;
            var p2 = c - dir * (len * 0.5f) + u + v;
            var p3 = c - dir * (len * 0.5f) - u + v;
            var p4 = c + dir * (len * 0.5f) - u - v;
            var p5 = c + dir * (len * 0.5f) + u - v;
            var p6 = c + dir * (len * 0.5f) + u + v;
            var p7 = c + dir * (len * 0.5f) - u + v;

            // 12 个三角形（6 面，每面 2 个三角形）
            Quad(tris, p0, p1, p2, p3);   // -z
            Quad(tris, p4, p5, p6, p7);   // +z
            Quad(tris, p0, p1, p5, p4);   // -y
            Quad(tris, p3, p2, p6, p7);   // +y
            Quad(tris, p0, p3, p7, p4);   // -x
            Quad(tris, p1, p2, p6, p5);   // +x
        }

        private void Quad(List<Vector3> tris, Vector3 a, Vector3 b, Vector3 c, Vector3 d)
        {
            tris.Add(a); tris.Add(b); tris.Add(c);
            tris.Add(a); tris.Add(c); tris.Add(d);
        }

        /// <summary>把单个 Collider 的线框线段追加到 segmentList（世界坐标，两两成对）</summary>
        private void DrawColliderWire(Collider col, Matrix4x4 localToWorld, List<Vector3> segments)
        {
            if (col is BoxCollider box)
            {
                Vector3 c = box.center;
                Vector3 s = box.size * 0.5f;
                var corners = new[]
                {
                    new Vector3(c.x - s.x, c.y - s.y, c.z - s.z), new Vector3(c.x + s.x, c.y - s.y, c.z - s.z),
                    new Vector3(c.x + s.x, c.y + s.y, c.z - s.z), new Vector3(c.x - s.x, c.y + s.y, c.z - s.z),
                    new Vector3(c.x - s.x, c.y - s.y, c.z + s.z), new Vector3(c.x + s.x, c.y - s.y, c.z + s.z),
                    new Vector3(c.x + s.x, c.y + s.y, c.z + s.z), new Vector3(c.x - s.x, c.y + s.y, c.z + s.z),
                };
                int[] edges = { 0,1, 1,2, 2,3, 3,0, 4,5, 5,6, 6,7, 7,4, 0,4, 1,5, 2,6, 3,7 };
                DrawEdgeLoop(corners, edges, localToWorld, segments);
            }
            else if (col is SphereCollider sphere)
            {
                Vector3 c = sphere.center;
                float r = sphere.radius;
                // 三个正交平面的圆（近似 16 段）
                DrawCircle(c, Vector3.right, r, 16, localToWorld, segments);
                DrawCircle(c, Vector3.up, r, 16, localToWorld, segments);
                DrawCircle(c, Vector3.forward, r, 16, localToWorld, segments);
            }
            else if (col is CapsuleCollider capsule)
            {
                float r = capsule.radius;
                float halfH = Mathf.Max(0f, capsule.height * 0.5f - r);
                Vector3 c = capsule.center;
                Vector3 dir = Vector3.up;
                if (capsule.direction == 0) dir = Vector3.right;
                else if (capsule.direction == 2) dir = Vector3.forward;
                Vector3 top = c + dir * halfH;
                Vector3 bottom = c - dir * halfH;
                DrawCircle(top, dir, r, 16, localToWorld, segments);
                DrawCircle(bottom, dir, r, 16, localToWorld, segments);
                // 连接两条边线
                var tangent1 = Quaternion.FromToRotation(Vector3.up, dir) * Vector3.right * r;
                var tangent2 = Quaternion.FromToRotation(Vector3.up, dir) * Vector3.forward * r;
                segments.Add(localToWorld.MultiplyPoint3x4(top + tangent1));
                segments.Add(localToWorld.MultiplyPoint3x4(bottom + tangent1));
                segments.Add(localToWorld.MultiplyPoint3x4(top + tangent2));
                segments.Add(localToWorld.MultiplyPoint3x4(bottom + tangent2));
            }
            else if (col is MeshCollider meshCol && meshCol.sharedMesh != null)
            {
                // 用网格三角形边画线框（直接画全部三角形边）
                var mesh = meshCol.sharedMesh;
                var verts = mesh.vertices;
                var tris = mesh.triangles;
                for (int i = 0; i < tris.Length; i += 3)
                {
                    DrawEdgeLoop(new[]
                    {
                        verts[tris[i]], verts[tris[i + 1]], verts[tris[i + 2]]
                    }, new[] { 0,1, 1,2, 2,0 }, localToWorld, segments);
                }
            }
        }

        private void DrawEdgeLoop(Vector3[] pts, int[] edges, Matrix4x4 localToWorld, List<Vector3> segments)
        {
            for (int i = 0; i < edges.Length; i += 2)
            {
                segments.Add(localToWorld.MultiplyPoint3x4(pts[edges[i]]));
                segments.Add(localToWorld.MultiplyPoint3x4(pts[edges[i + 1]]));
            }
        }

        private void DrawCircle(Vector3 center, Vector3 axis, float radius, int segments,
            Matrix4x4 localToWorld, List<Vector3> segmentList)
        {
            var q = Quaternion.FromToRotation(Vector3.up, axis);
            Vector3 prev = center + q * (Vector3.forward * radius);
            for (int i = 1; i <= segments; i++)
            {
                float a = (float)i / segments * Mathf.PI * 2f;
                Vector3 cur = center + q * (new Vector3(Mathf.Sin(a) * radius, 0f, Mathf.Cos(a) * radius));
                segmentList.Add(localToWorld.MultiplyPoint3x4(prev));
                segmentList.Add(localToWorld.MultiplyPoint3x4(cur));
                prev = cur;
            }
        }

        // ========== 右侧字段 ==========
        private void DrawRightInspector(Rect rect)
        {
            GUILayout.BeginArea(rect);
            using (var scope = new EditorGUILayout.ScrollViewScope(_rightScroll, GUILayout.ExpandHeight(true)))
            {
                _rightScroll = scope.scrollPosition;

                if (_selectedEditor == null)
                {
                    EditorGUILayout.HelpBox("请在左侧选择一个组件查看字段", MessageType.Info);
                    GUILayout.EndArea();
                    return;
                }

                EditorGUILayout.LabelField(_selectedTarget is Damageable
                        ? "Damageable 字段"
                        : "TransferDamageable 字段",
                    _groupHeaderStyle);
                EditorGUILayout.Space(2);

                _selectedEditor.OnInspectorGUI();

                // 若选中 Damageable，下方依次显示其关联的 TransferDamageable
                if (_selectedTarget is Damageable selectedDmg)
                {
                    DrawAssociatedTransfers(selectedDmg);
                }
            }
            GUILayout.EndArea();
        }

        private void DrawAssociatedTransfers(Damageable selectedDmg)
        {
            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("关联 TransferDamageable", _groupHeaderStyle);

            // LineArmor 为私有序列化字段，通过 SerializedObject 读取引用列表
            var lineArmor = _selectedEditor.serializedObject.FindProperty("LineArmor");
            if (lineArmor == null)
            {
                EditorGUILayout.HelpBox("未找到 LineArmor 字段", MessageType.Warning);
                return;
            }

            int count = lineArmor.arraySize;
            if (count == 0)
            {
                EditorGUILayout.HelpBox("该肢体未关联附属肢体", MessageType.None);
                return;
            }

            for (int i = 0; i < count; i++)
            {
                var element = lineArmor.GetArrayElementAtIndex(i);
                var td = element.objectReferenceValue as TransferDamageable;
                if (td == null)
                {
                    EditorGUILayout.HelpBox("第 " + i + " 项为空引用", MessageType.Warning);
                    continue;
                }

                // 缓存每个关联组件的 Editor
                var editor = GetAssocEditor(i, td);
                if (editor == null)
                {
                    EditorGUILayout.LabelField(td.gameObject.name + "（无字段）");
                    continue;
                }

                EditorGUILayout.BeginVertical(GUI.skin.box);
                EditorGUILayout.LabelField("◆ " + td.gameObject.name, EditorStyles.boldLabel);
                editor.OnInspectorGUI();
                EditorGUILayout.EndVertical();
            }
        }

        private UnityEditor.Editor GetAssocEditor(int index, TransferDamageable td)
        {
            if (_assocTargets != null && index < _assocTargets.Count &&
                ReferenceEquals(_assocTargets[index], td) && _assocEditors[index] != null)
            {
                return _assocEditors[index];
            }

            while (_assocTargets.Count <= index)
            {
                _assocTargets.Add(null);
                _assocEditors.Add(null);
            }

            if (_assocEditors[index] != null)
            {
                DestroyImmediate(_assocEditors[index]);
            }

            _assocTargets[index] = td;
            _assocEditors[index] = UnityEditor.Editor.CreateEditor(td);
            return _assocEditors[index];
        }

        // ========== 刷新与缓存管理 ==========
        private void RefreshStageIfChanged()
        {
            var stage = PrefabStageUtility.GetCurrentPrefabStage();
            GameObject newRoot = stage != null ? stage.prefabContentsRoot : null;
            int newVersion = stage != null ? stage.GetHashCode() : 0;

            if (newRoot != _stageRoot || newVersion != _stageVersion)
            {
                _stageRoot = newRoot;
                _stageVersion = newVersion;

                // 加载预制体资产用于模型预览（stage 实例的 Editor 没有预览 GUI，须对资产创建）
                _previewAsset = null;
                if (stage != null && !string.IsNullOrEmpty(stage.assetPath))
                {
                    _previewAsset = AssetDatabase.LoadAssetAtPath<GameObject>(stage.assetPath);
                }

                RefreshAll();
            }
        }

        private void RefreshAll()
        {
            DestroyCachedEditors();

            _damageables.Clear();
            _transfers.Clear();
            _selectedDamageableIndex = -1;
            _selectedTransferIndex = -1;

            if (_stageRoot == null) return;

            _damageables = _stageRoot.GetComponentsInChildren<Damageable>(true).ToList();
            _transfers = _stageRoot.GetComponentsInChildren<TransferDamageable>(true).ToList();
        }

        private void RebuildSelectedEditor(Object target)
        {
            DestroySelectedEditors();
            _selectedTarget = target;
            _selectedEditor = target != null ? UnityEditor.Editor.CreateEditor(target) : null;
        }

        private void DestroySelectedEditors()
        {
            if (_selectedEditor != null)
            {
                SafeDestroyEditor(_selectedEditor);
                _selectedEditor = null;
            }
            _selectedTarget = null;

            if (_assocEditors != null)
            {
                foreach (var e in _assocEditors)
                {
                    SafeDestroyEditor(e);
                }
            }
            _assocEditors.Clear();
            _assocTargets.Clear();
        }

        /// <summary>
        /// 安全销毁缓存的 Editor。若其 serializedObject 已被 Unity 释放，
        /// DestroyImmediate 触发 OnDisable 时可能抛 NRE，需捕获避免中断清理流程。
        /// </summary>
        private static void SafeDestroyEditor(UnityEditor.Editor editor)
        {
            if (editor == null) return;
            try
            {
                Object.DestroyImmediate(editor);
            }
            catch (System.Exception)
            {
                // 序列化对象已释放时忽略，避免清理流程被打断
            }
        }

        private void DestroyPreview()
        {
            if (_preview != null)
            {
                _preview.Cleanup();
                _preview = null;
            }
        }

        private void DestroyCachedEditors()
        {
            DestroySelectedEditors();
            DestroyPreview();
        }
    }
}
