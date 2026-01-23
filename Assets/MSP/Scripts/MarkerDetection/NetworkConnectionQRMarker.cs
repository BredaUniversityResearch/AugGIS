using Newtonsoft.Json;
using Sirenix.OdinInspector;
using Sirenix.Utilities;
using UnityEngine;

public class NetworkConnectionQRMarker : AMarker
{
	public struct NetworkData
	{
		public string ip;
		public ushort port;
	}

	[SerializeField]
	[Required]
	private TMPro.TextMeshProUGUI m_textComponent;

	private Camera m_camera = null;

	private NetworkData m_decodedNetworkData;
	public NetworkData DecodedNetworkData => m_decodedNetworkData;

	private Camera ActiveCamera
	{
		get
		{
			if (m_camera == null)
			{
				m_camera = Camera.main;
			}

			return m_camera;
		}
	}

	public override void UpdateMarker(Vector3 a_targetPosition, Quaternion a_targetRotation, Vector3 a_targetScale, string a_decodedString)
	{
		base.UpdateMarker(a_targetPosition, a_targetRotation, a_targetScale, a_decodedString);

		if (a_decodedString == null)
		{
			Debug.LogError("Decoded String is null");
		}

		m_decodedNetworkData = JsonUtility.FromJson<NetworkData>(a_decodedString);
		m_textComponent.text = string.Format("IP: {0}\nPort: {1}", m_decodedNetworkData.ip, m_decodedNetworkData.port);
	}

	public override void Update()
	{
		base.Update();
		m_textComponent.transform.rotation = Quaternion.LookRotation(m_textComponent.transform.position - ActiveCamera.transform.position);
	}
}
