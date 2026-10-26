using System.Collections.Generic;
using FPSGame.Attributes;
using FPSGame.GameContract;
using PEMaths;

using UnityEngine;

namespace FPSGame.Weapon
{
    using Attr = WeaponAttrType;
    /// <summary>
    /// 临时武器（战场拾取等）的控制器。
    /// </summary>
    [AddComponentMenu("武器/临时武器")]
    public class WeaponTemporaryController : WeaponController, IVfxEffect
    {

        /// <summary>非权威端只打表现弹。
        /// <para>▍为什么：本类会被**每个端各自本地生成**（空投 <c>creatObect</c> / 命中特效 <c>ImpactVfx</c>），
        /// 两端都开真弹 ⇒ 伤害按端数翻倍（见 <c>topics/net-dup-damage.md</c>）。
        /// 权威端（房主 / 单机）照旧真开火，伤害由它结算后广播。</para>
        /// <para>⚠ 门用 <c>IsTickDamageAuthority</c> 是现成的"房主/单机"口径；房主掉线后
        /// <c>EnemyNetBridge.OnHostLost</c> 会把它置回 true ⇒ 剩余端自动接管真开火。</para></summary>
        protected override bool IsShotVisualOnly => !FPSGame.Data.BattleState.IsTickDamageAuthority;

        protected virtual void OnEnable()
        {
            if(m_Initialized)
            {
                Ammo.CurrValue = Ammo.FinalValue;
                Magazine.CurrValue = Magazine.FinalValue;
            }
        }

        public override void LogicInit()
        {
            base.LogicInit();
            Owner = gameObject;
        }

        public override void LogicTick()
        {
            base.LogicTick();
            // 临时武器无外部输入：每帧模拟"按住扳机"，由 ShootType 决定实际行为(全自动/激光/蓄力/锁定等)
            HandleShootInputs(true, true, false);
        }


        public void SetOrigin(GameObject origin, GameObject weaponRoot, Collider target, Vector3 point)
        {
            Origin = origin;
        }



        
    }

   
}
