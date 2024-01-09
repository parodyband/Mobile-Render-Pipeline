using System;
using UnityEngine;
using UnityEngine.Rendering;

[Serializable]
public class CameraSettings
{
	public bool copyColor = true, copyDepth = true;

	[RenderingLayerMaskField]
	public int renderingLayerMask = -1;

	public bool maskLights = false;
	
	public bool disableShadowPass;

	public enum RenderScaleMode
	{ Inherit, Multiply, Override }

	public RenderScaleMode renderScaleMode = RenderScaleMode.Inherit;

	[Range(CameraRenderer.RenderScaleMin, CameraRenderer.RenderScaleMax)]
	public float renderScale = 1f;

	public bool overridePostFX = false;

	public PostFXSettings postFXSettings = default;

	public bool allowFXAA = false;

	public bool keepAlpha = false;

	[Serializable]
	public struct FinalBlendMode
	{
		public BlendMode source, destination;
	}

	public FinalBlendMode finalBlendMode = new()
	{
		source = BlendMode.One,
		destination = BlendMode.Zero
	};

	public float GetRenderScale(float scale) =>
		renderScaleMode == RenderScaleMode.Inherit ? scale :
		renderScaleMode == RenderScaleMode.Override ? renderScale :
		scale * renderScale;
}
