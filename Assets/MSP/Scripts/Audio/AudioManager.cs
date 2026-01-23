using UnityEngine;

public class AudioManager : MonoBehaviour
{
	private static AudioManager ms_instance;
	public static AudioManager Instance => ms_instance;

	private AudioSource m_audioSource;

	[SerializeField]
	private int m_audioPoolSize = 5;
	GameObject[] m_audioSourcePool;

	void Awake()
	{
		if (ms_instance == null)
		{
			ms_instance = this;
		}
		else
		{
			Debug.LogWarning("Instance of AudioManager already exists, destroying new instance");
			Destroy(gameObject);
			return;
		}

		DontDestroyOnLoad(gameObject);

		m_audioSource = gameObject.AddComponent<AudioSource>();

		// Create audio source pool
		m_audioSourcePool = new GameObject[m_audioPoolSize];
		for (int i = 0; i < m_audioPoolSize; i++)
		{
			GameObject audioSourceObj = new GameObject("PooledAudioSource_" + i);
			audioSourceObj.transform.parent = this.transform;
			audioSourceObj.AddComponent<AudioSource>();
			m_audioSourcePool[i] = audioSourceObj;
		}
	}

	public void PlaySound(AudioPreset preset)
	{
#if UNITY_EDITOR
		if (preset == null)
		{
			Debug.LogWarning("AudioPreset is null, cannot play sound.");
			return;
		}
#endif
		m_audioSource.clip = preset.audio;
		m_audioSource.priority = preset.priority;
		m_audioSource.volume = preset.volume;
		m_audioSource.pitch = preset.pitch;
		m_audioSource.panStereo = preset.stereoPan;
		m_audioSource.spatialBlend = preset.spatialBlend;
		m_audioSource.reverbZoneMix = preset.reverbZoneMix;

		m_audioSource.Play();
	}

	public void PlaySound3D(AudioPreset preset, Vector3 position)
	{
#if UNITY_EDITOR
		if (preset == null)
		{
			Debug.LogWarning("AudioPreset is null, cannot play sound.");
			return;
		}
#endif
		// Find an available audio source from the pool and use it as a 3D sound source
		foreach (GameObject audioSourceObj in m_audioSourcePool)
		{
			AudioSource source = audioSourceObj.GetComponent<AudioSource>();
			if (!source.isPlaying)
			{
				audioSourceObj.transform.position = position;

				source.clip = preset.audio;
				source.priority = preset.priority;
				source.volume = preset.volume;
				source.pitch = preset.pitch;
				source.panStereo = preset.stereoPan;
				source.spatialBlend = preset.spatialBlend;
				source.reverbZoneMix = preset.reverbZoneMix;

				source.Play();

				return;
			}
		}
#if UNITY_EDITOR
		Debug.LogWarning("All audio sources are currently in use. Cannot play sound: " + preset.audio.name, this);
#endif
	}
}
