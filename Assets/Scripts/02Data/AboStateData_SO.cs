using System.Collections;
using System.Collections.Generic;
using Core;
using UnityEngine;


[CreateAssetMenu(fileName = "new Data", menuName = "Data/异常状态")]
public class AboStateData_SO :ScriptableObject
{
    public DamageTypeEnum typeEnum;
    public Sprite icon;
    [ColorUsage(false, false)]  // 第一个参数 true 表示显示 HDR，第二个参数 false 表示不显示 Alpha
    public Color color;
    [InspectorName("最短维持时间")]
    public float duration;
    [InspectorName("恢复速度")]
    public float recovery;
    [InspectorName("伤害")]
    public float damage;
    [InspectorName("积蓄槽满了的伤害")]
    public float fullDamage;
    [InspectorName("积蓄槽满了的百分比伤害")]
    public float fullPerDamage;
    [InspectorName("添加特效")]
    public GameObject vfx;
    [InspectorName("伤害触发响应")]
    public bool damageTriggeredResponse;
}
