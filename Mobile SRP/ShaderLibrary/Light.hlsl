#ifndef CUSTOM_LIGHT_INCLUDED
#define CUSTOM_LIGHT_INCLUDED

#define MAX_DIRECTIONAL_LIGHT_COUNT 4
#define MAX_OTHER_LIGHT_COUNT 6

CBUFFER_START(_CustomLight)
	int _DirectionalLightCount;
	half4 _DirectionalLightColors[MAX_DIRECTIONAL_LIGHT_COUNT];
	half4 _DirectionalLightDirections[MAX_DIRECTIONAL_LIGHT_COUNT];
	half4 _DirectionalLightShadowData[MAX_DIRECTIONAL_LIGHT_COUNT];

	int _OtherLightCount;
	half4 _OtherLightColors[MAX_OTHER_LIGHT_COUNT];
	half4 _OtherLightPositions[MAX_OTHER_LIGHT_COUNT];
	half4 _OtherLightDirections[MAX_OTHER_LIGHT_COUNT];
	half4 _OtherLightSpotAngles[MAX_OTHER_LIGHT_COUNT];
	half4 _OtherLightShadowData[MAX_OTHER_LIGHT_COUNT];
CBUFFER_END

struct Light {
	half3 color;
	half3 direction;
	float attenuation;
};

int GetDirectionalLightCount () {
	return _DirectionalLightCount;
}

int GetOtherLightCount () {
	return _OtherLightCount;
}

DirectionalShadowData GetDirectionalShadowData ( int lightIndex, ShadowData shadowData ) {
	DirectionalShadowData data;
	data.strength =
		_DirectionalLightShadowData[lightIndex].x * shadowData.strength;
	data.tileIndex =
		_DirectionalLightShadowData[lightIndex].y + shadowData.cascadeIndex;
	data.normalBias = _DirectionalLightShadowData[lightIndex].z;
	return data;
}

Light GetDirectionalLight ( int index, Surface surfaceWS, ShadowData shadowData ) {
	Light light;
	light.color = _DirectionalLightColors[index].rgb;
	light.direction = _DirectionalLightDirections[index].xyz;
	const DirectionalShadowData dirShadowData = GetDirectionalShadowData(index, shadowData);
	light.attenuation = GetDirectionalShadowAttenuation(dirShadowData, shadowData, surfaceWS);
	return light;
}

// OtherShadowData GetOtherShadowData (int lightIndex) {
// 	OtherShadowData data;
// 	data.strength = _OtherLightShadowData[lightIndex].x;
// 	data.shadowMaskChannel = _OtherLightShadowData[lightIndex].w;
// 	return data;
// }

Light GetDirectionalLight (int index) {
	Light light;
	light.color = _DirectionalLightColors[index].rgb;
	light.direction = _DirectionalLightDirections[index].xyz;
	light.attenuation = 1.0;
	return light;
}

Light GetOtherLight (int index, Surface surfaceWS) {
	Light light;
	light.color = _OtherLightColors[index].rgb;
	float3 position = _OtherLightPositions[index].xyz;
	float3 ray = position - surfaceWS.position;
	light.direction = normalize(ray);
	float distanceSqr = max(dot(ray, ray), 0.00001);
	float rangeAttenuation = Square(
		saturate(1.0 - Square(distanceSqr * _OtherLightPositions[index].w))
	);
	light.attenuation = rangeAttenuation / distanceSqr;
	return light;
}

#endif