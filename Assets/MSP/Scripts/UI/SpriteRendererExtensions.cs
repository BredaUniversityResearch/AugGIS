using ColourPalette;
using UnityEngine;

public static class SpriteRendererExtensions
{
    public static void SetColor(this SpriteRenderer spriteRenderer, ColourAsset colorAsset)
    {
        spriteRenderer.color = colorAsset.GetColour();
    }
}