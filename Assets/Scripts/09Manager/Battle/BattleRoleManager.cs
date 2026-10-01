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
        return GameObject.FindGameObjectWithTag("Medivac").transform.TransformPoint(0,-4,6);
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
