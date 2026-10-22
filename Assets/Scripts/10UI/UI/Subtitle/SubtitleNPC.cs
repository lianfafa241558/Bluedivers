using FPSGame.GameContract;
using FPSGame.Core.Interface;   // IsValidMono（IActor 是接口引用，单位销毁后不是 Unity 的 null）
using UnityEngine;
using UnityEngine.UI;
using FPSGame.Audio;

namespace FPSGame.UI
{
using static FPSGame.WndTools.WndRootTool;
using FPSGame.Data;
    using FPSGame.Gameplay;

    /// <summary>
    /// NPC 对话字幕。
    /// </summary>
    [AddComponentMenu("UI/字幕/NPC 对话")]
public class SubtitleNPC : SubtitleBase
{
    public override SubtitleBase Creat(IActor owner, GameObject target, Transform parent, bool alwaysShow)
    {
        base.Creat(owner, target, parent, alwaysShow);

        var tarActor = target.GetComponent<IActor>();
        SetText(title, tarActor.ShowName);
        SetSprite(halo, tarActor.ExtraPortrait);
        SetActive(gameObject, tarActor != owner);

        // 隐藏不需要的 UI 元素
        SetActive(distance, false);
        SetActive(direction, false);

        // 默认整体隐藏，等待 OnActorSpeech 触发时显示
        SetAlpha(transform, 0);

        GlobalEventBus.OnActorSpeech += OnActorSpeech;
        return this;
    }

    private void OnDestroy()
    {
        GlobalEventBus.OnActorSpeech -= OnActorSpeech;
    }

    private float _lastSpeechTime = Mathf.NegativeInfinity;
    private float _showTime;
    private bool _isShowingSpeech;

    /// <summary>对话字幕**不受**"显示物体标记（只在蹲下时显示）"开关影响 —— 与 <see cref="SubTitleShout"/> 同口径。
    /// <para>那个开关只该管标记；这里显隐全由"是否在说话 + 距离"自己算（见 Update 为何不调 base）。</para></summary>
    public override void TryActive(bool state)
    {
    }

    private void OnActorSpeech(GameObject go, RuntimeSoundData data)
    {
        if (go != target)
        {
            return;
        }

        SetText(desc, data.Desc);
        LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)desc.parent);
        var source=AudioSvc.PlaySound(data);
        _lastSpeechTime = Time.time;
        _showTime = data.Clip.length + 2;
        _isShowingSpeech = true;
        SetAlpha(transform, 1);

    }

    protected override void Update()
    {
        // ⚠ 有意不调 base.Update：那套 targetState/completeTrans 淡入淡出是给"标记"响应开关的，
        //   本类的透明度只由"是否在说话 + 距离"决定 —— 否则开关一关，说出来的字会被拉成 0。
        if (!owner.IsValidMono() || !target)
        {
            SetActive(gameObject, false);
            return;
        }
        Follow(TargetPos);
        if (!TargetEnabled)
        {
            SetAlpha(transform, Mathf.Lerp(GetAlpha(transform), 0, Time.deltaTime * 3));
            return;
        }

        // 距离淡出：超过12完全消失，8-12渐变
        float dis = GetDistance();
        float distanceAlpha = 1 - Mathf.Clamp01((dis - 8) / 4f);

        // 对话文本过期后整体渐隐
        if (_isShowingSpeech && Time.time > _lastSpeechTime + _showTime)
        {
            float alpha = GetAlpha(transform);
            float newAlpha = Mathf.Lerp(alpha, 0, Time.deltaTime * 3);
            SetAlpha(transform, newAlpha);

            if (newAlpha <= 0.01f)
            {
                SetText(desc, "");
                SetAlpha(transform, 0);
                _isShowingSpeech = false;
            }
        }
        else if (_isShowingSpeech)
        {
            // 对话进行中，根据距离调整透明度
            float alpha = GetAlpha(transform);
            float newAlpha = Mathf.Lerp(alpha, distanceAlpha, Time.deltaTime * 2);
            SetAlpha(transform, newAlpha);
        }
    }
}
}
