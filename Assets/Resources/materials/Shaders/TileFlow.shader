Shader "DeepseaOil/2D/TileFlow"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" { }
        _Color ("Tint (整体染色)", Color) = (1, 1, 1, 1)
        _FlowSpeed ("Flow Speed (流动速度)", Float) = 3
        _FlowAmplitude ("Flow Amplitude (流动位移幅度)", Range(0, 0.05)) = 0.012
        _FlowFrequency ("Flow Frequency (波纹空间频度)", Float) = 20
        _PhaseNoise ("Phase Noise (逐格相位打散)", Range(0, 1)) = 0
        _AmbientFloor ("Ambient Floor (暗部保底)", Range(0.0, 0.6)) = 0.02
        _TileLightBoost ("Tile Light Boost (光场增强倍率)", Range(0.5, 3.0)) = 1.35

        [Header(Fluid Specular Glint)]
        _GlintIntensity ("Glint Intensity (波光反光强度)", Range(0.0, 3.0)) = 0.0
        _GlintThreshold ("Glint Trigger Threshold (光场触发门限)", Range(0.0, 0.5)) = 0.06
        _GlintExponent ("Glint Sharpness (波光尖锐度)", Range(2.0, 32.0)) = 12.0
        _GlintColor ("Glint Tint (波光染色)", Color) = (1.2, 0.95, 0.6, 1.0)
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
            Name "Universal2D"
            Tags { "LightMode" = "Universal2D" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile USE_SHAPE_LIGHT_TYPE_0 __

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
                float4 screenUV : TEXCOORD2;
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            #if USE_SHAPE_LIGHT_TYPE_0
            TEXTURE2D(_ShapeLightTexture0);
            SAMPLER(sampler_ShapeLightTexture0);
            #endif

            // 网格光照场由 TileGridLightmap 以全局量注入，严禁声明成材质属性（材质值会盖掉全局值）
            TEXTURE2D(_TileLightmap);
            SAMPLER(sampler_TileLightmap);

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float4 _MainTex_ST;
                float4 _GlintColor;
                float _FlowSpeed;
                float _FlowAmplitude;
                float _FlowFrequency;
                float _PhaseNoise;
                float _AmbientFloor;
                float _TileLightBoost;
                float _GlintIntensity;
                float _GlintThreshold;
                float _GlintExponent;
            CBUFFER_END

            // 全局量必须在 CBUFFER 之外：塞进 UnityPerMaterial 会破坏 SRP Batcher 的常量布局
            float4 _TileLightmapParams; // xy = 左下角世界坐标，zw = 宽高（世界单位）；zw 为 0 表示未装配
            float4 _CarryLightPos;      // xy = 随身光世界坐标，z = 每格衰减的 -log2，w = 强度
            float4 _CarryLightColor;    // rgb = 随身光颜色

            // 焦散相位扭曲的哈希：不用 sin 版本，省下每像素一次三角函数
            float Hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float ValueNoise(float2 p)
            {
                float2 cell = floor(p);
                float2 f = frac(p);
                float2 u = f * f * (3.0 - 2.0 * f);

                float n00 = Hash21(cell);
                float n10 = Hash21(cell + float2(1.0, 0.0));
                float n01 = Hash21(cell + float2(0.0, 1.0));
                float n11 = Hash21(cell + float2(1.0, 1.0));

                return lerp(lerp(n00, n10, u.x), lerp(n01, n11, u.x), u.y);
            }

            Varyings vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.color = input.color * _Color;
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                output.worldPos = TransformObjectToWorld(input.positionOS.xyz).xy;

                // 标准齐次裁剪坐标转换
                output.screenUV = ComputeScreenPos(output.positionCS);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float2 uv = input.uv;

                float2 cellPos = floor(input.worldPos);
                float hash = frac(sin(dot(cellPos, float2(12.9898, 78.233))) * 43758.5453);
                float phase = hash * _PhaseNoise * 6.2831853;

                float dx = sin(_Time.y * _FlowSpeed + uv.y * _FlowFrequency + phase) * _FlowAmplitude;
                float dy = sin(_Time.y * _FlowSpeed * 1.3 + uv.x * _FlowFrequency * 0.7 + phase) * _FlowAmplitude;

                half4 baseCol = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv);
                float lum = dot(baseCol.rgb, float3(0.299, 0.587, 0.114));
                float darkWeight = saturate(1.0 - lum);

                float2 flowUV = uv + float2(dx, dy) * darkWeight;
                half4 flowedColor = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, flowUV) * input.color;

                #if USE_SHAPE_LIGHT_TYPE_0
                float2 lightUV = input.screenUV.xy / input.screenUV.w;
                half4 envLight = SAMPLE_TEXTURE2D(_ShapeLightTexture0, sampler_ShapeLightTexture0, lightUV);
                #else
                half4 envLight = half4(1, 1, 1, 1);
                #endif

                // 暗部保险丝：保底按通道取大，值一高就把夜色三通道一起抬平、色相抹成中性灰（须 ≤ 0.02）
                envLight.rgb = max(envLight.rgb, half3(_AmbientFloor, _AmbientFloor, _AmbientFloor));

                // 网格光照场：水面与火场必须和地面走同一套扩散，否则夜里只有地面会亮
                float2 gridUV = (input.worldPos - _TileLightmapParams.xy) / max(_TileLightmapParams.zw, 0.0001);
                half onFlag = step(0.0001, _TileLightmapParams.z);
                half3 tileLight = SAMPLE_TEXTURE2D(_TileLightmap, sampler_TileLightmap, saturate(gridUV)).rgb * onFlag;

                float2 carryDelta = input.worldPos - _CarryLightPos.xy;
                half3 carryLight = _CarryLightColor.rgb * (_CarryLightPos.w * exp2(-length(carryDelta) * _CarryLightPos.z));

                half3 finalLight = max(max(envLight.rgb, tileLight * _TileLightBoost), carryLight * _TileLightBoost);

                // 2D 正交焦散：sin×cos 只在两轴同相时成峰，波峰封闭成细胞状，不会有 1D 平面波的平行亮条
                half3 waterSpecular = half3(0.0, 0.0, 0.0);

                // 火场材质强度为 0：整段跳过，同 Shader 的非水体材质输出不变
                if (_GlintIntensity > 0.0001)
                {
                    // 世界坐标建场：用逐格 uv 会让每格重复同一张光斑网格
                    float2 ripplePos = input.worldPos * (_FlowFrequency * 1.2);

                    // 双轴值噪声慢速剪切相位：把正交网格揉成不规则水脊，且跨格无缝
                    ripplePos += float2(
                        ValueNoise(ripplePos * 0.37 + float2(0.0, _Time.y * _FlowSpeed * 0.11)),
                        ValueNoise(ripplePos * 0.37 + float2(11.3, _Time.y * _FlowSpeed * 0.09))) * 2.8 - 1.4;

                    float waveX = sin(ripplePos.x + _Time.y * _FlowSpeed * 1.5);
                    float waveY = cos(ripplePos.y * 1.17 - _Time.y * _FlowSpeed * 1.2);
                    half caustic = saturate(waveX * waveY);

                    // 幂次收敛成碎斑：面板 _GlintExponent 10~14 对应实际指数 2.5~3.5
                    half shimmer = pow(caustic, max(_GlintExponent * 0.25, 1.0));

                    half dynLum = dot(tileLight.rgb, half3(0.299, 0.587, 0.114));
                    half glintTrigger = saturate((dynLum - _GlintThreshold) / max(1.0 - _GlintThreshold, 0.001));

                    waterSpecular = tileLight.rgb * _GlintColor.rgb * shimmer * _GlintIntensity * glintTrigger;
                }

                half3 finalRgb = (flowedColor.rgb * finalLight) + waterSpecular;
                finalRgb *= flowedColor.a;

                return half4(finalRgb, flowedColor.a);
            }
            ENDHLSL
        }
    }
}
