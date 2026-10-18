Shader "Custom/SpriteOutlineGlow" {
    Properties {
        _MainTex ("精灵贴图", 2D) = "white" {}
        _GlowColor ("发光颜色", Color) = (1,1,1,1)
        _GlowWidth ("发光宽度", Range(0, 0.1)) = 0.01
        _AlphaThreshold ("边缘判定阈值", Range(0, 1)) = 0.1
        _GlowIntensity ("发光强度 (HDR配合Bloom)", Range(1, 10)) = 2.0
    }
    SubShader {
        Tags { 
            "Queue" = "Transparent" 
            "RenderType" = "Transparent" 
            "IgnoreProjector"="True"
            "PreviewType"="Plane"
        }
        Blend SrcAlpha OneMinusSrcAlpha // 换回标准混合
        Cull Off
        Lighting Off
        ZWrite Off

        Pass {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                fixed4 color : COLOR;
            };

            struct v2f {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
                fixed4 color : COLOR;
            };

            sampler2D _MainTex;
            float4 _MainTex_TexelSize; 
            fixed4 _GlowColor;
            float _GlowWidth;
            float _AlphaThreshold;
            float _GlowIntensity;

            v2f vert (appdata v) {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.color = v.color;
                return o;
            }

            // 安全采样函数：如果 UV 超出 0~1，直接返回 0，不读取贴图
            float SafeSampleAlpha(float2 uv) {
                float inBounds = step(0, uv.x) * step(uv.x, 1) * step(0, uv.y) * step(uv.y, 1);
                return tex2D(_MainTex, uv).a * inBounds;
            }

            fixed4 frag (v2f i) : SV_Target {
                fixed4 col = tex2D(_MainTex, i.uv) * i.color;
                
                // 如果是实体像素，不参与向外发光，直接返回原色
                if (col.a > _AlphaThreshold) {
                    return col;
                }

                // 计算偏移量 (去掉了 100 这个夸张的倍数，直接用像素尺寸)
                // 如果觉得光晕不够宽，在面板里把 _GlowWidth 调到 0.02 ~ 0.05 即可
                float2 offset = _MainTex_TexelSize.xy * _GlowWidth * 100.0;
                
                float maxAlpha = 0;

                // 8方向安全采样
                maxAlpha = max(maxAlpha, SafeSampleAlpha(i.uv + float2(offset.x, 0)));
                maxAlpha = max(maxAlpha, SafeSampleAlpha(i.uv - float2(offset.x, 0)));
                maxAlpha = max(maxAlpha, SafeSampleAlpha(i.uv + float2(0, offset.y)));
                maxAlpha = max(maxAlpha, SafeSampleAlpha(i.uv - float2(0, offset.y)));
                
                maxAlpha = max(maxAlpha, SafeSampleAlpha(i.uv + float2(offset.x, offset.y)));
                maxAlpha = max(maxAlpha, SafeSampleAlpha(i.uv - float2(offset.x, offset.y)));
                maxAlpha = max(maxAlpha, SafeSampleAlpha(i.uv + float2(offset.x, -offset.y)));
                maxAlpha = max(maxAlpha, SafeSampleAlpha(i.uv - float2(offset.x, -offset.y)));

                // 如果周围找到了实体
                if (maxAlpha > _AlphaThreshold) {
                    fixed4 glow = _GlowColor;
                    glow.rgb *= _GlowIntensity;
                    glow.a = maxAlpha; // 光晕透明度跟随实体
                    return glow;
                }

                // 否则保持原样（透明）
                return col;
            }
            ENDCG
        }
    }
}