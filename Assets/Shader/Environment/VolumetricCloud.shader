Shader "WalkingFat/VolumetricCloud_URP_Fixed"
{
    Properties
    {
        // ===== 云外观（材质手调）=====
        _DayCloudColor ("白天云色", Color) = (1, 0.98, 0.95, 1)
        _NightCloudColor ("夜晚云色", Color) = (0.1, 0.12, 0.18, 1)
        _HorizonCloudTint ("地平线云色调(暖)", Color) = (1, 0.90, 0.87, 1)
        _ZenithCloudTint ("天顶云色调(冷白)", Color) = (0.93, 0.96, 1, 1)
        _ShadowTint ("背光面色调(云底)", Color) = (0.62, 0.68, 0.85, 1)
        _ShadowStrength ("背光面阴影强度", Range(0, 1)) = 0.45
        _LitBoost ("受光面提亮", Range(0.5, 2)) = 1.2
        _EdgeSoftness ("边缘柔和度", Range(0.02, 0.5)) = 0.45
        _ClusterStrength ("云团聚强度(越大云隙越多)", Range(0, 1)) = 0.30
        _CloudWarp ("云团扭曲强度", Range(0, 0.8)) = 0.50
        _DistanceSoftness ("远处额外柔化", Range(0, 0.5)) = 0.18
        _NoiseRotation ("噪声旋转(度，破轴线网格感)", Range(0, 180)) = 25
        _DomeFlatten ("云罩压平(0=无限平面,1=纯天穹)", Range(0.02, 1.5)) = 0.35
        _HorizonDensityBoost ("近地平线增密", Range(0, 0.3)) = 0.15
        _MaxOpaquePath ("掠射加厚上限", Range(1, 20)) = 8
        _MinRayY ("地平线最小仰角(正弦值，仅影响遮挡)", Range(0.002, 0.2)) = 0.04
        _HazeAlphaFade ("地平线淡出强度(不透明度，1=完全淡出)", Range(0, 1)) = 0.9
        _OcclusionEnabled ("启用地形遮挡(深度纹理)", Range(0, 1)) = 1
        _OcclusionBias ("遮挡容差(米)", Range(0, 100)) = 3

        // ===== 由 DrawVolumetricCloud 每帧用 MaterialPropertyBlock 写入（不污染材质资产）=====
        [HideInInspector] _LayerAltitude ("Layer Altitude", Float) = 550
        [HideInInspector] _LayerCoverage ("Layer Coverage", Float) = 0.4
        [HideInInspector] _LayerScale ("Layer Scale", Float) = 0.005
        [HideInInspector] _LayerDensity ("Layer Density", Float) = 1.4
        [HideInInspector] _LayerDetail ("Layer Detail", Float) = 0.3
        [HideInInspector] _LayerTint ("Layer Tint", Color) = (1,1,1,1)
        [HideInInspector] _LayerWind ("Layer Wind", Vector) = (0,0,0,0)
        [HideInInspector] _CloudLerp ("Cloud Lerp (0夜 1昼)", Float) = 1
        [HideInInspector] _CloudTintColor ("Cloud Tint", Color) = (1,1,1,1)
        [HideInInspector] _CloudExposure ("Cloud Exposure", Float) = 1
        [HideInInspector] _WeatherCloudCoverage ("Weather Coverage", Float) = 1
        [HideInInspector] _SunDirection ("Sun Direction (指向光源)", Vector) = (0,1,0,0)
        [HideInInspector] _CloudSunColor ("Cloud Sun Color (主光源色)", Color) = (1,1,1,1)
        [HideInInspector] _CloudHazeColor ("Haze Color (远景雾色)", Color) = (0.72,0.78,0.86,1)
        [HideInInspector] _CloudHazeStart ("Haze Start (米)", Float) = 3000
        [HideInInspector] _CloudHazeEnd ("Haze End (米)", Float) = 12000
        [HideInInspector] _CloudHazeStrength ("Haze Strength", Float) = 1
    }
    SubShader
    {
        Tags {
            "RenderType" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Transparent"
        }
        LOD 100

        Pass
        {
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            Cull Off
            ZWrite Off
            // 不做硬件深度测试：云的"远近"由视线投影距离决定，与天穹几何的位置无关。
            // 遮挡改用场景深度纹理手动判断（见 frag），这样不依赖绘制时机与深度压平面技巧
            ZTest Always

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            // ---- 脚本写入的每层参数 ----
            float _LayerAltitude;
            float _LayerCoverage;
            float _LayerScale;
            float _LayerDensity;
            float _LayerDetail;
            float4 _LayerTint;
            float2 _LayerWind;

            // ---- 脚本写入的环境/天气/雾参数 ----
            float _CloudLerp;
            float4 _CloudTintColor;
            float _CloudExposure;
            float _WeatherCloudCoverage;
            float4 _SunDirection;
            float4 _CloudSunColor;
            float4 _CloudHazeColor;
            float _CloudHazeStart;
            float _CloudHazeEnd;
            float _CloudHazeStrength;

            // ---- 材质手调 ----
            float4 _DayCloudColor;
            float4 _NightCloudColor;
            float4 _HorizonCloudTint;
            float4 _ZenithCloudTint;
            float4 _ShadowTint;
            float _ShadowStrength;
            float _LitBoost;
            float _EdgeSoftness;
            float _ClusterStrength;
            float _CloudWarp;
            float _DistanceSoftness;
            float _NoiseRotation;
            float _DomeFlatten;
            float _HorizonDensityBoost;
            float _MaxOpaquePath;
            float _MinRayY;
            float _OcclusionEnabled;
            float _OcclusionBias;
            float _HazeAlphaFade;

            struct appdata
            {
                float4 vertex : POSITION;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                // 单位半球上的位置 = 该像素的视线方向（天穹球心即相机，方向无需再算世界坐标）
                float3 dir : TEXCOORD0;
            };

            v2f vert(appdata v)
            {
                v2f o;
                VertexPositionInputs vertexInput = GetVertexPositionInputs(v.vertex.xyz);
                o.pos = vertexInput.positionCS;
                o.dir = v.vertex.xyz;
                return o;
            }

            // ---- 程序化噪声 ----
            // 哈希全程用 frac 把小数值留在低位，避免 sin(巨大数) 丢精度：
            // 云层投影距离可达数十公里，旧写法在远处会出现重复条纹
            float Hash21(float2 p)
            {
                float3 p3 = frac(float3(p.xyx) * 0.1031);
                p3 += dot(p3, p3.yzx + 33.33);
                return frac((p3.x + p3.y) * p3.z);
            }

            float ValueNoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                float2 u = f * f * (3.0 - 2.0 * f);
                return lerp(
                    lerp(Hash21(i), Hash21(i + float2(1, 0)), u.x),
                    lerp(Hash21(i + float2(0, 1)), Hash21(i + float2(1, 1)), u.x), u.y);
            }

            // 多倍频噪声。octaves 直接给出参与合成的倍频数（1 = 只剩最大形状，5 = 全细节），
            // 按倍频逐级淡出（而不是只砍掉后三个）：掠射角下一个像素会跨越数公里世界范围，
            // 多留一级高频倍频就会在屏幕上被拉成一条"虚线"，这是地平线条纹的根源。
            // 结果按实际权重归一化，使倍频数变化时值域稳定（覆盖率阈值不会漂移）
            float Fbm(float2 p, float octaves)
            {
                octaves = saturate(octaves);
                float v = 0.0;
                float a = 0.5;
                float norm = 0.0;
                [unroll]
                for (int k = 0; k < 5; k++)
                {
                    float w = saturate(octaves - k);
                    v += a * w * ValueNoise(p);
                    norm += a * w;
                    p = p * 2.03 + float2(19.7, 7.3);
                    a *= 0.5;
                }
                return v / max(norm, 1e-4);
            }

            half4 frag(v2f i) : SV_Target
            {
                float3 dir = normalize(i.dir);

                float altitude = max(_LayerAltitude - _WorldSpaceCameraPos.y, 1.0);

                // 真实视线距离（地平线方向可达数十公里，用 _MinRayY 钳住）：只用于远景雾与遮挡判断
                float rayLength = altitude / max(dir.y, _MinRayY);

                // ---- 云场采样坐标：以相机为中心的"圆罩"投影 ----
                // dir.xz / (dir.y + k)：k→0 等价于"无限大的云层平面"（掠射角下投影距离趋于无穷，
                // 云被压缩成横向条纹 —— 就是之前地平线出现条纹的原因）；k 越大越接近纯天穹（等角、无压缩）。
                // 取 0.35 左右时地平线方向的最大角放大被限制在 1/k ≈ 2.9 倍，云团在任何方向都能保持
                // 足够大的角尺寸，既保留一点自然的透视压缩又不会退化
                float2 domePos = dir.xz / (dir.y + _DomeFlatten) * altitude;

                // ---- 屏幕足迹 → 可解析的倍频数 ----
                // 圆罩投影下足迹有界，这里只作为抗锯齿保险：万一某个方向仍小于一个像素，
                // 就逐级降 LOD；连最大形状都不可解析时（unresolved → 1）把云推向"实心"。
                // 注意：导数必须在任何逐像素 discard 之前取，否则控制流不统一
                float footprint = max(length(ddx(domePos)), length(ddy(domePos)));
                float cellWorld = 1.0 / max(_LayerScale, 1e-5);        // 一个噪声格子的世界尺寸
                float octaves = clamp(log2(max(cellWorld / (2.5 * max(footprint, 0.001)), 1e-4)) * 0.979, 0.0, 5.0);
                float unresolved = saturate(1.0 - octaves);
                float fieldOctaves = max(octaves, 1.0);

                // 地平线以下没有云（天穹上半球理论上不会产生，双面绘制时保险起见仍判一次）
                if (dir.y <= 0.001)
                    discard;

                // ---- 被更近的实体遮挡则整像素丢弃 ----
                // 云的距离是 rayLength（可达数十公里），远比天穹几何所在的几百米远，所以不能用硬件深度测试；
                // 这里直接拿场景深度比距离：山体/建筑比云近就把云丢掉，云就不会糊在山前面
                if (_OcclusionEnabled > 0.5)
                {
                    float rawDepth = SampleSceneDepth(GetNormalizedScreenSpaceUV(i.pos));
                    #if UNITY_REVERSED_Z
                        bool isSkyPixel = rawDepth <= 1e-6;      // 反向 Z：远平面为 0
                    #else
                        bool isSkyPixel = rawDepth >= 1.0 - 1e-6;
                    #endif
                    if (!isSkyPixel)
                    {
                        float sceneEyeDepth = LinearEyeDepth(rawDepth, _ZBufferParams);
                        if (sceneEyeDepth + _OcclusionBias < rayLength)
                            discard;
                    }
                }

                float2 p = (domePos + _LayerWind) * _LayerScale;

                // 把噪声网格转一个角度：值噪声的格子是沿世界 XZ 轴的，
                // 在掠射角下会与"等距离线"对齐，形成规则的横向条纹
                float rotRad = radians(_NoiseRotation);
                float2x2 rotMat = float2x2(cos(rotRad), sin(rotRad), -sin(rotRad), cos(rotRad));
                p = mul(rotMat, p);

                // 域扭曲：用低频噪声偏移采样坐标，云团才会成块成带、边缘有机，而不是均匀的芝麻点
                float2 warp = (float2(ValueNoise(p * 0.4 + 13.7), ValueNoise(p * 0.4 + 71.3)) - 0.5) * _CloudWarp;
                float2 pw = p + warp;

                // 覆盖率：基础云量 × 天气倍率，再叠加"近地平线增密"（层积云在地平线附近堆叠）
                float coverage = saturate(_LayerCoverage * _WeatherCloudCoverage);
                coverage = saturate(coverage + (1.0 - saturate(dir.y * 2.5)) * _HorizonDensityBoost);
                float threshold = lerp(0.95, 0.28, coverage);

                // 大尺度云团聚分布：让云成团成带并留出云隙，而不是均匀撒满天空
                float clusters = Fbm(p * 0.12 + 137.31, min(fieldOctaves, 3.0));
                threshold += (clusters - 0.5) * _ClusterStrength;

                // 先算大形状：即使细节取最大值也到不了阈值的像素（天空）提前丢弃，省掉后面的采样。
                // 亚像素（unresolved>0）时不做剔除，因为那些像素随后会被推向"实心"
                float shape = Fbm(pw, fieldOctaves);
                if (unresolved < 0.02 && shape * (1.0 - _LayerDetail) + _LayerDetail < threshold)
                    discard;

                float detail = Fbm(pw * 3.1 + _Time.y * 0.03, fieldOctaves);
                float field = shape * (1.0 - _LayerDetail) + detail * _LayerDetail;

                // 远处额外加宽过渡带，再按"不可解析程度"把云推向实心：
                // 远端是一整条连续云带（交给雾色），而不是随机虚线
                float softness = _EdgeSoftness + unresolved * _DistanceSoftness;
                float cloud = smoothstep(threshold, threshold + softness + 0.05, field);
                cloud = lerp(cloud, 1.0, unresolved);
                if (cloud <= 0.001)
                    discard;

                // 伪光照：沿真实太阳/月亮方向的地面投影偏移再采样一次，密度差形成朝向光源的明暗起伏
                float2 sunOffset = normalize(_SunDirection.xz + 1e-4) * 0.18;
                float lit = Fbm(pw + sunOffset, fieldOctaves);
                float shade = saturate((field - lit) * 4.0 + 0.70);

                // ---- 沿视线的光学厚度（Beer-Lambert）----
                // 视线在云层内穿过的路径 ≈ 厚度 / sin(仰角)：正上方最薄、贴地平线最厚。
                // 这正是"地平线附近云海密实、头顶云层通透"的成因，也是体积感的来源
                float pathFactor = clamp(1.0 / max(dir.y, 0.05), 1.0, _MaxOpaquePath);
                float alpha = cloud * (1.0 - exp(-_LayerDensity * pathFactor));

                // ---- 着色 ----
                half3 baseCol = lerp(_NightCloudColor.rgb, _DayCloudColor.rgb, saturate(_CloudLerp));
                baseCol *= _CloudTintColor.rgb * _CloudExposure;

                // 受光面染主光源色并提亮，背光面（云底）压暗偏冷
                half3 shadowCol = baseCol * (1.0 - _ShadowStrength) * _ShadowTint.rgb;
                half3 litCol = baseCol * _CloudSunColor.rgb * _LitBoost;
                half3 col = lerp(shadowCol, litCol, shade);

                // 大气垂直渐变：近地平线偏暖（阳光长路径散射），天顶偏冷白；再叠每层的色调微调
                col *= lerp(_HorizonCloudTint.rgb, _ZenithCloudTint.rgb, saturate(dir.y * 1.8)) * _LayerTint.rgb;

                // ---- 地平线淡出（远景雾融合）----
                // 按"仰角"而不是"距离"做渐变：rayLength 在地平线方向被 _MinRayY 钳住，
                // 用它做 saturate 会在钳制处留下斜率拐点（就是那条锐利边）。
                // 这里把组件上的起止距离（米）换算成仰角正弦，用 smoothstep 平滑过渡（两端导数为 0），
                // 同时按雾化程度降低不透明度，云就在地平线附近"化掉"而不是被切一条线
                float yStart = altitude / max(_CloudHazeStart, 1.0);
                float yEnd = altitude / max(_CloudHazeEnd, 1.0);
                float horizonFade = 1.0 - smoothstep(yEnd, yStart, dir.y);
                float haze = horizonFade * _CloudHazeStrength;
                col = lerp(col, _CloudHazeColor.rgb, haze);
                alpha *= 1.0 - horizonFade * _HazeAlphaFade;

                return half4(col, alpha);
            }
            ENDHLSL
        }
    }
    Fallback "Universal Render Pipeline/Unlit"
}
