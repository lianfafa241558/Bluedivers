using System.Linq;
using Unity.FPS.Game;
using UnityEngine;
using static WndTools.WndRootTool;

public class EquipmentUninitiatedUI : WheelUI
{


    EquipController m_Controller;


    public void Init()
    {
        InputManager.BindDown(Core.WindowStateEnum.Game, InputState.Equip, TryShow);
    }


    private void OnDestroy()
    {
        InputManager.UnBindDown(Core.WindowStateEnum.Game, InputState.Equip, TryShow);
    }

    private void TryShow()
    {
        if(!m_Controller&& ActorsManager.Player.IsValid()) m_Controller = ActorsManager.Player.transform.GetComponent<EquipController>();
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
