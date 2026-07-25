using System.Collections;
using System.Collections.Generic;

using Unity.FPS.Game;
using Unity.FPS.Gameplay;
using UnityEngine;
using UnityEngine.UI;
using Utils;

public class SimpleAimCrosshairManager : CrosshairManagerBase
{


    private void OnDisable()
    {
        //if(!m_Weapons) SwitchWeapon(GetComponentInParent<WeaponPlayerController>(), false);
        //只能在自己被隐藏的时候吧自己扔出去，但是要怎么获得到父级被重新显示了呢？大概只能原地创建另一个组件，来置换了
        Tool.Exchange(transform);
    }
    /*
    protected override void Start()
    {
        Debug.LogError(gameObject+"开始初始化",this);
        base.Start();
        SwitchWeapon(GetComponentInParent<WeaponPlayerController>(), false);
    }*/
    private void OnEnable()
    {
        if (!m_Weapons) SwitchWeapon(GetComponentInParent<WeaponPlayerController>(), false);
        else SetAnimGo();
    }

    protected override void SetAnimGo()
    {
        m_ActiveSightGo = GetComponent<Animator>();
        m_ActiveSightGo.SetFloat(Constants.k_AnimChatgetSpeedParameter, 1 / Mathf.Max(m_Weapons.AttrFinal(WeaponAttrType.ChargeDuration).RawFloat, 0.1f));
    }

}
