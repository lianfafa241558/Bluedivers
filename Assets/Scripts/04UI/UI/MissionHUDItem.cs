using System.Collections.Generic;
using FpsGame.Mission;
using FPSGame.UI;
using GameContract;

using Unity.FPS.Game;
using UnityEngine;
using UnityEngine.UI;
using Utils;
using static WndTools.WndRootTool;

public class MissionHUDItem : MonoBehaviour
{
    [Foldout("组件",true)]
    public Transform self,title, counter,tip,icon;
    public DynamicBar bar;
    public CanvasGroup canvasGroup;

    public VerticalLayoutGroup subGroup;

    [Foldout("过渡", true)]
    [InspectorName("淡入持续时间")] 
    public float FadeInDuration = 0.5f;
    [InspectorName("淡出持续时间")] 
    public float FadeOutDuration = 2f;
    public MissionBase mission;
    [SerializeField]
    private bool allowShow;

    public void Initialize(MissionBase mission)
    {
        //设置目标的描述，并强制重新计算内容大小
        //Canvas.ForceUpdateCanvases();
        this.mission = mission;
        //Debug.LogError("任务创建" + mission.title);
        SetText(title, mission.title);
        SetText(tip, mission.tip);
        SetText(counter, mission.HasTag(MissionTag.DisplayProgress) ? (mission.NowProgress+"/"+mission.MaxProgress) :"");
        SetActive(tip, !string.IsNullOrEmpty(mission.tip)&& !mission.HasTag(MissionTag.hideSelf));
        SetActive(bar.transform.parent,false);
        SetSprite(icon,mission.icon);
        if(mission.missionType==MissionType.Nest)SetColor(icon, mission.color);

        SetActive(!mission.HasTag(MissionTag.hideSelf), icon.parent);
        //Debug.LogError("激活状态?"+ mission.HasTag(MissionTag.IsActive)+ " tag1: " +mission.missionTag.ToString() + " tag2: " + GetMissionTagNames(mission.missionTag)+"激活"+ mission.HasTag(MissionTag.IsActive));
        SetAlpha(canvasGroup, mission.HasTag(MissionTag.IsActive) ? 1:0.3f);
        //隐藏，直到显示状态变化
        allowShow = (mission.HasTag(MissionTag.StratDiscovered) && mission.missionType == MissionType.Main) && !mission.HasTag(MissionTag.hideAll);
        SetActive(transform, allowShow);
        //RefreshContentSizeFitter(self);

        if (mission.HasTag(MissionTag.hideSelf))
        {
            subGroup.padding.left = 0;
        }
    }

   
    public void StateChange(bool state)
    {
        if (state == allowShow) return;

        if (state)
        {
            SetActive(gameObject, true);
            SetAlpha(canvasGroup.transform, 0, 1, 500);
        }
        else
        {
            SetAlpha(canvasGroup.transform,1,0, 500, () => {
                SetActive(gameObject, false);
            });
        }
        allowShow = state;
        //Debug.LogError("任务显示状态变化" + mission.title+"变为"+ state);
    }

    public void EndOrDisable(bool isEnd=false)
    {
        SetActive(tip,false);
        SetActive(counter, false);
        SetActive(bar.transform.parent, false);
        //RefreshLayout(transform);


        SetAlpha(canvasGroup.transform, 1, 0.3f, (int)(1000 * FadeOutDuration), () => {
            if (mission.missionType != MissionType.Main && isEnd)
            {
                SetActive(transform, false);
                //RefreshContentSizeFitter(self);
                Tool.Destroy(gameObject);
            }
            else
            {
                //RefreshContentSizeFitter(self);
            }

        });
    }
    public void Fail()
    {
        EndOrDisable(true);
        SetColor(title,Color.red);
    }
    public void Completed()
    {
        EndOrDisable(true);

    }

    public void UpdateStage()
    {
        //Debug.LogError("任务更新"+ mission.title);
        SetActive(transform, allowShow&&!mission.HasTag(MissionTag.hideAll));

        if (GetAlpha(canvasGroup.transform) >=0.9f != mission.HasTag(MissionTag.IsActive))
        {
            //Debug.LogError("任务更新" + mission.title+"已显示" +(GetAlpha(transform) >= 0.9f)+"任务激活"+ mission.HasTag(MissionTag.IsActive));
            if (!mission.HasTag(MissionTag.IsActive))
            {
                EndOrDisable();
                return;
            }
            else
            {
                SetAlpha(canvasGroup.transform, GetAlpha(canvasGroup.transform),1, 500);
            }
        }


        SetActive(!mission.HasTag(MissionTag.hideSelf), tip, icon.parent, bar.transform.parent);

        bool emptyTip = string.IsNullOrEmpty(mission.tip);
        if (GetActive(tip) == emptyTip) SetActive(tip, !emptyTip);
        if (!emptyTip) SetText(tip, mission.tip);

        if (mission.HasTag(MissionTag.DisplayProgress))
        {
            SetText(counter, mission.NowProgress + "/" + mission.MaxProgress);
        }

        SetText(title, mission.title);
        var barDisplay = GetActive(bar.transform.parent);
        if (mission.percentage > 0 && mission.percentage < 1)
        {
            bar.SetFill(mission.percentage);
            SetActive(bar.transform.parent, true);
        }
        else
        {
            SetActive(bar.transform.parent, false);
        }


    }

}
