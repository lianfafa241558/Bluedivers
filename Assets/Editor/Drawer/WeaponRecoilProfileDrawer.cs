using System.Collections.Generic;
using System.Reflection;
using FPSGame.Weapon;
using UnityEditor;
using UnityEngine;

namespace FPSGame.EditorExt
{
    /// <summary>
    /// WeaponRecoilProfile 的 Inspector。
    /// <para>▍为什么要自己画：全局 <see cref="Pixeye.Unity.EditorOverride"/> 只在 Setup 里解析**顶层类型**字段的
    /// [InspectorName]，嵌套 [Serializable] 类的子字段由 Unity 原生递归绘制 ⇒ 显示成 Nicify 名
    /// （PitchKick → "Pitch Kick"），中文名全丢。这里按分组自己取 label（同 InlineFieldDrawer 的做法）。</para>
    /// </summary>
    [CustomPropertyDrawer(typeof(WeaponRecoilProfile))]
    public class WeaponRecoilProfileDrawer : PropertyDrawer
    {
        /// <summary>分组顺序 = Inspector 里的显示顺序；字段名必须与 C# 完全一致</summary>
        private static readonly (string Title, string[] Fields)[] Groups =
        {
            ("相机（视角被推）", new[]
            {
                "PitchKick", "YawKick", "YawStep", "YawRandom", "KickSharpness",
                "RecoveryDelay", "RecoverySpeed", "YawRecoveryRatio", "MaxPitch", "MaxYaw", "AdsScale",
            }),
            ("武器模型（枪被推）", new[]
            {
                "WeaponKickBack", "WeaponKickPitch", "WeaponKickRoll", "WeaponSpring",
            }),
            ("连射散布 / 屏震", new[]
            {
                "SpreadBloomPerShot", "SpreadBloomMax", "SpreadBloomDecay", "ShakePerShot",
            }),
        };

        private const float Gap = 2f;

        /// <summary>字段名 → 显示名（[InspectorName] 去掉 "分组/" 前缀，分组标题已经写过了）</summary>
        private static Dictionary<string, string> s_Labels;

        private static float GroupHeaderHeight => EditorGUIUtility.singleLineHeight + 1f;

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);

            var head = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
            property.isExpanded = EditorGUI.Foldout(head, property.isExpanded, label, true);
            if (property.isExpanded)
            {
                float y = head.y + head.height + Gap;
                var labels = Labels();

                foreach (var group in Groups)
                {
                    EditorGUI.LabelField(new Rect(position.x, y, position.width, EditorGUIUtility.singleLineHeight),
                        group.Title, EditorStyles.boldLabel);
                    y += GroupHeaderHeight;

                    foreach (var name in group.Fields)
                    {
                        var child = property.FindPropertyRelative(name);
                        if (child == null) continue;   // 字段改名后这里静默跳过，靠 GetPropertyHeight 同步

                        // 缩进 8px 表示"在分组标题之下"（rect 自己算，别动 indentLevel）
                        var childRect = new Rect(position.x + 8f, y, position.width - 8f, EditorGUI.GetPropertyHeight(child));
                        EditorGUI.PropertyField(childRect, child, new GUIContent(labels[name]), true);
                        y += childRect.height + Gap;
                    }
                }
            }

            EditorGUI.EndProperty();
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            float h = EditorGUIUtility.singleLineHeight;
            if (!property.isExpanded) return h;

            h += Gap;
            foreach (var group in Groups)
            {
                h += GroupHeaderHeight;
                foreach (var name in group.Fields)
                {
                    var child = property.FindPropertyRelative(name);
                    h += (child != null ? EditorGUI.GetPropertyHeight(child) : EditorGUIUtility.singleLineHeight) + Gap;
                }
            }
            return h;
        }

        private static Dictionary<string, string> Labels()
        {
            if (s_Labels != null) return s_Labels;

            s_Labels = new Dictionary<string, string>();
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public;
            foreach (var group in Groups)
            {
                foreach (var name in group.Fields)
                {
                    var field = typeof(WeaponRecoilProfile).GetField(name, flags);
                    var attr = field != null ? field.GetCustomAttribute<InspectorNameAttribute>() : null;
                    s_Labels[name] = attr != null
                        ? attr.displayName.Substring(attr.displayName.LastIndexOf('/') + 1)   // 去掉 "相机/" 前缀
                        : ObjectNames.NicifyVariableName(name);
                }
            }
            return s_Labels;
        }
    }
}
