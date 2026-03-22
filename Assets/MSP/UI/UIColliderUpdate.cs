using System.Collections;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.XR.Interaction.Toolkit.Filtering;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

public class UIColliderUpdate : MonoBehaviour
{
    public UIDocument uiDocument;

    private void Start()
    {
        if (uiDocument == null)
            uiDocument = GetComponent<UIDocument>();

        var root = uiDocument.rootVisualElement;
        root.RegisterCallback<GeometryChangedEvent>(evt =>
        {
            UpdateCollider();
        });
    }

    private void UpdateCollider()
    {
        var root = uiDocument.rootVisualElement;
        var box = GetComponent<BoxCollider>();

        float ppu = uiDocument.panelSettings.referenceSpritePixelsPerUnit;

        float width = root.resolvedStyle.width/ppu;
        float height = root.resolvedStyle.height/ppu;

        box.size = new Vector3(width, height, 0.01f);
        box.center = new Vector3(0, height * 0.5f, 0f);
    }
}
