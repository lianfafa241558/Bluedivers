using System.Collections;
using System.Collections.Generic;
using FPSGame.Game;
using UnityEngine;
using FPSGame.Gameplay;

namespace FPSGame.Managers
{

/// <summary>
/// 战斗中角色的出场、换人与出生点设置。
/// </summary>
[AddComponentMenu("管理/战斗角色管理")]
public class BattleRoleManager : RoleManagerBase
{
    protected override void Start()
    {
        base.Start();
        SetPlayerRole(m_player);
    }

    public override Vector3 GetStartPoint()
    {
        // 医疗船旁的固定点；再按**队伍序号沿 +X 铺开**（<see cref="RoleManagerBase.Spread"/>）——
        // 否则两端各自在自己机器上把玩家刷到同一个点，开打瞬间会互相顶开（2026-10-10 用户报）。
        var medivac = GameObject.FindGameObjectWithTag("Medivac");
        Vector3 p = medivac != null ? medivac.transform.TransformPoint(0, -4, 6) : Vector3.zero;
        return Spread(p);
    }

    public override void SetPlayerRole(PlayerController player)
    {
        player.SetBody(Instantiate(resManager.LoadRes<Transform>("Prefabs/StudentModle/" + dataList[m_nowSelectIndex].ID)), dataList[m_nowSelectIndex], new() { EmptyWeapon });
        //player.transform.parent=GameObject.FindGameObjectWithTag("Medivac").transform;
        //player.Controller.enabled = false;
        base.SetPlayerRole(player);
    }


}
}
