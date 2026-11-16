using FPSGame.Game;
using UnityEngine;

namespace FPSGame.Gameplay
{
    /// <summary>
    /// 当前**真正出图**的玩法相机（同一时刻只有一台）。
    ///
    /// <para>▍为什么不用 <c>Camera.main</c>：它按 tag 挑"第一台启用的相机"，主相机 / 倒地观察相机 /
    /// 自由相机 / 武器相机并存时挑到谁不确定 —— 本工程实测出现过它指向俯视观察相机的会话
    /// （主相机的 GameObject 被整台关掉时），于是所有按相机位置算的距离剔除都用了错的参照点。</para>
    ///
    /// <para>▍只认一个来源：本机玩家 <see cref="PlayerController.ActiveCamera"/>。没有玩家（大厅 / 编辑器）
    /// 返回 null —— 调用方各自退回"玩家位置 / 自身位置"，**不要**再去猜一台相机。</para>
    /// </summary>
    public static class GameplayCamera
    {
        private static Camera _cached;
        private static PlayerController _owner;

        /// <summary>当前出图的相机；没有玩家、或玩家相机暂时不在（上车 / 死亡切换中）时为 null。</summary>
        public static Camera Current
        {
            get
            {
                // 缓存只在"这台还在出图"时有效：倒地换观察相机、复活换回来，两种切换都会让它失效
                if (_cached != null && _cached.isActiveAndEnabled) return _cached;

                // ActorsManager.Player 是 IActor（接口引用不吃 Unity 假 null）⇒ 必须借 Component 判一次
                var actor = ActorsManager.Player as Component;
                if (actor == null)
                {
                    _owner = null;
                    _cached = null;
                    return null;
                }

                // 玩家被销毁（换场景）时 _owner 会变成假 null ⇒ 这里自动重新取；换成另一个玩家也重取
                if (_owner == null || _owner.gameObject != actor.gameObject)
                    _owner = actor.GetComponent<PlayerController>();

                _cached = _owner != null ? _owner.ActiveCamera : null;
                return _cached != null && _cached.isActiveAndEnabled ? _cached : null;
            }
        }
    }
}
