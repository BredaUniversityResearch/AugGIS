using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;

/// <summary>
/// The voice panel inside a session. One Record toggle sends a spoken sentence to the interface,
/// which decides whether it asks to show or hide layers on the map or to search the online
/// catalogue. The panel applies the changes and lists what was found, per map server.
/// </summary>
[RequireComponent(typeof(VoiceRecorder))]
[RequireComponent(typeof(VoiceClient))]
public class VoiceMenu : MonoBehaviour
{
	[SerializeField]
	private CustomXRToggle m_recordToggle;

	[SerializeField]
	private TMP_Text m_recordLabel;

	[SerializeField]
	private CustomXRButton m_newConversationButton;

	[SerializeField]
	private TMP_Text m_healthText;

	[SerializeField]
	private TMP_Text m_statusText;

	[SerializeField]
	private TMP_Text m_transcriptText;

	[SerializeField]
	private TMP_Text m_replyText;

	[SerializeField]
	private TMP_Text m_appliedText;

	[SerializeField]
	private TMP_Text m_geoServerText;

	[SerializeField]
	private TMP_Text m_emodnetText;

	[SerializeField]
	private TMP_Text m_timingText;

	private VoiceRecorder m_recorder;
	private VoiceClient m_client;
	private float m_sendStartTime;

	private void Awake()
	{
		m_recorder = GetComponent<VoiceRecorder>();
		m_client = GetComponent<VoiceClient>();

		m_recordToggle.OnPressOn.AddListener(OnRecordToggled);
		m_recordToggle.OnPressOff.AddListener(OnRecordToggled);
		m_newConversationButton.OnPress.AddListener(OnNewConversationPressed);
	}

	private void OnEnable()
	{
		m_recorder.StateChanged += OnRecorderStateChanged;
		m_recorder.Rejected += OnRecordingRejected;
		m_client.TranscriptReceived += OnTranscriptReceived;
		m_client.ResponseReceived += OnResponseReceived;
		m_client.Failed += OnSendFailed;
		m_client.HealthChecked += OnHealthChecked;

		m_recordLabel.text = "Record";
		m_statusText.text = "";
		m_healthText.text = "Checking the voice service...";
		ClearAnswer();
		m_client.CheckHealth();
	}

	private void OnDisable()
	{
		m_recorder.StateChanged -= OnRecorderStateChanged;
		m_recorder.Rejected -= OnRecordingRejected;
		m_client.TranscriptReceived -= OnTranscriptReceived;
		m_client.ResponseReceived -= OnResponseReceived;
		m_client.Failed -= OnSendFailed;
		m_client.HealthChecked -= OnHealthChecked;
	}

	private void Update()
	{
		switch (m_recorder.State)
		{
			case VoiceRecorder.EVoiceRecorderState.Recording:
				m_recordLabel.text = $"Recording {m_recorder.ElapsedSeconds:F0}s, press to send";
				break;
			case VoiceRecorder.EVoiceRecorderState.Sending:
				float waited = Time.time - m_sendStartTime;
				m_recordLabel.text = $"Thinking {waited:F0}s";
				break;
		}
	}

	private void OnRecordToggled()
	{
		m_recorder.Toggle();
		// A press while a previous recording is still being sent is ignored by the recorder, so put the toggle back.
		bool isRecording = m_recorder.State == VoiceRecorder.EVoiceRecorderState.Recording;
		m_recordToggle.IsSelected = isRecording;
	}

	private void OnNewConversationPressed()
	{
		if (m_recorder.State == VoiceRecorder.EVoiceRecorderState.Sending)
		{
			// The answer still on its way belongs to the old conversation and would bring it back.
			m_statusText.text = "Wait for the answer first.";
			return;
		}

		m_client.StartNewConversation();
		ClearAnswer();
		m_statusText.text = "New conversation.";
	}

	private void OnRecorderStateChanged(VoiceRecorder.EVoiceRecorderState a_state)
	{
		// The toggle mirrors the recorder, so a recording stopped by the time cap also switches it off.
		bool isRecording = a_state == VoiceRecorder.EVoiceRecorderState.Recording;
		m_recordToggle.IsSelected = isRecording;

		switch (a_state)
		{
			case VoiceRecorder.EVoiceRecorderState.Idle:
				m_recordLabel.text = "Record";
				break;
			case VoiceRecorder.EVoiceRecorderState.Recording:
				m_statusText.text = "";
				break;
			case VoiceRecorder.EVoiceRecorderState.Sending:
				m_sendStartTime = Time.time;
				ClearAnswer();
				break;
		}
	}

	private void OnRecordingRejected(string a_reason)
	{
		m_statusText.text = a_reason;
	}

	private void OnTranscriptReceived(string a_transcript)
	{
		m_transcriptText.text = $"You said: <noparse>{a_transcript}</noparse>";
	}

	private void OnResponseReceived(VoiceResponse a_response)
	{
		ShowTimings(a_response);

		if (string.IsNullOrWhiteSpace(a_response.transcript))
		{
			m_transcriptText.text = "You said: nothing was heard";
			return;
		}

		m_replyText.text = $"<noparse>{a_response.reply}</noparse>";

		string applied = VoiceLayerCommands.Apply(a_response, m_client.CommandableLayers());
		m_appliedText.text = $"On the map: {applied}{DescribeSearches(a_response.searches)}";

		m_geoServerText.text = DescribeFound("Team GeoServer", a_response.picks.geoserver);
		m_emodnetText.text = DescribeFound("EMODnet", a_response.picks.emodnet);
	}

	private void OnSendFailed(string a_message)
	{
		m_statusText.text = a_message;
		// A failure is the moment to find out which service is missing.
		m_client.CheckHealth();
	}

	private void OnHealthChecked(string a_line)
	{
		m_healthText.text = a_line;
	}

	private void ShowTimings(VoiceResponse a_response)
	{
		if (a_response.timings_ms == null)
		{
			return;
		}
		float speech = a_response.timings_ms.stt / 1000f;
		float model = a_response.timings_ms.llm / 1000f;
		float total = a_response.timings_ms.total / 1000f;
		m_timingText.text = $"speech {speech:F1}s, model {model:F1}s, total {total:F1}s";
	}

	private void ClearAnswer()
	{
		m_transcriptText.text = "You said: -";
		m_replyText.text = "";
		m_appliedText.text = "On the map: -";
		m_geoServerText.text = DescribeFound("Team GeoServer", new List<VoiceFoundLayer>());
		m_emodnetText.text = DescribeFound("EMODnet", new List<VoiceFoundLayer>());
		m_timingText.text = "";
	}

	// The words each search of this sentence used, so a strange result can be traced to what was searched.
	private static string DescribeSearches(List<VoiceSearch> a_searches)
	{
		if (a_searches.Count == 0)
		{
			return "";
		}

		List<string> queries = new List<string>();
		foreach (VoiceSearch search in a_searches)
		{
			queries.Add(search.query);
		}
		string joined = string.Join(", ", queries);
		return $"\nSearched: <noparse>{joined}</noparse>";
	}

	// A heading with the count, then one layer per line: its score, its name, and the sea or theme it belongs to.
	private static string DescribeFound(string a_server, List<VoiceFoundLayer> a_layers)
	{
		StringBuilder sb = new StringBuilder();
		sb.Append($"<b>{a_server} ({a_layers.Count})</b>");
		foreach (VoiceFoundLayer layer in a_layers)
		{
			// noparse: a "<" in a layer's name must show as text and not be read as a tag.
			sb.Append($"\n{layer.meaning:F2}  <noparse>{layer.spoken}</noparse> <size=80%><noparse>{layer.group}</noparse></size>");
		}
		return sb.ToString();
	}
}
