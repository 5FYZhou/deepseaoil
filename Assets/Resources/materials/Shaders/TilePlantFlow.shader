Shader "DeepseaOil/2D/TilePlantFlow"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" { }
        _Color ("Tint", Color) = (1, 1, 1, 1)

        [Header(Root Anchor and Bending)]
        _HeightCutoff ("Root Anchor Height", Range(0.0, 0.8)) = 0.35    // 根部硬锚定切分点
        _HeightPower ("Tip Bending Power", Range(1.0, 4.0)) = 1.6       // 梢部弯曲柔韧指数

        [Header(Wind Wave Macro)]
        _WindSpeed ("Macro Wind Speed", Float) = 2.2                    // 宏观风速
        _WindStrength ("Wind Sway Strength", Range(0.0, 0.08)) = 0.025  // 水平摆动幅度
        _WindFrequency ("Wind Spatial Frequency", Float) = 1.8          // 风场波长频度
        _WindDirection ("Wind Direction", Vector) = (1.0, 0.15, 0, 0)   // 风向 X, Y

        [Header(Local Noise and Chaos)]
        _NoiseMix ("Noise Mix", Range(0.0, 1.0)) = 0.5                  // 无序/有序混合比，推荐0.5
        _GustSpeed ("Local Flutter Speed", Float) = 5.5                 // 高频微风颤动速率
        _GustFrequency ("Local Flutter Frequency", Float) = 16.0        // 叶片微褶频次

        [Header(Foliage Color Mask)]
        _ColorMaskWeight ("Color/Lum Mask Influence", Range(0.0, 1.0)) = 0.5    // 叶片色彩特征权重

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

        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off

        Pass
        {
            Name "PlantFlowUnlit"
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
                float2 worldPos : TEXCOORD1;
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            float4 _MainTex_ST;

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float4 _WindDirection;
                float _HeightCutoff;
                float _HeightPower;
                float _WindSpeed;
                float _WindStrength;
                float _WindFrequency;
                float _NoiseMix;
                float _GustSpeed;
                float _GustFrequency;
                float _ColorMaskWeight;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.color = input.color * _Color;
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                output.worldPos = TransformObjectToWorld(input.positionOS.xyz).xy;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float2 uv = input.uv;

                // 1. 基准采样与透明通道裁剪
                half4 baseCol = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv);
                if (baseCol.a <= 0.001)
                {
                    discard;
                }

                // 2. 根部绝对锚定权重：UV.y 低于 Cutoff 严格为 0，向上按幂次平滑增强
                float heightNormalized = saturate((uv.y - _HeightCutoff) / max(1.0 - _HeightCutoff, 0.001));
                float heightWeight = pow(heightNormalized, _HeightPower);

                // 3. 提取叶片色彩与明度特征（亮度适中且高饱和/深对比区域作为响应主体）
                float lum = dot(baseCol.rgb, float3(0.299, 0.587, 0.114));
                float maxChannel = max(baseCol.r, max(baseCol.g, baseCol.b));
                float minChannel = min(baseCol.r, min(baseCol.g, baseCol.b));
                float sat = maxChannel > 0.001 ? (maxChannel - minChannel) / maxChannel : 0.0;

                // 综合叶片特征：明度高且色彩深的区域获得更充沛的权重
                float leafFeature = saturate(lum * 0.6 + sat * 0.7);
                float colorWeight = lerp(1.0, leafFeature, _ColorMaskWeight);

                // 最终形变权重系数
                float swayWeight = heightWeight * colorWeight;

                // 极小值早退，节省运算与边缘采样
                if (swayWeight <= 0.0001)
                {
                    return baseCol * input.color;
                }

                // 4. 双频风场解算
                // A. 宏观世界风场行波（大范围连续波动，形成整体阵风传递）
                float2 windDir = normalize(_WindDirection.xy + float2(0.0001, 0.0));
                float macroPhase = dot(input.worldPos, windDir) * _WindFrequency - _Time.y * _WindSpeed;
                float macroWave = sin(macroPhase);

                // B. 逐格离散哈希（提取当前格子的独立随机种子）
                float2 cellCoord = floor(input.worldPos);
                float cellHash = frac(sin(dot(cellCoord, float2(12.9898, 78.233))) * 43758.5453);

                // C. 局部高频微颤抖动（叶梢细小翻滚颤动）
                float localPhase = _Time.y * _GustSpeed + uv.y * _GustFrequency + cellHash * 6.2831853;
                float localWave = sin(localPhase);

                // D. 核心混合：lerp 混合有序大风与局部无序微颤（_NoiseMix 控制平衡）
                float windResult = lerp(macroWave, localWave, _NoiseMix);

                // 5. 偏移量计算与采样（X 轴侧向吹摆，Y 轴伴生极轻微投影下压）
                float xOffset = windResult * _WindStrength * swayWeight;
                float yOffset = -abs(xOffset) * 0.25 * swayWeight;

                float2 deformedUV = uv - float2(xOffset, yOffset);

                half4 swayCol = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, deformedUV);

                // 6. 边缘透明度安全过渡（若形变越界采到空气，自动向基准色插值，防止硬黑边/破面）
                half4 finalCol = lerp(baseCol, swayCol, saturate(swayCol.a * 2.0));

                return finalCol * input.color;
            }
            ENDHLSL
        }
    }
    FallBack "Universal Render Pipeline/2D/Sprite-Unlit-Default"
}