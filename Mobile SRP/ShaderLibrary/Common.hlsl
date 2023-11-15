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
#define UNITY_PREV_MATRIX_M unity_prev_MatrixM
#define UNITY_PREV_MATRIX_I_M unity_prev_MatrixIM
#define UNITY_MATRIX_P glstate_matrix_projection

#if defined(_SHADOW_MASK_ALWAYS) || defined(_SHADOW_MASK_DISTANCE)
    #define SHADOWS_SHADOWMASK
#endif

#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/UnityInstancing.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/SpaceTransforms.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Packing.hlsl"

half Square (half x) {
    return x * x;
}

half DistanceSquared(half3 pA, half3 pB) {
    return dot(pA - pB, pA - pB);
}

half3 DecodeNormal (half4 sample) {
    #if defined(UNITY_NO_DXT5nm)
    return normalize(UnpackNormalRGBNoScale(sample));
    #else
    return normalize(UnpackNormal(sample));
    #endif
}

real3 NormalTangentToWorld (half3 normalTS, half3 normalWS, half4 tangentWS) {
    const half3x3 tangentToWorld = CreateTangentToWorld(normalWS, tangentWS.xyz, tangentWS.w);
    return TransformTangentToWorld(normalTS, tangentToWorld, true);
}
real invLerp(real from, real to, real value){
    return (value - from) / (to - from);
}
real4 invLerp(real4 from, real4 to, real4 value) {
    return (value - from) / (to - from);
}
real remap(real origFrom, real origTo, real targetFrom, real targetTo, real value){
    const float rel = invLerp(origFrom, origTo, value);
    return lerp(targetFrom, targetTo, rel);
}
real4 remap(real4 origFrom, real4 origTo, real4 targetFrom, real4 targetTo, real4 value){
    const real4 rel = invLerp(origFrom, origTo, value);
    return lerp(targetFrom, targetTo, rel);
}

real2 ScreenSpaceUV(real2 PositionCSXY) {
    real2 screenUV = PositionCSXY;
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