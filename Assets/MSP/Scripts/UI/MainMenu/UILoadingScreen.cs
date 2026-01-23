using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace POV_Unity
{
	public class UILoadingScreen : MonoBehaviour
	{
		[SerializeField]
		private MSPLoadingBar m_loadingBar;

		[SerializeField] private float m_loadingBarLerpSpeed = 0.1f;

		private float progress = 0; // 0 - 100
		private float currentBarProgress = 0;

		public void OnEnable()
		{
			ImportedConfigRoot.Instance.m_onImportProgress += SetLoadingBarPercentageAndText;
			ImportedConfigRoot.Instance.m_onImportComplete += ImportComplete;
		}

		void OnDisable()
		{
			ImportedConfigRoot.Instance.m_onImportProgress -= SetLoadingBarPercentageAndText;
			ImportedConfigRoot.Instance.m_onImportComplete -= ImportComplete;
		}

		void Update()
		{
			currentBarProgress += (progress - currentBarProgress) * m_loadingBarLerpSpeed;
			m_loadingBar.SetValue(currentBarProgress /100f);
		}

		void ImportComplete()
		{
			ImportedConfigRoot.Instance.m_onImportProgress -= SetLoadingBarPercentageAndText;
			ImportedConfigRoot.Instance.m_onImportComplete -= ImportComplete;
		}

		void SetLoadingBarPercentageAndText(float a_percentage, string a_text)
		{
			progress = a_percentage;
			m_loadingBar.SetInfoText(a_text);
		}
	}
}
