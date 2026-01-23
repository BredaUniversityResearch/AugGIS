using UnityEngine;
using Sirenix.OdinInspector;
using ColourPalette;
using UnityEngine.UI;



[RequireComponent(typeof(ICustomXRUIVisualHelper))]
public class CustomXRUIProgressVisualization : MonoBehaviour
{
    ICustomXRUIVisualHelper m_UIElement;

    enum ProgressVisualizationType
    {
        ImageHorizontalFill,
        ImageRadialFill
    }

    [SerializeField]
    [Required]
    private GameObject m_progressBarVisual;

    public GameObject progressBarVisual
    {
        get { return m_progressBarVisual; }
    }

    [SerializeField]
    [Required]
    ProgressVisualizationType m_progressVisualizationType;

    CustomImage m_progressBarImage;
    
    void Awake()
    {
        m_UIElement = GetComponent<ICustomXRUIVisualHelper>();

        switch (m_progressVisualizationType)
        {
            case ProgressVisualizationType.ImageHorizontalFill:
                m_progressBarImage = m_progressBarVisual.GetComponent<CustomImage>();
                m_progressBarImage.fillMethod = Image.FillMethod.Horizontal;
                m_UIElement.updateProgressBarCallback += ProgressImage;
                ProgressImage(0f);
                break;

            case ProgressVisualizationType.ImageRadialFill:
                m_progressBarImage = m_progressBarVisual.GetComponent<CustomImage>();
                m_progressBarImage.fillMethod = Image.FillMethod.Radial360;
                m_UIElement.updateProgressBarCallback += ProgressImage;
                ProgressImage(0f);
                break;
        }
    }

    void OnDestroy()
    {
        if (m_UIElement != null)
        {
            m_UIElement.updateProgressBarCallback -= ProgressImage;
        }
    }

    void ProgressImage(float progress)
    {
        m_progressBarImage.fillAmount = progress;
    }
}