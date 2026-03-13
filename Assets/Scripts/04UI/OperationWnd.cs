using System.Collections;
using System.Collections.Generic;
using Unity.FPS.Game;
using UnityEngine;
using static WndTools.WndRootTool;

public class OperationWnd : WindowRoot
{
    [SerializeField]
    private Transform operRoot,textDesc, textOpteType, barRoot,bar;


    private PlayerOperationController m_Player;
    public Furniture_Base furn;

    public override void Init()
    {
        GlobalEventManager.OnFurnitureOperate += RefreshDisplay;
    }
    public override void UnInit()
    {
        GlobalEventManager.OnFurnitureOperate -= RefreshDisplay;
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
        if (!m_Player && ActorsManager.Player.IsValid()) TryPlayer();
        if (!m_Player) return;
        if (furn !=m_Player.target)
        {
            furn = m_Player.target;
            
            if (furn)
            {
                RefreshDisplay(m_Player.gameObject, furn);
            }
            else
            {
                SetActive(operRoot, false);
            }

        }
        if (furn)
        {
            SetActive(barRoot, GetFill(bar)>0.01f);
            if(furn.meetTime>0) SetFill(bar, furn.Press/ furn.meetTime, 5 * Time.deltaTime);
        }

    }
    void RefreshDisplay(GameObject user,Furniture_Base furn)
    {
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
                SetText(textOpteType, furn.meetTime > 0 ? "长按" : "按");
            }
            else
            {
                SetActive(textOpteType.parent, false);
            }
            SetActive(barRoot, false);
            if (furn.meetTime > 0) SetFill(bar, furn.Press / furn.meetTime);
            else SetFill(bar, 0);
        }

        
    }

}
