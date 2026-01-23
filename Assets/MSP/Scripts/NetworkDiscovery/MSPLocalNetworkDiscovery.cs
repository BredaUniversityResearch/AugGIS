using System;
using System.Collections.Generic;
using System.Net;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityEngine.Events;

[RequireComponent(typeof(NetworkManager))]
public class MSPLocalNetworkDiscovery : NetworkDiscovery<DiscoveryBroadcastData, DiscoveryResponseData>
{
	public class ServerResponseData
	{
		public IPEndPoint endPoint;
		public DiscoveryResponseData discoveryResponseData;
		public float elapsedTime;
	}

	NetworkManager m_networkManager;
	public Action OnServerListChanged;
	public Action<ServerResponseData> OnServerResponseReceived;

	[SerializeField]
	private float m_serverTimeoutTimeInSeconds = 4;
	private List<ServerResponseData> m_activeServerData = new List<ServerResponseData>();
	public List<ServerResponseData> ActiveServers => m_activeServerData;

	[SerializeField]
	private float m_scanCooldownTimer = 2f;

	private float m_scanElapsedTimer = 0;
	private int m_activeServersCountCache = 0;

	public void Awake()
	{
		m_networkManager = GetComponent<NetworkManager>();
		m_networkManager.OnServerStarted += OnServerStarted;
		m_networkManager.OnServerStopped += OnServerStopped;
		m_scanElapsedTimer = 0;
	}

	void Update()
	{
		if (!IsClient || !IsRunning)
		{
			return;
		}

		m_scanElapsedTimer -= Time.deltaTime;
		if (m_scanElapsedTimer <= 0f)
		{
			ClientBroadcast(new DiscoveryBroadcastData());
			m_scanElapsedTimer = m_scanCooldownTimer;
		}

		for (int i = m_activeServerData.Count - 1; i >= 0; i--)
		{
			m_activeServerData[i].elapsedTime -= Time.deltaTime;

			if (m_activeServerData[i].elapsedTime <= 0)
			{
				m_activeServerData.RemoveAt(i);
			}
		}

		if (m_activeServersCountCache != m_activeServerData.Count)
		{
			OnServerListChanged?.Invoke();
			m_activeServersCountCache = m_activeServerData.Count;
		}
	}

	protected override bool ProcessBroadcast(IPEndPoint a_sender, DiscoveryBroadcastData a_broadCast, out DiscoveryResponseData a_response)
	{
		a_response = new DiscoveryResponseData()
		{
			ServerName = a_sender.Address.ToString(),
			Port = ((UnityTransport)m_networkManager.NetworkConfig.NetworkTransport).ConnectionData.Port,
			PlayerCount = m_networkManager.ConnectedClients.Count
		};
		return true;
	}

	protected override void ResponseReceived(IPEndPoint a_sender, DiscoveryResponseData a_response)
	{
		int foundIndex = m_activeServerData.FindIndex(x => x.endPoint.Address.ToString() == a_sender.Address.ToString());

		if (foundIndex != -1)
		{
			ServerResponseData serverResponseData = m_activeServerData[foundIndex];
			serverResponseData.elapsedTime = m_serverTimeoutTimeInSeconds;
			serverResponseData.discoveryResponseData = a_response;
			OnServerResponseReceived?.Invoke(serverResponseData);
		}
		else
		{
			ServerResponseData serverData = new ServerResponseData() { elapsedTime = m_serverTimeoutTimeInSeconds, endPoint = a_sender, discoveryResponseData = a_response };
			m_activeServerData.Add(serverData);
			OnServerResponseReceived?.Invoke(serverData);
		}
	}

	private void OnServerStarted()
	{
		if (m_networkManager.IsServer)
		{
			StartServer();
		}
	}

	private void OnServerStopped(bool obj)
	{
		if (m_networkManager.IsServer)
		{
			StopDiscovery();
		}
	}
}