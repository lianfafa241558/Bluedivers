using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Rendering;
using UnityEngine;
using UnityEngine.Rendering;

namespace Unity.FPS.EditorExt
{
    /// <summary>
    /// 它实现了 Unity 的 IPreprocessShaders 接口，在构建阶段（Shader 编译前）对每个 Shader 的编译变体做一次预筛选，把用不到的变体从构建里剔除掉。
    ///Shader 里有大量 #pragma multi_compile / #pragma shader_feature 关键字，组合后会生成成百上千个变体（variant），每个都要编译和打包，非常占体积。
    ///该脚本维护了一个"要被剔除的关键字黑名单"（m_ExcludedKeywords），凡是启用了其中任意关键字的变体，就直接从 shaderCompilerData 列表里 RemoveAt 移除，不参与编译。
    ///关键点
    ///受宏 MANUAL_SHADER_STRIPPING 控制：所有核心逻辑都包在 #if MANUAL_SHADER_STRIPPING 里。也就是说，只有当你（或构建脚本）定义了 MANUAL_SHADER_STRIPPING 这个脚本宏时，裁剪逻辑才会生效。否则这个类实际什么都不做。所以它现在大概率是"空转"的。

    ///开发构建不裁剪：OnProcessShader 里先判断 EditorUserBuildSettings.development，开发构建直接 return，只在 release 构建下裁剪。
    ///裁剪范围：剔除的关键字包括调试/可视化类（DEBUG、EDITOR_VISUALIZATION）、用不到的渲染特性（SOFTPARTICLES_ON、PIXELSNAP_ON、DYNAMICLIGHTMAP_ON 等）、以及平台相关（SHADER_API_D3D11/SHADER_API_VULKAN）等。
    /// </summary>
    // Simple example of stripping of a debug build configuration
    class ShaderBuildStripping : IPreprocessShaders
    {
        List<ShaderKeyword> m_ExcludedKeywords;

        public ShaderBuildStripping()
        {
#if MANUAL_SHADER_STRIPPING
            m_ExcludedKeywords = new List<ShaderKeyword>
            {
                new ShaderKeyword("DEBUG"),
                // ifdef
                new ShaderKeyword("UNITY_GATHER_SUPPORTED"),
                new ShaderKeyword("UNITY_POSTFX_SSR"),
                new ShaderKeyword("DISTORT"),
                new ShaderKeyword("BLUR_HIGH_QUALITY"),
                new ShaderKeyword("UNITY_CAN_COMPILE_TESSELLATION"),
                new ShaderKeyword("ENABLE_WIND"),
                new ShaderKeyword("WIND_EFFECT_FROND_RIPPLE_ADJUST_LIGHTING"),
                new ShaderKeyword("LOD_FADE_CROSSFADE"),
                new ShaderKeyword("DYNAMICLIGHTMAP_ON"),
                new ShaderKeyword("EDITOR_VISUALIZATION"),
                new ShaderKeyword("UNITY_INSTANCING_ENABLED"),
                new ShaderKeyword("STEREO_MULTIVIEW_ON"),
                new ShaderKeyword("STEREO_INSTANCING_ON"),
                new ShaderKeyword("SOFTPARTICLES_ON"),
                new ShaderKeyword("PIXELSNAP_ON"),
                new ShaderKeyword("SHADER_API_D3D11"),
                // if defined()
                new ShaderKeyword("SHADER_API_VULKAN"),
                new ShaderKeyword("UNITY_SINGLE_PASS_STEREO"),
                new ShaderKeyword("FOG_LINEAR"),
                new ShaderKeyword("FOG_EXP"),
                new ShaderKeyword("FOG_EXP2"),
                new ShaderKeyword("UNITY_PASS_DEFERRED"),
                new ShaderKeyword("LIGHTMAP_ON"),
                new ShaderKeyword("_PARALLAXMAP"),
                new ShaderKeyword("SHADOWS_SCREEN"),
            };
#endif
        }

        // Multiple callback may be implemented. 
        // The first one executed is the one where callbackOrder is returning the smallest number.
        public int callbackOrder
        {
            get { return 0; }
        }

        public void OnProcessShader(
            Shader shader, ShaderSnippetData snippet, IList<ShaderCompilerData> shaderCompilerData)
        {
#if MANUAL_SHADER_STRIPPING
            // In development, don't strip debug variants
            if (EditorUserBuildSettings.development)
                return;

            for (int i = 0; i < shaderCompilerData.Count; ++i)
            {
                bool mustStrip = false;
                foreach (var kw in m_ExcludedKeywords)
                {
                    if (shaderCompilerData[i].shaderKeywordSet.IsEnabled(kw))
                    {
                        mustStrip = true;
                        break;
                    }
                }

                if (mustStrip)
                {
                    shaderCompilerData.RemoveAt(i);
                    --i;
                }
            }
#endif
        }
    }
}