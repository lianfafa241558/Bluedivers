using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using UnityEngine.AI;
using FPSGame.Core;
using FPSGame.Attributes;

namespace FPSGame.Utils
{
    public static partial class Tool
{

        /// <summary>
        /// 获取动画片段长度
        /// </summary>
        /// <param name="anim"></param>
        /// <param name="name"></param>
        /// <returns></returns>
        public static float GetAnimStateLenght(Animator anim, string name)
        {
            float scale = anim.speed;
            float re = 0;
            RuntimeAnimatorController controller = anim.runtimeAnimatorController;
            var states = controller.animationClips;
            for (int i = 0; i < states.Length; i++)
            {
                //Debug.LogWarning("动画的名 ?+ states[i].name);
                if (states[i].name.Contains(name))
                {
                    re += states[i].length / scale;
                }
            }

            /*
            UnityEditor.Animations.AnimatorStateMachine stateMachine = controller.layers[0].stateMachine;
            //获取层级中的状态机
            UnityEditor.Animations.ChildAnimatorState[] states = stateMachine.states;
            //遍历状态机
            for (int i = 0; i < states.Length; i++)
            {
                if (states[i].state.name.Contains(name))
                {
                    Motion motion = states[i].state.motion;
                    AnimationClip clip = motion as AnimationClip;
                    re += clip.length / scale / states[i].state.speed;
                }
            }*/

            return re;
        }

    }
}
