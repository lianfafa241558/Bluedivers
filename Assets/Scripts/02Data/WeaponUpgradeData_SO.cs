using System.Collections;
using System.Collections.Generic;

using UnityEngine;

namespace Unity.FPS.Game
{
    [CreateAssetMenu(fileName = "WUD_", menuName = "Data/武器升级")]
    public class WeaponUpgradeData_SO : ScriptableObject
    {

        public new string name;
        public string type;
        public Sprite icon;
        [TextArea(3, 5)]
        public string desc;

        [Header("费用")]
        public List<KVP<OOPartEnum, int>> cost;

        [Header("修改属性")]
        public List<ModifyAttrData> modifys;
    }
}