// AURELIA look test v2 (ENVIRONMENT_STRATEGY 4.4): atmosphere shared by every project shader.
// Globals are set by LookTestAtmosphere (runtime and editor). Include after URP Core.hlsl.
//
// Height fog: density(y) = a * exp(-b * (y - base)), integrated analytically along the view ray, so hollows,
// water and the valley sit in denser haze and tall landforms fade toward their base. The fog color blends from a
// cool haze (away from the sun) to a warm in-scatter glow (toward the sun). The sky uses the same function with a
// fixed distance, which gives a pale horizon band that matches the geometry.
//
// Canopy light: vertex color B = how much canopy is overhead (0 open sky, 1 full roof), baked by the scene builder.
// Under the roof the sun reaches a surface only through a dappled pattern (a tiling texture projected along the
// sun direction: a light cookie that exists only under the canopy), and the ambient light drops and turns green.
// No extra passes, no depth texture.
#ifndef JB_ATMOSPHERE_INCLUDED
#define JB_ATMOSPHERE_INCLUDED

half4 _JBFogColor;
half4 _JBFogSunColor;
float4 _JBFogParams;   // x density at base (1/m), y height falloff (1/m), z base height (world y), w start distance (m)
float4 _JBFogParams2;  // x sun glow power, y max opacity, z sky distance (m)
float4 _JBCanopyParams; // x 1 / dapple size (1/m), y lit fraction under full canopy, z ambient under full canopy, w scroll (m/s)
half4 _JBCanopyTint;
half4 _JBShaftColor;
half4 _JBMistColor;
float4 _JBSunDirection; // xyz: toward the sun
TEXTURE2D(_JBDappleTex); SAMPLER(sampler_JBDappleTex);

// Fog amount (0..1) for a ray from the camera of length dist along direction dir (normalized).
float JBFogAmount(float3 dir, float dist)
{
    float d = max(dist - _JBFogParams.w, 0.0);
    float b = max(_JBFogParams.y, 1e-5);
    float startY = _WorldSpaceCameraPos.y + dir.y * _JBFogParams.w;
    float k = b * dir.y * d;
    // (1 - e^-k) / k, stable near 0, clamped for long downward rays.
    float integral = abs(k) > 1e-3 ? (1.0 - exp(-clamp(k, -30.0, 30.0))) / k : 1.0 - 0.5 * k;
    float depth = _JBFogParams.x * exp(-b * clamp(startY - _JBFogParams.z, -60.0, 400.0)) * d * integral;
    return saturate(1.0 - exp(-max(depth, 0.0))) * _JBFogParams2.y;
}

half3 JBFogColor(float3 dir)
{
    half sun = pow(max(saturate(dot(dir, _JBSunDirection.xyz)), 1e-4h), max(_JBFogParams2.x, 1.0));
    return lerp(_JBFogColor.rgb, _JBFogSunColor.rgb, sun);
}

half3 JBApplyFog(half3 color, float3 positionWS)
{
    float3 v = positionWS - _WorldSpaceCameraPos;
    float dist = length(v);
    float3 dir = v / max(dist, 1e-4);
    return lerp(color, JBFogColor(dir), JBFogAmount(dir, dist));
}

// Additive or premultiplied effects fade out in fog instead of turning fog-colored.
half JBFogKeep(float3 positionWS)
{
    float3 v = positionWS - _WorldSpaceCameraPos;
    float dist = length(v);
    return 1.0h - JBFogAmount(v / max(dist, 1e-4), dist);
}

// Sun visibility (0..1) under the canopy for a surface at positionWS with canopy cover 0..1.
half JBCanopyLight(float3 positionWS, half cover)
{
    float3 toSun = _JBSunDirection.xyz;
    float2 projected = positionWS.xz - positionWS.y * toSun.xz / max(toSun.y, 0.2);
    float2 uv = projected * _JBCanopyParams.x + _Time.y * _JBCanopyParams.w;
    half pattern = SAMPLE_TEXTURE2D(_JBDappleTex, sampler_JBDappleTex, uv).r;
    half lit = smoothstep(1.0h - _JBCanopyParams.y - 0.08h, 1.0h - _JBCanopyParams.y + 0.08h, pattern);
    return lerp(1.0h, lit, saturate(cover));
}

half3 JBCanopyAmbient(half cover)
{
    return lerp(half3(1.0h, 1.0h, 1.0h), _JBCanopyTint.rgb * _JBCanopyParams.z, saturate(cover));
}

#endif
