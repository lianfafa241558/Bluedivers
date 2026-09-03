using PEMaths;
using UnityEngine;

public static class Constants
{
    public const int CanvasWidth = 1920;
    public const int CanvasHeight = 1080;

    public const int LoginFrameMs = 20;
    public static PEInt LoginFrame { get; } = new(0.02f);

    public const float MinTemp = 16;
    public const float MaxTemp = 24;
    public const int MaxPlayer = 4;

    /// <summary>死亡高度</summary>
    public const int KillHeight = 0;
    /// <summary>
    /// 默认地图边缘(直径)
    /// </summary>
    public const int MapDefaultBorder = 128;

    /// <summary>
    /// 本次任务地图边缘(半径)
    /// </summary>
    public static int TaskBorder = 16;

    /// <summary>
    /// 一天的时间:午夜0点，清晨4点，上午8点，正午12点，黄昏18点，午夜20点
    /// </summary>
    public static float[] DayStageTime = new float[] { 0f, 0.167f, 0.333f, 0.542f, 0.708f, 0.792f, 1 };
    /// <summary>
    /// 一天总时长（秒），默认800秒30分钟
    /// </summary>
    public const float FullDayDuration = 1800f;

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

    /// <summary>飞鹰重新装填ID</summary>
    public static int EagleReloadId = 1;
    /// <summary>呼叫增援ID</summary>
    public static int HealBag = 9;
    /// <summary>补给ID</summary>
    public static int SupplyId = 10;
    /// <summary>SOSID</summary>
    public static int SOSId = 11;

    /// <summary>探照灯ID</summary>
    public static int LampTowerId = 16;
    /// <summary>轨道照明弹ID</summary>
    public static int IlluminatorId = 17;

    /// <summary>火炮阵地ID</summary>
    public static int ArtilleryId = 18;
    /// <summary>极速射ID</summary>
    public static int PlayerArtilleryAId = 19;
    /// <summary>实验性弹头ID</summary>
    public static int PlayerArtilleryBId = 20;
}
