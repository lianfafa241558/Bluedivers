using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

public static class NormalizeMeshNormals
{
    [MenuItem("CONTEXT/MeshFilter/Normalize Mesh Normals")]
    private static void NormalizeMeshFilterNormals(MenuCommand command)
    {
        MeshFilter meshFilter = command.context as MeshFilter;
        if (meshFilter == null || meshFilter.sharedMesh == null) return;

        ProcessMesh(meshFilter.sharedMesh);
        EditorUtility.SetDirty(meshFilter.sharedMesh);
        Debug.Log($"Normalized normals for mesh: {meshFilter.sharedMesh.name}");
    }

    [MenuItem("CONTEXT/SkinnedMeshRenderer/Normalize Mesh Normals")]
    private static void NormalizeSkinnedMeshNormals(MenuCommand command)
    {
        SkinnedMeshRenderer smr = command.context as SkinnedMeshRenderer;
        if (smr == null || smr.sharedMesh == null) return;

        ProcessMesh(smr.sharedMesh);
        EditorUtility.SetDirty(smr.sharedMesh);
        Debug.Log($"Normalized normals for mesh: {smr.sharedMesh.name}");
    }

    private static void ProcessMesh(Mesh mesh)
    {
        // Calculate smoothed normals
        var averageNormalsHash = new Dictionary<Vector3, Vector3>();

        // 使用位置精度比较，避免浮点误差
        for (int i = 0; i < mesh.vertexCount; i++)
        {
            Vector3 vertex = mesh.vertices[i];
            Vector3 normal = mesh.normals[i];

            if (!averageNormalsHash.ContainsKey(vertex))
                averageNormalsHash.Add(vertex, normal);
            else
                averageNormalsHash[vertex] = (averageNormalsHash[vertex] + normal).normalized;
        }

        // Convert to Array
        Vector3[] averageNormals = new Vector3[mesh.vertexCount];
        for (int i = 0; i < mesh.vertexCount; i++)
        {
            averageNormals[i] = averageNormalsHash[mesh.vertices[i]];
        }

        // 将法线烘焙到贴图UV
        Vector2[] uv = new Vector2[mesh.vertexCount];
        Vector4[] tangents = mesh.tangents;
        Vector3[] normals = mesh.normals;

        // 检查是否有切线数据
        if (tangents == null || tangents.Length == 0)
        {
            Debug.LogWarning("Mesh has no tangents, recalculating...");
            mesh.RecalculateTangents();
            tangents = mesh.tangents;
        }

        for (int j = 0; j < mesh.vertexCount; j++)
        {
            Vector3 bitangent = (Vector3.Cross(normals[j], tangents[j]) * tangents[j].w).normalized;
            Vector3 bakeNormal = Vector3.Normalize(new Vector3(
                Vector3.Dot(tangents[j], averageNormals[j]),
                Vector3.Dot(bitangent, averageNormals[j]),
                Vector3.Dot(normals[j], averageNormals[j])));
            uv[j] = new Vector2(bakeNormal.x * 0.5f + 0.5f, bakeNormal.y * 0.5f + 0.5f);
        }

        mesh.SetUVs(3, uv); // uvIndex 3 是第4个UV通道
        mesh.RecalculateBounds();

        EditorUtility.SetDirty(mesh);
        AssetDatabase.SaveAssets();
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