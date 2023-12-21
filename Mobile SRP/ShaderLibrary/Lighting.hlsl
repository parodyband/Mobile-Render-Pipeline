#ifndef CUSTOM_LIGHTING_INCLUDED
#define CUSTOM_LIGHTING_INCLUDED

static const half3 grayscaleConversion = half3(0.299, 0.587, 0.114);

//PBR
float3 IncomingLight (Surface surface, Light light) {
    return
        saturate(dot(surface.normal, light.direction) * light.attenuation) *
        light.color;
}

float3 GetLighting (Surface surface, BRDF brdf, Light light) {
    return IncomingLight(surface, light) * DirectBRDF(surface, brdf, light);
}

float3 GetLighting (Surface surfaceWS, BRDF brdf, GI gi) {
    ShadowData shadowData = GetShadowData(surfaceWS);
    shadowData.shadowMask = gi.shadowMask;
	
    float3 color = IndirectBRDF(surfaceWS, brdf, gi.diffuse, gi.specular);
    for (int i = 0; i < GetDirectionalLightCount(); i++) {
        Light light = GetDirectionalLight(i, surfaceWS, shadowData);
        color += GetLighting(surfaceWS, brdf, light);
    }
	
    #if defined(_LIGHTS_PER_OBJECT)
    for (int j = 0; j < min(unity_LightData.y, 8); j++) {
        int lightIndex = unity_LightIndices[(uint)j / 4][(uint)j % 4];
        Light light = GetOtherLight(lightIndex, surfaceWS, shadowData);
        color += GetLighting(surfaceWS, brdf, light);
    }
    #else
    for (int j = 0; j < GetOtherLightCount(); j++) {
        Light light = GetOtherLight(j, surfaceWS, shadowData);
        color += GetLighting(surfaceWS, brdf, light);
    }
    #endif
    return color;
}

// half SpecularStrengthMobile(Surface surface, half smoothness, half3 lightDirection) {
//     // Convert smoothness to roughness
//     const half roughness = 1.0 - smoothness;
//
//     const half3 h = SafeNormalize(lightDirection + surface.viewDirection);
//     const half nh2 = Square(saturate(dot(surface.normal, h)));
//     const half lh2 = Square(saturate(dot(lightDirection, h)));
//     const half r2 = Square(roughness); // Use roughness for the calculation
//     const half d2 = Square(nh2 * (r2 - 1.0) + 1.00001);
//     const half normalization = roughness * 4.0 + 2.0; // Adjust based on roughness
//     const half nDotL = saturate(dot(surface.normal, lightDirection));
//     const half linearFalloff = 1.0 - nDotL;
//     return r2 / (d2 * max(0.1, lh2) * normalization) * linearFalloff;
// }
// //Mobile
// half GetSmoothnessPower(float smoothness) {
//     return exp2(10 * smoothness + 1);
// }
//
// //
// // half GetSpecularDot (Surface surface, float3 lightDirection) {
// //     return saturate(dot(surface.normal, normalize(surface.viewDirection + lightDirection)));
// // }
//
// half4 MobileLightingHandler (Surface surface, Light light) {
//     //const half lightIntensity = dot(light.color, grayscaleConversion);
//     half attenuation = light.attenuation;
//     half nDotL = max(0,dot(surface.normal, light.direction));
//
//     #if defined(_HALF_LAMBERT)
//     nDotL = Square(nDotL * .5 + .5);
//     #endif
//     
//     half3 radiance = light.color * attenuation;
//
//     //radiance *= attenuation;
//     
//     //Blinn Phong
//     //const half specularDot = GetSpecularDot(surface, light.direction);
//     //const half specular = pow(specularDot, GetSmoothnessPower(surface.smoothness)) * diffuseGray * surface.specularPower;
//
//     //GGX
//     const half specularStrength = SpecularStrengthMobile(surface, surface.smoothness, light.direction);
//     
//     const half specular = specularStrength * nDotL * surface.specularPower;
//     
//     half4 color = half4(surface.color * radiance * (nDotL + specular),1);
//     
//     #ifdef _MATCAP_ON
//     // const half matCapInfluence = lerp(0, 1, surface.metallic * nDotL);
//     // matCapColor.rgb = lerp(half3(0, 0, 0), surface.matcap * surface.color * lightIntensity, matCapInfluence);
//     // color = lerp(color, matCapColor, matCapInfluence);
//     #endif
//     
//     #if defined(SHADOWS_ENABLED)
//     color.a = attenuation;
//     #endif
//     
//     return color;
// }


// half3 GetMobileLighting (Surface surface, GI gi) {
//     const ShadowData shadowData = GetShadowData(surface);
//     
//     half4 radiance = half4(gi.diffuse * surface.color,1);
//
//     const int directionalLightCount = GetDirectionalLightCount();
//     for (int i = 0; i < directionalLightCount; i++) {
//         Light light = GetDirectionalLight(i, surface, shadowData);
//         #if defined(_HALF_LAMBERT)
//         light.attenuation += .2;
//         #endif
//         radiance += MobileLightingHandler(surface, light);
//     }
//     
//     const int otherLightCount = GetOtherLightCount();
//     for (int j = 0; j < otherLightCount; j++) {
//         Light light = GetOtherLight(j, surface, shadowData);
//         radiance += MobileLightingHandler(surface, light);
//     }
//
//     const half diffuseGray = saturate(dot(radiance.rgb, grayscaleConversion) * 5);
//     
//     #ifdef _MATCAP_ON
//     const half3 metal = lerp(0, surface.matcap * surface.color, surface.metallic * (radiance.a + .1));
//     radiance.rgb += lerp(0,metal, surface.metallic * .5 * diffuseGray);
//     #endif
//     
//     return radiance.rgb;
// }
//
//
//
// //Cheaper Vertex Lighting
// half3 MobileLightingHandlerVertex (Surface surface, Light light) {
//     
//     const half3 diffuse = IncomingLight(surface, light);
//     
//     half3 color = surface.color * diffuse;
//     
//     return color;
// }
//
// half3 GetMobileLightingVertex(Surface surface, float specular, GI gi) {
//     
//     #if defined(SHADOWS_ENABLED)
//     const ShadowData shadowData = GetShadowData(surface);
//     #endif
//     
//     half3 radiance = half3(gi.diffuse * surface.color);
//     
//     for (int i = 0; i < GetDirectionalLightCount(); i++) {
//         #if defined(SHADOWS_ENABLED)
//         const Light light = GetDirectionalLight(i, surface, shadowData);
//         #else
//         const Light light = GetDirectionalLight(i);
//         #endif
//         
//         radiance += MobileLightingHandlerVertex(surface, light);
//     }
//
//     for (int j = 0; j < GetOtherLightCount(); j++) {
//         Light light = GetOtherLight(j, surface);
//         radiance += MobileLightingHandlerVertex(surface, light);
//     }
//
//     radiance += specular;
//     
//     return radiance;
// }

#endif