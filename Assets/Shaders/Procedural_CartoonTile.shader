Shader "URP2D/ProceduralCartoonTile"
{
    Properties
    {
        [Header(Crayon Chalk Colors)]
        [MainColor] _Color ("Main Chalk Color", Color) = (1.0, 0.95686275, 0.83137256, 0.8) // 奶油暖白
        _InnerFillColor ("Inner Soft Fill", Color) = (1.0, 0.85, 0.55, 0.08)   // 内部微弱暖黄沉淀

        [Header(Rounded Shape)]
        _CornerRadius ("Corner Smoothness", Range(0.05, 0.45)) = 0.26
        _BorderThickness ("Chalk Stroke Thickness", Range(0.01, 0.12)) = 0.05
        _WobbleStrength ("Hand-Drawn Wobble", Range(0.0, 0.03)) = 0.025
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
        }

        Pass
        {
            Name "CartoonTilePass"
            Tags { "LightMode" = "Universal2D" }

            Blend SrcAlpha OneMinusSrcAlpha // 赛璐璐水彩颜料混合，绝不发黑
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float4 _InnerFillColor;
                float _CornerRadius;
                float _BorderThickness;
                float _WobbleStrength;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                output.color = input.color;
                return output;
            }

            // SDF 圆角矩形距离场
            float sdRoundedBox(float2 p, float2 b, float r)
            {
                float2 q = abs(p) - b + r;
                return length(max(q, 0.0)) + min(max(q.x, q.y), 0.0) - r;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float2 p = input.uv - 0.5; // 以中心为原点 (-0.5 ~ 0.5)

                // 1. 手绘微晃动 (Wobble) - 用极低频角度噪声打破死板直线
                float angle = atan2(p.y, p.x);
                float handWobble = (sin(angle * 6.0) * 0.6 + cos(angle * 10.0) * 0.4) * _WobbleStrength;

                // 2. 软萌圆角外框 SDF
                float2 boxHalfSize = float2(0.46, 0.46);
                float dist = sdRoundedBox(p, boxHalfSize, _CornerRadius) + handWobble;

                // 粗蜡笔线条边缘羽化 (抗锯齿 + 纸面颗粒感)
                float stroke = smoothstep(_BorderThickness * 0.5 + 0.008, _BorderThickness * 0.5, abs(dist));

                // 内部浅色粉笔轻微拓印
                float innerMask = smoothstep(0.005, -0.06, dist);

                // 3. 颜色与透明度合成
                half4 finalColor = _Color * input.color;
                float strokeAlpha = saturate(stroke) * finalColor.a;
                float fillAlpha = innerMask * _InnerFillColor.a;

                half3 rgb = lerp(_InnerFillColor.rgb, finalColor.rgb, strokeAlpha / max(strokeAlpha + fillAlpha, 0.0001));
                float alpha = saturate(strokeAlpha + fillAlpha);

                return half4(rgb, alpha);
            }
            ENDHLSL
        }
    }
}
