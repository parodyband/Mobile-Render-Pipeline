﻿using Unity.Collections;
using UnityEngine;
using UnityEngine.Experimental.Rendering.RenderGraphModule;
using UnityEngine.Rendering;

public class LightingPass
{
	static readonly ProfilingSampler sampler = new("Lighting");

	const int maxDirLightCount = 4, maxOtherLightCount = 64;

	static readonly GlobalKeyword lightsPerObjectKeyword =
		GlobalKeyword.Create("_LIGHTS_PER_OBJECT");

	static readonly int
		dirLightCountId = Shader.PropertyToID("_DirectionalLightCount"),
		dirLightColorsId = Shader.PropertyToID("_DirectionalLightColors"),
		dirLightDirectionsAndMasksId =
			Shader.PropertyToID("_DirectionalLightDirectionsAndMasks"),
		dirLightShadowDataId =
			Shader.PropertyToID("_DirectionalLightShadowData");

	static readonly Vector4[]
		dirLightColors = new Vector4[maxDirLightCount],
		dirLightDirectionsAndMasks = new Vector4[maxDirLightCount],
		dirLightShadowData = new Vector4[maxDirLightCount];

	static readonly int
		otherLightCountId = Shader.PropertyToID("_OtherLightCount"),
		otherLightColorsId = Shader.PropertyToID("_OtherLightColors"),
		otherLightPositionsId = Shader.PropertyToID("_OtherLightPositions"),
		otherLightDirectionsAndMasksId =
			Shader.PropertyToID("_OtherLightDirectionsAndMasks"),
		otherLightSpotAnglesId = Shader.PropertyToID("_OtherLightSpotAngles"),
		otherLightShadowDataId = Shader.PropertyToID("_OtherLightShadowData");

	static readonly Vector4[]
		otherLightColors = new Vector4[maxOtherLightCount],
		otherLightPositions = new Vector4[maxOtherLightCount],
		otherLightDirectionsAndMasks = new Vector4[maxOtherLightCount],
		otherLightSpotAngles = new Vector4[maxOtherLightCount],
		otherLightShadowData = new Vector4[maxOtherLightCount];

	CullingResults cullingResults;

	readonly Shadows shadows = new();

	int dirLightCount, otherLightCount;

	bool useLightsPerObject;

	public void Setup(
		CullingResults cullingResults, ShadowSettings shadowSettings,
		bool useLightsPerObject, int renderingLayerMask)
	{
		this.cullingResults = cullingResults;
		this.useLightsPerObject = useLightsPerObject;
		shadows.Setup(cullingResults, shadowSettings);
		SetupLights(renderingLayerMask);
	}

	void SetupLights(int renderingLayerMask)
	{
		NativeArray<int> indexMap = useLightsPerObject ?
			cullingResults.GetLightIndexMap(Allocator.Temp) : default;
		NativeArray<VisibleLight> visibleLights = cullingResults.visibleLights;
		int i;
		dirLightCount = otherLightCount = 0;
		for (i = 0; i < visibleLights.Length; i++)
		{
			int newIndex = -1;
			VisibleLight visibleLight = visibleLights[i];
			Light light = visibleLight.light;
			if ((light.renderingLayerMask & renderingLayerMask) != 0)
			{
				switch (visibleLight.lightType)
				{
					case LightType.Directional:
						if (dirLightCount < maxDirLightCount)
						{
							SetupDirectionalLight(
								dirLightCount++, i, ref visibleLight, light);
						}
						break;
					case LightType.Point:
						if (otherLightCount < maxOtherLightCount)
						{
							newIndex = otherLightCount;
							SetupPointLight(
								otherLightCount++, i, ref visibleLight, light);
						}
						break;
					case LightType.Spot:
						if (otherLightCount < maxOtherLightCount)
						{
							newIndex = otherLightCount;
							SetupSpotLight(
								otherLightCount++, i, ref visibleLight, light);
						}
						break;
				}
			}
			if (useLightsPerObject)
			{
				indexMap[i] = newIndex;
			}
		}

		if (useLightsPerObject)
		{
			for (; i < indexMap.Length; i++)
			{
				indexMap[i] = -1;
			}
			cullingResults.SetLightIndexMap(indexMap);
			indexMap.Dispose();
		}
	}

	void Render(RenderGraphContext context)
	{
		CommandBuffer buffer = context.cmd;
		buffer.SetKeyword(lightsPerObjectKeyword, useLightsPerObject);
		buffer.SetGlobalInt(dirLightCountId, dirLightCount);
		if (dirLightCount > 0)
		{
			buffer.SetGlobalVectorArray(dirLightColorsId, dirLightColors);
			buffer.SetGlobalVectorArray(
				dirLightDirectionsAndMasksId, dirLightDirectionsAndMasks);
			buffer.SetGlobalVectorArray(
				dirLightShadowDataId, dirLightShadowData);
		}

		buffer.SetGlobalInt(otherLightCountId, otherLightCount);
		if (otherLightCount > 0)
		{
			buffer.SetGlobalVectorArray(otherLightColorsId, otherLightColors);
			buffer.SetGlobalVectorArray(
				otherLightPositionsId, otherLightPositions);
			buffer.SetGlobalVectorArray(
				otherLightDirectionsAndMasksId, otherLightDirectionsAndMasks);
			buffer.SetGlobalVectorArray(
				otherLightSpotAnglesId, otherLightSpotAngles);
			buffer.SetGlobalVectorArray(
				otherLightShadowDataId, otherLightShadowData);
		}

		shadows.Render(context);
		context.renderContext.ExecuteCommandBuffer(buffer);
		buffer.Clear();
	}

	void SetupDirectionalLight(
		int index, int visibleIndex, ref VisibleLight visibleLight, Light light)
	{
		dirLightColors[index] = visibleLight.finalColor;
		Vector4 dirAndMask = -visibleLight.localToWorldMatrix.GetColumn(2);
		dirAndMask.w = light.renderingLayerMask.ReinterpretAsFloat();
		dirLightDirectionsAndMasks[index] = dirAndMask;
		dirLightShadowData[index] =
			shadows.ReserveDirectionalShadows(light, visibleIndex);
	}

	void SetupPointLight(
		int index, int visibleIndex, ref VisibleLight visibleLight, Light light)
	{
		otherLightColors[index] = visibleLight.finalColor;
		Vector4 position = visibleLight.localToWorldMatrix.GetColumn(3);
		position.w =
			1f / Mathf.Max(visibleLight.range * visibleLight.range, 0.00001f);
		otherLightPositions[index] = position;
		otherLightSpotAngles[index] = new Vector4(0f, 1f);
		Vector4 dirAndmask = Vector4.zero;
		dirAndmask.w = light.renderingLayerMask.ReinterpretAsFloat();
		otherLightDirectionsAndMasks[index] = dirAndmask;
		otherLightShadowData[index] = shadows.ReserveOtherShadows(
			light, visibleIndex);
	}

	void SetupSpotLight(
		int index, int visibleIndex, ref VisibleLight visibleLight, Light light)
	{
		otherLightColors[index] = visibleLight.finalColor;
		Vector4 position = visibleLight.localToWorldMatrix.GetColumn(3);
		position.w =
			1f / Mathf.Max(visibleLight.range * visibleLight.range, 0.00001f);
		otherLightPositions[index] = position;
		Vector4 dirAndMask = -visibleLight.localToWorldMatrix.GetColumn(2);
		dirAndMask.w = light.renderingLayerMask.ReinterpretAsFloat();
		otherLightDirectionsAndMasks[index] = dirAndMask;

		float innerCos = Mathf.Cos(Mathf.Deg2Rad * 0.5f * light.innerSpotAngle);
		float outerCos = Mathf.Cos(
			Mathf.Deg2Rad * 0.5f * visibleLight.spotAngle);
		float angleRangeInv = 1f / Mathf.Max(innerCos - outerCos, 0.001f);
		otherLightSpotAngles[index] = new Vector4(
			angleRangeInv, -outerCos * angleRangeInv
		);
		otherLightShadowData[index] = shadows.ReserveOtherShadows(
			light, visibleIndex);
	}

	public static ShadowTextures Record(
		RenderGraph renderGraph,
		CullingResults cullingResults, ShadowSettings shadowSettings,
		bool useLightsPerObject, int renderingLayerMask)
	{
		using RenderGraphBuilder builder = renderGraph.AddRenderPass(
			sampler.name, out LightingPass pass, sampler);
		pass.Setup(cullingResults, shadowSettings,
			useLightsPerObject, renderingLayerMask);
		builder.SetRenderFunc<LightingPass>(
			static (pass, context) => pass.Render(context));
		builder.AllowPassCulling(false);
		return pass.shadows.GetRenderTextures(renderGraph, builder);
	}
}
