using System.Collections;
using System.Collections.Generic;
using FPSGame.Core;
using FPSGame.GameContract;
using UnityEngine;

namespace FPSGame.Game
{

/// <summary>
/// 战役等级与全局难度参数配置。
/// </summary>
[AddComponentMenu("单位/战役配置")]
public class CampaignCfg : MonoBehaviour
{
    public int[] useAirdrops;
    public EnemyVarietyType enemy;
    [InspectorName("地图尺寸")]
    public SizeType sizeType = SizeType.Medium;



}
}
