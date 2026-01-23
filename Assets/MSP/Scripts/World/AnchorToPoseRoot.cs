using Sirenix.OdinInspector;
using Unity.VisualScripting;
using UnityEngine;

public class AnchorToPoseRoot : MonoBehaviour
{
	[SerializeField]
	[Required]
	private PoseData m_mapPoseData;

	[SerializeField]
	private Vector3 m_positionOffset = Vector3.zero;

	[SerializeField]
	private Vector2 m_minMaxOffsetScale = new Vector2(0,5);

	private void Awake()
	{
		ApplyOffset(m_mapPoseData.Position);
		m_mapPoseData.PositionChanged += ApplyOffset;
		m_mapPoseData.ScaleChanged += OnScaleChanged;
	}

	private void ApplyOffset(Vector3 a_position)
	{
		transform.position = a_position + (m_positionOffset * Mathf.Clamp(m_mapPoseData.Scale,m_minMaxOffsetScale.x,m_minMaxOffsetScale.y));
	}

	private void OnScaleChanged(float a_scale)
	{
		ApplyOffset(m_mapPoseData.Position);
	}

	private void OnDestroy()
	{
		m_mapPoseData.PositionChanged -= ApplyOffset;
		m_mapPoseData.ScaleChanged -= OnScaleChanged;
	}
}
