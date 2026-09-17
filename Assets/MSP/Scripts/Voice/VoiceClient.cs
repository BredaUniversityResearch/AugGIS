using System;
using System.Collections;
using System.Collections.Generic;
using POV_Unity;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// Sends each finished recording to the voice helper (<c>http://host:8080/command</c>) together with
/// the session's layer names, and raises the transcript and operations it gets back.
/// The host is the machine the session was joined from; the inspector value is the fallback.
/// </summary>
[RequireComponent(typeof(VoiceRecorder))]
public class VoiceClient : MonoBehaviour
{
	[SerializeField]
	private string m_defaultHelperHost = "127.0.0.1";

	[SerializeField]
	private int m_helperPort = 8080;

	[SerializeField]
	private int m_timeoutSeconds = 30;

	public event Action<string> SendStarted;
	public event Action<string> TranscriptReceived;
	public event Action<VoiceResponse> ResponseReceived;
	public event Action<string> Failed;

	private VoiceRecorder m_recorder;

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
	}

	private void OnClipReady(byte[] a_wav)
	{
		StartCoroutine(SendCommand(a_wav));
	}

	public string HelperHost
	{
		get
		{
			string sessionAddress = SessionAddress();
			if (IsRemoteAddress(sessionAddress))
			{
				return sessionAddress;
			}
			return m_defaultHelperHost;
		}
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

	public bool HasLayers
	{
		get
		{
			if (LayerManager.Instance == null)
			{
				return false;
			}
			return LayerManager.Instance.AllLayers.Count > 0;
		}
	}

	/// <summary>The loaded session's layer names, using each layer's short name where it has one.</summary>
	public List<string> CurrentLayerNames()
	{
		List<string> names = new List<string>();

		if (LayerManager.Instance == null)
		{
			return names;
		}

		foreach (ALayer layer in LayerManager.Instance.AllLayers)
		{
			string shortName = layer.@short;
			if (string.IsNullOrEmpty(shortName))
			{
				names.Add(layer.name);
			}
			else
			{
				names.Add(shortName);
			}
		}
		return names;
	}

	private IEnumerator SendCommand(byte[] a_wav)
	{
		// The layer list travels with the audio: the helper constrains the model to these names.
		List<string> layerNames = CurrentLayerNames();

		WWWForm form = new WWWForm();
		form.AddBinaryData("audio", a_wav, "command.wav", "audio/wav");
		form.AddField("layers", ToJsonArray(layerNames));
		form.AddField("trim", "true");

		string url = $"http://{HelperHost}:{m_helperPort}/command";
		SendStarted?.Invoke(url);

		using (UnityWebRequest request = UnityWebRequest.Post(url, form))
		{
			request.timeout = m_timeoutSeconds;
			yield return request.SendWebRequest();

			string body = null;
			if (request.downloadHandler != null)
			{
				body = request.downloadHandler.text;
			}

			bool connectionFailed = request.result == UnityWebRequest.Result.ConnectionError;
			bool badData = request.result == UnityWebRequest.Result.DataProcessingError;
			if (connectionFailed || badData)
			{
				Fail($"Voice helper unreachable at {url} ({request.error})");
				yield break;
			}

			VoiceResponse response;
			try
			{
				response = VoiceResponse.FromJson(body);
			}
			catch (Exception e)
			{
				Fail($"Bad response from voice helper: {e.Message}");
				yield break;
			}

			if (!string.IsNullOrWhiteSpace(response.transcript))
			{
				TranscriptReceived?.Invoke(response.transcript);
			}

			if (request.result != UnityWebRequest.Result.Success)
			{
				// 400 and 503 still carry a JSON body with an error, and 503 may carry the transcript.
				string message = response.error;
				if (string.IsNullOrEmpty(message))
				{
					message = $"HTTP {request.responseCode}";
				}
				Fail(message);
				yield break;
			}

			m_recorder.NotifySendComplete();
			ResponseReceived?.Invoke(response);
		}
	}

	private void Fail(string a_message)
	{
		Debug.LogWarning($"[Voice] {a_message}");
		m_recorder.NotifySendComplete();
		Failed?.Invoke(a_message);
	}

	// JsonUtility cannot serialise a bare list of strings, so build the array by hand.
	private static string ToJsonArray(List<string> a_values)
	{
		string[] quoted = new string[a_values.Count];
		for (int i = 0; i < a_values.Count; i++)
		{
			string escaped = a_values[i].Replace("\\", "\\\\");
			escaped = escaped.Replace("\"", "\\\"");
			quoted[i] = "\"" + escaped + "\"";
		}
		return "[" + string.Join(",", quoted) + "]";
	}
}
