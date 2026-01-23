using System;
using System.Collections.Generic;
using ColourPalette;
using Sirenix.OdinInspector;
using TMPro;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;

public class UIToggleablePagedList : UIPagedList
{
	public event Action<IUIListElementData> SelectedElementDataChanged;

	[SerializeField]
	[Required]
	private ToggleGroup m_toggleGroup = null;

	List<CustomXRToggle> m_toggles = new List<CustomXRToggle>();
	//Dictionary<CustomXRToggle, int> m_toggleToIndexMap = new Dictionary<CustomXRToggle, int>();

	private int m_currentlySelectedEntryIndex = -1;

	protected override void InitialiseElement(UIBaseListElement a_element, int a_index)
	{
		base.InitialiseElement(a_element, a_index);

		//Toggle toggle = a_element.GetComponent<Toggle>();

		if (a_element.TryGetComponent<Toggle>(out Toggle toggle))
		{
			Debug.LogWarning("Using Unity UI Toggle component. Consider using CustomXRToggle for better interaction support.");

			toggle.group = m_toggleGroup;
			toggle.isOn = false;

			toggle.onValueChanged.AddListener(((toggleValue) =>
			{
				if (toggleValue)
				{
					SelectedElementDataChanged?.Invoke(a_element.Data);
					m_currentlySelectedEntryIndex = a_index;
				}
				else
				{
					if (m_currentlySelectedEntryIndex == a_index)
					{
						m_currentlySelectedEntryIndex = -1;
					}
				}
			}));
		} else if (a_element.TryGetComponent<CustomXRToggle>(out CustomXRToggle xrtoggle))
		{
			xrtoggle.IsSelected = false;

			m_toggles.Add(xrtoggle);

			xrtoggle.OnPressOn.AddListener(() =>
			{
				SelectedElementDataChanged?.Invoke(a_element.Data);
				m_currentlySelectedEntryIndex = a_index;
				foreach (CustomXRToggle xrtoggleFromArray in m_toggles)
				{
					if (xrtoggleFromArray != xrtoggle)
					{
						xrtoggleFromArray.IsSelected = false;
					}
				}
			});
			
			xrtoggle.OnPressOff.AddListener(() =>
			{
				if (m_currentlySelectedEntryIndex == a_index)
				{
					m_currentlySelectedEntryIndex = -1;
				}
			});
		}
		else
		{
			Debug.LogError("A Toggle or CustomXRToggle component is required on the list element prefab to use a toggleable paged list!");
		}

	}

	public override void ChangePage(int a_newPageIndex)
	{
		base.ChangePage(a_newPageIndex);
		m_toggleGroup.SetAllTogglesOff();
	}

	public override void ClearMenu()
	{
		m_currentlySelectedEntryIndex = -1;
		base.ClearMenu();
	}

	public IUIListElementData GetCurrentlySelectedElementData()
	{
		if (m_currentlySelectedEntryIndex == -1 || CurrentPageIndex == -1)
		{
			return null;
		}

		UIPagedList.PageData currentPageData = GetCurrentPageData();

		if (currentPageData.pageElementData.Count == 0)
		{
			return null;
		}

		return currentPageData.pageElementData[m_currentlySelectedEntryIndex];
	}
}
