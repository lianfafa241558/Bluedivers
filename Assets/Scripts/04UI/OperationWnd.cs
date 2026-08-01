using System.Collections;
using System.Collections.Generic;
using FPSGame.Furn;
using Unity.FPS.Game;
using UnityEngine;
using static WndTools.WndRootTool;

public class OperationWnd : Window
{
    [SerializeField]
    private Transform operRoot,textDesc, textOpteType, barRoot,bar;


    private PlayerOperationController m_Player;
    public IFurniture furn;

    private bool m_IsThirdPerson;

    public void Init()
    {
        GlobalEventSub.OnFurnitureOperate += RefreshDisplay;
        GlobalEventSub.OnViewSwitch += OnViewSwitch;
        OnViewSwitch(ArchiveSvc.GetSetting("默认操作视角") > 0);
    }
    public void UnInit()
    {
        GlobalEventSub.OnFurnitureOperate -= RefreshDisplay;
        GlobalEventSub.OnViewSwitch -= OnViewSwitch;
    }

    private void OnViewSwitch(bool isThirdPerson)
    {
        m_IsThirdPerson = isThirdPerson;
        if (isThirdPerson)
            SetActive(operRoot, false);
    }

    protected override void FirstShowWnd()
    {
        
    }
    protected override void ShowWnd()
    {
        operRoot = transform.GetChild(0);
        SetActive(operRoot, false);
    }
    protected override void HideWnd()
    {

    }
    private void TryPlayer()
    {
        m_Player = ActorsManager.Player.transform.GetComponent<PlayerOperationController>();
    }

    private void Update()
    {
        if (m_IsThirdPerson) return;
        if (!m_Player && ActorsManager.Player.IsValid()) TryPlayer();
        if (!m_Player) return;
        if (furn != m_Player.target)
        {
            furn = m_Player.target;
            
            if (furn != null)
            {
                RefreshDisplay(m_Player.gameObject, furn);
            }
            else
            {
                SetActive(operRoot, false);
            }

        }
        if (furn!=null)
        {
            SetActive(barRoot, GetFill(bar)>0.01f);
            if(furn.MeetTime > 0) SetFill(bar, furn.Press/ furn.MeetTime, 5 * Time.deltaTime);
        }
        else if(GetActive(operRoot))
        {
            SetActive(operRoot, false);
        }

    }
    void RefreshDisplay(GameObject user,IFurniture furn)
    {
        if (m_IsThirdPerson) { SetActive(operRoot, false); return; }
        if (user != m_Player.gameObject) return;
        if(!furn.CanOperate(user))
        {
            SetActive(operRoot, false);
        }
        else
        {
            SetActive(operRoot, true);
            SetText(textDesc, furn.Desc);
            if (!string.IsNullOrEmpty(furn.Desc))
            {
                SetActive(textOpteType.parent, true);
                SetText(textOpteType, furn.MeetTime > 0 ? "长按" : "按");
            }
            else
            {
                SetActive(textOpteType.parent, false);
            }
            SetActive(barRoot, false);
            if (furn.MeetTime > 0) SetFill(bar, furn.Press / furn.MeetTime);
            else SetFill(bar, 0);
        }

        
    }

}
