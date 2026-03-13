
using UnityEngine;

public class NoticeData_SO : ScriptableObject
{
    public string Desc;
    public AudioClip Clip;
    public string SourceName;
    public Sprite Portrait;
    public int Priority;//优先级（越大约优先）
    public string Type;
    public bool Space;//使用世界空间
    public float Delay;
}
