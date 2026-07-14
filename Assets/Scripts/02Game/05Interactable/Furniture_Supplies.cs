using System.Collections;
using System.Collections.Generic;
using Unity.FPS.Game;
using UnityEngine;
using Utils;

namespace FPSGame.Furn
{

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