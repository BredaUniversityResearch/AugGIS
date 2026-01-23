using UnityEngine;
using Unity.Netcode;
using Sirenix.OdinInspector;
using System;
using System.Collections.Generic;
using NUnit.Framework;

public class SessionUserManager : NetworkBehaviour
{
	public event Action<SessionConnectedPlayerData[]> SessionConnectedPlayerDataListChanged;

	[SerializeField]
	[Required]
	private LocalPlayerSessionDataSO m_localPlayerSessionData;

	private NetworkList<SessionConnectedPlayerData> m_syncedConnectedPlayerDataList = new NetworkList<SessionConnectedPlayerData>(new List<SessionConnectedPlayerData>(), NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
	public int ConnectedPlayerCount => m_syncedConnectedPlayerDataList.Count;

	private SessionConnectedPlayerData m_localConnectedPlayerData;
	void Awake()
	{
		SessionManager.Instance.SetUserManager(this);
	}

	public override void OnNetworkSpawn()
	{
		base.OnNetworkSpawn();

		m_syncedConnectedPlayerDataList.OnListChanged += OnSyncedConnectedPlayerDataListChanged;

		if (IsServer)
		{
			NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDissconnected;
		}

		if(IsClient || IsHost)
		{
			m_localConnectedPlayerData = new SessionConnectedPlayerData();
			m_localConnectedPlayerData.playerName = m_localPlayerSessionData.playerName;
			m_localConnectedPlayerData.teamColor = m_localPlayerSessionData.teamColor;
			m_localConnectedPlayerData.clientID = NetworkManager.Singleton.LocalClientId;
			m_localConnectedPlayerData.isGameMaster = IsHost || m_syncedConnectedPlayerDataList.Count == 0; //if we are the host or we are the first to join we are immediatly promoted to game master

			AddPlayerConnectionDataServerRPC(m_localConnectedPlayerData);
		}
	}

	private void OnSyncedConnectedPlayerDataListChanged(NetworkListEvent<SessionConnectedPlayerData> a_changeEvent)
	{
		SessionConnectedPlayerData[] sessionConnectedPlayerData = new SessionConnectedPlayerData[m_syncedConnectedPlayerDataList.Count];

		for(int i = 0; i < sessionConnectedPlayerData.Length; i++)
		{
			sessionConnectedPlayerData[i] = m_syncedConnectedPlayerDataList[i];
		}

		SessionConnectedPlayerDataListChanged?.Invoke(sessionConnectedPlayerData);
	}

	public override void OnNetworkDespawn()
	{
		base.OnNetworkDespawn();

		if (IsServer)
		{
			NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDissconnected;
		}
	}

	public void DisconnectClient()
	{
		if (NetworkManager.Singleton == null)
		{
			return;
		}

		DisconnectClientServerRPC(NetworkManager.Singleton.LocalClientId);
	}

	public void PromoteClientToGameMaster(ulong a_clientId)
	{
		PromoteClientToGameMasterServerRPC(a_clientId);
	}

	public void DemoteClientFromGameMaster(ulong a_clientId)
	{
		DemoteClientFromGameMasterServerRPC(a_clientId);
	}

	public bool IsLocalClientGameMaster()
	{
		int index = FindSessionConnectedPlayerData(NetworkManager.Singleton.LocalClientId);

		if (index == -1)
		{
			return false;
		}

		return m_syncedConnectedPlayerDataList[index].isGameMaster;
	}

	[Rpc(SendTo.Server)]
	private void PromoteClientToGameMasterServerRPC(ulong a_clientId)
	{
		int index = FindSessionConnectedPlayerData(a_clientId);
		if (index == -1)
		{
			return;
		}

		SessionConnectedPlayerData data = m_syncedConnectedPlayerDataList[index];
		if (!data.isGameMaster)
		{
			data.isGameMaster = true;
			m_syncedConnectedPlayerDataList[index] = data;
		}
	}

	[Rpc(SendTo.Server)]
	private void DemoteClientFromGameMasterServerRPC(ulong a_clientId)
	{
		int index = FindSessionConnectedPlayerData(a_clientId);

		if (index == -1)
		{
			return;
		}

		SessionConnectedPlayerData data = m_syncedConnectedPlayerDataList[index];
		if (data.isGameMaster)
		{
			data.isGameMaster = false;
			m_syncedConnectedPlayerDataList[index] = data;
		}
	}

	private int FindSessionConnectedPlayerData(ulong a_clientId)
	{
		int foundIndex = -1;

		for (int i = 0; i < m_syncedConnectedPlayerDataList.Count; i++)
		{
			if (m_syncedConnectedPlayerDataList[i].clientID == a_clientId)
			{
				foundIndex = i;
				break;
			}
		}

		return foundIndex;
	}

	[Rpc(SendTo.Server, RequireOwnership = false)]
	private void DisconnectClientServerRPC(ulong a_clientId)
	{
		int index = FindSessionConnectedPlayerData(a_clientId);

		if (index == -1)
		{
			return;
		}

		RemovePlayerConnectionDataServerRPC(m_syncedConnectedPlayerDataList[index]);
		NetworkManager.Singleton.DisconnectClient(a_clientId);
	}

	[Rpc(SendTo.Server, RequireOwnership = false)]
	private void AddPlayerConnectionDataServerRPC(SessionConnectedPlayerData a_playerConnectedData)
	{
		if (m_syncedConnectedPlayerDataList.Contains(a_playerConnectedData))
		{
			Debug.LogWarning("Server Already Contains Player Connected Data with id: " + a_playerConnectedData.clientID);
			return;
		}

		m_syncedConnectedPlayerDataList.Add(a_playerConnectedData);
	}

	[Rpc(SendTo.Server, RequireOwnership = false)]
	private void RemovePlayerConnectionDataServerRPC(SessionConnectedPlayerData a_playerConnectedData)
	{
		if (!m_syncedConnectedPlayerDataList.Contains(a_playerConnectedData))
		{
			Debug.LogError("Server doesn't contain Player Connected Data with id: " + a_playerConnectedData.clientID);
			return;
		}

		m_syncedConnectedPlayerDataList.Remove(a_playerConnectedData);
	}

	private void OnClientDissconnected(ulong a_clientId)
	{
		int clientIndexToRemove = FindSessionConnectedPlayerData(a_clientId);
		if (clientIndexToRemove != -1)
		{
			m_syncedConnectedPlayerDataList.RemoveAt(clientIndexToRemove);
		}

		if (CalculateGameMasterPlayerCount() == 0 && m_syncedConnectedPlayerDataList.Count > 0)
		{
			PromoteClientToGameMasterServerRPC(NetworkManager.Singleton.ConnectedClientsIds[0]);
		}
	}

	private int CalculateGameMasterPlayerCount()
	{
		int gameMasterCount = 0;

		foreach (SessionConnectedPlayerData sessionConnectedPlayerData in m_syncedConnectedPlayerDataList)
		{
			if (sessionConnectedPlayerData.isGameMaster)
			{
				gameMasterCount++;
			}
		}

		return gameMasterCount;
	}

	public SessionConnectedPlayerData GetConnectedPlayerDataAtIndex(int a_index)
	{
		if (a_index < 0 || a_index >= m_syncedConnectedPlayerDataList.Count)
		{
			Debug.LogError("SessionConnectedPlayerData index is out of bounds");
		}

		return m_syncedConnectedPlayerDataList[a_index];
	}

	void OnApplicationQuit()
	{
		DisconnectClient();
	}
}
