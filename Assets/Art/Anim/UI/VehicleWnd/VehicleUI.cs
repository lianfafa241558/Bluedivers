using FPSGame.UI;
using TMPro;

using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public interface IVehicleUIController
{
    /// <summary>是否是主手，状态</summary>
    UnityAction<bool, bool> SetWeaponState { get; set; }

    UnityAction<bool> OnStateChange { get; set; }
    /// <summary>是否是主手，颜色</summary>
    UnityAction<bool, Color> OnColorChange { get; set; }
    /// <summary>是否是主手，值</summary>
    UnityAction<bool, float> OnFillChange { get; set; }
    /// <summary>是否是主手，文本</summary>
    UnityAction<bool, string> OnTextChange { get; set; }
    /// <summary>是否是主手，图标</summary>
    UnityAction<bool, Sprite> OnIconChange { get; set; }
}
public class VehicleUI : MonoBehaviour
{
    [SerializeField]
    GameObject contGo;
 

    [SerializeField]
    Animator anim;

    //[InspectorName("鐢诲竷")]
    public CanvasGroup Canvas;

    public DynamicBar weaponMainBar;
    public DynamicBar weaponSecBar;

    public TextMeshProUGUI weaponMainText;
    public TextMeshProUGUI weaponSecText;

    public Image weaponMainIcon;
    public Image weaponSecIcon;

   IVehicleUIController controller;


    private void Awake()
    {
        //涓嶇劧alpha浼氳烦鍥炲幓
        //鎴戞病鎷涗簡
        //anim.enabled = false;
        //Canvas.alpha = 0;
        //anim.enabled = true;


        controller = contGo.GetComponent<IVehicleUIController>();
        if (controller == null) return;
        controller.SetWeaponState += SetWeaponState;
        controller.OnFillChange += FillChange;
        controller.OnColorChange += ColorChange;
        controller.OnStateChange += StateChange;
        controller.OnTextChange += TextChange;
        controller.OnIconChange += IconChange;

    }


    private void OnDestroy()
    {
        if (controller == null) return;
        controller.SetWeaponState -= SetWeaponState;
        controller.OnFillChange -= FillChange;
        controller.OnColorChange -= ColorChange;
        controller.OnStateChange -= StateChange;
        controller.OnTextChange -= TextChange;
        controller.OnIconChange -= IconChange;
        controller = null;
    }

    void ColorChange(bool isMain, Color color)
    {
        (isMain ? weaponMainBar : weaponSecBar).SetColor(color);
    }
    void FillChange(bool isMain, float value)
    {
        (isMain ? weaponMainBar : weaponSecBar).SetFill(value);
    }
    void StateChange(bool state)
    {
        //Canvas.alpha = state ? 1 : 0;
        anim.SetBool(Constants.k_AnimIsActiveParameter, state);
    }
    void TextChange(bool isMain, string text)
    {
        (isMain ? weaponMainText : weaponSecText).SetText(text);
    }
    void IconChange(bool isMain, Sprite icon)
    {
        (isMain ? weaponMainIcon : weaponSecIcon).sprite= icon;
    }
    void SetWeaponState(bool isMain, bool state)
    {
        var go = (isMain ? weaponMainBar : weaponSecBar).transform.parent;
        if (go) go.gameObject.SetActive(state);
    }
}
