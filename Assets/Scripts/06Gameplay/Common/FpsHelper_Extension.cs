using System.Collections.Generic;
using System.Linq;
using FPSGame.Core;
using FPSGame.Core.Interface;
using FPSGame.MapUtils;
using FPSGame.GameContract;
using PEMaths;

using FPSGame.Game;
using UnityEngine;
using UnityEngine.AI;
using FPSGame.Utils;
using FPSGame.Data;

namespace FPSGame.Gameplay
{
public static partial class FpsHelper
{




    private static readonly string[] opterTmp = new string[] { "<sprite=2", "<sprite=0", "<sprite=1", "<sprite=3" };
    public static string OpterTMPString(this IEnumerable<DirectionEnum> opter)
    {
        return string.Join(">", opter.Select(item => opterTmp[(int)item])) + ">";
    }

    public static string OpterColorString(this IEnumerable<DirectionEnum> opter, int index, Color less, Color equal, Color greater)
    {
        return opter.Select((item, i) => {
            var color = i < index ? less : (i == index ? equal : greater);
            return opterTmp[(int)item] + " color=#" + ColorUtility.ToHtmlStringRGBA(color) + ">";
        }).Aggregate("", (current, next) => current + next);
        //Aggregate是累积计算
    }
    public static bool Compare(this IEnumerable<DirectionEnum> target, IEnumerable<DirectionEnum> input)
    {
        if (!input.Any()) return false;//判断非空
        return input.Zip(target, (i, t) => i == t).All(b => b);
        //zip生成一个list<Bool>，all检查出现false就返回false
    }


    public static EnemyType ToEnemyType(this EnemyVarietyType variety)
    {
        if (Tool.In(variety, EnemyVarietyType.KaiserBase - 1, EnemyVarietyType.Placeholder1 + 1)) return EnemyType.Kaiser;
        if (Tool.In(variety, EnemyVarietyType.Decagrammaton - 1, EnemyVarietyType.Placeholder3 + 1)) return EnemyType.Decagrammaton;
        if (Tool.In(variety, EnemyVarietyType.Colour - 1, EnemyVarietyType.Placeholder6 + 1)) return EnemyType.Colour;
        return EnemyType.Kaiser;
    }


    public static PEInt DiffDamageScale()
    {
        // 走任务服务契约：TaskManager 在 01Manager，而本文件未来要随玩法层进 asmdef ⇒ 不能直连（见 Interface_Manager.cs）
        ITaskService taskSvc = ServiceLocator.Task;
        PEInt scale = 1 + ((taskSvc != null ? taskSvc.ExtraDifficulty[0] : 0) * (PEInt)0.15f);
        switch (taskSvc != null ? taskSvc.Difficulty : DifficultyEnum.Normal)
        {
            case DifficultyEnum.Normal:
                scale *= (PEInt)0.6f;
                break;
            case DifficultyEnum.Hard:
                scale *= (PEInt)0.75f;
                break;
            case DifficultyEnum.VeryHard:
                scale *= (PEInt)0.9f;
                break;
            case DifficultyEnum.HardCode:
                break;
            case DifficultyEnum.Extreme:
                scale *= (PEInt)1.5f;
                break;
            case DifficultyEnum.Insane:
                scale *= (PEInt)2;
                break;
            case DifficultyEnum.Torment:
                scale *= (PEInt)2.5f;
                break;
            case DifficultyEnum.Lunatic:
                scale *= (PEInt)3;
                break;
        }
        return scale;
    }
}
}
