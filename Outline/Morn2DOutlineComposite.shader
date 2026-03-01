Shader "Morn2D/OutlineComposite"
{
    Properties
    {
        [HDR] _OutlineColor ("Outline Color", Color) = (1, 1, 1, 1)
        _OutlineWidth ("Outline Width", Float) = 3
        [HDR] _GlowColor ("Glow Color", Color) = (1, 1, 1, 1)
        _GlowWidth ("Glow Width", Float) = 0
    }
    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            Name "Morn2DOutlineComposite"

            ZWrite Off
            ZTest Always
            Cull Off
            Blend One OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            TEXTURE2D(_Morn2DOutlineSilhouetteTex);
            SAMPLER(sampler_Morn2DOutlineSilhouetteTex);
            float4 _Morn2DOutlineSilhouetteTex_TexelSize;

            CBUFFER_START(UnityPerMaterial)
                float4 _OutlineColor;
                float _OutlineWidth;
                float4 _GlowColor;
                float _GlowWidth;
            CBUFFER_END

            #define SAMPLE_COUNT 32

            float ComputeRawCoverage(float2 uv, float radius, float2 texelSize)
            {
                float coverage = 0;
                UNITY_UNROLL
                for (int i = 0; i < SAMPLE_COUNT; i++)
                {
                    float angle = (float)i / SAMPLE_COUNT * 6.28318530718;
                    float2 dir = float2(cos(angle), sin(angle));
                    float2 offset = dir * radius * texelSize;
                    half neighborAlpha = SAMPLE_TEXTURE2D(
                        _Morn2DOutlineSilhouetteTex, sampler_Morn2DOutlineSilhouetteTex, uv + offset).r;
                    coverage += smoothstep(0.3, 0.7, neighborAlpha);
                }
                return coverage / SAMPLE_COUNT;
            }

            Varyings vert(uint vertexID : SV_VertexID)
            {
                Varyings OUT;
                float2 uv = float2((vertexID << 1) & 2, vertexID & 2);
                OUT.positionCS = float4(uv * 2.0 - 1.0, UNITY_NEAR_CLIP_VALUE, 1.0);
                #if UNITY_UV_STARTS_AT_TOP
                OUT.uv = float2(uv.x, 1.0 - uv.y);
                #else
                OUT.uv = uv;
                #endif
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                if (_OutlineWidth < 0.01 && _GlowWidth < 0.01)
                {
                    return half4(0, 0, 0, 0);
                }

                float2 texelSize = _Morn2DOutlineSilhouetteTex_TexelSize.xy;

                // 内側エッジ: 中心+8方向(1.5px半径)の平均で約3px幅のなめらかな遷移を生成
                half centerAlpha = SAMPLE_TEXTURE2D(
                    _Morn2DOutlineSilhouetteTex, sampler_Morn2DOutlineSilhouetteTex, IN.uv).r;
                float innerCoverage = smoothstep(0.3, 0.7, centerAlpha);
                UNITY_UNROLL
                for (int j = 0; j < 8; j++)
                {
                    float a = (float)j / 8 * 6.28318530718;
                    float2 off = float2(cos(a), sin(a)) * 1.5 * texelSize;
                    half n = SAMPLE_TEXTURE2D(
                        _Morn2DOutlineSilhouetteTex, sampler_Morn2DOutlineSilhouetteTex, IN.uv + off).r;
                    innerCoverage += smoothstep(0.3, 0.7, n);
                }
                innerCoverage /= 9.0;
                float innerMask = 1.0 - smoothstep(0.2, 0.8, innerCoverage);

                if (innerMask < 0.001)
                {
                    return half4(0, 0, 0, 0);
                }

                // メインアウトライン: シャープな閾値で均一な不透明度（外縁のみ極薄AA）
                float outlineRaw = ComputeRawCoverage(IN.uv, _OutlineWidth, texelSize);
                float outlineCoverage = smoothstep(0.0, 0.1, outlineRaw);
                float outlineAlpha = innerMask * outlineCoverage * _OutlineColor.a;

                // Glow: ソフトな閾値で外側に向かってなめらかにフェード
                float glowAlpha = 0;
                if (_GlowWidth > 0.01)
                {
                    float glowRadius = _OutlineWidth + _GlowWidth;
                    float glowRaw = ComputeRawCoverage(IN.uv, glowRadius, texelSize);
                    float glowCoverage = smoothstep(0.0, 0.5, glowRaw);
                    glowAlpha = innerMask * saturate(glowCoverage - outlineCoverage) * _GlowColor.a;
                }

                // プリマルチプライド出力: OutlineとGlowを個別のHDR色で合成
                half3 outlineRgb = _OutlineColor.rgb * outlineAlpha;
                half3 glowRgb = _GlowColor.rgb * glowAlpha;
                float finalAlpha = saturate(outlineAlpha + glowAlpha);

                if (finalAlpha < 0.001)
                {
                    return half4(0, 0, 0, 0);
                }

                return half4(outlineRgb + glowRgb, finalAlpha);
            }
            ENDHLSL
        }
    }
}
