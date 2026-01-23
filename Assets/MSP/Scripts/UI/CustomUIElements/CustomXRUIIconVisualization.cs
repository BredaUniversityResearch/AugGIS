using UnityEngine;
using Sirenix.OdinInspector;
using ColourPalette;
using UnityEngine.UI;



[RequireComponent(typeof(CustomXRUIColorSet), typeof(ICustomXRUIVisualHelper))]
public class CustomXRToggleIconVisualization : MonoBehaviour
{
    ICustomXRUIVisualHelper m_UIElement;
    CustomXRUIColorSet m_colorSet;

    [SerializeField]
    [Required]
    Graphic m_icon;

    void Awake()
    {
        m_UIElement = GetComponent<ICustomXRUIVisualHelper>();
        m_colorSet = GetComponent<CustomXRUIColorSet>();

        m_UIElement.stateChangeCallback += HandleIconColor;
    }

    void OnDestroy()
    {
        if (m_UIElement != null)
            m_UIElement.stateChangeCallback -= HandleIconColor;
    }

    private void HandleIconColor(ICustomXRUIVisualHelper.CustomState newState)
    {
        switch (newState)
        {
            case ICustomXRUIVisualHelper.CustomState.Disabled:
                m_icon.color = m_colorSet.colorDisabled.GetColour();
                break;

            case ICustomXRUIVisualHelper.CustomState.Selected:
                m_icon.color = m_colorSet.colorFill.GetColour();
                break;

            case ICustomXRUIVisualHelper.CustomState.Deselected:
                m_icon.color = m_colorSet.colorDeselected.GetColour();
                break;
        }
    }
}