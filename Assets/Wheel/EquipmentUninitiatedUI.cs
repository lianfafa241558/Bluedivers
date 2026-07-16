using static WndTools.WndRootTool;

public class EquipmentUninitiatedUI : WheelUI
{
    protected override bool TriggerConditions()
    {
        return base.TriggerConditions()||InputManager.GetUp(InputState.Mule);
        //return base.TriggerConditions() || Input.GetKeyUp(KeyCode.X);
    }

    protected override void Awake()
    {
        base.Awake();
        InputManager.Bind( Core.WindowStateEnum.Game, InputState.Mule, TryShow);
        SetActive(gameObject, false);
    }


    private void TryShow()
    {
        ShowWnd(new() {
            new() {
                name = "放下 [传送背包]",
                icon = cancelIcon,
                cb=null,
            },
            new() {
                name = "放下 [护卫犬]",
                icon = cancelIcon,
                cb=null,
            },
        });
    }
}
