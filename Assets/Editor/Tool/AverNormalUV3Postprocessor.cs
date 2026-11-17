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
        if (!NormalizeMeshNormals.BakeAverNormalToUV3(mesh))
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
}
}
