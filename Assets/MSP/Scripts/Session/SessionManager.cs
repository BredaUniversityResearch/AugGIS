using System;
using System.Collections;
using System.Net;
using System.Net.Sockets;
using Sirenix.OdinInspector;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
public class SessionManager : MonoBehaviour
{
	public enum ESessionServerType
	{
		DedicatedServer,
		ClientHostedServer
	}

	public enum EDisconnectReason
	{
		FailedConnection,
		LostConnection,
		Kicked
	}

	public event Action DisconnectedFromSession;

	private static SessionManager ms_instance;
	public static SessionManager Instance => ms_instance;

	[SerializeField]
	[Required]
	private NetworkSessionConnectionData m_connectionData;

	[SerializeField]
	[Required]
	private MenuAttachmentData m_popupMessageAttachement;

	[SerializeField]
	[Required]
	private LocalPlayerSessionDataSO m_localPlayerSessionData;

	[SerializeField]
	[Required]
	private SessionFSM m_sessionFSM;
	public SessionFSM SessionFSM => m_sessionFSM;

	[Header("Scenes")]
	[SceneNameDropdown]
	[SerializeField]
	private string m_sessionScene;

	[SerializeField]
	[SceneNameDropdown]
	private string m_questScene;

	[SerializeField]
	[SceneNameDropdown]
	private string m_serverScene;

	private ESessionServerType m_sessionServerType;
	public ESessionServerType SessionServerType => m_sessionServerType;

	private SessionUserManager m_sessionUserManager;
	public SessionUserManager SessionUserManager => m_sessionUserManager;

	private WorldManager m_sessionWorldManager;
	public WorldManager WorldManager => m_sessionWorldManager;

	void Awake()
	{
		if (ms_instance == null)
		{
			ms_instance = this;
		}
		else
		{
			Debug.LogWarning("Instance of SessionManager already exists, destroying new instance");
			
			Destroy(gameObject);
		}

		DontDestroyOnLoad(gameObject);
	}

	void Start()
	{
		NetworkManager.Singleton.OnServerStarted += OnServerStarted;
		NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;
		NetworkManager.Singleton.OnServerStopped += OnServerStopped;
		NetworkManager.Singleton.OnClientConnectedCallback += (ulong clientID) => { Debug.LogFormat("client with id {0} connected...", clientID); };

		//reset local player data only when booting the session.
		m_localPlayerSessionData.Reset();

		InitializeSession();
	}

	private void InitializeSession()
	{
		SceneManager.sceneLoaded += OnSceneLoaded;

		m_connectionData.Reset();

#if UNITY_SERVER
		m_connectionData.ip = GetLocalIPAdress();
		m_connectionData.isServer = true;
		m_sessionServerType = ESessionServerType.DedicatedServer;
		SceneManager.LoadScene(m_serverScene);
#else
		m_sessionServerType = ESessionServerType.ClientHostedServer;
		SceneManager.LoadScene(m_questScene);
#endif
	}

	private void OnSceneLoaded(Scene a_scene, LoadSceneMode a_loadSceneMode)
	{
#if UNITY_SERVER
		if (a_scene.name == m_serverScene)
		{
			SessionManager.Instance.StartNetworkedSession();
		}
#endif
	}

	public void SetUserManager(SessionUserManager a_networkConnectionHandler)
	{
		m_sessionUserManager = a_networkConnectionHandler;
	}

	public void SetWorldManager(WorldManager a_worldManager)
	{
		m_sessionWorldManager = a_worldManager;
	}

	//DisconnectFromSession is called when the player is disconnected by external reason.
	public void DisconnectFromSession(EDisconnectReason a_disconnectionReason)
	{
		Debug.Log("Disconnected From Session!");
		DisconnectedFromSession?.Invoke();

		CloseConnection();

#if UNITY_SERVER
		StartCoroutine(QuitAppAfterNetworkShutdown());
#else
		ShowDisconnectMessagePopup(a_disconnectionReason);
#endif
	}

	//LeaveSession is called when player leaves the session gracefully
	public void LeaveSession()
	{
#if UNITY_SERVER
		Debug.LogError("Leave session should only be called by players!!!");
#else
		ShowLeaveSessionConfirmationPopup();
#endif
	}

	private void ShowDisconnectMessagePopup(EDisconnectReason a_disconnectionReason)
	{
		UIMessagePopup popup = m_popupMessageAttachement.EnableMenu().GetComponent<UIMessagePopup>();

		string title = string.Empty;
		string message = string.Empty;

		switch (a_disconnectionReason)
		{
			case EDisconnectReason.FailedConnection:
				title = "Failed Connection";
				message = " Failed to connect to the network! Check network status or server details.";
				break;
			case EDisconnectReason.LostConnection:
				title = "Lost Connection";
				message = "Lost connection to the network. Check network status.";
				break;
			case EDisconnectReason.Kicked:
				title = "Kicked";
				message = "You have been kicked by Game Master.";
				break;
			default:
				Debug.LogError("Unhandled disconnection reason");
				break;
		}

		Debug.Assert(popup != null);

		UIMessagePopup.PopupOptionData continueToMainMenuOptionData = new UIMessagePopup.PopupOptionData("Main Menu", () => { StartCoroutine(RestartSessionCoroutine()); m_popupMessageAttachement.DisableMenu(); });
		UIMessagePopup.PopupOptionData quitOptionData = new UIMessagePopup.PopupOptionData("Quit", () => Application.Quit());

		UIMessagePopup.PopupShowArguments popupShowArguments = new UIMessagePopup.PopupShowArguments(title, message, UIMessagePopup.EPopupType.Error, new UIMessagePopup.PopupOptionData[2] { continueToMainMenuOptionData, quitOptionData });
		popup.Show(popupShowArguments);
	}

	private void ShowLeaveSessionConfirmationPopup()
	{
		UIMessagePopup popup = m_popupMessageAttachement.EnableMenu().GetComponent<UIMessagePopup>();

		Debug.Assert(popup != null);

		string title = "Leave Session";
		string message = "Are you sure you want to leave the current session?";

		UIMessagePopup.PopupOptionData resumeOptionData = new UIMessagePopup.PopupOptionData("No", () => m_popupMessageAttachement.DisableMenu());
		UIMessagePopup.PopupOptionData continueToMainMenuOptionData = new UIMessagePopup.PopupOptionData("Yes", () => { CloseConnection(); StartCoroutine(RestartSessionCoroutine()); m_popupMessageAttachement.DisableMenu(); });

		UIMessagePopup.PopupShowArguments popupShowArguments = new UIMessagePopup.PopupShowArguments(title, message, UIMessagePopup.EPopupType.Error, new UIMessagePopup.PopupOptionData[2] { resumeOptionData, continueToMainMenuOptionData });
		popup.Show(popupShowArguments);
	}

	private IEnumerator RestartSessionCoroutine()
	{
		yield return new WaitWhile(() => NetworkManager.Singleton.IsServer || NetworkManager.Singleton.IsClient);
		InitializeSession();
	}

	private IEnumerator QuitAppAfterNetworkShutdown()
	{
		yield return new WaitWhile(() => NetworkManager.Singleton != null && NetworkManager.Singleton.ShutdownInProgress);
		Application.Quit();
	}

	private void CloseConnection()
	{
		if (NetworkManager.Singleton.IsServer)
		{
			NetworkManager.Singleton.Shutdown();
		}
		else
		{
			m_sessionUserManager.DisconnectClient();
		}
	}

	public void StartNetworkedSession()
	{
		string ipString = Utils.GetEnvironmentVariable("MSPXRClientAddress") ?? m_connectionData.ip?.ToString();

		if (m_connectionData.Port == 0)
		{
			string portEnv = Utils.GetEnvironmentVariable("MSPXRClientPort");
			m_connectionData.Port = string.IsNullOrEmpty(portEnv) ? (ushort)50123 : ushort.Parse(portEnv);
		}

		bool connectionEstablished;

		try
		{
			if (m_connectionData.isServer)
			{
				Debug.LogFormat("Creating Session with IP: {0} and Port: {1} ....", ipString, m_connectionData.Port);
				NetworkManager.Singleton.GetComponent<UnityTransport>().SetConnectionData(ipString, m_connectionData.Port, "0.0.0.0");

				if (m_sessionServerType == ESessionServerType.ClientHostedServer)
				{
					connectionEstablished = NetworkManager.Singleton.StartHost();
				}
				else
				{
					connectionEstablished = NetworkManager.Singleton.StartServer();
				}
			}
			else
			{
				Debug.LogFormat("Connecting to Session with IP: {0} and Port: {1} ....", ipString, m_connectionData.Port);
				NetworkManager.Singleton.GetComponent<UnityTransport>().SetConnectionData(ipString, m_connectionData.Port);
				connectionEstablished = NetworkManager.Singleton.StartClient();
			}
		}
		catch
		{
			connectionEstablished = false;
		}

		if (!connectionEstablished)
		{
			DisconnectFromSession(EDisconnectReason.FailedConnection);
		}
	}

	public IPAddress GetLocalIPAdress()
	{
		IPHostEntry host = Dns.GetHostEntry(Dns.GetHostName());
		foreach (IPAddress ip in host.AddressList)
		{
			Debug.Log("Address" + ip.ToString());
			if (ip.AddressFamily == AddressFamily.InterNetwork && !ip.ToString().StartsWith("127"))
			{
				return ip;
			}
		}

		Debug.LogError("No network adapters with a valid network address in the system!");
		return null;
	}

	private void LoadSessionSceneNetworked()
	{
		NetworkManager.Singleton.SceneManager.SetClientSynchronizationMode(LoadSceneMode.Additive);
		NetworkManager.Singleton.SceneManager.VerifySceneBeforeLoading = VerifySceneLoad;
		NetworkManager.Singleton.SceneManager.LoadScene(m_sessionScene, LoadSceneMode.Additive);
	}

	private bool VerifySceneLoad(int a_sceneIndex, string a_sceneName, LoadSceneMode loadSceneMode)
	{
		if (a_sceneName != m_sessionScene)
		{
			Debug.LogError("Scene Not verified: " + a_sceneName);
			return false;
		}
		return true;
	}

	private void OnServerStarted()
	{
		LoadSessionSceneNetworked();
	}

	private void OnClientDisconnected(ulong a_clientId)
	{
		Debug.LogFormat("client with id {0} disconnected...", a_clientId);
		if (a_clientId == NetworkManager.Singleton.LocalClientId)
		{
			DisconnectFromSession(EDisconnectReason.LostConnection);
		}
	}

	private void OnServerStopped(bool a_isClient)
	{
		if (!a_isClient)
		{
			DisconnectFromSession(EDisconnectReason.LostConnection);
		}
	}
}
