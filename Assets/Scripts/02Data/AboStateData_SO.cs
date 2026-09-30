using System.Collections;
using System.Collections.Generic;
using FPSGame.Core;
using UnityEngine;

namespace FPSGame.Data
{


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

    /// <summary>
    /// 运行时缓存（键 = 异常类型）。由 <c>ResSvc.Init()</c> 加载 <c>GameData/AboState</c> 时填充。
    /// <para>⚠ 2026-09-30 从 <c>ResSvc</c>（01Manager）迁到本类（04_Data）：读它的 <c>Health</c>/<c>Health_AboState</c>
    /// 属于单位内核（<c>05_UnitCore</c>），放在上层会让单位内核产生上行依赖、程序集拆不动。
    /// 这是**资源缓存**而非"每个资产各一份的配置"，放静态字段安全。</para>
    /// </summary>
    public static Dictionary<DamageTypeEnum, AboStateData_SO> Dic;
}
}
