using System;
using System.Collections;
using System.Collections.Generic;
using Core;
using UnityEngine;

/// <summary>
/// 检视器折叠
/// </summary>
public class FoldoutAttribute : PropertyAttribute
{
    public string name;

    public bool foldEverything;

    /// <summary>
    /// 将属性添加到指定的文件夹组
    /// </summary>
    /// <param name="name">文件夹的名称</param>
    /// <param name="foldEverything">切换以将所有属性放入指定组</param>
    public FoldoutAttribute(string name, bool foldEverything = false)
    {
        this.foldEverything = foldEverything;
        this.name = name;
    }
}


/// <summary>
/// 使字段在Inspector中根据比较结果自定义显示
/// </summary>
public class CompareAttribute : PropertyAttribute
{

    public string contField;

    public int enumValue;

    public CompareOperate operate;



    public CompareAttribute(string contField)
    {
        this.contField = contField;
    }

    public CompareAttribute(string contField, int enumValue, CompareOperate operate = CompareOperate.Greater)
    {
        this.contField = contField;
        this.enumValue = enumValue;
        this.operate = operate;
    }
}

#if UNITY_EDITOR
[AttributeUsage(AttributeTargets.Field)]
#endif
public class DisplayField : PropertyAttribute
{
    public bool read = true;
    public bool run = true;
    public bool editor = false;
    public DisplayField() { }

    public DisplayField(bool onlyRun)
    {
        this.run = onlyRun;
    }
    public DisplayField(bool onlyRun, bool onlyRead)
    {
        this.run = onlyRun;
        this.read = onlyRead;
    }

    public DisplayField(bool read, bool run, bool editor) : this(read, run)
    {
        this.editor = editor;
    }
}

#if UNITY_EDITOR
[AttributeUsage(AttributeTargets.Field)]
#endif
public class ReadOnly : PropertyAttribute
{
}
