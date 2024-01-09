using UnityEngine;
using UnityEngine.Experimental.Rendering.RenderGraphModule;
using UnityEngine.Rendering;
using static PostFXSettings;

public class PostFXStack
{
	public enum Pass
	{
		BloomAdd,
		BloomHorizontal,
		BloomPrefilter,
		BloomPrefilterFireflies,
		BloomScatter,
		BloomScatterFinal,
		BloomVertical,
		Copy,
		Sharpen,
		ChromaticAberration,
		Vignette,
		ColorGradingNone,
		ColorGradingACES,
		ColorGradingNeutral,
		ColorGradingReinhard,
		ApplyColorGrading,
		ApplyColorGradingWithLuma,
		FinalRescale,
		FXAA,
		FXAAWithLuma
	}

	private const string
		FXAAQualityLowKeyword = "FXAA_QUALITY_LOW",
		FXAAQualityMediumKeyword = "FXAA_QUALITY_MEDIUM";

	private const int MaxBloomPyramidLevels = 16;

	private static readonly Rect FullViewRect = new(0f, 0f, 1f, 1f);

	private readonly int
		m_BloomBicubicUpsamplingId =
			Shader.PropertyToID("_BloomBicubicUpsampling"),
		m_BloomIntensityId = Shader.PropertyToID("_BloomIntensity"),
		m_BloomPrefilterId = Shader.PropertyToID("_BloomPrefilter"),
		m_BloomResultId = Shader.PropertyToID("_BloomResult"),
		m_BloomThresholdId = Shader.PropertyToID("_BloomThreshold"),
		m_FXSourceId = Shader.PropertyToID("_PostFXSource"),
		m_FXSource2Id = Shader.PropertyToID("_PostFXSource2");

	private readonly int
		m_ColorGradingLUTId = Shader.PropertyToID("_ColorGradingLUT"),
		m_ColorGradingLUTParametersId =
			Shader.PropertyToID("_ColorGradingLUTParameters"),
		m_ColorGradingLUTInLogId = Shader.PropertyToID("_ColorGradingLUTInLogC"),
		m_ColorAdjustmentsId = Shader.PropertyToID("_ColorAdjustments"),
		m_ColorFilterId = Shader.PropertyToID("_ColorFilter"),
		m_WhiteBalanceId = Shader.PropertyToID("_WhiteBalance"),
		m_SplitToningShadowsId = Shader.PropertyToID("_SplitToningShadows"),
		m_SplitToningHighlightsId = Shader.PropertyToID("_SplitToningHighlights"),
		m_ChannelMixerRedId = Shader.PropertyToID("_ChannelMixerRed"),
		m_ChannelMixerGreenId = Shader.PropertyToID("_ChannelMixerGreen"),
		m_ChannelMixerBlueId = Shader.PropertyToID("_ChannelMixerBlue"),
		m_SmhShadowsId = Shader.PropertyToID("_SMHShadows"),
		m_SmhMidtonesId = Shader.PropertyToID("_SMHMidtones"),
		m_SmhHighlightsId = Shader.PropertyToID("_SMHHighlights"),
		m_SmhRangeId = Shader.PropertyToID("_SMHRange");

	private readonly int
		m_CopyBicubicId = Shader.PropertyToID("_CopyBicubic"),
		m_ColorGradingResultId = Shader.PropertyToID("_ColorGradingResult"),
		m_FinalResultId = Shader.PropertyToID("_FinalResult"),
		m_FinalSrcBlendId = Shader.PropertyToID("_FinalSrcBlend"),
		m_FinalDstBlendId = Shader.PropertyToID("_FinalDstBlend");

	private readonly int m_FXAAConfigId = Shader.PropertyToID("_FXAAConfig");
	private readonly int m_SharpenStrengthId = Shader.PropertyToID("_SharpenStrength");
	private readonly int m_ChromaticAberrationStrengthId = Shader.PropertyToID("_ChromaticAberrationStrength");
	private readonly int m_ChromaticAberrationParamsId = Shader.PropertyToID("_ChromaticAberrationParams");
	private readonly int m_VignetteColorId = Shader.PropertyToID("_VignetteColor");
	private readonly int m_VignetteParams = Shader.PropertyToID("_VignetteParams");

	private CommandBuffer m_Buffer;

	private Camera m_Camera;

	private PostFXSettings m_Settings;

	private readonly int m_BloomPyramidId;

	private bool m_KeepAlpha, m_UseHDR;

	private int m_ColorLUTResolution;

	private Vector2Int m_BufferSize;

	private CameraBufferSettings.BicubicRescalingMode m_BicubicRescaling;

	private CameraBufferSettings.FXAA m_FXAA;

	public bool IsActive => m_Settings != null;

	private CameraSettings.FinalBlendMode m_FinalBlendMode;

	public PostFXStack()
	{
		m_BloomPyramidId = Shader.PropertyToID("_BloomPyramid0");
		for (var i = 1; i < MaxBloomPyramidLevels * 2; i++) {
			Shader.PropertyToID("_BloomPyramid" + i);
		}
	}

	public void Setup(
		Camera camera, Vector2Int bufferSize,
		PostFXSettings settings, bool keepAlpha, bool useHDR,
		int colorLUTResolution,
		CameraSettings.FinalBlendMode finalBlendMode,
		CameraBufferSettings.BicubicRescalingMode bicubicRescaling,
		CameraBufferSettings.FXAA fxaa)
	{
		m_FXAA = fxaa;
		m_BicubicRescaling = bicubicRescaling;
		m_BufferSize = bufferSize;
		m_FinalBlendMode = finalBlendMode;
		m_ColorLUTResolution = colorLUTResolution;
		m_KeepAlpha = keepAlpha;
		m_UseHDR = useHDR;
		m_Camera = camera;
		m_Settings = settings;
	}

	public void Render(RenderGraphContext context, TextureHandle sourceId)
	{
		m_Buffer = context.cmd;
		
		if (m_Settings.sharpen.enabled)
		{
			SetSharpenStrength(m_Settings.sharpen.intensity);
		}

		if (m_Settings.chromaticAberration.enabled)
		{
			SetChromeAberrationStrength(m_Settings.chromaticAberration.intensity);
			m_Buffer.SetGlobalVector(m_ChromaticAberrationParamsId, new Vector4(
				m_Settings.chromaticAberration.chromaticAberrationParameters.x,
				m_Settings.chromaticAberration.chromaticAberrationParameters.y,
				0,
				0));
		}
		
		if (m_Settings.vignetteSettings.enabled)
		{
			m_Buffer.SetGlobalColor(m_VignetteColorId, m_Settings.vignetteSettings.color);
			m_Buffer.SetGlobalVector(m_VignetteParams, m_Settings.vignetteSettings.vignetteParameters);
		}
		
		if (DoBloom(sourceId))
		{
			DoFinal(m_BloomResultId);
			m_Buffer.ReleaseTemporaryRT(m_BloomResultId);
		}
		else
		{
			DoFinal(sourceId);
		}
		context.renderContext.ExecuteCommandBuffer(m_Buffer);
		m_Buffer.Clear();
	}

	private bool DoBloom(RenderTargetIdentifier sourceId)
	{
		var bloom = m_Settings.Bloom;
		int width, height;
		if (bloom.ignoreRenderScale)
		{
			width = m_Camera.pixelWidth / 2;
			height = m_Camera.pixelHeight / 2;
		}
		else
		{
			width = m_BufferSize.x / 2;
			height = m_BufferSize.y / 2;
		}

		if (
			bloom.maxIterations == 0 ||
			bloom.intensity <= 0f ||
			height < bloom.downscaleLimit * 2 ||
			width < bloom.downscaleLimit * 2)
		{
			return false;
		}

		m_Buffer.BeginSample("Bloom");
		if (bloom.enabled)
		{
			Vector4 threshold;
			threshold.x = Mathf.GammaToLinearSpace(bloom.threshold);
			threshold.y = threshold.x * bloom.thresholdKnee;
			threshold.z = 2f * threshold.y;
			threshold.w = 0.25f / (threshold.y + 0.00001f);
			threshold.y -= threshold.x;
			m_Buffer.SetGlobalVector(m_BloomThresholdId, threshold);

			var format = m_UseHDR ?
				RenderTextureFormat.DefaultHDR : RenderTextureFormat.Default;
			m_Buffer.GetTemporaryRT(
				m_BloomPrefilterId, width, height, 0, FilterMode.Bilinear, format);
			Draw(
				sourceId, m_BloomPrefilterId, bloom.fadeFireflies ?
					Pass.BloomPrefilterFireflies : Pass.BloomPrefilter);
			width /= 2;
			height /= 2;

			int fromId = m_BloomPrefilterId, toId = m_BloomPyramidId + 1;
			int i;
			for (i = 0; i < bloom.maxIterations; i++)
			{
				if (height < bloom.downscaleLimit || width < bloom.downscaleLimit)
				{
					break;
				}
				var midId = toId - 1;
				m_Buffer.GetTemporaryRT(
					midId, width, height, 0, FilterMode.Bilinear, format);
				m_Buffer.GetTemporaryRT(
					toId, width, height, 0, FilterMode.Bilinear, format);
				Draw(fromId, midId, Pass.BloomHorizontal);
				Draw(midId, toId, Pass.BloomVertical);
				fromId = toId;
				toId += 2;
				width /= 2;
				height /= 2;
			}

			m_Buffer.ReleaseTemporaryRT(m_BloomPrefilterId);
			m_Buffer.SetGlobalFloat(
				m_BloomBicubicUpsamplingId, bloom.bicubicUpsampling ? 1f : 0f);

			Pass combinePass, finalPass;
			float finalIntensity;
			if (bloom.mode == BloomSettings.Mode.Additive)
			{
				combinePass = finalPass = Pass.BloomAdd;
				m_Buffer.SetGlobalFloat(m_BloomIntensityId, 1f);
				finalIntensity = bloom.intensity;
			}
			else
			{
				combinePass = Pass.BloomScatter;
				finalPass = Pass.BloomScatterFinal;
				m_Buffer.SetGlobalFloat(m_BloomIntensityId, bloom.scatter);
				finalIntensity = Mathf.Min(bloom.intensity, 1f);
			}

			if (i > 1)
			{
				m_Buffer.ReleaseTemporaryRT(fromId - 1);
				toId -= 5;
				for (i -= 1; i > 0; i--)
				{
					m_Buffer.SetGlobalTexture(m_FXSource2Id, toId + 1);
					Draw(fromId, toId, combinePass);
					m_Buffer.ReleaseTemporaryRT(fromId);
					m_Buffer.ReleaseTemporaryRT(toId + 1);
					fromId = toId;
					toId -= 2;
				}
			}
			else
			{
				m_Buffer.ReleaseTemporaryRT(m_BloomPyramidId);
			}
			m_Buffer.SetGlobalFloat(m_BloomIntensityId, finalIntensity);
			m_Buffer.SetGlobalTexture(m_FXSource2Id, sourceId);
			m_Buffer.GetTemporaryRT(
				m_BloomResultId, m_BufferSize.x, m_BufferSize.y, 0,
				FilterMode.Bilinear, format);
			Draw(fromId, m_BloomResultId, finalPass);
			m_Buffer.ReleaseTemporaryRT(fromId);
		}
		else
		{
			m_Buffer.GetTemporaryRT(
				m_BloomResultId, m_BufferSize.x, m_BufferSize.y, 0,
				FilterMode.Bilinear, m_UseHDR ?
					RenderTextureFormat.DefaultHDR : RenderTextureFormat.Default);
			Draw(sourceId, m_BloomResultId, Pass.Copy);
		}
		m_Buffer.EndSample("Bloom");
		return true;
	}
	
	public void SetSharpenStrength(float strength) {
		m_Buffer.SetGlobalFloat(m_SharpenStrengthId, strength);
	}	
	
	public void SetChromeAberrationStrength(float strength) {
		m_Buffer.SetGlobalFloat(m_ChromaticAberrationStrengthId, strength);
	}

	private void ConfigureColorAdjustments()
	{
		var colorAdjustments = m_Settings.ColorAdjustments;
		m_Buffer.SetGlobalVector(m_ColorAdjustmentsId, new Vector4(
			Mathf.Pow(2f, colorAdjustments.postExposure),
			colorAdjustments.contrast * 0.01f + 1f,
			colorAdjustments.hueShift * (1f / 360f),
			colorAdjustments.saturation * 0.01f + 1f));
		m_Buffer.SetGlobalColor(
			m_ColorFilterId, colorAdjustments.colorFilter.linear);
	}

	private void ConfigureWhiteBalance()
	{
		var whiteBalance = m_Settings.WhiteBalance;
		m_Buffer.SetGlobalVector(m_WhiteBalanceId,
			ColorUtils.ColorBalanceToLMSCoeffs(
				whiteBalance.temperature, whiteBalance.tint));
	}

	private void ConfigureSplitToning()
	{
		var splitToning = m_Settings.SplitToning;
		var splitColor = splitToning.shadows;
		splitColor.a = splitToning.balance * 0.01f;
		m_Buffer.SetGlobalColor(m_SplitToningShadowsId, splitColor);
		m_Buffer.SetGlobalColor(m_SplitToningHighlightsId, splitToning.highlights);
	}

	private void ConfigureChannelMixer()
	{
		var channelMixer = m_Settings.ChannelMixer;
		m_Buffer.SetGlobalVector(m_ChannelMixerRedId, channelMixer.red);
		m_Buffer.SetGlobalVector(m_ChannelMixerGreenId, channelMixer.green);
		m_Buffer.SetGlobalVector(m_ChannelMixerBlueId, channelMixer.blue);
	}

	private void ConfigureShadowsMidtonesHighlights()
	{
		var smh =
			m_Settings.ShadowsMidtonesHighlights;
		m_Buffer.SetGlobalColor(m_SmhShadowsId, smh.shadows.linear);
		m_Buffer.SetGlobalColor(m_SmhMidtonesId, smh.midtones.linear);
		m_Buffer.SetGlobalColor(m_SmhHighlightsId, smh.highlights.linear);
		m_Buffer.SetGlobalVector(m_SmhRangeId, new Vector4(
			smh.shadowsStart,
			smh.shadowsEnd,
			smh.highlightsStart,
			smh.highLightsEnd));
	}

	private void ConfigureFXAA()
	{
		switch (m_FXAA.quality)
		{
			case CameraBufferSettings.FXAA.Quality.Low:
				m_Buffer.EnableShaderKeyword(FXAAQualityLowKeyword);
				m_Buffer.DisableShaderKeyword(FXAAQualityMediumKeyword);
				break;
			case CameraBufferSettings.FXAA.Quality.Medium:
				m_Buffer.DisableShaderKeyword(FXAAQualityLowKeyword);
				m_Buffer.EnableShaderKeyword(FXAAQualityMediumKeyword);
				break;
			case CameraBufferSettings.FXAA.Quality.High:
			default:
				m_Buffer.DisableShaderKeyword(FXAAQualityLowKeyword);
				m_Buffer.DisableShaderKeyword(FXAAQualityMediumKeyword);
				break;
		}

		m_Buffer.SetGlobalVector(m_FXAAConfigId, new Vector4(
			m_FXAA.fixedThreshold,
			m_FXAA.relativeThreshold,
			m_FXAA.subpixelBlending));
	}

	private void DoFinal(RenderTargetIdentifier sourceId)
	{
		ConfigureColorAdjustments();
		ConfigureWhiteBalance();
		ConfigureSplitToning();
		ConfigureChannelMixer();
		ConfigureShadowsMidtonesHighlights();

		var lutHeight = m_ColorLUTResolution;
		var lutWidth = lutHeight * lutHeight;
		m_Buffer.GetTemporaryRT(
			m_ColorGradingLUTId, lutWidth, lutHeight, 0,
			FilterMode.Bilinear, RenderTextureFormat.DefaultHDR);
		m_Buffer.SetGlobalVector(m_ColorGradingLUTParametersId, new Vector4(
			lutHeight,
			0.5f / lutWidth, 0.5f / lutHeight,
			lutHeight / (lutHeight - 1f)));

		var mode = m_Settings.ToneMapping.mode;
		var pass = Pass.ColorGradingNone + (int)mode;
		m_Buffer.SetGlobalFloat(m_ColorGradingLUTInLogId,
			m_UseHDR && pass != Pass.ColorGradingNone ? 1f : 0f);
		Draw(sourceId, m_ColorGradingLUTId, pass);

		m_Buffer.SetGlobalVector(m_ColorGradingLUTParametersId,
			new Vector4(1f / lutWidth, 1f / lutHeight, lutHeight - 1f));

		m_Buffer.SetGlobalFloat(m_FinalSrcBlendId, 1f);
		m_Buffer.SetGlobalFloat(m_FinalDstBlendId, 0f);
		
		
		
		var currentSource = sourceId;
		var tempRTId = -1;

		if (m_Settings.chromaticAberration.enabled) {
			ApplyPostProcessingEffect(ref currentSource, ref tempRTId, "ChromaticAberration", Pass.ChromaticAberration);
		}
		
		if (m_Settings.vignetteSettings.enabled) {
			ApplyPostProcessingEffect(ref currentSource, ref tempRTId, "Vignette", Pass.Vignette);
		}
		
		if (m_Settings.sharpen.enabled) {
			ApplyPostProcessingEffect(ref currentSource, ref tempRTId, "Sharpen", Pass.Sharpen);
		}
		if (m_FXAA.enabled)
		{
			ConfigureFXAA();
			m_Buffer.GetTemporaryRT(
				m_ColorGradingResultId, m_BufferSize.x, m_BufferSize.y, 0,
				FilterMode.Bilinear, RenderTextureFormat.Default);
			Draw(currentSource, m_ColorGradingResultId, m_KeepAlpha ?
				Pass.ApplyColorGrading : Pass.ApplyColorGradingWithLuma);
		}
		var bicubicSampling =
			m_BicubicRescaling ==
			CameraBufferSettings.BicubicRescalingMode.UpAndDown ||
			m_BicubicRescaling ==
			CameraBufferSettings.BicubicRescalingMode.UpOnly &&
			m_BufferSize.x < m_Camera.pixelWidth;
		if (m_BufferSize.x == m_Camera.pixelWidth)
		{
			if (m_FXAA.enabled)
			{
				DrawFinal(m_ColorGradingResultId,
					m_KeepAlpha ? Pass.FXAA : Pass.FXAAWithLuma);
				m_Buffer.ReleaseTemporaryRT(m_ColorGradingResultId);
			}
			else
			{
				DrawFinal(currentSource, Pass.ApplyColorGrading);
			}
		}
		else
		{
			m_Buffer.GetTemporaryRT(
				m_FinalResultId, m_BufferSize.x, m_BufferSize.y, 0,
				FilterMode.Bilinear, RenderTextureFormat.Default);

			if (m_FXAA.enabled)
			{
				Draw(
					m_ColorGradingResultId, m_FinalResultId,
					m_KeepAlpha ? Pass.FXAA : Pass.FXAAWithLuma);
				m_Buffer.ReleaseTemporaryRT(m_ColorGradingResultId);
			}
			else
			{
				Draw(sourceId, m_FinalResultId, Pass.ApplyColorGrading);
			}

			m_Buffer.SetGlobalFloat(m_CopyBicubicId, bicubicSampling ? 1f : 0f);
			DrawFinal(m_FinalResultId, Pass.FinalRescale);
			m_Buffer.ReleaseTemporaryRT(m_FinalResultId);
		}
		if (tempRTId != -1) {
			m_Buffer.ReleaseTemporaryRT(tempRTId);
		}
		m_Buffer.ReleaseTemporaryRT(m_ColorGradingLUTId);
	}
	
	private void ApplyPostProcessingEffect(ref RenderTargetIdentifier currentSource, ref int tempRTId, string effectName, Pass effectPass) {
		m_Buffer.BeginSample(effectName);
		var effectId = Shader.PropertyToID(effectName);
		m_Buffer.GetTemporaryRT(effectId, m_BufferSize.x, m_BufferSize.y, 0, FilterMode.Bilinear, RenderTextureFormat.Default);
		Draw(currentSource, effectId, effectPass);
		if (tempRTId != -1) {
			m_Buffer.ReleaseTemporaryRT(tempRTId); // Release the previous temporary RT
		}
		currentSource = effectId; // Update current source to the effect image
		tempRTId = effectId;
		m_Buffer.EndSample(effectName);
	}


	private void Draw(RenderTargetIdentifier from, RenderTargetIdentifier to, Pass pass)
	{
		m_Buffer.SetGlobalTexture(m_FXSourceId, from);
		m_Buffer.SetRenderTarget(
			to, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store);
		m_Buffer.DrawProcedural(Matrix4x4.identity, m_Settings.Material, (int)pass,
			MeshTopology.Triangles, 3);
	}

	private void DrawFinal(RenderTargetIdentifier from, Pass pass)
	{
		m_Buffer.SetGlobalFloat(m_FinalSrcBlendId, (float)m_FinalBlendMode.source);
		m_Buffer.SetGlobalFloat(
			m_FinalDstBlendId, (float)m_FinalBlendMode.destination);
		m_Buffer.SetGlobalTexture(m_FXSourceId, from);
		m_Buffer.SetRenderTarget(
			BuiltinRenderTextureType.CameraTarget,
			m_FinalBlendMode.destination == BlendMode.Zero &&
				m_Camera.rect == FullViewRect ?
				RenderBufferLoadAction.DontCare : RenderBufferLoadAction.Load,
			RenderBufferStoreAction.Store);
		m_Buffer.SetViewport(m_Camera.pixelRect);
		m_Buffer.DrawProcedural(Matrix4x4.identity, m_Settings.Material, (int)pass,
			MeshTopology.Triangles, 3);
	}
}
