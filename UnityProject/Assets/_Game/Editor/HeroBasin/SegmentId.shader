// Editor-only ID pass for the hero basin frame budgets (HeroBasinSegmentation): flat, unlit category colour, alpha
// cut from the source albedo where the source is cut-out or a backdrop layer, and a second colour for up-facing
// surfaces (arch strand tops). Never used in game.
Shader "Hidden/JungleBooze/Segment Id"
{
    Properties
    {
        _BaseMap("Alpha source", 2D) = "white" {}
        _IdColor("Id", Color) = (1, 0, 1, 1)
        _UpColor("Id (up-facing)", Color) = (1, 0, 1, 1)
        _UpThreshold("Up threshold (normal.y)", Float) = 2
        _UseAlpha("Use alpha", Float) = 0
        _Cutoff("Cutoff", Float) = 0.5
        _AlphaFromR("Alpha from R (mask maps)", Float) = 0
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }

        Pass
        {
            Name "SegmentId"
            Tags { "LightMode" = "UniversalForward" }
            Cull Off
            ZWrite On
            ZTest LEqual
            Blend Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _IdColor;
                half4 _UpColor;
                half _UpThreshold;
                half _UseAlpha;
                half _Cutoff;
                half _AlphaFromR;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                if (_UseAlpha > 0.5)
                {
                    half4 t = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv);
                    clip((_AlphaFromR > 0.5 ? t.r : t.a) - _Cutoff);
                }

                return normalize(input.normalWS).y > _UpThreshold ? _UpColor : _IdColor;
            }
            ENDHLSL
        }
    }
}
