using Sirenix.OdinInspector;
using UnityEngine;
using static ICustomXRUIAudio;


[RequireComponent(typeof(ICustomXRUIAudio))]
public class CustomXRUISoundPlayer : MonoBehaviour
{
    [Title("Audio Presets")]
    [SerializeField]
    private AudioPreset beginPressPreset;

    [SerializeField]
    private AudioPreset m_endPressPreset;

    void Start()
    {
        ICustomXRUIAudio audioInterface = GetComponent<ICustomXRUIAudio>();

        audioInterface.playSound += PlaySound;
    }

    private void PlaySound(SoundType type)
    {
        AudioPreset preset = null;
        switch (type)
        {
            case SoundType.BeginPress:
                preset = beginPressPreset;
                break;

            case SoundType.EndPress:
                preset = m_endPressPreset;
                break;

            default:
                Debug.LogWarning("Unhandled sound type: " + type, this);
                return;
        }

        if (preset == null)
        {
            return;
        }

        AudioManager.Instance.PlaySound(preset);
    }
}