#ifndef CUSTOM_LIT_PASS_INCLUDED
#define CUSTOM_LIT_PASS_INCLUDED

#include "../ShaderLibrary/Surface.hlsl"
#include "../ShaderLibrary/Shadows.hlsl"
#include "../ShaderLibrary/Light.hlsl"
#include "../ShaderLibrary/BRDF.hlsl"
#include "../ShaderLibrary/GI.hlsl"
#include "../ShaderLibrary/Lighting.hlsl"

struct Attributes {
	half3 positionOS : POSITION;
	half3 normalOS : NORMAL;
	half4 tangentOS : TANGENT;
	half2 baseUV : TEXCOORD0;
	GI_ATTRIBUTE_DATA
	UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct Varyings {
	half4 positionCS : SV_POSITION;
	half3 positionWS : VAR_POSITION;
	half3 normalWS : VAR_NORMAL;
	half2 baseUV : VAR_BASE_UV;
	half specularPower : TEXCOORD2;
	#if defined(_NORMAL_MAP)
	float4 tangentWS : VAR_TANGENT;
	#endif
	GI_VARYINGS_DATA
	UNITY_VERTEX_INPUT_INSTANCE_ID
};

Varyings LitPassVertex (Attributes input) {
	Varyings output;
	UNITY_SETUP_INSTANCE_ID(input);
	UNITY_TRANSFER_INSTANCE_ID(input, output);
	TRANSFER_GI_DATA(input, output);
	output.positionWS = TransformObjectToWorld(input.positionOS);
	output.positionCS = TransformWorldToHClip(output.positionWS);
	output.normalWS = TransformObjectToWorldNormal(input.normalOS);
	#if defined(_NORMAL_MAP)
	output.tangentWS = float4(
		TransformObjectToWorldDir(input.tangentOS.xyz), input.tangentOS.w
	);
	#endif
	
	half4 baseST = UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _BaseMap_ST);
	output.baseUV = input.baseUV * baseST.xy + baseST.zw;

	#ifdef _VERTEX_LIGHTING_ON

	// Light light = GetDirectionalLight(0);
	// // Compute the half-vector and specular power only if vertex lighting is enabled
	// const float3 viewDir = SafeNormalize(_WorldSpaceCameraPos - output.positionWS);
	// const float3 lightDir = SafeNormalize(light.direction.xyz); // Assuming a single directional light for simplicity
	// const float3 halfVector = SafeNormalize(viewDir + lightDir);
	//
	// // Compute the specular power based on the smoothness
	// const half smoothness = UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _Smoothness);
	// const half extraSpecularPower = UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _SpecularPower);
	// output.specularPower = pow(saturate(dot(output.normalWS, halfVector)), GetSmoothnessPower(smoothness)) * extraSpecularPower;
	//output.specularPower = 0;
	#else
	output.specularPower = 0.0;
	#endif
	
	return output;
}

float4 LitPassFragment (Varyings input) : SV_TARGET {
	
	UNITY_SETUP_INSTANCE_ID(input);
	const half4 baseMap = GetBase(input.baseUV);
	const half4 maskMap = SAMPLE_TEXTURE2D(_MaskMap, sampler_BaseMap, input.baseUV);
	const half3 emission = GetEmission(input.baseUV);
	half4 base = baseMap;
	
	#if defined(_CLIPPING)
		clip(base.a - UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _Cutoff)); 
	#endif

	Surface surface;
	surface.position = input.positionWS;
	InputConfig config = GetInputConfig(input.baseUV);
	#if defined(_NORMAL_MAP)
	surface.normal = NormalTangentToWorld(
		GetNormalTS(config), input.normalWS, input.tangentWS
	);
	surface.interpolatedNormal = input.normalWS;
	#else
	surface.normal = normalize(input.normalWS);
	surface.interpolatedNormal = surface.normal;
	#endif
	surface.viewDirection = normalize(_WorldSpaceCameraPos - input.positionWS);
	surface.depth = -TransformWorldToView(input.positionWS).z;
	surface.color = base.rgb;
	surface.alpha = base.a;
	surface.metallic = UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _Metallic) * maskMap.b;
	surface.smoothness = UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _Smoothness) * maskMap.g;
	//surface.dither = InterleavedGradientNoise(input.positionCS.xy, 0);
	
	#ifdef _MATCAP_ON
	float3 normal_view_space = normalize(mul(surface.normal, (float3x3)_WorldToViewMatrix));
	const float2 uv = float2(0.5 + atan2(normal_view_space.z, normal_view_space.x) / (2 * PI), 0.5 - asin(normal_view_space.y) / PI);
	surface.matcap = GetMatCap(uv) * UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _MatCapPower);
	#endif

	#ifdef _VERTEX_LIGHTING_ON
	GI gi = GetGI(GI_FRAGMENT_DATA(input), surface);
	half3 color = GetMobileLightingVertex(surface, input.specularPower,gi);
	#else
	surface.specularPower = UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _SpecularPower);
	GI gi = GetGI(GI_FRAGMENT_DATA(input), surface);
	half3 color = GetMobileLighting(surface,gi);
	#endif

	return half4(color + emission, surface.alpha);
}

#endif