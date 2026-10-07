using System.Collections;
using System.Collections.Generic;
using FPSGame.Game;
using UnityEngine;
using UnityEngine.Events;
using FPSGame.Gameplay;
using FPSGame.GameData;
using FPSGame.Weapon;

namespace FPSGame.Managers
{

public abstract class RoleManagerBase : MonoBehaviour
{

    [Header("角色数据列表")]
    public List<RoleData_SO> dataList;

    protected GameObject PlayerPrefab;
    protected PlayerController m_player;
    [SerializeField]
    protected int m_nowSelectIndex;
    protected ResSvc resManager;
    protected WeaponPlayerController EmptyWeapon;

    protected virtual void Start()
    {
        resManager = ResSvc.Instance;
        PlayerPrefab = resManager.LoadRes<GameObject>("Prefabs/BattleBase/Player");
        dataList = resManager.LoadObjects<RoleData_SO>("GameData/Role");

        m_nowSelectIndex = dataList.FindIndex(item => item.ID == ArchivesData_SO.Current.lastSelectRole);

        var player = Instantiate(PlayerPrefab, GetStartPoint(), default,null);
        m_player = player.GetComponent<PlayerController>();
        m_player.Init(TeamManager.Instance.SelfIndex);
        EmptyWeapon = resManager.LoadRes<GameObject>("Weapons/WeaponEmpty").GetComponent<WeaponPlayerController>();

    }

    public virtual void SetPlayerRole(PlayerController player)
    {
        GlobalEventSub.OnSwitchRole?.Invoke(player);
    }

    public abstract Vector3 GetStartPoint();
}
}
