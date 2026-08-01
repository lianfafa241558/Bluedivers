using System.Collections.Generic;
using System.IO;
using System.Text;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;
using static Core.InputManagerBase<InputState, Core.WindowStateEnum>;

using Core;
using Tool = Utils.Tool;
using Core.Interface;

//这个类完全是为了自定义编辑器才有的
public class InputManager : InputManagerBase<InputState, WindowStateEnum>,I_GlobaManager
{
    [System.Serializable]
    private class InputListSaveData
    {
        public List<InputItem> items;
    }

    private const string SaveFileName = "inputBindings.json";

    //留着切界面用的
    public WindowStateEnum nowState;
    public List<InputItem> InputList => inputList;

    public override WindowStateEnum NowWindowState => WndManager.WindowState;

    /// <summary>
    /// 获取存档目录路径
    /// </summary>
    private static string GetSaveDirectory()
    {
#if UNITY_ANDROID
        return Application.persistentDataPath + "/";
#elif UNITY_STANDALONE_WIN
        string path = Application.dataPath + "/../";
#if UNITY_EDITOR
        path += "/../";
#endif
        return path;
#endif
    }


    /// <summary>
    /// 从JSON文件加载输入配置，若文件不存在则以默认值生成(由root触发)
    /// </summary>
    public void Init()
    {
        string filePath = GetSaveDirectory() + SaveFileName;

        if (File.Exists(filePath))
        {
            string jsonStr = File.ReadAllText(filePath);
            var saveData = JsonUtility.FromJson<InputListSaveData>(jsonStr);
            if (saveData != null && saveData.items != null && saveData.items.Count > 0)
            {
                // 备份默认配置用于比对
                var defaultList = new List<InputItem>(inputList);
                inputList = saveData.items;

                // 对比：默认有但存档没有的条目，补充进去
                bool hasNewEntries = false;
                foreach (var defaultItem in defaultList)
                {
                    if (defaultItem == null) continue;
                    if (!inputList.Exists(loadedItem =>
                        loadedItem != null &&
                        loadedItem.key.Equals(defaultItem.key) &&
                        loadedItem.window.Equals(defaultItem.window)))
                    {
                        inputList.Add(defaultItem);
                        hasNewEntries = true;
                        Debug.Log($"输入配置补充新条目: {defaultItem.key} (界面: {defaultItem.window})");
                    }
                }

                if (hasNewEntries)
                {
                    Save();
                    Debug.Log("输入配置已更新（补充新条目）并保存");
                }

                Debug.Log("输入配置已从文件加载: " + filePath);
            }
        }
        else
        {
            Save();
            Debug.Log("生成默认输入配置文件: " + filePath);
        }

        // 重置运行时状态
        foreach (var item in inputList)
        {
            if (item != null)
            {
                item.lastTime = Mathf.Infinity;
            }
        }
    }

    public void UnInit()
    {

    }

    /// <summary>
    /// 保存当前输入配置到JSON文件
    /// </summary>
    public void Save()
    {
        string dir = GetSaveDirectory();
        if (!Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }

        var saveData = new InputListSaveData { items = inputList };
        string jsonStr = JsonUtility.ToJson(saveData, true);
        string filePath = dir + SaveFileName;
        File.WriteAllText(filePath, jsonStr, new UTF8Encoding(true));
    }

}


public enum InputState
{
    [InspectorName("设置界面")] Esc,
    [InspectorName("水平")] Horizontal,
    [InspectorName("垂直")] Vertical,
    [InspectorName("奔跑")] Shift,
    [InspectorName("跳跃")] Jump,
    [InspectorName("交互")] Operate,
    [InspectorName("投掷")] Throw,
    [InspectorName("装弹")] Reload,
    [InspectorName("开火")] Fire,
    [InspectorName("瞄准")] Aim,
    [InspectorName("下蹲")] Crouch,

    [InspectorName("武器1")] Weapon1,
    [InspectorName("武器2")] Weapon2,
    [InspectorName("武器3")] Weapon3,
    [InspectorName("武器4")] Weapon4,

    [InspectorName("战备面板")] Airdrop,
    [InspectorName("左")] Left,
    [InspectorName("上")] Up,
    [InspectorName("右")] Right,
    [InspectorName("下")] Down,

    [InspectorName("上升")] Rise,
    [InspectorName("下降")] Fall,
    [InspectorName("丢弃装备")] Equip,

    [InspectorName("呼叫Kei")] Mule,

    [InspectorName("显示-隐藏")] H,
    [InspectorName("暂停-运行")] J,

    [InspectorName("加速")] Acceler,
    [InspectorName("减速")] Deceler,

    [InspectorName("小地图")] MiniMap,

    [InspectorName("切换视角")] Perspective,
}

#if UNITY_EDITOR
[CustomEditor(typeof(InputManager))]
class InputManagerEditor : Editor
{
    private int foldCount = 2;
    private bool[][] foldouts;
    private InputManager classData;


    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        classData = (InputManager)target;

        GUIContent content = EditorGUIUtility.TrTextContent("添加类型", "添加一个类型", "PlayButton");
        GUIContent content2 = EditorGUIUtility.TrTextContent("移除类型", "移除最后一个类型", "PlayButton");
        GUIContent content3 = EditorGUIUtility.TrTextContent("整理类型", "整理类型", "PlayButton");
        GUIContent content4 = EditorGUIUtility.TrTextContent("继承", "继承类型", "PlayButton");

        SerializedProperty data = serializedObject.FindProperty("inputList");
        SerializedProperty state = serializedObject.FindProperty("nowState");
        //KVP<InputState, InputItem> data = (KVP<InputState, InputItem>)Convert.ChangeType(target, typeof(KVP<InputState, InputItem>));

        float originalLabelWidth = EditorGUIUtility.labelWidth;
        EditorGUIUtility.labelWidth = 80; // 设置标签宽度，即不显示标签文本
        EditorGUILayout.PropertyField(state, new GUIContent("界面"), GUILayout.Width(200));
        EditorGUIUtility.labelWidth = originalLabelWidth; // 恢复原始标签宽度

        if (foldouts == null)
        {
            foldouts = new bool[data.arraySize][];
        }

        for (int i = 0; i < data.arraySize; ++i)
            if (foldouts[i] == null || foldouts[i].Length != foldCount) foldouts[i] = new bool[foldCount];

        EditorGUI.indentLevel += 2;

        for (int i = 0; i < data.arraySize; i++)
        {
            SerializedProperty elementProp = data.GetArrayElementAtIndex(i);
            SerializedProperty typeProp = elementProp.FindPropertyRelative("key");
            SerializedProperty window = elementProp.FindPropertyRelative("window");
            if (state.intValue != window.intValue) continue;

            EditorGUILayout.Space();
            //Debug.LogWarning("?" + i + "个数" + foldouts[i][0]);
            foldouts[i][0] = EditorGUILayout.Foldout(foldouts[i][0], (Tool.GetEnumString((InputState)typeProp.intValue)));
            if (!foldouts[i][0])
            {

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.PropertyField(typeProp, new GUIContent(), true, GUILayout.Width(100));
                SerializedProperty positivemainValueProp = elementProp.FindPropertyRelative("positiveMainValue");
                SerializedProperty positivespareValueProp = elementProp.FindPropertyRelative("positiveSpareValue");
                SerializedProperty negativeMainValueProp = elementProp.FindPropertyRelative("negativeMainValue");
                SerializedProperty negativeMpareValueProp = elementProp.FindPropertyRelative("negativeSpareValue");

                EditorGUIUtility.labelWidth = 100; // 设置标签宽度，即不显示标签文本
                EditorGUILayout.PropertyField(positivemainValueProp, new GUIContent("主键"), GUILayout.Width(200));
                EditorGUILayout.PropertyField(positivespareValueProp, new GUIContent("备用键"), GUILayout.Width(200));
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.BeginHorizontal();
                GUILayout.Space(105);
                EditorGUILayout.PropertyField(negativeMainValueProp, new GUIContent("主键/否键"), GUILayout.Width(200));
                EditorGUILayout.PropertyField(negativeMpareValueProp, new GUIContent("备用键/否键"), GUILayout.Width(200));

                EditorGUIUtility.labelWidth = originalLabelWidth; // 恢复原始标签宽度
                EditorGUILayout.EndHorizontal();


            }

            EditorGUILayout.EndFoldoutHeaderGroup();
            //EditorGUILayout.PropertyField(releaseProp);
        }
        EditorGUI.indentLevel -= 2;

        EditorGUILayout.Space(20);

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button(content))
        {
            classData.InputList.Add(new InputItem() { window = classData.nowState });
            foldouts = new bool[classData.InputList.Count][];
            ReSet(data);
        }
        if (GUILayout.Button(content2))
        {
            classData.InputList.RemoveAt(classData.InputList.Count - 1);
            ReSet(data);
        }
        if (GUILayout.Button(content3))
        {
            ArrangeData(classData.InputList);
            ReSet(data);
        }
        EditorGUILayout.EndHorizontal();
        serializedObject.ApplyModifiedProperties();

    }


    public void ReSet(SerializedProperty data)
    {
        for (int i = 0; i < data.arraySize; ++i)
            if (foldouts[i] == null || foldouts[i].Length != foldCount) foldouts[i] = new bool[foldCount];
    }

    public void ArrangeData(List<InputItem> data)
    {
        InputItem tmp;
        for (int i = 0; i < data.Count; ++i) if (data[i] == null || data[i].positiveMainValue == 0) data.RemoveAt(i--);

        for (int i = 0, l = data.Count; i < l; ++i)
        {
            for (int j = i; j < l; ++j)
            {
                if (data[i].key > data[j].key)
                {
                    tmp = data[j];
                    data[j] = data[i];
                    data[i] = tmp;
                }
            }
        }
    }
}
#endif
