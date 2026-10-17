Shader "DeepseaOil/2D/TilePlantFlow"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" { }
        _Color ("Tint (整体染色)", Color) = (1, 1, 1, 1)

        [Header(Root Anchor and Bending)]
        _HeightCutoff ("Root Anchor Height (根部锚定高度)", Range(0.0, 0.8)) = 0.35    // 根部硬锚定切分点
        _HeightPower ("Tip Bending Power (梢部弯曲柔韧指数)", Range(1.0, 4.0)) = 1.6       // 梢部弯曲柔韧指数

        [Header(Wind Wave Macro)]
        _WindSpeed ("Macro Wind Speed (宏观风速)", Float) = 2.2                    // 宏观风速
        _WindStrength ("Wind Sway Strength (水平摆动幅度)", Range(0.0, 0.08)) = 0.025  // 水平摆动幅度
        _WindFrequency ("Wind Spatial Frequency (风场波长频度)", Float) = 1.8          // 风场波长频度
        _WindDirection ("Wind Direction (风向 XY)", Vector) = (1.0, 0.15, 0, 0)   // 风向 X, Y

        [Header(Local Noise and Chaos)]
        _NoiseMix ("Noise Mix (有序无序混合比)", Range(0.0, 1.0)) = 0.5                  // 无序/有序混合比，推荐0.5
        _GustSpeed ("Local Flutter Speed (高频微颤速率)", Float) = 5.5                 // 高频微风颤动速率
        _GustFrequency ("Local Flutter Frequency (叶片微褶频次)", Float) = 16.0        // 叶片微褶频次

        [Header(Foliage Color Mask)]
        _ColorMaskWeight ("Color/Lum Mask Influence (叶片特征权重)", Range(0.0, 1.0)) = 0.5    // 叶片色彩特征权重

        [Header(Light)]
        _AmbientFloor ("Ambient Floor (暗部保底)", Range(0.0, 0.6)) = 0.20
        _TileLightBoost ("Tile Light Boost (光场增强倍率)", Range(0.5, 3.0)) = 1.35

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
            float4 _MainTex_ST;

            #if USE_SHAPE_LIGHT_TYPE_0
            TEXTURE2D(_ShapeLightTexture0);
            SAMPLER(sampler_ShapeLightTexture0);
            #endif

            // 网格光照场由 TileGridLightmap 以全局量注入，严禁声明成材质属性（材质值会盖掉全局值）
            TEXTURE2D(_TileLightmap);
            SAMPLER(sampler_TileLightmap);

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
                float _AmbientFloor;
                float _TileLightBoost;
            CBUFFER_END

            // 全局量必须在 CBUFFER 之外：塞进 UnityPerMaterial 会破坏 SRP Batcher 的常量布局
            float4 _TileLightmapParams; // xy = 左下角世界坐标，zw = 宽高（世界单位）；zw 为 0 表示未装配
            float4 _CarryLightPos;      // xy = 随身光世界坐标，z = 每格衰减的 -log2，w = 强度
            float4 _CarryLightColor;    // rgb = 随身光颜色

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.color = input.color * _Color;
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                output.worldPos = TransformObjectToWorld(input.positionOS.xyz).xy;
                output.screenUV = ComputeScreenPos(output.positionCS);
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

                // 光照：草丛与地面必须同一套扩散，否则夜里只有地面会亮
                #if USE_SHAPE_LIGHT_TYPE_0
                float2 lightUV = input.screenUV.xy / input.screenUV.w;
                half4 envLight = SAMPLE_TEXTURE2D(_ShapeLightTexture0, sampler_ShapeLightTexture0, lightUV);
                #else
                half4 envLight = half4(1, 1, 1, 1);
                #endif

                envLight.rgb = max(envLight.rgb, half3(_AmbientFloor, _AmbientFloor, _AmbientFloor));

                float2 gridUV = (input.worldPos - _TileLightmapParams.xy) / max(_TileLightmapParams.zw, 0.0001);
                half onFlag = step(0.0001, _TileLightmapParams.z);
                half3 tileLight = SAMPLE_TEXTURE2D(_TileLightmap, sampler_TileLightmap, saturate(gridUV)).rgb * onFlag;

                float2 carryDelta = input.worldPos - _CarryLightPos.xy;
                half3 carryLight = _CarryLightColor.rgb * (_CarryLightPos.w * exp2(-length(carryDelta) * _CarryLightPos.z));

                half3 finalLight = max(max(envLight.rgb, tileLight * _TileLightBoost), carryLight * _TileLightBoost);

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
                    half4 rootCol = baseCol * input.color;
                    rootCol.rgb *= finalLight;
                    return rootCol;
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

                half4 litCol = finalCol * input.color;
                litCol.rgb *= finalLight;

                return litCol;
            }
            ENDHLSL
        }
    }
    FallBack "Universal Render Pipeline/2D/Sprite-Unlit-Default"
}