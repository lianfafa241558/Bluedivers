using System.Collections.Generic;
using Core;
using FPSGame.Attribute;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;


[CreateAssetMenu(fileName = "NT_", menuName = "Data/音效组")]
public class SoundGroup_SO: ScriptableObject
{
    static Dictionary<string, int> orderNumber=new();

    [InspectorName("名称")]
    public string groupName;

    [SerializeField]
    [Header("音频剪辑列表")]
    private List<SoundItem> clips = new();
    [SerializeField]
    [InspectorName("随机音量范围")]
    [MinMaxSlider(0.5f,1.5f)]
    private Vector2 volumeRange = Vector2.one;
    [SerializeField]
    [InspectorName("随机音调范围")]
    [MinMaxSlider(0.8f, 1.2f)]
    private Vector2 pitchRange = Vector2.one;
    [SerializeField]
    [InspectorName("随机延迟范围")]
    private Vector2 delayRange = Vector2.zero;

    [InspectorName("淡入时间（秒）")]
    public float fadeInTime = 0f;
    [InspectorName("淡出时间（秒）")]
    public float fadeOutTime = 0f;
    [InspectorName("播放范围")]
    [Compare("flags",(int)SoundFlag.Space, CompareOperate.Contain)]
    public float range = 60;
    [InspectorName("有序的")]
    public bool isOrder; 

    [InspectorName("优先级")]
    public int priority;
    public AudioGroups group;

    [InspectorName("标旗")]
    public SoundFlag flags;

    public RuntimeSoundData Get(Vector3 point=default)
    {
        SoundItem selectedItem;
        if (isOrder)
        {
            int number=0;
            number=orderNumber.GetValueOrDefault(name, 0);
            orderNumber[name]= number+1;
            selectedItem = clips[number%clips.Count];

        }
        else
        {
            selectedItem = clips.RandomTake();
        }
        return new RuntimeSoundData {
            Clip = selectedItem.audioClip,
            Desc = selectedItem.subtitle,
            Cfg=this,
            Volume = Random.Range(volumeRange.x, volumeRange.y),
            Pitch = Random.Range(pitchRange.x, pitchRange.y),
            Delay = Random.Range(delayRange.x, delayRange.y),
            Point = point,
        };

    }

  

#region 工具
#if UNITY_EDITOR
    [MenuItem("Assets/Create/Data/组合音效组", true)]
    private static bool ValidateCreateCombinedSoundGroup()
    {
        if (Selection.activeObject == null) return false;
        return Selection.GetFiltered<AudioClip>(SelectionMode.Assets).Length > 0;
    }

    [MenuItem("Assets/Create/Data/组合音效组")]
    private static void CreateCombinedSoundGroup()
    {
        // 收集选中的 AudioClip 资产（去掉重复项）
        var selectedClips = Selection.GetFiltered<AudioClip>(SelectionMode.Assets);
        if (selectedClips.Length == 0)
        {
            Debug.LogError("请先选中 AudioClip 音频资源！");
            return;
        }

        // 初始目录设为第一个音频所在的文件夹
        string initialDir = System.IO.Path.GetDirectoryName(AssetDatabase.GetAssetPath(selectedClips[0]));

        // 使用 SaveFilePanel 风格的输出（返回绝对路径）
        string absPath = EditorUtility.SaveFilePanel("新建组合音效组",
            initialDir, "NT_", "asset");

        if (string.IsNullOrEmpty(absPath)) return;

        // 转成 Assets 相对路径
        string assetPath = FileUtil.GetProjectRelativePath(absPath);
        if (string.IsNullOrEmpty(assetPath))
        {
            Debug.LogError("保存路径必须在 Assets 文件夹内！");
            return;
        }

        // 去掉 .asset 后缀，作为组名
        string groupName = System.IO.Path.GetFileNameWithoutExtension(assetPath);

        // 创建新的 SoundGroup_SO 实例
        SoundGroup_SO newSoundGroup = CreateInstance<SoundGroup_SO>();
        newSoundGroup.name = groupName;
        newSoundGroup.clips = new List<SoundItem>();
        newSoundGroup.group = AudioGroups.Player;
        newSoundGroup.flags = default;

        // 将选中的音频按序加入 clips
        foreach (var clip in selectedClips)
        {
            newSoundGroup.clips.Add(new SoundItem { audioClip = clip });
        }

        // 创建资产文件
        AssetDatabase.CreateAsset(newSoundGroup, assetPath);

        // 将选中的音频重命名为 组名+序号（如 NT_A1、NT_A2），并记录原路径用于撤销
        for (int i = 0; i < selectedClips.Length; i++)
        {
            string clipPath = AssetDatabase.GetAssetPath(selectedClips[i]);
            Undo.RecordObject(selectedClips[i], "Rename AudioClip");
            AssetDatabase.RenameAsset(clipPath, $"{groupName}{i + 1}");
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.ImportAsset(AssetDatabase.GetAssetPath(newSoundGroup));

        // 选中新创建的资源
        Selection.activeObject = newSoundGroup;
        EditorGUIUtility.PingObject(newSoundGroup);

        Debug.Log($"已创建组合音效组 {newSoundGroup.name}，共 {newSoundGroup.clips.Count} 个音频");
    }

    [MenuItem("Assets/Create/Data/注入音效", true)]
    private static bool ValidateCreateChildSoundGroup()
    {
        UnityEngine.Object selected = Selection.activeObject;
        if (selected == null) return false;

        string path = AssetDatabase.GetAssetPath(selected);
        return !string.IsNullOrEmpty(path);
    }

    [MenuItem("Assets/Create/Data/注入音效")]
    private static void CreateChildSoundGroup()
    {
        UnityEngine.Object selected = Selection.activeObject;
        if (selected == null)
        {
            Debug.LogError("请先选中一个资产！");
            return;
        }

        string assetPath = AssetDatabase.GetAssetPath(selected);
        var mainAsset = AssetDatabase.LoadMainAssetAtPath(assetPath);

        if (mainAsset == null)
        {
            Debug.LogError("无法获取主资源！");
            return;
        }

        // 使用 SaveFilePanel 风格的输出
        string newName = EditorUtility.SaveFilePanelInProject("新建 SoundGroup",
            "NewSoundGroup", "asset", "请输入 SoundGroup 名称");

        if (string.IsNullOrEmpty(newName)) return;

        // 去掉 .asset 后缀
        newName = System.IO.Path.GetFileNameWithoutExtension(newName);

        // 创建新的 SoundGroup_SO 实例
        SoundGroup_SO newSoundGroup = CreateInstance<SoundGroup_SO>();
        newSoundGroup.name = newName;
        newSoundGroup.clips = new List<SoundItem>();
        newSoundGroup.group = AudioGroups.Player;
        newSoundGroup.flags =default;

        // 添加为子资源
        AssetDatabase.AddObjectToAsset(newSoundGroup, assetPath);

        // 保存并刷新
        EditorUtility.SetDirty(mainAsset);
        AssetDatabase.SaveAssets();
        AssetDatabase.ImportAsset(assetPath);

        // 选中新创建的资源
        Selection.activeObject = newSoundGroup;
        EditorGUIUtility.PingObject(newSoundGroup);

        Debug.Log($"已创建新的 SoundGroup 子资源 {newSoundGroup.name}");
    }

    [MenuItem("Assets/Create/Data/把选中 SoundGroup 变成选中物体的子资源")]
    private static void AttachSelectedToAsset()
    {
        // 收集选中的 SoundGroup_SO
        var selectedGroups = Selection.GetFiltered<SoundGroup_SO>(SelectionMode.Assets);
        if (selectedGroups.Length == 0)
        {
            Debug.LogError("请先选中一个或多个 SoundGroup_SO！");
            return;
        }

        // 自动识别目标父资源：从所有选中的资产里找出"非 SoundGroup_SO"的那个
        var allSelected = Selection.objects;
        ScriptableObject parent = null;
        foreach (var obj in allSelected)
        {
            // 跳过 SoundGroup_SO、跳过子资源引用，只取项目里的资产
            if (obj == null || obj is SoundGroup_SO) continue;
            if (!(obj is ScriptableObject so)) continue;
            string p = AssetDatabase.GetAssetPath(so);
            if (string.IsNullOrEmpty(p)) continue;

            parent = so;
            break;
        }

        if (parent == null)
        {
            Debug.LogError("请在选中 SoundGroup_SO 的同时，再选中一个非 SoundGroup 的 ScriptableObject 作为目标父资源！");
            return;
        }

        string parentPath = AssetDatabase.GetAssetPath(parent);
        // 关键：必须用路径加载"主资源"（真正的持久化资产），
        // 因为 Selection.objects 可能拿到的是非持久化的运行时实例/子资源，AddObjectToAsset 会失败。
        var mainAsset = AssetDatabase.LoadMainAssetAtPath(parentPath);
        if (mainAsset == null)
        {
            Debug.LogError($"无法从路径加载主资源：{parentPath}");
            return;
        }
        parent = mainAsset as ScriptableObject;
        if (parent == null)
        {
            Debug.LogError($"主资源 {parentPath} 不是 ScriptableObject，无法挂载子资源！");
            return;
        }

        int movedCount = 0;
        foreach (var group in selectedGroups)
        {
            string groupPath = AssetDatabase.GetAssetPath(group);
            bool isStandalone = !string.IsNullOrEmpty(groupPath) && !AssetDatabase.IsSubAsset(group);

            // 1) 准备要挂载的对象
            UnityEngine.Object toAdd = group;
            if (isStandalone)
            {
                // 独立的 .asset 主资源：AddObjectToAsset 不接受仍是主资源的对象，
                // 且 DeleteAsset 会销毁内存对象导致引用变 null。
                // 所以先深拷贝一份数据到内存实例，挂到目标下，再删除原独立文件。
                toAdd = Object.Instantiate(group);
                toAdd.name = group.name;
            }
            else if (!string.IsNullOrEmpty(groupPath))
            {
                // 原先是某资产的子资源：解绑即可（对象仍存活）
                AssetDatabase.RemoveObjectFromAsset(group);
            }

            // 2) 挂载到目标父资源下作为子资源
            AssetDatabase.AddObjectToAsset(toAdd, parent);

            // 3) 删除旧的独立资产文件（若存在）
            if (isStandalone)
            {
                AssetDatabase.DeleteAsset(groupPath);
                AssetDatabase.SaveAssets();
            }

            movedCount++;
        }

        EditorUtility.SetDirty(parent);
        AssetDatabase.SaveAssets();
        AssetDatabase.ImportAsset(parentPath);

        Selection.activeObject = parent;
        EditorGUIUtility.PingObject(parent);

        Debug.Log($"已将 {movedCount} 个 SoundGroup 挂载为 {parent.name} 的子资源");
    }

    [ContextMenu("变成选中物体的子资源")]
    private void AttachSelfToAsset()
    {
        var parent = Selection.activeObject;
        if (parent == null || parent == this)
        {
            Debug.LogError("请先在 Project 窗口选中一个目标 ScriptableObject（不能是自身）！");
            return;
        }
        if (parent is SoundGroup_SO)
        {
            Debug.LogError("目标父资源不能是 SoundGroup_SO！");
            return;
        }

        string parentPath = AssetDatabase.GetAssetPath(parent);
        string selfPath = AssetDatabase.GetAssetPath(this);

        // 用主资源确保目标为持久化资产（Selection.activeObject 可能是非持久实例）
        var mainAsset = AssetDatabase.LoadMainAssetAtPath(parentPath);
        if (mainAsset == null || !(mainAsset is ScriptableObject soParent))
        {
            Debug.LogError($"目标 {parent.name} 不是有效的持久化 ScriptableObject 资产！");
            return;
        }
        parent = soParent;

        // 若自身是独立资产，先解绑/删除旧文件，再作为子资源挂载
        bool isStandalone = !string.IsNullOrEmpty(selfPath) && !AssetDatabase.IsSubAsset(this);

        UnityEngine.Object toAdd = this;
        if (isStandalone)
        {
            // 独立主资源：深拷贝一份到内存实例（避免 DeleteAsset 销毁引用变 null）
            toAdd = Object.Instantiate(this);
            toAdd.name = name;
        }
        else if (!string.IsNullOrEmpty(selfPath))
        {
            AssetDatabase.RemoveObjectFromAsset(this);
        }

        AssetDatabase.AddObjectToAsset(toAdd, parent);

        if (isStandalone)
        {
            AssetDatabase.DeleteAsset(selfPath);
            AssetDatabase.SaveAssets();
        }

        EditorUtility.SetDirty(parent);
        AssetDatabase.SaveAssets();
        AssetDatabase.ImportAsset(parentPath);

        Selection.activeObject = parent;
        EditorGUIUtility.PingObject(parent);

        Debug.Log($"已将 {name} 挂载为 {parent.name} 的子资源");
    }

    [ContextMenu("删除自身")]
    private void DeleteSelf()
    {
        // 获取当前资产的路径
        string assetPath = AssetDatabase.GetAssetPath(this);
        if (string.IsNullOrEmpty(assetPath))
        {
            Debug.LogError("无法获取资产路径");
            return;
        }

        // 获取父级对象（主资源）
        var mainAsset = AssetDatabase.LoadMainAssetAtPath(assetPath);

        string deletedName = name;

        // 删除自身（子资源）
        DestroyImmediate(this, true);

        // 保存并刷新
        AssetDatabase.SaveAssets();
        AssetDatabase.ImportAsset(assetPath);

        // 选中父级资源
        if (mainAsset != null)
        {
            Selection.activeObject = mainAsset;
        }

        Debug.Log($"已删除 SoundGroup: {deletedName}");
    }


#endif
#endregion
}



[System.Flags]
public enum SoundFlag
{
    [InspectorName("循环")]
    Loop = 1 << 0,
    [InspectorName("唯一")]//仅播放一个实例,单独音频源才行
    Unique = 1 << 1,
    [InspectorName("重要")]//占据单独的音频源
    Importance = 1 << 2,
    [InspectorName("空间音效")]
    Space = 1 << 3,
}
[System.Serializable]
public class SoundItem
{
    [InspectorName("音频")]
    public AudioClip audioClip;

    [InspectorName("字幕")]
    public string subtitle;
}
public struct RuntimeSoundData
{
    public AudioClip Clip;
    public string Desc;
    public SoundGroup_SO Cfg;
    public float Volume;
    public float Pitch;
    public float Delay;
    public Vector3 Point;
    public bool HasFlag(SoundFlag flag)
    {
        return (Cfg.flags & flag) == flag;
    }

    public static implicit operator AudioPlayInfo(RuntimeSoundData data)
    {
        return new AudioPlayInfo() {
            cilp = data.Clip,
            group = data.Cfg.group,
            volume = data.Volume,
            delay = data.Delay,
            speed = data.Pitch,
            importance = data.HasFlag(SoundFlag.Importance),
            nonStackable = data.HasFlag(SoundFlag.Unique),
            loop = data.HasFlag(SoundFlag.Loop),
            space = data.HasFlag(SoundFlag.Space) ? 1 : 0,
            range = data.Cfg.range,
            vector = data.Point,
        };
    }
}


