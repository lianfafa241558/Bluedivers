using FPSGame.Core;
using FPSGame.GameContract;
using FPSGame.GameData;
using FPSGame.Utils;
using FPSGame.Weapon;
using UnityEngine;

namespace FPSGame.Gameplay
{
    /// <summary>
    /// 手雷回收物（枕头砸地后留下的那份）：只有投掷者能捡，捡起后给投掷槽补 1 发。
    /// <para>▍它同时是 <see cref="IVfxEffect"/>：由 <c>PlayImpactFx</c> 在碰撞点创建后回传"来源 + 命中碰撞体"，
    /// 这里据此记下创建者；砸在单位身上（不是地面）就直接消失，不留可捡物。</para>
    /// <para>▍不调 <c>base.Operate()</c>：那是任务/阶段推进用的家具事件，捡手雷不需要进事件流。</para>
    /// </summary>
    [AddComponentMenu("交互/手雷回收")]
    public class Furniture_GrenadePickup : Furniture_Attached, IVfxEffect
    {
        [InspectorName("只允许投掷者拾取")]
        [SerializeField]
        private bool CreatorOnly = true;

        [InspectorName("回收数量")]
        [SerializeField]
        private int RefundCount = 1;

        /// <summary>投掷者（= 伤害起源，也是本机玩家）</summary>
        private GameObject m_Creator;

        public override string ShowName => "手雷";

        public override string Id => "GrenadePickup";

        protected override Sprite Icon => null;

        public override string Desc => "回收 +" + RefundCount;

        public void SetOrigin(GameObject owner, GameObject weaponRoot, Collider target, Vector3 point)
        {
            // 砸在单位/其他物体上不留回收物（只有真落空、砸到地面才留）
            if (target != null && !(target is TerrainCollider))
            {
                Tool.Destroy(gameObject);
                return;
            }

            m_Creator = owner;
            canOperate = true;
            inOperate = false;
            owner = null;
        }

        public override bool CanOperate(GameObject unit)
        {
            if (!base.CanOperate(unit)) return false;
            if (unit == null) return false;
            if (CreatorOnly && m_Creator != null && unit != m_Creator) return false;
            return ResolveGrenade(unit) != null;
        }

        public override void Operate()
        {
            var weapon = ResolveGrenade(owner);
            if (weapon != null) weapon.Ammo.CurrValue += RefundCount;

            Tool.Destroy(gameObject);
        }

        /// <summary>取交互者的投掷槽武器（没有该槽就不可交互）</summary>
        private static WeaponPlayerController ResolveGrenade(GameObject unit)
        {
            if (unit == null || !unit.TryGetComponent(out PlayerWeaponsManager weapons)) return null;
            return weapons.GetWeaponAtSlotIndex(PlayerWeaponsManager.SlotOf(WeaponTypeEnum.Grenade));
        }
    }
}
