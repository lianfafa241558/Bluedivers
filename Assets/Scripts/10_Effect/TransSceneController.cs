using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using FPSGame.Utils;
using FPSGame.Managers;
using FPSGame.Audio;

namespace FPSGame.Effect
{

    /// <summary>
    /// 场景切换时的转场表现。
    /// </summary>
    [AddComponentMenu("特效/转场控制")]
    public class TransSceneController : MonoBehaviour
    {
        /// <summary>
        /// 事件调用
        /// </summary>
        public void StartLoad()
        {
        
            AudioSvc.PlayMusic("Shooting Athletes", 0.3f);
            // 场景名走公共常量（房主与成员必须加载同一张图 ⇒ 不能各自硬编码字符串）
            ResSvc.Instance.AsyncLoadScene(FPSGame.Core.Constants.BattleSceneName, () => {
                BattleManager.Creat(true);
            }, true,false);

            transform.SetParent(null);
            DontDestroyOnLoad(gameObject);
            transform.position = Vector3.up * 500;

        }
        /// <summary>
        /// 动画调用（`EndLoad` 片段第 2 秒的动画事件）
        /// <para>⚠ 本组件的宿主就是**船本体**（`MapRoot/Utnapishitim`，19 个子物体都在它下面）：
        /// 只有 <see cref="StartLoad"/> 把船 SetParent(null) + DontDestroyOnLoad **带进战斗场景**之后，
        /// 这一发销毁才是"转场收尾"。没被带走的场合是误触发 —— 例如从教程直接回大厅时
        /// `Transition→Bridge` 发生在刚加载好的船里 ⇒ 会把整条船删掉（2026-10-26 实测）。</para>
        /// </summary>
        public void Destroy()
        {
            if (gameObject.scene.name != "DontDestroyOnLoad")
            {
                Debug.LogWarning("[转场] Destroy 被误触发：宿主没被 StartLoad 带进 DontDestroyOnLoad，销毁会把场景物体一起删掉 ⇒ 已忽略", this);
                return;
            }
            Tool.Destroy(gameObject);
        }

        /// <summary>
        /// 动画调用
        /// </summary>
        private void AllowSwitchSceen()
        {
            ResSvc.Instance.AsyncContinueLoadScene();
        }

    }
}
