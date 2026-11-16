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

		//A selection received for a category this machine has no entry for yet
		private Vector2Int? m_pendingSelection;

		private void Start()
		{
			ImportedConfigRoot.Instance.m_onLayerAdded += OnLayerAddedAfterImport;
		}

		public int CategoryCount => m_categoryEntries.Count;

		/// <summary>
		/// Names of the categories that currently have layers, in config order. Only valid
		/// once the import has completed.
		/// </summary>
		public List<string> GetNonEmptyCategoryNames()
		{
			List<string> names = new List<string>();
			foreach (LayerCategory category in ImportedConfigRoot.Instance.m_displayMethodConfig.categories)
			{
				if (category.Layers.Count > 0)
				{
					names.Add(category.name);
				}
			}
			return names;
		}

		/// <summary>
		/// Adds the entry for a category at the end of the category list. Which categories
		/// exist, and in which order, is decided by the host (see LayerMenuNetworked); the
		/// index of an entry is its position in that list. Only valid once the import has
		/// completed.
		/// </summary>
		public void AddCategory(string a_categoryName)
		{
			LayerCategory category = Array.Find(ImportedConfigRoot.Instance.m_displayMethodConfig.categories, a_candidate => a_candidate.name == a_categoryName);
			if (category == null)
			{
				UnityEngine.Debug.LogError($"Layer menu category '{a_categoryName}' does not exist in the display method config");
				return;
			}

			int index = m_categoryEntries.Count;
			availableLayerCategories.Add(category);
			if (index != 0)
			{
				Instantiate(m_spacerPrefab, m_categoryParent);
			}

			LayerMenuCategory categoryEntry = Instantiate(m_categoryPrefab, m_categoryParent).GetComponent<LayerMenuCategory>();
			categoryEntry.Initialise(category, OnCategorySelected, OnCategoryDeselected, m_categoryToggleGroup, index == m_selectedCategoryIndex, index);
			m_categoryEntries.Add(categoryEntry);

			if (index == m_selectedCategoryIndex)
			{
				SetLayersToCategory(category, 1);
			}

			if (m_pendingSelection.HasValue && m_pendingSelection.Value.x == index)
			{
				Vector2Int pending = m_pendingSelection.Value;
				m_pendingSelection = null;
				SelectCategory(pending.x, pending.y);
			}
		}

		//A layer was added after the initial import. If it belongs to the category
		//currently shown, refresh that page in place (it is appended to the end of its
		//category, so earlier pages don't change). If its category has no entry on this
		//machine yet there is nothing to do: the entry is created from the host's
		//category list (see AddCategory) and takes the layer from the category then.
		void OnLayerAddedAfterImport(ALayer a_layer)
		{
			int categoryIndex = a_layer.Category != null ? availableLayerCategories.IndexOf(a_layer.Category) : -1;
			if (categoryIndex >= 0 && m_selectedCategoryIndex == categoryIndex)
			{
				SetLayersToCategory(availableLayerCategories[categoryIndex], m_categoryEntries[categoryIndex].GetCurrentPage());
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
			//The selection is shared, so it can name a category this machine has no entry
			//for yet; remember it and apply it when the entry is created (AddCategory)
			if (a_index >= m_categoryEntries.Count)
			{
				m_pendingSelection = new Vector2Int(a_index, a_page);
				return;
			}

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
			//At least one (empty) page: a category can exist before any of its layers are loaded here
			int maxPages = Mathf.Max(1, Mathf.CeilToInt((float)validLayers.Count / (float)m_pageSize));
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