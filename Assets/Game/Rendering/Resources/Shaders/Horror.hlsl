// Shared by the game's shaders (section 4.4): HDRP's variables and transforms, the camera, scene depth, the
// exposure, and the fog as the look sets it (so glows and shafts fade into it the way the world does).
#ifndef HORROR_INCLUDED
#define HORROR_INCLUDED

#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
#include "Packages/com.unity.render-pipelines.high-definition/Runtime/ShaderLibrary/ShaderVariables.hlsl"

// Set every frame by the look (LookController): the fog's mean free path (metres) and its colour as it shows.
float _HorrorFogFreePath;
float4 _HorrorFogColor;

// Positions in HDRP are camera-relative: the camera is at the origin of "world" space here.
float3 HorrorObjectCentreRWS() { return TransformObjectToWorld(float3(0, 0, 0)); }

// The scene's depth behind this pixel, in metres from the camera.
float HorrorSceneDepth(float4 positionCS)
{
    return LinearEyeDepth(LoadCameraDepth(uint2(positionCS.xy)), _ZBufferParams);
}

// How much of something this far away shows through the fog (1 near, 0 lost in it); amount 0 ignores the fog.
float HorrorFogTransmittance(float distance, float amount)
{
    float path = max(_HorrorFogFreePath, 0.01);
    return lerp(1.0, exp(-distance / path), saturate(amount));
}

// Colours given in the same units as HDRP's emissive colours (they're pre-exposed like everything else).
float3 HorrorExposed(float3 c) { return c * GetCurrentExposureMultiplier(); }

// Cheap value noise, for flicker and dust.
float HorrorHash(float3 p)
{
    p = frac(p * 0.3183099 + 0.1);
    p *= 17.0;
    return frac(p.x * p.y * p.z * (p.x + p.y + p.z));
}

float HorrorNoise(float3 x)
{
    float3 i = floor(x);
    float3 f = frac(x);
    f = f * f * (3.0 - 2.0 * f);
    return lerp(lerp(lerp(HorrorHash(i + float3(0, 0, 0)), HorrorHash(i + float3(1, 0, 0)), f.x),
                     lerp(HorrorHash(i + float3(0, 1, 0)), HorrorHash(i + float3(1, 1, 0)), f.x), f.y),
                lerp(lerp(HorrorHash(i + float3(0, 0, 1)), HorrorHash(i + float3(1, 0, 1)), f.x),
                     lerp(HorrorHash(i + float3(0, 1, 1)), HorrorHash(i + float3(1, 1, 1)), f.x), f.y), f.z);
}

#endif
