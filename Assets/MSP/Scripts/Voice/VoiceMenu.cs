using TMPro;
using UnityEngine;

/// <summary>
/// The voice panel inside a session. One Record toggle sends a spoken sentence to the interface,
/// which decides whether it asks to show or hide layers on the map, to search the online
/// catalogue, or to download a layer found earlier, which is done on the helper and never sent
/// here. The panel applies the changes and adds the sentence and its answer to the conversation
/// list, which shows what was found per map server and the download's line beside it.
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

	// For what is not about one sentence: a recording that was not sent, and the answers of New conversation.
	[SerializeField]
	private TMP_Text m_statusText;

	[SerializeField]
	private VoiceConversationView m_conversationView;

	private VoiceRecorder m_recorder;
	private VoiceClient m_client;
	private float m_sendStartTime;

	// The words of the sentence being sent, for when the interface sends them and then fails.
	private string m_pendingTranscript;

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

		// Switching off drops a request under way (see VoiceClient.OnDisable), so its answer never comes.
		// A recording carries on while the panel is hidden, so its turn stays for when it is sent.
		bool isRecording = m_recorder.State == VoiceRecorder.EVoiceRecorderState.Recording;
		if (!isRecording)
		{
			m_conversationView.DropPendingTurn();
		}
	}

	private void Update()
	{
		switch (m_recorder.State)
		{
			case VoiceRecorder.EVoiceRecorderState.Recording:
				string recording = $"Recording {m_recorder.ElapsedSeconds:F0}s";
				m_recordLabel.text = $"{recording}, press to send";
				m_conversationView.ShowPendingWords(recording);
				break;
			case VoiceRecorder.EVoiceRecorderState.Sending:
				float waited = Time.time - m_sendStartTime;
				string thinking = $"Thinking {waited:F0}s";
				m_recordLabel.text = thinking;
				m_conversationView.ShowPendingReply(thinking);
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
		m_conversationView.Clear();
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
				m_conversationView.BeginPendingTurn("Recording 0s");
				break;
			case VoiceRecorder.EVoiceRecorderState.Sending:
				m_sendStartTime = Time.time;
				m_pendingTranscript = null;
				m_conversationView.ShowPendingWords("...");
				break;
		}
	}

	private void OnRecordingRejected(string a_reason)
	{
		m_conversationView.DropPendingTurn();
		m_statusText.text = a_reason;
	}

	private void OnTranscriptReceived(string a_transcript)
	{
		m_pendingTranscript = a_transcript;
		m_conversationView.ShowPendingWords($"<noparse>{a_transcript}</noparse>");
	}

	private void OnResponseReceived(VoiceResponse a_response)
	{
		if (string.IsNullOrWhiteSpace(a_response.transcript))
		{
			m_conversationView.FinishPendingTurn(VoiceTurn.Answered(a_response, null));
			return;
		}

		string applied = VoiceLayerCommands.Apply(a_response, m_client.CommandableLayers());
		m_conversationView.FinishPendingTurn(VoiceTurn.Answered(a_response, applied));
	}

	private void OnSendFailed(string a_message)
	{
		// Only a sentence's send can fail, so the error goes in that sentence's turn and stays with it.
		m_conversationView.FinishPendingTurn(VoiceTurn.Failed(m_pendingTranscript, a_message));
		// A failure is the moment to find out which service is missing.
		m_client.CheckHealth();
	}

	private void OnHealthChecked(string a_line)
	{
		m_healthText.text = a_line;
	}
}
