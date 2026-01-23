using UnityEngine;
using ColourPalette;

public class SpriteRendererColorChanger : MonoBehaviour
{
    [SerializeField]
    private SpriteRenderer m_spriteRenderer = null;

    public void ChangeColor(Color newColor)
    {
        m_spriteRenderer.color = newColor;
    }
    public void ChangeColor(ColourAsset newColor)
    {
        m_spriteRenderer.color = newColor.GetColour();
    }
}
