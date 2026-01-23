using UnityEngine.EventSystems;
using UnityEngine;



[RequireComponent(typeof(ICustomXRUIInteractions))]
public class PointerImplCustomXRUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerUpHandler, IPointerDownHandler
{
    private ICustomXRUIInteractions m_UIElement;
    protected void Start()
    {
        m_UIElement = GetComponent<ICustomXRUIInteractions>();
    }
    
    public void OnPointerEnter(PointerEventData eventData) { m_UIElement.ElementHoverEnter(); }
    public void OnPointerExit(PointerEventData eventData) { m_UIElement.ElementHoverExit(); }
    public void OnPointerDown(PointerEventData eventData) { m_UIElement.ElementPressed(); }
    public void OnPointerUp(PointerEventData eventData) { m_UIElement.ElementReleased(); }
}
