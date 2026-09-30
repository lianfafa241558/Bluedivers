using System.Collections;
using System.Collections.Generic;
using FPSGame.Game;
using UnityEngine;
using FPSGame.Utils;
using FPSGame.Gameplay;
using FPSGame.GameContract;

namespace FPSGame.Furn
{

    /// <summary>
    /// 提供补给的箱子。
    /// </summary>
    [AddComponentMenu("交互/补给箱")]
    public class Furniture_Supplies : Furniture_Base
    {
        public override string Desc => "采集[" + ShowName + "]";


        public override void Operate()
        {
            base.Operate();
            var type = Tool.StringToEnum<OOPartEnum>(Id);
            

            Tool.Destroy(gameObject);
        }

    }
}
