Shader "DeepseaOil/2D/SpriteFlashUnlit"
{
    // 名里带 Unlit 是为了让人一眼看出它没有 Universal2D 通道：受光层（Ground/Actor）之外
    // 的隔离层（Aim/Effect/UI）一旦挂上受光材质，光照贡献恒为 0，整块会被乘成黑
    Properties
    {
        _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _FlashAmount ("Flash Amount", Range(0.0, 1.0)) = 0.0
        _FlashColor ("Flash Color", Color) = (1,1,1,1)
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

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            float4 _MainTex_ST;

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float4 _FlashColor;
                float _FlashAmount;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.color = input.color * _Color;
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                return output;
            }

            // 闪白必须靠插值：SpriteRenderer.color 是逐像素乘法，乘以纯白等于原画，深色像素永远不可能变白
            half4 frag(Varyings input) : SV_Target
            {
                half4 finalColor = _MainTex.Sample(sampler_MainTex, input.uv) * input.color;

                finalColor.rgb = lerp(finalColor.rgb, _FlashColor.rgb, _FlashAmount);

                finalColor.rgb *= finalColor.a;

                return finalColor;
            }
            ENDHLSL
        }
    }
}
