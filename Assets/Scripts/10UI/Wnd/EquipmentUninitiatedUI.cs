using FPSGame.Core.Interface;
using System.Linq;
using FPSGame.Game;
using UnityEngine;
using FPSGame.UI;
using FPSGame.Gameplay;

namespace FPSGame.UI
{
using static FPSGame.WndTools.WndRootTool;

/// <summary>
/// 装备卸载用的轮盘菜单。
/// </summary>
[AddComponentMenu("UI/控件/卸载轮盘")]
public class EquipmentUninitiatedUI : WheelUI
{


    EquipController m_Controller;


    public void Init()
    {
        InputManager.BindDown(FPSGame.Core.WindowStateEnum.Game, InputState.Equip, TryShow);
    }


    private void OnDestroy()
    {
        InputManager.UnBindDown(FPSGame.Core.WindowStateEnum.Game, InputState.Equip, TryShow);
    }

    private void TryShow()
    {
        if(!m_Controller&& ActorsManager.Player.IsValidMono()) m_Controller = ActorsManager.Player.transform.GetComponent<EquipController>();
        if (m_Controller != null&& m_Controller.Equips.Count>0)
        {
            Show(m_Controller.Equips
                .Select(item => new WheelItemIfon() { 
                    name = $"卸载[{item.Value.ShowName}]", 
                    icon = item.Value.Portrait, 
                    cb = (_) => item.Value.Operate(),
                })
                .ToList()
            );
        }
    }

    protected override void HideWnd()
    {
        base.HideWnd();
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    protected override void ShowWnd()
    {
        base.ShowWnd();
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }


    protected override bool TriggerConditions()
    {
        return base.TriggerConditions() || InputManager.GetUp(InputState.Equip);
        //return base.TriggerConditions() || Input.GetKeyUp(KeyCode.X);
    }

}
}
