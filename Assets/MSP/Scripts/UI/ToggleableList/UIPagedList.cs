using System;
using System.Collections.Generic;
using System.Data.Common;
using ColourPalette;
using Sirenix.OdinInspector;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;

public class UIPagedList : MonoBehaviour
{
	public event Action<int, int> CurrentPageChanged;

	public struct PageData
	{
		public int pageIndex;
		public List<IUIListElementData> pageElementData;
	}

	[SerializeField]
	private bool m_spawnSeparatorBetweenElements = true;

	[SerializeField]
	[ShowIf("m_spawnSeparatorBetweenElements")]
	private GameObject m_separatorPrefab = null;

	[Header("Element Settings")]
	[SerializeField]
	[Required]
	private Transform m_elementRootTransform = null;

	[SerializeField]
	[Required]
	private UIBaseListElement m_elementPrefab = null;

	[Header("Page Settings")]
	[SerializeField]
	[Required]
	private CustomXRButton m_nextPageButton = null;

	[SerializeField]
	[Required]
	private CustomXRButton m_previousPageButton = null;

	[SerializeField]
	[Required]
	private TMPro.TextMeshProUGUI m_pageCountText = null;

	[SerializeField]
	private ColourAsset m_currentPageTextColorAsset;

	[SerializeField]
	private int m_maxElementPerPage = 5;

	private Dictionary<int, PageData> m_pageDataDictionary = new Dictionary<int, PageData>();

	private List<UIBaseListElement> m_elements = new List<UIBaseListElement>();
	private List<GameObject> m_separators = new List<GameObject>();

	private int m_currentPageCount = 0;
	public int CurrentPageCount => m_currentPageCount;

	private int m_currentPageIndex = -1;
	public int CurrentPageIndex => m_currentPageIndex;

	private int m_elementDataCount = 0;
	public int ElementDataCount => m_elementDataCount;
	private int m_lastElementAddedPageIndex = 0;

	private bool m_isInitialised = false;
	void Awake()
	{
		Initialise();
	}

	public void Initialise()
	{
		if (m_isInitialised)
		{
			return;
		}

		SpawnElements();

		if (m_currentPageCount == 0)
		{
			CreateNewPage();
		}

		ChangePage(0);

		m_nextPageButton.OnPress.AddListener(OnNextButtonClicked);
		m_previousPageButton.OnPress.AddListener(OnPreviousButtonClicked);

		m_isInitialised = true;
	}

	private void SpawnElements()
	{
		for (int i = 0; i < m_maxElementPerPage; i++)
		{
			UIBaseListElement newElement = Instantiate(m_elementPrefab, m_elementRootTransform);
			InitialiseElement(newElement, i);
		}
	}

	protected virtual void InitialiseElement(UIBaseListElement a_element, int a_index)
	{
		int index = a_index;
		a_element.GameObject.SetActive(false);
		m_elements.Add(a_element);
		if (m_spawnSeparatorBetweenElements && index < m_maxElementPerPage - 1)
		{
			GameObject newSeparator = Instantiate(m_separatorPrefab, m_elementRootTransform);
			newSeparator.SetActive(false);
			m_separators.Add(newSeparator);
		}
	}

	public void AddElementData(IUIListElementData a_data)
	{
		if (m_currentPageCount == 0)
		{
			CreateNewPage();
			m_currentPageIndex = 0;
		}

		PageData m_currentPageData = m_pageDataDictionary[m_lastElementAddedPageIndex];

		if (m_currentPageData.pageElementData.Count == m_maxElementPerPage)
		{
			m_currentPageData = CreateNewPage();
			m_lastElementAddedPageIndex++;
		}

		m_currentPageData.pageElementData.Add(a_data);

		m_pageDataDictionary[m_lastElementAddedPageIndex] = m_currentPageData;

		if (m_currentPageIndex == m_currentPageData.pageIndex)
		{
			int elementIndex = m_currentPageData.pageElementData.Count - 1;
			m_elements[elementIndex].SetData(a_data);
			SetElementActiveStatus(elementIndex, true);
		}
		ChangePage(m_currentPageIndex);
		m_elementDataCount++;
	}

	public virtual void ClearMenu()
	{
		DisableAllElements();

		m_pageDataDictionary.Clear();

		m_lastElementAddedPageIndex = 0;
		m_currentPageCount = 0;
		m_currentPageIndex = -1;
		m_elementDataCount = 0;
	}

	private PageData CreateNewPage()
	{
		PageData newPageData = new PageData();

		newPageData.pageIndex = m_currentPageCount;
		newPageData.pageElementData = new List<IUIListElementData>();

		m_pageDataDictionary[m_currentPageCount] = newPageData;
		m_currentPageCount++;
		UpdatePageText();
		UpdateNavigationElements();
		return newPageData;
	}

	private void OnNextButtonClicked()
	{
		if (m_currentPageIndex < m_currentPageCount - 1)
		{
			ChangePage(m_currentPageIndex + 1);
		}
	}

	private void OnPreviousButtonClicked()
	{
		if (m_currentPageIndex > 0)
		{
			ChangePage(m_currentPageIndex - 1);
		}
	}

	public virtual void ChangePage(int a_newPageIndex)
	{
		if (CurrentPageIndex == a_newPageIndex)
		{
			return;
		}

		DisableAllElements();

		PageData pageData = m_pageDataDictionary[a_newPageIndex];

		for (int i = 0; i < pageData.pageElementData.Count; i++)
		{
			m_elements[i].SetData(pageData.pageElementData[i]);
			SetElementActiveStatus(i, true);
		}

		int prevPageIndex = m_currentPageIndex;
		m_currentPageIndex = a_newPageIndex;

		UpdatePageText();
		UpdateNavigationElements();

		CurrentPageChanged?.Invoke(prevPageIndex, m_currentPageIndex);
	}

	public PageData GetCurrentPageData()
	{
		return m_pageDataDictionary[m_currentPageIndex];
	}

	private void UpdatePageText()
	{
		string colourString = "<color=#FFFFFF>";

		if (m_currentPageTextColorAsset != null)
		{
			colourString = "<color=#" + m_currentPageTextColorAsset.GetColour().ToHexString() + ">";
		}

		m_pageCountText.text = colourString + (m_currentPageIndex + 1).ToString() + "</color> / " + m_currentPageCount.ToString();
	}

	void UpdateNavigationElements()
	{
		bool elementsState = m_currentPageCount > 1;
		m_pageCountText.gameObject.SetActive(elementsState);
		m_nextPageButton.gameObject.SetActive(elementsState);
		m_previousPageButton.gameObject.SetActive(elementsState);

		if (!elementsState) return;

		m_previousPageButton.isInteractable = (m_currentPageIndex + 1) != 1;
		m_nextPageButton.isInteractable = (m_currentPageIndex + 1) != m_currentPageCount;
	}

	private void SetElementActiveStatus(int a_index, bool a_isActive)
	{
		m_elements[a_index].GameObject.SetActive(a_isActive);

		if (m_spawnSeparatorBetweenElements && a_index < m_maxElementPerPage - 1)
		{
			m_separators[a_index].gameObject.SetActive(a_isActive);
		}
	}

	public bool ContainsElementData(IUIListElementData a_elementData)
	{
		foreach (PageData pageData in m_pageDataDictionary.Values)
		{
			foreach (IUIListElementData data in pageData.pageElementData)
			{
				if (data.Text == a_elementData.Text)
				{
					return true;
				}
			}
		}
		
		return false;
	}

	public void RefreshAllElementData()
	{
		foreach (UIBaseListElement uIBaseListElement in m_elements)
		{
			if (uIBaseListElement.gameObject.activeInHierarchy)
			{
				uIBaseListElement.RefreshData();
			}
		}
	}

	private void DisableAllElements()
	{
		foreach (UIBaseListElement element in m_elements)
		{
			element.GameObject.SetActive(false);
		}

		foreach (GameObject separator in m_separators)
		{
			separator.SetActive(false);
		}
	}
}
