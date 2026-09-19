using System;
using UnityEngine;

namespace FPSGame.Furn
{
    /// <summary>
    /// 凯伊(Kei)身上取出的回收信标：玩家靠近交互即请求动态撤离。
    /// <para>该家具挂在凯伊的回收包物体上，默认禁用；由 <see cref="FPSGame.AI.SpecUnitKei"/> 抵达呼叫点后启用。</para>
    /// <para>只有动态撤离任务激活（<see cref="Usable"/>）时才可交互，避免在其它任务里误触发。</para>
    /// </summary>
    public class Furniture_ReturnBag : Furniture_Attached
    {
        /// <summary>家具 Id（区别于其它家具，日志/调试用）</summary>
        public const string IdName = "ReturnBag";

        /// <summary>玩家在回收信标上请求撤离(参数为发起请求的玩家)</summary>
        public event Action<GameObject> OnRequestEvacuate;

        /// <summary>是否允许交互，由动态撤离任务激活时打开</summary>
        public bool Usable { get; set; }

        public override string ShowName => "回收信标";

        public override string Id => IdName;

        public override string Desc => "请求撤离";

        /// <summary>回收包不是 Actor，取不到头像，直接返回空避免空引用</summary>
        protected override Sprite Icon => null;

        public override bool CanOperate(GameObject unit)
        {
            return Usable && base.CanOperate(unit);
        }

        public override void Operate()
        {
            base.Operate();
            if (!owner) return;
            //请求撤离，由订阅方(撤离任务)决定后续流程
            OnRequestEvacuate?.Invoke(owner);
        }
    }
}
