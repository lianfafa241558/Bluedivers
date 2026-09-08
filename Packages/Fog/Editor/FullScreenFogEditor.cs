using UnityEditor;
using UnityEditor.Rendering;
using UnityEngine;

namespace Meryuhi.Rendering
{
    [CustomEditor(typeof(FullScreenFog))]
    sealed class FullScreenFogEditor : VolumeComponentEditor
    {
        SerializedDataParameter _mode;
        SerializedDataParameter _intensity;
        SerializedDataParameter _color;
        SerializedDataParameter _densityMode;
        SerializedDataParameter _startLine;
        SerializedDataParameter _endLine;
        SerializedDataParameter _startHeight;
        SerializedDataParameter _endHeight;
        SerializedDataParameter _density;

        SerializedDataParameter _noiseMode;
        SerializedDataParameter _noiseTexture;
        SerializedDataParameter _noiseIntensity;
        SerializedDataParameter _noiseScale;
        SerializedDataParameter _noiseScrollSpeed;


        public override void OnEnable()
        {
            var o = new PropertyFetcher<FullScreenFog>(serializedObject);

            _mode = Unpack(o.Find(x => x.mode));
            _intensity = Unpack(o.Find(x => x.intensity));
            _color = Unpack(o.Find(x => x.color));
            _densityMode = Unpack(o.Find(x => x.densityMode));
            _startLine = Unpack(o.Find(x => x.startLine));
            _endLine = Unpack(o.Find(x => x.endLine));
            _startHeight = Unpack(o.Find(x => x.startHeight));
            _endHeight = Unpack(o.Find(x => x.endHeight));
            _density = Unpack(o.Find(x => x.density));

            _noiseMode = Unpack(o.Find(x => x.noiseMode));
            _noiseTexture = Unpack(o.Find(x => x.noiseTexture));
            _noiseIntensity = Unpack(o.Find(x => x.noiseIntensity));
            _noiseScale = Unpack(o.Find(x => x.noiseScale));
            _noiseScrollSpeed = Unpack(o.Find(x => x.noiseScrollSpeed));
        }

        public override void OnInspectorGUI()
        {
            var mode = (FullScreenFogMode)_mode.value.intValue;
            PropertyField(_mode, new GUIContent("模式"));

            PropertyField(_intensity, new GUIContent("强度"));

            PropertyField(_color, new GUIContent("雾颜色"));

            var densityMode = (FullScreenFogDensityMode)_densityMode.value.intValue;
            PropertyField(_densityMode, new GUIContent("衰减模式"));

            if (FullScreenFog.UseStartLine(mode))
            {
                PropertyField(_startLine, new GUIContent("距离雾起点"));
            }
            if (FullScreenFog.UseEndLine(mode, densityMode))
            {
                PropertyField(_endLine, new GUIContent("距离雾终点"));
            }
            if (FullScreenFog.UseStartHeight(mode) || FullScreenFog.UseHeightParams(mode))
            {
                PropertyField(_startHeight, new GUIContent("高度雾起点"));
            }
            if (FullScreenFog.UseEndHeight(mode, densityMode) || FullScreenFog.UseHeightParams(mode))
            {
                PropertyField(_endHeight, new GUIContent("高度雾终点"));
            }
            if (FullScreenFog.UseIntensity(densityMode))
            {
                PropertyField(_density, new GUIContent("衰减系数"));
            }

            var noiseMode = (FullScreenFogNoiseMode)_noiseMode.value.intValue;
            PropertyField(_noiseMode, new GUIContent("噪声模式"));

            if (FullScreenFog.UseNoiseTex(noiseMode))
            {
                PropertyField(_noiseTexture, new GUIContent("噪声贴图"));
            }
            if (FullScreenFog.UseNoiseIntensity(noiseMode))
            {
                PropertyField(_noiseIntensity, new GUIContent("噪声强度"));
                PropertyField(_noiseScale, new GUIContent("噪声缩放"));
                PropertyField(_noiseScrollSpeed, new GUIContent("噪声滚动速度"));
            }
        }
    }
}
