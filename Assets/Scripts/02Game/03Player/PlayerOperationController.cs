using System.Collections;
using System.Collections.Generic;
using Core;
using UnityEngine;

public class PlayerOperationController : MonoBehaviour
{

    public Furniture_Base target;

    private Camera m_Camera;

    private PlayerInputHandler m_InputHandler;
    private AudioSource aud;

    void Start()
    {
        m_Camera = GetComponentInChildren<Camera>();
        m_InputHandler = GetComponent<PlayerInputHandler>();
        GameRoot.OnWindowStateChange += OnWindowStateChange;
    }
    private void OnDestroy()
    {
        GameRoot.OnWindowStateChange -= OnWindowStateChange;
    }

    void Update()
    {
        

        if (Physics.Raycast(m_Camera.ScreenPointToRay(new(Screen.width / 2, Screen.height / 2, 0)), out var hit, 1.3f, m_Camera.cullingMask))
        {
            var newtar = hit.transform.GetComponent<Furniture_Base>();
            if(target != newtar&&(newtar==null|| newtar.CanOperate(gameObject)))
            {
                if (target && !target.HaveFlag(FurnitureFlag.KeepPress)) target.Press = 0;
                target = newtar;
                if (!newtar)
                {
                    if (aud) { aud.Stop(); aud = null; }
                    m_InputHandler.InOperation = false;
                }
            }
        }
        //没有获取到交互道具，但是正在操作
        else if(target&&(!target.HaveFlag(FurnitureFlag.SwitchState)||!target.inOperate))
        {
            if (!target.HaveFlag(FurnitureFlag.KeepPress)) target.Press = 0;
            if (aud) { aud.Stop(); aud = null; }
            target = null;
            m_InputHandler.InOperation = false;
        }
        if (target)
        {
            if (target.meetTime == 0)
            {
                if (m_InputHandler.GetOperateDown())
                {
                    target.Handle(gameObject);
                    if (!target || !target.CanOperate(gameObject))
                    {
                        if (aud) { aud.Stop(); aud = null; }
                        target = null;
                    }
                }
            }
            else
            {
                if (m_InputHandler.GetOperateDown())
                {
                    if (target.audioPress) aud = AudioManager.PlaySound(new(target.audioPress, target.transform.position, 20, AudioGroups.General) { loop=true});
                }
                else if (m_InputHandler.GetOperateHeld())//完成操作
                {
                    if ((target.Press += Time.deltaTime) >= target.meetTime)
                    {
                        target.Handle(gameObject);
                        if (aud) { aud.Stop(); aud = null; }
                        if (!target.HaveFlag(FurnitureFlag.KeepPress)) target.Press = 0;
                        if (!target || !target.CanOperate(gameObject))
                        {
                            target = null;
                        }
                    }
                }
                else if (m_InputHandler.GetOperateUp())//取消操作
                {
                    if (!target.HaveFlag(FurnitureFlag.KeepPress)) target.Press = 0;
                    if (aud) { aud.Stop(); aud = null; }
                }

            }


        }

    }

    private void OnWindowStateChange(WindowStateEnum oldState,WindowStateEnum state)
    {
        switch (state)
        {
            case WindowStateEnum.Game:
                enabled = true;
                break;
            default:
                if (target&&!target.HaveFlag(FurnitureFlag.KeepPress)) target.Press = 0;
                if (aud) { aud.Stop(); aud = null; }
                target = null;
                m_InputHandler.InOperation = false;
                enabled = false;
                break;
        }
    }
}
