// AURELIA painterly v4 (ADR 0011): painted cascade sheets for the short falls (spillways, travertine notches, the
// wide shelf cascade). One alpha-blended quad strip per fall instead of 2-4 layered procedural sheets plus an
// impact-foam disc: the painted row (FX_Cascade_P) carries the strands, gaps, lip roll and foot spray.
// Motion: the body of the painting slides down in two phases that cross-fade (a cheap flipbook along the flow); the
// lip and the foot spray stay put. Light: the painting is lit by the sun colour and sky ambient, plus project fog.
// UV0 = atlas UV (row rectangle), UV1.x = 0 at the lip .. 1 at the foot. Vertex color: R = phase seed, G = speed,
// A = opacity.
Shader "JungleBooze/Painted Sheet"
{
    Properties
    {
        [MainTexture] _MainTex("Painted Sheet (RGBA)", 2D) = "white" {}
        _FlowSpeed("Flow (atlas V per s)", Float) = 0.06
        _FlowSlide("Body Slide (atlas V per cycle)", Float) = 0.035
        _LitTint("Sun Share", Range(0, 2)) = 0.85
        _AmbientShare("Ambient Share", Range(0, 2)) = 0.55
        _Opacity("Opacity", Range(0, 1)) = 0.92
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

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "JBAtmosphere.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float _FlowSpeed;
                float _FlowSlide;
                half _LitTint;
                half _AmbientShare;
                half _Opacity;
            CBUFFER_END

            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                float2 uv1 : TEXCOORD1;
                half4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                half4 color : TEXCOORD2;
                half along : TEXCOORD3;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.uv = input.uv;
                output.color = input.color;
                output.along = input.uv1.x;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                // Body = between the lip roll and the foot spray: only there does the paint slide down.
                half body = smoothstep(0.08h, 0.25h, input.along) * (1.0h - smoothstep(0.62h, 0.85h, input.along));
                float cycle = _Time.y * _FlowSpeed * (0.7 + 0.6 * input.color.g) + input.color.r;
                float p0 = frac(cycle);
                float p1 = frac(cycle + 0.5);
                half w0 = 1.0h - abs(p0 * 2.0 - 1.0);
                float2 uv0 = input.uv + float2(0.0, p0 * _FlowSlide * body);
                float2 uv1 = input.uv + float2(0.0, p1 * _FlowSlide * body);
                half4 c = lerp(SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv1), SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv0), w0);

                Light mainLight = GetMainLight();
                half3 ambient = SampleSH(half3(0.0h, 1.0h, 0.0h));
                half3 lit = c.rgb * (mainLight.color * _LitTint + ambient * _AmbientShare);
                lit = JBApplyFog(lit, input.positionWS);
                return half4(lit, c.a * _Opacity * input.color.a);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
