using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using POV_Unity;
using Unity.Netcode;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.XR.Hands;
using XRMultiplayer;

public class PlayerVisual : NetworkBehaviour
{
	[SerializeField]
	private TransformReference m_parentTransform;

	public override void OnNetworkSpawn()
	{
		base.OnNetworkSpawn();

		if (IsOwner)
		{
			ParentToPlayerVisualRoot();
		}
	}

	public override void OnLostOwnership()
	{
		base.OnLostOwnership();

		if (OwnerClientId == 0 && IsServer)
		{
			GetComponent<NetworkObject>().Despawn(destroy: true);
		}
	}

	public void ParentToPlayerVisualRoot()
	{
		GetComponent<NetworkObject>().TrySetParent(m_parentTransform.TransformRef);
	}

	public override void OnNetworkDespawn()
	{
		base.OnNetworkDespawn();
	}
}
