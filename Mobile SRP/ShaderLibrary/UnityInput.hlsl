#ifndef CUSTOM_UNITY_INPUT_INCLUDED
#define CUSTOM_UNITY_INPUT_INCLUDED

CBUFFER_START(UnityPerDraw)
	float4x4 unity_ObjectToWorld;
	float4x4 unity_WorldToObject;
	float4 unity_LODFade;
	real4 unity_WorldTransformParams;
	float4 unity_RenderingLayer;

	real4 unity_LightData;
	real4 unity_LightIndices[2];

	float4 unity_ProbesOcclusion;
	float4 unity_SpecCube0_HDR;
	
	float4 unity_LightmapST;
	float4 unity_DynamicLightmapST;
	float4 unity_SHAr;
	float4 unity_SHAg;
	float4 unity_SHAb;
	float4 unity_SHBr;
	float4 unity_SHBg;
	float4 unity_SHBb;
	float4 unity_SHC;

	float4 unity_DeltaTime;
	float4 unity_ProbeVolumeParams;
	float4x4 unity_ProbeVolumeWorldToObject;
	float4 unity_ProbeVolumeSizeInv;
	float4 unity_ProbeVolumeMin;

CBUFFER_END

CBUFFER_START(UnityPerCamera)
float4 _Time;
float4 _SinTime; // sin(t/8), sin(t/4), sin(t/2), sin(t)
float4 _CosTime; // cos(t/8), cos(t/4), cos(t/2), cos(t)
CBUFFER_END

float4x4 unity_MatrixVP;
float4x4 unity_MatrixV;
float4x4 unity_MatrixInvV;

float4x4 glstate_matrix_projection;

float3 _WorldSpaceCameraPos;
float3 _WorldSpaceCameraForward;
float4 unity_OrthoParams;
float4 _ProjectionParams;
float4 _ScreenParams;
float4 _ZBufferParams;
float4x4 unity_prev_MatrixM;
float4x4 unity_prev_MatrixIM;
#endif