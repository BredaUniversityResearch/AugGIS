using System.Collections;
using System;
using UnityEngine;
using UnityEngine.UI;

namespace POV_Unity
{
	public class LayerMenuCategory : MonoBehaviour
	{
		public CustomXRToggle m_toggle;
        [SerializeField] Image m_icon;
		[SerializeField] int m_currentPage = 1;

        Action<int> m_onSelectCallback;
		Action<int> m_onDeselectCallback;
		int m_index;

		public void Initialise(LayerCategory a_category, Action<int> a_onSelectCallback, Action<int> a_onDeselectCallback, ToggleGroup a_toggleGroup, bool a_startOn, int a_index)
		{
			m_icon.sprite = AssetManager.GetSprite(a_category.icon);
			m_toggle.IsSelected = a_startOn;
			m_onSelectCallback = a_onSelectCallback;
			m_onDeselectCallback = a_onDeselectCallback;
			m_toggle.OnPressOn.RemoveListener(OnToggleOnListener);
			m_toggle.OnPressOff.RemoveListener(OnToggleOffListener);
			m_toggle.OnPressOn.AddListener(OnToggleOnListener);
			m_toggle.OnPressOff.AddListener(OnToggleOffListener);
			//m_toggle.group = a_toggleGroup;
			m_index = a_index;
		}

		void OnToggleOnListener()
		{
			m_onSelectCallback.Invoke(m_index);
		}

		void OnToggleOffListener()
		{
			m_onDeselectCallback.Invoke(m_index);
		}

		public int GetCurrentPage()
		{
			return m_currentPage;
		}
        public void SetCurrentPage(int tabID)
        {
            m_currentPage = tabID;
        }

        public void Select(bool ignoreCallback)
		{
            m_toggle.IsSelected = true;
        }            
    }
}