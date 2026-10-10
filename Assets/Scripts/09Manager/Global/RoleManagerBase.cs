using System.Collections;
using System.Collections.Generic;
using FPSGame.Game;
using FPSGame.Net;
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

    /// <summary>出生点/集合点的**序号间距**（米）。以中心点对称沿 **+X** 铺开（口径见 <see cref="SpawnSlots"/>）：
    /// 2 人 ⇒ 0 号 -0.25 / 1 号 +0.25；3 人 ⇒ -0.5 / 0 / +0.5（本字段 = 0.5）。
    /// <para>大厅开工点、准备点与战斗出生点（<c>BattleRoleManager</c>）共用这一个字段。</para></summary>
    [SerializeField]
    [InspectorName("出生点序号间距(米)")]
    protected float spawnSpacing = 0.5f;

    /// <summary>
    /// 本机玩家在**房主视角名单**里的序号（房主 = 0；成员 = 自己在房主名单里的下标）。
    /// <para>▍⚠ 不能用 <c>TeamManager.SelfIndex</c>：本地名单把"自己"固定放在 <c>players[0]</c>（自己视角，
    /// 见 <c>TeamNetBridge.HandlePlayerList</c>）⇒ 它**恒为 0**，谁都错不开位。
    /// 实现在 <see cref="SpawnSlots.LocalOrdinal"/>（读 <c>NetRoomFlow.SelfHostIndex</c>）。</para>
    /// <para>单机 / 名单未知 ⇒ 0（与原行为一致）。</para>
    /// </summary>
    protected static int SelfOrdinal() => SpawnSlots.LocalOrdinal;

    /// <summary>把"一个点"摊成"本机玩家该站的那一位"（以中心对称沿 +X 排开，见 <see cref="SpawnSlots"/>）。
    /// <para>⚠ 各端只用它挪**自己**那台；其余玩家靠位姿同步跟过来 ⇒ 不需要额外同步出生点。</para></summary>
    protected Vector3 Spread(Vector3 center) => SpawnSlots.Spread(center, SelfOrdinal(), spawnSpacing);

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
        GlobalEventBus.OnSwitchRole?.Invoke(player);
    }

    public abstract Vector3 GetStartPoint();
}
}
