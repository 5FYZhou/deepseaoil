Shader "UI/CircleStencil"
{
    Properties
    {
        _Radius ("Radius", Range(0, 1)) = 0.1
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent-1"
            "RenderType"="Transparent"
        }

        Cull Off
        ZWrite Off
        ZTest Always

        ColorMask 0

        Stencil
        {
            Ref 1
            Comp Always
            Pass Replace
        }

        Pass
        {
            CGPROGRAM

            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            float _Radius;

            v2f vert(appdata v)
            {
                v2f o;

                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;

                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float2 center = float2(0.5, 0.5);

                float2 delta = i.uv - center;

                float distance = length(delta);

                if (distance > _Radius)
                    discard;

                return 0;
            }

            ENDCG
        }
    }
}