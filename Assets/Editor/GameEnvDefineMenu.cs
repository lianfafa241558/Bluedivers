namespace FPSGame.EditorExt
{
#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

/// <summary>
/// 测试环境开关：在 Scripting Define Symbols 里一键增删 <c>TEST_ENV</c>。
///
/// <para>▍为什么改宏而不是改静态变量：购买判定是编译期的 <c>#if TEST_ENV</c>
/// （<c>SelectRoleWnd.HasBuy</c> / <c>ArmamentWnd.IsOwned</c>），所以"切环境"只能落在宏上。
/// 改完 Unity 自动重编 ⇒ Play 模式会被踢出来，切环境请在非播放状态下点。</para>
/// <para>⚠ 只改**当前选中的 Build Target Group**（Standalone / Android 各有一套宏），换平台后要再点一次。</para>
/// </summary>
public static class GameEnvDefineMenu
{
    private const string Define = "TEST_ENV";
    private const string MenuPath = "Tools/环境/测试环境（全部已购买）";

    [MenuItem(MenuPath, false, 1)]
    private static void Toggle()
    {
        var target = CurrentTarget;
        PlayerSettings.GetScriptingDefineSymbols(target, out string[] defines);

        var list = new List<string>(defines);
        bool had = list.Contains(Define);
        if (had) list.Remove(Define);
        else list.Add(Define);

        PlayerSettings.SetScriptingDefineSymbols(target, list.ToArray());
        Debug.Log($"[环境] 已{(had ? "关闭" : "开启")}测试环境：{target.TargetName} 的宏 {Define} {(had ? "已移除" : "已加上")}（Unity 会重编）");
    }

    [MenuItem(MenuPath, true)]
    private static bool ToggleValidate()
    {
        var target = CurrentTarget;
        PlayerSettings.GetScriptingDefineSymbols(target, out string[] defines);
        Menu.SetChecked(MenuPath, System.Array.IndexOf(defines, Define) >= 0);
        return true;
    }

    /// <summary>当前 Build Target Group；编辑器停在 "No Target" 时按 Standalone 算（否则 API 会抛）。</summary>
    private static NamedBuildTarget CurrentTarget
    {
        get
        {
            var group = EditorUserBuildSettings.selectedBuildTargetGroup;
            if (group == BuildTargetGroup.Unknown) group = BuildTargetGroup.Standalone;
            return NamedBuildTarget.FromBuildTargetGroup(group);
        }
    }
}
#endif
}
