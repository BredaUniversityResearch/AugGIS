using UnityEditor;
using UnityEngine;

public class PawnColorChanger : MonoBehaviour
{
    [SerializeField] public Color color;

    public void Start()
    {
        UpdateDynamicColor();
    }

    public void UpdateDynamicColor()
    {
        GetComponent<Renderer>().material.SetColor("_DynamicColor", color);
    }

    [ContextMenu("Preview dynamic colors")]
    private void TestColor()
    {
        UpdateDynamicColor();
    }
}