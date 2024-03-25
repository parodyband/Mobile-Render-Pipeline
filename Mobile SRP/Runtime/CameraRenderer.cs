using UnityEngine;
using UnityEngine.Experimental.Rendering.RenderGraphModule;
using UnityEngine.Rendering;

public class CameraRenderer
{
	public const float RenderScaleMin = 0.1f, RenderScaleMax = 2f;

	private static readonly CameraSettings DefaultCameraSettings = new();

	private readonly PostFXStack m_PostFXStack = new();

	private readonly Material m_Material;

	public CameraRenderer(Shader shader) => m_Material = CoreUtils.CreateEngineMaterial(shader);

	public void Dispose() => CoreUtils.Destroy(m_Material);

	private float m_FrameTime;

	private static readonly int Time = Shader.PropertyToID("_Time");
	private static readonly int CameraForwardVector = Shader.PropertyToID("_CameraForwardVector");

	public void Render(RenderGraph renderGraph, ScriptableRenderContext context, Camera camera,
		CameraBufferSettings bufferSettings,
		bool useLightsPerObject, ShadowSettings shadowSettings, PostFXSettings postFXSettings, int colorLUTResolution)
	{
		m_FrameTime += UnityEngine.Time.deltaTime;

		if (m_FrameTime > 100)
		{
			m_FrameTime = 0;
		}

		Shader.SetGlobalVector(CameraForwardVector, camera.transform.forward);

		ProfilingSampler cameraSampler;
		CameraSettings cameraSettings;
		if (camera.TryGetComponent(out MobileRenderPipelineCamera crpCamera))
		{
			cameraSampler = crpCamera.Sampler;
			cameraSettings = crpCamera.Settings;
		}
		else
		{
			cameraSampler = ProfilingSampler.Get(camera.cameraType);
			cameraSettings = DefaultCameraSettings;
		}

		bool useColorTexture, useDepthTexture;
		if (camera.cameraType == CameraType.Reflection)
		{
			useColorTexture = bufferSettings.copyColorReflection;
			useDepthTexture = bufferSettings.copyDepthReflection;
		}
		else
		{
			useColorTexture =
				bufferSettings.copyColor && cameraSettings.copyColor;
			useDepthTexture =
				bufferSettings.copyDepth && cameraSettings.copyDepth;
		}

		if (cameraSettings.overridePostFX)
		{
			postFXSettings = cameraSettings.postFXSettings;
		}

		var hasActivePostFX =
			postFXSettings != null && PostFXSettings.IsActiveFor(camera);

		var renderScale = cameraSettings.GetRenderScale(
			bufferSettings.renderScale);
		var useScaledRendering = renderScale is < 0.99f or > 1.01f;

#if UNITY_EDITOR
		if (camera.cameraType == CameraType.SceneView)
		{
			ScriptableRenderContext.EmitWorldGeometryForSceneView(camera);
			useScaledRendering = false;
		}
#endif

		if (!camera.TryGetCullingParameters(
			    out var scriptableCullingParameters))
		{
			return;
		}

		scriptableCullingParameters.shadowDistance =
			Mathf.Min(shadowSettings.maxDistance, camera.farClipPlane);
		CullingResults cullingResults = context.Cull(
			ref scriptableCullingParameters);

		var useHDR = bufferSettings.allowHDR && camera.allowHDR;
		Vector2Int bufferSize = default;
		if (useScaledRendering)
		{
			renderScale = Mathf.Clamp(
				renderScale, RenderScaleMin, RenderScaleMax);
			bufferSize.x = (int)(camera.pixelWidth * renderScale);
			bufferSize.y = (int)(camera.pixelHeight * renderScale);
		}
		else
		{
			bufferSize.x = camera.pixelWidth;
			bufferSize.y = camera.pixelHeight;
		}

		bufferSettings.fxaa.enabled &= cameraSettings.allowFXAA;
		m_PostFXStack.Setup(
			camera, bufferSize, postFXSettings, cameraSettings.keepAlpha,
			useHDR, colorLUTResolution, cameraSettings.finalBlendMode,
			bufferSettings.bicubicRescaling, bufferSettings.fxaa);

		var useIntermediateBuffer = useScaledRendering ||
		                            useColorTexture || useDepthTexture || hasActivePostFX;

		var renderGraphParameters = new RenderGraphParameters
		{
			commandBuffer = CommandBufferPool.Get(),
			currentFrameIndex = UnityEngine.Time.frameCount,
			executionName = cameraSampler.name,
			rendererListCulling = true,
			scriptableRenderContext = context
		};

		using (renderGraph.RecordAndExecute(renderGraphParameters))
		{
			using var _ = new RenderGraphProfilingScope(
				renderGraph, cameraSampler);
			var shadowTextures = new ShadowTextures();

			if (!cameraSettings.disableShadowPass)
			{
				shadowTextures = LightingPass.Record(
					renderGraph, cullingResults, shadowSettings, useLightsPerObject,
					cameraSettings.maskLights ? cameraSettings.renderingLayerMask : -1);
			}

			var textures = SetupPass.Record(
				renderGraph, useIntermediateBuffer, useColorTexture,
				useDepthTexture, useHDR, bufferSize, camera);

			GeometryPass.Record(cameraSettings,
				renderGraph, camera, cullingResults,
				useLightsPerObject, cameraSettings.renderingLayerMask, true,
				textures, shadowTextures);

			SkyboxPass.Record(renderGraph, camera, textures);

			var copier = new CameraRendererCopier(
				m_Material, camera, cameraSettings.finalBlendMode);
			CopyAttachmentsPass.Record(
				renderGraph, useColorTexture, useDepthTexture,
				copier, textures);

			GeometryPass.Record(cameraSettings,
				renderGraph, camera, cullingResults,
				useLightsPerObject, cameraSettings.renderingLayerMask, false,
				textures, shadowTextures);

			UnsupportedShadersPass.Record(renderGraph, camera, cullingResults);

			if (hasActivePostFX)
			{
				PostFXPass.Record(renderGraph, m_PostFXStack, textures);
			}
			else if (useIntermediateBuffer)
			{
				FinalPass.Record(renderGraph, copier, textures);
			}

			GizmosPass.Record(renderGraph, useIntermediateBuffer,
				copier, textures);
		}

		context.ExecuteCommandBuffer(renderGraphParameters.commandBuffer);
		context.Submit();
		CommandBufferPool.Release(renderGraphParameters.commandBuffer);
	}
}