using System;
using System.Collections.Generic;
using PEMaths;
using System.Linq;

using FPSGame.Core;
using FPSGame.Core.Interface;
using FPSGame.GameContract;
using UnityEngine;
using FPSGame.Utils;

namespace FPSGame.Game
{

public class UnitQueryGrid
{
    public UnitQueryGridNode[,] nodes;
    public PERect rect;
    public int cellSize;
    public int xCount;
    public int yCount;

    public ObjectPool<List<UnitQueryGridNode>> nodePool;


    #region 生命周期
    public UnitQueryGrid(PERect rect, PEInt cellSize)
    {
        this.rect = rect;
        this.cellSize = cellSize.RawInt;

        xCount = PEMath.Ceil(rect.width / cellSize).RawInt;
        yCount = PEMath.Ceil(rect.height / cellSize).RawInt;
        nodes = new UnitQueryGridNode[xCount, yCount];

        nodePool = new(()=>new(),(item)=>item.Clear(),10);

        GenerateGrid();
    }
    /// <summary>格子 rect 的装箱缓存：<c>IPERange.Overlaps(IPERange)</c> 只吃接口，不复用的话
    /// 每次范围查询都要把 <c>PERect</c> 结构体装箱一次（实测每单位每帧 +1 次 24B 分配）。</summary>
    private IPERange[,] _nodeRanges;

    private void GenerateGrid()
    {
        _nodeRanges = new IPERange[xCount, yCount];
        for (int i = 0; i < xCount; i++)
        {
            for (int j = 0; j < yCount; j++)
            {
                PEInt x = i * cellSize + rect.x;
                PEInt y = j * cellSize + rect.y;
                nodes[i, j] = new UnitQueryGridNode(new PERect(x, y, cellSize, cellSize), i, j);
                _nodeRanges[i, j] = nodes[i, j].rect;   // 只在这里装箱一次
            }
        }
    }

    public void AddUnit(IActor unit)
    {
        List<UnitQueryGridNode> nodes = GetOverlapsNodes(unit.Range,false);
        foreach (var node in nodes)
        {
            AddUnit(node,unit);
            unit.GridNodes.Add(node);
        }
        nodePool.Release(nodes);
    }

    public void RemoveUnit(IActor unit)
    {
        foreach (var node in unit.GridNodes)
        {
            RemoveUnit(node,unit);
        }
        unit.GridNodes.Clear();
    }

    public void UpdateNodes(IActor unit)
    {
        var newNodes = GetOverlapsNodes(unit.Range,false);
        //比对顺序和数量，都一样才返回true
        // ⚠ 别用 SequenceEqual：参数是 IEnumerable ⇒ 两个 List 的枚举器结构体各装箱一次；
        //   而 UnitQueryGridNode 是结构体，默认比较器还要走反射式 Equals。这段每移动单位每帧都跑。
        //   节点顺序由 GetOverlapsNodes 的双层循环固定生成 ⇒ 比 x/y 序列等价于比"同一批格子"。
        List<UnitQueryGridNode> oldNodes = unit.GridNodes;
        bool same = newNodes.Count == oldNodes.Count;
        for (int i = 0; same && i < newNodes.Count; ++i)
        {
            if (newNodes[i].x != oldNodes[i].x || newNodes[i].y != oldNodes[i].y) same = false;
        }
        if (!same)
        {
            RemoveUnit(unit);
            AddUnit(unit);
        }
        nodePool.Release(newNodes);
    }


    #endregion

    #region 搜索
    public UnitQueryGridNode GetNodeByPos(PEVector2 pos)
    {
        int i = PEMath.Floor((pos.x - rect.x) / cellSize).RawInt;
        int j = PEMath.Floor((pos.y - rect.y) / cellSize).RawInt;
        if (i < 0 || i > xCount - 1 || j < 0 || j > yCount - 1)
        {
            return default;
        }
        return nodes[i, j];

    }

    /// <summary>
    /// 获取范围内所有满足目标配置的单位
    /// </summary>
    public HashSet<IActor> QueryUnits(IPERange range, TargetCfg targetCfg, Func<IActor, bool> customFilter)
    {

        List<IActor> units = GetOverlapsUnits(range, targetCfg);

        //去重以及二次筛选出与range相交的单位
        HashSet<IActor> unitsInRange = new HashSet<IActor>();
        IActor unit;
        for (int i=0,l= units.Count; i < l; ++i)
        {
            unit = units[i];

            //if (customFilter.IsValid())
            //{
            //    Debug.LogError(unit.ShowName+"单位有效"+ unit.IsValidMono());
            //    Debug.LogError(unit.ShowName + "状态有效" + unit.ActorState.HasFlag(targetCfg.actorState));
            //    Debug.LogError(unit.ShowName + "范围有效" + range.Overlaps(unit.Range));
            //    Debug.LogError(unit.ShowName + "过滤有效" + customFilter.Invoke(unit));

            //}
            if (unit.IsValidMono() && unit.ActorState.HasFlag(targetCfg.actorState)
                && range.Overlaps(unit.Range)
                && (!customFilter.IsValid() || customFilter.Invoke(unit)))
            {
                unitsInRange.Add(unit);
            }
        }
        return unitsInRange;
    }

    // 查询缓冲：查询在主线程串行调用，结果必须在下次查询前用完（同一帧内别嵌套两次范围查询）。
    private readonly List<IActor> _overlapsUnits = new();
    private readonly HashSet<IActor> _unitsInRange = new();

    /// <summary>
    /// 零分配版 <see cref="QueryUnits(IPERange, TargetCfg, Func{IActor, bool})"/>：结果写进调用方的
    /// <paramref name="result"/>（会先 Clear）。
    /// <para>▍为什么要有：老接口每次调用都要 new List + new HashSet，且每个节点再 new 一个 List；
    /// 高频调用点（地雷触发判定 88 颗/帧）实测每帧 100KB+ 的 GC 全在这条链上。</para>
    /// </summary>
    public void QueryUnits(IPERange range, TargetCfg targetCfg, Func<IActor, bool> customFilter, List<IActor> result)
    {
        if (result == null) return;
        result.Clear();

        GatherOverlaps(range, targetCfg, _overlapsUnits);

        _unitsInRange.Clear();
        for (int i = 0, l = _overlapsUnits.Count; i < l; ++i)
        {
            IActor unit = _overlapsUnits[i];
            // 位运算等价于 Enum.HasFlag（定义就是 (a & b) == b），但不会装箱；ActorState 不是 [Flags] 也照样成立
            if (unit.IsValidMono() && (unit.ActorState & targetCfg.actorState) == targetCfg.actorState
                && range.Overlaps(unit.Range)
                && (!customFilter.IsValid() || customFilter.Invoke(unit)))
            {
                _unitsInRange.Add(unit);
            }
        }

        result.AddRange(_unitsInRange);
    }

    /// <summary>零分配版 <see cref="QueryUnits(TargetCfg, Func{IActor, bool})"/>（不限范围）：结果写进
    /// <paramref name="result"/>（会先 Clear）。单位会横跨多个节点 ⇒ 内部照旧去重。</summary>
    public void QueryUnits(TargetCfg targetCfg, Func<IActor, bool> customFilter, List<IActor> result)
    {
        if (result == null) return;
        result.Clear();

        _unitsInRange.Clear();
        for (int i = 0, ix = nodes.GetLength(0); i < ix; ++i)
        {
            for (int j = 0, iy = nodes.GetLength(1); j < iy; ++j)
            {
                UnitQueryGridNode node = nodes[i, j];
                if (node.units == null || node.units.Count == 0) continue;

                _overlapsUnits.Clear();
                AddUnits(node, targetCfg, _overlapsUnits);
                for (int k = 0, kl = _overlapsUnits.Count; k < kl; ++k)
                {
                    IActor unit = _overlapsUnits[k];
                    if (unit.IsValidMono() && unit.ActorState != ActorState.Dead
                        && (customFilter == null || customFilter.Invoke(unit)))   // 这里本来就是 != 比较，不装箱
                    {
                        _unitsInRange.Add(unit);
                    }
                }
            }
        }

        result.AddRange(_unitsInRange);
    }

    /// <summary>把"与 range 相交的节点里的单位"（可能重复）收集进缓冲，不再为每个节点新建 List。</summary>
    private void GatherOverlaps(IPERange range, TargetCfg targetCfg, List<IActor> result)
    {
        result.Clear();

        List<UnitQueryGridNode> overlapsNodes = GetOverlapsNodes(range, true);
        for (int i = 0, l = overlapsNodes.Count; i < l; ++i)
        {
            AddUnits(overlapsNodes[i], targetCfg, result);
        }
        nodePool.Release(overlapsNodes);
    }


    /// <summary>
    /// 获取全图所有满足目标配置的单位
    /// </summary>
    public HashSet<IActor> QueryUnits(TargetCfg targetCfg, Func<IActor, bool> customFilter)
    {

        List<IActor> units = GetUnits(targetCfg);
        //去重以及二次筛选
        HashSet<IActor> filteredUnits = new HashSet<IActor>();
        foreach (var unit in units)
        {
            if (unit.IsValidMono() && unit.ActorState != ActorState.Dead
                && (customFilter==null || customFilter.Invoke(unit)))
            {
                filteredUnits.Add(unit);
            }
        }
        return filteredUnits;
    }


    public List<IActor> GetUnits(TargetCfg targetCfg)
    {
        List<IActor> units = new List<IActor>();
        foreach (var node in nodes)
        {
            units.AddRange(GetUnits(node,targetCfg));
        }
        return units;
    }

    /// <summary>
    /// 与（与range相交的节点）相交的单位，可能重复
    /// </summary>
    /// <param name="range"></param>
    /// <param name="targetCfg"></param>
    /// <returns></returns>
    /// 
    public List<IActor> GetOverlapsUnits(IPERange range, TargetCfg targetCfg)
    {
        List<IActor> overlapsUnits = new List<IActor>();
        List<UnitQueryGridNode> overlapsNodes = GetOverlapsNodes(range,true);
        foreach (var node in overlapsNodes)
        {
            //无法避免的GC
            overlapsUnits.AddRange(GetUnits(node,targetCfg));
        }
        nodePool.Release(overlapsNodes);
        return overlapsUnits;
    }

    /// <summary>
    /// 获取与范围相交的节点
    /// </summary>
    /// <param name="range"></param>
    /// <returns></returns>
    public List<UnitQueryGridNode> GetOverlapsNodes(IPERange range,bool ignoreEmpty)
    {
        var overlapsNodes = nodePool.Get();//直接从池里获得，降低压力
        PEInt halfWidth = range.GetHalfWidth();
        PEInt halfHeight = range.GetHalfHeight();
        
        UnitQueryGridNode centerNode = GetNodeByPos(range.GetXY());
        if (centerNode.IsVaild())
        {
            int widthOffset = PEMath.Ceil(halfWidth / cellSize).RawInt;
            int heightOffset = PEMath.Ceil(halfHeight / cellSize).RawInt;

            // 预计算边界
            int minX = Math.Max(0, centerNode.x - widthOffset);
            int maxX = Math.Min(xCount - 1, centerNode.x + widthOffset);
            int minY = Math.Max(0, centerNode.y - heightOffset);
            int maxY = Math.Min(yCount - 1, centerNode.y + heightOffset);

            for (int i = minX; i <= maxX; ++i)
            {
                for (int j = minY; j <= maxY; ++j)
                {
                    // range/rect 都是值类型转接口：rect 走 _nodeRanges 缓存（见 GenerateGrid），别再直接传 nodes[i,j].rect
                    if ((ignoreEmpty && nodes[i, j].units.Count == 0)||!range.Overlaps(_nodeRanges[i, j]))
                    {
                        continue;
                    }
                    overlapsNodes.Add(nodes[i, j]);
                }
            }
        }

        return overlapsNodes;
    }




    public List<IActor> GetUnits(UnitQueryGridNode node, TargetCfg targetCfg)
    {
        List<IActor> reslut = new();
        AddUnits(node, targetCfg, reslut);
        return reslut;
    }

    /// <summary>把某节点里符合目标类型的单位追加到 <paramref name="result"/>（类型过滤只此一处，
    /// 老接口 <see cref="GetUnits(UnitQueryGridNode, TargetCfg)"/> 与零分配版共用）。</summary>
    private static void AddUnits(UnitQueryGridNode node, TargetCfg targetCfg, List<IActor> result)
    {
        UnitTypeEnum targetType = targetCfg.targetType;
        // Dictionary 的 foreach 走结构体枚举器，不分配
        foreach (var keyValue in node.units)
        {
            if ((targetType & keyValue.Key) != 0)
            {
                result.AddRange(keyValue.Value);
            }
        }
    }

    public void AddUnit(UnitQueryGridNode node, IActor unit)
    {
        //int teamID = unit.Team;
        UnitTypeEnum unitType = unit.Type;
        if (!node.units.ContainsKey(unitType))
        {
            node.units[unitType] = new List<IActor>();
        }
        node.units[unitType].Add(unit);

    }

    public void RemoveUnit(UnitQueryGridNode node, IActor unit)
    {
        //int teamID = unit.Team;
        UnitTypeEnum unitType = unit.Type;
        node.units[unitType].Remove(unit);

        //清除空列表
        if (node.units[unitType].Count == 0)
        {
            node.units.Remove(unitType);
        }
    }

    #endregion

}



}
