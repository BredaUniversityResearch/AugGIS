using System.Collections.Generic;
using Sirenix.Utilities;
using UnityEngine;

public class InteractableMaterialHandler : MonoBehaviour
{
    private List<MeshRenderer> m_renderers = new List<MeshRenderer>();
    [SerializeField]
    private bool m_useDynamicColor = false;

    private void Awake()
    {
        m_renderers.AddRange(GetComponentsInChildren<MeshRenderer>());

#if UNITY_EDITOR
        if (m_renderers.IsNullOrEmpty())
        {
            Debug.LogWarning("No Mesh Renderers found on pawn and its children", this);
        }
#endif

        foreach (var renderer in m_renderers)
        {
            renderer.material.SetInt("_UseDynamicColor", m_useDynamicColor ? 1 : 0);
        }
    }

    public void UpdateTrashVisuals(bool isTrash)
    {
        foreach (var renderer in m_renderers)
        {
            renderer.material.SetInt("_IsTrash", isTrash ? 1 : 0);
        }
    }
}
