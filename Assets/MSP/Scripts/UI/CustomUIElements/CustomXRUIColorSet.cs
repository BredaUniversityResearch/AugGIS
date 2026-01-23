using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using Sirenix.OdinInspector;
using ColourPalette;

[RequireComponent(typeof(ICustomXRUIVisualHelper))]
public class CustomXRUIColorSet : SerializedMonoBehaviour
{
    [EnumToggleButtons]
    public CustomXRUIType m_UIType = CustomXRUIType.Button;

    public enum CustomXRUIType
    {
        Button,
        Toggle
    }

    public List<Graphic> targetGraphics = new List<Graphic>();

    [Header("Color per state:")]

    [ShowIf("@this.m_UIType == CustomXRUIType.Button || this.m_UIType == CustomXRUIType.Toggle")]
    [LabelText("Disabled")]
    public IColourContainer colorDisabled = new ConstColour(Color.darkGray);

    [ShowIf("@this.m_UIType == CustomXRUIType.Button")]
    [LabelText("Normal")]
    public IColourContainer colorNormal = new ConstColour(Color.grey);

    [ShowIf("@this.m_UIType == CustomXRUIType.Button || this.m_UIType == CustomXRUIType.Toggle")]
    [LabelText("Hover")]
    public IColourContainer colorHover = new ConstColour(Color.lightGray);

    [ShowIf("@this.m_UIType == CustomXRUIType.Button || this.m_UIType == CustomXRUIType.Toggle")]
    [LabelText("Pressed")]
    public IColourContainer colorPressed = new ConstColour(Color.white);

    [ShowIf("@this.m_UIType == CustomXRUIType.Toggle")]
    [LabelText("Selected")]
    public IColourContainer colorSelected = new ConstColour(Color.white);

    [ShowIf("@this.m_UIType == CustomXRUIType.Toggle")]
    [LabelText("Deselected")]
    public IColourContainer colorDeselected = new ConstColour(Color.white);

    [Header("Progress bar colors:")]

    [ShowIf("@this.m_UIType == CustomXRUIType.Button || this.m_UIType == CustomXRUIType.Toggle")]
    [LabelText("Default")]
    public IColourContainer colorFill = new ConstColour(Color.black);

    [ShowIf("@this.m_UIType == CustomXRUIType.Toggle")]
    [LabelText("Hovered")]
    public IColourContainer colorFillHovered = new ConstColour(Color.white);

    // [ShowIf("@this.m_UIType == CustomXRUIType.Button || this.m_UIType == CustomXRUIType.Toggle")]
    // public IColourContainer colorFillPressed = new ConstColour(Color.white);
    [HideInInspector]
    [LabelText("Pressed")]
    public IColourContainer colorFillPressed => colorFill; // Design mentions to use the same color as fill

    ICustomXRUIVisualHelper m_UIElement;

    private Graphic m_progressBarVisual;

    void Awake()
    {
        SubscribeToAssetChange();
        m_UIElement = GetComponent<ICustomXRUIVisualHelper>();

        if (TryGetComponent<CustomXRUIProgressVisualization>(out CustomXRUIProgressVisualization progressVisualizationComponent))
        {
            m_progressBarVisual = progressVisualizationComponent.progressBarVisual.GetComponent<Graphic>();
            m_progressBarVisual.color = colorFill.GetColour();
        }

        m_UIElement.stateChangeCallback += HandleInteractabilityChange;
    }

    void OnDestroy()
    {
        if (m_UIElement != null)
            m_UIElement.stateChangeCallback -= HandleInteractabilityChange;

        UnSubscribeFromAssetChange();
    }

    private void HandleInteractabilityChange(ICustomXRUIVisualHelper.CustomState newState)
    {
        switch (newState)
        {
            case ICustomXRUIVisualHelper.CustomState.Normal:
                SetGraphicSetToColor(colorNormal);
                SetFillColor(colorFill);
                break;

            case ICustomXRUIVisualHelper.CustomState.Hovered:
                SetGraphicSetToColor(colorHover);
                SetFillColor(colorFillHovered);
                break;

            case ICustomXRUIVisualHelper.CustomState.Pressed:
                SetGraphicSetToColor(colorPressed);
                SetFillColor(colorFillPressed);
                break;

            case ICustomXRUIVisualHelper.CustomState.Disabled:
                SetGraphicSetToColor(colorDisabled);
                SetFillColor(colorDisabled);
                break;

            case ICustomXRUIVisualHelper.CustomState.Selected:
                SetGraphicSetToColor(colorSelected);
                SetFillColor(colorFill);
                break;

            case ICustomXRUIVisualHelper.CustomState.Deselected:
                SetGraphicSetToColor(colorDeselected);
                SetFillColor(colorFill);
                break;

            default:
                SetGraphicSetToColor(colorNormal);
                SetFillColor(colorFill);
                break;
        }
    }

    void SetGraphicSetToColor(IColourContainer colourAsset)
    {
        foreach (Graphic g in targetGraphics)
        {
            if (null == g)
            {
                Debug.LogError("Missing graphic: " + gameObject.name);
                continue;
            }
            g.color = colourAsset.GetColour();
        }
    }

    void SetFillColor(IColourContainer colourAsset)
    {
        if (m_progressBarVisual == null) return;
        m_progressBarVisual.color = colourAsset.GetColour();
    }

    void SubscribeToAssetChange()
    {
        if (Application.isPlaying)
        {
            colorNormal?.SubscribeToChanges(OnNormalColourAssetChanged);
            colorPressed?.SubscribeToChanges(OnPressedColourAssetChanged);
            colorHover?.SubscribeToChanges(OnHoverColourAssetChanged);
            colorSelected?.SubscribeToChanges(OnSelectedColourAssetChanged);
            colorDeselected?.SubscribeToChanges(OnDeselectedColourAssetChanged);
            colorDisabled?.SubscribeToChanges(OnDisabledColourAssetChanged);
            colorFill?.SubscribeToChanges(OnFillColourAssetChanged);
            colorFillHovered?.SubscribeToChanges(OnFillHoverColourAssetChanged);
            colorFillPressed?.SubscribeToChanges(OnFillPressedColourAssetChanged);
        }
    }

    void UnSubscribeFromAssetChange()
    {
        if (Application.isPlaying)
        {
            colorNormal?.UnSubscribeFromChanges(OnNormalColourAssetChanged);
            colorPressed?.UnSubscribeFromChanges(OnPressedColourAssetChanged);
            colorHover?.UnSubscribeFromChanges(OnHoverColourAssetChanged);
            colorFill?.UnSubscribeFromChanges(OnFillColourAssetChanged);
            colorSelected?.UnSubscribeFromChanges(OnSelectedColourAssetChanged);
            colorDeselected?.UnSubscribeFromChanges(OnDeselectedColourAssetChanged);
            colorDisabled?.UnSubscribeFromChanges(OnDisabledColourAssetChanged);
            colorFillHovered?.UnSubscribeFromChanges(OnFillHoverColourAssetChanged);
            colorFillPressed?.UnSubscribeFromChanges(OnFillPressedColourAssetChanged);
        }
    }

    void OnNormalColourAssetChanged(Color newColour)
    {
        if (m_UIElement.state == ICustomXRUIVisualHelper.CustomState.Normal)
            SetGraphicSetToColor(colorNormal);
    }

    void OnPressedColourAssetChanged(Color newColour)
    {
        if (m_UIElement.state == ICustomXRUIVisualHelper.CustomState.Pressed)
            SetGraphicSetToColor(colorPressed);
    }

    void OnHoverColourAssetChanged(Color newColour)
    {
        if (m_UIElement.state == ICustomXRUIVisualHelper.CustomState.Hovered)
            SetGraphicSetToColor(colorHover);
    }

    void OnDisabledColourAssetChanged(Color newColour)
    {
        if (m_UIElement.state == ICustomXRUIVisualHelper.CustomState.Disabled)
        {
            SetGraphicSetToColor(colorDisabled);
            SetFillColor(colorDisabled);
        }
    }

    void OnSelectedColourAssetChanged(Color newColour)
    {
        if (m_UIElement.state == ICustomXRUIVisualHelper.CustomState.Selected)
            SetGraphicSetToColor(colorSelected);
    }

    void OnDeselectedColourAssetChanged(Color newColour)
    {
        if (m_UIElement.state == ICustomXRUIVisualHelper.CustomState.Deselected)
            SetGraphicSetToColor(colorDeselected);
    }

    void OnFillColourAssetChanged(Color newColour)
    {
        if (m_progressBarVisual == null) return;

        if (m_UIElement.state == ICustomXRUIVisualHelper.CustomState.Normal
        ||  m_UIElement.state == ICustomXRUIVisualHelper.CustomState.Selected
        ||  m_UIElement.state == ICustomXRUIVisualHelper.CustomState.Deselected)
            SetFillColor(colorFill);
    }

    void OnFillHoverColourAssetChanged(Color newColour)
    {
        if (m_progressBarVisual == null) return;
        if (m_UIElement.state == ICustomXRUIVisualHelper.CustomState.Hovered)
            SetFillColor(colorFillHovered);
    }
    
    void OnFillPressedColourAssetChanged(Color newColour)
    {
        if (m_progressBarVisual == null) return;
        if (m_UIElement.state == ICustomXRUIVisualHelper.CustomState.Pressed)
            SetFillColor(colorFillPressed);
    }
}