using Sirenix.OdinInspector;
using Unity.Netcode;
using UnityEngine;

public class ToolStationSpawner : NetworkBehaviour
{
	[SerializeField]
	[Required]
	private TransformReference m_toolStationRootTransformRef;

	[SerializeField]
	[Required]
	private PawnToolStation m_pawnToolStaionPrefab = null;

	[SerializeField]
	[Required]
	private PenToolStation m_penToolStationPrefab = null;

	[SerializeField]
	[Required]
	private EraserToolStation m_eraserToolStationPrefab = null;

	[SerializeField]
	[Required]
	private LayerProbeToolStation m_layerProbeToolStationPrefab = null;

	[SerializeField]
	[Required]
	private GameObject m_toolTrashPrefab = null;

	[SerializeField]
	private AudioPreset m_toolStationSpawnAudioPreset = null;

	[Button]
	public void SpawnPawnToolStation(Vector3 a_worldPosition)
	{
		SpawnPawnToolStationServerRPC(a_worldPosition);
	}

	[Button]
	public void SpawnPenToolStation(Vector3 a_worldPosition)
	{
		SpawnPenToolStationServerRPC(a_worldPosition);
	}

	[Button]
	public void SpawnEraserToolStation(Vector3 a_worldPosition)
	{
		SpawnEraserToolStationServerRPC(a_worldPosition);
	}

	[Button]
	public void SpawnLayerProbeToolStation(Vector3 a_worldPosition)
	{
		SpawnLayerProbeToolStationServerRPC(a_worldPosition);
	}

	[Button]
	public void SpawnTrashToolStation(Vector3 a_worldPosition)
	{
		SpawnTrashToolStationServerRPC(a_worldPosition);
	}

	[Rpc(SendTo.Server, RequireOwnership = false)]
	private void SpawnPawnToolStationServerRPC(Vector3 a_worldPosition)
	{
		PawnToolStation pawnToolStation = Instantiate(m_pawnToolStaionPrefab);
		NetworkObject spawnedToolNetworkObject = pawnToolStation.GetComponent<NetworkObject>();
		InitialiseSpawnedNetworkObject(spawnedToolNetworkObject, a_worldPosition);
	}

	[Rpc(SendTo.Server, RequireOwnership = false)]
	private void SpawnPenToolStationServerRPC(Vector3 a_worldPosition)
	{
		PenToolStation penToolStation = Instantiate(m_penToolStationPrefab);
		NetworkObject spawnedToolNetworkObject = penToolStation.GetComponent<NetworkObject>();
		InitialiseSpawnedNetworkObject(spawnedToolNetworkObject, a_worldPosition);
	}

	[Rpc(SendTo.Server, RequireOwnership = false)]
	private void SpawnEraserToolStationServerRPC(Vector3 a_worldPosition)
	{
		EraserToolStation eraserToolStation = Instantiate(m_eraserToolStationPrefab);
		NetworkObject spawnedToolNetworkObject = eraserToolStation.GetComponent<NetworkObject>();
		InitialiseSpawnedNetworkObject(spawnedToolNetworkObject, a_worldPosition);
	}

	[Rpc(SendTo.Server, RequireOwnership = false)]
	private void SpawnLayerProbeToolStationServerRPC(Vector3 a_worldPosition)
	{
		LayerProbeToolStation layerProbeToolStation = Instantiate(m_layerProbeToolStationPrefab);
		NetworkObject spawnedToolNetworkObject = layerProbeToolStation.GetComponent<NetworkObject>();
		InitialiseSpawnedNetworkObject(spawnedToolNetworkObject, a_worldPosition);
	}

	[Rpc(SendTo.Server, RequireOwnership = false)]
	private void SpawnTrashToolStationServerRPC(Vector3 a_worldPosition)
	{
		GameObject toolTrash = Instantiate(m_toolTrashPrefab);
		NetworkObject spawnedToolNetworkObject = toolTrash.GetComponent<NetworkObject>();
		InitialiseSpawnedNetworkObject(spawnedToolNetworkObject, a_worldPosition);
	}

	private void InitialiseSpawnedNetworkObject(NetworkObject a_spawnedNetworkObject, Vector3 a_worldPosition)
	{
		a_spawnedNetworkObject.Spawn();
		AudioManager.Instance.PlaySound3D(m_toolStationSpawnAudioPreset, a_worldPosition);
		a_spawnedNetworkObject.transform.parent = m_toolStationRootTransformRef.TransformRef;
		a_spawnedNetworkObject.transform.position = a_worldPosition;
		SetUpParentOfSpawnedPrefabClientRPC(a_spawnedNetworkObject, a_worldPosition);
	}

	[Rpc(SendTo.NotServer)]
	private void SetUpParentOfSpawnedPrefabClientRPC(NetworkObjectReference a_spawnedPrefabNetworkObject, Vector3 a_worldPosition)
	{
		if (a_spawnedPrefabNetworkObject.TryGet(out NetworkObject networkObject))
		{
			networkObject.transform.parent = transform.parent;
			networkObject.transform.position = a_worldPosition;
		}
	}
}
