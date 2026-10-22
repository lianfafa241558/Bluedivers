
using UnityEngine;
using FPSGame.Gameplay;
using FPSGame.GameContract;
using FPSGame.Core.Interface;   // IsValidMono（IActor 是接口引用，不能用 == null 判活）

namespace FPSGame.Effect
{

/// <summary>
/// 把装备的瞄准点摆到**装备所有者**的准心上（模型上的 LookTarget 再朝它 LookAt）。
/// </summary>
[AddComponentMenu("特效/相机朝中心")]
internal class CameraLookCenter : MonoBehaviour
{
    /// <summary>装备所有者（装到谁身上就是谁）。惰性解析一次后缓存 —— 父链由 <c>BagBase.OnInstall</c> 建立。</summary>
    private IActor _owner;

    private void LateUpdate()
    {
        // ⚠ 不能无条件读 FpsHelper.PlayerCameraLookPoint（那个静态量的语义是"**本机玩家**的准心"）：
        //   装备装在盟友身上时（联机重放装上的那份）会把模型拽向"我的"方向（2026-10-22 用户报）。
        //   TryGetAimPoint 自己分流：本机玩家 = 相机屏幕中心；盟友 = 已同步的 yaw/pitch 本地重算。
        if (!_owner.IsValidMono()) _owner = GetComponentInParent<IActor>();

        if (_owner.IsValidMono() && FpsHelper.TryGetAimPoint(_owner.gameObject, out Vector3 point))
        {
            transform.position = point;
            return;
        }

        // 还没装上（空投箱躺在场上，父链里没有 IActor）⇒ 保持原口径
        transform.position = FpsHelper.PlayerCameraLookPoint;
    }
}
}
