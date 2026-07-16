using Unity.BaseTool;
using UnityEngine;

public class SpritePreviewAttribute : PropertyAttribute
{
    public int height;
    public int width;

    public SpritePreviewAttribute(int width, int height)
    {
        this.height = height;
        this.width = width;
    }
    public SpritePreviewAttribute(int height = 4)
    {
        this.height = height;
        this.width = height;
    }
}

public class SingleLineItemNameAttribute : PropertyAttribute
{
    public string item1Name;
    public string item2Name;
    public string item3Name;
    public string item4Name;

    public SingleLineItemNameAttribute(string item1Name)
    {
        this.item1Name = item1Name;
    }

    public SingleLineItemNameAttribute(string item1Name, string item2Name) : this(item1Name)
    {
        this.item2Name = item2Name;
    }

    public SingleLineItemNameAttribute(string item1Name, string item2Name, string item3Name) : this(item1Name, item2Name)
    {
        this.item3Name = item3Name;
    }

    public SingleLineItemNameAttribute(string item1Name, string item2Name, string item3Name, string item4Name) : this(item1Name, item2Name, item3Name)
    {
        this.item4Name = item4Name;
    }
}

[System.AttributeUsage(System.AttributeTargets.Class | System.AttributeTargets.Struct)]
public class InlineAttribute : PropertyAttribute
{
    // 可选的：是否显示字段名（默认显示缩写名）
    public bool showLabels = true;

    public InlineAttribute(bool showLabels = true)
    {
        this.showLabels = showLabels;
    }
}