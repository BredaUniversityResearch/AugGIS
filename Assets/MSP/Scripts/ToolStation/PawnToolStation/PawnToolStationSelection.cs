using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Events;

public class PawnToolStationSelection : ToolStationSelection
{
	[SerializeField]
	[Required]
	protected ToolStationElementConfig m_menuStationConfig;
	
	[SerializeField]
	[Required]
	private Transform m_visualsRoot;
	private GameObject m_currentSpawnedVisuals = null;

	public UnityEvent OnVisualsSet;

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

		if(m_stationIndexData.Value.CategoryIndex != -1 && m_stationIndexData.Value.EntryIndex != -1)
		{
			SetVisuals(m_menuStationConfig.categorySettings[m_stationIndexData.Value.CategoryIndex].categoryEntries[m_stationIndexData.Value.EntryIndex].visualPrefab);
		}
    }

    public void SetVisuals(GameObject a_visualsPrefab)
	{
		if (m_currentSpawnedVisuals != null)
		{
			Destroy(m_currentSpawnedVisuals);
		}

		m_currentSpawnedVisuals = Instantiate(a_visualsPrefab, m_visualsRoot);

		if (TryGetComponent<IPlaceableVisual>(out var visualStorage))
		{
			visualStorage.StoreVisuals(a_visualsPrefab);
		}

		m_interactableVisualHandler = m_currentSpawnedVisuals.GetComponent<InteractableMaterialHandler>();
		OnTrashStateChanged(!m_trashState.Value, m_trashState.Value);
        
#if UNITY_EDITOR
		if (!m_interactableVisualHandler)
		{
			Debug.Log("Cannot Assign Visuals to a pawn", this);
		}
#endif

		OnVisualsSet?.Invoke();
    }
}
