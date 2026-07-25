using GameContract;
using UnityEngine;
using UnityEngine.UI;
using static WndTools.WndRootTool;

public class SubtitleNPC : SubtitleBase
{
    public override SubtitleBase Creat(I_Actor owner, GameObject target, Transform parent, bool alwaysShow)
    {
        base.Creat(owner, target, parent, alwaysShow);

        var tarActor = target.GetComponent<I_Actor>();
        SetText(title, tarActor.ShowName);
        SetSprite(halo, tarActor.ExtraPortrait);
        SetActive(gameObject, tarActor != owner);

        // 隐藏不需要的 UI 元素
        SetActive(distance, false);
        SetActive(direction, false);

        // 默认整体隐藏，等待 OnActorSpeech 触发时显示
        SetAlpha(transform, 0);

        GlobalEventSub.OnActorSpeech += OnActorSpeech;
        return this;
    }

    private void OnDestroy()
    {
        GlobalEventSub.OnActorSpeech -= OnActorSpeech;
    }

    private float _lastSpeechTime = Mathf.NegativeInfinity;
    private float _showTime;
    private bool _isShowingSpeech;

    public override void TryActive(bool state)
    {
        targetState = state;
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
        base.Update();

        if (!target)
        {
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
