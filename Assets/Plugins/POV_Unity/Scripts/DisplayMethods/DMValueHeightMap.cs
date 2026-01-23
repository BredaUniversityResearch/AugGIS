using System;
using System.Collections.Generic;
using Unity.Mathematics;
using Unity.VisualScripting;
using Newtonsoft.Json;
using UnityEngine;

namespace POV_Unity
{
    public class DMValueHeightMap : ARasterDisplayMethod
    {
        public float groundPosY = 0.0f;
        public float posYScaler = .1f;
        public string colour_scale = "Gradient_Rainbow";

        protected override IDisplayMethodRenderData DisplayRasterLayer(RasterLayer a_layer, GameObject a_displayMethodRoot)
        {
            List<Vector3> vertices = new List<Vector3>();
            List<Color> colors = new List<Color>();

            List<int> triangles = new List<int>();

            Texture2D colourTexture = AssetManager.GetTexture(colour_scale);

            // Calculating vertices for the heightmap
            for (int row = 0; row < a_layer.Raster.height; row++)
            {
                for (int col = 0; col < a_layer.Raster.width; col++)
                {
                    float texValue = a_layer.Raster.GetPixel(col, -row - 1).r;

                    // if (texValue < cutoff)
                    // 	continue;

                    Vector3 pos = CalculatePosition(row, col, texValue, new int2(a_layer.Raster.width, a_layer.Raster.height), a_layer.coordinate0[0], a_layer.coordinate0[1], a_layer.coordinate1[0], a_layer.coordinate1[1]);
                    vertices.Add(pos);

                    colors.Add(colourTexture.GetPixelBilinear(texValue, 0.5f));
                }
            }
            
            for (int row = 0; row < a_layer.Raster.height - 1; row++)
            {
                for (int col = 0; col < a_layer.Raster.width - 1; col++)
                {
                    int bottomLeft = row * a_layer.Raster.width + col;
                    int bottomRight = bottomLeft + 1;
                    int topLeft = (row + 1) * a_layer.Raster.width + col;
                    int topRight = topLeft + 1;

                    // Create interpolated center vertex
                    Vector3 centerPos = (vertices[bottomLeft] + vertices[bottomRight] + vertices[topLeft] + vertices[topRight]) * 0.25f;
                    Color centerColor = (colors[bottomLeft] + colors[bottomRight] + colors[topLeft] + colors[topRight]) * 0.25f;
                    
                    int centerIndex = vertices.Count;
                    vertices.Add(centerPos);
                    colors.Add(centerColor);

                    // Create 4 triangles using the center vertex
                    // Bottom triangle
                    triangles.Add(bottomLeft);
                    triangles.Add(bottomRight);
                    triangles.Add(centerIndex);

                    // Right triangle
                    triangles.Add(bottomRight);
                    triangles.Add(topRight);
                    triangles.Add(centerIndex);

                    // Top triangle
                    triangles.Add(topRight);
                    triangles.Add(topLeft);
                    triangles.Add(centerIndex);

                    // Left triangle
                    triangles.Add(topLeft);
                    triangles.Add(bottomLeft);
                    triangles.Add(centerIndex);
                }
            }

            Mesh heightmapMesh = new Mesh();
            heightmapMesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            heightmapMesh.vertices = vertices.ToArray();
            heightmapMesh.triangles = triangles.ToArray();
            heightmapMesh.colors = colors.ToArray();
            heightmapMesh.RecalculateNormals();
            heightmapMesh.RecalculateBounds();

            a_displayMethodRoot.AddComponent<MeshFilter>().mesh = heightmapMesh;
            MeshRenderer renderer = a_displayMethodRoot.AddComponent<MeshRenderer>();
            renderer.material = new Material(AssetManager.GetMaterial("HeightMap"));

            return null;
        }

        private Vector3 CalculatePosition(int row, int col, float value, int2 gridSize, float coord00, float coord01, float coord10, float coord11)
        {
            float stepX = (coord10 - coord00) / gridSize.x;
            float stepZ = (coord01 - coord11) / gridSize.y;

            Vector3 pos = ImportedConfigRoot.Instance.ConfigToWorldSpace(new Vector3(coord00 + col * stepX + 0.5f * stepX,
                                                                         0.0f,
                                                                         coord11 + row * stepZ + 0.5f * stepZ));

            pos.y = groundPosY + value * posYScaler; // Raise the cube so that its base is at y=0.

            return pos;
        }
	}
}
