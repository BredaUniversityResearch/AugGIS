using UnityEngine;
using Unity.Netcode;
using Sirenix.OdinInspector;


[RequireComponent(typeof(Pen))]
public class PenNetworking : NetworkBehaviour
{
	private Pen m_pen;

	[SerializeField]
	[ReadOnly]
	private NetworkVariable<Color> m_syncedPenColor = new NetworkVariable<Color>(Color.white,NetworkVariableReadPermission.Everyone,NetworkVariableWritePermission.Owner);
	[SerializeField]
	[ReadOnly]
	private NetworkVariable<float> m_syncedPenWidth = new NetworkVariable<float>(1,NetworkVariableReadPermission.Everyone,NetworkVariableWritePermission.Owner);

	private void Awake()
	{
		m_pen = GetComponent<Pen>();

		m_pen.ColorChangedEvent += OnColorChanged;
		m_pen.WidthChangedEvent += OnWidthChanged;
		
		m_syncedPenColor.OnValueChanged += OnSyncedColorValueChanged;
		m_syncedPenWidth.OnValueChanged += OnSyncedWidthValueChanged;

		m_pen.ExternalLineSpawnerEvent += OnLineSpawned;
	}
	public override void OnNetworkSpawn()
	{
		base.OnNetworkSpawn();

		if (IsServer)
		{
			m_syncedPenColor.Value = m_pen.PenColor;
			m_syncedPenWidth.Value = m_pen.PenWidth;
		}
		else
		{
			m_pen.SetColorWithoutNotify(m_syncedPenColor.Value);
			m_pen.SetWidthWithoutNotify(m_syncedPenWidth.Value);
		}
	}

	public override void OnNetworkDespawn()
	{
		base.OnNetworkDespawn();
		m_pen.ExternalLineSpawnerEvent -= OnLineSpawned;
		m_syncedPenColor.OnValueChanged -= OnSyncedColorValueChanged;
		m_syncedPenWidth.OnValueChanged -= OnSyncedWidthValueChanged;
	}
	
	private void OnSyncedWidthValueChanged(float a_previousValue, float a_newValue)
	{
		m_pen.SetWidthWithoutNotify(a_newValue);
	}

	private void OnSyncedColorValueChanged(Color a_previousValue, Color a_newValue)
	{
		m_pen.SetColorWithoutNotify(a_newValue);
	}

	void OnLineSpawned(Vector3 a_newLinePos)
	{
		SpawnLineServerRpc(a_newLinePos, NetworkManager.Singleton.LocalClientId);
	}

	[Rpc(SendTo.Server,RequireOwnership = false)]
	void SpawnLineServerRpc(Vector3 a_localPosition, ulong a_clientId)
	{
		PenLine newLine = Instantiate(m_pen.LinePrefab);

		NetworkObject instanceNetworkObject = newLine.GetComponent<NetworkObject>();
		
		PenLine lineScript = instanceNetworkObject.GetComponent<PenLine>();
		
		instanceNetworkObject.SpawnWithOwnership(a_clientId);

		if(a_clientId == NetworkManager.Singleton.LocalClientId)
		{
			InternalInitialiseSpawnedLine(lineScript, instanceNetworkObject, a_localPosition);
		}
		else
		{
			InstantiateLineRpc(instanceNetworkObject.NetworkObjectId, a_clientId, a_localPosition);
		}
	}

	[Rpc(SendTo.NotServer,RequireOwnership = false)]
	void InstantiateLineRpc(ulong a_lineId, ulong a_clientId, Vector3 a_localPosition)
	{
		if (NetworkManager.Singleton.LocalClientId != a_clientId)
			return;

		NetworkObject[] ownedObjects = NetworkManager.SpawnManager.GetClientOwnedObjects(NetworkManager.Singleton.LocalClientId);

		foreach (NetworkObject networkObject in ownedObjects)
		{
			if (networkObject.NetworkObjectId == a_lineId)
			{
				InternalInitialiseSpawnedLine(networkObject.GetComponent<PenLine>(), networkObject, a_localPosition);
				break;
			}
		}
	}

	private void InternalInitialiseSpawnedLine(PenLine a_line, NetworkObject a_lineNetworkObject, Vector3 a_localPosition)
	{
		m_pen.SetCurrentLine(a_line);
		a_line.ChangeLineColor(m_syncedPenColor.Value);
		a_line.ChangeLineWidth(m_syncedPenWidth.Value);

		if (!a_lineNetworkObject.TrySetParent(m_pen.PenLineRootTransformReference.TransformRef))
		{
			Debug.LogError("Networked parenting failed for newly spawned line");
		}

		a_line.transform.localRotation = Quaternion.identity;
		a_line.transform.localPosition = a_localPosition;
	}

	private void OnWidthChanged(float a_width)
	{
		ChangeWidthValueOwnerRPC(a_width);
	}

	[Rpc(SendTo.Owner, RequireOwnership = false)]
	private void ChangeWidthValueOwnerRPC(float a_newWidth)
	{
		m_syncedPenWidth.Value = a_newWidth;
	}

	private void OnColorChanged(Color a_color)
	{
		ChangeColorValueOwnerRPC(a_color);
	}

	[Rpc(SendTo.Owner, RequireOwnership = false)]
	private void ChangeColorValueOwnerRPC(Color a_newColor)
	{
		m_syncedPenColor.Value = a_newColor;
	}
}
