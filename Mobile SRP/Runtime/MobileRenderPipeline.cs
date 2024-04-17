using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering.RenderGraphModule;
using UnityEngine.Rendering;

public partial class MobileRenderPipeline : RenderPipeline
{
	private readonly CameraRenderer m_Renderer;

	private readonly CameraBufferSettings m_CameraBufferSettings;

	private readonly bool m_UseLightsPerObject;

	private readonly DecalSettings m_DecalSettings;

	private readonly ShadowSettings m_ShadowSettings;

	private readonly PostFXSettings m_PostFXSettings;

	private readonly int m_ColorLUTResolution;

	private readonly RenderGraph m_RenderGraph = new("Mobile SRP Render Graph");
	

	public MobileRenderPipeline(
		CameraBufferSettings cameraBufferSettings,
		bool useSRPBatcher,
		bool useLightsPerObject, DecalSettings decalSettings, ShadowSettings shadowSettings,
		PostFXSettings postFXSettings, int colorLUTResolution,
		Shader cameraRendererShader, bool isMobilePlatform)
	{
		m_ColorLUTResolution = colorLUTResolution;
		m_CameraBufferSettings = cameraBufferSettings;
		m_PostFXSettings = postFXSettings;
		m_ShadowSettings = shadowSettings;
		m_UseLightsPerObject = useLightsPerObject;
		m_DecalSettings = decalSettings;
		GraphicsSettings.useScriptableRenderPipelineBatching = useSRPBatcher;
		GraphicsSettings.lightsUseLinearIntensity = true;
		InitializeForEditor();
		m_Renderer = new CameraRenderer(cameraRendererShader);
		
		if (m_DecalSettings.useDecals)
		{
			DecalRenderer.FlushDecals();
			DecalRenderer.InitializeDecalShaderResources(m_DecalSettings);
		}
		
		if (isMobilePlatform)
		{
			Shader.EnableKeyword("RENDER_MOBILE");
		}
		else
		{
			Shader.DisableKeyword("RENDER_MOBILE");
		}
	}

	protected override void Render(ScriptableRenderContext context, Camera[] cameras)
	{
	}
	

	protected override void Render(ScriptableRenderContext context, List<Camera> cameras)
	{
		foreach (Camera camera in cameras)
		{
			BeginCameraRendering(context, camera);
			m_Renderer.Render(
				m_RenderGraph, context, camera, m_CameraBufferSettings,
				m_UseLightsPerObject,
				m_ShadowSettings, m_PostFXSettings, m_ColorLUTResolution);
			

		}
		
		if (m_DecalSettings.useDecals)
		{
			DecalRenderer.UpdateDecals();
		}
		
		m_RenderGraph.EndFrame();
		EndFrameRendering(context, cameras.ToArray());
	}

	protected override void Dispose(bool disposing)
	{
		base.Dispose(disposing);
		DisposeForEditor();
		m_Renderer.Dispose();
		m_RenderGraph.Cleanup();
	}
}