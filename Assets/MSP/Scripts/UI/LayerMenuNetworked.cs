using System;
using POV_Unity;
using Unity.Collections;
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

	//Names of the categories that have an entry in the menu, in menu order. Written by the
	//host only and append-only, so a category's index (what the selection above refers
	//to) never changes. Clients create their entries from this list.
	private NetworkList<FixedString128Bytes> m_categoryNames;
	private bool m_initialCategoriesPublished;

	private void Awake()
	{
		m_categoryNames = new NetworkList<FixedString128Bytes>();
		m_networkObject = GetComponent<NetworkObject>();
		m_layerMenu = GetComponent<LayerMenu>();
	}

	public override void OnNetworkSpawn()
	{
		base.OnNetworkSpawn();

		m_layerMenu.CategoryChangedEvent += (int index, int page) => { SetSelectedCategoryIndexServerRPC(index, page); };
		m_selectedCategoryPageIndexVar.OnValueChanged += OnSelectedPageIndexChanged;
		m_isEnabledNetworkVar.OnValueChanged += OnEnabledVariableChanged; 
		m_categoryNames.OnListChanged += OnCategoryNamesChanged;
		ImportedConfigRoot.Instance.m_onImportComplete += SyncCategories;
		ImportedConfigRoot.Instance.m_onLayerAdded += OnLayerAddedAfterImport;

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

		//In case the import has completed already
		SyncCategories();
	}

	public override void OnNetworkDespawn()
	{
		base.OnNetworkDespawn();
		m_selectedCategoryPageIndexVar.OnValueChanged -= OnSelectedPageIndexChanged;
		m_isEnabledNetworkVar.OnValueChanged -= OnEnabledVariableChanged; 
		m_categoryNames.OnListChanged -= OnCategoryNamesChanged;
		if (ImportedConfigRoot.Instance != null)
		{
			ImportedConfigRoot.Instance.m_onImportComplete -= SyncCategories;
			ImportedConfigRoot.Instance.m_onLayerAdded -= OnLayerAddedAfterImport;
		}
	}

	private void OnCategoryNamesChanged(NetworkListEvent<FixedString128Bytes> a_change)
	{
		SyncCategories();
	}

	//Makes the menu's category entries match the host's list. Entries need the config,
	//so this only does something once the import has completed; it is called again
	//when that happens and whenever the list changes.
	private void SyncCategories()
	{
		if (!IsSpawned || ImportedConfigRoot.Instance == null || !ImportedConfigRoot.Instance.ImportComplete)
		{
			return;
		}

		//The host publishes the categories that have layers once its import is done
		if (IsServer && !m_initialCategoriesPublished)
		{
			m_initialCategoriesPublished = true;
			foreach (string categoryName in m_layerMenu.GetNonEmptyCategoryNames())
			{
				AddCategoryName(categoryName);
			}
		}

		for (int i = m_layerMenu.CategoryCount; i < m_categoryNames.Count; i++)
		{
			m_layerMenu.AddCategory(m_categoryNames[i].ToString());
		}
	}

	//Host: a layer added after the initial import may be the first one of its category
	private void OnLayerAddedAfterImport(ALayer a_layer)
	{
		if (!IsServer || !m_initialCategoriesPublished || a_layer.Category == null)
		{
			return;
		}

		//Only layers the menu actually lists (same test the menu applies when showing a page)
		if (a_layer.DisplayMethods.Count == 0 || ImportedConfigRoot.Instance.m_displayMethodConfig.IsStaticLayer(a_layer))
		{
			return;
		}

		AddCategoryName(a_layer.Category.name);
	}

	private void AddCategoryName(string a_categoryName)
	{
		FixedString128Bytes name = default;
		if (name.CopyFromTruncated(a_categoryName) != CopyError.None)
		{
			Debug.LogWarning($"Layer menu category name was truncated: \"{a_categoryName}\"");
		}

		foreach (FixedString128Bytes existing in m_categoryNames)
		{
			if (existing.Equals(name))
			{
				return;
			}
		}
		m_categoryNames.Add(name);
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
