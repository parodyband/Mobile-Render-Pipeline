#ifndef CUSTOM_LIGHTING_INCLUDED
#define CUSTOM_LIGHTING_INCLUDED

static const half3 grayscaleConversion = half3(0.299, 0.587, 0.114);

half3 IncomingLight (Surface surface, Light light) {
    half nDotL = max(0, dot(surface.normal, light.direction));
    return saturate(nDotL * light.attenuation) * light.color;
}

//saturate(dot(surface.normal, light.direction) * light.attenuation) * light.color;
//PBR
half3 GetLighting (Surface surface, BRDF brdf, Light light) {
    return IncomingLight(surface, light) * DirectBRDF(surface, brdf, light);
}

half3 GetLighting (Surface surfaceWS, BRDF brdf, GI gi) {
    const ShadowData shadowData = GetShadowData(surfaceWS);
    float3 color = gi.diffuse * brdf.diffuse;
    
    for (int i = 0; i < GetDirectionalLightCount(); i++) {
        const Light light = GetDirectionalLight(i, surfaceWS, shadowData);
        color += GetLighting(surfaceWS, brdf, light);
    }
    
    return color;
}

half SpecularStrengthMobile(Surface surface, float smoothness, float3 lightDirection) {
    // Convert smoothness to roughness
    const float roughness = 1.0 - smoothness;

    float3 h = SafeNormalize(lightDirection + surface.viewDirection);
    float nh2 = Square(saturate(dot(surface.normal, h)));
    float lh2 = Square(saturate(dot(lightDirection, h)));
    float r2 = Square(roughness); // Use roughness for the calculation
    float d2 = Square(nh2 * (r2 - 1.0) + 1.00001);
    const float normalization = roughness * 4.0 + 2.0; // Adjust based on roughness
    const float ndotl = saturate(dot(surface.normal, lightDirection));
    const float linearFalloff = 1.0 - ndotl;
    return r2 / (d2 * max(0.1, lh2) * normalization) * linearFalloff;
}
//Mobile
half GetSmoothnessPower(float smoothness) {
    return exp2(10 * smoothness + 1);
}
//
// half GetSpecularDot (Surface surface, float3 lightDirection) {
//     return saturate(dot(surface.normal, normalize(surface.viewDirection + lightDirection)));
// }

half3 MobileLightingHandler (Surface surface, Light light) {
    const half lightIntensity = dot(light.color, grayscaleConversion);
    const half3 diffuse = IncomingLight(surface, light);
    const half diffuseGray = dot(diffuse, grayscaleConversion);

    //Blinn Phong
    //const half specularDot = GetSpecularDot(surface, light.direction);
    //const half specular = pow(specularDot, GetSmoothnessPower(surface.smoothness)) * diffuseGray * surface.specularPower;

    //GGX
    const half specularStrength = SpecularStrengthMobile(surface, surface.smoothness, light.direction);
    const half specular = specularStrength * diffuseGray * surface.specularPower;
    
    half3 metalColor = surface.color * (diffuse + (specular * light.color));
    
    half3 color = metalColor;

    #ifdef _MATCAP_ON
    const half nDotV = dot(surface.normal, light.direction); // Use half-Lambert term for matcap blending
    const half matcapInfluence = lerp(0, 1, surface.metallic * nDotV); // Blend factor based on metallic and light angle
    half3 matcapColor = surface.matcap * surface.color * lightIntensity;
    matcapColor = lerp(half3(0, 0, 0), matcapColor, matcapInfluence);

    #if defined(SHADOWS_ENABLED)
    metalColor *= light.attenuation + 0.2;
    #endif

    color = lerp(color, matcapColor, matcapInfluence);
    #endif
    
    return color;
}


half3 GetMobileLighting (Surface surface, GI gi) {
    #if defined(SHADOWS_ENABLED)
    const ShadowData shadowData = GetShadowData(surface);
    #endif
    
    half3 radiance = gi.diffuse * surface.color;

    const int directionalLightCount = GetDirectionalLightCount();
    for (int i = 0; i < directionalLightCount; i++) {
        #if defined(SHADOWS_ENABLED)
        const Light light = GetDirectionalLight(i, surface, shadowData);
        #else
        const Light light = GetDirectionalLight(i);
        #endif
        radiance += MobileLightingHandler(surface, light);
    }

    const int otherLightCount = GetOtherLightCount();
    for (int j = 0; j < otherLightCount; j++) {
        Light light = GetOtherLight(j, surface);
        radiance += MobileLightingHandler(surface, light);
    }
    
    return radiance;
}

//Cheaper Vertex Lighting
half3 MobileLightingHandlerVertex (Surface surface, Light light, float specular) {
    
    const half3 diffuse = IncomingLight(surface, light);
    
    half3 color = surface.color * (diffuse + specular);
    
    return color;
}

half3 GetMobileLightingVertex(Surface surface, float specular, GI gi) {
    
    #if defined(SHADOWS_ENABLED)
    const ShadowData shadowData = GetShadowData(surface);
    #endif
    
    half3 radiance = gi.diffuse * surface.color;
    
    for (int i = 0; i < GetDirectionalLightCount(); i++) {
        #if defined(SHADOWS_ENABLED)
        const Light light = GetDirectionalLight(i, surface, shadowData);
        #else
        const Light light = GetDirectionalLight(i);
        #endif
        
        radiance += MobileLightingHandlerVertex(surface, light, specular);
    }

    for (int j = 0; j < GetOtherLightCount(); j++) {
        Light light = GetOtherLight(j, surface);
        radiance += MobileLightingHandlerVertex(surface, light, specular);
    }
    
    return radiance;
}

#endif