using UnityEngine;
using System.Collections.Generic;
using System.Linq;


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

#if UNITY_EDITOR
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
    }
#endif
}





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

#endif