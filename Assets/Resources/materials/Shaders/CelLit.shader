Shader "DeepseaOil/2D/CelLit"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1, 1, 1, 1)

        [Header(Cel Shading and Floor)]
        _AmbientFloor ("Ambient Floor", Range(0.35, 0.6)) = 0.35
        _CelBands ("Cel Bands Count", Range(2, 4)) = 3

        [Header(Organic Light Jitter)]
        _OrganicJitter ("Organic Light Jitter", Range(0.0, 0.05)) = 0.015

        [Header(Self Emission Boost)]
        _EmissiveBoost ("Emissive Boost", Range(0.0, 1.5)) = 0.0

        [Header(Rim and Metal Highlight)]
        [Toggle] _EnableRim ("Enable Top Rim Highlight", Float) = 0
        _RimThreshold ("Rim Threshold UV Y", Range(0.8, 1.0)) = 0.92
        _RimColor ("Rim Highlight Color", Color) = (0.976, 0.808, 0.353, 1) // #F9CE5A 明黄
        _MetalThreshold ("Metal Highlight Threshold", Range(0.7, 0.95)) = 0.85
        _MetalIntensity ("Metal Highlight Intensity", Range(0.0, 1.0)) = 0.0

        [Header(Hit Flash Integration)]
        _FlashAmount ("Flash Amount", Range(0.0, 1.0)) = 0.0
        _FlashColor ("Flash Color", Color) = (1, 1, 1, 1)
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
        }

        Blend SrcAlpha OneMinusSrcAlpha
        Cull Off
        ZWrite Off

        Pass
        {
            Name "Universal2D"
            Tags { "LightMode" = "Universal2D" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile USE_SHAPE_LIGHT_TYPE_0 __

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float3 positionOS : POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
                float2 worldPos : TEXCOORD1;
                float4 screenUV : TEXCOORD2;
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            #if USE_SHAPE_LIGHT_TYPE_0
            TEXTURE2D(_ShapeLightTexture0);
            SAMPLER(sampler_ShapeLightTexture0);
            #endif

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float4 _MainTex_ST;
                float4 _RimColor;
                float4 _FlashColor;
                float _FlashAmount;
                float _AmbientFloor;
                float _CelBands;
                float _OrganicJitter;
                float _EmissiveBoost;
                float _EnableRim;
                float _RimThreshold;
                float _MetalThreshold;
                float _MetalIntensity;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                output.positionCS = TransformObjectToHClip(input.positionOS);
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                output.color = input.color * _Color;
                output.worldPos = TransformObjectToWorld(input.positionOS).xy;

                // 官方正交/透视标准屏幕坐标输出：严禁在顶点阶段自行除以 w
                output.screenUV = ComputeScreenPos(output.positionCS);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                half4 mainTex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                half4 baseCol = mainTex * input.color;

                #if USE_SHAPE_LIGHT_TYPE_0
                // 世界坐标低频微扰：打破正圆，使光斑在赛璐珞分阶下呈现手绘齿状边缘
                float2 jitter = float2(
                    sin(input.worldPos.y * 3.5 + _Time.y * 1.5),
                    cos(input.worldPos.x * 3.5 + _Time.y * 1.5)
                ) * _OrganicJitter;

                // 片元透视除法获取准确归一化屏幕 UV
                float2 lightUV = input.screenUV.xy / input.screenUV.w + jitter;
                half4 light = SAMPLE_TEXTURE2D(_ShapeLightTexture0, sampler_ShapeLightTexture0, lightUV);
                #else
                half4 light = half4(1, 1, 1, 1);
                #endif

                // 1. 暗部下限锁定：防止弱光跌入死黑
                light.rgb = max(light.rgb, half3(_AmbientFloor, _AmbientFloor, _AmbientFloor));

                // 2. 赛璐珞光强阶梯量化
                half lightLum = dot(light.rgb, half3(0.299, 0.587, 0.114));
                half steppedLum = floor(lightLum * _CelBands) / _CelBands + (0.5 / _CelBands);
                half3 celLight = light.rgb * (steppedLum / max(lightLum, 0.001));

                half3 finalRgb = baseCol.rgb * celLight;

                // 3. 瓦片自发光增益（使火海自身明亮通透，摆脱外部光照依赖）
                finalRgb += baseCol.rgb * _EmissiveBoost;

                // 4. 顶端 UV 伪高光亮线
                if (_EnableRim > 0.5 && input.uv.y > _RimThreshold)
                {
                    finalRgb = lerp(finalRgb, _RimColor.rgb, 0.65 * baseCol.a);
                }

                // 5. 金属亮度阈值阶梯反光
                half pixLum = dot(baseCol.rgb, half3(0.299, 0.587, 0.114));
                half metalMask = step(_MetalThreshold, pixLum) * _MetalIntensity;
                finalRgb = lerp(finalRgb, half3(1, 1, 1), metalMask);

                // 6. 受击纯白剪影材质插值（最高优先级）：不受环境光压暗影响
                finalRgb = lerp(finalRgb, _FlashColor.rgb, _FlashAmount);

                finalRgb *= baseCol.a;
                return half4(finalRgb, baseCol.a);
            }
            ENDHLSL
        }
    }
}
