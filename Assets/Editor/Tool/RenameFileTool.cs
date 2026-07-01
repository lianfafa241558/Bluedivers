using System.Linq;
using UnityEditor;
using UnityEngine;

public class RenameFile : EditorWindow
{

    //[MenuItem("Assets/批量修改文件名字（后缀序号排序）", false, 2)]
    [MenuItem("Tools/批量修改文件名字")]
    public static void SetTextureName()
    {
        head_name = Selection.objects[0].name;
        window = GetWindow<RenameFile>(false, "批量修改文件名");
        window.Show();
    }
    static EditorWindow window;
    Object[] chooseObj;
    string[] oldName;
    static string head_name; //前缀名
    int frist_id = 1; //从这个序号开始排序
    int add_num = 1;  //增量 - 
    int numCoune = 1; //序号的位数
    string[] prefixType = new string[] { "[self]#", "[self]_#", "#_[self]", "[self].#", "#.[self]" };
    string[] prefixType2 = new string[] { "[self]#1_#2", "[self]_#1_#2" };
    int prefixType1_id = 1;
    int prefixType2_id = 0;
    bool foldout = true;

    string[] toolbarValues = { "后缀单排序", "双排序", "替换" , "替换为排序","前缀" };
    int toolbarIndex = 0;
    private void OnGUI()
    {
        GUILayout.Space(30);
        toolbarIndex = GUILayout.Toolbar(toolbarIndex, toolbarValues, GUILayout.Height(30));
        GUILayout.Space(30);
        if (toolbarIndex == 0) //后缀排序
        {
            PrefixSort();
        }
        else if (toolbarIndex == 1)
        {
            PrefixSort2();
        }
        else if (toolbarIndex == 2)
        {
            Replace();
        }
        else if (toolbarIndex == 3)
        {
            ReplaceSort();
        }
        else if (toolbarIndex == 4)
        {
            SetPrefix();
        }
    }
    //后缀 排序
    void PrefixSort()
    {
        head_name = EditorGUILayout.TextField("前缀", head_name);
        frist_id = EditorGUILayout.IntField("初始数字", frist_id);
        add_num = EditorGUILayout.IntField("增量", add_num);
        numCoune = EditorGUILayout.IntField("序号的位数", numCoune);
        GUILayout.Space(30);
        GUILayout.Label("选择序号类型，[self]为前缀，# 代表编号");
        prefixType1_id = GUILayout.Toolbar(prefixType1_id, prefixType, GUILayout.Height(25));

        if (GUILayout.Button("确认修改", GUILayout.Height(40)))
        {
            if (EditorUtility.DisplayDialog("是否修改", "修改后可通过返回修改返回，但只能返回一次, 是否修改", "确定", "取消"))
            {
                if (head_name == "")
                {
                    Debug.LogError("操作失误,前缀不能为空，如不需要前缀，请填‘#’");
                    return;
                }
                //记录一下，避免点错了
                chooseObj = Selection.objects;
                oldName = new string[chooseObj.Length];
                for (int i = 0; i < chooseObj.Length; i++)
                {
                    oldName[i] = chooseObj[i].name;
                }
                int temp = 0;
                foreach (var obj in Selection.objects)
                {
                    AssetDatabase.RenameAsset(AssetDatabase.GetAssetPath(obj), GetNewName(temp));
                    temp++;
                }
            }
        }
        if (GUILayout.Button("返回修改（每次修改只能返回一次）", GUILayout.Height(40)))
        {
            if (EditorUtility.DisplayDialog("取消修改修改", "取消刚刚的修改", "确定", "取消"))
            {
                if (chooseObj == null || oldName == null || oldName.Length == 0)
                {
                    Debug.LogError("还没开始修改");
                }
                for (int i = 0; i < chooseObj.Length; i++)
                {
                    AssetDatabase.RenameAsset(AssetDatabase.GetAssetPath(chooseObj[i]), oldName[i]);
                }
            }
        }
        GUILayout.Space(30);
        foldout = EditorGUILayout.Foldout(foldout, "文件名预览：");
        if (foldout)
        {
            GUILayout.Label($"{GetNewName(0)}");
            GUILayout.Label($"{GetNewName(1)}");
            GUILayout.Label($"{GetNewName(2)}");
            GUILayout.Label($"{GetNewName(3)}");
            GUILayout.Label($"{GetNewName(4)}");
            GUILayout.Label($"{GetNewName(5)}");
            GUILayout.Label($"{GetNewName(6)}");
        }

        string GetNewName(int count)
        {
            string tempName = head_name;
            if (head_name == "#" || head_name == "＃")
            {
                tempName = "";
            }
            int curID = frist_id + count * add_num;
            string curID_str = curID.ToString();
            if (numCoune > curID_str.Length)
            {
                for (int i = curID_str.Length; i < numCoune; i++)
                {
                    curID_str = "0" + curID_str;
                }
            }
            switch (prefixType1_id)
            {
                case 0: return $"{tempName}{curID_str}";
                case 1: return $"{tempName}_{curID_str}";
                case 2: return $"{curID_str}_{tempName}";
                case 3: return $"{tempName}.{curID_str}";
                case 4: return $"{curID_str}.{tempName}";
                default: return "错误";
            }
        }
    }

    int frist2_id = 1;
    int add_num2 = 1;
    int numCoune2 = 1;
    int maxNum2 = 2;
    //循环 排序
    void PrefixSort2()
    {
        head_name = EditorGUILayout.TextField("前缀", head_name);
        frist_id = EditorGUILayout.IntField("序号1初始值", frist_id);
        add_num = EditorGUILayout.IntField("序号1增量", add_num);
        numCoune = EditorGUILayout.IntField("序号1的位数", numCoune);
        GUILayout.Space(20);
        frist2_id = EditorGUILayout.IntField("序号2初始值", frist2_id);
        add_num2 = EditorGUILayout.IntField("序号2增量", add_num2);
        maxNum2 = EditorGUILayout.IntField("序号2最大值", maxNum2);
        numCoune2 = EditorGUILayout.IntField("序号2的位数", numCoune2);
        GUILayout.Space(30);
        GUILayout.Label("选择序号类型，[self]为前缀，# 代表编号");
        prefixType2_id = GUILayout.Toolbar(prefixType2_id, prefixType2, GUILayout.Height(25));

        if (GUILayout.Button("确认修改", GUILayout.Height(40)))
        {
            if (EditorUtility.DisplayDialog("是否修改", "修改后可通过返回修改返回，但只能返回一次, 是否修改", "确定", "取消"))
            {
                if (head_name == "")
                {
                    Debug.LogError("操作失误,前缀不能为空，如不需要前缀，请填‘#’");
                    return;
                }
                //记录一下，避免点错了
                chooseObj = Selection.objects;
                oldName = new string[chooseObj.Length];
                for (int i = 0; i < chooseObj.Length; i++)
                {
                    oldName[i] = chooseObj[i].name;
                }
                int temp1 = 0;
                int temp2 = 0;
                foreach (var obj in Selection.objects)
                {
                    AssetDatabase.RenameAsset(AssetDatabase.GetAssetPath(obj), GetNewName(temp1, temp2));
                    temp2++;
                    if (temp2 >= maxNum2)
                    {
                        temp1++;
                        temp2 = 0;
                    }
                }
            }
        }
        if (GUILayout.Button("返回修改（每次修改只能返回一次）", GUILayout.Height(40)))
        {
            if (EditorUtility.DisplayDialog("取消修改修改", "取消刚刚的修改", "确定", "取消"))
            {
                if (chooseObj == null || oldName == null || oldName.Length == 0)
                {
                    Debug.LogError("还没开始修改");
                }
                for (int i = 0; i < chooseObj.Length; i++)
                {
                    AssetDatabase.RenameAsset(AssetDatabase.GetAssetPath(chooseObj[i]), oldName[i]);
                }
            }
        }
        GUILayout.Space(30);
        foldout = EditorGUILayout.Foldout(foldout, "文件名预览：");
        if (foldout)
        {
            int temp1 = 0;
            int temp2 = 0;
            for (int i = 0; i < 10; i++)
            {
                GUILayout.Label($"{GetNewName(temp1, temp2)}");
                temp2++;
                if (temp2 >= maxNum2)
                {
                    temp1++;
                    temp2 = 0;
                }
            }

        }

        string GetNewName(int count1, int count2)
        {
            string tempName = head_name;
            if (head_name == "#" || head_name == "＃")
            {
                tempName = "";
            }
            int curID1 = frist_id + count1 * add_num;
            int curID2 = frist2_id + count2 * add_num2;
            string curID1_str = curID1.ToString();
            string curID2_str = curID2.ToString();
            if (numCoune > curID1_str.Length)
            {
                for (int i = curID1_str.Length; i < numCoune; i++)
                {
                    curID1_str = "0" + curID1_str;
                }
            }
            if (numCoune2 > curID2_str.Length)
            {
                for (int i = curID2_str.Length; i < numCoune; i++)
                {
                    curID2_str = "0" + curID2_str;
                }
            }
            switch (prefixType2_id)
            {
                case 0: return $"{tempName}_{curID1_str}_{curID2_str}";
                case 1: return $"{tempName}{curID1_str}_{curID2_str}";
                default: return "错误";
            }
        }
    }

    string str1 = "";
    string str2 = "";
    //替换
    void Replace()
    {
        str1 = EditorGUILayout.TextField("替换前", str1);
        str2 = EditorGUILayout.TextField("替换后", str2);
        if (GUILayout.Button("确认修改", GUILayout.Height(40)))
        {
            if (EditorUtility.DisplayDialog("是否修改", "修改后可通过返回修改返回，但只能返回一次, 是否修改", "确定", "取消"))
            {
                if (head_name == "")
                {
                    Debug.LogError("操作失误");
                    return;
                }
                //记录一下，避免点错了
                chooseObj = Selection.objects;
                oldName = new string[chooseObj.Length];
                for (int i = 0; i < chooseObj.Length; i++)
                {
                    oldName[i] = chooseObj[i].name;
                }
                foreach (var obj in Selection.objects)
                {
                    AssetDatabase.RenameAsset(AssetDatabase.GetAssetPath(obj), GetNewName(obj.name));
                }
            }
        }
        if (GUILayout.Button("返回修改（每次修改只能返回一次）", GUILayout.Height(40)))
        {
            if (EditorUtility.DisplayDialog("取消修改修改", "取消刚刚的修改", "确定", "取消"))
            {
                if (chooseObj == null || oldName == null || oldName.Length == 0)
                {
                    Debug.LogError("还没开始修改");
                }
                for (int i = 0; i < chooseObj.Length; i++)
                {
                    AssetDatabase.RenameAsset(AssetDatabase.GetAssetPath(chooseObj[i]), oldName[i]);
                }
            }
        }
        GUILayout.Space(30);
        foldout = EditorGUILayout.Foldout(foldout, "文件名预览：");
        if (foldout)
        {
            foreach (var obj in Selection.objects)
            {
                GUILayout.Label($"{GetNewName(obj.name)}");
            }
            
        }
        string GetNewName(string oldName)
        {
            if (!string.IsNullOrEmpty(oldName)&&oldName.Contains(str1))
            {
                oldName = oldName.Replace(str1, str2);
            }
            return oldName;
        }
    }

    int frist4_id = 1;
    int add_num4 = 1;
    int numCoune4 = 1;

    string str3 = "";

    //替换并排序
    void ReplaceSort()
    {
        str3 = EditorGUILayout.TextField("替换内容", str3);

        head_name = EditorGUILayout.TextField("替换后内容的前缀", head_name);
        frist4_id = EditorGUILayout.IntField("初始数字", frist4_id);
        add_num4 = EditorGUILayout.IntField("增量", add_num4);
        numCoune4 = EditorGUILayout.IntField("序号的位数", numCoune4);


        if (GUILayout.Button("确认修改", GUILayout.Height(40)))
        {
            if (EditorUtility.DisplayDialog("是否修改", "修改后可通过返回修改返回，但只能返回一次, 是否修改", "确定", "取消"))
            {
  
                if (head_name == "")
                {
                    Debug.LogError("操作失误,前缀不能为空，如不需要前缀，请填‘#’");
                    return;
                }

                //记录一下，避免点错了
                chooseObj = Selection.objects;
                oldName = new string[chooseObj.Length];
                for (int i = 0; i < chooseObj.Length; i++)
                {
                    oldName[i] = chooseObj[i].name;
                }
                int temp = 0;
                foreach (var obj in Selection.objects)
                {
                    AssetDatabase.RenameAsset(AssetDatabase.GetAssetPath(obj), GetNewName(temp, obj.name));
                    temp++;
                }
            }
        }
        if (GUILayout.Button("返回修改（每次修改只能返回一次）", GUILayout.Height(40)))
        {
            if (EditorUtility.DisplayDialog("取消修改修改", "取消刚刚的修改", "确定", "取消"))
            {
                if (chooseObj == null || oldName == null || oldName.Length == 0)
                {
                    Debug.LogError("还没开始修改");
                }
                for (int i = 0; i < chooseObj.Length; i++)
                {
                    AssetDatabase.RenameAsset(AssetDatabase.GetAssetPath(chooseObj[i]), oldName[i]);
                }
            }
        }
        GUILayout.Space(30);
        foldout = EditorGUILayout.Foldout(foldout, "文件名预览：");
        if (foldout)
        {

            int i = 0;
            foreach (var obj in Selection.objects)
            {
                GUILayout.Label($"{GetNewName(i++, obj.name)}");
            }
            

        }


        string GetNewName(int count,string repName)
        {
            string tempName = head_name;


            //加前缀
            if (head_name == "#" || head_name == "＃")
            {
                tempName = "";
            }
            int curID = frist_id + count * add_num;
            string curID_str = curID.ToString();
            if (numCoune > curID_str.Length)
            {
                for (int i = curID_str.Length; i < numCoune; i++)
                {
                    curID_str = "0" + curID_str;
                }
            }

            //替换
            if (repName.Contains(str3))
            {
                repName = repName.Replace(str3, head_name+curID_str);
            }
            return repName;

        }


    }


    //后缀 排序
    void SetPrefix()
    {
        head_name = EditorGUILayout.TextField("前缀", head_name);
        GUILayout.Space(30);

        if (GUILayout.Button("确认修改", GUILayout.Height(40)))
        {
            if (EditorUtility.DisplayDialog("是否修改", "修改后可通过返回修改返回，但只能返回一次, 是否修改", "确定", "取消"))
            {
                //记录一下，避免点错了
                chooseObj = Selection.objects;
                oldName = new string[chooseObj.Length];
                for (int i = 0; i < chooseObj.Length; i++)
                {
                    oldName[i] = chooseObj[i].name;
                }
                foreach (var obj in Selection.objects)
                {
                    AssetDatabase.RenameAsset(AssetDatabase.GetAssetPath(obj), head_name+ obj.name);
                }
            }
        }
        if (GUILayout.Button("返回修改（每次修改只能返回一次）", GUILayout.Height(40)))
        {
            if (EditorUtility.DisplayDialog("取消修改修改", "取消刚刚的修改", "确定", "取消"))
            {
                if (chooseObj == null || oldName == null || oldName.Length == 0)
                {
                    Debug.LogError("还没开始修改");
                }
                for (int i = 0; i < chooseObj.Length; i++)
                {
                    AssetDatabase.RenameAsset(AssetDatabase.GetAssetPath(chooseObj[i]), oldName[i]);
                }
            }
        }
       
    }


    void OnDestroy()
    {
        chooseObj = null;
        oldName = null;
        window = null;
    }
}
