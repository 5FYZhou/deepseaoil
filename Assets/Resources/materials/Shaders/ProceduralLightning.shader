Shader "DeepseaOil/Particles/ProceduralLightning"
{
    Properties
    {
        _GlowColor ("Glow Color (外圈电青色)", Color) = (0.15, 0.88, 1.0, 1.0)
        _CoreWidth ("Core Width (内芯粗细)", Range(0.01, 0.06)) = 0.025
        _GlowWidth ("Glow Width (外边粗细)", Range(0.02, 0.12)) = 0.055
        _Jaggedness ("Jaggedness (折角幅度)", Range(0.1, 0.6)) = 0.28
        _FlickerSpeed ("Flicker Speed (抽搐频次)", Float) = 22.0
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
        }

        // 手绘风格标准半透明混合（在亮色沙地与深色水面均有清晰电青描边）
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off

        Pass
        {
            Name "Unlit"
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _GlowColor;
                float _CoreWidth;
                float _GlowWidth;
                float _Jaggedness;
                float _FlickerSpeed;
            CBUFFER_END

            float hash11(float p)
            {
                p = frac(p * 0.1031);
                p *= p + 33.33;
                p *= p + p;
                return frac(p);
            }

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.color = input.color;
                output.uv = input.uv;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                // p 映射到 [-1, 1]，以垂直纵向 Y 轴为主贯穿轴（对齐参考图 2）
                float2 p = (input.uv - 0.5) * 2.0;

                // 长度范围裁剪（上下留出边界）
                if (abs(p.y) > 0.88) discard;

                // 阶跃抽搐时间（手绘关键帧抽帧手感）
                float timeStep = floor(_Time.y * _FlickerSpeed);
                float seed = timeStep * 1.618 + input.color.a * 100.0;

                // 1. 将纵向 Y 轴划分为 5 段（6 个折转节点），完全复刻参考图 2 的转折节奏
                float t = saturate((p.y + 0.85) / 1.7); // 0 (底端) -> 1 (顶端)
                float seg = t * 5.0;
                float i = floor(seg);
                float f = frac(seg);

                // 相邻两点纯直线连接，在节点处斜率突变形成干净利落的硬折角
                float h0 = (hash11(i + seed) * 2.0 - 1.0) * _Jaggedness;
                float h1 = (hash11(i + 1.0 + seed) * 2.0 - 1.0) * _Jaggedness;
                float targetX = lerp(h0, h1, f);

                // 2. 距离场：当前像素到手绘闪电主轴的水平距离
                float dist = abs(p.x - targetX);

                // 3. 核心突破【末端如针尖般收细 (对齐参考图 2 底部针尖)】
                // 从顶端到末梢逐渐收缩，底端极细收尖
                float taper = smoothstep(0.0, 0.4, t) * smoothstep(1.0, 0.85, t);

                float coreW = _CoreWidth * taper;
                float glowW = coreW + _GlowWidth * taper;

                // 4. 纯平滑手绘抗锯齿（彻底告别像素方块锯齿）
                float core = smoothstep(coreW + 0.004, coreW - 0.004, dist);
                float glow = smoothstep(glowW + 0.008, glowW - 0.008, dist);

                // 外层极其微弱的柔和手绘透光
                float softFalloff = saturate(1.0 - dist / (glowW + 0.04));
                softFalloff = pow(softFalloff, 3.0) * 0.45 * taper;

                // 5. 双色手绘赛璐珞着色（纯白电芯 + 鲜艳电青外轮廓）
                half3 cyanOutline = _GlowColor.rgb * input.color.rgb;
                half3 whiteCore = half3(1.0, 1.0, 1.0);

                half3 finalColor = lerp(cyanOutline, whiteCore, core);
                float finalAlpha = saturate(core + glow + softFalloff) * input.color.a;

                return half4(finalColor, finalAlpha);
            }
            ENDHLSL
        }
    }
}

// Shader "DeepseaOil/Particles/ProceduralLightning"
// {
//     Properties
//     {
//         _Jaggedness ("Jaggedness (折角剧烈幅度)", Range(0.1, 0.8)) = 0.45
//         _CoreWidth ("Core Width (电芯极细线宽)", Range(0.005, 0.04)) = 0.015
//         _CoreBrightness ("Core Brightness (白热电芯强度)", Range(1.0, 5.0)) = 3.5
//         _GlowFalloff ("Glow Falloff (电晕衰减速度)", Range(5.0, 30.0)) = 15.0
//         _GlowBrightness ("Glow Brightness (电晕亮度)", Range(0.1, 2.0)) = 0.9
//         _FlickerSpeed ("Flicker Speed (抽搐频次)", Float) = 30.0
//     }

//     SubShader
//     {
//         Tags
//         {
//
// "RenderType" = "Transparent"
//
// "Queue" = "Transparent"
//
// "RenderPipeline" = "UniversalPipeline"
//
// "IgnoreProjector" = "True"
//         }

//         Blend One One
//         ZWrite Off
//         Cull Off

//         Pass
//         {
//             Name "Unlit"
//             HLSLPROGRAM
//             #pragma vertex vert
//             #pragma fragment frag

//             #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

//             struct Attributes
//             {
//                 float4 positionOS : POSITION;
//                 float4 color : COLOR;
//                 float2 uv : TEXCOORD0;
//             };

//             struct Varyings
//             {
//                 float4 positionCS : SV_POSITION;
//                 float4 color : COLOR;
//                 float2 uv : TEXCOORD0;
//             };

//             CBUFFER_START(UnityPerMaterial)
//                 float _Jaggedness;
//                 float _CoreWidth;
//                 float _CoreBrightness;
//                 float _GlowFalloff;
//                 float _GlowBrightness;
//                 float _FlickerSpeed;
//             CBUFFER_END

//             float hash1D(float p)
//             {
//                 p = frac(p * 0.1031);
//                 p *= p + 33.33;
//                 p *= p + p;
//                 return frac(p);
//             }

//             // 核心算法：分段线性插值（Piecewise Linear）
//             // 节点之间绝对是一根笔直的斜线，节点处斜率瞬间突变，产生刀刻般的尖锐 V/Z 硬折角！
//             float jaggedNoise(float x, float seed)
//             {
//                 float i = floor(x);
//                 float f = frac(x); // 绝对不加 smoothstep！保持纯线性，杜绝一切平滑曲线！
//                 float h0 = hash1D(i + seed) * 2.0 - 1.0;
//                 float h1 = hash1D(i + 1.0 + seed) * 2.0 - 1.0;
//                 return lerp(h0, h1, f);
//             }

//             // 手绘卡通风格折线：剔除写实高频碎刺，仅保留 2~3 处果断决绝的手绘大转折
//             float getLightningY(float x, float seed)
//             {
//                 // 主笔势：全段仅 2~3 处大折角（类似动漫原画的一笔成型）
//                 float mainStroke = jaggedNoise(x * 1.8, seed);
//                 // 辅助笔势：仅在关键转折处给出一道清脆的次级转角
//                 float accentStroke = jaggedNoise(x * 3.6, seed + 11.3);

//                 return (mainStroke * 0.75 + accentStroke * 0.25);
//             }

//             Varyings vert(Attributes input)
//             {
//                 Varyings output;
//                 output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
//                 output.color = input.color;
//                 output.uv = input.uv;
//                 return output;
//             }

//             half4 frag(Varyings input) : SV_Target
//             {
//                 float2 p = (input.uv - 0.5) * 2.0;

//                 // 24Hz 阶跃抽搐（手绘动画的抽帧定格感，告别平滑线性）
//                 float timeStep = floor(_Time.y * _FlickerSpeed);
//                 float seed = timeStep * 1.618 + input.color.a * 100.0;

//                 // 1. 动势包络：两端如毛笔笔尖锐利收窄，中段最粗壮
//                 float envelope = saturate(1.0 - abs(p.x));
//                 float strokeEnvelope = smoothstep(0.0, 0.45, envelope);

//                 float mainBoltY = getLightningY(p.x, seed) * _Jaggedness * strokeEnvelope;
//                 float distMain = abs(p.y - mainBoltY);

//                 // 2. 手绘短尖刺分支：单侧斜向刺出一道利落的小锐角分叉（类似动漫速度线）
//                 float forkX = (p.x - 0.15) * 2.2;
//                 float forkY = mainBoltY + forkX * 0.65;
//                 float distFork = abs(p.y - forkY);
//                 float forkMask = smoothstep(0.5, 0.1, abs(p.x - 0.2)) * step(0.0, p.x);

//                 float d = min(distMain, lerp(10.0, distFork, forkMask));

//                 // 3. 手绘笔触粗细变化（转弯与中段饱满扎实，两端如刀锋般锐利收笔）
//                 float dynamicWidth = _CoreWidth * (0.4 + 0.6 * strokeEnvelope);

//                 // 4. 赛璐珞高亮白热电芯 (Cel Core)
//                 float core = saturate(1.0 - d / dynamicWidth);
//                 core = smoothstep(0.2, 0.95, core) * _CoreBrightness;

//                 // 5. 卡通紧凑外边缘光（收窄漫反射雾感，呈现锐利手绘勾边电晕）
//                 float glow = exp(-d * _GlowFalloff) * _GlowBrightness;

//                 // 6. 两端利落收刀（无拖泥带水）
//                 float endFade = saturate((1.0 - abs(p.x)) * 4.0);

//                 float intensity = (core + glow) * endFade;
//                 half3 boltColor = input.color.rgb * intensity;

//                 return half4(boltColor, 1.0);
//             }

//             ENDHLSL
//         }
//     }
// }