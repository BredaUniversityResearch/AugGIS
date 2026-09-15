using System;
using UnityEngine;
#if UNITY_ANDROID && !UNITY_EDITOR
using UnityEngine.Android;
#endif

/// <summary>
/// Records the microphone as a press-to-start, press-to-stop clip and hands the finished WAV to
/// whoever listens to <see cref="ClipReady"/>. Recordings that are too short or silent are dropped.
/// </summary>
public class VoiceRecorder : MonoBehaviour
{
	public enum EVoiceRecorderState
	{
		Idle,
		Recording,
		Sending
	}

	// Whisper's native rate; recording higher only costs bytes.
	private const int SAMPLE_RATE = 16000;
	private const float MIN_SECONDS = 0.5f;
	private const float MIN_PEAK = 0.01f;

	[SerializeField]
	private int m_maxSeconds = 15;

	public event Action<EVoiceRecorderState> StateChanged;
	public event Action<byte[]> ClipReady;
	public event Action<string> Rejected;

	private EVoiceRecorderState m_state = EVoiceRecorderState.Idle;
	private AudioClip m_clip;
	private string m_device;
	private float m_startTime;

	public EVoiceRecorderState State => m_state;
	public float ElapsedSeconds
	{
		get
		{
			if (m_state != EVoiceRecorderState.Recording)
			{
				return 0f;
			}
			return Time.time - m_startTime;
		}
	}
	public int MaxSeconds => m_maxSeconds;

	private void Start()
	{
#if UNITY_ANDROID && !UNITY_EDITOR
		if (!Permission.HasUserAuthorizedPermission(Permission.Microphone))
		{
			Permission.RequestUserPermission(Permission.Microphone);
		}
#endif
	}

	private void Update()
	{
		if (m_state == EVoiceRecorderState.Recording && ElapsedSeconds >= m_maxSeconds)
		{
			Debug.LogWarning($"[Voice] Hit the {m_maxSeconds}s cap, sending what was recorded.");
			StopAndSend();
		}
	}

	/// <summary>Idle starts a recording, Recording stops and sends it, Sending is ignored.</summary>
	public void Toggle()
	{
		switch (m_state)
		{
			case EVoiceRecorderState.Idle:
				StartRecording();
				break;
			case EVoiceRecorderState.Recording:
				StopAndSend();
				break;
			case EVoiceRecorderState.Sending:
				Debug.Log("[Voice] Still sending the previous command, press ignored.");
				break;
		}
	}

	public void StartRecording()
	{
		if (m_state != EVoiceRecorderState.Idle)
		{
			return;
		}

		if (Microphone.devices.Length == 0)
		{
			Debug.LogError("[Voice] No microphone device. On a sideloaded build this usually means the RECORD_AUDIO permission was not granted.");
			Rejected?.Invoke("No microphone");
			return;
		}

		m_device = Microphone.devices[0];
		m_clip = Microphone.Start(m_device, false, m_maxSeconds, SAMPLE_RATE);

		if (m_clip == null)
		{
			Debug.LogError($"[Voice] Microphone.Start returned null for device '{m_device}'.");
			Rejected?.Invoke("Microphone failed to start");
			return;
		}

		m_startTime = Time.time;
		SetState(EVoiceRecorderState.Recording);
	}

	public void StopAndSend()
	{
		if (m_state != EVoiceRecorderState.Recording)
		{
			return;
		}

		// The position must be read before End(); afterwards it is 0.
		int written = Microphone.GetPosition(m_device);
		Microphone.End(m_device);

		if (m_clip == null || written <= 0)
		{
			Debug.LogWarning("[Voice] Nothing was recorded.");
			SetState(EVoiceRecorderState.Idle);
			Rejected?.Invoke("Nothing recorded");
			return;
		}

		float[] samples = new float[written * m_clip.channels];
		m_clip.GetData(samples, 0);

		float peak = WavEncoder.PeakAmplitude(samples);
		float seconds = written / (float)SAMPLE_RATE;
		Debug.Log($"[Voice] Captured {seconds:F2}s, peak amplitude {peak:F4}");

		bool tooShort = seconds < MIN_SECONDS;
		bool silent = peak < MIN_PEAK;
		if (tooShort || silent)
		{
			string reason;
			if (tooShort)
			{
				reason = $"Too short ({seconds:F1}s), not sent";
			}
			else
			{
				reason = $"Silent (peak {peak:F3}), not sent. Microphone permission?";
			}
			Debug.LogWarning($"[Voice] {reason}");
			m_clip = null;
			SetState(EVoiceRecorderState.Idle);
			Rejected?.Invoke(reason);
			return;
		}

		byte[] wav = WavEncoder.Encode16(samples, SAMPLE_RATE, m_clip.channels);
		m_clip = null;

		SetState(EVoiceRecorderState.Sending);
		ClipReady?.Invoke(wav);
	}

	/// <summary>Called by the sender when the upload finished, either way, so the next press records again.</summary>
	public void NotifySendComplete()
	{
		if (m_state == EVoiceRecorderState.Sending)
		{
			SetState(EVoiceRecorderState.Idle);
		}
	}

	private void SetState(EVoiceRecorderState a_state)
	{
		m_state = a_state;
		StateChanged?.Invoke(a_state);
	}
}
