#ifndef CUSTOM_GI_INCLUDED
#define CUSTOM_GI_INCLUDED

#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/EntityLighting.hlsl"

TEXTURE2D(unity_Lightmap);
SAMPLER(samplerunity_Lightmap);

TEXTURE3D_FLOAT(unity_ProbeVolumeSH);
SAMPLER(samplerunity_ProbeVolumeSH);

TEXTURE2D(unity_LightmapInd);
SAMPLER(samplerunity_LightmapInd);

#define LIGHTMAP_NAME unity_Lightmap
#define LIGHTMAP_INDIRECTION_NAME unity_LightmapInd
#define LIGHTMAP_SAMPLER_NAME samplerunity_Lightmap
#define LIGHTMAP_SAMPLE_EXTRA_ARGS staticLightmapUV

#if defined(LIGHTMAP_ON)
	#define GI_ATTRIBUTE_DATA float2 lightMapUV : TEXCOORD1;
	#define GI_VARYINGS_DATA float2 lightMapUV : VAR_LIGHT_MAP_UV;
	#define TRANSFER_GI_DATA(input, output) \
		output.lightMapUV = input.lightMapUV * \
		unity_LightmapST.xy + unity_LightmapST.zw;
	#define GI_FRAGMENT_DATA(input) input.lightMapUV
#else
	#define GI_ATTRIBUTE_DATA
	#define GI_VARYINGS_DATA
	#define TRANSFER_GI_DATA(input, output)
	#define GI_FRAGMENT_DATA(input) 0.0
#endif

struct GI {
	float3 diffuse;
	float3 normal;
};

float3 SampleLightMap (float2 staticLightmapUV, half3 normalWS) {

	#ifdef UNITY_LIGHTMAP_FULL_HDR
	bool encodedLightmap = false;
	#else
	bool encodedLightmap = true;
	#endif
	float3 diffuseLighting = 0;
	half4 transformCoords = half4(1, 1, 0, 0);
	half4 decodeInstructions = half4(LIGHTMAP_HDR_MULTIPLIER, LIGHTMAP_HDR_EXPONENT, 0.0h, 0.0h);
	#if defined(LIGHTMAP_ON) && defined(DIRLIGHTMAP_COMBINED)
	diffuseLighting = SampleDirectionalLightmap(TEXTURE2D_LIGHTMAP_ARGS(LIGHTMAP_NAME, LIGHTMAP_SAMPLER_NAME),
		TEXTURE2D_LIGHTMAP_ARGS(LIGHTMAP_INDIRECTION_NAME, LIGHTMAP_SAMPLER_NAME),
		LIGHTMAP_SAMPLE_EXTRA_ARGS, transformCoords, normalWS, encodedLightmap, decodeInstructions);
	#elif defined(LIGHTMAP_ON)
	diffuseLighting = SampleSingleLightmap(TEXTURE2D_LIGHTMAP_ARGS(LIGHTMAP_NAME, LIGHTMAP_SAMPLER_NAME), LIGHTMAP_SAMPLE_EXTRA_ARGS, transformCoords, encodedLightmap, decodeInstructions);
	#endif

	#if defined(DYNAMICLIGHTMAP_ON) && defined(DIRLIGHTMAP_COMBINED)
	diffuseLighting += SampleDirectionalLightmap(TEXTURE2D_ARGS(unity_DynamicLightmap, samplerunity_DynamicLightmap),
		TEXTURE2D_ARGS(unity_DynamicDirectionality, samplerunity_DynamicLightmap),
		dynamicLightmapUV, transformCoords, normalWS, false, decodeInstructions);
	#elif defined(DYNAMICLIGHTMAP_ON)
	diffuseLighting += SampleSingleLightmap(TEXTURE2D_ARGS(unity_DynamicLightmap, samplerunity_DynamicLightmap),
		dynamicLightmapUV, transformCoords, false, decodeInstructions);
	#endif

	return diffuseLighting;
}

float3 SampleLightProbe (Surface surfaceWS) {
	#if defined(LIGHTMAP_ON)
		return 0.0;
	#else
		if (unity_ProbeVolumeParams.x) {
			return SampleProbeVolumeSH4(
				TEXTURE3D_ARGS(unity_ProbeVolumeSH, samplerunity_ProbeVolumeSH),
				surfaceWS.position, surfaceWS.normal,
				unity_ProbeVolumeWorldToObject,
				unity_ProbeVolumeParams.y, unity_ProbeVolumeParams.z,
				unity_ProbeVolumeMin.xyz, unity_ProbeVolumeSizeInv.xyz
			);
		}
		else {
			float4 coefficients[7];
			coefficients[0] = unity_SHAr;
			coefficients[1] = unity_SHAg;
			coefficients[2] = unity_SHAb;
			coefficients[3] = unity_SHBr;
			coefficients[4] = unity_SHBg;
			coefficients[5] = unity_SHBb;
			coefficients[6] = unity_SHC;
			return max(0.0, SampleSH9(coefficients, surfaceWS.normal));
		}
	#endif
}

GI GetGI (float2 lightMapUV, Surface surfaceWS) {
	GI gi;
	gi.diffuse = SampleLightMap(lightMapUV,surfaceWS.normal) + SampleLightProbe(surfaceWS);
	return gi;
}

#endif