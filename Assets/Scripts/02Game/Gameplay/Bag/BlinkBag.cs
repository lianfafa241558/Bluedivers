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
    #endregion
    AudioSource AudioSource;
    private void Awake()
    {
        AudioSource = GetComponent<AudioSource>();
    }


    protected override void Update()
    {
        base.Update();//恢复充电总是执行

        if (!Owner.IsValid()) return;

        //按下使用背包键(UseBag)即闪现，未被跳跃占用时
        if (m_InputHandler.GetUseBagDown() && CurrentFillRatio > cost && !m_InputHandler.useJump)
        {
            Blink();
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
    }
}
