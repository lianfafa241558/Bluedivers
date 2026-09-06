using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using FPSGame.Attribute;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Pixeye.Unity
{
    [CustomEditor(typeof(Object), true, isFallback = true)]
    [CanEditMultipleObjects]
    public partial class EditorOverride : Editor
    {
        //===============================//
        // Members
        //===============================//

        Dictionary<string, CacheFoldProp> cacheFolds = new Dictionary<string, CacheFoldProp>();
        List<SerializedProperty> props = new List<SerializedProperty>();
        List<MethodInfo> methods = new List<MethodInfo>();
        bool initialized;
        Dictionary<string, string> InspectorNames = new Dictionary<string, string>();

        //===============================//
        // Logic
        //===============================//

        void OnEnable()
        {
            initialized = false;
        }

        void OnDisable()
        {
            if (target != null)
                foreach (var c in cacheFolds)
                {
                    EditorPrefs.SetBool(string.Format($"{c.Value.atr.name}{c.Value.props[0].name}{target.GetInstanceID()}"), c.Value.expanded);
                    c.Value.Dispose();
                }
        }

        public override bool RequiresConstantRepaint()
        {
            return EditorFramework.needToRepaint;
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            Setup();

            if (props.Count == 0)
            {
                DrawDefaultInspector();
                return;
            }

            Header();
            Body();

            serializedObject.ApplyModifiedProperties();

            void Header()
            {
                using (new EditorGUI.DisabledScope("m_Script" == props[0].propertyPath))
                {
                    EditorGUILayout.Space();
                    EditorGUILayout.PropertyField(props[0], true);
                    EditorGUILayout.Space();
                }
            }

            void Body()
            {
                foreach (var pair in cacheFolds)
                {
                    this.UseVerticalLayout(() => Foldout(pair.Value), StyleFramework.box);
                    EditorGUI.indentLevel = 0;
                }

                EditorGUILayout.Space();

                for (var i = 1; i < props.Count; i++)
                {
                    var prop = props[i];
                    if (!ShouldDisplayField(prop)) continue;

                    if (IsInlineField(prop))
                    {
                        // 濡傛灉鏄暟缁?List锛屼娇鐢ㄨ嚜瀹氫箟鏁扮粍缁樺埗锛堜繚鐣欏師鐢熸牱寮忥紝鍏冪礌鍐呰仈锛?
                        if (prop.isArray)
                        {
                            // 内联数组头部为自绘，Unity 不会自动画 DecoratorDrawer（如 [Divider]），
                            // 这里按注册表统一补画，任何 Decorator 型特性无需再改 EditorOverride
                            DrawDecorators(prop);
                            DrawInlineArrayNative(prop);
                            //EditorGUILayout.PropertyField(prop, true);
                        }
                        else
                        {
                            DrawInlineObject(prop);
                        }
                    }
                    else
                    {
                        string label = InspectorNames.TryGetValue(prop.name, out var cl) ? cl : null;
                        if (label != null)
                            EditorGUILayout.PropertyField(prop, new GUIContent(label), true);
                        else
                            EditorGUILayout.PropertyField(prop, true);
                    }
                }

                EditorGUILayout.Space();

                if (methods == null) return;
                foreach (MethodInfo memberInfo in methods)
                {
                    this.UseButton(memberInfo);
                }
            }

            void Foldout(CacheFoldProp cache)
            {
                cache.expanded = EditorGUILayout.Foldout(cache.expanded, cache.atr.name, true,
                        StyleFramework.foldout);

                if (cache.expanded)
                {
                    EditorGUI.indentLevel = 1;

                    for (int i = 0; i < cache.props.Count; i++)
                    {
                        if (!ShouldDisplayField(cache.props[i])) continue;
                        this.UseVerticalLayout(() => Child(i), StyleFramework.boxChild);
                    }
                }

                void Child(int i)
                {
                    var prop = cache.props[i];

                    if (IsInlineField(prop))
                    {
                        if (prop.isArray)
                        {
                            // 同 Body：内联数组自绘头部需手动补画 Decorator
                            DrawDecorators(prop);
                            DrawInlineArrayNative(prop);
                        }
                        else
                            DrawInlineObject(prop);
                    }
                    else
                    {
                        string label = InspectorNames.TryGetValue(prop.name, out var cl) ? cl : ObjectNames.NicifyVariableName(prop.name);
                        EditorGUILayout.PropertyField(prop, new GUIContent(label), true);
                    }
                }
            }

            void Setup()
            {
                EditorFramework.currentEvent = Event.current;
                if (!initialized)
                {
                    List<FieldInfo> objectFields;
                    FoldoutAttribute prevFold = default;

                    var length = EditorTypes.Get(target, out objectFields);

                    for (var i = 0; i < length; i++)
                    {
                        #region FOLDERS

                        var fold = Attribute.GetCustomAttribute(objectFields[i], typeof(FoldoutAttribute)) as FoldoutAttribute;
                        CacheFoldProp c;
                        if (fold == null)
                        {
                            if (prevFold != null && prevFold.foldEverything)
                            {
                                if (!cacheFolds.TryGetValue(prevFold.name, out c))
                                {
                                    cacheFolds.Add(prevFold.name, new CacheFoldProp { atr = prevFold, types = new HashSet<string> { objectFields[i].Name } });
                                }
                                else
                                {
                                    c.types.Add(objectFields[i].Name);
                                }
                            }

                            continue;
                        }

                        prevFold = fold;

                        if (!cacheFolds.TryGetValue(fold.name, out c))
                        {
                            var expanded = EditorPrefs.GetBool(string.Format($"{fold.name}{objectFields[i].Name}{target.GetInstanceID()}"), false);
                            cacheFolds.Add(fold.name, new CacheFoldProp { atr = fold, types = new HashSet<string> { objectFields[i].Name }, expanded = expanded });
                        }
                        else c.types.Add(objectFields[i].Name);

                        #endregion
                    }

                    // 鏀堕泦 ListLabel
                    InspectorNames.Clear();
                    foreach (var field in objectFields)
                    {
                        var attr = Attribute.GetCustomAttribute(field, typeof(InspectorNameAttribute)) as InspectorNameAttribute;
                        if (attr != null)
                        {
                            InspectorNames[field.Name] = attr.displayName;
                        }
                    }

                    var property = serializedObject.GetIterator();
                    var next = property.NextVisible(true);
                    if (next)
                    {
                        do
                        {
                            HandleFoldProp(property);
                        } while (property.NextVisible(false));
                    }

                    initialized = true;
                }
            }
        }

        public void HandleFoldProp(SerializedProperty prop)
        {
            bool shouldBeFolded = false;

            foreach (var pair in cacheFolds)
            {
                if (pair.Value.types.Contains(prop.name))
                {
                    var pr = prop.Copy();
                    shouldBeFolded = true;
                    pair.Value.props.Add(pr);

                    break;
                }
            }

            if (shouldBeFolded == false)
            {
                var pr = prop.Copy();
                props.Add(pr);
            }
        }

        /// <summary>
        /// 在字段上方绘制其声明的所有 DecoratorDrawer 型特性（如 [Divider]）。
        /// 普通绘制路径由 Unity 自动处理 Decorator，只有内联数组的自绘头部需要手动补画；
        /// 任何 Decorator 型特性无需修改 EditorOverride 即可自动生效。
        /// </summary>
        private void DrawDecorators(SerializedProperty prop)
        {
            var fieldInfo = target.GetType().GetField(prop.name,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (fieldInfo == null) return;
            foreach (var attr in fieldInfo.GetCustomAttributes(typeof(PropertyAttribute), true))
            {
                var drawer = DecoratorDrawerCache.Bind(attr.GetType(), (PropertyAttribute)attr);
                if (drawer == null) continue;
                Rect rect = GUILayoutUtility.GetRect(0, drawer.GetHeight(), GUILayout.ExpandWidth(true));
                drawer.OnGUI(rect);
            }
        }

        /// <summary>
        /// DecoratorDrawer 注册表：按特性类型缓存 DecoratorDrawer 实例。
        /// Unity 没有公开"根据特性获取 DecoratorDrawer"的 API，这里在首次访问时扫描
        /// 所有 DecoratorDrawer 子类的 [CustomPropertyDrawer] 自建映射（纯公开 API）。
        /// 新的 Decorator 型特性只要写了 Drawer 类，即自动加入本机制。
        /// </summary>
        static class DecoratorDrawerCache
        {
            static readonly Dictionary<Type, DecoratorDrawer> drawers = new Dictionary<Type, DecoratorDrawer>();
            static bool initialized;

            // 自建的 Drawer 实例绕过了 Unity 的创建流程，Unity 不会注入 m_Attribute，
            // 必须在每次绘制前手动绑定实际特性实例，否则 Drawer 内取 attribute 会 NRE
            static readonly FieldInfo attributeField =
                typeof(DecoratorDrawer).GetField("m_Attribute", BindingFlags.Instance | BindingFlags.NonPublic);

            /// <summary>
            /// 取缓存 Drawer 并绑定本次绘制的实际特性实例（绑定必须每次进行：缓存实例被所有字段复用，
            /// 且实际实例携带真实构造参数，如 [TextArea(4,10)] 的 minLines/maxLines）。
            /// </summary>
            public static DecoratorDrawer Bind(Type attributeType, PropertyAttribute actualAttribute)
            {
                EnsureInit();
                if (drawers.Count == 0) return null;
                if (!drawers.TryGetValue(attributeType, out var drawer) || drawer == null) return null;
                attributeField?.SetValue(drawer, actualAttribute);
                return drawer;
            }

            static void EnsureInit()
            {
                if (initialized) return;
                initialized = true;
                if (attributeField == null) return;

                foreach (var type in TypeCache.GetTypesDerivedFrom<DecoratorDrawer>())
                {
                    // Unity 的特性类名为 CustomPropertyDrawer（无 Attribute 后缀），
                    // 且其 GetHandledType() 为 internal，故通过公开的 GetCustomAttributesData() 读取构造参数
                    foreach (var data in type.GetCustomAttributesData())
                    {
                        if (data.AttributeType != typeof(CustomPropertyDrawer)) continue;

                        var attrType = data.ConstructorArguments.Count > 0
                            ? data.ConstructorArguments[0].Value as Type
                            : null;
                        if (attrType == null || drawers.ContainsKey(attrType)) continue;

                        var ctor = type.GetConstructor(Type.EmptyTypes);
                        if (ctor != null)
                            drawers[attrType] = (DecoratorDrawer)Activator.CreateInstance(type);
                        break;
                    }
                }
            }
        }

        /// <summary>
        /// 字段是否应显示（支持 [Compare] 条件控制）。
        /// 数组/List 字段本身也会被正确判断，避免"字段级 Compare 只作用到元素上"的问题。
        /// </summary>
        private bool ShouldDisplayField(SerializedProperty prop)
        {
            if (target == null) return true;
            var fieldInfo = target.GetType().GetField(prop.name,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (fieldInfo == null) return true;
            var compare = Attribute.GetCustomAttribute(fieldInfo, typeof(CompareAttribute)) as CompareAttribute;
            if (compare == null) return true;
            return CustomLabelDrawer.ShouldDisplayField(prop, compare);
        }


        //===============================//
        // 鍐呴儴绫?
        //===============================//

        class CacheFoldProp
        {
            public HashSet<string> types = new HashSet<string>();
            public List<SerializedProperty> props = new List<SerializedProperty>();
            public FoldoutAttribute atr;
            public bool expanded;

            public void Dispose()
            {
                props.Clear();
                types.Clear();
                atr = null;
            }
        }
    }

    //===============================//
    // 鎵╁睍鏂规硶
    //===============================//

    static class ditorUIHelper
    {
        public static void UseVerticalLayout(this Editor e, Action action, GUIStyle style)
        {
            EditorGUILayout.BeginVertical(style);
            action();
            EditorGUILayout.EndVertical();
        }

        public static void UseButton(this Editor e, MethodInfo m)
        {
            if (GUILayout.Button(m.Name))
            {
                m.Invoke(e.target, null);
            }
        }
    }

    //===============================//
    // 鏍峰紡
    //===============================//

    static class StyleFramework
    {
        public static GUIStyle box;
        public static GUIStyle boxChild;
        public static GUIStyle foldout;
        public static GUIStyle button;
        public static GUIStyle text;

        static StyleFramework()
        {
            bool pro = EditorGUIUtility.isProSkin;

            Texture2D uiTex_in = null;
            Texture2D uiTex_in_on = null;

            var c_on = pro ? Color.white : new Color(51 / 255f, 102 / 255f, 204 / 255f, 1);

            button = new GUIStyle(EditorStyles.miniButton);
            button.font = Font.CreateDynamicFontFromOSFont(new[] { "Terminus (TTF) for Windows", "Calibri" }, 17);

            text = new GUIStyle(EditorStyles.label);
            text.richText = true;
            text.contentOffset = new Vector2(0, 5);
            text.font = Font.CreateDynamicFontFromOSFont(new[] { "Terminus (TTF) for Windows", "Calibri" }, 14);

            foldout = new GUIStyle(EditorStyles.foldout);

            foldout.overflow = new RectOffset(-10, 0, 3, 0);
            foldout.padding = new RectOffset(25, 0, -3, 0);

            foldout.active.textColor = c_on;
            foldout.active.background = uiTex_in;
            foldout.onActive.textColor = c_on;
            foldout.onActive.background = uiTex_in_on;

            foldout.focused.textColor = c_on;
            foldout.focused.background = uiTex_in;
            foldout.onFocused.textColor = c_on;
            foldout.onFocused.background = uiTex_in_on;

            foldout.hover.textColor = c_on;
            foldout.hover.background = uiTex_in;

            foldout.onHover.textColor = c_on;
            foldout.onHover.background = uiTex_in_on;

            box = new GUIStyle();
            box.padding = new RectOffset(10, 10, 10, 0);

            boxChild = new GUIStyle();
            boxChild.active.textColor = c_on;
            boxChild.active.background = uiTex_in;
            boxChild.onActive.textColor = c_on;
            boxChild.onActive.background = uiTex_in_on;

            boxChild.focused.textColor = c_on;
            boxChild.focused.background = uiTex_in;
            boxChild.onFocused.textColor = c_on;
            boxChild.onFocused.background = uiTex_in_on;

            EditorStyles.foldout.active.textColor = c_on;
            EditorStyles.foldout.active.background = uiTex_in;
            EditorStyles.foldout.onActive.textColor = c_on;
            EditorStyles.foldout.onActive.background = uiTex_in_on;

            EditorStyles.foldout.focused.textColor = c_on;
            EditorStyles.foldout.focused.background = uiTex_in;
            EditorStyles.foldout.onFocused.textColor = c_on;
            EditorStyles.foldout.onFocused.background = uiTex_in_on;

            EditorStyles.foldout.hover.textColor = c_on;
            EditorStyles.foldout.hover.background = uiTex_in;

            EditorStyles.foldout.onHover.textColor = c_on;
            EditorStyles.foldout.onHover.background = uiTex_in_on;
        }

        public static string FirstLetterToUpperCase(this string s)
        {
            if (string.IsNullOrEmpty(s))
                return string.Empty;

            var a = s.ToCharArray();
            a[0] = char.ToUpper(a[0]);
            return new string(a);
        }

        public static IList<Type> GetTypeTree(this Type t)
        {
            var types = new List<Type>();
            while (t.BaseType != null)
            {
                types.Add(t);
                t = t.BaseType;
            }

            return types;
        }
    }

    //===============================//
    // 鍙嶅皠宸ュ叿
    //===============================//

    static class EditorTypes
    {
        public static Dictionary<int, List<FieldInfo>> fields = new Dictionary<int, List<FieldInfo>>(FastComparable.Default);

        public static int Get(Object target, out List<FieldInfo> objectFields)
        {
            var t = target.GetType();
            var hash = t.GetHashCode();

            if (!fields.TryGetValue(hash, out objectFields))
            {
                var typeTree = t.GetTypeTree();
                objectFields = target.GetType()
                        .GetFields(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.NonPublic)
                        .OrderByDescending(x => typeTree.IndexOf(x.DeclaringType))
                        .ToList();
                fields.Add(hash, objectFields);
            }

            return objectFields.Count;
        }
    }

    //===============================//
    // 姣旇緝鍣?
    //===============================//

    class FastComparable : IEqualityComparer<int>
    {
        public static FastComparable Default = new FastComparable();

        public bool Equals(int x, int y)
        {
            return x == y;
        }

        public int GetHashCode(int obj)
        {
            return obj.GetHashCode();
        }
    }

    //===============================//
    // 缂栬緫鍣ㄦ鏋?
    //===============================//

    [InitializeOnLoad]
    public static class EditorFramework
    {
        internal static bool needToRepaint;

        internal static Event currentEvent;
        internal static float t;

        static EditorFramework()
        {
            EditorApplication.update += Updating;
        }

        static void Updating()
        {
            CheckMouse();

            if (needToRepaint)
            {
                t += Time.deltaTime;

                if (t >= 0.3f)
                {
                    t -= 0.3f;
                    needToRepaint = false;
                }
            }
        }

        static void CheckMouse()
        {
            var ev = currentEvent;
            if (ev == null) return;

            if (ev.type == EventType.MouseMove)
                needToRepaint = true;
        }
    }
}