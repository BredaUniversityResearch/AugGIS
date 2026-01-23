using System;
using POV_Unity;
using Unity.Netcode;
using Unity.VisualScripting;
using UnityEngine;

[RequireComponent(typeof(NetworkObject))]
[RequireComponent(typeof(LayerMenu))]
public class LayerMenuNetworked : NetworkBehaviour
{
	private NetworkObject m_networkObject;
	private LayerMenu m_layerMenu = null;

	private NetworkVariable<bool> m_isEnabledNetworkVar = new NetworkVariable<bool>(true,NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
	//x = category, y = page
	private NetworkVariable<Vector2Int> m_selectedCategoryPageIndexVar = new NetworkVariable<Vector2Int>(new Vector2Int(0,1), NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

	private void Awake()
	{
		m_networkObject = GetComponent<NetworkObject>();
		m_layerMenu = GetComponent<LayerMenu>();
	}

	public override void OnNetworkSpawn()
	{
		base.OnNetworkSpawn();

		m_layerMenu.CategoryChangedEvent += (int index, int page) => { SetSelectedCategoryIndexServerRPC(index, page); };
		m_selectedCategoryPageIndexVar.OnValueChanged += OnSelectedPageIndexChanged;
		m_isEnabledNetworkVar.OnValueChanged += OnEnabledVariableChanged; 

		m_layerMenu.MenuEnabledEvent.AddListener(() => { SetMenuEnabledValueServerRPC(true); });
		m_layerMenu.MenuDisabledEvent.AddListener(() => { SetMenuEnabledValueServerRPC(false); });

		//sync the index for the late joining clients without actually selecting it.
		//main reason we dont want to select immediately on spawn is beacuse the layer menu will select a category once the import config has finish importing.
		m_layerMenu.SetCategoryIndexWithoutNotify(m_selectedCategoryPageIndexVar.Value.x);
		
		//sync the enabled/disabled state of the menu for all late joining clients
		if (m_isEnabledNetworkVar.Value)
		{
			m_layerMenu.EnableMenu();
		}
		else
		{
			m_layerMenu.DisableMenu();
		}
	}

	public override void OnNetworkDespawn()
	{
		base.OnNetworkDespawn();
		m_selectedCategoryPageIndexVar.OnValueChanged -= OnSelectedPageIndexChanged;
		m_isEnabledNetworkVar.OnValueChanged -= OnEnabledVariableChanged; 
	}

	private void OnSelectedPageIndexChanged(Vector2Int previousValue, Vector2Int newValue)
	{
		m_layerMenu.SelectCategory(newValue.x, newValue.y);
	}
	
	private void OnEnabledVariableChanged(bool previousValue, bool newValue)
	{
		if (newValue)
		{
			m_layerMenu.EnableMenu();
		}
		else
		{
			m_layerMenu.DisableMenu();
		}
	}

	[Rpc(SendTo.Server, RequireOwnership = false)]
	private void SetMenuEnabledValueServerRPC(bool enabled)
	{
		m_isEnabledNetworkVar.Value = enabled;
	}

	[Rpc(SendTo.Server, RequireOwnership = false)]
	private void SetSelectedCategoryIndexServerRPC(int catIndex, int pageIndex)
	{
		Vector2Int newIndex = new Vector2Int(catIndex, pageIndex);

		if (m_selectedCategoryPageIndexVar.Value == newIndex)
		{
			newIndex.x = -1;
		}

		m_selectedCategoryPageIndexVar.Value = newIndex;

	}
}
