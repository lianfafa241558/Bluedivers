using System;
using FPSGame.Core.Interface;
using UnityEngine;

namespace FPSGame.Furn
{
    [Flags]
    public enum FurnitureFlag
    {
        /// <summary>自动操作</summary>
        [InspectorName("自动操作")] AutoOperate = 1 << 0,
        /// <summary>切换状态</summary>
        [InspectorName("切换状态")] SwitchState = 1 << 1,
        /// <summary>瞬间完成操作</summary>
        [InspectorName("瞬间完成操作")] Immediately = 1 << 2,
        /// <summary>一次性</summary>
        [InspectorName("一次性")] Disposable = 1 << 3,
        /// <summary>(完成操作时播放动画</summary>
        [InspectorName("播放动画")] PlayAnim = 1 << 4,
        /// <summary>喊话</summary>
        [InspectorName("喊话")] Speech = 1 << 5,
        /// <summary>离开保留进度</summary>
        [InspectorName("保留进度")] KeepPress = 1 << 6,
        /// <summary>长按时控制动画</summary>
        [InspectorName("长按时控制动画")] ControlAnim = 1 << 7,
        /// <summary>任意角度</summary>
        [InspectorName("任意角度")] AnyAngle = 1 << 8,
    }

    public interface IFurniture:IMonoVaild
    {
        float Press { get; set; }

        float MeetTime { get; }
        bool InOperate { get; }
        AudioClip AudioPress { get; }
        string ShowName { get; }
        string Id { get; }
        int NumberID { get; }
        string Desc { get; 
        }
        Sprite Portrait { get; }
        Vector3 CenterPos { get; }
        Vector3 Forward { get; }

        GameObject gameObject { get; }

        event Action OnOperate;

        void Operate();
        bool CanOperate(GameObject unit);
        bool Handle(GameObject user);

        bool HaveFlag(FurnitureFlag flag);
    }
}
