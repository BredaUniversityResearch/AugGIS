using System;
using UnityEngine;
using UnityEngine.UIElements;

[CreateAssetMenu(fileName = "PoseData", menuName = "MSP/PoseData")]
public class PoseData : ScriptableObject
{
	public event Action<Vector3> PositionChanged;
	public event Action<Quaternion> RotationChanged;
	public event Action<float> ScaleChanged;

	private Vector3 m_position = Vector3.zero;
	public Vector3 Position => m_position;
	private Quaternion m_rotation = Quaternion.identity;
	public Quaternion Rotation => m_rotation;
	private float m_scale = 1;
	public float Scale => m_scale;

	private Transform m_rootTransform = null;
	public Transform RootTransform => m_rootTransform;

	public Matrix4x4 GetMapMatrix()
	{
		if (m_rootTransform == null)
		{
			Debug.LogWarning("PoseData: RootTransform is not set. Cannot get MapTransform.");
			return Matrix4x4.identity;
		}
		
		Matrix4x4 mapMatrix = Matrix4x4.TRS(m_position, m_rotation, Vector3.one * m_scale);

		return mapMatrix;
	}

	public void Reset()
	{
		m_position = Vector3.zero;
		m_rotation = Quaternion.identity;
		m_scale = 1;
	}

	public void SetPosition(Vector3 a_position)
	{
		m_position = a_position;
		PositionChanged?.Invoke(a_position);
	}

	public void SetRotation(Quaternion a_rotation)
	{
		m_rotation = a_rotation;
		RotationChanged?.Invoke(a_rotation);
	}

	public void SetScale(float a_scale)
	{
		m_scale = a_scale;
		ScaleChanged?.Invoke(a_scale);
	}

	public void SetRootTransform(Transform newRoot)
	{
		m_rootTransform = newRoot;
	}
}
