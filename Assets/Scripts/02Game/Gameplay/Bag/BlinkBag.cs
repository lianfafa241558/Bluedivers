using System.ComponentModel;
using FPSGame.Gameplay;

using UnityEngine;

public class BlinkBag : BagBase
{

    #region 参数

    [InspectorName("特效的粒子系统")]
    public GameObject[] vfxs;

    [InspectorName("耗电量")]
    public float cost=0.5f;
    //[SerializeField]
    //private float lastStartTime;
    private bool isUse;
    #endregion
    AudioSource AudioSource;
    private void Awake()
    {
        AudioSource = GetComponent<AudioSource>();
        CancelUse();
    }


    protected override void Update()
    {
        base.Update();//恢复充电总是执行

        if (!Owner.IsValid()) return;
        /*
        if (m_InputHandler.GetJumpInputDown())
        {
            StartlUse();
        }*/
        //没有被其他人占用，自己也还没启用
        if (m_InputHandler.GetJumpInputLong(0.3f) && CurrentFillRatio > cost && !m_InputHandler.useJump && !isUse)
        {
            Blink();
        }
        else if(m_InputHandler.GetJumpInputUp(true))
        {
            CancelUse();
        }

    }

    private void Blink()
    {
        foreach (var vfx in vfxs)
        {
            VFXManager.Creat(vfx, m_PlayerCharacterController.CenterPos);
        }
        Vector3 dir = m_InputHandler.GetMoveInput();
        if(dir==default)dir=Vector3.forward;
        m_PlayerCharacterController.Move(m_PlayerCharacterController.PlayerCamera.transform.TransformVector(dir*10),true);
        AudioSource.Play();
        foreach (var vfx in vfxs)
        {
            VFXManager.Creat(vfx, m_PlayerCharacterController.CenterPos);
        }
        CurrentFillRatio -= cost;
        OnStateChange?.Invoke(true);
        isUse = true;
        m_InputHandler.useJump = true;
    }

    private void CancelUse()
    {
        if (isUse)
        {
            isUse = false;
            m_InputHandler.useJump = false;
        }
        //lastStartTime = Mathf.Infinity;

    }
}
