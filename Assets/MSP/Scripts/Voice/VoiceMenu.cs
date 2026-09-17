using TMPro;
using UnityEngine;

/// <summary>
/// The small panel that follows the player inside a session: a Record toggle, a status line, and the
/// transcript and commands that came back from the voice helper. Commands are shown, never applied.
/// </summary>
[RequireComponent(typeof(VoiceRecorder))]
[RequireComponent(typeof(VoiceClient))]
public class VoiceMenu : MonoBehaviour
{
	[SerializeField]
	private CustomXRToggle m_recordToggle;

	[SerializeField]
	private TMP_Text m_statusText;

	[SerializeField]
	private TMP_Text m_transcriptText;

	[SerializeField]
	private TMP_Text m_commandsText;

	private VoiceRecorder m_recorder;
	private VoiceClient m_client;

	private void Awake()
	{
		m_recorder = GetComponent<VoiceRecorder>();
		m_client = GetComponent<VoiceClient>();

		m_recordToggle.OnPressOn.AddListener(OnRecordToggled);
		m_recordToggle.OnPressOff.AddListener(OnRecordToggled);
	}

	private void OnEnable()
	{
		m_recorder.StateChanged += OnRecorderStateChanged;
		m_recorder.Rejected += OnRecordingRejected;
		m_client.TranscriptReceived += OnTranscriptReceived;
		m_client.ResponseReceived += OnResponseReceived;
		m_client.Failed += OnSendFailed;

		ShowIdle();
		m_transcriptText.text = "Heard: -";
		m_commandsText.text = "Commands: -";
	}

	private void OnDisable()
	{
		m_recorder.StateChanged -= OnRecorderStateChanged;
		m_recorder.Rejected -= OnRecordingRejected;
		m_client.TranscriptReceived -= OnTranscriptReceived;
		m_client.ResponseReceived -= OnResponseReceived;
		m_client.Failed -= OnSendFailed;
	}

	private void Update()
	{
		if (m_recorder.State == VoiceRecorder.EVoiceRecorderState.Recording)
		{
			m_statusText.text = $"Recording {m_recorder.ElapsedSeconds:F0}s of {m_recorder.MaxSeconds}s. Press again to send.";
		}
	}

	private void OnRecordToggled()
	{
		bool startingRecording = m_recorder.State == VoiceRecorder.EVoiceRecorderState.Idle;
		if (startingRecording && !m_client.HasLayers)
		{
			// The helper constrains the model to the names it is sent, so an empty list has nothing to answer with.
			m_statusText.text = "No layers loaded";
			m_recordToggle.IsSelected = false;
			return;
		}

		m_recorder.Toggle();
		// A press while a previous recording is still being sent is ignored by the recorder, so put the toggle back.
		bool isRecording = m_recorder.State == VoiceRecorder.EVoiceRecorderState.Recording;
		m_recordToggle.IsSelected = isRecording;
	}

	private void OnRecorderStateChanged(VoiceRecorder.EVoiceRecorderState a_state)
	{
		// The toggle mirrors the recorder, so a recording stopped by the time cap also switches it off.
		bool isRecording = a_state == VoiceRecorder.EVoiceRecorderState.Recording;
		m_recordToggle.IsSelected = isRecording;

		switch (a_state)
		{
			case VoiceRecorder.EVoiceRecorderState.Idle:
				ShowIdle();
				break;
			case VoiceRecorder.EVoiceRecorderState.Recording:
				m_statusText.text = "Recording...";
				break;
			case VoiceRecorder.EVoiceRecorderState.Sending:
				m_statusText.text = $"Sending to {m_client.HelperHost}...";
				break;
		}
	}

	private void OnRecordingRejected(string a_reason)
	{
		m_statusText.text = a_reason;
	}

	private void OnTranscriptReceived(string a_transcript)
	{
		m_transcriptText.text = $"Heard: {a_transcript}";
	}

	private void OnResponseReceived(VoiceResponse a_response)
	{
		if (string.IsNullOrWhiteSpace(a_response.transcript))
		{
			m_transcriptText.text = "Heard: nothing";
		}

		m_commandsText.text = $"Commands, not applied:\n{a_response.DescribeOperations()}";

		if (a_response.timings_ms != null)
		{
			m_statusText.text = $"Done in {a_response.timings_ms.total} ms. Press Record to speak.";
		}
	}

	private void OnSendFailed(string a_message)
	{
		m_statusText.text = $"Failed: {a_message}";
	}

	private void ShowIdle()
	{
		m_statusText.text = "Press Record to speak.";
	}
}
