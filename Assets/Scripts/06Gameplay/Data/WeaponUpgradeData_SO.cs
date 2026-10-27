using System.Collections;
using System.Collections.Generic;
using FPSGame.Attributes;
using UnityEngine;
using FPSGame.Core;
using FPSGame.GameContract;
using FPSGame.Game;
using FPSGame.Weapon;


namespace FPSGame.GameData
{
    [CreateAssetMenu(fileName = "WUD_", menuName = "Data/武器升级")]
    public class WeaponUpgradeData_SO : ScriptableObject
    {

        [InspectorName("显示名称")]
        public new string name;

        [InspectorName("分类")]
        public string type;

        [InspectorName("图标")]
        [SpritePreview(3,3)]
        public Sprite icon;

        [InspectorName("描述")]
        [TextArea(3, 5)]
        public string desc;

        // 分节标题由 WeaponUpgradeData_SOEditor 的 SectionTitle 负责，故不再挂 [Header]（会重复）
        [InspectorName("费用")]
        public List<SKVP<OOPartEnum, int>> cost = new() {new(OOPartEnum.Pyroxene,1000) };

        [InspectorName("修改属性")]
        public List<ModifyAttrData> modifys;
    }
}