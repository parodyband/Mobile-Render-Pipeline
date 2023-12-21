using Unity.Collections;
using UnityEngine;
using UnityEngine.Rendering;

namespace Mobile_SRP.Runtime
{
    public class Lighting
    {
        private const string BufferName = "Lighting";
        private const int MaxDirLightCount = 4, MaxOtherLightCount = 12;
        
        static readonly string LightsPerObjectKeyword = "_LIGHTS_PER_OBJECT";

        private static readonly int DirLightCountId = Shader.PropertyToID("_DirectionalLightCount");
        private static readonly int DirLightColorsId = Shader.PropertyToID("_DirectionalLightColors");
        private static readonly int DirLightDirectionsId = Shader.PropertyToID("_DirectionalLightDirections");
        private static readonly int DirLightShadowDataId = Shader.PropertyToID("_DirectionalLightShadowData");
        
        private static readonly int OtherLightCountID = Shader.PropertyToID("_OtherLightCount");
        private static readonly int OtherLightColorsId = Shader.PropertyToID("_OtherLightColors");
        private static readonly int OtherLightPositionsId = Shader.PropertyToID("_OtherLightPositions");
        private static readonly int OtherLightDirectionsId = Shader.PropertyToID("_OtherLightDirections");
        private static readonly int OtherLightSpotAnglesId = Shader.PropertyToID("_OtherLightSpotAngles");
        private static readonly int OtherLightShadowDataId = Shader.PropertyToID("_OtherLightShadowData");

        private static readonly Vector4[] DirLightColors = new Vector4[MaxDirLightCount];
        private static readonly Vector4[] DirLightDirections = new Vector4[MaxDirLightCount];
        private static readonly Vector4[] DirLightShadowData = new Vector4[MaxDirLightCount];
        
        private static readonly Vector4[] OtherLightColors = new Vector4[MaxOtherLightCount];
        private static readonly Vector4[] OtherLightPositions = new Vector4[MaxOtherLightCount];
        private static readonly Vector4[] OtherLightDirections = new Vector4[MaxOtherLightCount];
        private static readonly Vector4[] OtherLightSpotAngles = new Vector4[MaxOtherLightCount];
        private static readonly Vector4[] OtherLightShadowData = new Vector4[MaxOtherLightCount];

        private readonly CommandBuffer m_Buffer = new()
        {
            name = BufferName
        };

        private CullingResults m_CullingResults;
        private readonly Shadows m_Shadows = new();

        public void Setup(ScriptableRenderContext context, CullingResults cullingResults, ShadowSettings shadowSettings, bool useLightsPerObject)
        {
            m_CullingResults = cullingResults;
            m_Buffer.BeginSample(BufferName);
            m_Shadows.Setup(context, cullingResults, shadowSettings);
            SetupLights(useLightsPerObject);
            m_Shadows.Render();
            m_Buffer.EndSample(BufferName);
            context.ExecuteCommandBuffer(m_Buffer);
            m_Buffer.Clear();
        }
        
        public void Cleanup()
        {
            m_Shadows.Cleanup();
        }

        private void SetupLights(bool useLightsPerObject)
        {
			NativeArray<int> indexMap = useLightsPerObject ?
						m_CullingResults.GetLightIndexMap(Allocator.Temp) : default;
			NativeArray<VisibleLight> visibleLights = m_CullingResults.visibleLights;
			int dirLightCount = 0, otherLightCount = 0;
			int i;
			for (i = 0; i < visibleLights.Length; i++) {
				int newIndex = -1;
				VisibleLight visibleLight = visibleLights[i];
				switch (visibleLight.lightType) {
					case LightType.Directional:
						if (dirLightCount < MaxDirLightCount) {
							SetupDirectionalLight(dirLightCount++, ref visibleLight);
						}
						break;
					case LightType.Point:
						if (otherLightCount < MaxOtherLightCount) {
							newIndex = otherLightCount;
							SetupPointLight(otherLightCount++, ref visibleLight);
						}
						break;
					case LightType.Spot:
						if (otherLightCount < MaxOtherLightCount) {
							newIndex = otherLightCount;
							SetupSpotLight(otherLightCount++, ref visibleLight);
						}
						break;
				}
				if (useLightsPerObject) {
					indexMap[i] = newIndex;
				}
			}

			if (useLightsPerObject) {
				for (; i < indexMap.Length; i++) {
					indexMap[i] = -1;
				}
				m_CullingResults.SetLightIndexMap(indexMap);
				indexMap.Dispose();
				Shader.EnableKeyword(LightsPerObjectKeyword);
			}
			else {
				Shader.DisableKeyword(LightsPerObjectKeyword);
			}

			m_Buffer.SetGlobalInt(DirLightCountId, dirLightCount);
			if (dirLightCount > 0) {
				m_Buffer.SetGlobalVectorArray(DirLightColorsId, DirLightColors);
				m_Buffer.SetGlobalVectorArray(DirLightDirectionsId, DirLightDirections);
				m_Buffer.SetGlobalVectorArray(DirLightShadowDataId, DirLightShadowData);
			}

			m_Buffer.SetGlobalInt(OtherLightCountID, otherLightCount);
			if (otherLightCount > 0) {
				m_Buffer.SetGlobalVectorArray(OtherLightColorsId, OtherLightColors);
				m_Buffer.SetGlobalVectorArray(
					OtherLightPositionsId, OtherLightPositions
				);
				m_Buffer.SetGlobalVectorArray(
					OtherLightDirectionsId, OtherLightDirections
				);
				m_Buffer.SetGlobalVectorArray(
					OtherLightSpotAnglesId, OtherLightSpotAngles
				);
				m_Buffer.SetGlobalVectorArray(
					OtherLightShadowDataId, OtherLightShadowData
				);
			}
        }
        
        private void SetupDirectionalLight(int index, ref VisibleLight visibleLight)
        {
            DirLightColors[index] = visibleLight.finalColor;
            DirLightDirections[index] = -visibleLight.localToWorldMatrix.GetColumn(2);
            DirLightShadowData[index] = m_Shadows.ReserveDirectionalShadows(visibleLight.light, index);
        }
        
        private void SetupPointLight (int index, ref VisibleLight visibleLight) {
            OtherLightColors[index] = visibleLight.finalColor;
            var position = visibleLight.localToWorldMatrix.GetColumn(3);
            position.w =
                1f / Mathf.Max(visibleLight.range * visibleLight.range, 0.00001f);
            OtherLightPositions[index] = position;
            OtherLightSpotAngles[index] = new Vector4(0f, 1f);
            var light = visibleLight.light;
            OtherLightShadowData[index] = m_Shadows.ReserveOtherShadows(light, index);
        }
        
        
        void SetupSpotLight (int index, ref VisibleLight visibleLight) {
            OtherLightColors[index] = visibleLight.finalColor;
            var position = visibleLight.localToWorldMatrix.GetColumn(3);
            position.w =
                1f / Mathf.Max(visibleLight.range * visibleLight.range, 0.00001f);
            OtherLightPositions[index] = position;
            OtherLightDirections[index] =
                -visibleLight.localToWorldMatrix.GetColumn(2);

            var light = visibleLight.light;
            var innerCos = Mathf.Cos(Mathf.Deg2Rad * 0.5f * light.innerSpotAngle);
            var outerCos = Mathf.Cos(Mathf.Deg2Rad * 0.5f * visibleLight.spotAngle);
            var angleRangeInv = 1f / Mathf.Max(innerCos - outerCos, 0.001f);
            OtherLightSpotAngles[index] = new Vector4(
                angleRangeInv, -outerCos * angleRangeInv
            );
            OtherLightShadowData[index] = m_Shadows.ReserveOtherShadows(light, index);
        }

        
    }
}