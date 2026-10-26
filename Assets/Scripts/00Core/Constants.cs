using PEMaths;
using UnityEngine;

namespace FPSGame.Core
{
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

        // Console 报 `Parameter 'Hash 1234567' does not exist.` 时，拿这个十进制值到下面反查是哪个参数：
        // 例 1080829965 = Attack（2026-10-20 实测；Unity 的 StringToHash 是固定算法，值不会随版本变）。
        // ⚠ 单位是**十进制有符号**，Console 里可能显示正数、也可能显示负数，两边对照时注意符号。

        /// <summary>Animator 参数 <c>Reset</c>（hash = -1856204727）</summary>
        public static int k_AnimResetParameter { get; } = Animator.StringToHash("Reset");
        /// <summary>Animator 参数 <c>Chatget</c>（hash = 1314759673）</summary>
        public static int k_AnimChatgetParameter { get; } = Animator.StringToHash("Chatget");
        /// <summary>Animator 参数 <c>ChatgetSpeed</c>（hash = -561804707）</summary>
        public static int k_AnimChatgetSpeedParameter { get; } = Animator.StringToHash("ChatgetSpeed");
        /// <summary>Animator 参数 <c>MoveSpeed</c>（hash = 526065662）</summary>
        public static int k_AnimMoveSpeedParameter { get; } = Animator.StringToHash("MoveSpeed");
        /// <summary>Animator 参数 <c>Attack</c>（hash = 1080829965）</summary>
        public static int k_AnimAttackParameter { get; } = Animator.StringToHash("Attack");
        /// <summary>Animator 参数 <c>SpecialAttack1</c>（hash = -938659887）</summary>
        public static int k_AnimSpecialAttack1Parameter { get; } = Animator.StringToHash("SpecialAttack1");
        /// <summary>Animator 参数 <c>SpecialAttack2</c>（hash = 1359249003）</summary>
        public static int k_AnimSpecialAttack2Parameter { get; } = Animator.StringToHash("SpecialAttack2");
        /// <summary>Animator 参数 <c>SpecialAttack3</c>（hash = 637751037）</summary>
        public static int k_AnimSpecialAttack3Parameter { get; } = Animator.StringToHash("SpecialAttack3");
        /// <summary>Animator 参数 <c>Alerted</c>（hash = -1854995305）</summary>
        public static int k_AnimAlertedParameter { get; } = Animator.StringToHash("Alerted");
        /// <summary>Animator 参数 <c>OnDamaged</c>（hash = -1403378696）</summary>
        public static int k_AnimOnDamagedParameter { get; } = Animator.StringToHash("OnDamaged");
        /// <summary>Animator 参数 <c>OnDeath</c>（hash = -42616333）</summary>
        public static int k_AnimOnDeathParameter { get; } = Animator.StringToHash("OnDeath");
        /// <summary>Animator 参数 <c>OnReload</c>（hash = 1522553424）</summary>
        public static int k_AnimOnReloadParameter { get; } = Animator.StringToHash("OnReload");
        /// <summary>Animator 参数 <c>IsActive</c>（hash = 1929417870）</summary>
        public static int k_AnimIsActiveParameter { get; } = Animator.StringToHash("IsActive");

        /// <summary>Animator 参数 <c>Entry</c>（hash = -354373004）</summary>
        public static int k_AnimEntry { get; } = Animator.StringToHash("Entry");

        /// <summary>
        /// 战斗场景名
        /// </summary>
        public const string BattleSceneName = "BattleScene";

        /// <summary>飞鹰重新装填ID</summary>
        public const int EagleReloadId = 1;
        /// <summary>呼叫增援ID</summary>
        public const int HealBag = 9;
        /// <summary>补给ID</summary>
        public const int SupplyId = 10;
        /// <summary>SOSID</summary>
        public const int SOSId = 11;

        /// <summary>探照灯ID</summary>
        public const int LampTowerId = 16;
        /// <summary>轨道照明弹ID</summary>
        public const int IlluminatorId = 17;

        /// <summary>火炮阵地ID</summary>
        public const int ArtilleryId = 18;
        /// <summary>极速射ID</summary>
        public const int PlayerArtilleryAId = 19;
        /// <summary>实验性弹头ID</summary>
        public const int PlayerArtilleryBId = 20;
    }
}
