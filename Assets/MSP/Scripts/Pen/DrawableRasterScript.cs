using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class DrawableRasterScript : MonoBehaviour
{
    //Get quad / area size
    //Create ARGB raster based on x y pixel size
    //Set all raster to white and transparent

    //Get position (local?) of pen hit, as well as pen-size
    //Get pixel in position / in hit area
    //Change pixel color to other color

    [SerializeField]
    private Color startColor = Color.white;
    [SerializeField]
    private Texture2D rasterTexture = null;

    [SerializeField]
    private Vector2Int rasterSize = new Vector2Int(512, 512);

    [SerializeField]
    private Vector2 drawAreaSize = new Vector2(1f, 1f);
    [SerializeField]
    private Renderer previewObject = null;

    [Header("Debug")]
    [SerializeField]
    private Transform hitObject = null;
    [SerializeField]
    private float hitRadius = 0.05f;

    private void Start()
    {
        GenerateRaster();       
    }

    public void ColorFromHitPosition(RaycastHit hit)
    {
        List<Vector2Int> positions = new List<Vector2Int>();

            Vector3 quadSize = previewObject.transform.lossyScale;
            Vector2 intersectedTextureCoord = hit.textureCoord;

            Vector2Int hitPoint = new Vector2Int((int)(rasterSize.x * hit.textureCoord.x), (int)(rasterSize.y * hit.textureCoord.y));

            int xRange = (int)(hitRadius / (quadSize.x / rasterSize.x));
            int yRange = (int)(hitRadius / (quadSize.y / rasterSize.y));

            for (int x = -xRange; x < xRange; x++)
            {
                for (int y = -yRange; y < yRange; y++)
                {
                    positions.Add(new Vector2Int(hitPoint.x + x, hitPoint.y + y));
                }
            }
            positions.Add(hitPoint);

        ColorPixels(positions.ToArray(), Color.red);
    }

    // TESTING
    void ColorFromHitObject()
    {
        List<Vector2Int> positions = new List<Vector2Int>();
        RaycastHit hit;
        if (Physics.Raycast(hitObject.position, hitObject.forward, out hit))
        {
            Vector3 quadSize = previewObject.transform.lossyScale;
            Vector2 intersectedTextureCoord = hit.textureCoord;

            Debug.Log(intersectedTextureCoord);

            //Get TextureCenter
            //Get All Positions Around, based on QuadSize and Rastersize/Quadsize
            Vector2Int hitPoint = new Vector2Int((int)(rasterSize.x*hit.textureCoord.x), (int)(rasterSize.y * hit.textureCoord.y));

            //Now get all textureposition within an area, can prolly calculate this with an outward calculation
            //X Distance per Pixel = quadScale.x/rasterSize.x
            int xRange = (int)(hitRadius / (quadSize.x / rasterSize.x));
            int yRange = (int)(hitRadius / (quadSize.y / rasterSize.y));

            for (int x = -xRange; x < xRange; x++)
            {
                for (int y = -yRange; y < yRange; y++)
                {
                    positions.Add(new Vector2Int(hitPoint.x+x, hitPoint.y+y));
                }
            }
            positions.Add(hitPoint);
        }


        ColorPixels(positions.ToArray(), Color.red);

        Debug.DrawRay(hitObject.position, hitObject.forward * 1, Color.yellow);
    }
    void ColorRandomBlock()
    {
        Vector2Int position = new Vector2Int(Random.Range(0, 511), Random.Range(0, 511));
        Vector2Int size = new Vector2Int(Random.Range(0, 50), Random.Range(0, 50));

        ColorBlock(position, size, Color.red);
    }
    void ChangeRandomPixel()
    {
        Vector2Int position = new Vector2Int(Random.Range(0,511), Random.Range(0, 511));
        ColorPixel(position, Color.red);
    }

    //LOCALIZATION
    Vector2Int[] GetPixelPositions()
    {
        Vector2Int[] positions = new Vector2Int[3];

        return positions;
    }

    // SETUP
    void GenerateRaster()
    {
        rasterTexture = new Texture2D(rasterSize.x, rasterSize.y, TextureFormat.RGBA32, false);
        Color[] pixels = Enumerable.Repeat(startColor, rasterSize.x*rasterSize.y).ToArray();
        rasterTexture.SetPixels(pixels);
        rasterTexture.Apply();

        previewObject.material.mainTexture = rasterTexture;
    }

    //PIXEL EDITING
    void ColorPixel(Vector2Int position, Color color)
    {
        rasterTexture.SetPixel(position.x, position.y, color);
        rasterTexture.Apply();
    }

    void ColorPixels(Vector2Int[] positions, Color color)
    {
        foreach (Vector2Int position in positions)
            rasterTexture.SetPixel(position.x, position.y, color);

        rasterTexture.Apply();
    }
    void ColorBlock(Vector2Int position, Vector2Int size, Color color)
    {
        Color[] colors = Enumerable.Repeat(color, size.x * size.y).ToArray();
        rasterTexture.SetPixels(position.x, position.y, size.x, size.y, colors, 0);

        rasterTexture.Apply();
    }
}


