using UnityEngine;

public interface IPlaceableVisual
{
    void StoreVisuals(GameObject a_visualsPrefab);
    GameObject VisualsPrefab { get; }
}