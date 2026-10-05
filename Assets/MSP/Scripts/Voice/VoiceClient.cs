using System;
using System.Collections;
using System.Collections.Generic;
using Newtonsoft.Json;
using POV_Unity;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// Sends each finished recording to the interface (<c>http://host:8080/voice</c>) together with the
/// session's layer names and this headset's conversation, and raises the answer it gets back.
/// The host is the machine the session was joined from; the inspector value is the fallback.
/// </summary>
[RequireComponent(typeof(VoiceRecorder))]
public class VoiceClient : MonoBehaviour
{
	[SerializeField]
	private string m_defaultInterfaceHost = "127.0.0.1";

	[SerializeField]
	private int m_interfacePort = 8080;

	private const int HEALTH_TIMEOUT_SECONDS = 5;

	// A search takes about 6 seconds alone and about 12 with 8 people asking at once.
	[SerializeField]
	private int m_timeoutSeconds = 60;

	public event Action<string> TranscriptReceived;
	public event Action<VoiceResponse> ResponseReceived;
	public event Action<string> Failed;
	public event Action<string> HealthChecked;

	private VoiceRecorder m_recorder;
	private VoiceConversation m_conversation = new VoiceConversation();

	private void Awake()
	{
		m_recorder = GetComponent<VoiceRecorder>();
	}

	private void OnEnable()
	{
		m_recorder.ClipReady += OnClipReady;
	}

	private void OnDisable()
	{
		m_recorder.ClipReady -= OnClipReady;

		// Unity stops this object's coroutines when it is switched off, so a request under way never
		// reaches its end. Without this the recorder would wait for that answer forever.
		m_recorder.NotifySendComplete();
	}

	private void OnClipReady(byte[] a_wav)
	{
		StartCoroutine(SendSentence(a_wav));
	}

	private string InterfaceHost
	{
		get
		{
			string sessionAddress = SessionAddress();
			if (IsRemoteAddress(sessionAddress))
			{
				return sessionAddress;
			}
			return m_defaultInterfaceHost;
		}
	}

	/// <summary>
	/// Asks the interface which of its services are up and raises <see cref="HealthChecked"/> with one
	/// line for the panel.
	/// </summary>
	public void CheckHealth()
	{
		StartCoroutine(RequestHealth());
	}

	private IEnumerator RequestHealth()
	{
		string url = $"http://{InterfaceHost}:{m_interfacePort}/health";

		using (UnityWebRequest request = UnityWebRequest.Get(url))
		{
			request.timeout = HEALTH_TIMEOUT_SECONDS;
			yield return request.SendWebRequest();

			if (request.result != UnityWebRequest.Result.Success)
			{
				HealthChecked?.Invoke($"No answer from {InterfaceHost}:{m_interfacePort}");
				yield break;
			}

			VoiceHealth health;
			try
			{
				health = JsonConvert.DeserializeObject<VoiceHealth>(request.downloadHandler.text);
			}
			catch (Exception)
			{
				HealthChecked?.Invoke($"Unreadable answer from {InterfaceHost}:{m_interfacePort}");
				yield break;
			}

			HealthChecked?.Invoke($"interface {health.interfaceState}, speech {health.whisper}, search {health.search}");
		}
	}

	/// <summary>Forgets the conversation, so the next sentence starts a new one.</summary>
	public void StartNewConversation()
	{
		m_conversation = new VoiceConversation();
	}

	// The IP the session was created or joined with, or null when no session is running.
	private static string SessionAddress()
	{
		if (NetworkManager.Singleton == null)
		{
			return null;
		}
		UnityTransport transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
		if (transport == null)
		{
			return null;
		}
		return transport.ConnectionData.Address;
	}

	// "Any" (0.0.0.0) and loopback (127.x) both mean the session runs on this machine, not on a remote host.
	private static bool IsRemoteAddress(string a_address)
	{
		if (string.IsNullOrEmpty(a_address))
		{
			return false;
		}
		bool isAny = a_address == "0.0.0.0";
		bool isLoopback = a_address.StartsWith("127.");
		return !isAny && !isLoopback;
	}

	/// <summary>
	/// The loaded session's layers a voice command may act on. Static layers and layers with no
	/// display method are left out: the layer menu leaves them out too, and a static layer has no
	/// control anywhere to switch it back on, so hiding one by voice would be a one-way trip.
	/// </summary>
	public List<ALayer> CommandableLayers()
	{
		List<ALayer> layers = new List<ALayer>();

		if (LayerManager.Instance == null)
		{
			return layers;
		}

		ImportedConfigRoot config = ImportedConfigRoot.Instance;
		if (config == null || !config.ImportComplete)
		{
			// m_displayMethodConfig is only filled during the import, so there is nothing to filter on yet.
			return layers;
		}

		foreach (ALayer layer in LayerManager.Instance.AllLayers)
		{
			if (layer.DisplayMethods.Count == 0)
			{
				continue;
			}
			if (config.m_displayMethodConfig.IsStaticLayer(layer))
			{
				continue;
			}
			layers.Add(layer);
		}
		return layers;
	}

	/// <summary>The name a layer is sent and spoken by: its short name, or its full name when it has none.</summary>
	public static string DisplayName(ALayer a_layer)
	{
		if (string.IsNullOrEmpty(a_layer.@short))
		{
			return a_layer.name;
		}
		return a_layer.@short;
	}

	private List<string> CurrentLayerNames()
	{
		List<string> names = new List<string>();
		foreach (ALayer layer in CommandableLayers())
		{
			names.Add(DisplayName(layer));
		}
		return names;
	}

	private IEnumerator SendSentence(byte[] a_wav)
	{
		// The layer names travel with the audio: the interface only lets a command name one of these.
		WWWForm form = new WWWForm();
		form.AddBinaryData("audio", a_wav, "sentence.wav", "audio/wav");
		form.AddField("layers", JsonConvert.SerializeObject(CurrentLayerNames()));
		form.AddField("conversation", JsonConvert.SerializeObject(m_conversation));

		string url = $"http://{InterfaceHost}:{m_interfacePort}/voice";
		float startTime = Time.realtimeSinceStartup;

		using (UnityWebRequest request = UnityWebRequest.Post(url, form))
		{
			request.timeout = m_timeoutSeconds;
			yield return request.SendWebRequest();

			float seconds = Time.realtimeSinceStartup - startTime;
			bool neverAnswered = request.result == UnityWebRequest.Result.ConnectionError;
			bool brokenAnswer = request.result == UnityWebRequest.Result.DataProcessingError;
			if (neverAnswered || brokenAnswer)
			{
				Fail(DescribeNoAnswer(request, url, seconds));
				yield break;
			}

			VoiceResponse response;
			try
			{
				response = VoiceResponse.FromJson(request.downloadHandler.text);
			}
			catch (Exception e)
			{
				Fail($"The interface answered with something unreadable (HTTP {request.responseCode}): {e.Message}");
				yield break;
			}

			if (!string.IsNullOrWhiteSpace(response.transcript))
			{
				TranscriptReceived?.Invoke(response.transcript);
			}

			if (request.result != UnityWebRequest.Result.Success)
			{
				// 400 and 503 carry a JSON body with the reason, and a 503 after speech-to-text carries the transcript.
				string reason = response.error;
				if (string.IsNullOrEmpty(reason))
				{
					reason = "no reason given";
				}
				Fail($"The interface refused (HTTP {request.responseCode}): {reason}");
				yield break;
			}

			m_conversation = response.conversation;
			m_recorder.NotifySendComplete();
			ResponseReceived?.Invoke(response);
		}
	}

	// UnityWebRequest reports a timeout, a refused connection and an unknown host all as ConnectionError,
	// and they need different fixes, so the panel says which one it was.
	private string DescribeNoAnswer(UnityWebRequest a_request, string a_url, float a_seconds)
	{
		string error = a_request.error;
		if (string.IsNullOrEmpty(error))
		{
			error = "unknown error";
		}

		if (error.Contains("timeout"))
		{
			return $"No answer after {a_seconds:F0}s from {a_url}. It may be slow or blocked, try again.";
		}
		if (error.Contains("Cannot connect"))
		{
			return $"Nothing is listening at {a_url}. Are the voice containers running, and is port {m_interfacePort} open in the firewall?";
		}
		if (error.Contains("Cannot resolve"))
		{
			return $"The address in {a_url} does not exist.";
		}
		return $"No answer from {a_url}: {error}";
	}

	private void Fail(string a_message)
	{
		Debug.LogWarning($"[Voice] {a_message}");
		m_recorder.NotifySendComplete();
		Failed?.Invoke(a_message);
	}
}
