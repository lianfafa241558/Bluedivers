using FpsGame.Mission;
using FPSGame.UI;
using Unity.BaseTool;
using Unity.FPS.Game;
using UnityEngine;
using UnityEngine.UI;
using static WndTools.WndRootTool;

public class MissionHUDItem : MonoBehaviour
{
    [Foldout("组件",true)]
    public Transform title,counter,tip,icon;
    public DynamicBar bar;
    public CanvasGroup canvasGroup;

    public VerticalLayoutGroup subGroup;

    [Foldout("过渡", true)]
    [CustomLabel("淡入持续时间")] 
    public float FadeInDuration = 0.5f;
    [CustomLabel("淡出持续时间")] 
    public float FadeOutDuration = 2f;


    public void Initialize(MissionBase mission)
    {
        //设置目标的描述，并强制重新计算内容大小
        //Canvas.ForceUpdateCanvases();
        SetText(title, mission.title);
        SetText(tip, mission.tip);
        SetText(counter, mission.MaxProgress>0?(mission.NowProgress+"/"+mission.MaxProgress) :"");
        SetActive(tip, !string.IsNullOrEmpty(mission.tip));
        SetActive(bar.transform.parent,false);
        SetSprite(icon,mission.icon);

        if (GetComponent<RectTransform>())
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(GetComponent<RectTransform>());
        }
      
        
    }
    public void StateChange(bool state)
    {
        
        if (state) SetActive(gameObject, true);
        else SetActive(gameObject, false, 500);
        SetAlpha(transform, state?0:1, state?1:0,500);
    }

    public void Complete()
    {
        SetActive(tip,false);
        SetActive(counter, false);
        SetAlpha(transform, 1, 0.3f, (int)(1000 * FadeOutDuration));
    }

  

}
