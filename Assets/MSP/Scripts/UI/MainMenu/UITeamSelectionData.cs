using System;
using Sirenix.OdinInspector;
using Sirenix.Serialization;
using Unity.VisualScripting;
using UnityEngine;

public class TeamSelector : MonoBehaviour
{
	[Serializable]
	public class UITeamSelectionData : IUICarouselSelectionData
	{
		private Sprite m_icon;

		[SerializeField]
		private Color m_color;

		[SerializeField]
		private string m_label;

		public Sprite Icon => m_icon;
		public Color Color => m_color;
		public string Label => m_label;

		public void SetIcon(Sprite a_newIcon)
		{
			m_icon = a_newIcon;
		}
	}

	[SerializeField]
	[Required]
	private LocalPlayerSessionDataSO m_playerSessionData;

	[SerializeField]
	[Required]
	private UICarouselSelector m_uiCarouselSelector;

	[SerializeField]
	private Sprite m_optionIcon;

	[SerializeField]
	private AudioPreset m_selectionChangeAudioPreset;

	private bool m_canPlaySound = false;

	[SerializeField]
	private UITeamSelectionData[] m_teamOptionData;

	private void Awake()
	{
		m_uiCarouselSelector.SelectionChanged += OnCarouselSelectionChanged;

		for (int i = 0; i < m_teamOptionData.Length; i++)
		{
			UITeamSelectionData teamSelectionOptionData = m_teamOptionData[i];
			teamSelectionOptionData.SetIcon(m_optionIcon);
			m_uiCarouselSelector.AddNewOption(teamSelectionOptionData);
		}
	}

	private void OnEnable()
	{
		//we alway select the first one regardless if it matches previous color or not
		int selectedIndex = 0;

		for (int i = 0; i < m_uiCarouselSelector.OptionCount; i++)
		{
			UICarouselSelectorOption option = m_uiCarouselSelector.GetOptionAtIndex(i);
			Color optionColor = (option.Data as UITeamSelectionData).Color;
			if (optionColor == m_playerSessionData.teamColor)
			{
				selectedIndex = i;
				break;
			}
		}

		m_uiCarouselSelector.SelectOptionAtIndex(selectedIndex);

		m_canPlaySound = true;
	}

	private void OnDisable()
	{
		m_canPlaySound = false;
	}

	private void OnCarouselSelectionChanged(IUICarouselSelectionData a_data)
	{
		if (m_canPlaySound)
			AudioManager.Instance.PlaySound(m_selectionChangeAudioPreset);
		
		m_playerSessionData.teamColor = (a_data as UITeamSelectionData).Color;
	}
}
