// AURELIA look test (ADR 0004): skybox from an HDRI cubemap (Poly Haven, imported as a cube by
// LookTestAssetImportRules). Exposure, tint and rotation for matching the sun. Also feeds the environment bake
// (ambient SH and the default reflection probe), so lighting and sky always agree.
Shader "JungleBooze/Sky HDRI"
{
    Properties
    {
        [NoScaleOffset] _Tex("HDRI Cubemap", Cube) = "grey" {}
        _Tint("Tint", Color) = (1, 1, 1, 1)
        _Exposure("Exposure", Range(0, 8)) = 1
        _Rotation("Rotation (deg)", Range(0, 360)) = 0
    }

    SubShader
    {
        Tags { "Queue" = "Background" "RenderType" = "Background" "PreviewType" = "Skybox" "RenderPipeline" = "UniversalPipeline" }
        Cull Off
        ZWrite Off

        Pass
        {
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Tint;
                half _Exposure;
                float _Rotation;
            CBUFFER_END

            TEXTURECUBE(_Tex); SAMPLER(sampler_Tex);

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 direction : TEXCOORD0;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                float angle = _Rotation * (PI / 180.0);
                float s = sin(angle);
                float c = cos(angle);
                float3 p = input.positionOS.xyz;
                output.direction = float3(c * p.x - s * p.z, p.y, s * p.x + c * p.z);
                output.positionCS = TransformObjectToHClip(p);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half3 sky = SAMPLE_TEXTURECUBE_LOD(_Tex, sampler_Tex, input.direction, 0).rgb;
                return half4(sky * _Tint.rgb * _Exposure, 1.0h);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
