using System.Collections;
using System.Collections.Generic;
using FPSGame.Attributes;
using UnityEngine;
using FPSGame.Core;
using FPSGame.Game;
using FPSGame.Weapon;


namespace FPSGame.GameData
{

    [CreateAssetMenu(fileName = "WMD_", menuName = "Data/武器模组")]
    public class WeaponModuleData_SO : ScriptableObject
    {
        public new string name;
        [InspectorName("模组类型")]
        public ModuleType type;
        [SpritePreview(3,3)]
        public Sprite icon;

        public List<SKVP<bool, string>> desc;

        [InspectorName("修改属性")]
        public List<ModifyAttrData> modifys;

        // ⚠ `frame`（模组边框图标）已于 2026-10-01 **移除**：它原先在本类（数据层）里直接调
        //   `ServiceLocator.Res.LoadSprite(...)` 并返回 `Sprite` ⇒ 双重违规：
        //   ① 数据层调用上层服务；② 表现概念（Sprite）进入数据层。
        //   现在由**表现侧按 type 查表加载**：见 `04UI/SelectRoleWnd.LoadModuleFrame(data)`。
        //   （`icon`/`typeName`/`color` 留在这里是合理的：前两者是本 SO 自带的序列化资产/纯字符串，
        //    `color` 是纯值；真正越界的是"向服务要资源"。）

        public string typeName => type switch {
            ModuleType.Clean => "无暇改装模组",
            ModuleType.Balanced => "均衡改装模组",
            ModuleType.Unstable => "危险改装模组",
            _=> "",
        };
        public Color color => type switch {
            ModuleType.Clean => new(0.26f, 0.61f, 0.37f),
            ModuleType.Balanced => new(0.88f,0.78f,0.24f),
            ModuleType.Unstable => new(0.78f, 0.21f, 0f),
            _ => Color.gray,
        };

        public enum ModuleType
        {
            [InspectorName("无")]
            /// <summary>无</summary>
            None,
            [InspectorName("无暇")]
            /// <summary>无暇</summary>
            Clean,
            [InspectorName("平衡")]
            /// <summary>平衡</summary>
            Balanced,
            [InspectorName("危险")]
            /// <summary>危险</summary>
            Unstable,
        }

    }
}