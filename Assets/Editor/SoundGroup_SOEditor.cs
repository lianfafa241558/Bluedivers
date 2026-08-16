using System.Reflection;
using UnityEditor;
using UnityEngine;

/// <summary>
/// SoundGroup_SO 的预览播放编辑器。
/// 在 Inspector 顶部提供「预览播放」按钮，播放 clips 中的随机一条音效；
/// 若勾选了"有序的"，则按顺序播放。再次点击可停止预览。
/// 说明：Edit Mode 下场景 AudioSource 不会真正出声，必须走 Unity 内部 AudioUtil.PlayPreviewClip 预览机制。
/// </summary>
[CustomEditor(typeof(SoundGroup_SO))]
public class SoundGroup_SOEditor : Editor
{
    private static System.Type s_AudioUtilType;

    // 缓存的方法
    private static MethodInfo s_PlayPreviewClipMethod;
    private static MethodInfo s_StopAllPreviewClipsMethod;
    private static MethodInfo s_IsPreviewClipPlayingMethod;
    private static MethodInfo s_LoopPreviewClipMethod;

    private AudioClip _previewClip;
    private bool _isLooping;

    /// <summary>反射获取 UnityEditor.AudioUtil 的预览播放相关静态方法（类为 internal，方法为 public static）</summary>
    private static void EnsureAudioUtil()
    {
        if (s_AudioUtilType != null) return;

        s_AudioUtilType = typeof(Editor).Assembly.GetType("UnityEditor.AudioUtil");
        if (s_AudioUtilType == null)
        {
            Debug.LogWarning("未能找到 UnityEditor.AudioUtil，预览功能不可用");
            return;
        }

        const BindingFlags flags = BindingFlags.Public | BindingFlags.Static;

        // PlayPreviewClip(AudioClip clip, int startSample = 0, bool loop = false)
        s_PlayPreviewClipMethod = s_AudioUtilType.GetMethod("PlayPreviewClip", flags,
            null, new[] { typeof(AudioClip), typeof(int), typeof(bool) }, null);

        s_StopAllPreviewClipsMethod = s_AudioUtilType.GetMethod("StopAllPreviewClips", flags);
        s_IsPreviewClipPlayingMethod = s_AudioUtilType.GetMethod("IsPreviewClipPlaying", flags);
        s_LoopPreviewClipMethod = s_AudioUtilType.GetMethod("LoopPreviewClip", flags);
    }

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        EditorGUILayout.Space();

        var sg = (SoundGroup_SO)target;
        bool isPreviewing = IsPreviewing();

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button(isPreviewing ? "停止预览" : "预览播放", GUILayout.Height(30)))
        {
            if (isPreviewing)
            {
                StopPreview();
            }
            else
            {
                PlayPreview(sg);
            }
        }

        if (GUILayout.Button("循环试听", GUILayout.Height(30)))
        {
            if (!isPreviewing)
            {
                PlayPreview(sg, true);
            }
            else
            {
                _isLooping = !_isLooping;
                SetLoop(_isLooping);
            }
        }
        EditorGUILayout.EndHorizontal();

        if (_previewClip != null)
        {
            EditorGUILayout.HelpBox("正在预览：" + _previewClip.name + (_isLooping ? "（循环）" : ""), MessageType.Info);
        }
    }

    private void PlayPreview(SoundGroup_SO sg, bool loop = false)
    {
        int clipCount = serializedObject.FindProperty("clips").arraySize;
        if (sg == null || clipCount == 0)
        {
            Debug.LogWarning("SoundGroup 没有可预览的音频！");
            return;
        }

        EnsureAudioUtil();
        if (s_PlayPreviewClipMethod == null)
        {
            Debug.LogWarning("AudioUtil.PlayPreviewClip 不可用，无法预览");
            return;
        }

        StopPreview();

        // 通过 Get 获取一条音效（支持有序/随机），仅取剪辑用于预览
        RuntimeSoundData data = sg.Get(Vector3.zero);
        if (!data.Clip)
        {
            Debug.LogWarning("预览失败：音频剪辑为空！");
            return;
        }

        _previewClip = data.Clip;
        _isLooping = loop;
        s_PlayPreviewClipMethod.Invoke(null, new object[] { _previewClip, 0, _isLooping });
    }

    private void SetLoop(bool loop)
    {
        EnsureAudioUtil();
        if (s_LoopPreviewClipMethod == null) return;
        s_LoopPreviewClipMethod.Invoke(null, new object[] { loop });
    }

    private bool IsPreviewing()
    {
        if (_previewClip == null) return false;

        EnsureAudioUtil();
        if (s_IsPreviewClipPlayingMethod == null) return true;
        return (bool)s_IsPreviewClipPlayingMethod.Invoke(null, null);
    }

    private void StopPreview()
    {
        EnsureAudioUtil();
        if (s_StopAllPreviewClipsMethod != null)
        {
            s_StopAllPreviewClipsMethod.Invoke(null, null);
        }
        _previewClip = null;
        _isLooping = false;
    }

    private void OnDisable()
    {
        StopPreview();
    }
}
