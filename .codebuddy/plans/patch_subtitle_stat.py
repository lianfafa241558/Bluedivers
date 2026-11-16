# -*- coding: utf-8 -*-
"""在 SubtitleWnd.cs 的命名空间末尾插入 SubtitleWndStat 统计类（幂等：已存在则跳过）。"""
import io
import os
import sys

ROOT = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', '..'))
PATH = os.path.join(ROOT, 'Assets', 'Scripts', '10UI', 'Wnd', 'SubtitleWnd.cs')

MARK = 'public static class SubtitleWndStat'

BLOCK = u'''#if UNITY_EDITOR || DEVELOPMENT_BUILD
/// <summary>
/// <see cref="SubtitleWnd.Update()"/> 的分段计时（**仅编辑器 / 开发版编译**，Release 整段被编掉）。
///
/// <para>▍为什么需要它：全工程没有任何 <c>ProfilerMarker</c>，而 Profiler 在本项目的 Play 模式下
/// 经常只录到编辑器帧（<c>PlayerLoop</c> 恒为 0）⇒ 拿不到脚本级明细。用 Stopwatch 分段后，
/// 不依赖 Profiler 就能随时 <c>Dump()</c>。</para>
///
/// <para>▍三段口径：<b>相机</b> = <c>GameplayCamera.Current</c> 解析（含失效重取）；
/// <b>家具循环</b> = 遍历 <c>Furniture_Attached.list</c> 并做投影；<b>其它</b> = 余下
/// （第三人称提示 / 空投字幕池 / 获得物品提示）。</para>
/// </summary>
public static class SubtitleWndStat
{
    private static int _frames;
    private static long _totalNs;
    private static long _camNs;
    private static long _loopNs;
    private static int _items;

    public static int Frames => _frames;

    public static long Stamp() => System.Diagnostics.Stopwatch.GetTimestamp();

    private static long ToNs(long ticks)
    {
        return (long)(ticks * (1000000000.0 / System.Diagnostics.Stopwatch.Frequency));
    }

    /// <summary>一次 Update 结束（正常返回 / 提前 return 都要调）。</summary>
    public static void Finish(long t0, long tCam, long tLoop, int items)
    {
        long t1 = Stamp();
        _frames++;
        _totalNs += ToNs(t1 - t0);
        _camNs += ToNs(tCam - t0);
        _loopNs += ToNs(tLoop - tCam);
        _items += items;
    }

    public static void Reset()
    {
        _frames = 0;
        _totalNs = 0;
        _camNs = 0;
        _loopNs = 0;
        _items = 0;
    }

    /// <summary>输出均值：总计 / 相机解析 / 家具循环 / 其它，以及每帧平均遍历项数。</summary>
    public static string Dump()
    {
        if (_frames == 0) return "[SubtitleWndStat] 无数据（Update 没跑过）";

        double n = _frames;
        var sb = new System.Text.StringBuilder(512);
        sb.Append("[SubtitleWndStat] 采样 ").Append(_frames).Append(" 帧\\n");
        sb.Append("  总计      ").Append((_totalNs / n / 1000.0).ToString("F3")).Append(" µs/帧   累计 ")
          .Append((_totalNs / 1000000.0).ToString("F2")).Append(" ms\\n");
        sb.Append("  相机解析  ").Append((_camNs / n / 1000.0).ToString("F3")).Append(" µs/帧\\n");
        sb.Append("  家具循环  ").Append((_loopNs / n / 1000.0).ToString("F3")).Append(" µs/帧   平均遍历 ")
          .Append((_items / n).ToString("F1")).Append(" 项\\n");
        sb.Append("  其它      ").Append(((_totalNs - _camNs - _loopNs) / n / 1000.0).ToString("F3")).Append(" µs/帧\\n");
        return sb.ToString();
    }
}
#endif
'''

raw = open(PATH, 'rb').read()
bom = raw[:3] == b'\xef\xbb\xbf'
text = raw.decode('utf-8-sig')

if MARK in text:
    print('已存在 SubtitleWndStat，跳过')
    sys.exit(0)

i = text.rfind('}')          # 命名空间右花括号
text = text[:i] + BLOCK + text[i:]

out = text.encode('utf-8')
if bom:
    out = b'\xef\xbb\xbf' + out
open(PATH, 'wb').write(out)
print('inserted, new size', len(out))
