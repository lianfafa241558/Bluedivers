using System.Collections;
using System.Collections.Generic;
using FpsGame.Mission;
using UnityEngine;
using static WndTools.WndRootTool;

public class MissionCompleteWnd : WindowRoot
{
    [SerializeField]
    Animator anim;

    [SerializeField]
    Color MainColor, ExtraColor;

    [SerializeField]
    Transform left, text, right;

    [SerializeField]
    Transform desc, item1, item2;

    private void Awake()
    {
        //临时的，以后再想办法创建
        gameObject.SetActive(false);


        SetWndState(true);
        SetWndState(false);
    }

    protected override void FirstShowWnd()
    {
        GlobalEventManager.OnMissionCompleted += MissionCompleted;

    }
    private void OnDestroy()
    {
        GlobalEventManager.OnMissionCompleted -= MissionCompleted;

    }

    void MissionCompleted(MissionBase mission)
    {
        SetWndState(true);
        Color color = mission.missionType == MissionType.Main ? MainColor : ExtraColor;
        SetColor(left, color);
        SetColor(text, color);
        SetColor(right, color);
        SetText(desc,mission.title);
        SetText(item1, mission.data.reward);
        SetText(item2, mission.data.reward/5);
        anim.Play("Entry",0,0);
    }



    

    IEnumerator CloseWndAfterDelay()
    {
        yield return new WaitForSeconds(4f);
        CloseWnd();
    }

    protected override void ShowWnd()
    {
        StartCoroutine(CloseWndAfterDelay());
    }

    protected override void HideWnd()
    {

    }

    public override void Init() { }
    public override void UnInit() { }

}
