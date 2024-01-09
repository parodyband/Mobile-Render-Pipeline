using System;
using UnityEditor;
using UnityEngine;

[CreateAssetMenu(menuName = "Rendering/Mobile RP FX Settings")]
public class PostFXSettings : ScriptableObject
{
	[SerializeField] private Shader shader;

	[Serializable]
	public struct BloomSettings
	{
		public bool enabled;
		public bool ignoreRenderScale;

		[Range(0f, 16f)]
		public int maxIterations;

		[Min(1f)]
		public int downscaleLimit;

		public bool bicubicUpsampling;

		[Min(0f)]
		public float threshold;

		[Range(0f, 1f)]
		public float thresholdKnee;

		[Min(0f)]
		public float intensity;

		public bool fadeFireflies;

		public enum Mode
		{ Additive, Scattering }

		public Mode mode;

		[Range(0.05f, 0.95f)]
		public float scatter;
	}

	[SerializeField] private BloomSettings bloom = new()
	{
		scatter = 0.7f
	};

	public BloomSettings Bloom => bloom;

	[Serializable]
	public struct ColorAdjustmentsSettings
	{
		public float postExposure;

		[Range(-100f, 100f)]
		public float contrast;

		[ColorUsage(false, true)]
		public Color colorFilter;

		[Range(-180f, 180f)]
		public float hueShift;

		[Range(-100f, 100f)]
		public float saturation;
	}

	[SerializeField] private ColorAdjustmentsSettings colorAdjustments = new()
	{
		colorFilter = Color.white
	};

	public ColorAdjustmentsSettings ColorAdjustments => colorAdjustments;

	[Serializable]
	public struct WhiteBalanceSettings
	{
		[Range(-100f, 100f)]
		public float temperature, tint;
	}

	[SerializeField] private WhiteBalanceSettings whiteBalance = default;

	public WhiteBalanceSettings WhiteBalance => whiteBalance;

	[Serializable]
	public struct SplitToningSettings
	{
		[ColorUsage(false)]
		public Color shadows, highlights;

		[Range(-100f, 100f)]
		public float balance;
	}

	[SerializeField] private SplitToningSettings splitToning = new()
	{
		shadows = Color.gray,
		highlights = Color.gray
	};

	public SplitToningSettings SplitToning => splitToning;

	[Serializable]
	public struct ChannelMixerSettings
	{
		public Vector3 red, green, blue;
	}

	[SerializeField] private ChannelMixerSettings channelMixer = new()
	{
		red = Vector3.right,
		green = Vector3.up,
		blue = Vector3.forward
	};

	public ChannelMixerSettings ChannelMixer => channelMixer;

	[Serializable]
	public struct ShadowsMidtonesHighlightsSettings
	{
		[ColorUsage(false, true)]
		public Color shadows, midtones, highlights;

		[Range(0f, 2f)]
		public float shadowsStart, shadowsEnd, highlightsStart, highLightsEnd;
	}

	[SerializeField] private ShadowsMidtonesHighlightsSettings shadowsMidtonesHighlights = new()
	{
		shadows = Color.white,
		midtones = Color.white,
		highlights = Color.white,
		shadowsEnd = 0.3f,
		highlightsStart = 0.55f,
		highLightsEnd = 1f
	};

	public ShadowsMidtonesHighlightsSettings ShadowsMidtonesHighlights =>
		shadowsMidtonesHighlights;
	
	[Serializable]
	public struct ToneMappingSettings
	{
		public enum Mode
		{ None, ACES, Neutral, Reinhard }

		public Mode mode;
	}

	[SerializeField] private ToneMappingSettings toneMapping = default;

	public ToneMappingSettings ToneMapping => toneMapping;

	[NonSerialized] private Material m_Material;

	public Material Material
	{
		get
		{
			if (m_Material == null && shader != null)
			{
				m_Material = new Material(shader)
				{
					hideFlags = HideFlags.HideAndDontSave
				};
			}
			return m_Material;
		}
	}

	public static bool IsActiveFor(Camera camera)
	{
#if UNITY_EDITOR
		if (camera.cameraType == CameraType.SceneView &&
			!SceneView.currentDrawingSceneView.sceneViewState.showImageEffects)
		{
			return false;
		}
#endif
		return camera.cameraType <= CameraType.SceneView;
	}
}
