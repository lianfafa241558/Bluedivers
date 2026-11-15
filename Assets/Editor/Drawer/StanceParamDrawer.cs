using UnityEditor;
using UnityEngine;
using FPSGame.Gameplay;

namespace FPSGame.EditorExt
{
    /// <summary>姿态参数表里"一条姿态"的检视器。
    /// <para>▍折叠态只占一行：姿态中文名 + 关键数值摘要，用来一眼看出这条配的是谁、大概什么手感；
    /// 展开后按 占位 / 手感 / 感知 三组排，同一组的数值左右并排，10 个字段不至于拉成一长条。</para>
    /// <para>⚠ 高度必须和 <see cref="OnGUI"/> 完全一致（分组标题 + 每行 Pad），否则 List 里会出现重叠/空隙。</para>
    /// </summary>
    [CustomPropertyDrawer(typeof(StanceParam))]
    public class StanceParamDrawer : PropertyDrawer
    {
        private const float Pad = 2f;                 // 行距
        private const float HeaderHeight = 20f;       // 折叠头
        private const float PairLabelWidth = 76f;     // 并排行左侧标签宽
        private const float PairGap = 8f;             // 左右两列间距

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);

            DrawHeader(position, property);

            if (property.isExpanded)
            {
                EditorGUI.indentLevel++;
                float y = position.y + HeaderHeight + Pad;

                y = DrawRow(position, property.FindPropertyRelative("Stance"), "姿态",
                    "这条参数属于哪个姿态（表里同一姿态只应出现一次）", y);

                y = DrawGroupTitle(position, "占位 · 胶囊与第一人称镜头",
                    "高度/前移填 0 = 沿用站立基线", y);
                y = DrawRow(position, property.FindPropertyRelative("CapsuleHeight"), "胶囊高度",
                    "<=0 用站立基线；运行时还会夹到 radius*2 以上，否则 Unity 会把胶囊压成球", y);
                y = DrawRow(position, property.FindPropertyRelative("CameraHeight"), "镜头高度",
                    "第一人称相机离地高度（米）；<=0 用站立基线", y);
                y = DrawRow(position, property.FindPropertyRelative("CameraForward"), "镜头前移",
                    "相机沿玩家本地 +Z 前移多少米：趴伏时头在身体前方，只降高度相机会埋进脖子/胸口", y);
                y = DrawRow(position, property.FindPropertyRelative("SocketDrop"), "挂点下沉",
                    "FirstPersonSocket 往下挪多少米（它挂在玩家根节点、不吃相机高度）", y);

                y = DrawGroupTitle(position, "手感 · 移动与射击", "倍率填 0 会按 1 处理", y);
                y = DrawPair(position, property.FindPropertyRelative("SpeedScale"), "移速",
                    property.FindPropertyRelative("SpreadScale"), "散布", y);
                y = DrawPair(position, property.FindPropertyRelative("RecoilScale"), "后坐",
                    property.FindPropertyRelative("PitchLimit"), "俯仰限位", y);

                y = DrawGroupTitle(position, "感知 · AI", "越小越难被发现", y);
                y = DrawRow(position, property.FindPropertyRelative("DetectRangeScale"), "被侦测距离倍率",
                    "敌人视觉发现本单位的距离倍率：1 = 站直", y);

                EditorGUI.indentLevel--;
            }

            EditorGUI.EndProperty();
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            if (!property.isExpanded) return HeaderHeight;

            float h = HeaderHeight + Pad;
            h += RowHeight(property, "Stance");

            h += GroupTitleHeight;
            h += RowHeight(property, "CapsuleHeight");
            h += RowHeight(property, "CameraHeight");
            h += RowHeight(property, "CameraForward");
            h += RowHeight(property, "SocketDrop");

            h += GroupTitleHeight;
            h += PairHeight(property, "SpeedScale", "SpreadScale");
            h += PairHeight(property, "RecoilScale", "PitchLimit");

            h += GroupTitleHeight;
            h += RowHeight(property, "DetectRangeScale");
            return h;
        }

        #region 绘制

        /// <summary>折叠头：左侧姿态中文名（加粗），右侧关键数值摘要（mini）。</summary>
        private static void DrawHeader(Rect position, SerializedProperty property)
        {
            var headRect = new Rect(position.x, position.y, position.width, HeaderHeight);
            property.isExpanded = EditorGUI.Foldout(headRect, property.isExpanded, GUIContent.none, true);

            var stance = property.FindPropertyRelative("Stance");
            string title = StanceName(stance);

            var titleRect = new Rect(headRect.x + 14f, headRect.y, headRect.width - 14f, EditorGUIUtility.singleLineHeight);
            EditorGUI.LabelField(titleRect, new GUIContent(title, "展开调这条姿态的完整参数"), EditorStyles.boldLabel);

            // 摘要靠右放：不去量中文宽（编辑器主字体对 CJK 的 CalcSize 不准，量了会互相压字）
            float sumW = Mathf.Min(300f, headRect.width * 0.6f);
            var sumRect = new Rect(headRect.xMax - sumW - 4f, headRect.y, sumW, EditorGUIUtility.singleLineHeight);
            EditorGUI.LabelField(sumRect, Summary(property), EditorStyles.miniLabel);
        }

        /// <summary>分组小标题（一行，左侧加粗，右侧灰字提示）。</summary>
        private static float DrawGroupTitle(Rect position, string title, string hint, float y)
        {
            var rect = new Rect(position.x, y, position.width, EditorGUIUtility.singleLineHeight);
            EditorGUI.LabelField(rect, title, EditorStyles.boldLabel);
            if (!string.IsNullOrEmpty(hint))
            {
                float hintW = Mathf.Min(260f, rect.width * 0.5f);
                EditorGUI.LabelField(new Rect(rect.xMax - hintW, y, hintW, rect.height), hint, EditorStyles.miniLabel);
            }
            return y + GroupTitleHeight;
        }

        /// <summary>整行一个字段。</summary>
        private static float DrawRow(Rect position, SerializedProperty prop, string label, string tip, float y)
        {
            if (prop == null) return y;
            float h = EditorGUI.GetPropertyHeight(prop);
            var rect = new Rect(position.x, y, position.width, h);
            EditorGUI.PropertyField(rect, prop, new GUIContent(label, tip), true);
            return y + h + Pad;
        }

        /// <summary>左右并排两个字段（同一组内成对出现，省纵向空间）。</summary>
        private static float DrawPair(Rect position, SerializedProperty left, string leftLabel,
            SerializedProperty right, string rightLabel, float y)
        {
            float hl = left != null ? EditorGUI.GetPropertyHeight(left) : EditorGUIUtility.singleLineHeight;
            float hr = right != null ? EditorGUI.GetPropertyHeight(right) : EditorGUIUtility.singleLineHeight;
            float h = Mathf.Max(hl, hr);
            float colW = (position.width - PairGap) * 0.5f;
            float fieldW = Mathf.Max(24f, colW - PairLabelWidth);

            if (left != null)
            {
                var lr = new Rect(position.x, y, PairLabelWidth, h);
                EditorGUI.LabelField(lr, new GUIContent(leftLabel, left.tooltip));
                EditorGUI.PropertyField(new Rect(lr.xMax, y, fieldW, h), left, GUIContent.none, true);
            }
            if (right != null)
            {
                var rr = new Rect(position.x + colW + PairGap, y, PairLabelWidth, h);
                EditorGUI.LabelField(rr, new GUIContent(rightLabel, right.tooltip));
                EditorGUI.PropertyField(new Rect(rr.xMax, y, fieldW, h), right, GUIContent.none, true);
            }
            return y + h + Pad;
        }

        #endregion

        #region 尺寸

        private static float GroupTitleHeight => EditorGUIUtility.singleLineHeight + Pad;

        private static float RowHeight(SerializedProperty parent, string name)
        {
            var p = parent.FindPropertyRelative(name);
            return p == null ? 0f : EditorGUI.GetPropertyHeight(p) + Pad;
        }

        private static float PairHeight(SerializedProperty parent, string a, string b)
        {
            float ha = EditorGUIUtility.singleLineHeight;
            float hb = EditorGUIUtility.singleLineHeight;
            var pa = parent.FindPropertyRelative(a);
            var pb = parent.FindPropertyRelative(b);
            if (pa != null) ha = EditorGUI.GetPropertyHeight(pa);
            if (pb != null) hb = EditorGUI.GetPropertyHeight(pb);
            return Mathf.Max(ha, hb) + Pad;
        }

        #endregion

        #region 文本

        /// <summary>枚举显示名（走 Unity 的 [InspectorName]，所以这里是"站立/下蹲/卧倒"而不是枚举标识符）。</summary>
        private static string StanceName(SerializedProperty stance)
        {
            if (stance == null) return "姿态";
            var names = stance.enumDisplayNames;
            if (names == null || names.Length == 0) return "姿态";
            int i = Mathf.Clamp(stance.enumValueIndex, 0, names.Length - 1);
            return names[i];
        }

        /// <summary>折叠态摘要：只列最常改的四个值，0 的高度类按"沿用基线"理解。</summary>
        private static string Summary(SerializedProperty p)
        {
            float cap = p.FindPropertyRelative("CapsuleHeight").floatValue;
            float cam = p.FindPropertyRelative("CameraHeight").floatValue;
            float fwd = p.FindPropertyRelative("CameraForward").floatValue;
            float speed = p.FindPropertyRelative("SpeedScale").floatValue;
            float detect = p.FindPropertyRelative("DetectRangeScale").floatValue;

            string capText = cap > 0f ? cap.ToString("0.##") : "基线";
            string camText = cam > 0f ? cam.ToString("0.##") : "基线";
            if (fwd > 0f) camText += "+前" + fwd.ToString("0.##");
            return $"胶囊 {capText}   镜高 {camText}   移速 {(speed > 0f ? speed.ToString("0.##") : "1")}   侦测 {(detect > 0f ? detect.ToString("0.##") : "1")}";
        }

        #endregion
    }
}
