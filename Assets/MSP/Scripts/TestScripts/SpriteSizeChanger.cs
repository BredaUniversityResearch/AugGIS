using Sirenix.OdinInspector;
using UnityEngine;

public class SpriteSizeChanger : MonoBehaviour
{
    [SerializeField] private Vector2 m_sizeIncrement = new Vector2(0.1f,0.1f);
    [SerializeField] private SpriteRenderer m_spriteRenderer;

    [Button("IncreaseSpriteSize")]
    private void IncreaseSpriteSize()
    {
        m_spriteRenderer.size += m_sizeIncrement;
    }

    [Button("DecreaseSpriteSize")]
    private void DecreaseSpriteSize()
    {
        Debug.Log("LOLOLOLOOL");
        m_spriteRenderer.size -= m_sizeIncrement;
    }
}
