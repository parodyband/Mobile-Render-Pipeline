#ifndef CUSTOM_COMMON_INCLUDED
#define CUSTOM_COMMON_INCLUDED

#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/CommonMaterial.hlsl"
#include "UnityInput.hlsl"

#define UNITY_MATRIX_M unity_ObjectToWorld
#define UNITY_MATRIX_I_M unity_WorldToObject
#define UNITY_MATRIX_V unity_MatrixV
#define UNITY_MATRIX_I_V unity_MatrixInvV
#define UNITY_MATRIX_VP unity_MatrixVP
#define UNITY_MATRIX_P glstate_matrix_projection
#define UNITY_PREV_MATRIX_M unity_prev_MatrixM
#define UNITY_PREV_MATRIX_I_M unity_prev_MatrixIM

#if defined(_SHADOW_MASK_ALWAYS) || defined(_SHADOW_MASK_DISTANCE)
	#define SHADOWS_SHADOWMASK
#endif

#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/UnityInstancing.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/SpaceTransforms.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Packing.hlsl"

SAMPLER(sampler_linear_clamp);
SAMPLER(sampler_point_clamp);

bool IsOrthographicCamera () {
	return unity_OrthoParams.w;
}

float OrthographicDepthBufferToLinear (float rawDepth) {
	#if UNITY_REVERSED_Z
		rawDepth = 1.0 - rawDepth;
	#endif
	return (_ProjectionParams.z - _ProjectionParams.y) * rawDepth + _ProjectionParams.y;
}

#include "Fragment.hlsl"

float Square (float x) {
	return x * x;
}

float DistanceSquared(float3 pA, float3 pB) {
	return dot(pA - pB, pA - pB);
}

void ClipLOD (Fragment fragment, float fade) {
	#if defined(LOD_FADE_CROSSFADE)
		float dither = InterleavedGradientNoise(fragment.positionSS, 0);
		clip(fade + (fade < 0.0 ? dither : -dither));
	#endif
}

real3 DecodeNormal (float4 sample, float scale) {
	// #if defined(UNITY_NO_DXT5nm)
	//     return normalize(UnpackNormalRGB(sample, scale));
	// #else
	//     return normalize(UnpackNormalmapRGorAG(sample, scale));
	// #endif
	return normalize(UnpackNormalScale(sample,scale));
}

float3 NormalTangentToWorld (float3 normalTS, float3 normalWS, float4 tangentWS) {
	float3x3 tangentToWorld =
		CreateTangentToWorld(normalWS, tangentWS.xyz, tangentWS.w);
	return TransformTangentToWorld(normalTS, tangentToWorld);
}
float4 invLerp(real4 from, real4 to, real4 value) {
	return (value - from) / (to - from);
}
float invLerp_F(float from, float to, float value) {
	return (value - from) / (to - from);
}
float remap(real origFrom, real origTo, real targetFrom, real targetTo, real value){
	const float rel = invLerp_F(origFrom, origTo, value);
	return lerp(targetFrom, targetTo, rel);
}
float4 remap(real4 origFrom, real4 origTo, real4 targetFrom, real4 targetTo, real4 value){
	const real4 rel = invLerp(origFrom, origTo, value);
	return lerp(targetFrom, targetTo, rel);
}

float2 ScreenSpaceUV(float2 PositionCSXY) {
	float2 screenUV = PositionCSXY;
	screenUV.x /= _ScreenParams.x;
	screenUV.y /= _ScreenParams.y;
	//Flip y if needed
	#if UNITY_UV_STARTS_AT_TOP
	#else
	screenUV.y = 1.0 - screenUV.y;
	#endif
    
	return screenUV;
}
#endif