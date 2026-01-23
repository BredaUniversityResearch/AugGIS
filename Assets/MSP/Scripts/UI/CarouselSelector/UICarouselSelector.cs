using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.UI;

public class UICarouselSelector : MonoBehaviour
{
	public Action<IUICarouselSelectionData> SelectionChanged;

	[Header("Selection Settings")]
	[SerializeField]
	[Required]
	private UICarouselSelectorSelection m_selection;

	[Header("Traversal Buttons")]
	[SerializeField]
	[Required]
	private Button m_previousButton;

	[SerializeField]
	[Required]
	private Button m_nextButton;

	[Header("Option Settings")]
	[SerializeField]
	private bool m_shouldOptionWrapAround = true;

	[SerializeField]
	[Required]
	private UICarouselSelectorOption m_optionPrefab;

	[SerializeField]
	[Required]
	private RectTransform m_optionRoot;

	[Header("Debug")]
	[SerializeField]
	[ReadOnly]
	private List<UICarouselSelectorOption> m_options;

	private int m_currentlySelectedOptionIndex = -1;
	public IUICarouselSelectionData CurrentSelectionData => m_selection.CurrentData;
	public int OptionCount => m_options.Count;

	private void Start()
	{
		m_nextButton.onClick.AddListener(OnNextButtonClicked);
		m_previousButton.onClick.AddListener(OnPreviousButtonClicked);
	}

	public void AddNewOption(IUICarouselSelectionData a_optionSelectionData)
	{
		UICarouselSelectorOption newOption = Instantiate(m_optionPrefab, m_optionRoot);
		newOption.SetData(a_optionSelectionData);

		m_options.Add(newOption);

		int index = m_options.Count - 1;
		newOption.XRSimpleInteractable.selectEntered.AddListener((args) => { SelectOptionAtIndex(index); });
	}

	public void OnNextButtonClicked()
	{
		int targetIndex = m_currentlySelectedOptionIndex + 1;
		if (targetIndex > m_options.Count - 1)
		{
			if(m_shouldOptionWrapAround)
			{
				targetIndex = 0;
			}
			else
			{
				return;
			}
		}
		
		SelectOptionAtIndex(targetIndex);
	}

	public void OnPreviousButtonClicked()
	{
		int targetIndex = m_currentlySelectedOptionIndex - 1;
		if (targetIndex < 0)
		{
			if (m_shouldOptionWrapAround)
			{
				targetIndex = m_options.Count - 1;
			}
			else
			{
				return;
			}
		}

		SelectOptionAtIndex(targetIndex);
	}


	public void SelectOptionAtIndex(int a_optionIndex)
	{
		SelectOptionAtIndexWithoutNotify(a_optionIndex);
		SelectionChanged?.Invoke(m_selection.CurrentData);
	}

	public void SelectOptionAtIndexWithoutNotify(int a_optionIndex)
	{
		UICarouselSelectorOption previousSelectedOption = GetOptionAtIndex(m_currentlySelectedOptionIndex);

		if (previousSelectedOption != null)
		{
			previousSelectedOption.DeSelect();
		}

		m_currentlySelectedOptionIndex = a_optionIndex;

		UICarouselSelectorOption currentlySelectedOption = GetOptionAtIndex(m_currentlySelectedOptionIndex);
		currentlySelectedOption.Select();

		m_selection.SetData(currentlySelectedOption.Data);
	}


	public UICarouselSelectorOption GetOptionAtIndex(int a_index)
	{
		if (a_index < 0 || a_index >= m_options.Count)
		{
			return null;
		}

		return m_options[a_index];
	}
}
