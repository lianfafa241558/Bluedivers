using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

namespace FPSGame.EditorExt
{

public static class NormalizeMeshNormals
{
    [MenuItem("CONTEXT/MeshFilter/Normalize Mesh Normals")]
    private static void NormalizeMeshFilterNormals(MenuCommand command)
    {
        MeshFilter meshFilter = command.context as MeshFilter;
        if (meshFilter == null || meshFilter.sharedMesh == null) return;

        ApplyAndSave(meshFilter.sharedMesh);
    }

    [MenuItem("CONTEXT/SkinnedMeshRenderer/Normalize Mesh Normals")]
    private static void NormalizeSkinnedMeshNormals(MenuCommand command)
    {
        SkinnedMeshRenderer smr = command.context as SkinnedMeshRenderer;
        if (smr == null || smr.sharedMesh == null) return;

        ApplyAndSave(smr.sharedMesh);
    }

    private static void ApplyAndSave(Mesh mesh)
    {
        if (!BakeAverNormalToUV3(mesh))
        {
            Debug.LogWarning($"{mesh.name} 缺法线或切线，跳过");
            return;
        }

        EditorUtility.SetDirty(mesh);
        AssetDatabase.SaveAssets();
        Debug.Log($"Normalized normals for mesh: {mesh.name}");

        // FBX 内嵌 Mesh 是 ModelImporter 的产物，SetDirty 写不进盘，重导/重启即回滚
        if (AssetDatabase.IsSubAsset(mesh))
            Debug.LogWarning($"{mesh.name} 来自模型导入产物，此处改动不会持久；请用「Assets/美术/重导模型」重建 UV3");
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

    // 验证菜单项，确保菜单只在有效时显示
    [MenuItem("CONTEXT/MeshFilter/Normalize Mesh Normals", true)]
    private static bool ValidateMeshFilterNormals(MenuCommand command)
    {
        MeshFilter meshFilter = command.context as MeshFilter;
        return meshFilter != null && meshFilter.sharedMesh != null;
    }

    [MenuItem("CONTEXT/SkinnedMeshRenderer/Normalize Mesh Normals", true)]
    private static bool ValidateSkinnedMeshNormals(MenuCommand command)
    {
        SkinnedMeshRenderer smr = command.context as SkinnedMeshRenderer;
        return smr != null && smr.sharedMesh != null;
    }
}
}
