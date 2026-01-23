using System;
using System.Collections;
using Unity.Netcode;
using UnityEngine;

[RequireComponent(typeof(MapPlaceableTool))]
public class MapPlaceableToolDuplicator : NetworkBehaviour, IPlaceableVisual
{
	public Action OnDuplicationComplete;
		
	[Header("Duplication Settings")]

	[SerializeField]
	private ToolType m_toolType;
	[SerializeField]
	private float m_timeToDuplicate = 1f;
	[SerializeField]
	private float m_failSafeBeforeDuplicating = 0.3f;
	[SerializeField]
	private Material m_hologramMaterial;
	
	private GameObject m_hologramInstance = null;
    public GameObject VisualsPrefab => m_hologramInstance;
	private GameObject m_hologramVisualsRoot;

	private MapPlaceableTool m_mapPlaceableTool;
	private ToolStationSelection m_toolStationSelection;

    void Awake()
	{
		m_mapPlaceableTool = GetComponent<MapPlaceableTool>();
		m_toolStationSelection = GetComponent<ToolStationSelection>();
	}

	public override void OnNetworkSpawn()
	{
		base.OnNetworkSpawn();

		m_toolStationSelection.Grabbed += OnGrabbed;
	}

    private IEnumerator DuplicatePawnCoroutine()
	{
		float elapsedTime = 0f;
		bool duplicationApproved = true;

		m_hologramInstance.SetActive(true);

		while (elapsedTime < m_timeToDuplicate)
		{
			elapsedTime += Time.deltaTime;

			if (!m_mapPlaceableTool.IsGrabbedLocally || !m_mapPlaceableTool.IsPlaced)
			{
				duplicationApproved = false;
				break;
			}

			if (elapsedTime >= m_failSafeBeforeDuplicating)
			{
				float hologramProgress = (elapsedTime - m_failSafeBeforeDuplicating) / (m_timeToDuplicate - m_failSafeBeforeDuplicating);

				foreach (var renderer in m_hologramInstance.GetComponentsInChildren<Renderer>())
				{
					renderer.material.SetFloat("_DissolveValue", 1f - hologramProgress);
				}
			}

			yield return null;
		}

		HologramReset();

		if (duplicationApproved)
		{
			DuplicatePlaceableServerRPC(m_mapPlaceableTool.GetSnappedLocalPosition(), m_mapPlaceableTool.GetSnappedLocalRotation());
			m_mapPlaceableTool.PlaceableWasDuplicated();
			OnDuplicationComplete?.Invoke();
		}
	}

	private void HologramReset()
	{
		foreach (var renderer in m_hologramInstance.GetComponentsInChildren<Renderer>())
		{
			renderer.material.SetFloat("_DissolveValue", 1f);
		}
		m_hologramInstance.SetActive(false);
	}

	// sometimes this RPC throws an error ownership not being assigned.
	[Rpc(SendTo.Server, RequireOwnership = true)]
	private void DuplicatePlaceableServerRPC(Vector3 a_spawnLocalPosition, Quaternion a_spawnLocalRotation)
	{
		GameObject duplicatedPlaceable = Instantiate(SessionManager.Instance.WorldManager.ToolPrefabRegistry.prefabs[m_toolType]);
		MapPlaceableTool duplicatedMapPlaceableTool = duplicatedPlaceable.GetComponent<MapPlaceableTool>();
		ToolStationSelection duplicatedToolStationSelection = duplicatedPlaceable.GetComponent<ToolStationSelection>();

		duplicatedPlaceable.name = this.name + "_" + duplicatedToolStationSelection.SetCount;

		duplicatedMapPlaceableTool.SetDefaultIsPlaced(true);

		duplicatedToolStationSelection.SetDefaultEntryIndices(
			this.m_toolStationSelection.StationIndexData.CategoryIndex,
			this.m_toolStationSelection.StationIndexData.EntryIndex);

		duplicatedToolStationSelection.SetDefaultState(ToolStationSelection.EState.Released);

		duplicatedPlaceable.transform.SetParent(this.transform.parent); // Should give the same result as HandleParentingLocally()
		duplicatedPlaceable.transform.localPosition = a_spawnLocalPosition;
		duplicatedPlaceable.transform.localRotation = a_spawnLocalRotation;
		duplicatedPlaceable.GetComponent<NetworkObject>().Spawn();
	}

	private void OnGrabbed()
	{
		if (m_mapPlaceableTool.IsPlaced)
		{
			StartCoroutine(DuplicatePawnCoroutine());
		}
	}

	public void StoreVisuals(GameObject a_visualsObject)
    {
		if (m_hologramInstance != null)
		{
			Destroy(m_hologramInstance);
		}
		else
        {
            m_hologramVisualsRoot = new GameObject("HologramVisualsRoot");
			m_hologramVisualsRoot.transform.SetParent(transform);
			m_hologramVisualsRoot.transform.localPosition = Vector3.zero;
			m_hologramVisualsRoot.transform.localRotation = Quaternion.identity;
			m_hologramVisualsRoot.transform.localScale = m_mapPlaceableTool.VisualsRoot.localScale;
        }

        m_hologramInstance = Instantiate(a_visualsObject);
		m_hologramInstance.transform.SetParent(m_hologramVisualsRoot.transform, false);

		foreach (Renderer rend in m_hologramInstance.GetComponentsInChildren<Renderer>())
		{
			rend.material = new Material(m_hologramMaterial);
		}

		HologramReset();
    }
}
