using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using Core;



#if UNITY_EDITOR
using UnityEditor;
using UnityEditorInternal;
#endif

[CreateAssetMenu(fileName = "NT_", menuName = "Data/角色台词树")]
public class NoticeTree_SO : ScriptableObject
{
    public string SourceName;
    public Sprite Portrait;
    public bool UseResLoad;
    public List<NoticeData_SO> nodes = new List<NoticeData_SO>();
    public List<SoundGroup_SO> groups = new List<SoundGroup_SO>();

#if UNITY_EDITOR
    [ContextMenu("转换为SoundGroup子资源")]
    public void ConvertToSoundGroups()
    {
        NoticeTree_SO sourceTree = this;
        if (sourceTree == null)
        {
            Debug.LogError("转换失败：目标对象不是 NoticeTree_SO！");
            return;
        }

        if (sourceTree.nodes == null || sourceTree.nodes.Count == 0)
        {
            Debug.LogWarning($"台词树 '{sourceTree.name}' 没有任何节点！");
            return;
        }

        // 按 Type 分组
        var groupedNodes = sourceTree.nodes
            .Where(node => node != null && !string.IsNullOrEmpty(node.Type))
            .GroupBy(node => node.Type)
            .ToDictionary(g => g.Key, g => g.ToList());

        if (groupedNodes.Count == 0)
        {
            Debug.LogWarning("没有找到有效 Type 的节点！");
            return;
        }

        // 获取原资产路径
        string sourcePath = AssetDatabase.GetAssetPath(sourceTree);

        

        AssetDatabase.ImportAsset(sourcePath);

        int createdCount = 0;
        List<SoundGroup_SO> createdGroups = new List<SoundGroup_SO>();

        foreach (var kvp in groupedNodes)
        {
            string typeName = kvp.Key;
            List<NoticeData_SO> nodesOfType = kvp.Value;

            // 创建 SoundGroup_SO 实例
            SoundGroup_SO soundGroup = CreateInstance<SoundGroup_SO>();
            soundGroup.name = $"{sourceTree.name}_{typeName}";
            soundGroup.clips = new List<SoundItem>();
            soundGroup.group = default;
            soundGroup.flags = SoundGroup_SO.SoundFlag.Space;
            soundGroup.groupName = typeName;
            // 将 NoticeData 转换为 SoundItem
            foreach (var node in nodesOfType)
            {
                if (node.Clip == null) continue;

                SoundItem item = new SoundItem {
                    audioClip = node.Clip,
                    subtitle = node.Desc ?? string.Empty
                };
                soundGroup.clips.Add(item);
            }

            if (soundGroup.clips.Count == 0)
            {
                Debug.LogWarning($"类型 '{typeName}' 没有有效的 AudioClip，跳过生成。");
                DestroyImmediate(soundGroup);
                continue;
            }

            // 从第一个节点继承属性
            var firstNode = nodesOfType.First();
            soundGroup.priority = firstNode.Priority;
            if (firstNode.Space)
            {
                soundGroup.flags |= SoundGroup_SO.SoundFlag.Space;
            }
            else
            {
                soundGroup.flags &= ~SoundGroup_SO.SoundFlag.Space;
            }

            // 添加为子资源
            AssetDatabase.AddObjectToAsset(soundGroup, sourcePath);
            createdGroups.Add(soundGroup);
            createdCount++;


            // 删除现有的所有 NoticeData_SO 子资源
            var existingSubAssets = AssetDatabase.LoadAllAssetsAtPath(sourcePath)
                .Where(asset => asset != sourceTree && asset is NoticeData_SO)
                .ToArray();

            foreach (var subAsset in existingSubAssets)
            {
                DestroyImmediate(subAsset, true);
            }

            Debug.Log($"已添加子资源: {soundGroup.name} (包含 {soundGroup.clips.Count} 个音频，Type: {typeName})");
        }
        groups = createdGroups;
        nodes = null;

        // 保存并刷新
        EditorUtility.SetDirty(sourceTree);
        AssetDatabase.SaveAssets();
        AssetDatabase.ImportAsset(sourcePath);

        // 选中源文件，在检视面板中可以看到子资源
        Selection.activeObject = sourceTree;

        Debug.Log($"转换完成！共生成 {createdCount} 个 SoundGroup 子资源，已添加到源文件 '{sourceTree.name}' 中。");
    }


    [ContextMenu("删除所有SoundGroup子资源")]
    private void DeleteAllSoundGroupSubAssets()
    {
        NoticeTree_SO sourceTree = this;
        if (sourceTree == null)
        {
            Debug.LogError("操作失败：目标对象不是 NoticeTree_SO！");
            return;
        }

        // 获取原资产路径
        string sourcePath = AssetDatabase.GetAssetPath(sourceTree);

        // 查找所有 SoundGroup_SO 子资源
        var soundGroupSubAssets = AssetDatabase.LoadAllAssetsAtPath(sourcePath)
            .Where(asset => asset != sourceTree && asset is NoticeData_SO)
            .ToArray();

        if (soundGroupSubAssets.Length == 0)
        {
            Debug.Log($"文件 '{sourceTree.name}' 中没有找到任何 SoundGroup 子资源。");
            return;
        }

        // 确认删除对话框
        if (!EditorUtility.DisplayDialog("确认删除",
            $"确定要删除文件 '{sourceTree.name}' 中的 {soundGroupSubAssets.Length} 个 SoundGroup 子资源吗？\n\n此操作不可撤销！",
            "确认删除", "取消"))
        {
            return;
        }


        foreach (var subAsset in soundGroupSubAssets)
        {
            DestroyImmediate(subAsset, true);
            //Debug.Log($"已删除子资源: {subAsset.name}");
        }

        // 保存并刷新
        EditorUtility.SetDirty(sourceTree);
        AssetDatabase.SaveAssets();
        AssetDatabase.ImportAsset(sourcePath);

        // 选中源文件
        Selection.activeObject = sourceTree;

        Debug.Log($"已删除 {soundGroupSubAssets.Length} 个 SoundGroup 子资源。");
    }


    /*
    [ContextMenu("添加节点")]
    public NoticeData_SO CreateNode()
    {
        NoticeData_SO node = CreateInstance<NoticeData_SO>();
        node.name = $"Notice_{nodes.Count + 1}";
        Undo.RecordObject(this, "Add Notice Node");
        nodes.Add(node);
        node.SourceName = SourceName;
        node.Portrait = Portrait;
        if (!Application.isPlaying)
        {
            AssetDatabase.AddObjectToAsset(node, this);
            EditorUtility.SetDirty(this);
            AssetDatabase.SaveAssets();
        }
        return node;
    }

    [ContextMenu("删除最后节点")]
    public void DeleteNode()
    {
        if (nodes.Count == 0)
        {
            Debug.LogWarning("没有可删除的节点");
            return;
        }

        NoticeData_SO node = nodes[nodes.Count - 1];
        Undo.RecordObject(this, "Remove Notice Node");
        nodes.RemoveAt(nodes.Count - 1);

        if (!Application.isPlaying)
        {
            AssetDatabase.RemoveObjectFromAsset(node);
            EditorUtility.SetDirty(this);
            AssetDatabase.SaveAssets();
        }

        Debug.Log($"已删除节点: {node.name}");
    }*/
#endif
}




/*
#if UNITY_EDITOR
[CustomEditor(typeof(NoticeTree_SO))]
public class NoticeTree_SOEditor : Editor
{
    private SerializedProperty nodesProperty;
    private Vector2 scrollPos;
    private List<bool> foldoutStates = new();

    private void OnEnable()
    {
        nodesProperty = serializedObject.FindProperty("nodes");
        UpdateFoldoutStates();
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.LabelField("台词节点管理", EditorStyles.boldLabel);
        EditorGUILayout.BeginHorizontal();
        // 添加节点按钮
        if (GUILayout.Button("添加新节点", GUILayout.Height(24)))
        {
            AddNewNode();
        }
        // 添加节点按钮
        if (GUILayout.Button("展开", GUILayout.Height(24)))
        {
            Expand(true);
        }
        if (GUILayout.Button("折叠", GUILayout.Height(24)))
        {
            Expand(false);
        }
        // 添加节点按钮
        if (GUILayout.Button("刷新", GUILayout.Height(24)))
        {
            Refresh();
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.PropertyField(serializedObject.FindProperty("SourceName"), new GUIContent("名称"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("Portrait"), new GUIContent("头像"));
        var UseResLoad = serializedObject.FindProperty("UseResLoad");
        EditorGUILayout.PropertyField(UseResLoad, new GUIContent("使用资源管理器读取"));

        EditorGUILayout.Space(20);
        scrollPos = EditorGUILayout.BeginScrollView(scrollPos);

        // 显示节点列表
        for (int i = 0; i < nodesProperty.arraySize; i++)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            SerializedProperty nodeProperty = nodesProperty.GetArrayElementAtIndex(i);
            var node = nodeProperty.objectReferenceValue as NoticeData_SO;

            if (node == null) continue;
            if (foldoutStates.Count <= i)foldoutStates.Add(false);
             // 节点折叠面板
             foldoutStates[i] = EditorGUILayout.Foldout(foldoutStates[i], node.Desc, true);

            if (foldoutStates[i])
            {
                EditorGUI.indentLevel++;

                // 显示Desc字段
                EditorGUI.BeginChangeCheck();
                string newDesc = EditorGUILayout.TextField("描述", node.Desc);
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(node, "Modify Desc");
                    node.Desc = newDesc;
                    node.name = node.SourceName+": "+newDesc;
                    
                    EditorUtility.SetDirty(node);
                    EditorUtility.SetDirty(target);
                    AssetDatabase.SaveAssets();
                }

                // 显示Clip字段
                EditorGUI.BeginChangeCheck();
                AudioClip newClip = (AudioClip)EditorGUILayout.ObjectField("音频", node.Clip, typeof(AudioClip), false);
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(node, "Modify Clip");
                    node.Clip = newClip;
                    EditorUtility.SetDirty(node);
                }

                if (UseResLoad.boolValue) {
                    // 显示Type字段
                    EditorGUI.BeginChangeCheck();
                    string newType = EditorGUILayout.TextField("分类", node.Type);
                    if (EditorGUI.EndChangeCheck())
                    {
                        Undo.RecordObject(node, "Modify Desc");
                        node.Type = newType;
                        EditorUtility.SetDirty(node);
                        EditorUtility.SetDirty(target);
                        AssetDatabase.SaveAssets();
                    } 
                }

                EditorGUI.BeginChangeCheck();
                string newPriority = EditorGUILayout.TextField("优先级", ""+node.Priority);
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(node, "Modify Desc");
                    if (int.TryParse(newPriority,out int number))
                    {
                        node.Priority = number;
                    }
                    
                    EditorUtility.SetDirty(node);
                    EditorUtility.SetDirty(target);
                    AssetDatabase.SaveAssets();
                }

                EditorGUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();

                if (i < nodesProperty.arraySize - 1 && GUILayout.Button("下移", GUILayout.Width(60)))
                {
                    nodesProperty.MoveArrayElement(i, i + 1);
                    nodesProperty.serializedObject.ApplyModifiedProperties();
                }
                if (i > 0 && GUILayout.Button("上移", GUILayout.Width(60)))
                {
                    nodesProperty.MoveArrayElement(i, i - 1);
                    nodesProperty.serializedObject.ApplyModifiedProperties();
                }
                if (GUILayout.Button("删除", GUILayout.Width(60)))
                {
                    RemoveNode(i);
                    EditorGUI.indentLevel--;
                    break; // 退出循环避免索引越界
                }
                EditorGUILayout.EndHorizontal();
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.EndVertical();
        }

        EditorGUILayout.EndScrollView();

        serializedObject.ApplyModifiedProperties();
    }

    private void RemoveNode(int index)
    {
        NoticeTree_SO tree = (NoticeTree_SO)target;
        Undo.RecordObject(tree, "Remove Node");

        SerializedProperty nodeProperty = nodesProperty.GetArrayElementAtIndex(index);
        NoticeData_SO node = (NoticeData_SO)nodeProperty.objectReferenceValue;

        tree.nodes.RemoveAt(index);
        AssetDatabase.RemoveObjectFromAsset(node);

        UpdateFoldoutStates();
        AssetDatabase.SaveAssets();
        EditorUtility.SetDirty(tree);
    }
    private void AddNewNode()
    {
        NoticeTree_SO tree = (NoticeTree_SO)target;
        Undo.RecordObject(tree, "Add Node");
        tree.CreateNode();
        tree.nodes = new List<NoticeData_SO>(AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GetAssetPath(tree)).Where(x => x is NoticeData_SO).Cast<NoticeData_SO>().Reverse());

        EditorUtility.SetDirty(tree);
    }
    private void Expand(bool state)
    {
        for (int i = 0; i < nodesProperty.arraySize; i++)
        {
            foldoutStates[i] = state;
        }
    }
    private void Refresh()
    {
        NoticeTree_SO tree = (NoticeTree_SO)target;

        for (int i = 0; i < nodesProperty.arraySize; i++)
        {
            SerializedProperty nodeProperty = nodesProperty.GetArrayElementAtIndex(i);
            var node = nodeProperty.objectReferenceValue as NoticeData_SO;

            node.SourceName = tree.SourceName;
            node.Portrait = tree.Portrait;
            node.name = node.SourceName + ": " + node.Desc;
            EditorUtility.SetDirty(node);
            EditorUtility.SetDirty(target);
            
        }
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    private void UpdateFoldoutStates()
    {
        while (foldoutStates.Count < nodesProperty.arraySize)
        {
            foldoutStates.Add(false);
        }
    }
}

#endif*/