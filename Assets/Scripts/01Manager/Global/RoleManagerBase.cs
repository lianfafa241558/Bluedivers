using System.Collections;
using System.Collections.Generic;
using Unity.FPS.Game;
using UnityEngine;
using UnityEngine.Events;

public abstract class RoleManagerBase : MonoBehaviour
{

    [Header("角色数据列表")]
    public List<RoleData_SO> dataList;

    protected GameObject PlayerPrefab;
    protected PlayerController m_player;
    [SerializeField]
    protected int m_nowSelectIndex;
    protected ResManager resManager;
    protected WeaponPlayerController EmptyWeapon;

    protected virtual void Start()
    {
        resManager = ResManager.Instance;
        PlayerPrefab = resManager.LoadRes<GameObject>("Prefabs/Player");
        dataList = resManager.LoadObjects<RoleData_SO>("GameData/Role");

        m_nowSelectIndex = dataList.FindIndex(item => item.ID == GameRoot.Archive.lastSelectRole);

        GameObject startPoint = GameObject.FindGameObjectWithTag("StartPoint");
        //Debug.LogError("初始点"+ (startPoint.transform.position + Vector3.up * 3));
        var player = Instantiate(PlayerPrefab, startPoint.transform.position+Vector3.up*3,default,null);
        m_player = player.GetComponent<PlayerController>();
        m_player.Init(RoomManager.Instance.SelfIndex);
        EmptyWeapon = resManager.LoadRes<GameObject>("Weapons/Empty").GetComponent<WeaponPlayerController>();

    }

    public virtual void SetPlayerRole(PlayerController player)
    {
        GlobalEventManager.OnSwitchRole?.Invoke(player);
    }
    

}
