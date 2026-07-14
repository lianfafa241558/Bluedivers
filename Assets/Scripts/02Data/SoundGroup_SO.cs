using System.Collections.Generic;
using Core;
using FPSGame.Attribute;
using UnityEditor;
using UnityEngine;


[CreateAssetMenu(fileName = "NT_", menuName = "Data/角色台词")]
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
            Delay = Random.Range(delayRange.x, delayRange.y)
        };

    }

  

#region 工具
#if UNITY_EDITOR
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


