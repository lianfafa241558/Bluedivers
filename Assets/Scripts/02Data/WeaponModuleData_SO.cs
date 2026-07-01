using System.Collections;
using System.Collections.Generic;

using UnityEngine;

namespace Unity.FPS.Game
{

    [CreateAssetMenu(fileName = "WMD_", menuName = "Data/武器模组")]
    public class WeaponModuleData_SO : ScriptableObject
    {
        public new string name;
        [InspectorName("模组类型")]
        public ModuleType type;
        public Sprite icon;

        public List<KVP<bool, string>> desc;

        [Header("修改属性")]
        public List<ModifyAttrData> modifys;


        public Sprite frame => ResSvc.Instance.LoadSprite("Icon/Frame_Module"+ type.ToString(),true);

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