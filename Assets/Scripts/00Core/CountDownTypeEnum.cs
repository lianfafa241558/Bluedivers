
namespace FPSGame.Core
{
/// <summary>
/// 倒计时窗口样式（2026-10-01 从 `04UI/CountDownWnd.cs` 下沉到 `00_Core`）。
///
/// <para>▍为什么下沉：`TaskManager`/`WndManager`（01Manager）与 `DeathUI`（04UI）都要用它，
/// 而它原来住在 `04UI` ⇒ 管理器引用它就构成"管理器 → UI"的反向依赖，`09_Managers` 切不出来。</para>
///
/// <para>▍判据：被"管理器 + UI"共见 ⇒ 放**两者共见的最低层**＝`00_Core`。
/// **保持全局命名空间不变 ⇒ 零调用点改动**（同 `DifficultyEnum`/`BoosterType` 的下沉手法）。</para>
/// </summary>
public enum CountDownTypeEnum
{
    /// <summary>蓝方样式。</summary>
    Blue,

    /// <summary>红方样式。</summary>
    Red,
}
}
