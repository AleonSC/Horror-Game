// What every Horror shader needs in HDRP: its space transforms, the camera, the screen and the depth
// buffer. HDRP draws in camera-relative world space (the camera at the origin), so positions from
// HorrorWorld are relative to the camera; HorrorAbsolute turns one back into true world space, for things
// anchored in the world (noise, heights, positions sent from scripts).
//
// The colours these shaders write go straight to the screen, unaffected by HDRP's exposure, so they look
// the same however bright or dark the scene's lighting is.
#ifndef HORROR_HDRP_INCLUDED
#define HORROR_HDRP_INCLUDED

#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
#include "Packages/com.unity.render-pipelines.high-definition/Runtime/ShaderLibrary/ShaderVariables.hlsl"

/// Object space to the (camera-relative) world space HDRP draws in.
float3 HorrorWorld(float3 positionOS) { return TransformObjectToWorld(positionOS); }

/// A direction from object space to world space, keeping the object's scale.
float3 HorrorWorldVector(float3 vectorOS) { return mul((float3x3)GetObjectToWorldMatrix(), vectorOS); }

/// World space (camera-relative) to clip space.
float4 HorrorClip(float3 positionRWS) { return TransformWorldToHClip(positionRWS); }

/// Object space straight to clip space.
float4 HorrorObjectToClip(float3 positionOS) { return TransformWorldToHClip(TransformObjectToWorld(positionOS)); }

/// A camera-relative position back in true world space.
float3 HorrorAbsolute(float3 positionRWS) { return GetAbsolutePositionWS(positionRWS); }

/// The camera's position, in the same (camera-relative) space as HorrorWorld.
float3 HorrorCameraPos() { return GetCurrentViewPosition(); }

/// The camera's position in true world space.
float3 HorrorCameraAbsolute() { return GetAbsolutePositionWS(GetCurrentViewPosition()); }

/// Where on the screen a pixel is, 0-1 (pass the fragment's SV_POSITION).
float2 HorrorScreenUV(float4 positionCS) { return positionCS.xy * _ScreenSize.zw; }

/// How far away (along the view) the nearest solid thing behind this pixel is.
float HorrorSceneEyeDepth(float4 positionCS) { return LinearEyeDepth(LoadCameraDepth(uint2(positionCS.xy)), _ZBufferParams); }

/// How far away (along the view) a camera-relative world position is.
float HorrorEyeDepth(float3 positionRWS) { return -TransformWorldToView(positionRWS).z; }

#endif
