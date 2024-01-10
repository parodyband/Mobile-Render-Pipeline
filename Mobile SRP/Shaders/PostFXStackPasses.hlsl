#ifndef CUSTOM_POST_FX_PASSES_INCLUDED
#define CUSTOM_POST_FX_PASSES_INCLUDED

#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Filtering.hlsl"

TEXTURE2D(_PostFXSource);
TEXTURE2D(_PostFXSource2);

real4 _PostFXSource_TexelSize;

real4 GetSourceTexelSize () {
	return _PostFXSource_TexelSize;
}

real4 GetSource(real2 screenUV) {
	return SAMPLE_TEXTURE2D_LOD(_PostFXSource, sampler_linear_clamp, screenUV, 0);
}

real4 GetSourceBicubic (real2 screenUV) {
	return SampleTexture2DBicubic(
		TEXTURE2D_ARGS(_PostFXSource, sampler_linear_clamp), screenUV,
		_PostFXSource_TexelSize.zwxy, 1.0, 0.0
	);
}

real4 GetSource2(real2 screenUV) {
	return SAMPLE_TEXTURE2D_LOD(_PostFXSource2, sampler_linear_clamp, screenUV, 0);
}

struct Varyings {
	real4 positionCS : SV_POSITION;
	real2 screenUV : VAR_SCREEN_UV;
};

Varyings DefaultPassVertex (uint vertexID : SV_VertexID) {
	Varyings output;
	output.positionCS = real4(
		vertexID <= 1 ? -1.0 : 3.0,
		vertexID == 1 ? 3.0 : -1.0,
		0.0, 1.0
	);
	output.screenUV = real2(
		vertexID <= 1 ? 0.0 : 2.0,
		vertexID == 1 ? 2.0 : 0.0
	);
	if (_ProjectionParams.x < 0.0) {
		output.screenUV.y = 1.0 - output.screenUV.y;
	}
	return output;
}

bool _BloomBicubicUpsampling;
real _BloomIntensity;

real4 BloomAddPassFragment (Varyings input) : SV_TARGET {
	real3 lowRes;
	if (_BloomBicubicUpsampling) {
		lowRes = GetSourceBicubic(input.screenUV).rgb;
	}
	else {
		lowRes = GetSource(input.screenUV).rgb;
	}
	real4 highRes = GetSource2(input.screenUV);
	return real4(lowRes * _BloomIntensity + highRes.rgb, highRes.a);
}

real4 BloomHorizontalPassFragment (Varyings input) : SV_TARGET {
	real3 color = 0.0;
	real offsets[] = {
		-4.0, -3.0, -2.0, -1.0, 0.0, 1.0, 2.0, 3.0, 4.0
	};
	real weights[] = {
		0.01621622, 0.05405405, 0.12162162, 0.19459459, 0.22702703,
		0.19459459, 0.12162162, 0.05405405, 0.01621622
	};
	for (int i = 0; i < 9; i++) {
		real offset = offsets[i] * 2.0 * GetSourceTexelSize().x;
		color += GetSource(input.screenUV + real2(offset, 0.0)).rgb * weights[i];
	}
	return real4(color, 1.0);
}

real4 _BloomThreshold;

real3 ApplyBloomThreshold (real3 color) {
	real brightness = Max3(color.r, color.g, color.b);
	real soft = brightness + _BloomThreshold.y;
	soft = clamp(soft, 0.0, _BloomThreshold.z);
	soft = soft * soft * _BloomThreshold.w;
	real contribution = max(soft, brightness - _BloomThreshold.x);
	contribution /= max(brightness, 0.00001);
	return color * contribution;
}

real4 BloomPrefilterPassFragment (Varyings input) : SV_TARGET {
	real3 color = ApplyBloomThreshold(GetSource(input.screenUV).rgb);
	return real4(color, 1.0);
}

real4 BloomPrefilterFirefliesPassFragment (Varyings input) : SV_TARGET {
	real3 color = 0.0;
	real weightSum = 0.0;
	real2 offsets[] = {
		real2(0.0, 0.0),
		real2(-1.0, -1.0), real2(-1.0, 1.0), real2(1.0, -1.0), real2(1.0, 1.0)
	};
	for (int i = 0; i < 5; i++) {
		real3 c =
			GetSource(input.screenUV + offsets[i] * GetSourceTexelSize().xy * 2.0).rgb;
		c = ApplyBloomThreshold(c);
		real w = 1.0 / (Luminance(c) + 1.0);
		color += c * w;
		weightSum += w;
	}
	color /= weightSum;
	return real4(color, 1.0);
}

real4 BloomScatterPassFragment (Varyings input) : SV_TARGET {
	real3 lowRes;
	if (_BloomBicubicUpsampling) {
		lowRes = GetSourceBicubic(input.screenUV).rgb;
	}
	else {
		lowRes = GetSource(input.screenUV).rgb;
	}
	real3 highRes = GetSource2(input.screenUV).rgb;
	return real4(lerp(highRes, lowRes, _BloomIntensity), 1.0);
}

real4 BloomScatterFinalPassFragment (Varyings input) : SV_TARGET {
	real3 lowRes;
	if (_BloomBicubicUpsampling) {
		lowRes = GetSourceBicubic(input.screenUV).rgb;
	}
	else {
		lowRes = GetSource(input.screenUV).rgb;
	}
	real4 highRes = GetSource2(input.screenUV);
	lowRes += highRes.rgb - ApplyBloomThreshold(highRes.rgb);
	return real4(lerp(highRes.rgb, lowRes, _BloomIntensity), highRes.a);
}

real4 BloomVerticalPassFragment (Varyings input) : SV_TARGET {
	real3 color = 0.0;
	const real offsets[] = {
		-3.23076923, -1.38461538, 0.0, 1.38461538, 3.23076923
	};
	const real weights[] = {
		0.07027027, 0.31621622, 0.22702703, 0.31621622, 0.07027027
	};
	for (int i = 0; i < 5; i++) {
		real offset = offsets[i] * GetSourceTexelSize().y;
		color += GetSource(input.screenUV + real2(0.0, offset)).rgb * weights[i];
	}
	return real4(color, 1.0);
}

real4 CopyPassFragment (Varyings input) : SV_TARGET {
	return GetSource(input.screenUV);
}

real _SharpenStrength;

real4 SharpenPassFragment(Varyings input) : SV_TARGET {
	const real2 texelSize = GetSourceTexelSize().xy;

	const real3x3 sharpenKernel = real3x3(
	   0, -1, 0,
	   -1, 5, -1,
	   0, -1, 0);

	real3 color = 0;
	for (int y = -1; y <= 1; y++) {
		for (int x = -1; x <= 1; x++) {
			const real2 offset = real2(x, y) * texelSize;
			color += GetSource(input.screenUV + offset).rgb * sharpenKernel[y + 1][x + 1];
		}
	}

	const real3 originalColor = GetSource(input.screenUV).rgb;

	//color = lerp(originalColor, color, _SharpenStrength);
	color = originalColor;
	return real4(color, 1.0);
}


real4 _ColorAdjustments;
real4 _ColorFilter;
real4 _WhiteBalance;
real4 _SplitToningShadows, _SplitToningHighlights;
real4 _ChannelMixerRed, _ChannelMixerGreen, _ChannelMixerBlue;
real4 _SMHShadows, _SMHMidtones, _SMHHighlights, _SMHRange;

real Luminance (real3 color, bool useACES) {
	return useACES ? AcesLuminance(color) : Luminance(color);
}

real3 ColorGradePostExposure (real3 color) {
	return color * _ColorAdjustments.x;
}

real3 ColorGradeWhiteBalance (real3 color) {
	color = LinearToLMS(color);
	color *= _WhiteBalance.rgb;
	return LMSToLinear(color);
}

real3 ColorGradingContrast (real3 color, bool useACES) {
	color = useACES ? ACES_to_ACEScc(unity_to_ACES(color)) : LinearToLogC(color);
	color = (color - ACEScc_MIDGRAY) * _ColorAdjustments.y + ACEScc_MIDGRAY;
	return useACES ? ACES_to_ACEScg(ACEScc_to_ACES(color)) : LogCToLinear(color);
}

real3 ColorGradeColorFilter (real3 color) {
	return color * _ColorFilter.rgb;
}

real3 ColorGradingHueShift (real3 color) {
	color = RgbToHsv(color);
	const real hue = color.x + _ColorAdjustments.z;
	color.x = RotateHue(hue, 0.0, 1.0);
	return HsvToRgb(color);
}

real3 ColorGradingSaturation (real3 color, bool useACES) {
	const real luminance = Luminance(color, useACES);
	return (color - luminance) * _ColorAdjustments.w + luminance;
}

real3 ColorGradeSplitToning (real3 color, bool useACES) {
	color = PositivePow(color, 1.0 / 2.2);
	const real t = saturate(Luminance(saturate(color), useACES) + _SplitToningShadows.w);
	const real3 shadows = lerp(0.5, _SplitToningShadows.rgb, 1.0 - t);
	const real3 highlights = lerp(0.5, _SplitToningHighlights.rgb, t);
	color = SoftLight(color, shadows);
	color = SoftLight(color, highlights);
	return PositivePow(color, 2.2);
}

real3 ColorGradingChannelMixer (real3 color) {
	return mul(
		real3x3(_ChannelMixerRed.rgb, _ChannelMixerGreen.rgb, _ChannelMixerBlue.rgb),
		color
	);
}

real3 ColorGradingShadowsMidtonesHighlights (real3 color, bool useACES) {
	const real luminance = Luminance(color, useACES);
	const real shadowsWeight = 1.0 - smoothstep(_SMHRange.x, _SMHRange.y, luminance);
	const real highlightsWeight = smoothstep(_SMHRange.z, _SMHRange.w, luminance);
	const real midtonesWeight = 1.0 - shadowsWeight - highlightsWeight;
	return
		color * _SMHShadows.rgb * shadowsWeight +
		color * _SMHMidtones.rgb * midtonesWeight +
		color * _SMHHighlights.rgb * highlightsWeight;
}

real3 ColorGrade (real3 color, bool useACES = false) {
	color = ColorGradePostExposure(color);
	color = ColorGradeWhiteBalance(color);
	color = ColorGradingContrast(color, useACES);
	color = ColorGradeColorFilter(color);
	color = max(color, 0.0);
	color =	ColorGradeSplitToning(color, useACES);
	color = ColorGradingChannelMixer(color);
	color = max(color, 0.0);
	color = ColorGradingShadowsMidtonesHighlights(color, useACES);
	color = ColorGradingHueShift(color);
	color = ColorGradingSaturation(color, useACES);
	return max(useACES ? ACEScg_to_ACES(color) : color, 0.0);
}

// Vignette parameters: x = intensity, y = smoothness, z/w = inner/outer radius
real4 _VignetteParams; 
real3 _VignetteColor;

// Calculate the vignette effect
real Vignette(real2 screenUV, real4 vignetteParams) {
	// Transform screenUV to [-1, 1] range
	const real2 uv = screenUV * 2.0 - 1.0;

	// Calculate distance from the center of the screen
	const real dist = length(uv);

	// Map the distance to [0, 1] based on inner and outer radius
	const real innerRadius = vignetteParams.z;
	const real outerRadius = vignetteParams.w;
	const real vignetteEffect = smoothstep(innerRadius, outerRadius, dist);

	// Apply intensity and smoothness
	const real intensity = vignetteParams.x;
	const real smoothness = vignetteParams.y;
	return 1.0 - smoothstep(0.0, smoothness, vignetteEffect * intensity);
}

// Apply the vignette effect to the fragment
real4 VignettePassFragment(Varyings input) : SV_TARGET {
	const real2 screenUV = input.screenUV;
	real3 color = GetSource(screenUV).rgb;
	const real3 vignetteColor = _VignetteColor.rgb;

	// Calculate vignette value
	const real vignette = Vignette(screenUV, _VignetteParams);

	// Apply the vignette effect by blending with the vignette color
	color = lerp(color, vignetteColor, vignette);
	return real4(color, 1.0);
}


real _ChromaticAberrationStrength;
real4 _ChromaticAberrationParams; // x = inner radius, y = outer radius

real4 ChromaticAberrationPassFragment(Varyings input) : SV_TARGET {
	const real2 texelSize = GetSourceTexelSize().xy;
	const real2 screenUV = input.screenUV;
	const real2 uv = screenUV * 2.0 - 1.0; // Convert UV to [-1, 1] range

	// Calculate distance from the center for vignette-like effect
	const real dist = length(uv);
	const real vignetteEffect = smoothstep(_ChromaticAberrationParams.x, _ChromaticAberrationParams.y, dist);

	// Define offsets for chromatic aberration
	const real2 redOffset = real2(_ChromaticAberrationStrength, 0) * texelSize * vignetteEffect;
	const real2 blueOffset = -real2(_ChromaticAberrationStrength, 0) * texelSize * vignetteEffect;

	// Sample the texture for each color channel with different offsets
	real red = GetSource(screenUV + redOffset).r;
	real green = GetSource(screenUV).g; // No offset for green channel
	real blue = GetSource(screenUV + blueOffset).b;

	// Combine the channels back into a single color
	real3 color = real3(red, green, blue);

	return real4(color, 1.0);
}


real4 _ColorGradingLUTParameters;

bool _ColorGradingLUTInLogC;

real3 GetColorGradedLUT (real2 uv, bool useACES = false) {
	const real3 color = GetLutStripValue(uv, _ColorGradingLUTParameters);
	return ColorGrade(_ColorGradingLUTInLogC ? LogCToLinear(color) : color, useACES);
}

real4 ColorGradingNonePassFragment (Varyings input) : SV_TARGET {
	real3 color = GetColorGradedLUT(input.screenUV);
	return real4(color, 1.0);
}

real4 ColorGradingACESPassFragment (Varyings input) : SV_TARGET {
	real3 color = GetColorGradedLUT(input.screenUV, true);
	color = AcesTonemap(color);
	return real4(color, 1.0);
}

real4 ColorGradingNeutralPassFragment (Varyings input) : SV_TARGET {
	real3 color = GetColorGradedLUT(input.screenUV);
	color = NeutralTonemap(color);
	return real4(color, 1.0);
}

real4 ColorGradingReinhardPassFragment (Varyings input) : SV_TARGET {
	real3 color = GetColorGradedLUT(input.screenUV);
	color /= color + 1.0;
	return real4(color, 1.0);
}

TEXTURE2D(_ColorGradingLUT);

real3 ApplyColorGradingLUT (real3 color) {
	return ApplyLut2D(
		TEXTURE2D_ARGS(_ColorGradingLUT, sampler_linear_clamp),
		saturate(_ColorGradingLUTInLogC ? LinearToLogC(color) : color),
		_ColorGradingLUTParameters.xyz
	);
}

real4 ApplyColorGradingPassFragment (Varyings input) : SV_TARGET {
	real4 color = GetSource(input.screenUV);
	color.rgb = ApplyColorGradingLUT(color.rgb);
	return color;
}

real4 ApplyColorGradingWithLumaPassFragment (Varyings input) : SV_TARGET {
	real4 color = GetSource(input.screenUV);
	color.rgb = ApplyColorGradingLUT(color.rgb);
	color.a = sqrt(Luminance(color.rgb));
	return color;
}

bool _CopyBicubic;

real4 FinalPassFragmentRescale (Varyings input) : SV_TARGET {
	if (_CopyBicubic) {
		return GetSourceBicubic(input.screenUV);
	}
	else {
		return GetSource(input.screenUV);
	}
}

#endif