using PassthroughCameraSamples;
using UnityEngine;
using UnityEngine.UI;

public class XRAssignmCameraViewToImage : MonoBehaviour
{
	[SerializeField]
	private WebCamTextureManager m_webCamTextureManager;

	[SerializeField]
	private RawImage m_imageComponent;

	void Update()
	{
		if(m_webCamTextureManager.WebCamTexture != null)
		{
			m_imageComponent.texture = m_webCamTextureManager.WebCamTexture;
		}
	}
}
