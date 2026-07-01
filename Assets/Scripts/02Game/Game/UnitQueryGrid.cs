using System;
using System.Collections.Generic;
using PEMaths;
using System.Linq;

using Core;
using GameContract;

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
    private void GenerateGrid()
    {
        for (int i = 0; i < xCount; i++)
        {
            for (int j = 0; j < yCount; j++)
            {
                PEInt x = i * cellSize + rect.x;
                PEInt y = j * cellSize + rect.y;
                nodes[i, j] = new UnitQueryGridNode(new PERect(x, y, cellSize, cellSize), i, j);
            }
        }
    }

    public void AddUnit(I_Actor unit)
    {
        List<UnitQueryGridNode> nodes = GetOverlapsNodes(unit.Range,false);
        foreach (var node in nodes)
        {
            AddUnit(node,unit);
            unit.GridNodes.Add(node);
        }
        nodePool.Release(nodes);
    }

    public void RemoveUnit(I_Actor unit)
    {
        foreach (var node in unit.GridNodes)
        {
            RemoveUnit(node,unit);
        }
        unit.GridNodes.Clear();
    }

    public void UpdateNodes(I_Actor unit)
    {
        var newNodes = GetOverlapsNodes(unit.Range,false);
        //比对顺序和数量，都一样才返回true
        if (!newNodes.SequenceEqual(unit.GridNodes))
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
    public HashSet<I_Actor> QueryUnits(IPERange range, TargetCfg targetCfg, Func<I_Actor, bool> customFilter)
    {

        List<I_Actor> units = GetOverlapsUnits(range, targetCfg);

        //去重以及二次筛选出与range相交的单位
        HashSet<I_Actor> unitsInRange = new HashSet<I_Actor>();
        I_Actor unit;
        for (int i=0,l= units.Count; i < l; ++i)
        {
            unit = units[i];
            if (unit.IsValid() && unit.ActorState.HasFlag(targetCfg.actorState)
                && range.Overlaps(unit.Range)
                && (!customFilter.IsValid() || customFilter.Invoke(unit)))
            {
                unitsInRange.Add(unit);
            }
        }
        return unitsInRange;
    }


    /// <summary>
    /// 获取全图所有满足目标配置的单位
    /// </summary>
    public HashSet<I_Actor> QueryUnits(TargetCfg targetCfg, Func<I_Actor, bool> customFilter)
    {

        List<I_Actor> units = GetUnits(targetCfg);
        //去重以及二次筛选
        HashSet<I_Actor> filteredUnits = new HashSet<I_Actor>();
        foreach (var unit in units)
        {
            if (unit.IsValid() && unit.ActorState != ActorState.Dead
                && (customFilter==null || customFilter.Invoke(unit)))
            {
                filteredUnits.Add(unit);
            }
        }
        return filteredUnits;
    }


    public List<I_Actor> GetUnits(TargetCfg targetCfg)
    {
        List<I_Actor> units = new List<I_Actor>();
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
    public List<I_Actor> GetOverlapsUnits(IPERange range, TargetCfg targetCfg)
    {
        List<I_Actor> overlapsUnits = new List<I_Actor>();
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
                    //range是值类型的接口，所以会产生gc，没办法
                    if ((ignoreEmpty && nodes[i, j].units.Count == 0)||!range.Overlaps(nodes[i, j].rect))
                    {
                        continue;
                    }
                    overlapsNodes.Add(nodes[i, j]);
                }
            }
        }

        return overlapsNodes;
    }




    public List<I_Actor> GetUnits(UnitQueryGridNode node, TargetCfg targetCfg)
    {
        UnitTypeEnum targetType = targetCfg.targetType;
        List<I_Actor> reslut = new();
        foreach (var keyValue in node.units)
        {
            if ((targetType & keyValue.Key) != 0
            )
            {
                reslut.AddRange(keyValue.Value);
            }
        }
        return reslut;
    }

    public void AddUnit(UnitQueryGridNode node, I_Actor unit)
    {
        //int teamID = unit.Team;
        UnitTypeEnum unitType = unit.Type;
        if (!node.units.ContainsKey(unitType))
        {
            node.units[unitType] = new List<I_Actor>();
        }
        node.units[unitType].Add(unit);

    }

    public void RemoveUnit(UnitQueryGridNode node, I_Actor unit)
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


[System.Serializable]
public class TargetCfg
{
    public UnitTypeEnum targetType= UnitTypeEnum.All;//可以是多类目
    public ActorState actorState = ActorState.Normal;//可以是多类目
    //public float selectRange=-1;

    public static TargetCfg EnemyAI = new() {targetType = UnitTypeEnum.All & ~UnitTypeEnum.Other };
    public static TargetCfg Enemy = new() { targetType = UnitTypeEnum.Enemy };
}

