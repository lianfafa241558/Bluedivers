using UnityEngine;
using System.Collections.Generic;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace FPSGame.Data
{


[CreateAssetMenu(fileName = "NT_", menuName = "Data/角色台词组")]
public class NoticeTree_SO : ScriptableObject
{
    public string SourceName;
    public string ID;
    public Sprite Portrait;
    public bool UseResLoad;
    public List<SoundGroup_SO> sounds = new List<SoundGroup_SO>();

#if UNITY_EDITOR
    /// <summary>把本资产下的 SoundGroup_SO 子资源同步进 sounds。
    /// 右键 Project 里的本资产执行；已存在的保持原顺序、新子资源按名字追加，避免重排打乱人工排序。</summary>
    [ContextMenu("同步子音效组到 sounds")]
    private void SyncSoundsFromSubAssets()
    {
        string path = AssetDatabase.GetAssetPath(this);
        if (string.IsNullOrEmpty(path))
        {
            Debug.LogError("无法获取资产路径，请在 Project 窗口右键该资产执行", this);
            return;
        }

        // 1) 收集本资产内的全部 SoundGroup_SO 子资源，按名字排序保证结果稳定
        List<SoundGroup_SO> subs = new List<SoundGroup_SO>();
        foreach (var obj in AssetDatabase.LoadAllAssetsAtPath(path))
        {
            if (obj is SoundGroup_SO group && group != null && !subs.Contains(group)) subs.Add(group);
        }
        subs.Sort((a, b) => string.CompareOrdinal(a.name, b.name));

        // 2) 原列表里仍存在的保持在前面，新出现的追加到末尾
        List<SoundGroup_SO> result = new List<SoundGroup_SO>(subs.Count);
        foreach (var old in sounds)
        {
            if (old != null && subs.Contains(old) && !result.Contains(old)) result.Add(old);
        }
        foreach (var group in subs)
        {
            if (!result.Contains(group)) result.Add(group);
        }

        // 3) 统计变化（空引用也算移除）
        int added = 0, removed = 0;
        foreach (var group in sounds)
        {
            if (group == null || !subs.Contains(group)) removed++;
        }
        foreach (var group in result)
        {
            if (!sounds.Contains(group)) added++;
        }

        if (added == 0 && removed == 0 && result.Count == sounds.Count)
        {
            Debug.Log($"「{name}」sounds 已是最新，共 {sounds.Count} 组", this);
            return;
        }

        sounds = result;
        EditorUtility.SetDirty(this);
        AssetDatabase.SaveAssets();
        Debug.Log($"「{name}」音效组同步完成：现有 {sounds.Count} 组（新增 {added}，移除 {removed}）", this);
    }
#endif
}
 
}
