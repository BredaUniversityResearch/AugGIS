using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.XR.Hands;

[CreateAssetMenu(fileName = "HandPoseData", menuName = "MSP/HandPoseData")]
public class HandPoseData : ScriptableObject
{
	private Vector3 m_position;
	public Vector3 Position => m_position;

	private Quaternion m_rotation;
	public Quaternion Rotation => m_rotation;

	private Dictionary<XRHandJointID,Vector3> m_handIdToRotationEulerMap = new Dictionary<XRHandJointID, Vector3>();

	public int HandJointCount => m_handIdToRotationEulerMap.Count;

	public void SetPose(Vector3 a_position, Quaternion a_rotation)
	{
		m_position = a_position;
		m_rotation = a_rotation;
	}

	public void AddHandJoint(XRHandJointID a_xrHandJointID)
	{
		m_handIdToRotationEulerMap[a_xrHandJointID] = Vector3.zero;
	}

	public void UpdateHandJoint(XRHandJointID a_xrHandJointID,Vector3 a_eulerRotation)
	{
		m_handIdToRotationEulerMap[a_xrHandJointID] = a_eulerRotation;
	}

	public Vector3 GetRotationForJoint(XRHandJointID a_xrHandJointID)
	{
		if(!m_handIdToRotationEulerMap.ContainsKey(a_xrHandJointID))
		{
			return Vector3.zero;
		}
	
		return m_handIdToRotationEulerMap[a_xrHandJointID];
	}

	public void Reset()
	{
		m_position = Vector3.zero;
		m_rotation = Quaternion.identity;
		m_handIdToRotationEulerMap.Clear();
	}
}