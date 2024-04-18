using UnityEngine;
using UnityEngine.Rendering;

public enum BRDFTypes
{
	CHEAP,
	STANDARD,
	GGXHIGHQUALITY
}

[CreateAssetMenu(menuName = "Rendering/Mobile Render Pipeline Settings")]
public partial class MobileRenderPipelineAsset : RenderPipelineAsset
{
	[SerializeField] private CameraBufferSettings cameraBuffer = new()
	{
		allowHDR = true,
		renderScale = 1f,
		fxaa = new CameraBufferSettings.FXAA
		{
			fixedThreshold = 0.0833f,
			relativeThreshold = 0.166f,
			subpixelBlending = 0.75f
		}
	};
	
	public float RenderScale
	{
		get => cameraBuffer.renderScale;
		set => cameraBuffer.renderScale = value > 0.5f ? value : 0.5f;
	}

	[SerializeField] private bool
		useSRPBatcher = true,
		useLightsPerObject = true;
	
	[SerializeField] private BRDFTypes brdfType = BRDFTypes.STANDARD;

	[SerializeField] private ShadowSettings shadows = default;

	[SerializeField] private DecalSettings decalSettings = default;

	[SerializeField] private PostFXSettings postFXSettings = default;
	
	public enum ColorLUTResolution
	{
		_16 = 16,
		_32 = 32,
		_64 = 64
	}

	[SerializeField] ColorLUTResolution colorLUTResolution = ColorLUTResolution._32;

	[SerializeField] private Shader cameraRendererShader = default;

	[Header("Deprecated Settings")] [SerializeField, Tooltip("Dynamic batching is no longer used.")]
	private bool useDynamicBatching;

	[SerializeField, Tooltip("GPU instancing is always enabled.")]
	private bool useGPUInstancing;

	protected override RenderPipeline CreatePipeline() =>
		new MobileRenderPipeline(cameraBuffer, useSRPBatcher,
			useLightsPerObject, decalSettings, shadows, postFXSettings,
			(int)colorLUTResolution, cameraRendererShader, brdfType);
}