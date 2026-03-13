using System.Collections;
using System.Collections.Generic;
using PEMaths;
using UnityEngine;

public static class Constants
{
    public const int LoginFrameMs = 50;
    public static PEInt LoginFrame { get; } = new(0.05f);

    public const float MinTemp = 16;
    public const float MaxTemp = 24;
    public const int MaxPlayer = 4;

    /// <summary>死亡高度</summary>
    public const int KillHeight = -50;
    /// <summary>
    /// 地图边缘(直径)
    /// </summary>
    public const int MapBorder = 32;

    public static int k_AnimResetParameter { get; } = Animator.StringToHash("Reset");
    public static int k_AnimChatgetParameter { get; } = Animator.StringToHash("Chatget");
    public static int k_AnimChatgetSpeedParameter { get; } = Animator.StringToHash("ChatgetSpeed");
    public static int k_AnimMoveSpeedParameter { get; } = Animator.StringToHash("MoveSpeed");
    public static int k_AnimAttackParameter { get; } = Animator.StringToHash("Attack");
    public static int k_AnimSpecialAttack1Parameter { get; } = Animator.StringToHash("SpecialAttack1");
    public static int k_AnimSpecialAttack2Parameter { get; } = Animator.StringToHash("SpecialAttack2");
    public static int k_AnimSpecialAttack3Parameter { get; } = Animator.StringToHash("SpecialAttack3");
    public static int k_AnimAlertedParameter { get; } = Animator.StringToHash("Alerted");
    public static int k_AnimOnDamagedParameter { get; } = Animator.StringToHash("OnDamaged");
    public static int k_AnimOnDeathParameter { get; } = Animator.StringToHash( "OnDeath");
    public static int k_AnimOnReloadParameter { get; } = Animator.StringToHash("OnReload");
    public static int k_AnimIsActiveParameter { get; } = Animator.StringToHash("IsActive");

    public static int k_AnimEntry { get; } = Animator.StringToHash("Entry");


}
