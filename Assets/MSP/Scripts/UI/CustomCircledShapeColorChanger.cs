using UnityEngine;

public class CustomDiscShapeColorChanger : MonoBehaviour
{
    public MeshRenderer[] meshRenderers;

    public void ChangeColor(Color newColor)
    {
        foreach (var renderer in meshRenderers)
        {
            renderer.material.color = newColor;
        }
    }
}
