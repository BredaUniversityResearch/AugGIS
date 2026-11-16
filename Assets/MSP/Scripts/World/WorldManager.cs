using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using MSP.Scripts.GisData;
using MSP.Scripts.Session;
using Newtonsoft.Json;
using POV_Unity;
using Sirenix.OdinInspector;
using Unity.Collections;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

public class WorldManager : NetworkBehaviour
{
	[SerializeField]
	private bool m_loadConfigFromLocalFile = false;

	[SerializeField]
	[Required]
	private PoseData m_worldPoseData;
	public PoseData WorldPoseData => m_worldPoseData;

	[SerializeField]
	private MapAnchorPlacementData m_firstAnchorData;

	[SerializeField]
	private MapAnchorPlacementData m_secondAnchorData;

	[SerializeField]
	[Required]
	private TransformReference m_drawingLinesRoot;

	[SerializeField]
	[Required]
	private Transform m_worldRootTransform;

	[SerializeField]
	private ToolStationSpawner m_toolStationSpawner;
	public ToolStationSpawner ToolStationSpawner => m_toolStationSpawner;

	[SerializeField]
	[Required]
	private LoadZipData m_zipDataLoader;

	[Header("Debug")]

	[SerializeField]
	[Tooltip("Editor only, set before entering play mode. The last N layers of the dataset are held back from the initial load and can be added later, one per press, with the 'Add next held-back layer' button.")]
	private int m_debugHoldBackLayerCount = 0;

	[Header("Gameobject Sets")]

	[SerializeField]
	private GameObjectSet m_toolStationSet;

	[SerializeField]
	private GameObjectSet m_mapToolSet;

	[SerializeField]
	private GameObjectSet m_mapPawnSet;

	[SerializeField]
	private GameObjectSet m_lineDrawingSet;

	[SerializeField]
	[Required]
	private ToolPrefabRegistry m_toolPrefabRegistry;
	public ToolPrefabRegistry ToolPrefabRegistry => m_toolPrefabRegistry;

	private NetworkVariable<float> m_gameMasterWorldScale = new NetworkVariable<float>(1, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

	private const int DEFAULT_SESSION_ID = 1;
	private const string DEFAULT_BASE_URL_FORMAT = "https://server.mspchallenge.info/{0}/";

	private NetworkVariable<Vector2> bottomLeftMapCoordinates = new NetworkVariable<Vector2>(Vector2.zero, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
	private NetworkVariable<Vector2> topRightMapCoordinates = new NetworkVariable<Vector2>(Vector2.zero, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

	private ARAnchor m_worldArAnchor = null;

	private GisDatabaseService m_gisDatabaseService;
	private GisHttpServer m_gisHttpServer;

	private class HeldBackLayer
	{
		public ALayer m_layer;
		public LayerKind m_kind;
		public Dictionary<string, byte[]> m_rasterImages = new Dictionary<string, byte[]>();
	}

	//Layers withheld from the initial load (editor debug), in the order they get added
	private readonly Queue<HeldBackLayer> m_heldBackLayers = new Queue<HeldBackLayer>();

	void Awake()
	{
		SessionManager.Instance.SetWorldManager(this);
		m_gisDatabaseService = new GisDatabaseService();
	}

	public override void OnNetworkSpawn()
	{
		base.OnNetworkSpawn();

#if !UNITY_SERVER
		MSPARAnchorManager.Instance.ARAnchorManager.trackablesChanged.AddListener(OnARAnchorTrackableChanged);
#endif

		m_worldPoseData.Reset();
		m_worldPoseData.SetRootTransform(m_worldRootTransform);
		SessionManager.Instance.SessionFSM.OnStateEnter += OnSessionStateEntered;
		m_gameMasterWorldScale.OnValueChanged += OnWorldScaleValueChanged;

		if (IsServer)
		{
			//Only the host keeps a database (it serves layers from it); clients fetch
			//layers over HTTP and have no local copy.
			m_gisDatabaseService.Open();

			m_gisHttpServer = new GisHttpServer(new SqliteLayerPayloadProvider(m_gisDatabaseService.DatabasePath), GisHttpServer.ResolvePort());
			m_gisHttpServer.Start();
			//default region coords if none is set in enviorment variables
			//Small sample
			//RegionCoords regionCoords = new(3474027, 3470232, 4063746, 3488599);
			//Medium sample
			//RegionCoords regionCoords = new(3957954, 3320200, 4108655, 3459703);
			//German EEZ
			//RegionCoords regionCoords = new(3797132, 3137432, 3965925, 3291676);
			//Large sample
			//RegionCoords regionCoords = new(3773329, 3131921, 4212268, 3474027);
			//Massive sample
			//RegionCoords regionCoords = new(3620936, 3337660, 4222555, 4054338);
			RegionCoords regionCoords = new(3797132, 3137432, 3965925, 3291676);

			string json = Utils.GetEnvironmentVariable("IMMERSIVE_SESSION_REGION_COORDS");
			if (!string.IsNullOrEmpty(json))
			{
				regionCoords = JsonConvert.DeserializeObject<RegionCoords>(json);
			}

			bottomLeftMapCoordinates.Value = new Vector2(regionCoords.BottomLeftX, regionCoords.BottomLeftY);
			topRightMapCoordinates.Value = new Vector2(regionCoords.TopRightX, regionCoords.TopRightY);

			string configUrlLaunchArgument = string.Empty;
			string[] launchArgument = Environment.GetCommandLineArgs();
			foreach (string arg in launchArgument)
			{
				string[] splitArgs = arg.Split("=");

				if (splitArgs.Length == 2 && splitArgs[0] == "configUrl")
				{
					configUrlLaunchArgument = splitArgs[1];
					Debug.Log($"Launch argument set: {splitArgs[1]}");
				}
			}

			if(configUrlLaunchArgument != string.Empty)
			{
				LoadHostDataset(MspChallengeZipDataSource.FromFilePath(configUrlLaunchArgument));
			}
			else
			{
				string serverBaseURL = Utils.GetEnvironmentVariable("MSP_CHALLENGE_API_BASE_URL_FOR_SERVER") ?? string.Format(DEFAULT_BASE_URL_FORMAT, DEFAULT_SESSION_ID);
				if(m_loadConfigFromLocalFile)
				{
					LoadHostDataset(MspChallengeZipDataSource.FromApplicationFolder(m_zipDataLoader.FileName));
				}
				else
				{
					LoadHostDataset(MspChallengeZipDataSource.FromWeb(serverBaseURL, regionCoords));
				}
			}
		}
		else
		{
			LayerCatalogue.Instance.WhenReady(OnLayerCatalogueReady);
		}
	
	}

	private void LoadHostDataset(IGisDataSource a_dataSource)
	{
		ImportedConfigRoot.Instance.NotifyLoadStarted();
		StartCoroutine(a_dataSource.LoadDataset(OnHostDatasetLoaded, OnDatasetLoadFailed));
	}

	private void OnHostDatasetLoaded(GisDataset a_dataset)
	{
		//Persisted so GisHttpServer can serve layers to clients, but the host's own
		//render uses the in-memory dataset directly rather than reading it back out
		//of SQLite - nothing mutates it between here and Initialise().
		new SqliteGisDatasetWriter(m_gisDatabaseService).Write(a_dataset);
#if UNITY_EDITOR
		HoldBackLayers(a_dataset);
#endif
		PublishLayerCatalogue(a_dataset);
		GisRasterTextureLoader.Load(a_dataset);
		ImportedConfigRoot.Instance.Initialise(a_dataset.Config, ParseDisplayMethodConfig());
	}

	//Layer ids were assigned by SqliteGisDatasetWriter.Write (raster layers first,
	//then vector layers; the sort order equals the id), so this must run after it.
	private void PublishLayerCatalogue(GisDataset a_dataset)
	{
		foreach (RasterLayer rasterLayer in a_dataset.Config.datamodel.raster_layers)
		{
			LayerCatalogue.Instance.AddLayer(rasterLayer, LayerKind.Raster, rasterLayer.LayerId);
		}

		foreach (VectorLayer vectorLayer in a_dataset.Config.datamodel.vector_layers)
		{
			LayerCatalogue.Instance.AddLayer(vectorLayer, LayerKind.Vector, vectorLayer.LayerId);
		}

		//Clients must not start loading before the host has finished its own import:
		//LayerManager's per-layer NetworkLists are filled during that import, and a
		//client importing earlier would miss the entries added after it finished.
		//(Matches the old flow, where the data was only sent after ImportComplete.)
		ImportedConfigRoot.Instance.m_onImportComplete += OnHostImportComplete;
	}

	/// <summary>
	/// Host only, main thread, after the initial import has completed. Adds a layer
	/// whose data is already in the database to the running session: loads it on the
	/// host (hidden by default) and, once that is done, announces it to the clients
	/// through the layer catalogue so they fetch it.
	/// </summary>
	/// <param name="a_rasterTextures">Textures the layer needs (raster layers), keyed
	/// like GisDataset.RasterImages; null if it needs none.</param>
	public void AddLayerToSession(ALayer a_layer, LayerKind a_kind, Dictionary<string, Texture2D> a_rasterTextures = null)
	{
		if (!IsServer)
		{
			Debug.LogError("Layers can only be added to the session on the server");
			return;
		}

		ImportedConfigRoot.Instance.AddLayer(a_layer, a_rasterTextures, a_success =>
		{
			if (!a_success)
			{
				Debug.LogWarning($"Layer '{a_layer.name}' was not added to the session");
				return;
			}

			LayerCatalogue.Instance.AddLayer(a_layer, a_kind, a_layer.LayerId);
		});
	}

#if UNITY_EDITOR
	//Withholds the last m_debugHoldBackLayerCount layers (vector layers first) from the
	//initial load. They stay in the database, so clients can fetch them once added.
	private void HoldBackLayers(GisDataset a_dataset)
	{
		DataConfigContent datamodel = a_dataset.Config.datamodel;
		int holdBackCount = Mathf.Clamp(m_debugHoldBackLayerCount, 0, datamodel.raster_layers.Length + datamodel.vector_layers.Length);
		if (holdBackCount == 0)
		{
			return;
		}

		List<RasterLayer> rasterLayers = new List<RasterLayer>(datamodel.raster_layers);
		List<VectorLayer> vectorLayers = new List<VectorLayer>(datamodel.vector_layers);
		List<HeldBackLayer> heldBack = new List<HeldBackLayer>();

		for (int i = 0; i < holdBackCount; i++)
		{
			if (vectorLayers.Count > 0)
			{
				heldBack.Add(new HeldBackLayer { m_layer = vectorLayers[vectorLayers.Count - 1], m_kind = LayerKind.Vector });
				vectorLayers.RemoveAt(vectorLayers.Count - 1);
			}
			else
			{
				RasterLayer rasterLayer = rasterLayers[rasterLayers.Count - 1];
				rasterLayers.RemoveAt(rasterLayers.Count - 1);

				HeldBackLayer heldBackLayer = new HeldBackLayer { m_layer = rasterLayer, m_kind = LayerKind.Raster };
				string imageKey = RasterImageKey.Resolve(rasterLayer.data);
				heldBackLayer.m_rasterImages[imageKey] = a_dataset.RasterImages[imageKey];
				heldBack.Add(heldBackLayer);
			}
		}

		datamodel.raster_layers = rasterLayers.ToArray();
		datamodel.vector_layers = vectorLayers.ToArray();

		//Don't decode images that no remaining layer uses
		Dictionary<string, byte[]> remainingImages = new Dictionary<string, byte[]>();
		foreach (RasterLayer rasterLayer in rasterLayers)
		{
			string imageKey = RasterImageKey.Resolve(rasterLayer.data);
			remainingImages[imageKey] = a_dataset.RasterImages[imageKey];
		}
		a_dataset.RasterImages = remainingImages;

		//Lowest layer id first
		heldBack.Reverse();
		foreach (HeldBackLayer heldBackLayer in heldBack)
		{
			m_heldBackLayers.Enqueue(heldBackLayer);
		}

		Debug.Log($"[WorldManager] Holding back {holdBackCount} layer(s) from the initial load");
	}
#endif

	private bool CanAddHeldBackLayer => Application.isPlaying && IsSpawned && IsServer
		&& ImportedConfigRoot.Instance != null && ImportedConfigRoot.Instance.ImportComplete && m_heldBackLayers.Count > 0;

	[Button("Add next held-back layer")]
	[HideInEditorMode]
	[EnableIf(nameof(CanAddHeldBackLayer))]
	private void DebugAddNextHeldBackLayer()
	{
		HeldBackLayer heldBackLayer = m_heldBackLayers.Dequeue();
		Dictionary<string, Texture2D> textures = heldBackLayer.m_kind == LayerKind.Raster ? GisRasterTextureLoader.Decode(heldBackLayer.m_rasterImages) : null;

		Debug.Log($"[WorldManager] Adding held-back layer {heldBackLayer.m_layer.LayerId} ({heldBackLayer.m_layer.name}), {m_heldBackLayers.Count} remaining");
		AddLayerToSession(heldBackLayer.m_layer, heldBackLayer.m_kind, textures);
	}

	private void OnHostImportComplete()
	{
		ImportedConfigRoot.Instance.m_onImportComplete -= OnHostImportComplete;
		LayerCatalogue.Instance.MarkInitialLayersPublished();
	}

	//Mirrors the parse step inside LoadZipData.ImportConfig (duplicated rather than
	//reusing that method directly, since it also re-parses the data config from raw
	//json text, which this path no longer has - GisDataset.Config is already a
	//parsed DataConfig).
	private DisplayMethodConfig ParseDisplayMethodConfig()
	{
		ConfigLoadHelper helper = new ConfigLoadHelper();
		DisplayMethodConfig displayMethodConfig = helper.ParseDisplayMethodConfig(m_zipDataLoader.DisplayMethodConfigFile.text);
		helper.Clear();
		return displayMethodConfig;
	}

	private void OnDatasetLoadFailed()
	{
		SessionManager.Instance.DisconnectFromSession(SessionManager.EDisconnectReason.FailedConnection);
	}

	//Client side: once the host's catalogue is complete, fetch every layer from the
	//host's GisHttpServer (same address the session connection uses) and hand the
	//assembled dataset to the usual Initialise path.
	private void OnLayerCatalogueReady()
	{
		string address = NetworkManager.Singleton.GetComponent<UnityTransport>().ConnectionData.Address;
		string baseUrl = $"http://{address}:{GisHttpServer.ResolvePort()}/";

		Debug.Log($"[WorldManager] Layer catalogue ready, loading dataset from {baseUrl}");
		ImportedConfigRoot.Instance.NotifyLoadStarted();

		IReadOnlyList<LayerCatalogueEntry> initialCatalogue = LayerCatalogue.Instance.Entries;
		GisHttpDataSource dataSource = new GisHttpDataSource(baseUrl, initialCatalogue);

		//Layers the host announces after this snapshot are loaded by the layer loader
		m_clientLayerLoader = new ClientLayerLoader(this, baseUrl, initialCatalogue, OnRuntimeLayerFailed);
		StartCoroutine(dataSource.LoadDataset(OnClientDatasetLoaded, OnDatasetLoadFailed));
	}

	private ClientLayerLoader m_clientLayerLoader;

	private void OnRuntimeLayerFailed(int a_layerId, string a_reason)
	{
		ReportLayerLoadFailedRpc(a_layerId, a_reason);
	}

	//Lets the host see during a session that a client could not load a layer
	[Rpc(SendTo.Server, RequireOwnership = false)]
	private void ReportLayerLoadFailedRpc(int a_layerId, string a_reason, RpcParams a_rpcParams = default)
	{
		Debug.LogWarning($"[WorldManager] Client {a_rpcParams.Receive.SenderClientId} could not load layer {a_layerId}: {a_reason}");
	}

	private void OnClientDatasetLoaded(GisDataset a_dataset)
	{
		GisRasterTextureLoader.Load(a_dataset);
		m_clientLayerLoader?.Start();
		ImportedConfigRoot.Instance.Initialise(a_dataset.Config, ParseDisplayMethodConfig());
	}

	public override void OnNetworkDespawn()
	{
		base.OnNetworkDespawn();
		m_clientLayerLoader?.Stop();
		m_clientLayerLoader = null;
		SessionManager.Instance.SetWorldManager(null);
		m_gameMasterWorldScale.OnValueChanged -= OnWorldScaleValueChanged;
		SessionManager.Instance.SessionFSM.OnStateEnter -= OnSessionStateEntered;
		m_gisHttpServer?.Stop();
		m_gisHttpServer = null;
		m_gisDatabaseService.Close();
#if !UNITY_SERVER
		MSPARAnchorManager.Instance.ARAnchorManager.trackablesChanged.RemoveListener(OnARAnchorTrackableChanged);
#endif
	}
	
	public void OnARAnchorTrackableChanged(ARTrackablesChangedEventArgs<ARAnchor> a_args)
	{
		for (int i = 0; i < a_args.updated.Count; i++)
		{
			ARAnchor anchor = a_args.updated[i];
			if (anchor == m_worldArAnchor)
			{
				m_worldRootTransform.transform.SetPositionAndRotation(anchor.pose.position, anchor.pose.rotation);
			}
		}
	}

	private void OnWorldScaleValueChanged(float previousValue, float newValue)
	{
		Debug.Log("ScaleValue Changed: " + newValue);
		m_worldPoseData.SetScale(newValue);

		foreach (ALayer layer in ImportedConfigRoot.Instance.m_dataConfig.datamodel.raster_layers)
		{
			layer.OnWorldScaleChanged(newValue);
		}

		foreach (ALayer layer in ImportedConfigRoot.Instance.m_dataConfig.datamodel.vector_layers)
		{
			layer.OnWorldScaleChanged(newValue);
		}
	}

	private void OnSessionStateEntered(Type a_stateType)
	{
		if (a_stateType == typeof(WorldViewSessionState))
		{
			CreateWorldViewBasedOnAnchors(m_firstAnchorData.AnchorPosition, m_secondAnchorData.AnchorPosition);

			if (NetworkManager.Singleton.LocalClientId == 1)
			{
				CreateWorldBasedOnAnchorsServerRPC(m_firstAnchorData.AnchorPosition, m_secondAnchorData.AnchorPosition);
			}
		}
	}

	private void CreateWorldViewBasedOnAnchors(Vector3 a_firstAnchorPosition, Vector3 a_secondAnchorPosition)
	{
		if (!ImportedConfigRoot.Instance.ImportComplete)
		{
			Debug.LogError("Config loading not completed");
			return;
		}

		Matrix4x4 worldMatrix = Utils.CalculateWorldMatrixBasedOnAnchorPosition(a_firstAnchorPosition, a_secondAnchorPosition);
		m_worldPoseData.SetPosition(worldMatrix.GetPosition());
		m_worldPoseData.SetRotation(worldMatrix.rotation);

		if (SessionManager.Instance.SessionUserManager.IsLocalClientGameMaster())
		{
			float worldScale = worldMatrix.lossyScale.x;
			SetWorldScaleServerRPC(worldScale);
			Debug.Log("Setting Scale: " + worldScale);
		}
		else
		{
			OnWorldScaleValueChanged(-1, m_gameMasterWorldScale.Value);
		}

#if (!UNITY_SERVER && !UNITY_EDITOR)
		AnchorMap();
#endif
	}

	private async void AnchorMap()
	{
		m_worldArAnchor = await MSPARAnchorManager.Instance.TryCreateAnchorAtPose(new Pose(m_worldPoseData.Position, m_worldPoseData.Rotation));
	}

	[Rpc(SendTo.Server, RequireOwnership = false)]
	private void CreateWorldBasedOnAnchorsServerRPC(Vector3 a_firstAnchorPosition, Vector3 a_secondAnchorPosition)
	{
		if (SessionManager.Instance.SessionServerType == SessionManager.ESessionServerType.DedicatedServer)
		{
			CreateWorldViewBasedOnAnchors(a_firstAnchorPosition, a_secondAnchorPosition);
		}
	}

	[Rpc(SendTo.Server, RequireOwnership = false)]
	private void SetWorldScaleServerRPC(float a_scale)
	{
		m_gameMasterWorldScale.Value = a_scale;
	}

	public void DestroyAllStations()
	{
		DestroyAllStationsServerRPC();
	}

	[Rpc(SendTo.Server, RequireOwnership = false)]
	private void DestroyAllStationsServerRPC()
	{
		for (int i = m_toolStationSet.Count - 1; i >= 0; i--)
		{
			m_toolStationSet[i].GetComponent<NetworkObject>().Despawn(destroy: true);
		}
	}

	public void DestroyAllTools()
	{
		DestroyAllToolsServerRPC();
	}

	[Rpc(SendTo.Server, RequireOwnership = false)]
	private void DestroyAllToolsServerRPC()
	{
		for (int i = m_mapToolSet.Count - 1; i >= 0; i--)
		{
			ToolStationSelection tool = m_mapToolSet[i].GetComponent<ToolStationSelection>();
			Debug.Assert(tool != null, "ToolStationSelection is null in map tool set. Make sure you have added the correct gameobject!");

			if (tool.CurrentState != ToolStationSelection.EState.Idle)
			{
				tool.GetComponent<NetworkObject>().Despawn(destroy: true);
			}
		}
	}

	public void DestroyAllPawns()
	{
		DestroyAllPawnsServerRPC();
	}

	[Rpc(SendTo.Server, RequireOwnership = false)]
	private void DestroyAllPawnsServerRPC()
	{
		for (int i = m_mapPawnSet.Count - 1; i >= 0; i--)
		{
			PawnToolStationSelection pawn = m_mapPawnSet[i].GetComponent<PawnToolStationSelection>();
			Debug.Assert(pawn != null, "PawnToolStationSelection is null in map tool set. Make sure you have added the correct gameobject!");

			if (pawn.CurrentState != ToolStationSelection.EState.Idle)
			{
				pawn.GetComponent<NetworkObject>().Despawn(destroy: true);
			}
		}
	}

	public void HideAllDrawings()
	{
		m_drawingLinesRoot.TransformRef.gameObject.SetActive(false);
		HideAllDrawingsRPC();
	}

	[Rpc(SendTo.NotMe)]
	private void HideAllDrawingsRPC()
	{
		m_drawingLinesRoot.TransformRef.gameObject.SetActive(false);
	}

	public void ShowAllDrawings()
	{
		m_drawingLinesRoot.TransformRef.gameObject.SetActive(true);
		ShowAllDrawingsRPC();
	}

	[Rpc(SendTo.NotMe)]
	private void ShowAllDrawingsRPC()
	{
		m_drawingLinesRoot.TransformRef.gameObject.SetActive(true);
	}
	public void DeleteAllDrawings()
	{
		DeleteAllDrawingsServerRPC();
	}

	[Rpc(SendTo.Server)]
	private void DeleteAllDrawingsServerRPC()
	{
		for (int i = m_lineDrawingSet.Count - 1; i >= 0; i--)
		{
			m_lineDrawingSet[i].GetComponent<NetworkObject>().Despawn(destroy: true);
		}
	}
}
