using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Mobile_SRP.Runtime
{
	public class Shadows
	{
		private const string BufferName = "Shadows";

		private const int MaxShadowedDirLightCount = 4, MaxCascades = 4;

		private static readonly string[] DirectionalFilterKeywords =
		{
			"_DIRECTIONAL_PCF3",
			"_DIRECTIONAL_PCF5",
			"_DIRECTIONAL_PCF7",
		};

		private static readonly string[] CascadeBlendKeywords =
		{
			"_CASCADE_BLEND_SOFT",
			"_CASCADE_BLEND_DITHER"
		};
		
		private static readonly int DirShadowAtlasId = Shader.PropertyToID("_DirectionalShadowAtlas");
		private static readonly int DirShadowMatricesId = Shader.PropertyToID("_DirectionalShadowMatrices");
		private static readonly int CascadeCountId = Shader.PropertyToID("_CascadeCount");
		private static readonly int CascadeCullingSpheresId = Shader.PropertyToID("_CascadeCullingSpheres");
		private static readonly int CascadeDataId = Shader.PropertyToID("_CascadeData");
		private static readonly int ShadowAtlasSizeId = Shader.PropertyToID("_ShadowAtlasSize");
		private static readonly int ShadowDistanceFadeId = Shader.PropertyToID("_ShadowDistanceFade");
		private static readonly GlobalKeyword ShadowsEnabledKeyword = GlobalKeyword.Create("_SHADOWS_ENABLED");

		private static readonly Vector4[] CascadeCullingSpheres = new Vector4[MaxCascades];
		private static readonly Vector4[] CascadeData = new Vector4[MaxCascades];
		private static readonly Matrix4x4[] DirShadowMatrices = new Matrix4x4[MaxShadowedDirLightCount * MaxCascades];

		private struct ShadowedDirectionalLight
		{
			public int VisibleLightIndex;
			public float SlopeScaleBias;
			public float NearPlaneOffset;
		}

		private readonly ShadowedDirectionalLight[] m_ShadowedDirectionalLights =
			new ShadowedDirectionalLight[MaxShadowedDirLightCount];

		private int m_ShadowedDirLightCount;

		private readonly CommandBuffer m_Buffer = new CommandBuffer
		{
			name = BufferName
		};

		private ScriptableRenderContext m_Context;

		private CullingResults m_CullingResults;

		public ShadowSettings Settings;

		public void Setup(
			ScriptableRenderContext context, CullingResults cullingResults,
			ShadowSettings settings
		)
		{
			m_Context = context;
			m_CullingResults = cullingResults;
			Settings = settings;
			m_ShadowedDirLightCount = 0;
		}

		public void Cleanup()
		{
			m_Buffer.ReleaseTemporaryRT(DirShadowAtlasId);
			ExecuteBuffer();
		}

		public Vector3 ReserveDirectionalShadows(
			Light light, int visibleLightIndex
		)
		{
			if (
				m_ShadowedDirLightCount < MaxShadowedDirLightCount &&
				light.shadows != LightShadows.None && light.shadowStrength > 0f &&
				m_CullingResults.GetShadowCasterBounds(visibleLightIndex, out Bounds b)
			)
			{
				m_ShadowedDirectionalLights[m_ShadowedDirLightCount] =
					new ShadowedDirectionalLight
					{
						VisibleLightIndex = visibleLightIndex,
						SlopeScaleBias = light.shadowBias,
						NearPlaneOffset = light.shadowNearPlane
					};
				return new Vector3(
					light.shadowStrength,
					Settings.directional.cascadeCount * m_ShadowedDirLightCount++,
					light.shadowNormalBias
				);
			}

			return Vector3.zero;
		}

		public void Render()
		{
			if (!Settings.enableShadows || m_ShadowedDirLightCount == 0)
			{
				return;
			}
			if (m_ShadowedDirLightCount > 0)
			{
				RenderDirectionalShadows();
			}
			else
			{
				m_Buffer.GetTemporaryRT(
					DirShadowAtlasId, 1, 1,
					32, FilterMode.Bilinear, RenderTextureFormat.Shadowmap
				);
			}
		}

		private void RenderDirectionalShadows()
		{
			var atlasSize = (int)Settings.directional.atlasSize;
			m_Buffer.GetTemporaryRT(
				DirShadowAtlasId, atlasSize, atlasSize,
				32, FilterMode.Bilinear, RenderTextureFormat.Shadowmap
			);
			
			m_Buffer.SetRenderTarget(
				DirShadowAtlasId,
				RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store
			);
			
			m_Buffer.ClearRenderTarget(true, false, Color.clear);
			m_Buffer.BeginSample(BufferName);
			ExecuteBuffer();

			var tiles = m_ShadowedDirLightCount * Settings.directional.cascadeCount;
			var split = tiles <= 1 ? 1 : tiles <= 4 ? 2 : 4;
			var tileSize = atlasSize / split;

			for (var i = 0; i < m_ShadowedDirLightCount; i++)
			{
				RenderDirectionalShadows(i, split, tileSize);
			}

			m_Buffer.SetGlobalInt(CascadeCountId, Settings.directional.cascadeCount);
			m_Buffer.SetGlobalVectorArray(
				CascadeCullingSpheresId, CascadeCullingSpheres
			);
			
			m_Buffer.SetGlobalVectorArray(CascadeDataId, CascadeData);
			m_Buffer.SetGlobalMatrixArray(DirShadowMatricesId, DirShadowMatrices);
			var f = 1f - Settings.directional.cascadeFade;
			
			m_Buffer.SetGlobalVector(
				ShadowDistanceFadeId, new Vector4(
					1f / Settings.maxDistance, 1f / Settings.distanceFade,
					1f / (1f - f * f)
				)
			);
			
			SetKeywords(DirectionalFilterKeywords, (int)Settings.directional.filter - 1);
			SetKeywords(CascadeBlendKeywords, (int)Settings.directional.cascadeBlend - 1);
			

			
			m_Buffer.SetGlobalVector(
				ShadowAtlasSizeId, new Vector4(atlasSize, 1f / atlasSize)
			);
			m_Buffer.EndSample(BufferName);
			ExecuteBuffer();
		}

		private void SetKeywords(string[] keywords, int enabledIndex)
		{
			for (var i = 0; i < keywords.Length; i++)
			{
				if (i == enabledIndex)
				{
					m_Buffer.EnableShaderKeyword(keywords[i]);
				}
				else
				{
					m_Buffer.DisableShaderKeyword(keywords[i]);
				}
			}
		}

		public void RenderDirectionalShadows(int index, int split, int tileSize)
		{
			var light = m_ShadowedDirectionalLights[index];
			var shadowSettings = new ShadowDrawingSettings(
				m_CullingResults, light.VisibleLightIndex,
				BatchCullingProjectionType.Orthographic
			);
			var cascadeCount = Settings.directional.cascadeCount;
			var tileOffset = index * cascadeCount;
			var ratios = Settings.directional.CascadeRatios;
			var cullingFactor =
				Mathf.Max(0f, 0.8f - Settings.directional.cascadeFade);

			for (var i = 0; i < cascadeCount; i++)
			{
				m_CullingResults.ComputeDirectionalShadowMatricesAndCullingPrimitives(
					light.VisibleLightIndex, i, cascadeCount, ratios, tileSize,
					light.NearPlaneOffset, out Matrix4x4 viewMatrix,
					out Matrix4x4 projectionMatrix, out ShadowSplitData splitData
				);
				splitData.shadowCascadeBlendCullingFactor = cullingFactor;
				shadowSettings.splitData = splitData;
				if (index == 0)
				{
					SetCascadeData(i, splitData.cullingSphere, tileSize);
				}

				var tileIndex = tileOffset + i;
				DirShadowMatrices[tileIndex] = ConvertToAtlasMatrix(
					projectionMatrix * viewMatrix,
					SetTileViewport(tileIndex, split, tileSize), split
				);
				m_Buffer.SetViewProjectionMatrices(viewMatrix, projectionMatrix);
				m_Buffer.SetGlobalDepthBias(0f, light.SlopeScaleBias);
				ExecuteBuffer();
				m_Context.DrawShadows(ref shadowSettings);
				m_Buffer.SetGlobalDepthBias(0f, 0f);
			}
		}

		private void SetCascadeData(int index, Vector4 cullingSphere, float tileSize)
		{
			var texelSize = 2f * cullingSphere.w / tileSize;
			var filterSize = texelSize * ((float)Settings.directional.filter + 1f);
			cullingSphere.w -= filterSize;
			cullingSphere.w *= cullingSphere.w;
			CascadeCullingSpheres[index] = cullingSphere;
			CascadeData[index] = new Vector4(
				1f / cullingSphere.w,
				filterSize * 1.4142136f
			);
		}

		private static Matrix4x4 ConvertToAtlasMatrix(Matrix4x4 m, Vector2 offset, int split)
		{
			if (SystemInfo.usesReversedZBuffer)
			{
				m.m20 = -m.m20;
				m.m21 = -m.m21;
				m.m22 = -m.m22;
				m.m23 = -m.m23;
			}

			var scale = 1f / split;
			m.m00 = (0.5f * (m.m00 + m.m30) + offset.x * m.m30) * scale;
			m.m01 = (0.5f * (m.m01 + m.m31) + offset.x * m.m31) * scale;
			m.m02 = (0.5f * (m.m02 + m.m32) + offset.x * m.m32) * scale;
			m.m03 = (0.5f * (m.m03 + m.m33) + offset.x * m.m33) * scale;
			m.m10 = (0.5f * (m.m10 + m.m30) + offset.y * m.m30) * scale;
			m.m11 = (0.5f * (m.m11 + m.m31) + offset.y * m.m31) * scale;
			m.m12 = (0.5f * (m.m12 + m.m32) + offset.y * m.m32) * scale;
			m.m13 = (0.5f * (m.m13 + m.m33) + offset.y * m.m33) * scale;
			m.m20 = 0.5f * (m.m20 + m.m30);
			m.m21 = 0.5f * (m.m21 + m.m31);
			m.m22 = 0.5f * (m.m22 + m.m32);
			m.m23 = 0.5f * (m.m23 + m.m33);
			return m;
		}

		private Vector2 SetTileViewport(int index, int split, float tileSize)
		{
			var offset = new Vector2(index % split, (float)index / split);
			m_Buffer.SetViewport(new Rect(
				offset.x * tileSize, offset.y * tileSize, tileSize, tileSize
			));
			return offset;
		}

		private void ExecuteBuffer()
		{
			if (!Settings.enableShadows) return;
			m_Context.ExecuteCommandBuffer(m_Buffer);
			m_Buffer.Clear();
		}
	}
}