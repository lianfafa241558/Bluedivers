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

                    if (IsInlineField(prop))
                    {
                        // 濡傛灉鏄暟缁?List锛屼娇鐢ㄨ嚜瀹氫箟鏁扮粍缁樺埗锛堜繚鐣欏師鐢熸牱寮忥紝鍏冪礌鍐呰仈锛?
                        if (prop.isArray)
                        {
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
                        this.UseVerticalLayout(() => Child(i), StyleFramework.boxChild);
                    }
                }

                void Child(int i)
                {
                    var prop = cache.props[i];
                    if (IsInlineField(prop))
                    {
                        if (prop.isArray)
                            DrawInlineArrayNative(prop);
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