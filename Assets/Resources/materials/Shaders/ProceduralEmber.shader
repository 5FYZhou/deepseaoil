Shader "DeepseaOil/Particles/ProceduralEmber"
{
    Properties
    {
        _CoreSize ("Core Size (白热核大小)", Range(0.1, 0.6)) = 0.35
        _Sharpness ("Edge Sharpness (菱形边缘锐度)", Range(1.0, 6.0)) = 2.5
        _FlickerSpeed ("Flicker Speed (频闪速率)", Float) = 22.0
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
                float _CoreSize;
                float _Sharpness;
                float _FlickerSpeed;
            CBUFFER_END

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
                float2 p = (input.uv - 0.5) * 2.0;

                // 1. 切比雪夫正方形距离场：绝对 100% 纯正硬朗的像素小方块，彻底杜绝星芒！
                float d = max(abs(p.x), abs(p.y));

                // 纯像素硬边裁剪（绝无边缘模糊）
                if (d > 0.92) discard;

                // 2. 像素阶跃频闪：模拟 2D 抽帧跳动，不再是平滑呼吸，更具复古感
                float seed = frac(sin(input.color.a * 100.0 + input.color.r * 15.0) * 43758.5453);
                float stepFlicker = lerp(0.85, 1.25, step(0.45, frac(_Time.y * _FlickerSpeed + seed)));

                // 3. 嵌套白热方块核 (Nested Hot Pixel Core)
                // 方块正中心（d < 0.45）为高亮白热小方芯，外层保持粒子原本的焦糖橙红
                float isCore = step(d, _CoreSize);

                half3 baseColor = input.color.rgb;
                half3 hotWhiteCore = half3(1.35, 1.30, 0.95); // 白热高温像素心

                half3 finalColor = lerp(baseColor, hotWhiteCore, isCore) * stepFlicker;
                float finalAlpha = input.color.a;

                return half4(finalColor, finalAlpha);
            }

            ENDHLSL
        }
    }
}