using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;
using System.Linq;

namespace POV_Unity
{
	public class LayerManager : NetworkBehaviour
	{
		public struct LayerQueryData
		{
			public ALayer layer;
			public string typeData;
			public Vector2 queryPosition;
		}

		public enum ELayerVisualisationSetting
		{
			Basic,
			Fancy
		}

		private static LayerManager ms_instance;
		public static LayerManager Instance
		{
			get
			{
				if (ms_instance == null)
				{
					ms_instance = FindAnyObjectByType<LayerManager>();
				}
				return ms_instance;
			}
		}

		public delegate void OnLayerInstanceDataChanged(LayerInstanceData a_newData, ALayer a_layer);
		public event OnLayerInstanceDataChanged m_onLayerInstanceDataChanged;

		public delegate void OnLayerTagDataChanged_ (LayerTagData a_newData);
		public event OnLayerTagDataChanged_ m_onLayerTagDataChanged;

		public delegate void OnDMInstanceDataChanged(DMInstanceData a_newData);
		public event OnDMInstanceDataChanged m_onDMInstanceDataChanged;

		[SerializeField]
		private int m_layerMaxVerticalStepCount = 10;
		public int LayerMaxVerticalStepCount => m_layerMaxVerticalStepCount;

		[SerializeField]
		private float m_layerVerticalStepDisplacement = 0.05f;
		public float LayerVerticalStepDisplacement => m_layerVerticalStepDisplacement;

		Dictionary<int, ALayer> m_layers = new Dictionary<int, ALayer>();
		public IEnumerable<ALayer> AllLayers => m_layers.Values;
		NetworkList<DMInstanceData> m_DMNetworkData;
		NetworkList<LayerInstanceData> m_layerNetworkData;
		NetworkList<int> m_layerOrderNetworkData;
		public NetworkList<int> LayerOrderNetworkData => m_layerOrderNetworkData;
		NetworkList<LayerTagData> m_layerTagNetworkData;
		List<int>[] m_handlesPerStep; //Ids of layer handles, only up to date on server

		private LayerVisualizationMode m_layerVisualisationSetting = LayerVisualizationMode.Basic;
		public LayerVisualizationMode LayerVisualisationSetting => m_layerVisualisationSetting;

		[SerializeField]
		private LayerTagsCanvas m_layerTagCanvasWest;

		[SerializeField]
		private LayerTagsCanvas m_layerTagCanvasEast;

		[SerializeField]
		private LayerTag m_layerTagPrefab;

		//VectorLayers sorted by their render queue
		private List<VectorLayer> m_sortedVectorLayers = new List<VectorLayer>();
		public List<VectorLayer> SortedVectorLayers => m_sortedVectorLayers;

		[SerializeField] PoseData m_worldPoseData;
		[SerializeField] private ImportedConfigRoot m_importConfigRoot;

		private void Awake()
		{
			if (ms_instance != null && ms_instance != this)
			{
				Debug.LogError("instance of LayerManager already exists, removing new instance");
				Destroy(gameObject);
			}
			ms_instance = this;

			m_DMNetworkData = new NetworkList<DMInstanceData>();
			m_DMNetworkData.OnListChanged += OnDisplayMethodDataChanged;
			m_layerNetworkData = new NetworkList<LayerInstanceData>();
			m_layerNetworkData.OnListChanged += OnLayerDataChanged;
			m_layerTagNetworkData = new NetworkList<LayerTagData>();
			m_layerTagNetworkData.OnListChanged += OnLayerTagDataChanged;
			m_layerOrderNetworkData = new NetworkList<int>();
			m_handlesPerStep = new List<int>[m_layerMaxVerticalStepCount];
			for (int i = 0; i < m_layerMaxVerticalStepCount; i++)
			{
				m_handlesPerStep[i] = new List<int>();
			}

			m_sortedVectorLayers.Clear();

			m_importConfigRoot.m_onLayerImported += OnLayerImported;
			m_importConfigRoot.m_onDMAddedToLayer += OnDMAddedToLayer;
			m_importConfigRoot.m_onImportComplete += OnConfigImportComplete;

			m_worldPoseData.ScaleChanged += OnWorldScaleChanged;
		}

		void Update()
		{
#if !UNITY_SERVER
			if (!ImportedConfigRoot.Instance.ImportComplete) return;
			InstanceRenderLayerDM();
#endif
		}

		public override void OnDestroy()
		{
			m_worldPoseData.ScaleChanged -= OnWorldScaleChanged;

			base.OnDestroy();
		}

		private void InstanceRenderLayerDM()
		{
			//update loop for all display methods that are active
			foreach (LayerInstanceData layerInstanceData in m_layerNetworkData)
			{
				if (layerInstanceData.m_visualizationMode == LayerVisualizationMode.Off)
				{
					continue;
				}

				ALayer layer = m_layers[layerInstanceData.m_layerId];
				for (int i = 0; i < layer.DisplayMethods.Count; i++)
				{
					ADisplayMethod dm = layer.DisplayMethods[i];
					IDisplayMethodRenderData displayMethodRenderData = layer.DisplayMethodRenderData[i];

					if (displayMethodRenderData == null)
					{
						continue;
					}

					if (dm.visualization_mode != layerInstanceData.m_visualizationMode)
					{
						continue;
					}

					dm.Render(layer.DisplayMethodRenderData[i]);
				}
			}
		}

		//The NetworkLists below are positional; look entries up by layer id instead of
		//assuming list position equals LayerId.
		private int IndexOfLayerData(int a_layerId)
		{
			for (int i = 0; i < m_layerNetworkData.Count; i++)
			{
				if (m_layerNetworkData[i].m_layerId == a_layerId) return i;
			}
			Debug.LogError($"No LayerInstanceData for layer id {a_layerId}");
			return -1;
		}

		private int IndexOfTagData(int a_layerId)
		{
			for (int i = 0; i < m_layerTagNetworkData.Count; i++)
			{
				if (m_layerTagNetworkData[i].m_layerId == a_layerId) return i;
			}
			Debug.LogError($"No LayerTagData for layer id {a_layerId}");
			return -1;
		}

		public void RegisterDisplayMethod(int a_layerId, int a_DMIndexInLayer)
		{
			m_DMNetworkData.Add(new DMInstanceData()
			{
				m_active = true,
				m_layerId = a_layerId,
				m_DMIndexInLayer = a_DMIndexInLayer
			});
		}

		void OnLayerImported(ALayer a_layer)
		{
			m_layers.Add(a_layer.LayerId, a_layer);

			bool IsStartingLayer = ImportedConfigRoot.Instance.m_displayMethodConfig.IsStartingLayer(a_layer);
			bool isStaticLayer = ImportedConfigRoot.Instance.m_displayMethodConfig.IsStaticLayer(a_layer);

			if (NetworkManager.Singleton.IsServer || NetworkManager.Singleton.IsHost)
			{
				if (IsStartingLayer && !isStaticLayer)
				{
					m_handlesPerStep[0].Add(a_layer.LayerId);
					m_layerOrderNetworkData.Add(a_layer.LayerId);
				}

				LayerInstanceData layerInstanceData = new LayerInstanceData()
				{
					m_visualizationMode = (IsStartingLayer || isStaticLayer) ? LayerVisualizationMode.Basic : LayerVisualizationMode.Off,
					m_layerId = a_layer.LayerId,
					m_verticalStep = 0,
					m_indexInStep = 0,
					m_ownerID = -1,
				};
				m_layerNetworkData.Add(layerInstanceData);

				LayerTagData layerTagData = new LayerTagData()
				{
					m_layerId = a_layer.LayerId,
					m_deletionProgress = 0f,
					m_newLocalZValue = 0f
				};
				m_layerTagNetworkData.Add(layerTagData);
			}

			if (!isStaticLayer)
			{
				if (NetworkManager.Singleton.IsServer)
				{
					SpawnLayerTagsServer(a_layer.LayerId);
				}

				m_layerTagCanvasWest.OnClientInitLayerTag(a_layer);
				m_layerTagCanvasEast.OnClientInitLayerTag(a_layer);
			}
		}

		private void OnDMAddedToLayer(ALayer a_layer, ADisplayMethod a_displayMethod, int a_DMIndexInLayer)
		{
			if (NetworkManager.Singleton.IsServer || NetworkManager.Singleton.IsHost)
			{
				//Add DM data
				m_DMNetworkData.Add(new DMInstanceData()
				{
					m_active = true,
					m_layerId = a_layer.LayerId,
					m_DMIndexInLayer = a_DMIndexInLayer
				});
			}
		}

		void OnConfigImportComplete()
		{
			foreach (LayerInstanceData data in m_layerNetworkData)
			{
				m_layers[data.m_layerId].SetVisualizationMode(data.m_visualizationMode);
			}

			//Update #elements in step for starting layers
			for (int i = 0; i < m_handlesPerStep[0].Count; i++)
			{
				LayerInstanceData current = m_layerNetworkData[IndexOfLayerData(m_handlesPerStep[0][i])];
				current.m_indexInStep = i;
				current.m_elementsInStep = m_handlesPerStep[0].Count;
				m_layerNetworkData[IndexOfLayerData(m_handlesPerStep[0][i])] = current;
			}

			foreach (ALayer layer in m_layers.Values)
			{
				if (layer is VectorLayer)
				{
					m_sortedVectorLayers.Add(layer as VectorLayer);
				}
			}
		}

		void OnLayerDataChanged(NetworkListEvent<LayerInstanceData> a_change)
		{
			if (a_change.Type == NetworkListEvent<LayerInstanceData>.EventType.Value && ImportedConfigRoot.Instance.ImportComplete)
			{
				m_onLayerInstanceDataChanged?.Invoke(a_change.Value, m_layers[a_change.Value.m_layerId]);
				m_layers[a_change.Value.m_layerId].SetVisualizationMode(a_change.Value.m_visualizationMode);

				m_sortedVectorLayers = m_sortedVectorLayers.OrderBy(x => x.CurrentRenderOrderValue).ToList();

				UpdateLayerVerticalStep(a_change.Value.m_layerId);
			}
		}

		void OnLayerTagDataChanged(NetworkListEvent<LayerTagData> a_change)
		{
			if (a_change.Type == NetworkListEvent<LayerTagData>.EventType.Value && ImportedConfigRoot.Instance.ImportComplete)
			{
				m_onLayerTagDataChanged?.Invoke(a_change.Value);
			}
		}

		void OnDisplayMethodDataChanged(NetworkListEvent<DMInstanceData> a_change)
		{
			if (a_change.Type == NetworkListEvent<DMInstanceData>.EventType.Value && ImportedConfigRoot.Instance.ImportComplete)
			{
				m_onDMInstanceDataChanged?.Invoke(a_change.Value);
			}
		}

		void OnWorldScaleChanged(float newScale)
		{
			if (!ImportedConfigRoot.Instance.ImportComplete) return;

			foreach (int layerId in m_layerOrderNetworkData)
			{
				UpdateLayerVerticalStep(layerId);

				// When the scale has been changed on the server side, reset the layer tags
				if (NetworkManager.Singleton.IsServer || NetworkManager.Singleton.IsHost)
                {
					ResetLayerTags(layerId);
                }
			}
		}

		[ServerRpc(RequireOwnership = false)]
		public void SynchronizeLayerTagsServerRPC(LayerTagData a_newData)
		{
			m_layerTagNetworkData[IndexOfTagData(a_newData.m_layerId)] = a_newData;
		}

		[ServerRpc(RequireOwnership = false)]
		public void ResetLayerTagsServerRPC(int a_layerId)
		{
			ResetLayerTags(a_layerId);
		}

		private void ResetLayerTags(int a_layerId)
		{
			LayerTagData currentData = m_layerTagNetworkData[IndexOfTagData(a_layerId)];

			currentData.m_deletionProgress = 0f;
			// Negating the value for canvas UI & converting to local Z using (any) canvas scale
			currentData.m_newLocalZValue = -1f * VerticalStepToMapPositionZ(m_layerNetworkData[IndexOfLayerData(a_layerId)].m_verticalStep) / m_layerTagCanvasEast.transform.localScale.z;

			m_layerTagNetworkData[IndexOfTagData(a_layerId)] = currentData;
		}

		public LayerInstanceData GetLayerData(int a_layerId)
		{
			return m_layerNetworkData[IndexOfLayerData(a_layerId)];
		}

		private void SpawnLayerTagsServer(int a_layerId)
		{
			LayerTag layerTag = Instantiate(m_layerTagPrefab);
			layerTag.OnServerInit(a_layerId, LayerTag.LayerTagSide.West);
			layerTag.NetworkObject.Spawn(true);

			LayerTag layerTag2 = Instantiate(m_layerTagPrefab);
			layerTag2.OnServerInit(a_layerId, LayerTag.LayerTagSide.East);
			layerTag2.NetworkObject.Spawn(true);
		}

		[ServerRpc(RequireOwnership = false)]
		public void ChangeLHStepServerRPC(int a_layerId, int a_newVerticalStep)
		{
			LayerInstanceData current = m_layerNetworkData[IndexOfLayerData(a_layerId)];

			//Remove from old step
			m_handlesPerStep[current.m_verticalStep].RemoveAt(current.m_indexInStep);
			UpdateIndicesAtStep(current.m_verticalStep);

			//Add to new step
			m_handlesPerStep[a_newVerticalStep].Add(a_layerId);
			UpdateIndicesAtStep(a_newVerticalStep);
		}

		private void UpdateLayerVerticalStep(int a_layerId)
		{
			int verticalStep = m_layerNetworkData[IndexOfLayerData(a_layerId)].m_verticalStep;

			float verticalDisplacement = VerticalStepToMapPositionZ(verticalStep) + m_layers[a_layerId].VerticalOffset;
			m_layers[a_layerId].UpdateLayerMaterialHeight(verticalStep);
			m_layers[a_layerId].LayerRoot.transform.localPosition = new Vector3
			(
				m_layers[a_layerId].LayerRoot.transform.localPosition.x,
				verticalDisplacement,
				m_layers[a_layerId].LayerRoot.transform.localPosition.z
			);
		}

		[ServerRpc(RequireOwnership = false)]
		public void SetLayerVisualizationServerRPC(int a_layerId, LayerVisualizationMode a_newMode)
		{
			LayerInstanceData current = m_layerNetworkData[IndexOfLayerData(a_layerId)];

			bool isStaticLayer = ImportedConfigRoot.Instance.m_displayMethodConfig.IsStaticLayer(m_layers[current.m_layerId]);

			if (!isStaticLayer)
			{
				if (current.m_visualizationMode == LayerVisualizationMode.Off && a_newMode != LayerVisualizationMode.Off)
				{
					//Turned on
					m_layerOrderNetworkData.Add(a_layerId);
					m_handlesPerStep[current.m_verticalStep].Add(a_layerId);
					UpdateIndicesAtStep(current.m_verticalStep);
					ResetLayerTags(a_layerId);
				}
				else if (current.m_visualizationMode != LayerVisualizationMode.Off && a_newMode == LayerVisualizationMode.Off)
				{
					//Turned off
					m_layerOrderNetworkData.Remove(a_layerId);
					m_handlesPerStep[current.m_verticalStep].RemoveAt(current.m_indexInStep);
					UpdateIndicesAtStep(current.m_verticalStep);
				}
			}
			//Set actual mode after index update, so the change test works
			current = m_layerNetworkData[IndexOfLayerData(a_layerId)]; //Reset current as indices have changed
			current.m_visualizationMode = a_newMode;
			m_layerNetworkData[IndexOfLayerData(a_layerId)] = current;
		}

		[ServerRpc(RequireOwnership = false)]
		public void SetLayerOwnerIDServerRPC(int a_layerId, ulong a_ownerID)
		{
			LayerInstanceData current = m_layerNetworkData[IndexOfLayerData(a_layerId)];
			current.m_ownerID = (long)a_ownerID;
			m_layerNetworkData[IndexOfLayerData(a_layerId)] = current;
		}

		[ServerRpc(RequireOwnership = false)]
		public void RemoveLayerOwnershipServerRPC(int a_layerId)
		{
			LayerInstanceData current = m_layerNetworkData[IndexOfLayerData(a_layerId)];
			current.m_ownerID = -1;
			m_layerNetworkData[IndexOfLayerData(a_layerId)] = current;
		}	

		void UpdateIndicesAtStep(int a_step)
		{
			LayerInstanceData current;
			for (int i = 0; i < m_handlesPerStep[a_step].Count; i++)
			{
				current = m_layerNetworkData[IndexOfLayerData(m_handlesPerStep[a_step][i])];
				current.m_indexInStep = i;
				current.m_verticalStep = a_step;
				current.m_elementsInStep = m_handlesPerStep[a_step].Count;
				m_layerNetworkData[IndexOfLayerData(m_handlesPerStep[a_step][i])] = current;
			}
		}

		public void SetLayerVisualisationSetting(LayerVisualizationMode a_layerVisualisationSetting)
		{
			m_layerVisualisationSetting = a_layerVisualisationSetting;

			foreach (LayerInstanceData layerData in m_layerNetworkData)
			{
				if (layerData.m_visualizationMode == LayerVisualizationMode.Off)
				{
					continue;
				}

				SetLayerVisualizationServerRPC(layerData.m_layerId, m_layerVisualisationSetting);
			}
		}
		
		public int QueryLayersAtMapPosition(Vector2 a_mapPosition, float a_maxDistance, bool a_showHiddenLayers, ref LayerQueryData[] refLayerQueryDatas)
		{
			int queryCount = 0;
			foreach (ALayer layer in m_layers.Values)
			{
				LayerVisualizationMode layerVisualizationMode = LayerManager.Instance.GetLayerData(layer.LayerId).m_visualizationMode;

				if ((layerVisualizationMode != LayerVisualizationMode.Off || a_showHiddenLayers) &&
					layer.IsPointInsideLayer(a_mapPosition, a_maxDistance, out string typeData))
				{
					LayerQueryData layerQueryData = new LayerQueryData();
					layerQueryData.layer = layer;
					layerQueryData.typeData = typeData;
					layerQueryData.queryPosition = a_mapPosition;

					refLayerQueryDatas[queryCount] = layerQueryData;
					queryCount++;
				}
			}

			return queryCount;
		}

		private float VerticalStepToMapPositionZ(int verticalStep)
		{
			return 1f / m_worldPoseData.Scale * verticalStep * m_layerVerticalStepDisplacement;
		}

		public void AddLayerTagToCanvas(LayerTag a_layerTag, int a_layerId, LayerTag.LayerTagSide a_side)
        {
			switch (a_side)
            {
				case LayerTag.LayerTagSide.West:
					m_layerTagCanvasWest.AddLayerTag(a_layerTag, a_layerId);
					break;
				case LayerTag.LayerTagSide.East:
					m_layerTagCanvasEast.AddLayerTag(a_layerTag, a_layerId);
					break;
			}
		}
	}
}
