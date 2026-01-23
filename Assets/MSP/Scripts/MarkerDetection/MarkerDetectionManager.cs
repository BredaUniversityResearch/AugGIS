using UnityEngine;

public class MarkerDetectionManager : MonoBehaviour
{
	private static MarkerDetectionManager s_instance = null;
	public static MarkerDetectionManager Instance => s_instance;

	[SerializeField]
	private QRMarkerDetector m_qrMarkerDetector = null;
	public QRMarkerDetector QRMarkerDetector => m_qrMarkerDetector;

	[SerializeField]
	private ArUcoMarkerDetector m_arUcoMarkerDetector = null;
	public ArUcoMarkerDetector ArUcoMarkerDetector => m_arUcoMarkerDetector;

	void Awake()
	{
		if (s_instance != null)
		{
			Debug.LogError("An Instance of Marker Detection Manager already exists!");
			Destroy(gameObject);
			return;
		}

		s_instance = this;
	}
}
