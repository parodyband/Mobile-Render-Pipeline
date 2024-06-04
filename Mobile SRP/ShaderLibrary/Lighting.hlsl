#ifndef CUSTOM_LIGHTING_INCLUDED
#define CUSTOM_LIGHTING_INCLUDED

float3 IncomingLight (Surface surface, Light light, half halfLambertMask = 0) {
	// Calculate the normal lighting term
	float NdotL = saturate(dot(surface.normal, light.direction));
	float3 normalLight = NdotL * light.attenuation * light.color;

	// Calculate the half Lambert lighting term
	float halfLambert = saturate(NdotL * 0.5 + 0.5);
	float3 halfLambertLight = halfLambert * light.attenuation * light.color;

	// Interpolate between normal lighting and half Lambert lighting
	return lerp(normalLight, halfLambertLight, halfLambertMask);
}


float3 GetLighting (Surface surface, BRDF brdf, Light light, half halfLambertMask = 0) {
	return IncomingLight(surface, light, halfLambertMask) * DirectBRDF(surface, brdf, light);
}

bool RenderingLayersOverlap (Surface surface, Light light) {
	return (surface.renderingLayerMask & light.renderingLayerMask) != 0;
}

float3 GetLighting (Surface surfaceWS, BRDF brdf, GI gi, half halfLambertMask = 0) {
	ShadowData shadowData = GetShadowData(surfaceWS);
	shadowData.shadowMask = gi.shadowMask;
	
	float3 color = IndirectBRDF(surfaceWS, brdf, gi.diffuse, gi.specular);
	//float3 color = float3(0,0,0);
	for (int i = 0; i < GetDirectionalLightCount(); i++) {
		Light light = GetDirectionalLight(i, surfaceWS, shadowData);
		if (RenderingLayersOverlap(surfaceWS, light)) {
			color += GetLighting(surfaceWS, brdf, light, halfLambertMask);
		}
	}
	
	#if defined(_LIGHTS_PER_OBJECT)
		for (int j = 0; j < min(unity_LightData.y, 8); j++) {
			int lightIndex = unity_LightIndices[(uint)j / 4][(uint)j % 4];
			Light light = GetOtherLight(lightIndex, surfaceWS, shadowData);
			if (RenderingLayersOverlap(surfaceWS, light)) {
				color += GetLighting(surfaceWS, brdf, light);
			}
		}
	#else
		for (int j = 0; j < GetOtherLightCount(); j++) {
			Light light = GetOtherLight(j, surfaceWS, shadowData);
			if (RenderingLayersOverlap(surfaceWS, light)) {
				color += GetLighting(surfaceWS, brdf, light);
			}
		}
	#endif
	return color;
}

#endif