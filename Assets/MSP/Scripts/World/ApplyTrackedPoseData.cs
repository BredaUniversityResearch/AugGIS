using System;
using Sirenix.OdinInspector;
using Unity.VisualScripting;
using UnityEngine;

public class ApplyTrackedPoseData : MonoBehaviour
{
	[Serializable]
	[Flags]
	public enum ETrackingMode
	{
		Position = 1,
		Rotation = 2,
		Scale = 4,
		All = Position | Rotation | Scale
	}

	public bool ScaleYAxis = true;

	[SerializeField]
	PoseData m_trackedPoseData;

	[SerializeField]
	protected ETrackingMode m_trackingMode = ETrackingMode.All;

	[SerializeField]
	[ShowIf("@this.m_trackingMode.HasFlag(ETrackingMode.Scale)")]
	protected Vector2 m_minMaxScale = new Vector2(0.01f, 1000f);
	
	private void Awake()
	{
		if (m_trackingMode.HasFlag(ETrackingMode.Position))
		{
			m_trackedPoseData.PositionChanged += OnPositionChanged;
		}

		if (m_trackingMode.HasFlag(ETrackingMode.Rotation))
		{
			m_trackedPoseData.RotationChanged += OnRotationChanged;
		}

		if (m_trackingMode.HasFlag(ETrackingMode.Scale))
		{
			m_trackedPoseData.ScaleChanged += OnScaleChanged;
		}
	}

	private void OnDestroy()
	{
		if (m_trackingMode.HasFlag(ETrackingMode.Position))
		{
			m_trackedPoseData.PositionChanged -= OnPositionChanged;
		}

		if (m_trackingMode.HasFlag(ETrackingMode.Rotation))
		{
			m_trackedPoseData.RotationChanged -= OnRotationChanged;
		}

		if (m_trackingMode.HasFlag(ETrackingMode.Scale))
		{
			m_trackedPoseData.ScaleChanged -= OnScaleChanged;
		}
	}

	protected virtual void OnPositionChanged(Vector3 a_position)
	{
		transform.localPosition = a_position;
	}

	protected virtual void OnRotationChanged(Quaternion a_rotation)
	{
		transform.localRotation = a_rotation;
	}

	protected virtual void OnScaleChanged(float a_scale)
	{
		float scaleFactor = Mathf.Clamp(a_scale,m_minMaxScale.x,m_minMaxScale.y);
		if (ScaleYAxis)
			transform.localScale = new Vector3(scaleFactor, scaleFactor, scaleFactor);
		else
            transform.localScale = new Vector3(scaleFactor, transform.localScale.y, scaleFactor);
    }
}