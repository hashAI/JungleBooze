// Editor-only measurement shader for PerfOverdraw (performance-engineer; docs/perf/2026-10-iphone12-readiness.md).
// Pass 0: depth only (ZWrite On), optional alpha clip from the source material's coverage texture.
// Pass 1: +1 per fragment that passes ZTest LEqual (shaded fragments of alpha-tested and blended draws).
// Pass 2: +1 per fragment that passes ZTest Equal (opaque pixels that survive hidden-surface removal).
// _Card = 1 rebuilds the camera-facing Atmos Card quads (centre in POSITION, axis in NORMAL, corner in UV0, size in
// UV1) as AtmosCard.shader does; _MistPull = 1 adds the mist pull toward the camera. Never in a player build.
Shader "Hidden/JungleBooze/Perf Overdraw"
{
    Properties
    {
        _CoverTex("Coverage", 2D) = "white" {}
        _CoverChannel("Coverage channel (0 = A, 1 = R)", Float) = 0
        _Cutoff("Cutoff", Float) = 0.5
        _Clip("Clip", Float) = 0
        _Card("Atmos Card", Float) = 0
        _MistPull("Mist Pull", Float) = 0
        _PerfCull("Cull", Float) = 0
    }

    CGINCLUDE
    #include "UnityCG.cginc"
    sampler2D _CoverTex;
    float4 _CoverTex_ST;
    float _CoverChannel;
    float _Cutoff;
    float _Clip;
    float _Card;
    float _MistPull;
    float4 _PerfCameraPos;

    struct appdata
    {
        float4 vertex : POSITION;
        float3 normal : NORMAL;
        float2 uv : TEXCOORD0;
        float4 size : TEXCOORD1;
    };

    struct v2f
    {
        float4 pos : SV_POSITION;
        float2 uv : TEXCOORD0;
    };

    v2f vert(appdata v)
    {
        v2f o;
        float3 world = mul(unity_ObjectToWorld, float4(v.vertex.xyz, 1.0)).xyz;
        if (_Card > 0.5)
        {
            float3 axis = normalize(mul((float3x3)unity_ObjectToWorld, v.normal));
            float3 toCam = _PerfCameraPos.xyz - world;
            float dist = length(toCam);
            toCam /= max(dist, 1e-4);
            world += toCam * min(v.size.x * 0.35, dist * 0.4) * _MistPull;
            float3 side = cross(axis, toCam);
            float sl = length(side);
            side = sl > 1e-3 ? side / sl : normalize(cross(axis, float3(0, 0, 1)));
            world = world + side * (v.uv.x * v.size.x) + axis * ((v.uv.y - 0.5) * v.size.y);
        }

        o.pos = mul(UNITY_MATRIX_VP, float4(world, 1.0));
        o.uv = TRANSFORM_TEX(v.uv, _CoverTex);
        return o;
    }

    float4 fragDepth(v2f i) : SV_Target
    {
        if (_Clip > 0.5)
        {
            float4 c = tex2D(_CoverTex, i.uv);
            clip((_CoverChannel > 0.5 ? c.r : c.a) - _Cutoff);
        }

        return 0;
    }

    float4 fragCount(v2f i) : SV_Target
    {
        return float4(1, 0, 0, 0);
    }
    ENDCG

    SubShader
    {
        Tags { "RenderType" = "Opaque" }

        Pass
        {
            ColorMask 0
            ZWrite On
            ZTest LEqual
            Cull [_PerfCull]

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment fragDepth
            ENDCG
        }

        Pass
        {
            ZWrite Off
            ZTest LEqual
            Cull [_PerfCull]
            Blend One One

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment fragCount
            ENDCG
        }

        Pass
        {
            ZWrite Off
            ZTest Equal
            Cull [_PerfCull]
            Blend One One

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment fragCount
            ENDCG
        }
    }

    FallBack Off
}
