using System;
using System.Collections;
using System.Collections.Generic;
using MSP.Scripts.Session;
using Newtonsoft.Json;
using POV_Unity;
using Sirenix.OdinInspector;
using Unity.Collections;
using Unity.Netcode;
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

	[SerializeField]
	private NetworkDataMessenger m_networkDataMessenger;

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

	void Awake()
	{
		SessionManager.Instance.SetWorldManager(this);
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
			NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
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
				m_zipDataLoader.LoadZipFromFilePath(configUrlLaunchArgument);
			}
			else
			{
				string serverBaseURL = Utils.GetEnvironmentVariable("MSP_CHALLENGE_API_BASE_URL_FOR_SERVER") ?? string.Format(DEFAULT_BASE_URL_FORMAT, DEFAULT_SESSION_ID);
				if(m_loadConfigFromLocalFile)
				{
					m_zipDataLoader.LoadZipFromApplicationFolder();
				}
				else
				{
					m_zipDataLoader.LoadZipFromWeb(serverBaseURL, regionCoords);
				}
			}
		}
		else
		{
			m_networkDataMessenger.DataReceivedCallback += OnConfigDataReceivedFromServer;
		}
	
	}

    private void OnConfigDataReceivedFromServer()
	{
		byte[] configData = m_networkDataMessenger.Consume();
		ImportedConfigRoot.Instance.NotifyLoadStarted();
		StartCoroutine(ZipUtil.ParseRawZipConfigFile(configData, (string config, byte[] data) =>
		{
			m_zipDataLoader.OnZipLoaded(config, data);
		}));
	}

	private void OnClientConnected(ulong a_clientId)
	{
		StartCoroutine(SendConfigDataToClientRoutine(a_clientId));
	}

	private IEnumerator SendConfigDataToClientRoutine(ulong a_clientId)
    {
        yield return new WaitUntil(()=> ImportedConfigRoot.Instance.ImportComplete);
		m_networkDataMessenger.SendMessage(a_clientId, m_zipDataLoader.LoadedRawData);
		yield return null;
    }

	public override void OnNetworkDespawn()
	{
		base.OnNetworkDespawn();
		SessionManager.Instance.SetWorldManager(null);
		m_gameMasterWorldScale.OnValueChanged -= OnWorldScaleValueChanged;
		SessionManager.Instance.SessionFSM.OnStateEnter -= OnSessionStateEntered;
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
