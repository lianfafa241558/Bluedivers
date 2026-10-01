using UnityEngine;

namespace FPSGame.GameContract
{

    public struct WaveCreateParams
    {
        public Vector3 center;
        public Vector3[] points;

        public bool extraWave;
        public float range;
        public float scale;
        public bool tip;

        /// <summary>波次结束(所有单位清空)时的回调，用于续航/续刷</summary>
        public System.Action onEnd;

        /// <summary>持续跟踪的中心点(如玩家位置)；不为 null 时波次每 Tick 用它刷新 center，实现移动追击</summary>
        public System.Func<Vector3> centerGetter;

        public static WaveCreateParams Default => new WaveCreateParams {
            extraWave = false,
            range = 35,
            scale = 1,
            tip = true
        };

        public static WaveCreateParams Extra => new WaveCreateParams {
            extraWave = true,
            range = 35,
            scale = 0.5f,
            tip = true
        };

        public static WaveCreateParams Defensive => new WaveCreateParams {
            extraWave = true,
            range = 5,
            scale = 1,
            tip = true,
        };

        public static WaveCreateParams Evacuate => new WaveCreateParams {
            extraWave = true,
            range = 10,
            scale = 0.35f,
            tip = false,
        };
    }
    public static class WaveUtil
    {
        public static WaveCreateParams Set(this WaveCreateParams para, Vector3 center)
        {
            para.center = center;
            return para;
        }
        public static WaveCreateParams Set(this WaveCreateParams para, Vector3 center,Vector3[] points)
        {
            para.center = center;
            para.points = points;
            return para;
        }
        public static WaveCreateParams Scale(this WaveCreateParams para, float scale)
        {
            para.scale = scale;
            return para;
        }
    }
}
