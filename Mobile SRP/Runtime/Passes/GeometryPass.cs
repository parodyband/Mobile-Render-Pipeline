﻿using UnityEngine;
using UnityEngine.Experimental.Rendering.RenderGraphModule;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RendererUtils;

public class GeometryPass
{
	private static readonly ProfilingSampler
		SamplerOpaque = new("Opaque Geometry"),
		SamplerTransparent = new("Transparent Geometry");

	private static readonly ShaderTagId[] ShaderTagIDs = {
		new("SRPDefaultUnlit"),
		new("DefaultLit")
	};

	RendererListHandle list;

	private void Render(RenderGraphContext context)
	{
		context.cmd.DrawRendererList(list);
		context.renderContext.ExecuteCommandBuffer(context.cmd);
		context.cmd.Clear();
	}

	public static void Record(
		RenderGraph renderGraph,
		Camera camera,
		CullingResults cullingResults,
		bool useLightsPerObject,
		int renderingLayerMask,
		bool opaque,
		in CameraRendererTextures textures,
		in ShadowTextures shadowTextures)
	{
		var sampler = opaque ? SamplerOpaque : SamplerTransparent;

		using var builder = renderGraph.AddRenderPass(
			sampler.name, out GeometryPass pass, sampler);

		pass.list = builder.UseRendererList(renderGraph.CreateRendererList(
			new RendererListDesc(ShaderTagIDs, cullingResults, camera)
			{
				sortingCriteria = opaque ?
					SortingCriteria.CommonOpaque :
					SortingCriteria.CommonTransparent,
				rendererConfiguration =
					PerObjectData.ReflectionProbes |
					PerObjectData.Lightmaps |
					PerObjectData.ShadowMask |
					PerObjectData.LightProbe |
					PerObjectData.OcclusionProbe |
					PerObjectData.LightProbeProxyVolume |
					PerObjectData.OcclusionProbeProxyVolume |
					(useLightsPerObject ?
						PerObjectData.LightData | PerObjectData.LightIndices :
						PerObjectData.None),
				renderQueueRange = opaque ?
					RenderQueueRange.opaque : RenderQueueRange.transparent,
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
		builder.ReadTexture(shadowTextures.directionalAtlas);
		builder.ReadTexture(shadowTextures.otherAtlas);

		builder.SetRenderFunc<GeometryPass>(
			static (pass, context) => pass.Render(context));
	}
}
