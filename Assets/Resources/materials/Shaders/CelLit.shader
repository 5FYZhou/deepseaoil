Shader "DeepseaOil/2D/CelLit"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint (整体染色)", Color) = (1, 1, 1, 1)

        [Header(Light)]
        _AmbientFloor ("Ambient Floor (暗部保底)", Range(0.0, 0.6)) = 0.02
        _TileLightBoost ("Tile Light Boost (光场增强倍率)", Range(0.5, 3.0)) = 1.35

        [Header(Self Emission Boost)]
        _EmissiveBoost ("Emissive Boost (自发光增益)", Range(0.0, 1.5)) = 0.0

        [Header(Rim and Metal Highlight)]
        [Toggle] _EnableRim ("Enable Top Rim Highlight (顶端高光开关)", Float) = 0
        _RimThreshold ("Rim Threshold UV Y (高光起始高度)", Range(0.8, 1.0)) = 0.92
        _RimColor ("Rim Highlight Color (高光颜色)", Color) = (0.976, 0.808, 0.353, 1) // #F9CE5A 明黄
        _RimAlphaWeight ("Rim Alpha Weight (高光按透明度加权)", Range(0.0, 1.0)) = 0.65
        _MetalThreshold ("Metal Highlight Threshold (金属亮度阈值)", Range(0.7, 0.95)) = 0.85
        _MetalIntensity ("Metal Highlight Intensity (金属反光强度)", Range(0.0, 1.0)) = 0.0

        [Header(Hit Flash Integration)]
        _FlashAmount ("Flash Amount (受击闪白强度)", Range(0.0, 1.0)) = 0.0
        _FlashColor ("Flash Color (受击闪白色)", Color) = (1, 1, 1, 1)
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

            // 网格光照场由 TileGridLightmap 以全局量注入，严禁声明成材质属性（材质值会盖掉全局值）
            TEXTURE2D(_TileLightmap);
            SAMPLER(sampler_TileLightmap);

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float4 _MainTex_ST;
                float4 _RimColor;
                float4 _FlashColor;
                float _FlashAmount;
                float _AmbientFloor;
                float _EmissiveBoost;
                float _TileLightBoost;
                float _EnableRim;
                float _RimThreshold;
                float _RimAlphaWeight;
                float _MetalThreshold;
                float _MetalIntensity;
            CBUFFER_END

            // 全局量必须在 CBUFFER 之外：塞进 UnityPerMaterial 会破坏 SRP Batcher 的常量布局
            float4 _TileLightmapParams; // xy = 左下角世界坐标，zw = 宽高（世界单位）；zw 为 0 表示未装配
            float4 _CarryLightPos;      // xy = 随身光世界坐标，z = 每格衰减的 -log2，w = 强度
            float4 _CarryLightColor;    // rgb = 随身光颜色

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
                // 片元透视除法获取准确归一化屏幕 UV
                float2 screenUV = input.screenUV.xy / input.screenUV.w;
                half4 envLight = SAMPLE_TEXTURE2D(_ShapeLightTexture0, sampler_ShapeLightTexture0, screenUV);
                #else
                half4 envLight = half4(1, 1, 1, 1);
                #endif

                // 1. 暗部保险丝：保底按通道取大，值一高就把夜色三通道一起抬平、色相抹成中性灰（须 ≤ 0.02）
                envLight.rgb = max(envLight.rgb, half3(_AmbientFloor, _AmbientFloor, _AmbientFloor));

                // 2. 网格光场采样：世界坐标 → 光照图 UV（纯净双线性羽化，不做分阶，色块交还给原画贴图）
                float2 gridUV = (input.worldPos - _TileLightmapParams.xy) / max(_TileLightmapParams.zw, 0.0001);

                // 未装配网格光照场时 zw 为 0，onFlag 随之归零，整段网格光被乘没，退回纯环境光
                half onFlag = step(0.0001, _TileLightmapParams.z);
                half3 tileLight = SAMPLE_TEXTURE2D(_TileLightmap, sampler_TileLightmap, saturate(gridUV)).rgb * onFlag;

                // 3. 随身光源：世界坐标连续推进，exp2 衰减与火光同一条曲线（走路时不会一格一跳）
                float2 carryDelta = input.worldPos - _CarryLightPos.xy;
                half3 carryLight = _CarryLightColor.rgb * (_CarryLightPos.w * exp2(-length(carryDelta) * _CarryLightPos.z));

                // 4. 高反差取大：环境光是暗夜帷幕，网格光场是被撕开的那道火光（_TileLightBoost = 火光过曝余量）
                half3 finalLight = max(max(envLight.rgb, tileLight * _TileLightBoost), carryLight * _TileLightBoost);

                half3 finalRgb = baseCol.rgb * finalLight;

                // 5. 瓦片自发光增益（使火海自身明亮通透，摆脱外部光照依赖）
                finalRgb += baseCol.rgb * _EmissiveBoost;

                // 6. 顶端 UV 伪高光亮线
                if (_EnableRim > 0.5 && input.uv.y > _RimThreshold)
                {
                    finalRgb = lerp(finalRgb, _RimColor.rgb, _RimAlphaWeight * baseCol.a);
                }

                // 7. 金属亮度阈值阶梯反光
                half pixLum = dot(baseCol.rgb, half3(0.299, 0.587, 0.114));
                half metalMask = step(_MetalThreshold, pixLum) * _MetalIntensity;
                finalRgb = lerp(finalRgb, half3(1, 1, 1), metalMask);

                // 8. 受击纯白剪影材质插值（最高优先级）：不受环境光压暗影响
                finalRgb = lerp(finalRgb, _FlashColor.rgb, _FlashAmount);

                finalRgb *= baseCol.a;
                return half4(finalRgb, baseCol.a);
            }
            ENDHLSL
        }
    }
}
