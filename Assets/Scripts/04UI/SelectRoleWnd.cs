using System;
using Core;

using Unity.FPS.Game;
using UnityEngine;
using Utils;
using static WndTools.WndRootTool;

/// <summary>
/// 选择角色界面，这个界面不应该在除舰桥模式以外的界面打开
/// </summary>
public class SelectRoleWnd : Window
{
    [Foldout("配置人物",true)]
    [SerializeField]
    private Transform roleRoot, weaponListRoot, 
        btn_Cancel, btn_Left, btn_Right,btn_Random, btn_Select, 
        txt_Name,txt_Level,txt_Switch;

    [Foldout("配置武器", true)]
    [SerializeField]
    private Transform weaponRoot,weaponName,weaponType,
        weaponitemListLayout, weaponUpgradeItemLayout,weaponUpgradeSelectLayout,weaponUpgradeBuyLayout,
        weaponParameterRoot,weaponParameterLayout, weaponDescRoot,weaponDescText,
        showDescButton, showParameterButton;
    [SerializeField]
    private Sprite buyIcon, unbuyIcon;
    [SerializeField]
    private Color buyColor, unbuyColor, selectColor, unSelectColor,unLevelColor;


    [Foldout("配置武器提示框", true)]
    [SerializeField]
    private Transform tipRoot, tipName, tipType, tipDesc, tipIcon, tipOpter;

    [Foldout("配置模组", true)]
    [SerializeField]
    private Transform moduleRoot, moduleFrame, moduleIocn, moduleTitle, moduleName, moduleType, moduleLayout, moduleOpterLayout;


    private RoleData_SO data;
    private ArchivesData_SO.ArchRoleData arch;

    private Camera m_SelectRoleCamera, m_SelectWeaponCamera;
    private Transform m_SelectPoint,m_ShowWeaponPoint;

    private BridgeRoleManager m_manager;
    private Transform m_showModleGo;

    private int nowSelectWeapon=0;
    private int nowSelectModule = 0;
    private WeaponPlayerController showWeapon;

    private bool meetSave;

    #region 生命周期

    private RoleWndState wndState;
    private RoleWndState WndState
    {
        get=> wndState;
        set {
            wndState = value;
            bool role = value == RoleWndState.Role;
            SetActive(roleRoot, role);
            SetActive(weaponRoot, !role);
            m_SelectRoleCamera.enabled = role;

            SetActive(m_SelectRoleCamera.gameObject, role);
            SetActive(m_SelectWeaponCamera.gameObject, !role);
            if (showWeapon) Tool.Destroy(showWeapon.gameObject);
            switch (value)
            {
                case RoleWndState.Role:
                    break;
                case RoleWndState.Switch:
                    break;
                case RoleWndState.Weapon:
                    wndManager.PlaySound(new("UI/UI_Bubble"));
                    ShowWeaponWnd();
                    break;
            }
        }
    }
    public void Init()
    {
        WndManager.Instance.selectRoleWnd = this;
    }
    public void Uninit()
    {
        WndManager.Instance.selectRoleWnd = null;
    }

    protected override void FirstShowWnd()
    {
        #region 选择人物
        SetCilck(btn_Cancel, () =>
        {
            wndManager.PlaySound(new("UI/UI_Button_Back"));
            WndState = RoleWndState.Role;
            SetWndState(false);
        });
        SetCilck(btn_Left, () =>
        {
            m_manager.SwitchShowRole(false, out var go, out var data, out var arch, out bool isNow);
            Refresh(go, data, arch, isNow);
        });
        SetCilck(btn_Right, () =>
        {
            m_manager.SwitchShowRole(true, out var go, out var data, out var arch, out bool isNow);
            Refresh(go, data, arch, isNow);
        });
        SetCilck(btn_Random, () =>
        {
            m_manager.RandomShowRole(out var go, out var data, out var arch, out bool isNow);
            Refresh(go, data, arch, isNow);
        });
        SetCilck(btn_Select, () =>
        {
            m_manager.SelectRole();
            meetSave = true;
            SetActive(btn_Select, false);
            wndManager.PlaySoundData(data.SpeechGroup(SpeechTypeEnum.Select).Get());
        });
        #endregion
        #region 选择升级

        for (int i = 0; i < weaponListRoot.childCount; ++i)
        {
            int a = i;
            SetButtonEnter(weaponListRoot.GetChild(a), (data => EnterSelectWeapon(a)));
            SetButtonExit(weaponListRoot.GetChild(a), (data => ExitSelectWeapon(a)));
            SetCilck(weaponListRoot.GetChild(a), () =>
            {
                WndState = RoleWndState.Weapon;
            });
            SetCilck(weaponListRoot.GetChild(a, 3, 0), () =>
            {
                SwitchWeapon(a, true);
            });
            SetCilck(weaponListRoot.GetChild(a, 3, 1), () =>
            {
                SwitchWeapon(a, false);
            });
        }
        for (int i = 0; i < weaponitemListLayout.childCount; ++i)
        {
            var a = i;
            SetCilck(weaponitemListLayout.GetChild(i), () => {
                arch.weaponSelect[(WeaponTypeEnum)nowSelectWeapon] = a;
                meetSave = true;
                WndState = RoleWndState.Weapon;
            });
        }


        #endregion

        #region 选择武器
        SetActive(false, showDescButton, showParameterButton);

        SetCilck(showDescButton, () =>
        {
            weaponDescRoot.GetComponent<Animator>().Play("Entry");
            SetActive(showDescButton, false);
        });
        SetCilck(showParameterButton, () =>
        {
            weaponParameterRoot.GetComponent<Animator>().Play("Entry");
            SetActive(showParameterButton, false);
        });
        SetCilck(weaponDescRoot, () =>
        {
            weaponDescRoot.GetComponent<Animator>().Play("Exit");
            SetActive(showDescButton, true);
        });
        SetCilck(weaponParameterRoot, () =>
        {
            weaponParameterRoot.GetComponent<Animator>().Play("Exit");
            SetActive(showParameterButton, true);
        });
        for (int y = 0; y < weaponUpgradeItemLayout.childCount; ++y)
        {
            for (int x = 0; x < 3; ++x)
            {
                int a = y, b = x;
                var item = weaponUpgradeItemLayout.GetChild(a, b).TryGetOrAddComponent<ButtonEnterDetector>();

                item.Enter = data => ShowTip(a, b);
                item.In = data => MoveTip();
                item.Enter = data => HideTip(a, b);

                SetCilck(weaponUpgradeItemLayout.GetChild(a, b), () =>
                {
                    SelectUpgrade(a,b);
                });


            }
        }
        #endregion

        InitModule();

    }



    protected override void ShowWnd()
    {

        var selectCameraGo = TransformUtils.SceenFind("SelectRoleCameras");
        if (selectCameraGo)
        {
            m_SelectRoleCamera = selectCameraGo.transform.GetChild(0).GetComponent<Camera>();
            m_SelectWeaponCamera = selectCameraGo.transform.GetChild(1).GetComponent<Camera>();
            m_ShowWeaponPoint = m_SelectWeaponCamera.transform.GetChild(0).transform;
        }
        var selectPointGo = TransformUtils.SceenFind("ShowStudentPoint");
        if (selectPointGo)
        {
            m_SelectPoint = selectPointGo.transform;
            SetActive(m_SelectPoint.gameObject, true);
        }
        m_manager = FindObjectOfType<BridgeRoleManager>();
        WindowState = WindowStateEnum.UI;
        ActorsManager.Player.gameObject.SetActive(false);
        m_manager.StartShowRole(out var go, out var data,out var arch, out bool isNow);
        Refresh(go, data, arch, isNow);

        EnterSelectWeapon(-1);

        InputManager.AddListenerCancel(Cancel);

        WndState = RoleWndState.Role;
    }

    protected override void HideWnd()
    {

        if(meetSave)ArchiveSvc.Archive.Save();
        ActorsManager.Player.gameObject.SetActive(true);
        WindowState = WindowStateEnum.Game;
        if (m_SelectRoleCamera) SetActive(m_SelectRoleCamera.gameObject, false);
        if(m_SelectPoint) SetActive(m_SelectPoint.gameObject, false);
        InputManager.RemoveListenerCancel(Cancel);

    }


    private Vector3 lastDragPoint;
    private void Update()
    {
       

        if (InputManager.GetDown(InputState.Fire))
        {
            lastDragPoint = Input.mousePosition;
        }
        else if(InputManager.Get(InputState.Fire))
        {
            var dx = (Input.mousePosition-lastDragPoint).x;
            lastDragPoint = Input.mousePosition;
            var rotaBody = Mathf.Lerp(0, dx >0?-10:10, Time.deltaTime *Mathf.Abs(dx)*5);
            if (wndState == RoleWndState.Role)
            {
                m_showModleGo.localEulerAngles += Vector3.up * rotaBody;
            }
            else if (wndState == RoleWndState.Weapon)
            {
                m_ShowWeaponPoint.localEulerAngles += Vector3.up * rotaBody;
            }
        }
        else
        {
            var angle = m_showModleGo.localEulerAngles;
            var rotaBody = Mathf.Lerp(angle.y, angle.y>180?360:0, Time.deltaTime * 2);
            m_showModleGo.localEulerAngles =new(angle.x,rotaBody ,angle.z);

            m_ShowWeaponPoint.localEulerAngles += Vector3.up * Time.deltaTime * 20;
        }
    }

    private bool Cancel()
    {
        if (!State) return false;
        wndManager.PlaySound(new("UI/UI_Button_Back"));
        switch (wndState)
        {
            case RoleWndState.Role:
                SetWndState(false);
                break;
            case RoleWndState.Switch:
                InputManager.AddListenerCancel(Cancel);
                break;
            case RoleWndState.Weapon:
                WndState = RoleWndState.Role;
                InputManager.AddListenerCancel(Cancel);
                break;
        }
        
        return true;
    }
    #endregion 

    #region 人物界面武器列表
    private void SwitchWeapon(int index,bool left)
    {
        var type = (WeaponTypeEnum)index;

        int count = data.weapons[type].Count;
        arch.weaponSelect[type] = (arch.weaponSelect[type] + count + (left ? -1 : 1)) % count;
        SetWeaponPreView(index, data.weapons[type][arch.weaponSelect[type]]);
        SetText(weaponListRoot.GetChild(index, 4), (1 + arch.weaponSelect[type]) + "/" + count);
        meetSave = true;
    }

    /// <summary>
    /// 鼠标进入武器列表
    /// </summary>
    private void EnterSelectWeapon(int index)
    {
        if (nowSelectWeapon >= 0)
        {
            SetActive(weaponListRoot.GetChild(nowSelectWeapon, 3), false);
        }
        nowSelectWeapon = index;
        if (index < 0) return;

        SetActive(weaponListRoot.GetChild(index, 3), true);
        
        int count = data.weapons[(WeaponTypeEnum)index].Count;

        SetActive(weaponListRoot.GetChild(index, 3, 0), count > 1);
        SetActive(weaponListRoot.GetChild(index, 3, 1), count > 1);

    }
    /// <summary>
    /// 鼠标离开武器列表
    /// </summary>
    private void ExitSelectWeapon(int index)
    {
        if (index >= 0)
        {
            SetActive(weaponListRoot.GetChild(index, 3), false);
            SetActive(weaponListRoot.GetChild(index, 3, 0), false);
            SetActive(weaponListRoot.GetChild(index, 3, 1), false);
        }

    }

    private void SetWeaponPreView(int index, WeaponPlayerController weapon)
    {
        SetText(weaponListRoot.GetChild(index, 0), weapon.WeaponName);
        SetSprite(weaponListRoot.GetChild(index, 2), weapon.WeaponIcon);

        var lenghts = weapon.UpgradeCount();
        Debug.LogError("ID"+ arch.ID + "_" + weapon.WeaponName + "升级数量" + lenghts.Length);
        var weaponData = ArchiveSvc.Archive.weaponUpgradeDic.TryGet(arch.ID + "_" + weapon.WeaponName, new(arch.ID + "_" + weapon.WeaponName, lenghts.Length));
        for (int x = 0; x < lenghts.Length; ++x)
        {
            if (weaponData.selectIndex[x] >= 0)
            {
                SetSprite(weaponListRoot.GetChild(index, 5, x,0), weapon.GetUpgrade(x, weaponData.selectIndex[x]).icon);
                SetActive(weaponListRoot.GetChild(index, 5, x,0),true);
            }
            else
            {
                SetActive(weaponListRoot.GetChild(index, 5, x,0),false);
            }
        }
    }

    #endregion

    private void Refresh(GameObject go, RoleData_SO data,ArchivesData_SO.ArchRoleData arch, bool isNow)
    {
        m_showModleGo = go.transform;
        this.data = data;
        this.arch = arch;
        string stuName = m_showModleGo.GetComponent<BaseObject>().ShowName;
        SetText(txt_Name, stuName);
        SetText(txt_Switch,"切换"+ stuName);
        ArchiveSvc.Archive.GetRoleLevel(data.ID,out int level,out float exp);
        SetText(txt_Level,level);
        SetActive(btn_Select, !isNow);
        var list = data.GetStartingWeapons(arch);

        for(int i = 0; i < 6; ++i)
        {
            var type = (WeaponTypeEnum)i;
            int index = arch.weaponSelect[type], count = data.weapons[type].Count;

            SetWeaponPreView(i, data.weapons[type][index]);
            SetActive(weaponListRoot.GetChild(i, 4), count > 1);
            SetText(weaponListRoot.GetChild(i, 4), (1 + index) + "/" + count);

            var lenghts = data.weapons[type][index].UpgradeCount();
            for (int x = 0; x < weaponListRoot.GetChild(i, 5).childCount; ++x)
            {
                SetActive(weaponListRoot.GetChild(i, 5, x), x < lenghts.Length);
            }
        }

    }

    #region 武器界面

    private string showParameterType, showParameterValue;
    ArchivesData_SO.WeaponUpgradeData archWeaponData;
    private void ShowWeaponWnd()
    {
        var type = (WeaponTypeEnum)nowSelectWeapon;
        var weaponTemp = data.weapons[type][arch.weaponSelect[type]];
        var weaponInst= showWeapon = Instantiate(weaponTemp, m_SelectWeaponCamera.transform.GetChild(0));
        if (weaponInst.ShowRoot)
        {
            weaponInst.transform.localEulerAngles = weaponInst.ShowRoot.transform.localEulerAngles;
            weaponInst.transform.localPosition = -weaponInst.ShowRoot.transform.localPosition;
            weaponInst.transform.localScale = weaponInst.ShowRoot.transform.localScale;
        }
        if(weaponInst.WeaponMuzzle!= weaponInst.WeaponRoot.transform) SetActive(weaponInst.WeaponMuzzle,false);
        SetText(weaponName, weaponInst.WeaponName);
        SetText(weaponType, weaponInst.WeaponType);


        int count = data.weapons[type].Count;
        for (int i=0;i<weaponitemListLayout.childCount;++i)
        {
            if (SetActive(weaponitemListLayout.GetChild(i), i < count))
            {
                SetSprite(weaponitemListLayout.GetChild(i,0), data.weapons[type][i].WeaponIcon);
            }
        }


        SetActive(tipRoot, false);
        var lenghts= weaponInst.UpgradeCount();
        var levels = weaponInst.UpgradeLevel();
        archWeaponData = ArchiveSvc.Archive.weaponUpgradeDic.TryGet(arch.ID+"_"+ weaponInst.WeaponName, new(arch.ID + "_" + weaponInst.WeaponName, lenghts.Length));
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
                SetActive(weaponUpgradeItemLayout.GetChild(y),true);
                SetFill(weaponUpgradeItemLayout.GetChild(y), Mathf.Clamp01(0.5f * (lenghts[y] - 1)));
            }
            else
            {
                SetActive(weaponUpgradeItemLayout.GetChild(y),false);
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


        SetParameter();
        SetText(weaponDescText, weaponInst.desc);
        if (GetActive(showParameterButton)) weaponParameterRoot.GetComponent<Animator>().Play("Exit", 0, 1);
        if (GetActive(showDescButton)) weaponDescRoot.GetComponent<Animator>().Play("Exit", 0, 1);

        RefreshLayout(weaponDescText);

        RefreshLayout(weaponUpgradeItemLayout);

        SwitchModule();


    }


    /// <summary>
    /// 鼠标进入升级框
    /// </summary>
    private void ShowTip(int y,int x)
    {
        SetActive(tipRoot, true);
        tipRoot.position = new(tipRoot.position.x, Input.mousePosition.y-50);
        var data = showWeapon.GetUpgrade(y,x);
        SetText(tipName, data.name);
        SetText(tipType, data.type);
        SetText(tipDesc, data.desc);
        SetSprite(tipIcon,data.icon);
        var select = archWeaponData.selectIndex[y];

        //如果没选择就是new，否则正常使用
        var oldData = select != -1 && select !=x? showWeapon.GetUpgrade(y, select).modifys : new();
        //如果进入的不是选择的，那就使用
        var newData = select != x ? data.modifys : new();
        showWeapon.TryUpgrade(oldData, newData);
        SetParameter();
        
    }
    /// <summary>
    /// 鼠标离开升级框
    /// </summary>
    private void HideTip(int y, int x)
    {
        SetActive(tipRoot, false);
        var select = archWeaponData.selectIndex[y];

        var oldData = showWeapon.GetUpgrade(y, x).modifys;
        var newData = select != -1 ? showWeapon.GetUpgrade(y, select).modifys : oldData;
        showWeapon.TryUpgrade(oldData, newData);
        SetParameter();
    }
    /// <summary>
    /// 鼠标在升级框内
    /// </summary>
    private void MoveTip()
    {
        tipRoot.position = new(tipRoot.position.x, Input.mousePosition.y-50);
    }
    /// <summary>
    /// 点击升级
    /// </summary>
    private void SelectUpgrade(int y, int x)
    {
        if (arch.Level < showWeapon.UpgradeLevel(y))
        {
            wndManager.PlaySound(new("UI/UI_Reward2", volume:0.1f));
            return;
        }
        var upgrade = showWeapon.GetUpgrade(y, x);
        if (archWeaponData.GetBuy(y, x))
        {
            SetUpgradeSelectButton(archWeaponData.selectIndex,y, x,true);
            meetSave = true;
        }
        else
        {
            wndManager.CreatTip(new()
            {
                title = upgrade.name,
                desc = upgrade.desc+"\n\n要购买这项升级吗?",
                optA_Click = () =>
                {
                    archWeaponData.SetBuy(y, x);
                    SetUpgradeItemButton(y, x,1);
                    SetBuyCountLayout(-1, archWeaponData.BuyCount);
                    wndManager.PlaySound(new("UI/UI_Reward", volume: 0.25f));
                    wndManager.PlaySoundData(data.SpeechGroup(SpeechTypeEnum.Upgrade).Get());
                    meetSave = true;
                },
                costs= upgrade.cost.ToArray(),
                optA_Text = "确认",
                optB_Text="取消"
            });
        }
        
        SetActive(tipRoot, false);
    }

    /// <summary>
    /// 设置选项按钮 0选择 1购买 2未购买
    /// </summary>
    public void SetUpgradeItemButton(int y,int x,int state)
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

        if ((set&&arr[y] == x)|| x == -1)//重复点击或者本身就是-1都有可能
        {
            if (set) {
                showWeapon.RemoveUpgrade(y,x);
                arr[y] = -1;
                wndManager.PlaySound(new("UI/UI_Ready", volume:0.5f));
            }
            SetActive(weaponUpgradeSelectLayout.GetChild(y, 0), false);
            SetColor(weaponUpgradeSelectLayout.GetChild(y), Color.black);
            if (x != -1) SetUpgradeItemButton(y, x, 1);//将原来选择的位置重置
            else
            {
                SetColor(weaponUpgradeSelectLayout.GetChild(y), arch.Level < showWeapon.UpgradeLevel(y)?unLevelColor:Color.black);
            }
        }
        else //X有效且不相同
        {
            SetActive(weaponUpgradeSelectLayout.GetChild(y, 0), true);
            SetSprite(weaponUpgradeSelectLayout.GetChild(y, 0),GetSprite(weaponUpgradeItemLayout.GetChild(y,x,0)));
            SetColor(weaponUpgradeSelectLayout.GetChild(y), Color.white);

            if (set)
            {

                if (arr[y] != -1) {
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
            SetParameter();
        }
    }
    private void SetBuyCountLayout(int max,int count)
    {

        for (int i = 0; i < weaponUpgradeBuyLayout.childCount; ++i)
        {
            if(max!=-1) SetActive(weaponUpgradeBuyLayout.GetChild(i), i < max);
            SetSprite(weaponUpgradeBuyLayout.GetChild(i), i < count ? buyIcon : unbuyIcon);
        }
    }

    private void SetParameter()
    {
        //showWeapon.ShowText(out showParameterType, out showParameterValue);
        showWeapon.ShowText(out var parameters);
        
        for (int i=0; i<parameters.Count;++i)
        {
            if (weaponParameterLayout.childCount < parameters.Count)
            {
                Instantiate(weaponParameterLayout.GetChild(0), weaponParameterLayout);
            }
            SetActive(weaponParameterLayout.GetChild(i),true);
            if (string.IsNullOrEmpty(parameters[i].Item2))
            {
                SetText(weaponParameterLayout.GetChild(i, 0), string.Format("<color=#{1}>+{0}</color>",parameters[i].Item1, ColorUtility.ToHtmlStringRGB(new(0.6f, 1, 1))));
                SetText(weaponParameterLayout.GetChild(i,1), "");
            }
            else
            {
                SetText(weaponParameterLayout.GetChild(i, 0), parameters[i].Item1);
                SetText(weaponParameterLayout.GetChild(i, 1), parameters[i].Item2);
                SetColor(weaponParameterLayout.GetChild(i, 1), parameters[i].Item3 ? new(0.6f, 1, 1) : new(0.5f, 0.8f, 1));
            }
            SetColor(weaponParameterLayout.GetChild(i), parameters[i].Item4 ? new(0, 0, 0,0.5f) : new(0, 0, 0,0));
        }
        for (int i = parameters.Count; i < weaponParameterLayout.childCount; ++i)
        {
            SetActive(weaponParameterLayout.GetChild(i), false);
        }
        RefreshLayout(weaponParameterLayout);

    }


    #endregion

    #region 模组相关
    private void InitModule()
    {
        SetCilck(moduleFrame, () => {
            bool state = GetActive(moduleOpterLayout);
            SetActive(moduleOpterLayout, !state);
            if (!state) SetAlpha(moduleOpterLayout, 0, 1, 150);
            moduleRoot.GetComponent<ButtonEnterDetector>().enabled = false;
        });

        SetButtonEnter(moduleFrame, (e) => {
            SetActive(moduleRoot, true);
            RefreshLayout(moduleLayout);
            moduleRoot.GetComponent<ButtonEnterDetector>().InEnter = true;
            moduleRoot.GetComponent<ButtonEnterDetector>().enabled = true;
        });

        SetButtonExit(moduleRoot, (e) => {
            SetActive(moduleRoot, false);
        });

        SetButtonExit(moduleOpterLayout, (e) => {
            SetActive(moduleOpterLayout, false);
            SetActive(moduleRoot, false);
        });

        for (int i = 0; i < moduleOpterLayout.childCount; ++i)
        {
            var a = i;
            var root = moduleOpterLayout.GetChild(i);
            SetCilck(root, () => {
                nowSelectModule = a + 1;
                showWeapon.SetModule(nowSelectModule);
                SetActive(moduleOpterLayout, false);

                //showWeapon.TryUpgrade(showWeapon.Modules[a + 1].modifys, showWeapon.Modules[a + 1].modifys);
                //showWeapon.ApplyModule(archWeaponData.selectModuleIndex, nowSelectModule);
                SetParameter();
                archWeaponData.selectModuleIndex = nowSelectModule;
                meetSave = true;
            });

            SetButtonEnter(root, (e) => {
                SetModuleToTrans(showWeapon.Modules[a + 1], moduleFrame, true);
                showWeapon.TryUpgrade(showWeapon.ActiveModule.modifys, showWeapon.Modules[a + 1].modifys);
                SetParameter();

            });

            SetButtonExit(root, (e) => {
                SetModuleToTrans(showWeapon.ActiveModule, moduleFrame, true);
                showWeapon.TryUpgrade(showWeapon.Modules[a + 1].modifys, showWeapon.Modules[a + 1].modifys);
                SetParameter();
            });

        }

    }

    void SwitchModule()
    {
        SetActive(moduleFrame, showWeapon.Modules.Count > 0);
        SetActive(moduleOpterLayout, false);
        SetActive(moduleRoot, false);

        if (showWeapon.Modules.Count == 0) return;
        nowSelectModule = archWeaponData.selectModuleIndex;
        var activeModule = showWeapon.Modules[nowSelectModule];
        SetModuleToTrans(activeModule, moduleFrame,true);




        for (int i = 0; i < moduleOpterLayout.childCount; ++i)
        {
            if (i+1 < showWeapon.Modules.Count)
            {
                SetActive(moduleOpterLayout.GetChild(i), true);
                SetModuleToTrans(showWeapon.Modules[i+1], moduleOpterLayout.GetChild(i),false);
            }
            else
            {
                SetActive(moduleOpterLayout.GetChild(i), false);
            }
        }
        

    }

    void SetModuleToTrans(WeaponModuleData_SO data,Transform transform,bool andName)
    {
        SetSprite(transform, data.frame);
        SetSprite(transform.GetChild(0), data.icon);
        SetColor(transform, data.color);
        if (andName)
        {
            SetText(moduleName, data.name);
            SetText(moduleType, data.typeName);
            SetColor(moduleTitle, data.color);

            for (int i = 0; i < moduleLayout.childCount; ++i)
            {
                if (i < data.desc.Count)
                {
                    SetActive(moduleLayout.GetChild(i), true);
                    SetColor(moduleLayout.GetChild(i, 0), data.desc[i].Key ? Color.green : Color.red);
                    moduleLayout.GetChild(i, 0).eulerAngles = new(0, 0, data.desc[i].Key ? 180 : 0);
                    SetText(moduleLayout.GetChild(i, 1), data.desc[i].Value);
                    //RefreshLayout(moduleLayout.GetChild(i, 1));
                    //RefreshLayout(moduleLayout.GetChild(i));
                }
                else
                {
                    SetActive(moduleLayout.GetChild(i), false);
                }

            }
            RefreshLayout(moduleLayout);
        }

    }



    #endregion

    private enum RoleWndState
    {
        Role,
        Switch,
        Weapon,
    }
}
