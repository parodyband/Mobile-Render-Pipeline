using UnityEngine;
using UnityEngine.Rendering;

[DisallowMultipleComponent, RequireComponent(typeof(Camera))]
public class MobileRenderPipelineCamera : MonoBehaviour
{
	[SerializeField] private CameraSettings settings = default;

	[System.NonSerialized] private ProfilingSampler m_Sampler;

	public ProfilingSampler Sampler =>
		m_Sampler ??= new ProfilingSampler(GetComponent<Camera>().name);

	public CameraSettings Settings => settings ??= new CameraSettings();

#if UNITY_EDITOR || DEVELOPMENT_BUILD
	private void OnEnable() => m_Sampler = null;
#endif
}
