using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.XR.Hands;

public class HandPoseUpdater : MonoBehaviour
{
	[SerializeField]
	private Transform m_handTransform;

	[SerializeField]
	private HandPoseData m_handPoseData;

	[SerializeField]
	private XRHandSkeletonDriver m_xrHandSkeletonDriver;

	[SerializeField]
	private Vector3 m_offset;

	private void Start()
	{
		m_handPoseData.Reset();
		foreach(JointToTransformReference jointToTransformReference in m_xrHandSkeletonDriver.jointTransformReferences)
		{
			m_handPoseData.AddHandJoint(jointToTransformReference.xrHandJointID);
		}
	}
	
	private void Update()
	{
		m_handPoseData.SetPose(m_handTransform.transform.position - m_handTransform.TransformDirection(m_offset),m_handTransform.transform.rotation);

		foreach (JointToTransformReference jointToTransformReference in m_xrHandSkeletonDriver.jointTransformReferences)
		{
			m_handPoseData.UpdateHandJoint(jointToTransformReference.xrHandJointID,jointToTransformReference.jointTransform.localRotation.eulerAngles);
		}
	}
}
