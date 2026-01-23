using POV_Unity;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

public class LayerProbe : MonoBehaviour
{
	[SerializeField]
	[Required]
	private MapPlaceableTool m_mapPlaceableTool = null;

	[SerializeField]
	[Required]
	private XRGrabInteractable m_xrGrabInteractable = null;

	[SerializeField]
	[Required]
	private UILayerProbe m_uiLayerProbe = null;

	[SerializeField]
	private float m_maxDistance = 0.01f;

	[SerializeField]
	private bool m_showHiddenLayers = false;

	private const int MAX_LAYER_QUERY_DATA_COUNT = 100;
	private LayerManager.LayerQueryData[] m_layerQueryDatas = new LayerManager.LayerQueryData[MAX_LAYER_QUERY_DATA_COUNT];


	[SerializeField]
	private GameObject m_visualsPrefab;

	void Awake()
	{
		LayerManager.Instance.m_onLayerInstanceDataChanged += OnLayerInstanceDataChanged;

		m_mapPlaceableTool.AttachedToMap += OnPlacedOnMap;
		m_xrGrabInteractable.selectEntered.AddListener(OnTakenFromMap);
	}

	void Start()
	{
		if (m_uiLayerProbe.gameObject.activeSelf)
		{
			QueryLayers();
		}

		if (TryGetComponent<IPlaceableVisual>(out var placeableVisual))
		{
			placeableVisual.StoreVisuals(m_visualsPrefab);
		}
	}

    void OnDestroy()
	{
		LayerManager.Instance.m_onLayerInstanceDataChanged -= OnLayerInstanceDataChanged;

		m_mapPlaceableTool.AttachedToMap -= OnPlacedOnMap;
		m_xrGrabInteractable.selectEntered.RemoveListener(OnTakenFromMap);
	}

	private void OnLayerInstanceDataChanged(LayerInstanceData a_newData, ALayer a_layer)
	{
		if(m_mapPlaceableTool.IsPlaced)
		{
			QueryLayers();
		}
	}


	[Button]
	public void QueryLayers()
	{
		m_uiLayerProbe.ClearProbeData();

		Vector3 queryPosition = SessionManager.Instance.WorldManager.WorldPoseData.GetMapMatrix().inverse.MultiplyPoint3x4(transform.position);

		int queryCount = LayerManager.Instance.QueryLayersAtMapPosition(new Vector2(queryPosition.x, queryPosition.z), m_maxDistance, m_showHiddenLayers, ref m_layerQueryDatas);

		for (int i = 0; i < queryCount; i++)
		{
			m_uiLayerProbe.AddLayerQueryData(m_layerQueryDatas[i]);
		}
	}

	private void OnPlacedOnMap()
	{
		QueryLayers();
		m_uiLayerProbe.gameObject.SetActive(true);
	}
	private void OnTakenFromMap(SelectEnterEventArgs a_args)
	{
		m_uiLayerProbe.gameObject.SetActive(false);
	}
}
