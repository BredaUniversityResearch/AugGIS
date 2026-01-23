using POV_Unity;
using Sirenix.OdinInspector;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;

public class UILayerVisualisationSetting : MonoBehaviour
{
	[SerializeField]
	[Required]
	private Toggle m_toggle;

	private void Awake()
	{
		m_toggle.onValueChanged.AddListener(OnToggleValueChanged);
	}

	private void OnToggleValueChanged(bool value)
	{
		LayerManager.Instance.SetLayerVisualisationSetting(value ? LayerVisualizationMode.Fancy : LayerVisualizationMode.Basic);
	}
}
