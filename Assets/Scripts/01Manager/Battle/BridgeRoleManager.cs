using System.Collections;
using System.Collections.Generic;
using Unity.FPS.Game;
using UnityEngine;
using Utils;

public class BridgeRoleManager : RoleManagerBase
{

    [Header("展示模型列表")]
    public List<GameObject> showModleList;


    private int m_nowShowIndex;
    private GameObject selectPointGo;


    protected override void Start()
    {
        base.Start();
        
        var modleList = resManager.LoadObjects<GameObject>("Prefabs/StudentModle");
        //Debug.LogWarning("获取到的学生模型长度"+modleList.Count);
        selectPointGo = TransformUtils.SceenFind("ShowStudentPoint");
        if (!selectPointGo)
        {
            Debug.LogError("场景中找不到ShowStudentPoint");
            return;
        }
        dataList.ForEach(item => {

            var show = Instantiate(modleList.Find(modle => modle.name == item.ID));
            showModleList.Add(show);
            if (selectPointGo != null)
            {
                show.transform.SetParent(selectPointGo.transform, false);
                show.transform.localPosition = default;
                show.transform.localRotation = Quaternion.identity;
            }
            show.SetActive(false);
        });

        m_nowShowIndex = m_nowSelectIndex;

        SetPlayerRole(m_player);
        //仅供试验
        ArchiveSvc.Archive.GainRoleExp(dataList[m_nowShowIndex].ID, Random.Range(5000, 99999), out int level, out float expScale);

        GameRoot.GameState = Core.GameStateEnum.Bridge;
        WndManager.WindowState = Core.WindowStateEnum.Game;
    }

    public override Vector3 GetStartPoint()
    {
        return GameObject.FindGameObjectWithTag("StartPoint").transform.position + Vector3.up * 0.2f;
    }

    public override void SetPlayerRole(PlayerController player)
    {
        player.SetBody(Instantiate(resManager.LoadRes<Transform>("Prefabs/StudentModle/" + dataList[m_nowSelectIndex].ID)), dataList[m_nowSelectIndex], new() { EmptyWeapon });
        player.WeaponsManager.SwitchWeapon(false);//切到空武??
        base.SetPlayerRole(player);
    }


    #region 换人界面相关
    public void StartShowRole(out GameObject go, out RoleData_SO data, out ArchivesData_SO.ArchRoleData arch, out bool isNow)
    {
        SetShow(out go, out data, out arch, out isNow);
    }
    public void SwitchShowRole(bool add, out GameObject go, out RoleData_SO data, out ArchivesData_SO.ArchRoleData arch, out bool isNow)
    {
        showModleList[m_nowShowIndex].gameObject.SetActive(false);
        m_nowShowIndex = Tool.PositiveRemainder(m_nowShowIndex + (add ? 1 : -1), dataList.Count);

        SetShow(out go, out data, out arch, out isNow);
    }
    public void RandomShowRole(out GameObject go, out RoleData_SO data, out ArchivesData_SO.ArchRoleData arch, out bool isNow)
    {
        showModleList[m_nowShowIndex].gameObject.SetActive(false);
        var old = m_nowShowIndex;
        while (old == m_nowShowIndex)
        {
            m_nowShowIndex = Random.Range(0, dataList.Count);
        }


        SetShow(out go, out data, out arch, out isNow);
    }

    private void SetShow(out GameObject go, out RoleData_SO data, out ArchivesData_SO.ArchRoleData arch, out bool isNow)
    {
        go = showModleList[m_nowShowIndex];
        data = dataList[m_nowShowIndex];
        isNow = m_nowSelectIndex == m_nowShowIndex;
        arch = ArchiveSvc.Archive.GetRoleCfg(data.ID);
        showModleList[m_nowShowIndex].gameObject.SetActive(true);
        selectPointGo.transform.GetChild(0).gameObject.SetActive(false);
        selectPointGo.transform.GetChild(0).gameObject.SetActive(true);
    }

    public void SelectRole()
    {
        m_nowSelectIndex = m_nowShowIndex;
        var newRoleId = dataList[m_nowShowIndex].ID;
        ArchiveSvc.Archive.lastSelectRole = newRoleId;
        selectPointGo.transform.GetChild(0).gameObject.SetActive(false);
        selectPointGo.transform.GetChild(0).gameObject.SetActive(true);

        SetPlayerRole(m_player);

        // 同步更新 roomManager.players 中的角色数据，确保其他窗口（GameEndWnd、ArmamentWnd 等）能获取到正确的角色
        ArchiveSvc.Archive.GetRoleLevel(newRoleId, out int level, out float exp);
        var selfData = RoomManager.Instance.players[RoomManager.Instance.SelfIndex];
        selfData.roleName = newRoleId;
        selfData.roleLevel = level;
        selfData.roleExp = exp;
        selfData.weapons = ArchiveSvc.Archive.GetWeaponSelect(newRoleId);
        selfData.Upgrades = ArchiveSvc.Archive.GetWeaponUpgrade(newRoleId);
    }
    #endregion
}
