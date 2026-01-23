using UnityEngine;

public class AudioPresetPlayer : MonoBehaviour
{
	public void PlaySound(AudioPreset a_audioPreset)
	{
		if (a_audioPreset == null)
		{
			return;
		}

		AudioManager.Instance.PlaySound(a_audioPreset);
	}
}