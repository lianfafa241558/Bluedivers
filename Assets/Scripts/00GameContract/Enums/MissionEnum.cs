using UnityEngine;

namespace FPSGame.GameContract
{
    /// <summary>
    /// 任务类型。命名前缀即分类：<c>主线/</c>、<c>撤离/</c>、<c>次要/</c>、<c>巢穴/</c>、<c>子任务/</c>。
    /// <para>被 <c>CampData_SO</c> 的 <c>mainTypes</c>/<c>extraTypes</c>/<c>nestTypes</c>/<c>mainTypesBackup</c> 等资产按 int 序列化。</para>
    /// <para>⚠ 取值分四段、<b>禁止混用</b>：
    /// ① 0 起连续自增（主线 / 撤离 / 次要）；② 100~110 巢穴；③ 200~204 子任务；④ 300 次要撤离区。
    /// 其中①段是<b>隐式取值</b> ⇒ 只能在段尾追加，禁止在中间插入或删除，否则后续成员全体错位、存量资产会指向错误任务。
    /// ②③④段均为显式取值，可自由排序。</para>
    /// </summary>
    public enum MissionEnum
    {
        // ═══ 0 起连续自增段：只能在段尾追加 ═══

        /// <summary>歼灭</summary>
        [InspectorName("主线/歼灭")] Annihilation,
        /// <summary>解救</summary>
        [InspectorName("主线/解救")] Rescue,
        /// <summary>采集</summary>
        [InspectorName("主线/采集")] Explore,
        /// <summary>护送</summary>
        [InspectorName("主线/护送")] Escort,
        /// <summary>上传数据</summary>
        [InspectorName("主线/上传数据")] RetrieveData,
        /// <summary>防御</summary>
        [InspectorName("主线/防御")] Defend,
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
        /// <summary>钻机摧毁工厂</summary>
        [InspectorName("主线/钻机摧毁工厂")] NukeNursery,

        /// <summary>占位符（保留取值，勿删）</summary>
        [InspectorName("主线/占位符")] Placeholder23,
        /// <summary>占位符（保留取值，勿删）</summary>
        [InspectorName("主线/占位符")] Placeholder24,
        /// <summary>占位符（保留取值，勿删）</summary>
        [InspectorName("主线/占位符")] Placeholder25,
        /// <summary>占位符（保留取值，勿删）</summary>
        [InspectorName("主线/占位符")] Placeholder26,
        /// <summary>占位符（保留取值，勿删）</summary>
        [InspectorName("主线/占位符")] Placeholder27,

        /// <summary>摧毁空军基地</summary>
        [InspectorName("主线/摧毁空军基地")] Airport,
        /// <summary>拦截车队</summary>
        [InspectorName("主线/拦截车队")] Motorcade,

        /// <summary>占位符（保留取值，勿删）</summary>
        [InspectorName("主线/占位符")] Placeholder30,
        /// <summary>占位符（保留取值，勿删）</summary>
        [InspectorName("主线/占位符")] Placeholder31,
        /// <summary>占位符（保留取值，勿删）</summary>
        [InspectorName("主线/占位符")] Placeholder32,
        /// <summary>占位符（保留取值，勿删）</summary>
        [InspectorName("主线/占位符")] Placeholder33,
        /// <summary>占位符（保留取值，勿删）</summary>
        [InspectorName("主线/占位符")] Placeholder34,
        /// <summary>占位符（保留取值，勿删）</summary>
        [InspectorName("主线/占位符")] Placeholder35,
        /// <summary>占位符（保留取值，勿删）</summary>
        [InspectorName("主线/占位符")] Placeholder36,
        /// <summary>占位符（保留取值，勿删）</summary>
        [InspectorName("主线/占位符")] Placeholder37,
        /// <summary>占位符（保留取值，勿删）</summary>
        [InspectorName("主线/占位符")] Placeholder38,
        /// <summary>占位符（保留取值，勿删）</summary>
        [InspectorName("主线/占位符")] Placeholder39,

        /// <summary>战役</summary>
        [InspectorName("主线/战役")] Campaign,

        // ── 撤离 ──

        /// <summary>迅速静态撤离</summary>
        [InspectorName("撤离/迅速静态撤离")] EvacuateFast,
        /// <summary>动态撤离</summary>
        [InspectorName("撤离/动态撤离")] EvacuateMove,
        /// <summary>静态撤离</summary>
        [InspectorName("撤离/静态撤离")] EvacuateStatic,
        /// <summary>迅速动态撤离</summary>
        [InspectorName("撤离/迅速动态撤离")] EvacuateMoveFast,

        // ── 次要 ──

        /// <summary>黑盒</summary>
        [InspectorName("次要/黑盒")] BlackBox,
        /// <summary>激光雷达站</summary>
        [InspectorName("次要/激光雷达站")] RadarStation,
        /// <summary>非法广播</summary>
        [InspectorName("次要/非法广播")] Broadcast,
        /// <summary>科研哨站</summary>
        [InspectorName("次要/科研哨站")] ScienceFacility,
        /// <summary>火炮阵地</summary>
        [InspectorName("次要/火炮阵地")] ArtilleryPosition,
        /// <summary>占位符（保留取值，勿删）</summary>
        [InspectorName("次要/占位符")] Placeholder3,
        /// <summary>飞龙巢</summary>
        [InspectorName("次要/飞龙巢")] SpireNest,
        /// <summary>隐刀巢穴</summary>
        [InspectorName("次要/隐刀巢穴")] StealthNest,
        /// <summary>占位符（保留取值，勿删）</summary>
        [InspectorName("次要/占位符")] Placeholder4,
        /// <summary>占位符（保留取值，勿删）</summary>
        [InspectorName("次要/占位符")] Placeholder5,
        /// <summary>占位符（保留取值，勿删）</summary>
        [InspectorName("次要/占位符")] Placeholder6,
        /// <summary>直升机制造厂</summary>
        [InspectorName("次要/直升机制造厂")] HelicopterFactory,
        /// <summary>干扰塔/机器人</summary>
        [InspectorName("次要/干扰塔/机器人")] JammingTowerRoBot,
        /// <summary>占位符（保留取值，勿删）</summary>
        [InspectorName("次要/占位符")] Placeholder7,
        /// <summary>占位符（保留取值，勿删）</summary>
        [InspectorName("次要/占位符")] Placeholder8,
        /// <summary>占位符（保留取值，勿删）</summary>
        [InspectorName("次要/占位符")] Placeholder9,
        /// <summary>干扰塔/色彩</summary>
        [InspectorName("次要/干扰塔/色彩")] JammingTowerColour,
        /// <summary>占位符（保留取值，勿删）</summary>
        [InspectorName("次要/占位符")] Placeholder10,
        /// <summary>占位符（保留取值，勿删）</summary>
        [InspectorName("次要/占位符")] Placeholder11,
        /// <summary>占位符（保留取值，勿删）</summary>
        [InspectorName("次要/占位符")] Placeholder12,
        /// <summary>占位符（保留取值，勿删）</summary>
        [InspectorName("次要/占位符")] Placeholder13,

        /// <summary>次要撤离区（摧毁区域内单位完成，同时作为可选撤离点）</summary>
        [InspectorName("次要/次要撤离区")] SecondaryEvacuate = 300,

        // ═══ 巢穴（100~110，显式取值）═══

        /// <summary>十字神明 - S</summary>
        [InspectorName("巢穴/十字神明-S")] NestDecS = 100,
        /// <summary>十字神明 - M</summary>
        [InspectorName("巢穴/十字神明-M")] NestDecM = 101,
        /// <summary>十字神明 - L</summary>
        [InspectorName("巢穴/十字神明-L")] NestDecL = 102,
        /// <summary>凯撒 - S</summary>
        [InspectorName("巢穴/凯撒-S")] NestKaiserS = 104,
        /// <summary>凯撒 - M</summary>
        [InspectorName("巢穴/凯撒-M")] NestKaiserM = 105,
        /// <summary>凯撒 - L</summary>
        [InspectorName("巢穴/凯撒-L")] NestKaiserL = 106,
        /// <summary>色彩 - S</summary>
        [InspectorName("巢穴/色彩-S")] NestColourS = 108,
        /// <summary>色彩 - M</summary>
        [InspectorName("巢穴/色彩-M")] NestColourM = 109,
        /// <summary>色彩 - L</summary>
        [InspectorName("巢穴/色彩-L")] NestColourL = 110,

        // ═══ 子任务（200~204，显式取值）═══

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
