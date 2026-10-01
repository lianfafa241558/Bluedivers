using System;
using System.Collections.Generic;
using FPSGame.Core;
using FPSGame.Core.Interface;
using FPSGame.Attributes;
using FPSGame.Game;
using UnityEngine;
using UnityEngine.EventSystems;
using FPSGame.Utils;
using FPSGame.Gameplay;

namespace FPSGame.UI
{
using static FPSGame.WndTools.WndRootTool;
using FPSGame.Managers;
using FPSGame.GameData;

/// <summary>
/// 工程岗配置界面
/// </summary>
[AddComponentMenu("UI/窗口/工程岗")]
public class VehicleWnd : Window
{

    [Foldout("配置", true)]
    [SerializeField]
    private Transform btn_Cancel,weaponRoot, weaponName,
        weaponitemListLayout, weaponUpgradeItemLayout,weaponUpgradeSelectLayout,weaponUpgradeBuyLayout,
        weaponDescRoot,weaponDescText,
        showDescButton, weaponLeftRoot, weaponRightRoot;

    [SerializeField]
    private Transform skinFrame,blendFrame,skinExpandRoot;

    [SerializeField]
    private Sprite buyIcon, unbuyIcon;
    [SerializeField]
    private Color buyColor, unbuyColor, selectColor, unSelectColor,unLevelColor;


    [Foldout("配置提示", true)]
    [SerializeField]
    private Transform tipRoot, tipName, tipType, tipDesc, tipIcon, tipOpter;

    private ArchivesData_SO arch;

    [SerializeField]
    private Camera m_SelectVehicleCamera;
    [SerializeField]
    private ShowVehicle[] showVehicles;
    //[SerializeField]
    private int nowSelect=0;
    //[SerializeField]

    //private GameObject leftModel, rightModel;
    private bool meetSave,selectIsBlend;



    #region 生命周期
    public void Init()
    {
    }
    public void Uninit()
    {
    }

    protected override void FirstShowWnd()
    {
        arch= ArchivesData_SO.Current;
        SetCilck(btn_Cancel, () => {
            wndManager.PlaySound(new("UI/UI_Button_Back"));
            SetWndState(false);
        });

        SetActive(showDescButton,false);

        SetCilck(showDescButton, () => {
            weaponDescRoot.GetComponent<Animator>().Play("Entry");
            SetActive(showDescButton, false);
        });
       
        SetCilck(weaponDescRoot, () => {
            weaponDescRoot.GetComponent<Animator>().Play("Exit");
            SetActive(showDescButton, true);
        });
        for (int y = 0; y < weaponitemListLayout.childCount; ++y)
        {
            var item = weaponitemListLayout.GetChild(y);
            if (y < showVehicles.Length)
            {
                var a = y;
                SetActive(item, true);
                SetSprite(item.GetChild(0), showVehicles[y].data.icon);
                SetCilck(item, () => {
                    SwitchItem(a);
                });
            }
            else
            {
                SetActive(item, false);
            }
        }

        SetCilck(skinFrame, () => {
            if (GetActive(skinExpandRoot)&&!selectIsBlend) ExitSkinFrame();
            else EnterSkinFrame(false);
        });
        SetCilck(blendFrame, () => {
            if (GetActive(skinExpandRoot) && selectIsBlend) ExitSkinFrame();
            else EnterSkinFrame(true);
        });
        for (int i = 0; i < skinExpandRoot.GetChild(1).childCount; ++i)
        {
            int a = i;
            SetCilck(skinExpandRoot.GetChild(1,i), () => {
                //Debug.LogError("点击 "+a);
                SelectSkinItem(a);
            });
            SetButtonEnter(skinExpandRoot.GetChild(1,i), data => EnterSkinItem(a));
            SetButtonExit(skinExpandRoot.GetChild(1, i), data => ExitSkinItem());
        }

        SetSlider(skinExpandRoot.GetChild(2), SetBlendScale);

        SetButtonEnter(weaponLeftRoot, data => EnterSelectWeapon(false));
        SetButtonExit(weaponLeftRoot, data => ExitSelectWeapon(false));
        SetButtonEnter(weaponRightRoot, data => EnterSelectWeapon(true));
        SetButtonExit(weaponRightRoot, data => ExitSelectWeapon(true));


        SetCilck(weaponLeftRoot.GetChild(3, 0), () =>
        {
            SwitchWeapon(false,false);
        });
        SetCilck(weaponLeftRoot.GetChild(3, 1), () =>
        {
            SwitchWeapon(false,true);
        });
        SetCilck(weaponRightRoot.GetChild(3, 0), () => {
            SwitchWeapon(true, false);
        });
        SetCilck(weaponRightRoot.GetChild(3, 1), () => {
            SwitchWeapon(true, true);
        });
        m_SelectVehicleCamera.transform.position = showVehicles[0].LookPoint.position;
        m_SelectVehicleCamera.transform.rotation = showVehicles[0].LookPoint.rotation;
    }

    protected override void ShowWnd()
    {
        WindowState = WindowStateEnum.UI;
        m_SelectVehicleCamera.gameObject.SetActive(true);
        ActorsManager.Player.gameObject.SetActive(false);
        InputManager.AddListenerCancel(Cancel);
        SetActive(skinExpandRoot,false);//拓展
        SwitchItem(0);
    }

    protected override void HideWnd()
    {

        if (meetSave) ArchivesData_SO.Current.Save();
        m_SelectVehicleCamera.gameObject.SetActive(false);
        if(ActorsManager.Player.IsValidMono()) ActorsManager.Player.gameObject.SetActive(true);
        WindowState = WindowStateEnum.Game;
        InputManager.RemoveListenerCancel(Cancel);
    }
    private bool Cancel()
    {
        if (!State) return false;
        wndManager.PlaySound(new("UI/UI_Button_Back"));
        SetWndState(false);
        return true;
    }

    /// <summary>
    /// 切换载具类型
    /// </summary>
    /// <param name="index"></param>
    void SwitchItem(int index)
    {
        nowSelect = index % showVehicles.Length;
        var data= showVehicles[index].data;
        if (SetActive(weaponLeftRoot, data.weaponLefts.Length > 0))
        {
            SwitchWeapon(false,true);
        }
        if (SetActive(weaponRightRoot, data.weaponRights.Length > 0))
        {
            SwitchWeapon(true, true);
        }
        SetText(weaponName, data.vehicleName);
        SetText(weaponDescText, data.desc);
        SetSprite(blendFrame.GetChild(0, 0), NowVehicle.data.Blends[NowArchData.blendIndex].icon);
        SetSprite(skinFrame.GetChild(0, 0), NowVehicle.data.Diffs[NowArchData.skinIndex].icon);

        ExitSkinFrame();
    }

    private void LateUpdate()
    {
        m_SelectVehicleCamera.transform.position = Vector3.Lerp(
            m_SelectVehicleCamera.transform.position, 
            showVehicles[nowSelect].LookPoint.position,
            5*Time.deltaTime
        );

        m_SelectVehicleCamera.transform.rotation = Quaternion.Slerp(
            m_SelectVehicleCamera.transform.rotation,
            showVehicles[nowSelect].LookPoint.rotation,
            5 * Time.deltaTime
        );

        // 检测鼠标左键按下
        if (Input.GetMouseButtonDown(0))
        {
            // 核心判断：检查是否点在了UI上
            bool isOverUI = EventSystem.current.IsPointerOverGameObject();
            // 如果没有点在UI上
            if (!isOverUI)
            {
                ExitSkinFrame();
            }
        }
    }

    private ShowVehicle NowVehicle => showVehicles[nowSelect];
    private ArchivesData_SO.ArchVehicleData NowArchData => arch.VehicleCustomDic[NowVehicle.data.vehicleName];


    private void SwitchWeapon(bool isRight, bool isAdd)
    {

        var data = NowVehicle.data;
        if (isRight)
        {
            NowArchData.rightWeaponIndex= (NowArchData.rightWeaponIndex + (isAdd ? 1 : data.weaponRights.Length - 1)) % data.weaponRights.Length;
        }
        else
        {
            NowArchData.leftWeaponIndex= (NowArchData.leftWeaponIndex + (isAdd ? 1 : data.weaponLefts.Length - 1)) % data.weaponLefts.Length;
        }
        var count = (isRight ? data.weaponLefts : data.weaponRights).Length;
        var root = isRight ? weaponRightRoot : weaponLeftRoot;
        var index= isRight ? NowArchData.rightWeaponIndex : NowArchData.leftWeaponIndex;
        var list= isRight ? data.weaponRights : data.weaponLefts;

        SetText(root.GetChild(0), list[index].name);
        SetSprite(root.GetChild(2), list[index].icon);
        SetText(root.GetChild(4), (index + 1) + "/" + data.weaponRights.Length);
        if (isRight)
        {
            if (NowVehicle.weaponPointR.childCount>0)
            {
                NowVehicle.mpb.Remove(NowVehicle.weaponPointR.GetChild(0).transform);
                Destroy(NowVehicle.weaponPointR.GetChild(0).gameObject);
            }
            var rightModel = Instantiate(list[index].go, NowVehicle.weaponPointR);
            rightModel.transform.localPosition = Vector3.zero;
            rightModel.transform.localRotation = Quaternion.identity;
            NowVehicle.mpb.Add(rightModel.transform);
        }
        else
        {
            if (NowVehicle.weaponPointL.childCount > 0)
            {
                NowVehicle.mpb.Remove(NowVehicle.weaponPointL.GetChild(0).transform);
                Destroy(NowVehicle.weaponPointL.GetChild(0).gameObject);
            }
            var leftModel = Instantiate(list[index].go, NowVehicle.weaponPointL);
            leftModel.transform.localPosition = Vector3.zero;
            leftModel.transform.localRotation = Quaternion.identity;
            NowVehicle.mpb.Add(leftModel.transform);
        }
        NowVehicle.mpb.Set("_BaseMap", NowVehicle.data.Diffs[NowArchData.skinIndex].texture).Apply();
        NowVehicle.mpb.Set("_BlendingMap", NowVehicle.data.Blends[NowArchData.blendIndex].texture).Apply();

        meetSave = true;
    }

    /// <summary>
    /// 鼠标进入武器
    /// </summary>
    private void EnterSelectWeapon(bool isRight)
    {
        var count = (isRight ? showVehicles[nowSelect].data.weaponLefts : showVehicles[nowSelect].data.weaponRights).Length;
        var root = isRight ? weaponRightRoot : weaponLeftRoot;
        SetActive(root.GetChild(3), true);
        SetActive(root.GetChild(3, 0), count > 1);
        SetActive(root.GetChild(3, 1), count > 1);
    }
    /// <summary>
    /// 鼠标离开武器
    /// </summary>
    private void ExitSelectWeapon(bool isRight)
    {
        var root= isRight ? weaponRightRoot : weaponLeftRoot;
        SetActive(root.GetChild( 3), false);
        SetActive(root.GetChild( 3, 0), false);
        SetActive(root.GetChild( 3, 1), false);
    }


    void EnterSkinItem(int index)
    {
        if (selectIsBlend)
        {
            NowVehicle.mpb.Set("_BlendingMap", NowVehicle.data.Blends[index].texture).Apply();
            SetText(skinExpandRoot.GetChild(0, 0), NowVehicle.data.Blends[index].name);
            SetSprite(blendFrame.GetChild(0, 0), NowVehicle.data.Blends[index].icon);
        }
        else
        {
            NowVehicle.mpb.Set("_BaseMap", NowVehicle.data.Diffs[index].texture).Apply();
            SetText(skinExpandRoot.GetChild(0, 0), NowVehicle.data.Diffs[index].name);
            SetSprite(skinFrame.GetChild(0, 0), NowVehicle.data.Diffs[index].icon);
        }
    }
    void ExitSkinItem()
    {

        if (selectIsBlend)
        {
            var index= NowArchData.blendIndex;
            NowVehicle.mpb.Set("_BlendingMap", NowVehicle.data.Blends[index].texture).Apply();
            SetText(skinExpandRoot.GetChild(0, 0), NowVehicle.data.Blends[index].name);

        }
        else
        {
            var index = NowArchData.skinIndex;
            NowVehicle.mpb.Set("_BaseMap", NowVehicle.data.Diffs[index].texture).Apply();
            SetText(skinExpandRoot.GetChild(0, 0), NowVehicle.data.Diffs[index].name);
        }
        meetSave = true;
    }


    void SelectSkinItem(int index)
    {
        
        if (selectIsBlend) NowArchData.blendIndex = index;
        else NowArchData.skinIndex = index;

        meetSave = true;
        EnterSkinItem(index);
    }
    private void SetBlendScale(float value)
    {

        NowVehicle.mpb.Set("_BlendingScale", value).Apply();
        NowArchData.blendScale = value;
        meetSave = true;
    }

    /// <summary>
    /// 开启皮肤选择
    /// </summary>
    /// <param name="isBlend"></param>
    void EnterSkinFrame(bool isBlend)
    {
        selectIsBlend = isBlend;
        SetActive(skinExpandRoot,true);
        SetActive(skinExpandRoot.GetChild(2), isBlend);
        SetText(skinExpandRoot.GetChild(0, 0), isBlend? 
            NowVehicle.data.Blends[NowArchData.blendIndex].name: 
            NowVehicle.data.Diffs[NowArchData.skinIndex].name
        );

        if (isBlend)
        {
            var data = showVehicles[nowSelect].data.Blends;
            for (int i=0;i< skinExpandRoot.GetChild(1).childCount; ++i)
            {
                var item = skinExpandRoot.GetChild(1, i);
                if (SetActive(item, i< data.Length))
                {
                    SetSprite(item.GetChild(0, 0), data[i].icon);
                }
            }
        }
        else
        {
            var data = showVehicles[nowSelect].data.Diffs;
            for (int i = 0; i < skinExpandRoot.GetChild(1).childCount; ++i)
            {
                var item = skinExpandRoot.GetChild(1, i);
                if (SetActive(item, i < data.Length))
                {
                    SetSprite(item.GetChild(0, 0), data[i].icon);
                }
            }
        }
    }

    /// <summary>
    /// 关闭皮肤选择
    /// </summary>
    void ExitSkinFrame()
    {
        SetActive(skinExpandRoot, false);
    }

    #endregion



}
}
