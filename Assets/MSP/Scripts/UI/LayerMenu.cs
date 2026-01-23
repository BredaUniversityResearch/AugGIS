using System;
using System.Collections;
using System.Collections.Generic;
using ColourPalette;
using Sirenix.OdinInspector;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using UnityEngine.UIElements;

namespace POV_Unity
{
	public class LayerMenu : MonoBehaviour
	{
		public event Action<int, int> CategoryChangedEvent;

		[SerializeField]
		[Required]
		private GameObject m_menuRoot;

		[SerializeField] bool m_consistentScale = true;
		//PAGES
		[SerializeField] int m_pageSize = 5;
		[SerializeField] TMP_Text m_currentPageTextField;
		[SerializeField] TMP_Text m_maxPageTextField;
		[SerializeField] CustomXRButton m_nextPageButton;
		[SerializeField] CustomXRButton m_previousPageButton;
		[SerializeField] Transform m_pageIndexInfo;

		//PREFABS TO SPAWN
		[SerializeField] GameObject m_layerPrefab;
		[SerializeField] GameObject m_categoryPrefab;
		[SerializeField] GameObject m_spacerPrefab;

		//LAYER AND CATEGORY OBJECTS
		[SerializeField] Transform m_layerParent;
		[SerializeField] Transform m_layerFooter;
		[SerializeField] Transform m_layerHeader;
		[SerializeField] Transform m_categoryParent;
		[SerializeField] ToggleGroup m_categoryToggleGroup;

		//MENU ENABLE AND DISABLE
		public UnityEvent MenuEnabledEvent;
		public UnityEvent MenuDisabledEvent;

		bool m_ignoreCallback;

		//SPAWNED OBJECTSFOR LAYERS AND CATEGORIES
		List<LayerMenuCategory> m_categoryEntries = new List<LayerMenuCategory>();
		List<LayerMenuLayer> m_layerEntries = new List<LayerMenuLayer>();

		[SerializeField]
		private int m_selectedCategoryIndex = 0;

		private List<LayerCategory> availableLayerCategories = new List<LayerCategory>();

		private void Start()
		{
			if (ImportedConfigRoot.Instance != null && ImportedConfigRoot.Instance.ImportComplete)
			{
				OnConfigImportComplete();
			}

			ImportedConfigRoot.Instance.m_onImportComplete += OnConfigImportComplete;
		}

		void OnConfigImportComplete()
		{
			int index = 0;
			int selected = m_selectedCategoryIndex;
			foreach (LayerCategory category in ImportedConfigRoot.Instance.m_displayMethodConfig.categories)
			{
				//Don't show categories without layers
				if (category.Layers.Count == 0)
				{
					continue;
				}

				availableLayerCategories.Add(category);
				if (index != 0)
				{
					Instantiate(m_spacerPrefab, m_categoryParent);
				}

				LayerMenuCategory categoryEntry = Instantiate(m_categoryPrefab, m_categoryParent).GetComponent<LayerMenuCategory>();
				categoryEntry.Initialise(category, OnCategorySelected, OnCategoryDeselected, m_categoryToggleGroup, index == selected, index);
				m_categoryEntries.Add(categoryEntry);
				if (index == m_selectedCategoryIndex)
				{
					SetLayersToCategory(category, 1);
				}
				index += 1;
			}
		}

		public void EnableMenu()
		{
			m_menuRoot.gameObject.SetActive(true);
			MenuEnabledEvent.Invoke();
		}

		public void DisableMenu()
		{
			m_menuRoot.gameObject.SetActive(false);
			MenuDisabledEvent.Invoke();
		}

		//TODO (Igli): Rework this and the networking logic
		public void SelectCategory(int a_index, int a_page)
		{
			if ((m_selectedCategoryIndex == a_index && m_categoryEntries[a_index].GetCurrentPage() == a_page) || a_index == -1)
			{
				DisableCategories();
				return;
			}
			else if (m_selectedCategoryIndex == -1 && a_index != -1)
            {
                EnableCategories();
            }

			m_selectedCategoryIndex = a_index;
			m_categoryEntries[a_index].Select(true);
			for (int i = 0; i < m_categoryEntries.Count; i++)
			{
				if (i == a_index) continue;
				m_categoryEntries[i].m_toggle.IsSelected = false;
			}

			SetLayersToCategory(availableLayerCategories[a_index], a_page);
		}

		public void SetCategoryIndexWithoutNotify(int a_index)
		{
			m_selectedCategoryIndex = a_index;
		}

		void OnCategorySelected(int a_index, int a_page)
		{
			if (m_ignoreCallback)
				return;
			
			if (CategoryChangedEvent == null || CategoryChangedEvent.GetInvocationList().Length == 0)
			{
				SelectCategory(a_index, m_categoryEntries[a_index].GetCurrentPage());
			}
			else
			{
				CategoryChangedEvent?.Invoke(a_index, a_page);
			}
		}


		void OnCategorySelected(int a_index)
		{
			OnCategorySelected(a_index, m_categoryEntries[a_index].GetCurrentPage());
		}

		void OnCategoryDeselected(int a_index)
		{
			OnCategorySelected(a_index, m_categoryEntries[a_index].GetCurrentPage());
		}

		public void ChangePage(int pageChange)
		{
			OnCategorySelected(m_selectedCategoryIndex, m_categoryEntries[m_selectedCategoryIndex].GetCurrentPage() + pageChange);
		}

		void SetLayersToCategory(LayerCategory a_category, int a_page)
		{
			//Which layers can / should be shown
			List<int> validLayers = new List<int>();
			for (int i = 0; i < a_category.Layers.Count; i++)
			{
				//Don't show layers without displaymethods
				if (a_category.Layers[i].DisplayMethods.Count == 0 || ImportedConfigRoot.Instance.m_displayMethodConfig.IsStaticLayer(a_category.Layers[i]))
					continue;
				validLayers.Add(i);
			}

			//Identify tab and layers to show
			int currentPage = a_page;//m_categoryEntries[m_selectedCategoryIndex].GetCurrentTab();
			int maxPages = Mathf.CeilToInt((float)validLayers.Count / (float)m_pageSize);
			currentPage = Math.Clamp(currentPage, 1, maxPages);

			int startLayer = (currentPage - 1) * m_pageSize;
			int endLayer = Math.Clamp(startLayer + m_pageSize, 0, validLayers.Count);

			int nextIndex = 0;
			for (int i = startLayer; i < endLayer; i++)
			{
				if (nextIndex < m_layerEntries.Count)
				{
					m_layerEntries[nextIndex].SetToLayer(a_category.Layers[validLayers[i]]);
				}
				else
				{
					LayerMenuLayer newEntry = Instantiate(m_layerPrefab, m_layerParent).GetComponent<LayerMenuLayer>();
					newEntry.Initialise();
					m_layerEntries.Add(newEntry);
					newEntry.SetToLayer(a_category.Layers[validLayers[i]]);
				}
				nextIndex++;
			}
			for (; nextIndex < m_layerEntries.Count; nextIndex++)
			{
				if (m_consistentScale)
					m_layerEntries[nextIndex].SetEmpty();
				else
                    m_layerEntries[nextIndex].gameObject.SetActive(false);
            }

            if (m_layerHeader != null)
			{
				m_layerHeader.gameObject.SetActive(true);
				m_layerHeader.SetAsFirstSibling();
			}
			if (m_layerFooter != null)
			{
				m_layerFooter.gameObject.SetActive(true);
				m_layerFooter.SetAsLastSibling();
			}

			m_categoryEntries[m_selectedCategoryIndex].SetCurrentPage(currentPage);

			SetPageInfo(maxPages, currentPage);
		}

		void SetPageInfo(int maxPages, int currentPage)
		{
			m_nextPageButton.gameObject.SetActive(!(maxPages == 1));
			m_previousPageButton.gameObject.SetActive(!(maxPages == 1));
			m_pageIndexInfo.gameObject.SetActive(!(maxPages == 1));

			if (maxPages >= 1)
			{
				m_maxPageTextField.text = maxPages.ToString();
				m_currentPageTextField.text = currentPage.ToString();

				m_previousPageButton.isInteractable = !(currentPage == 1);	
				m_nextPageButton.isInteractable = !(currentPage == maxPages);
			}
		}


		void DisableCategories()
		{
			m_selectedCategoryIndex = -1;
			m_categoryToggleGroup.SetAllTogglesOff();

			if (m_layerHeader != null) m_layerHeader.gameObject.SetActive(false);
			if (m_layerFooter != null) m_layerFooter.gameObject.SetActive(false);

			for (int i = 0; i < m_layerEntries.Count; i++)
			{
				m_layerEntries[i].gameObject.SetActive(false);
			}
		}

		void EnableCategories()
		{
			for (int i = 0; i < m_layerEntries.Count; i++)
			{
				m_layerEntries[i].gameObject.SetActive(true);
			}
		}
	}
}