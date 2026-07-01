using Core;
using UnityEngine;
using UnityEngine.UI;
using static WndTools.WndRootTool;

public class FlareGun : MonoBehaviour
{
    public Unity.FPS.Game.WeaponController weapon;
    public Infrared line;
    //public Text itemName;
    //public Text distance;
    public Image icon1, icon2;
    public Sprite empty;
    public Transform itemName, distance, tip;

    private AirdropController.AirdropData data;
    private int state;
    void Update()
    {
        if (AirdropController.WaitRelease!= data)
        {
            data = AirdropController.WaitRelease;
            
            
            if (data!=null)
            {
                SetSprite(icon1, data.cfg.icon);
                SetSprite(icon2, data.cfg.icon);
                SetText(tip,"点击设置空投");
                SetText(itemName, data.cfg.showName);
            }
            else
            {
                SetSprite(icon1, empty);
                SetSprite(icon2, empty);
                SetText(tip,"点击进行标记");
                SetText(itemName, "");
            }
            state = -1;
        }
        if (data != null)
        {
            int dis = Mathf.RoundToInt(Vector3.Distance(line.line.GetPosition(1), transform.position));
            SetText(distance, line.RayGo ? dis + "M" : "ERROR");
            if (state!=1&&dis > weapon.CurrentWeaponRange)
            {
                state = 1;
                SetColor(distance, new(1, 0, 0));
                SetText(tip, "超出部署范围");
            }
            else if(state != 0 && dis <= weapon.CurrentWeaponRange)
            {
                state = 0;
                SetColor(distance, new(1, 0.905f, 0));
                SetText(tip, "点击设置空投");
            }

            return;
        }
        else
        {
            bool useDeault = true;
            if (line.RayGo)
            {
                int dis = Mathf.RoundToInt(Vector3.Distance(line.line.GetPosition(1), transform.position));
                if (line.RayGo.TryGetComponent<BaseObject>(out var obj))
                {
                    SetText(itemName, obj.ShowName);
                    icon1.sprite = icon2.sprite = obj.Portrait ? obj.Portrait : empty;
                    SetText(distance, dis + "M");
                    useDeault = false;
                }
                else if (line.RayGo != null)
                {
                    SetText(itemName, "");
                    icon1.sprite = icon2.sprite = empty;
                    SetText(distance, dis + "M");
                    useDeault = false;
                }

                if (dis > weapon.CurrentWeaponRange)
                {
                    SetColor(distance, new(1, 0, 0));
                }
                else
                {
                    SetColor(distance, new(1, 0.905f, 0));
                }
            }
            if (useDeault)
            {
                SetText(itemName, "");
                icon1.sprite = icon2.sprite = empty;
                SetText(distance, "ERROR");
            }
        }
    }
}
