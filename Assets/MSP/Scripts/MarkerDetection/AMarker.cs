using Sirenix.OdinInspector;
using UnityEngine;

public abstract class AMarker : SerializedMonoBehaviour
{
	[SerializeField]
	private bool disableMarkerOnTimeout = true;

	[SerializeField]
	[ShowIf("disableMarkerOnTimeout")]
	private float m_deactivateMarkerTime = 2f;

	private string m_stringData;
	public string DecodedStringData => m_stringData;

	private float m_lastUpdateTime = 0f;

	public virtual void UpdateMarker(Vector3 a_targetPosition, Quaternion a_targetRotation, Vector3 a_targetScale, string a_decodedString)
	{
		transform.position = a_targetPosition;
		transform.rotation = a_targetRotation;
		transform.localScale = a_targetScale;

		m_stringData = a_decodedString;

		m_lastUpdateTime = Time.time;

		if (!gameObject.activeSelf)
		{
			gameObject.SetActive(true);
		}
	}

	public virtual void Update()
	{
		if (disableMarkerOnTimeout && gameObject.activeSelf && Time.time - m_lastUpdateTime > m_deactivateMarkerTime)
		{
			gameObject.SetActive(false);
			m_stringData = string.Empty;
		}
	}
}
