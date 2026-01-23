using System.Collections.Generic;
using POV_Unity;
using UnityEngine;
using UnityEngine.UI;
using Unity.Netcode;


public class LayerTagsCanvas : MonoBehaviour
{
	[SerializeField]
	private VerticalLayoutGroup m_verticalLayoutGroup;
	[SerializeField]
	Transform m_layerHandleFiller;

	[SerializeField]
	private bool m_reverseOrder;
	
	private LayerManager layerManager => LayerManager.Instance;
	private Dictionary<int, LayerTag> m_layerTags = new Dictionary<int, LayerTag>();

	void Awake()
	{
		ImportedConfigRoot.Instance.m_onImportComplete += OnConfigImportComplete;
		layerManager.m_onLayerInstanceDataChanged += OnLayerDataChanged;
		layerManager.m_onLayerTagDataChanged += OnLayerTagDataChanged;
		
		if (ImportedConfigRoot.Instance.ImportComplete)
		{
			OnConfigImportComplete();
		}
	}

	private void OnConfigImportComplete()
	{
		ReorderLayerTags();
		m_layerHandleFiller.SetSiblingIndex(transform.childCount - 1);
	}

	public void AddLayerTag(LayerTag a_layerTag, int a_layerIndex)
	{
		m_layerTags.Add(a_layerIndex, a_layerTag);
	}

	public void OnClientInitLayerTag(ALayer a_layer)
	{
		LayerTag layerTag = m_layerTags[a_layer.LayerIndex];
		layerTag.OnClientInit(a_layer, transform);
	}

	private void ReorderLayerTags()
    {
		foreach (int layerIndex in layerManager.LayerOrderNetworkData)
        {
			m_layerTags[layerIndex].transform.SetAsLastSibling();
        }
	}

	private void OnLayerDataChanged(LayerInstanceData a_newData, ALayer a_layer)
	{
		if (!ImportedConfigRoot.Instance.m_displayMethodConfig.IsStaticLayer(a_layer))
		{
			m_layerTags[a_layer.LayerIndex].OnLayerInstanceDataChanged(a_newData);
			m_layerHandleFiller.SetSiblingIndex(transform.childCount - 1);
		}
	}
	
	private void OnLayerTagDataChanged(LayerTagData a_newData)
	{
		if(!NetworkManager.Singleton.IsServer) return;

		LayerTag layerTag = m_layerTags[a_newData.m_layerIndex];
		if (layerTag.IsOwnedByServer)
		{
			layerTag.SetDeletionProgress(a_newData.m_deletionProgress);
			layerTag.OnLayerTagPositionChanged(a_newData);
		}
    }

	void OnValidate()
	{
		m_verticalLayoutGroup.reverseArrangement = m_reverseOrder;
	}
}
