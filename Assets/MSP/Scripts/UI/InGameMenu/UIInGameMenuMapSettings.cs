using System;
using POV_Unity;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.UI;

public class UIInGameMenuMapSettings : MonoBehaviour
{
	[SerializeField]
	[Required]
	private CustomXRButton m_hideAllLayersButton;

	[SerializeField]
	[Required]
	private CustomXRButton m_deleteAllToolsButton;

	[SerializeField]
	[Required]
	private CustomXRButton m_deleteAllStationsButton;

	[SerializeField]
	[Required]
	private CustomXRButton m_deleteAllPawnsButton;

	[SerializeField]
	[Required]
	private CustomXRButton m_deleteAllDrawingsButton;

	[SerializeField]
	[Required]
	private CustomXRToggle m_hideDrawingsToggle;

	void Awake()
	{
		m_hideAllLayersButton.OnPress.AddListener(OnHideAllLayersButtonClicked);
		m_deleteAllStationsButton.OnPress.AddListener(OnDeleteAllStationsButtonClicked);
		m_deleteAllToolsButton.OnPress.AddListener(OnDeleteAllToolsButtonClicked);
		m_deleteAllPawnsButton.OnPress.AddListener(OnDestroyAllPawnsButtonClicked);
		m_deleteAllDrawingsButton.OnPress.AddListener(OnDeleteAllDrawingsButtonClicked);

		m_hideDrawingsToggle.OnPressOn.AddListener(OnHideDrawingToggleValueChanged);
		m_hideDrawingsToggle.OnPressOff.AddListener(OnShowDrawingToggleValueChanged);
	}

	private void OnShowDrawingToggleValueChanged()
	{
		SessionManager.Instance.WorldManager.ShowAllDrawings();
	}

	private void OnHideDrawingToggleValueChanged()
	{
		SessionManager.Instance.WorldManager.HideAllDrawings();
	}

	private void OnHideAllLayersButtonClicked()
	{
		foreach (ALayer layer in LayerManager.Instance.AllLayers)
		{
			if (ImportedConfigRoot.Instance.m_displayMethodConfig.IsStaticLayer(layer))
			{
				continue;
			}

			LayerManager.Instance.SetLayerVisualizationServerRPC(layer.LayerIndex, LayerVisualizationMode.Off);
		}
	}

	private void OnDeleteAllStationsButtonClicked()
	{
		SessionManager.Instance.WorldManager.DestroyAllStations();
	}
	private void OnDeleteAllToolsButtonClicked()
	{
		SessionManager.Instance.WorldManager.DestroyAllTools();
	}
	private void OnDeleteAllDrawingsButtonClicked()
	{
		SessionManager.Instance.WorldManager.DeleteAllDrawings();
	}

	private void OnDestroyAllPawnsButtonClicked()
	{
		SessionManager.Instance.WorldManager.DestroyAllPawns();
	}
}
