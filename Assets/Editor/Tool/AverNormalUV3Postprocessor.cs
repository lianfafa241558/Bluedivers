using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace FPSGame.EditorExt
{

    /// <summary>
    /// 把描边用的平均法线烘进 Mesh 的 UV3（= SetUVs(3) = 第 4 通道 = TEXCOORD3 = mesh.uv4，shader 里叫 uvAver），
    /// 不动 mesh.normals 本身。必须在导入回调里做：FBX 内嵌 Mesh 是 ModelImporter 的产物，每次重导按 FBX 重建，
    /// 只有写进导入产物才不会回滚。核对时读 mesh.uv4 / GetUVs(3, …)，mesh.uv3 是 channel 2。
    /// </summary>
    public class AverNormalUV3Postprocessor : AssetPostprocessor
    {
        // UV3 是额外顶点通道（每顶点 8 字节），只给美术模型；要扩大范围往这里加
        private static readonly string[] ModelRoots = { "Assets/Art/" };

        // 个别模型不需要（法线本身很碎，平均后描边反而更糟）：填路径片段
        private static readonly string[] SkipPaths = { };

        private void OnPostprocessModel(GameObject root)
        {
            if (!MatchAny(assetPath, ModelRoots)) return;
            if (MatchAny(assetPath, SkipPaths)) return;

            var baked = new HashSet<Mesh>(); // 同一 Mesh 可能被多个渲染器共用
            foreach (MeshFilter meshFilter in root.GetComponentsInChildren<MeshFilter>(true))
            {
                Bake(meshFilter.sharedMesh, baked);
            }

            foreach (SkinnedMeshRenderer smr in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                Bake(smr.sharedMesh, baked);
            }
        }

        // 新加/改了本脚本不会自动重导旧模型，用这个入口按选择补烘（可选中整个文件夹）
        [MenuItem("Assets/美术/重导模型（重建 AverNormal UV3）", false, 40)]
        private static void ReimportSelectedModels()
        {
            var models = new List<string>();
            foreach (UnityEngine.Object obj in Selection.objects)
            {
                string path = AssetDatabase.GetAssetPath(obj);
                if (AssetDatabase.IsValidFolder(path))
                {
                    foreach (string guid in AssetDatabase.FindAssets("t:Model", new[] { path }))
                    {
                        models.Add(AssetDatabase.GUIDToAssetPath(guid));
                    }
                }
                else if (AssetImporter.GetAtPath(path) is ModelImporter)
                {
                    models.Add(path);
                }
            }

            if (models.Count == 0)
            {
                Debug.LogWarning("没选中模型或模型文件夹");
                return;
            }

            try
            {
                for (int i = 0; i < models.Count; i++)
                {
                    EditorUtility.DisplayProgressBar("重导模型", models[i], (float)i / models.Count);
                    AssetDatabase.ImportAsset(models[i], ImportAssetOptions.ForceUpdate);
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            Debug.Log($"已重导 {models.Count} 个模型，AverNormal UV3 已重建");
        }

        private static void Bake(Mesh mesh, HashSet<Mesh> baked)
        {
            if (mesh == null || !baked.Add(mesh)) return;

            // 没法线/切线时只影响描边方向，不该中断整个模型的导入
            if (!BakeAverNormalToUV3(mesh))
            {
                Debug.LogWarning($"{mesh.name} 缺法线或切线，跳过 AverNormal UV3");
            }
        }

        private static bool MatchAny(string path, string[] keys)
        {
            foreach (string key in keys)
            {
                if (path.Contains(key)) return true;
            }

            return false;
        }


        /// <summary>
        /// 把平均法线烘到 UV3（切线空间，xy*0.5+0.5），不动 mesh.normals 本身。
        /// 返回 false 表示缺法线/切线，没有烘。导入回调与右键菜单共用这一份实现。
        /// </summary>
        public static bool BakeAverNormalToUV3(Mesh mesh)
        {
            int count = mesh.vertexCount;
            Vector3[] normals = mesh.normals;
            if (count == 0 || normals == null || normals.Length != count) return false;

            Vector4[] tangents = mesh.tangents;
            if (tangents == null || tangents.Length != count)
            {
                mesh.RecalculateTangents(); // 没切线算不出切线空间
                tangents = mesh.tangents;
                if (tangents.Length != count) return false;
            }

            // 同一位置的多个顶点（硬边/UV 接缝）取平均，得到描边要的"平滑法线"
            Vector3[] vertices = mesh.vertices;
            var averageNormalsHash = new Dictionary<Vector3, Vector3>();
            for (int i = 0; i < count; i++)
            {
                Vector3 vertex = vertices[i];
                if (!averageNormalsHash.TryGetValue(vertex, out Vector3 average))
                    averageNormalsHash.Add(vertex, normals[i]);
                else
                    averageNormalsHash[vertex] = (average + normals[i]).normalized;
            }

            Vector2[] uv = new Vector2[count];
            for (int j = 0; j < count; j++)
            {
                Vector3 averageNormal = averageNormalsHash[vertices[j]];
                Vector4 tangent = tangents[j];
                Vector3 bitangent = (Vector3.Cross(normals[j], tangent) * tangent.w).normalized;
                Vector3 bakeNormal = Vector3.Normalize(new Vector3(
                    Vector3.Dot(tangent, averageNormal),
                    Vector3.Dot(bitangent, averageNormal),
                    Vector3.Dot(normals[j], averageNormal)));
                // shader 的 UnpackNormalRG 只还原 xy（z 恒正），所以只存 xy
                uv[j] = new Vector2(bakeNormal.x * 0.5f + 0.5f, bakeNormal.y * 0.5f + 0.5f);
            }

            // uvIndex 3 = 第 4 个通道 = TEXCOORD3 = shader 的 uvAver；核对要读 mesh.uv4（mesh.uv3 是 channel 2）
            mesh.SetUVs(3, uv);
            mesh.RecalculateBounds();
            return true;
        }

    }
}
