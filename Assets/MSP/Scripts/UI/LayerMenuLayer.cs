using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;
using Unity.VisualScripting;

namespace POV_Unity
{
	public class LayerMenuLayer : MonoBehaviour
	{
		[SerializeField] TextMeshProUGUI m_nameText;
		[SerializeField] CustomXRToggle m_toggle;
		[SerializeField] RectTransform[] m_toggleLines;
		[SerializeField] Vector2 m_lineMarginOffOn;

		private LayerManager m_layerManager = null;

		bool m_ignoreCallback;
		ALayer m_currentLayer;


		public void Initialise()
		{
			m_layerManager = LayerManager.Instance;
			Debug.Assert(m_layerManager != null);

			m_toggle.OnPressOn.AddListener(OnToggled);
			m_toggle.OnPressOff.AddListener(OnToggled);
			m_layerManager.m_onLayerInstanceDataChanged += OnLayerInstanceDataChanged;

			RefreshLayerVisualisationSetting();
		}

		void OnDestroy()
		{
			m_layerManager.m_onLayerInstanceDataChanged -= OnLayerInstanceDataChanged;
		}

		private void RefreshLayerVisualisationSetting()
		{
			if(!m_toggle.IsSelected)
			{
				return;
			}

			m_layerManager.SetLayerVisualizationServerRPC(m_currentLayer.LayerIndex, m_layerManager.LayerVisualisationSetting);

			DetermineLineMargin();
		}

		void OnToggled()
		{
			if (m_ignoreCallback)
				return;

			if (m_toggle.IsSelected)
			{
				m_layerManager.SetLayerVisualizationServerRPC(m_currentLayer.LayerIndex, m_layerManager.LayerVisualisationSetting);
			}
			else
			{
				m_layerManager.SetLayerVisualizationServerRPC(m_currentLayer.LayerIndex, LayerVisualizationMode.Off);
			}
			DetermineLineMargin();
		}

		public void SetToLayer(ALayer a_layer)
		{
			m_toggle.gameObject.SetActive(true);

			gameObject.SetActive(true);
			foreach (RectTransform line in m_toggleLines)
				line.gameObject.SetActive(true);

			m_currentLayer = a_layer;
			m_nameText.text = a_layer.@short;
			RefreshVisualisationMode();
		}

		void OnLayerInstanceDataChanged(LayerInstanceData a_layerData, ALayer a_layer)
		{
			if (m_currentLayer != a_layer || !gameObject.activeSelf || m_ignoreCallback)
				return;

			RefreshVisualisationMode();
		}

		private void RefreshVisualisationMode()
		{
			m_ignoreCallback = true;
			//TODO (Igli): Atm this does not handle basic vs fancy visualization mode. Adjust this once the global settings is added.
			m_toggle.IsSelected = m_layerManager.GetLayerData(m_currentLayer.LayerIndex).m_visualizationMode != LayerVisualizationMode.Off;

			m_ignoreCallback = false;
			DetermineLineMargin();
		}

		public void SetEmpty()
		{
			m_toggle.gameObject.SetActive(false);
			foreach (RectTransform line in m_toggleLines)
				line.gameObject.SetActive(false);
		}

		//Checks if item is toggled, if so then the On line margin is used, otherwise the x margin is used
		void DetermineLineMargin()
		{
			if (m_toggle.IsSelected)
				SetLineMargin(m_lineMarginOffOn.y);
			else
				SetLineMargin(m_lineMarginOffOn.x);
		}

		void SetLineMargin(float margin)
		{
			foreach (RectTransform line in m_toggleLines)
			{
				line.SetLeft(margin);
				line.SetRight(margin);
			}
		}
	}
}