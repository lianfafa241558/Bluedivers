#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEngine;

public class GenerateKeyCodeItems
{
    // 键盘物理布局顺序（从左上到右下）
    private static readonly KeyCode[] KeyboardLayout =
    {
        // 第一行：功能键区
        KeyCode.Escape, KeyCode.F1, KeyCode.F2, KeyCode.F3, KeyCode.F4,
        KeyCode.F5, KeyCode.F6, KeyCode.F7, KeyCode.F8,
        KeyCode.F9, KeyCode.F10, KeyCode.F11, KeyCode.F12,
        KeyCode.Print, KeyCode.ScrollLock, KeyCode.Pause,

        // 第二行：数字键区 + 编辑键区
        KeyCode.BackQuote,
        KeyCode.Alpha1, KeyCode.Alpha2, KeyCode.Alpha3, KeyCode.Alpha4, KeyCode.Alpha5,
        KeyCode.Alpha6, KeyCode.Alpha7, KeyCode.Alpha8, KeyCode.Alpha9, KeyCode.Alpha0,
        KeyCode.Minus, KeyCode.Equals,
        KeyCode.Backspace,
        KeyCode.Insert, KeyCode.Home, KeyCode.PageUp,
        KeyCode.Numlock, KeyCode.KeypadDivide, KeyCode.KeypadMultiply, KeyCode.KeypadMinus,

        // 第三行：字母区上排（QWERTY）
        KeyCode.Tab,
        KeyCode.Q, KeyCode.W, KeyCode.E, KeyCode.R, KeyCode.T,
        KeyCode.Y, KeyCode.U, KeyCode.I, KeyCode.O, KeyCode.P,
        KeyCode.LeftBracket, KeyCode.RightBracket, KeyCode.Backslash,
        KeyCode.Delete, KeyCode.End, KeyCode.PageDown,
        KeyCode.Keypad7, KeyCode.Keypad8, KeyCode.Keypad9, KeyCode.KeypadPlus,

        // 第四行：字母区中排（ASDF）
        KeyCode.CapsLock,
        KeyCode.A, KeyCode.S, KeyCode.D, KeyCode.F, KeyCode.G,
        KeyCode.H, KeyCode.J, KeyCode.K, KeyCode.L,
        KeyCode.Semicolon, KeyCode.Quote,
        KeyCode.Return,
        KeyCode.Keypad4, KeyCode.Keypad5, KeyCode.Keypad6,

        // 第五行：字母区下排（ZXCV）+ 方向键
        KeyCode.LeftShift,
        KeyCode.Z, KeyCode.X, KeyCode.C, KeyCode.V, KeyCode.B,
        KeyCode.N, KeyCode.M,
        KeyCode.Comma, KeyCode.Period, KeyCode.Slash,
        KeyCode.RightShift,
        KeyCode.UpArrow,
        KeyCode.Keypad1, KeyCode.Keypad2, KeyCode.Keypad3, KeyCode.KeypadEnter,

        // 第六行：底部修饰键 + 方向键 + 小键盘
        KeyCode.LeftControl, KeyCode.LeftWindows, KeyCode.LeftAlt,
        KeyCode.Space,
        KeyCode.RightAlt, KeyCode.RightWindows, KeyCode.Menu, KeyCode.RightControl,
        KeyCode.LeftArrow, KeyCode.DownArrow, KeyCode.RightArrow,
        KeyCode.Keypad0, KeyCode.KeypadPeriod,
    };

    [MenuItem("Tools/Generate KeyCode Items")]
    private static void Generate()
    {
        if (Selection.activeGameObject == null)
        {
            Debug.LogWarning("请先在 Hierarchy 中选择一个模板 GameObject");
            return;
        }

        GameObject template = Selection.activeGameObject;
        Transform parent = template.transform.parent;

        // 判断模板是否为预制体实例，预制体则用 InstantiatePrefab 保持预制体链接
        GameObject prefabAsset = null;
        if (PrefabUtility.IsPartOfPrefabInstance(template))
        {
            prefabAsset = PrefabUtility.GetCorrespondingObjectFromSource(template);
        }

        foreach (KeyCode keyCode in KeyboardLayout)
        {
            string keyName = keyCode.ToString();

            // 预制体实例用 InstantiatePrefab 保持预制体链接，普通场景物体用 Instantiate
            GameObject instance;
            if (prefabAsset != null)
            {
                instance = (GameObject)PrefabUtility.InstantiatePrefab(prefabAsset, parent);
                instance.transform.position = template.transform.position;
                instance.transform.rotation = template.transform.rotation;
                instance.transform.localScale = template.transform.localScale;
            }
            else
            {
                instance = GameObject.Instantiate(template, parent);
            }

            instance.name = keyName;

            // 设置 KeyCodeItem 组件
            KeyCodeItem item = instance.GetComponent<KeyCodeItem>();
            if (item != null)
            {
                item.key = keyCode;
            }

            // 设置第一个子物体的 TMP_Text 文本
            if (instance.transform.childCount > 0)
            {
                Transform firstChild = instance.transform.GetChild(0);
                TextMeshProUGUI tmpText = firstChild.GetComponent<TextMeshProUGUI>();
                if (tmpText != null)
                {
                    tmpText.text = keyName;
                }
            }

            Undo.RegisterCreatedObjectUndo(instance, "Generate KeyCode Items");
        }
    }

    [MenuItem("Tools/Generate KeyCode Items", true)]
    private static bool ValidateGenerate()
    {
        return Selection.activeGameObject != null;
    }
}
#endif
