using POV_Unity;
using Unity.Netcode;
using UnityEngine;

public class UILayerProbeNetworked : NetworkBehaviour
{	
	private NetworkVariable<bool> m_uiEnabled = new NetworkVariable<bool>(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
	private NetworkVariable<int> m_pageIndexSynced = new NetworkVariable<int>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

	[SerializeField]
	private UILayerProbe m_uiLayerProbe = null;

	public override void OnNetworkSpawn()
	{
		base.OnNetworkSpawn();

		m_uiEnabled.OnValueChanged += OnUIEnabledChanged;
		m_pageIndexSynced.OnValueChanged += OnPageIndexSyncedValueChanged;
		m_uiLayerProbe.PagedList.CurrentPageChanged += OnLayerProbePagedListPageChanged;
		m_uiLayerProbe.OnEnabled += OnUIEnabledLocally;
		m_uiLayerProbe.OnDisabled += OnUIDisabledLocally;
		
		if (!NetworkManager.Singleton.IsServer && m_pageIndexSynced.Value > 0)
		{
			OnPageIndexSyncedValueChanged(-1, m_pageIndexSynced.Value);
		}
		OnUIEnabledChanged(false, m_uiEnabled.Value);
	}

	public override void OnNetworkDespawn()
	{
		base.OnNetworkDespawn();

		m_uiEnabled.OnValueChanged -= OnUIEnabledChanged;
		m_pageIndexSynced.OnValueChanged -= OnPageIndexSyncedValueChanged;
		m_uiLayerProbe.PagedList.CurrentPageChanged -= OnLayerProbePagedListPageChanged;
		m_uiLayerProbe.OnEnabled -= OnUIEnabledLocally;
		m_uiLayerProbe.OnDisabled -= OnUIDisabledLocally;
	}

	private void OnUIEnabledChanged(bool a_prevValue, bool a_newValue)
	{
		m_uiLayerProbe.gameObject.SetActive(a_newValue);
		// Otherwise current client was the one who changed the value, no need to set again.
	}

	[Rpc(SendTo.Server, RequireOwnership = false)]
	private void SetSyncedPageIndexValueServerRPC(int a_newPageIndex)
	{
		m_pageIndexSynced.Value = a_newPageIndex;
	}

	private void OnUIEnabledLocally()
	{
		if (!m_uiEnabled.Value && ImportedConfigRoot.Instance.ImportComplete)
		{
			EnableUIServerRPC();
		}
	}

	[Rpc(SendTo.Server, RequireOwnership = false)]
	private void EnableUIServerRPC()
	{
		m_uiEnabled.Value = true;
	}

	private void OnUIDisabledLocally()
	{
		if (m_uiEnabled.Value && ImportedConfigRoot.Instance.ImportComplete)
		{
			DisableUIServerRPC();
		}
	}

	[Rpc(SendTo.Server, RequireOwnership = false)]
	private void DisableUIServerRPC()
	{
		m_uiEnabled.Value = false;
		m_pageIndexSynced.Value = 0; // Page list will be reset when the UI is enabled again.
	}

	private void OnLayerProbePagedListPageChanged(int a_prevPageIndex, int a_newPageIndex)
	{
		SetSyncedPageIndexValueServerRPC(a_newPageIndex);
	}

	private void OnPageIndexSyncedValueChanged(int a_prevValue, int a_newValue)
	{
		m_uiLayerProbe.PagedList.ChangePage(a_newValue);
	}
}
