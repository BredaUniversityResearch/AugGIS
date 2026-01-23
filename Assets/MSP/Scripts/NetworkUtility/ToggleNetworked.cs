using System;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Toggle))]
public class ToggleNetworked : NetworkBehaviour
{
	private Toggle m_toggle;

	private NetworkVariable<bool> m_isToggledSynced = new NetworkVariable<bool>(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

	private void Awake()
	{
		m_toggle = GetComponent<Toggle>();
	}

	public override void OnNetworkSpawn()
	{
		base.OnNetworkSpawn();
		m_toggle.onValueChanged.AddListener(OnToggleValueChanged);

		if (IsServer)
		{
			m_isToggledSynced.Value = m_toggle.isOn;
		}
		else
		{
			m_toggle.isOn = m_isToggledSynced.Value;
		}
	}

	public override void OnNetworkDespawn()
	{
		base.OnNetworkDespawn();
		m_toggle.onValueChanged.RemoveListener(OnToggleValueChanged);
	}

	private void OnToggleValueChanged(bool a_value)
	{
		ToggleValueChangedServerRpc(a_value, NetworkManager.Singleton.LocalClientId);
	}

	[Rpc(SendTo.Server,RequireOwnership = false)]
	private void ToggleValueChangedServerRpc(bool a_newValue, ulong a_clientId)
	{
		m_isToggledSynced.Value = a_newValue;
		ToggleValueChangedClientRpc(a_newValue, a_clientId);
	}

	[Rpc(SendTo.NotServer)]
	private void ToggleValueChangedClientRpc(bool a_newValue, ulong a_clientId)
	{
		if (a_clientId != NetworkManager.Singleton.LocalClientId)
		{
			m_toggle.onValueChanged.RemoveListener(OnToggleValueChanged);
			m_toggle.isOn = a_newValue;
			m_toggle.onValueChanged.AddListener(OnToggleValueChanged);
		}
	}
}
