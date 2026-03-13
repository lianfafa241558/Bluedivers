using Unity.FPS.Game;
using Unity.FPS.Gameplay;
using UnityEngine;
using UnityEngine.UI;
using static WndTools.WndRootTool;

public class JetpackWnd : WindowRoot
{
    /// <summary>落地后隐藏UI的延迟时间</summary>
    const float HideUIDelay = 3;


    [SerializeField]
    Image JetpackImage;
    Jetpack Jetpack;
    CanvasGroup canvasGroup;
    float delay;

    public override void Init()
    {

    }
    public override void UnInit()
    {

    }
    protected override void FirstShowWnd()
    {
        canvasGroup = GetComponent<CanvasGroup>();
    }

    protected override void ShowWnd()
    {
    }

    protected override void HideWnd()
    {
        if(Jetpack)Jetpack.OnJetpackChange -= OnJetpackChange;

    }
    private void TryPlayer()
    {
        Jetpack = ActorsManager.Player.transform.GetComponent<Jetpack>();
        Jetpack.OnJetpackChange += OnJetpackChange;
        OnJetpackChange(false);
        delay = 0;
    }

    void Update()
    {
        if (!Jetpack && ActorsManager.Player.IsValid()) TryPlayer();
        if (!Jetpack) return;
        UpdateJetpack();
    }

    void UpdateJetpack()
    {
        if (canvasGroup.alpha==0) return;

        JetpackImage.fillAmount = 1-Jetpack.CurrentFillRatio;
        JetpackImage.color = Color.Lerp(JetpackImage.color, JetpackImage.fillAmount > 0.6f ? new(1,0.5f,0.5f,0.35f) : new(0.9f, 0.96f, 1f, 0.35f), Time.deltaTime);
        if (JetpackImage.fillAmount<=0&&(delay+=Time.deltaTime) > HideUIDelay)
        {
            OnJetpackChange(false);
        }
    }

    /// <summary> 切换喷气状态时 </summary>
    void OnJetpackChange(bool state)
    {
        canvasGroup.alpha = state ? 1 : 0;
        delay = 0;
    }
}
