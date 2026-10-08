// AURELIA look test (ADR 0004): soft spray and mist particles at the waterfall foot. Unlit, alpha-blended,
// texture times particle color, lit only by the ambient SH so it matches the scene's brightness. Keep counts low
// (overdraw is the cost on mobile GPUs).
Shader "JungleBooze/Mist"
{
    Properties
    {
        [MainTexture] _BaseMap("Soft Particle", 2D) = "white" {}
        [MainColor] _BaseColor("Color", Color) = (1, 1, 1, 0.5)
        _AmbientBoost("Ambient Boost", Range(0, 4)) = 1.4
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor;
                half _AmbientBoost;
            CBUFFER_END

            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                half4 color : TEXCOORD1;
                half fogFactor : TEXCOORD2;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                output.color = input.color * _BaseColor;
                output.fogFactor = ComputeFogFactor(output.positionCS.z);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half4 tex = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv);
                half3 light = SampleSH(half3(0.0h, 1.0h, 0.0h)) * _AmbientBoost;
                half3 color = MixFog(tex.rgb * input.color.rgb * light, input.fogFactor);
                return half4(color, tex.a * input.color.a);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
