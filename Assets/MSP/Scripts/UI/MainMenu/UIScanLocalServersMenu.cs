using System;
using System.Collections.Generic;
using System.Net;
using Sirenix.OdinInspector;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.VisualScripting;
using UnityEngine;

public class UIScanLocalServersMenu : MonoBehaviour
{
	public class UILocalServerData : IUIListElementData
	{
		public MSPLocalNetworkDiscovery.ServerResponseData serverData;

		public string Text => serverData.endPoint.Address?.ToString();
		public string ServerInfoText => string.Format("Local | <sprite=\"user\" index=0> {0}", serverData.discoveryResponseData.PlayerCount.ToString());
	}

	[SerializeField]
	[Required]
	private NetworkSessionConnectionData m_networkSessionConnectionData;

	[SerializeField]
	[Required]
	UIToggleablePagedList m_uiToggleableList;
	private MSPLocalNetworkDiscovery m_networkDiscovery = null;

	public bool AnyServersSelected => m_uiToggleableList.GetCurrentlySelectedElementData() != null;

	void Awake()
	{
		m_uiToggleableList.SelectedElementDataChanged += OnSelectedElementDataChanged;
	}

	private void OnEnable()
	{
		m_networkDiscovery = NetworkManager.Singleton.GetComponent<MSPLocalNetworkDiscovery>();
		Debug.Assert(m_networkDiscovery != null, "An Network Discovery must be attached to the network manager gameobject!");

		m_networkDiscovery.OnServerListChanged += OnServerListChanged;
		m_networkDiscovery.OnServerResponseReceived += OnServerResponseReceived;

		m_networkDiscovery.StartClient();

		m_uiToggleableList.ClearMenu();

		OnServerListChanged();
	}

	private void OnDisable()
	{
		m_networkDiscovery.OnServerListChanged -= OnServerListChanged;
		m_networkDiscovery.OnServerResponseReceived -= OnServerResponseReceived;

		m_networkDiscovery.StopDiscovery();
	}

	private void OnDestroy()
	{
		m_networkDiscovery.OnServerListChanged -= OnServerListChanged;
	}

	private void OnSelectedElementDataChanged(IUIListElementData data)
	{
		UILocalServerData uiLocalServerData = data as UILocalServerData;
		Debug.Assert(uiLocalServerData != null);

		m_networkSessionConnectionData.ip = uiLocalServerData.serverData.endPoint.Address;
		m_networkSessionConnectionData.Port = uiLocalServerData.serverData.discoveryResponseData.Port;
		m_networkSessionConnectionData.isServer = false;
	}

	private void OnServerListChanged()
	{
		if (m_uiToggleableList.ElementDataCount != m_networkDiscovery.ActiveServers.Count)
		{
			m_uiToggleableList.ClearMenu();
		}

		foreach (MSPLocalNetworkDiscovery.ServerResponseData activeServerData in m_networkDiscovery.ActiveServers)
		{
			UILocalServerData uiLocalServerData = new UILocalServerData();
			uiLocalServerData.serverData = activeServerData;

			if (!m_uiToggleableList.ContainsElementData(uiLocalServerData))
			{
				m_uiToggleableList.AddElementData(uiLocalServerData);
			}
		}
	}

	private void OnServerResponseReceived(MSPLocalNetworkDiscovery.ServerResponseData data)
	{
		m_uiToggleableList.RefreshAllElementData();
	}
}
