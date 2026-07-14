using System;
using System.Collections.Generic;
using Core;
using FPSGame.Attribute;
using Unity.FPS.Game;
using UnityEngine;
using UnityEngine.EventSystems;
using Utils;
using static WndTools.WndRootTool;

/// <summary>
/// 工程岗配置界面
/// </summary>
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
        WndManager.Instance.vehicleWnd = this;
    }
    public void Uninit()
    {
        WndManager.Instance.vehicleWnd = null;
    }

    protected override void FirstShowWnd()
    {
        arch= ArchiveSvc.Archive;
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

        if (meetSave) ArchiveSvc.Archive.Save();
        m_SelectVehicleCamera.gameObject.SetActive(false);
        if(ActorsManager.Player.IsValid()) ActorsManager.Player.gameObject.SetActive(true);
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

    #region 备份
    /*

    private string showParameterType, showParameterValue;
    ArchivesData_SO.WeaponUpgradeData archWeaponData;
    private void ShowWeaponWnd()
    {
        var type = (WeaponTypeEnum)nowSelectWeapon;
        var weaponTemp = arch.weapons[type][arch.weaponSelect[type]];
        var weaponInst = showWeapon = Instantiate(weaponTemp, m_SelectWeaponCamera.transform.GetChild(0));
        if (weaponInst.ShowRoot)
        {
            weaponInst.transform.localEulerAngles = weaponInst.ShowRoot.transform.localEulerAngles;
            weaponInst.transform.localPosition = -weaponInst.ShowRoot.transform.localPosition;
            weaponInst.transform.localScale = weaponInst.ShowRoot.transform.localScale;
        }
        if (weaponInst.WeaponMuzzle != weaponInst.WeaponRoot.transform) SetActive(weaponInst.WeaponMuzzle, false);
        SetText(weaponName, weaponInst.WeaponName);


        //载具列表
        int count = arch.weapons[type].Count;
        for (int i = 0; i < weaponitemListLayout.childCount; ++i)
        {
            if (SetActive(weaponitemListLayout.GetChild(i), i < count))
            {
                SetSprite(weaponitemListLayout.GetChild(i, 0), data.weapons[type][i].WeaponIcon);
            }
        }


        SetActive(tipRoot, false);
        var lenghts = weaponInst.UpgradeCount();
        var levels = weaponInst.UpgradeLevel();
        archWeaponData = ArchiveSvc.Archive.weaponUpgradeDic.TryGet(arch.ID + "_" + weaponInst.WeaponName, new(arch.ID + "_" + weaponInst.WeaponName, lenghts.Length));
        var totleLenght = IEnumerableUtils.Sum(lenghts);


        for (int y = 0; y < weaponUpgradeItemLayout.childCount; ++y)
        {

            for (int x = 0; x < 3; ++x)
            {
                bool show = y < lenghts.Length && x < lenghts[y];
                SetActive(weaponUpgradeItemLayout.GetChild(y, x), show);
                if (show)
                {
                    SetSprite(weaponUpgradeItemLayout.GetChild(y, x, 0), showWeapon.GetUpgrade(y, x).icon);
                    int state = 0;
                    if (arch.Level < levels[y]) state = 3;
                    else if (archWeaponData.selectIndex[y] == x) state = 0;
                    else if (archWeaponData.GetBuy(y, x)) state = 1;
                    else state = 2;

                    SetUpgradeItemButton(y, x, state);
                }
            }
            if (y < lenghts.Length && lenghts[y] > 0)
            {
                SetActive(weaponUpgradeItemLayout.GetChild(y), true);
                SetFill(weaponUpgradeItemLayout.GetChild(y), Mathf.Clamp01(0.5f * (lenghts[y] - 1)));
            }
            else
            {
                SetActive(weaponUpgradeItemLayout.GetChild(y), false);
            }

            bool showSelect = y < lenghts.Length;
            SetActive(weaponUpgradeSelectLayout.GetChild(y), showSelect);
            if (showSelect)
            {
                SetUpgradeSelectButton(archWeaponData.selectIndex, y, archWeaponData.selectIndex[y], false);
                SetActive(weaponUpgradeSelectLayout.GetChild(y, 2), arch.Level < levels[y]);
                SetActive(weaponUpgradeSelectLayout.GetChild(y, 1), arch.Level >= levels[y]);
                SetText(weaponUpgradeSelectLayout.GetChild(y, 2, 0), levels[y]);
            }


        }
        SetBuyCountLayout(totleLenght, archWeaponData.BuyCount);


        weaponInst.ApplyUpgrade(archWeaponData.selectIndex, archWeaponData.selectModuleIndex);

        SetText(weaponDescText, weaponInst.desc);
        if (GetActive(showParameterButton)) weaponParameterRoot.GetComponent<Animator>().Play("Exit", 0, 1);
        if (GetActive(showDescButton)) weaponDescRoot.GetComponent<Animator>().Play("Exit", 0, 1);

        RefreshLayout(weaponDescText);

        RefreshLayout(weaponUpgradeItemLayout);

        

    }



    /// <summary>
    /// 鼠标进入升级框
    /// </summary>
    private void ShowTip(int y, int x)
    {
        SetActive(tipRoot, true);
        tipRoot.position = new(tipRoot.position.x, Input.mousePosition.y - 50);
        var data = showWeapon.GetUpgrade(y, x);
        SetText(tipName, data.name);
        SetText(tipType, data.type);
        SetText(tipDesc, data.desc);
        SetSprite(tipIcon, data.icon);
        var select = archWeaponData.selectIndex[y];

        showWeapon.ResetAllChangeValues();

        List<ModifyAttrData> oldData, newData;
        if (select == -1)
        {
            oldData = new();
            newData = data.modifys;
        }
        else if (select == x)
        {
            oldData = showWeapon.GetUpgrade(y, select).modifys;
            newData = new();
        }
        else
        {
            oldData = showWeapon.GetUpgrade(y, select).modifys;
            newData = data.modifys;
        }
        showWeapon.TryUpgrade(oldData, newData);
        
    }
    /// <summary>
    /// 鼠标离开升级框
    /// </summary>
    private void HideTip(int y, int x)
    {
        if (!GetActive(tipRoot)) return;
        SetActive(tipRoot, false);
        showWeapon.ResetAllChangeValues();
    }
    /// <summary>
    /// 鼠标在升级框内
    /// </summary>
    private void MoveTip()
    {
        tipRoot.position = new(tipRoot.position.x, Input.mousePosition.y - 50);
    }
    /// <summary>
    /// 点击升级
    /// </summary>
    private void SelectUpgrade(int y, int x)
    {
        if (arch.Level < showWeapon.UpgradeLevel(y))
        {
            wndManager.PlaySound(new("UI/UI_Reward2", volume: 0.1f));
            return;
        }
        var upgrade = showWeapon.GetUpgrade(y, x);
        if (archWeaponData.GetBuy(y, x))
        {
            SetUpgradeSelectButton(archWeaponData.selectIndex, y, x, true);
            meetSave = true;
        }
        else
        {
            wndManager.CreatTip(new() {
                title = upgrade.name,
                desc = upgrade.desc + "\n\n要购买这项升级吗?",
                optA_Click = () => {
                    archWeaponData.SetBuy(y, x);
                    SetUpgradeItemButton(y, x, 1);
                    SetBuyCountLayout(-1, archWeaponData.BuyCount);
                    wndManager.PlaySound(new("UI/UI_Reward", volume: 0.25f));
                    wndManager.PlaySound(new(data.Speech(SpeechTypeEnum.Upgrade).Clip, AudioGroups.Player, 1, 0.5f));
                    meetSave = true;
                },
                costs = upgrade.cost.ToArray(),
                optA_Text = "确认",
                optB_Text = "取消"
            });
        }

        SetActive(tipRoot, false);
    }

    /// <summary>
    /// 设置选项按钮 0选择 1购买 2未购买
    /// </summary>
    public void SetUpgradeItemButton(int y, int x, int state)
    {
        switch (state)
        {
            case 0:
                SetColor(weaponUpgradeItemLayout.GetChild(y, x), selectColor);
                SetColor(weaponUpgradeItemLayout.GetChild(y, x, 1), Color.white);

                break;
            case 1:

                SetColor(weaponUpgradeItemLayout.GetChild(y, x), buyColor);
                SetColor(weaponUpgradeItemLayout.GetChild(y, x, 1), new(0, 0, 0, 0));
                break;
            case 2:
                SetColor(weaponUpgradeItemLayout.GetChild(y, x), Color.black);
                SetColor(weaponUpgradeItemLayout.GetChild(y, x, 1), unbuyColor);
                break;
            case 3:
                SetColor(weaponUpgradeItemLayout.GetChild(y, x), Color.black);
                SetColor(weaponUpgradeItemLayout.GetChild(y, x, 1), unSelectColor);
                break;
        }
    }

    /// <summary>
    /// 设置选择按钮
    /// </summary>
    public void SetUpgradeSelectButton(int[] arr, int y, int x, bool set)
    {

        if ((set && arr[y] == x) || x == -1)//重复点击或者本身就是-1都有可能
        {
            if (set)
            {
                showWeapon.RemoveUpgrade(y, x);
                arr[y] = -1;
                wndManager.PlaySound(new("UI/UI_Ready", volume: 0.5f));
            }
            SetActive(weaponUpgradeSelectLayout.GetChild(y, 0), false);
            SetColor(weaponUpgradeSelectLayout.GetChild(y), Color.black);
            if (x != -1) SetUpgradeItemButton(y, x, 1);//将原来选择的位置重置
            else
            {
                SetColor(weaponUpgradeSelectLayout.GetChild(y), arch.Level < showWeapon.UpgradeLevel(y) ? unLevelColor : Color.black);
            }
        }
        else //X有效且不相同
        {
            SetActive(weaponUpgradeSelectLayout.GetChild(y, 0), true);
            SetSprite(weaponUpgradeSelectLayout.GetChild(y, 0), GetSprite(weaponUpgradeItemLayout.GetChild(y, x, 0)));
            SetColor(weaponUpgradeSelectLayout.GetChild(y), Color.white);

            if (set)
            {

                if (arr[y] != -1)
                {
                    showWeapon.RemoveUpgrade(y, arr[y]);
                    SetUpgradeItemButton(y, arr[y], 1);
                }
                arr[y] = x;
                wndManager.PlaySound(new("UI/UI_Ready"));
                SetUpgradeItemButton(y, x, 0);
                showWeapon.ApplyUpgrade(y, x);
            }
        }
        if (set)
        {
            //SetParameter();
        }
    }
    private void SetBuyCountLayout(int max, int count)
    {

        for (int i = 0; i < weaponUpgradeBuyLayout.childCount; ++i)
        {
            if (max != -1) SetActive(weaponUpgradeBuyLayout.GetChild(i), i < max);
            SetSprite(weaponUpgradeBuyLayout.GetChild(i), i < count ? buyIcon : unbuyIcon);
        }
    }

    */

    #endregion


}
