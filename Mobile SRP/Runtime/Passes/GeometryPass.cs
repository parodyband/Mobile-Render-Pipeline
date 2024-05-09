using UnityEngine;
using UnityEngine.Experimental.Rendering.RenderGraphModule;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RendererUtils;

public class GeometryPass
{
	private static readonly ProfilingSampler
		SamplerOpaque = new("Opaque Geometry"),
		SamplerTransparent = new("Transparent Geometry");

	private static readonly ShaderTagId[] ShaderTagIDs =
	{
		new("SRPDefaultUnlit"),
		new("DefaultLit"),
		new ("XRay")
	};

	private RendererListHandle m_List;

	private void Render(RenderGraphContext context)
	{
		context.cmd.DrawRendererList(m_List);
		context.renderContext.ExecuteCommandBuffer(context.cmd);
		context.cmd.Clear();
	}

	public static void Record(
		CameraSettings cameraSettings,
		RenderGraph renderGraph,
		Camera camera,
		CullingResults cullingResults,
		bool useLightsPerObject,
		int renderingLayerMask,
		bool opaque,
		in CameraRendererTextures textures,
		in ShadowTextures shadowTextures)
	{
		ProfilingSampler sampler = opaque ? SamplerOpaque : SamplerTransparent;

		using RenderGraphBuilder builder = renderGraph.AddRenderPass(
			sampler.name, out GeometryPass pass, sampler);
		
		pass.m_List = builder.UseRendererList(renderGraph.CreateRendererList(
			new RendererListDesc(ShaderTagIDs, cullingResults, camera)
			{
				sortingCriteria = opaque ? SortingCriteria.CommonOpaque : SortingCriteria.CommonTransparent,
				rendererConfiguration =
					PerObjectData.ReflectionProbes |
					PerObjectData.Lightmaps |
					PerObjectData.ShadowMask |
					PerObjectData.LightProbe |
					PerObjectData.OcclusionProbe |
					PerObjectData.LightProbeProxyVolume |
					PerObjectData.OcclusionProbeProxyVolume |
					(useLightsPerObject ? PerObjectData.LightData | PerObjectData.LightIndices : PerObjectData.None),
				renderQueueRange = opaque ? RenderQueueRange.opaque : RenderQueueRange.transparent,
				renderingLayerMask = (uint)renderingLayerMask
			}));

		builder.ReadWriteTexture(textures.colorAttachment);
		builder.ReadWriteTexture(textures.depthAttachment);
		
		if (!opaque)
		{
			if (textures.colorCopy.IsValid())
			{
				builder.ReadTexture(textures.colorCopy);
			}

			if (textures.depthCopy.IsValid())
			{
				builder.ReadTexture(textures.depthCopy);
			}
		}

		if (!cameraSettings.disableShadowPass)
		{
			builder.ReadTexture(shadowTextures.directionalAtlas);
			builder.ReadTexture(shadowTextures.otherAtlas);
		}

		builder.SetRenderFunc(delegate(GeometryPass data, RenderGraphContext context)
		{
			RenderFunc(data, context, camera);
		});
	}
	private static readonly int CameraForwardVector = Shader.PropertyToID("_CameraForwardVector");
	private static readonly int WorldSpaceCameraPos = Shader.PropertyToID("_WorldSpaceCameraPos");
	private static void RenderFunc(GeometryPass pass, RenderGraphContext context, Component camera)
	{
		CommandBuffer buffer = context.cmd;
		buffer.SetGlobalVector(CameraForwardVector, camera.transform.forward);
		buffer.SetGlobalVector(WorldSpaceCameraPos, camera.transform.position);
		pass.Render(context);
	}
}