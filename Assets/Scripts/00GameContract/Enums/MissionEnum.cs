using UnityEngine;

namespace FPSGame.GameContract
{

public enum MissionEnum
{
    /// <summary>歼灭</summary>
    [InspectorName("主线/歼灭")]Annihilation,
    /// <summary>解救</summary>
    [InspectorName("主线/解救")]Rescue,
    /// <summary>采集</summary>
    [InspectorName("主线/采集")]Explore,
    /// <summary>护送</summary>
    [InspectorName("主线/护送")] Escort,
    /// <summary>上传数据</summary>
    [InspectorName("主线/上传数据")] RetrieveData,
    /// <summary>防御</summary>
    [InspectorName("主线/防御")]Defend,
    /// <summary>升旗</summary>
    [InspectorName("主线/升旗")] FlagRaising,
    /// <summary>彻底消灭</summary>
    [InspectorName("主线/彻底消灭")] Eradicate,
    /// <summary>搜索并摧毁</summary>
    [InspectorName("主线/搜索并摧毁")] SearchAndDestroy,


    /// <summary>摧毁虫卵</summary>
    [InspectorName("主线/摧毁虫卵")] DestroyEggs,
    /// <summary>采集虫蛋</summary>
    [InspectorName("主线/采集虫蛋")] CollectEggs,
    /// <summary>占位符</summary>
    [InspectorName("主线/钻机摧毁工厂")] NukeNursery,
    /// <summary>占位符</summary>
    [InspectorName("主线/占位符")] Placeholder23,
    /// <summary>占位符</summary>
    [InspectorName("主线/占位符")] Placeholder24,
    /// <summary>占位符</summary>
    [InspectorName("主线/占位符")] Placeholder25,
    /// <summary>占位符</summary>
    [InspectorName("主线/占位符")] Placeholder26,
    /// <summary>占位符</summary>
    [InspectorName("主线/占位符")] Placeholder27,

    /// <summary>占位符</summary>
    [InspectorName("主线/摧毁空军基地")] Airport,
    /// <summary>占位符</summary>
    [InspectorName("主线/拦截车队")] Motorcade,
    /// <summary>占位符</summary>
    [InspectorName("主线/占位符")] Placeholder30,
    /// <summary>占位符</summary>
    [InspectorName("主线/占位符")] Placeholder31,
    /// <summary>占位符</summary>
    [InspectorName("主线/占位符")] Placeholder32,
    /// <summary>占位符</summary>
    [InspectorName("主线/占位符")] Placeholder33,
    /// <summary>占位符</summary>
    [InspectorName("主线/占位符")] Placeholder34,
    /// <summary>占位符</summary>
    [InspectorName("主线/占位符")] Placeholder35,

    /// <summary>占位符</summary>
    [InspectorName("主线/占位符")] Placeholder36,
    /// <summary>占位符</summary>
    [InspectorName("主线/占位符")] Placeholder37,
    /// <summary>占位符</summary>
    [InspectorName("主线/占位符")] Placeholder38,
    /// <summary>占位符</summary>
    [InspectorName("主线/占位符")] Placeholder39,


    [InspectorName("主线/战役")]
    /// <summary>主线/战役</summary>
    Campaign,

    /// <summary>撤离/迅速撤离</summary>
    [InspectorName("撤离/迅速静态撤离")] EvacuateFast,
    /// <summary>撤离/动态撤离</summary>
    [InspectorName("撤离/动态撤离")] EvacuateMove,
    /// <summary>撤离/静态撤离</summary>
    [InspectorName("撤离/静态撤离")] EvacuateStatic,
    /// <summary>撤离/迅速动态撤离</summary>
    [InspectorName("撤离/迅速动态撤离")] EvacuateMoveFast,


    /// <summary>次要/黑盒</summary>
    [InspectorName("次要/黑盒")] BlackBox,
    /// <summary>次要/激光雷达站</summary>
    [InspectorName("次要/激光雷达站")] RadarStation,
    /// <summary>次要/非法广播</summary>
    [InspectorName("次要/非法广播")] Broadcast,
    /// <summary>科研哨站</summary>
    [InspectorName("次要/科研哨站")] ScienceFacility,
    /// <summary>火炮阵地</summary>
    [InspectorName("次要/火炮阵地")] ArtilleryPosition,
    /// <summary>占位符</summary>
    [InspectorName("次要/占位符")] Placeholder3,

    /// <summary>次要/飞龙巢</summary>
    [InspectorName("次要/飞龙巢")] SpireNest,
    /// <summary>次要/隐刀巢穴</summary>
    [InspectorName("次要/隐刀巢穴")] StealthNest,
    /// <summary>占位符</summary>
    [InspectorName("次要/占位符")] Placeholder4,
    /// <summary>占位符</summary>
    [InspectorName("次要/占位符")] Placeholder5,
    /// <summary>占位符</summary>
    [InspectorName("次要/占位符")] Placeholder6,


    /// <summary>次要/直升机制造厂</summary>
    [InspectorName("次要/直升机制造厂")]
    HelicopterFactory,
    /// <summary>次要/干扰塔/机器人</summary>
    [InspectorName("次要/干扰塔/机器人")]
    JammingTowerRoBot,
    /// <summary>占位符</summary>
    [InspectorName("次要/占位符")] Placeholder7,
    /// <summary>占位符</summary>
    [InspectorName("次要/占位符")] Placeholder8,
    /// <summary>占位符</summary>
    [InspectorName("次要/占位符")] Placeholder9,

    /// <summary>次要/干扰塔/色彩</summary>
    [InspectorName("次要/干扰塔/色彩")]
    JammingTowerColour,
    /// <summary>占位符</summary>
    [InspectorName("次要/占位符")] Placeholder10,
    /// <summary>占位符</summary>
    [InspectorName("次要/占位符")] Placeholder11,
    /// <summary>占位符</summary>
    [InspectorName("次要/占位符")] Placeholder12,
    /// <summary>占位符</summary>
    [InspectorName("次要/占位符")] Placeholder13,

    /// <summary>次要/次要撤离区(摧毁区域内单位完成，同时作为可选撤离点)</summary>
    [InspectorName("次要/次要撤离区")] SecondaryEvacuate = 300,

    [InspectorName("巢穴/十字神明-S")] NestDecS = 100,
    [InspectorName("巢穴/十字神明-M")] NestDecM = 101,
    [InspectorName("巢穴/十字神明-L")] NestDecL = 102,

    [InspectorName("巢穴/凯撒-S")] NestKaiserS = 104,
    [InspectorName("巢穴/凯撒-M")] NestKaiserM = 105,
    [InspectorName("巢穴/凯撒-L")] NestKaiserL = 106,

    [InspectorName("巢穴/色彩-S")] NestColourS = 108,
    [InspectorName("巢穴/色彩-M")] NestColourM = 109,
    [InspectorName("巢穴/色彩-L")] NestColourL = 110,

    /// <summary>升旗子任务</summary>
    [InspectorName("子任务/升旗")] SubFlagRaising = 200,
    /// <summary>获取高价值数据</summary>
    [InspectorName("子任务/获取高价值数据")] SubGetData = 201,
    /// <summary>重启发电机</summary>
    [InspectorName("子任务/重启发电机")] SubRestartGenerator = 202,
    /// <summary>连接油管</summary>
    [InspectorName("子任务/连接油管")] SubConnectPipes = 203,
    /// <summary>采集虫蛋</summary>
    [InspectorName("子任务/采集虫蛋")] SubEggHunt = 204,
}
}
