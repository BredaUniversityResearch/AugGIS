using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.XR.Hands;
using XRMultiplayer;

public class PlayerHandVisual : NetworkBehaviour
{
	[SerializeField]
	private HandPoseData m_handPoseData;

	[SerializeField]
	private SkinnedMeshRenderer m_handMeshRenderer;

	[SerializeField]
	private JointBasedHand m_jointBasedHand;

	private NetworkList<Vector3> m_syncedFingerEulerRotations = new NetworkList<Vector3>(new List<Vector3> (), NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
	public override void OnNetworkSpawn()
	{
		base.OnNetworkSpawn();
		if(IsOwner)
		{
			m_handMeshRenderer.enabled = false;
			SetupHandJoints();
		}
	}

	private void LateUpdate()
	{
		if(IsOwner)
		{
			//apply hand pose over the network
			UpdateHandPose();
		}
		else
		{
			//other clients read the updated hand pose
			SyncHandPose();
		}
	}

	private void SetupHandJoints()
	{
		for (int i = 0; i < m_jointBasedHand.handFidelityOptions[2].fingerJoints.Length; i++)
		{
			List<JointToTransformReference> currentJointTransformReferences = m_jointBasedHand.handFidelityOptions[2].fingerJoints[i].jointTransformReferences;
			for (int j = 0; j < currentJointTransformReferences.Count; j++)
			{
				m_syncedFingerEulerRotations.Add(Vector3.zero);
			}
		}
	}

	private void UpdateHandPose()
	{
		if (m_syncedFingerEulerRotations.Count == 0)
		{
			return;
		}
		
		transform.SetPositionAndRotation(m_handPoseData.Position,m_handPoseData.Rotation);
		int currentIndex = 0;
		for (int i = 0; i < m_jointBasedHand.handFidelityOptions[2].fingerJoints.Length; i++)
		{
			List<JointToTransformReference> currentJointTransformReferences = m_jointBasedHand.handFidelityOptions[2].fingerJoints[i].jointTransformReferences;
			for (int j = 0; j < currentJointTransformReferences.Count; j++)
			{
				JointToTransformReference currentJointTransformReference = currentJointTransformReferences[j];
				Vector3 fingerLocalEulerRotation = m_handPoseData.GetRotationForJoint(currentJointTransformReference.xrHandJointID);
				m_syncedFingerEulerRotations[currentIndex++] = fingerLocalEulerRotation;
				currentJointTransformReference.jointTransform.localRotation = Quaternion.Euler(fingerLocalEulerRotation);
			}
		}
	}

	private void SyncHandPose()
	{
		if(m_syncedFingerEulerRotations.Count == 0)
		{
			return;
		}
		
		int currentIndex = 0;
		for (int i = 0; i < m_jointBasedHand.handFidelityOptions[2].fingerJoints.Length; i++)
		{
			List<JointToTransformReference> currentJointTransformReferences = m_jointBasedHand.handFidelityOptions[2].fingerJoints[i].jointTransformReferences;
			for (int j = 0; j < currentJointTransformReferences.Count; j++)
			{
				JointToTransformReference currentJointTransformReference = currentJointTransformReferences[j];
				currentJointTransformReference.jointTransform.localRotation = Quaternion.Euler(m_syncedFingerEulerRotations[currentIndex++]);
			}
		}
	}
}
