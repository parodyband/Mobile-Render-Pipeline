using UnityEditor;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.Rendering;

namespace Mobile_SRP.Runtime
{
    partial class CameraRenderer
    {

        partial void DrawGizmos();

        partial void DrawUnsupportedShaders();

        partial void PrepareForSceneWindow();

        partial void PrepareBuffer();

#if UNITY_EDITOR

        private static readonly ShaderTagId[] LegacyShaderTagIds =
        {
            new("Always"),
            new("ForwardBase"),
            new("PrepassBase"),
            new("Vertex"),
            new("VertexLMRGBM"),
            new("VertexLM")
        };

        private static Material _errorMaterial;

        private string SampleName { get; set; }

        partial void DrawGizmos()
        {
            if (!Handles.ShouldRenderGizmos()) return;
            m_Context.DrawGizmos(m_Camera, GizmoSubset.PreImageEffects);
            m_Context.DrawGizmos(m_Camera, GizmoSubset.PostImageEffects);
        }

        partial void DrawUnsupportedShaders()
        {
            if (_errorMaterial == null)
            {
                _errorMaterial =
                    new Material(Shader.Find("Hidden/InternalErrorShader"));
            }

            var drawingSettings = new DrawingSettings(
                LegacyShaderTagIds[0], new SortingSettings(m_Camera)
            )
            {
                overrideMaterial = _errorMaterial
            };
            for (var i = 1; i < LegacyShaderTagIds.Length; i++)
            {
                drawingSettings.SetShaderPassName(i, LegacyShaderTagIds[i]);
            }

            var filteringSettings = FilteringSettings.defaultValue;
            m_Context.DrawRenderers(
                m_CullingResults, ref drawingSettings, ref filteringSettings
            );
        }

        partial void PrepareForSceneWindow()
        {
            if (m_Camera.cameraType == CameraType.SceneView)
            {
                ScriptableRenderContext.EmitWorldGeometryForSceneView(m_Camera);
            }
        }

        partial void PrepareBuffer()
        {
            Profiler.BeginSample("Editor Only");
            m_Buffer.name = SampleName = m_Camera.name;
            Profiler.EndSample();
        }

#else

	const string SampleName = BufferName;

#endif
    }
}