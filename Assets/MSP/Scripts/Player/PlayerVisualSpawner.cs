using UnityEngine;
using Unity.Netcode;
using System;

public class PlayerVisualSpawner : NetworkBehaviour
{
	[SerializeField]
	private GameObject m_playerVisualPrefab;

	public void Awake()
	{
		SessionManager.Instance.SessionFSM.OnStateEnter += OnSessionStateEntered;
	}

	private void OnSessionStateEntered(Type a_stateType)
	{
		//For a dedicated server we do not want player visuals
#if !UNITY_SERVER
		if(a_stateType == typeof(WorldViewSessionState))
		{
			SpawnPlayerVisualsServerRPC(NetworkManager.Singleton.LocalClientId);
		}
#endif
	}

	[Rpc(SendTo.Server, RequireOwnership = false)]
	public void SpawnPlayerVisualsServerRPC(ulong a_clientID)
	{
		PlayerVisual playerVisuals = Instantiate(m_playerVisualPrefab).GetComponent<PlayerVisual>();
		NetworkObject playerNetworkObject = playerVisuals.GetComponent<NetworkObject>();
		playerNetworkObject.SpawnWithOwnership(a_clientID, true);
		playerVisuals.ParentToPlayerVisualRoot();
	}

	public override void OnNetworkDespawn()
	{
		base.OnNetworkDespawn();
		SessionManager.Instance.SessionFSM.OnStateEnter -= OnSessionStateEntered;
	}
}
