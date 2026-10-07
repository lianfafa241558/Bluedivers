using System.Collections;
using System.Collections.Generic;
using TMPro;
using FPSGame.Game;
using UnityEngine;
using FPSGame.Utils;
using FPSGame.Rendering;
using FPSGame.Gameplay;
using FPSGame.GameData;

namespace FPSGame.Managers
{

/// <summary>
/// 舰桥（准备阶段）的角色展示、选择与换人。
/// </summary>
[AddComponentMenu("管理/舰桥角色管理")]
public class BridgeRoleManager : RoleManagerBase
{
    [SerializeField]
    private GameObject ReadyPoint;

    [Header("展示模型列表")]
    public List<GameObject> showModleList;


    private int m_nowShowIndex;
    private GameObject selectPointGo;


    protected override void Start()
    {
        base.Start();
        SnowController.SetEnabled(false);

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
#if UNITY_EDITOR
        //仅供试验
        ArchivesData_SO.Current.GainRoleExp(dataList[m_nowShowIndex].ID, Random.Range(5000, 99999), out int level, out float expScale);
#endif

        GameRoot.GameState = FPSGame.Core.GameStateEnum.Bridge;
        WndManager.WindowState = FPSGame.Core.WindowStateEnum.Game;
    }
    private void Update()
    {
        if(Input.GetKeyDown(KeyCode.O))
        {
            if(GameRoot.GameState== FPSGame.Core.GameStateEnum.Bridge)
            {
                WndManager.Instance.SetWndState(FPSGame.GameContract.WndType.SelectMap, true);
            }
            else if (GameRoot.GameState == FPSGame.Core.GameStateEnum.Ready)
            {
                m_player.Controller.enabled = false;
                m_player.transform.position = ReadyPoint.transform.position;
                m_player.Controller.enabled = true;
            }

        }
    }



    /// <summary>本机玩家出生点的**序号间距**（米）。与盟友那边的 <c>NetFriendBridge.spawnSpacing</c> 同一约定
    /// （第 N 个玩家沿 -Z 让开 N×间距），两处都调一致的观感即可。</summary>
    [SerializeField]
    [InspectorName("出生点序号间距(米)")]
    private float spawnSpacing = 1f;

    public override Vector3 GetStartPoint()
    {
        var go = GameObject.FindGameObjectWithTag("StartPoint");
        Vector3 p = go != null ? go.transform.position : Vector3.zero;

        // ⚠ 按**本机玩家的队伍序号**让开：以前所有人都拿同一个点 ⇒ 大厅里几个人完全重叠
        //   （2026-10-07 实测："加入游戏没有根据队伍序号做出生点偏差"）。
        //   各端只偏移**自己**（SelfIndex），位姿同步之后大家自然分开 ⇒ 不需要额外同步出生点。
        //   ⚠ 盟友那侧 <c>NetFriendBridge.NextSpawnPos</c> 的初始偏移只是"出生瞬间不打架"，
        //     它的真实位置由位姿同步覆盖 —— 所以**偏移必须发生在本机自己身上**，否则同步完还是重叠。
        int index = TeamManager.Instance != null ? TeamManager.Instance.SelfIndex : 0;
        return p + Vector3.up * 0.2f + new Vector3(0f, 0f, -index * spawnSpacing);
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
        arch = ArchivesData_SO.Current.GetRoleCfg(data.ID);
        showModleList[m_nowShowIndex].gameObject.SetActive(true);
        selectPointGo.transform.GetChild(0).gameObject.SetActive(false);
        selectPointGo.transform.GetChild(0).gameObject.SetActive(true);
    }

    public void SelectRole()
    {
        m_nowSelectIndex = m_nowShowIndex;
        var newRoleId = dataList[m_nowShowIndex].ID;
        ArchivesData_SO.Current.lastSelectRole = newRoleId;
        selectPointGo.transform.GetChild(0).gameObject.SetActive(false);
        selectPointGo.transform.GetChild(0).gameObject.SetActive(true);

        SetPlayerRole(m_player);

        // 同步更新 teamManager.players 中的角色数据，确保其他窗口（GameEndWnd、ArmamentWnd 等）能获取到正确的角色
        ArchivesData_SO.Current.GetRoleLevel(newRoleId, out int level, out float exp);
        var selfData = TeamManager.Instance.players[TeamManager.Instance.SelfIndex];
        selfData.roleName = newRoleId;
        selfData.roleLevel = level;
        selfData.roleExp = exp;
        selfData.weapons = ArchivesData_SO.Current.GetWeaponSelect(newRoleId);
        selfData.Upgrades = ArchivesData_SO.Current.GetWeaponUpgrade(newRoleId);

        // 联机：把"我换了角色"上报（房主写进 HostProfile 并广播；单机时是空操作）
        TeamNetBridge.SendSelfProfile();
    }
    #endregion
}
}
