Shader "DeepseaOil/2D/TileFlow"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" { }
        _Color ("Tint", Color) = (1, 1, 1, 1)
        _FlowSpeed ("Flow Speed", Float) = 3
        _FlowAmplitude ("Flow Amplitude", Range(0, 0.05)) = 0.012
        _FlowFrequency ("Flow Frequency", Float) = 20
        _PhaseNoise ("Phase Noise ", Range(0, 1)) = 0 // (0=统一, 1=逐格随机)

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
                float2 worldPos : TEXCOORD1;
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            float4 _MainTex_ST;

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float _FlowSpeed;
                float _FlowAmplitude;
                float _FlowFrequency;
                float _PhaseNoise;
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

                // 逐格相位：floor 量化到格，格内一致、格间不同
                float2 cellPos = floor(input.worldPos);
                float hash = frac(sin(dot(cellPos, float2(12.9898, 78.233))) * 43758.5453);
                float phase = hash * _PhaseNoise * 6.2831853;

                float dx = sin(_Time.y * _FlowSpeed + uv.y * _FlowFrequency + phase) * _FlowAmplitude;
                float dy = sin(_Time.y * _FlowSpeed * 1.3 + uv.x * _FlowFrequency * 0.7 + phase) * _FlowAmplitude;

                // 先采原色，按亮度算"深色权重"
                half4 baseCol = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv);
                float lum = dot(baseCol.rgb, float3(0.299, 0.587, 0.114));
                float darkWeight = saturate(1.0 - lum);

                float2 flowUV = uv + float2(dx, dy) * darkWeight;

                return SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, flowUV) * input.color;
            }
            
            ENDHLSL
        }
    }
}